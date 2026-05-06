---
title: ADR-030 Inbox 单条总结 V0：IInboxSummarizer 端口、LLM 直调实现与 POST /api/inbox/items/{id}/summarize 端点
type: adr
subtype: architecture
canonical: true
summary: V0 inbox 总结落地为 Application/Abstractions/Inbox 下的 IInboxSummarizer 端口（按 id 取材料 + 调 ILlmProvider + 返回 Result<InboxItemSummary>），实现放 Application/Services/InboxSummaryAppService 协调 IInboxRepository 与 ILlmProvider；不持久化，每次按需重生；prompt 用固定中文模板（system 1-3 句要点 + user = inbox content），temperature=0.3，max_tokens=200；新错误码 inbox.notFound 映射 HTTP 404；新增 POST /api/inbox/items/{id:guid}/summarize 端点；不引入持久化表 / 缓存层 / 流式 / 多轮上下文 / 自动触发 / 批量端点 / Memory Ledger 写入 / tool calling / 结构化输出。
tags: [agent, engineering, llm]
sources: []
created: 2026-05-03
updated: 2026-05-03
verified_at: 2026-05-03
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/llm-provider-v0-openai-deepseek-abstraction.md, pages/adrs/llm-provider-azure-openai-extension.md, pages/adrs/inbox-v0-capture-and-list-contract.md, pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/options-over-elaboration.md, pages/adrs/important-action-levels-and-confirmation.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-03
adr_revisit_when: "总结质量需要稳定（用户反馈同一条 inbox item 多次拿到差异过大的总结）；renderer 上线 inbox 视图后用户开始抱怨重复调用产生 LLM 费用，需要持久化或缓存；产品扩展到批量 / 自动总结路径（capture 即触发）；要在总结结果上做检索 / tag 派生 / 关键词索引（迫使持久化）；流式响应进入产品（B 轴失效）；总结需要参考用户长期记忆（Memory Ledger 写入）；prompt 需要按 inbox source 分流（chat vs clipboard 用不同 system message）；token 配额 / 成本核算上线（错误码新增 llm.budgetExceeded）；多 user 进入产品（每用户独立的总结历史）。"
---

# ADR-030 Inbox 单条总结 V0：IInboxSummarizer 端口、LLM 直调实现与 POST /api/inbox/items/{id}/summarize 端点

> V0 inbox 总结落地为 Application/Abstractions/Inbox 下的 IInboxSummarizer 端口，实现放 Application/Services 协调 IInboxRepository + ILlmProvider；不持久化、按需重生；中文 1-3 句固定 prompt 模板；新增 POST /api/inbox/items/{id:guid}/summarize 端点。

## 背景

[ADR-014 第一版切片](mvp-first-slice-chat-inbox-read-side.md) 把第一版的 read-side 动作明确写为「总结、分类、打标签、生成候选整理方案」。当前代码只完成了 inbox 写入端（[ADR-026](inbox-v0-capture-and-list-contract.md)）和 LLM 端口（[ADR-028](llm-provider-v0-openai-deepseek-abstraction.md) + [ADR-029](llm-provider-azure-openai-extension.md)），二者尚未对接：`/api/llm/ping` 只是 hard-coded 冒烟，没有真实业务路径走过这条管子。

ADR-014 的四个动作中，「总结」是最窄、最可逆、最贴近 L0 信息型动作（只读，不写库，可重试）的入口：

- **窄**：单条 inbox item 输入，纯 LLM 输出 1-3 句中文，无需 schema-constrained output
- **可逆**：不修改任何状态，重新调用即可得到新结果
- **L0 等级**（[ADR-004 重要动作分级](important-action-levels-and-confirmation.md)）：只读型，无须 user 确认，无回滚成本
- **铺路**：一旦走通，分类 / 候选标签 / 候选整理方案都能复用同一个"取 inbox item → 拼 prompt → 调 ILlmProvider → 映射 Result"的骨架

本 ADR 决定先把这条最短路径打通，确认 ADR-028/029 的端口契约能承载真实业务，并固化 prompt 模式与错误传播形态。

## 决策

### A1：端口位置 = `Application/Abstractions/Inbox/IInboxSummarizer`，实现 = `Application/Services/InboxSummaryAppService`

`IInboxSummarizer` 接口放 `Application/Abstractions/Inbox/`（与 ADR-028 `ILlmProvider` 对称：抽象层放 Application 下，零 Infrastructure 依赖）。具体实现放 `Application/Services/InboxSummaryAppService`，与 `InboxAppService` 平级。

**不与 `IInboxAppService` 合并**：

- ISP（接口隔离）— `IInboxAppService` 是写入 + 列表语义；总结是 read-side 派生，职责不同
- 后续扩展点 — 同一 AppService 可继续加 `SuggestTagsAsync` / `ClassifyAsync` / `DraftCurationOptionsAsync`，而 `IInboxAppService` 保持纯捕获 + 列表
- 失败隔离 — summary 集成测试出问题不影响 capture / list 回归

接口位置选 `Application/Abstractions/Inbox/` 而非 `Application/Interfaces/`：

- `Application/Interfaces/` 按 ADR-022 是 AppService facade（API 层消费），但 `IInboxSummarizer` 是**应用层内部端口**（让 InboxSummaryAppService 自身可单元测试，未来若要替换 LLM 直调为别的实现也方便）
- 但 V0 只有一个实现，IInboxSummaryAppService（facade，放 Interfaces/）即可承担端口角色

**最终决策**：V0 不引入双层（facade + 端口）。直接做 `Application/Interfaces/IInboxSummaryAppService`（facade）+ `Application/Services/InboxSummaryAppService`（实现）。两个类同义，跟现有 `IInboxAppService` / `InboxAppService` 形成对称。

### B1：方法签名

```csharp
Task<Result<InboxItemSummary>> SummarizeAsync(Guid itemId, CancellationToken cancellationToken);
```

**记录类型**：

```csharp
// Application/Inbox/InboxItemSummary.cs
public sealed record InboxItemSummary(
    Guid ItemId,
    string Summary,         // LLM 返回的 1-3 句中文
    string Model,           // 实际使用的模型 ID（来自 LlmCompletion.Model）
    int? PromptTokens,
    int? CompletionTokens,
    TimeSpan Latency
);
```

**不返回 inbox item 本身**：调用方已知 `itemId`；返回内容聚焦"总结相关字段"。

### C1：实现位置不跨进 Infrastructure

`InboxSummaryAppService` 在 Application 层；其依赖（`IInboxRepository` + `ILlmProvider`）都是 Application 已知端口。**不**新建 `Infrastructure/Inbox/LlmInboxSummarizer`：

- 业务逻辑（取 item / 拼 prompt / 错误聚合）属于应用层协调；
- LLM HTTP 调用细节已封装在 `Infrastructure/Llm/` 实现里，AppService 只看到 `ILlmProvider` 端口；
- Infrastructure 不需要任何变更。

### D1：Prompt 策略

- **System message**（中文、固定模板，V0 写死在 AppService 常量）：

  ```
  你是一个信息整理助手。用 1-3 句中文总结用户提供的材料的核心要点。直接返回总结正文，不要前缀（如"总结："）、不要 markdown 标记、不要解释你做了什么。
  ```

- **User message** = `inboxItem.Content`，原样投喂；不附加 source、不附加捕获时间（V0 不让上下文影响总结）。

- **`LlmRequest` 参数**：
  - `Model = null`（让 provider 用各自配置的默认模型）
  - `Temperature = 0.3`（求稳；总结类不需要发散）
  - `MaxTokens = 200`（中文 1-3 句通常 30-90 tokens，留 2-3x 余量；模型若提前结束会自然停）

- **不做 system prompt 注入防护**：V0 user 自己投喂自己的材料，不存在第三方 prompt injection；后续若 inbox 接外部源（邮件 / 网页），再单独加 ADR。

### E1：不持久化

每次调用即时生成；不缓存；不写表；**非幂等**（重新调可能拿到不同总结）。

理由：

1. **窄而可逆**（[ADR-014](mvp-first-slice-chat-inbox-read-side.md)）— V0 的总结质量、prompt 模板、模型选择都会迭代；过早持久化反而妨碍试错。
2. **schema 锁定成本** — 一旦写表，后续改 prompt / 加字段都需要 migration；当前阶段 prompt 形态尚未稳定。
3. **L0 动作零成本** — 总结调用是只读，重试无副作用；用户重新点"总结"按钮拿新结果是可接受 UX。
4. **持久化推迟到 ADR-031+** — 当出现持久化驱动信号（renderer 抱怨费用 / 检索需求 / Memory Ledger 写入）时，再开新 ADR 引入 `inbox_item_summary` 表 + 缓存语义。

### F1：错误模型

复用 [ADR-028 §H1](llm-provider-v0-openai-deepseek-abstraction.md) 错误映射表，并新增一条 inbox 专属错误：

| 场景 | 错误码 | HTTP 状态 |
| --- | --- | --- |
| inbox item 不存在 | `inbox.notFound` | 404 |
| LLM 401/403 | `llm.authenticationFailed` | 401 |
| LLM 429 | `llm.rateLimited` | 429 |
| LLM 408/5xx | `llm.upstreamUnavailable` | 502 |
| LLM 其它 4xx | `llm.invalidRequest` | 400 |
| LLM HttpRequestException | `llm.upstreamUnavailable` | 502 |
| `OperationCanceledException` | 透传，不映射 | — |

新增静态类 `Application/Inbox/InboxErrors`：

```csharp
public static class InboxErrors
{
    public const string ItemNotFoundCode = "inbox.notFound";

    public static DomainError ItemNotFound(Guid itemId) =>
        new(Code: ItemNotFoundCode, Message: $"Inbox item '{itemId}' not found.", Field: null);
}
```

### G1：API 端点

```
POST /api/inbox/items/{id:guid}/summarize
```

- **路径参数** `id` = inbox item UUIDv7（route constraint `:guid` 让格式错误自然 404）
- **请求体** = 空（V0 没有可调参数；temperature / max_tokens 由 AppService 固定）
- **成功响应**（200）：

  ```json
  {
    "itemId": "01976e08-...",
    "summary": "用户分享了一篇关于…，重点是…，并提出 3 个问题。",
    "model": "gpt-4.1-2025-04-14",
    "promptTokens": 156,
    "completionTokens": 42,
    "durationMs": 1247
  }
  ```

- **错误响应**：走 `Result<T>.ToHttpResult()` → ProblemDetails，extensions 含 `code` 字段（与 ADR-028 §G1 / `/api/llm/ping` 失败响应同形态）

**HTTP 动词选 POST**：

- 非幂等（同一 id 多次调用结果可能不同）
- 有副作用（产生 LLM token 计费）
- 这是动作语义，不是资源查询；GET 不合适

### H1：DI 注册

`Application` 项目的 `AddApplication()` 扩展中追加：

```csharp
services.AddScoped<IInboxSummaryAppService, InboxSummaryAppService>();
```

**Scoped** 而非 Singleton：与 `IInboxAppService` 一致（`IInboxRepository` 是 Scoped，会被注入）。

### I1：测试覆盖

V0 必须验证：

1. **单元测试**（`Application.Tests/Services/InboxSummaryAppServiceTests`，mock `IInboxRepository` + `ILlmProvider`）：
   - `SummarizeAsync_ReturnsSuccess_WhenLlmCompletes`
   - `SummarizeAsync_ReturnsInboxNotFound_WhenItemMissing`
   - `SummarizeAsync_PropagatesLlmAuthenticationFailedError`
   - `SummarizeAsync_PropagatesLlmRateLimitedError`
   - `SummarizeAsync_PropagatesLlmUpstreamError`
   - `SummarizeAsync_PassesContentAsUserMessage`（验证 prompt 拼装：system 模板固定 + user message 等于 inbox content）
   - `SummarizeAsync_PropagatesCancellation`

2. **API 端点测试**（`Api.Tests/Endpoints/InboxSummarizeEndpointTests`，`WebApplicationFactory<Program>` + mock `IInboxSummaryAppService`）：
   - 200 success path
   - 404 inbox.notFound
   - 502 llm.upstreamUnavailable
   - 401 llm.authenticationFailed

3. **架构测试**：现有 `LayeringTests` 与 `Application_InterfacesFolder_OnlyContainsInterfaces` 等无需调整（新接口落在 `Application.Interfaces`，新实现落在 `Application.Services`，符合既有规则）。

## 实施概要

1. **Application 层（新增）**：
   - `Application/Interfaces/IInboxSummaryAppService.cs`
   - `Application/Inbox/InboxItemSummary.cs`
   - `Application/Inbox/InboxErrors.cs`
   - `Application/Services/InboxSummaryAppService.cs`（依赖 `IInboxRepository` + `ILlmProvider`）
   - `Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`（追加 `AddScoped<IInboxSummaryAppService, InboxSummaryAppService>`）

2. **Api 层（新增）**：
   - `Api/Endpoints/Inbox/InboxSummarizeEndpoints.cs`（注册 `POST /api/inbox/items/{id:guid}/summarize`），或扩展现有 `InboxEndpoints.MapInboxEndpoints` 增一个 sub-route
   - `Api/Program.cs` 不变（端点已通过 `MapInboxEndpoints` 总入口挂载）

3. **测试（新增）**：
   - `Application.Tests/Services/InboxSummaryAppServiceTests.cs`（7 个测试，mock 模式同现有 `InboxAppServiceTests`）
   - `Api.Tests/Endpoints/InboxSummarizeEndpointTests.cs`（4 个测试，模式同现有 `LlmPingEndpointTests` if exists；否则参考现有 inbox endpoint tests）

4. **构建 + 测试 + 提交链**：
   - `dotnet build` 0/0 ✅
   - `dotnet test` 全绿 ✅（预计 +11 测试 → 195 总）
   - `feat(api+app)` scope 一次提交（ADR-030 文档单独 docs(adr) 提交）
   - 实测：用 `curl` 调真实 Azure OpenAI 验证非冒烟路径打通

## 不在本 ADR 范围内

- **持久化** — 不写 `inbox_item_summary` 表；不引入缓存层；推迟到 ADR-031+。
- **流式响应** — 用 `ILlmProvider.CompleteAsync` 的非流式接口（ADR-028 §B1 已锁定 V0 非流式）。
- **多轮上下文 / 历史** — 总结只看单条 inbox content，不参考其它 inbox item、不参考 chat 历史、不写 Memory Ledger。
- **自动触发** — capture inbox item 时**不**自动调用总结；user 必须显式发起。
- **批量端点** — 不做 `/api/inbox/items/summarize?ids=...`；一次一条。
- **总结质量度量** — 不做评分、不做 A/B、不收集用户反馈写表。
- **prompt 多语言 / 多风格** — 中文模板固定；不按 user 语言 / 偏好分流。
- **可调参数** — 请求体为空；user 不能调 temperature / max_tokens / 风格指令。
- **tool calling / structured output** — V0 用纯文本输出；不让 LLM 调工具、不强制 schema。
- **renderer UI** — 端点上线即可；UI 集成（"总结"按钮）走另一份 ADR。
- **Memory Ledger 联动** — 总结结果不写记忆；不做"记住这条总结"。

## 折衷与风险

- **不持久化的代价** — 同一条 inbox item 反复调用每次都付费（且耗时秒级）。MVP 单用户可接受；renderer 上线后若用户每次刷新页面都触发，会很快出现费用与延迟抱怨——届时需要 ADR-031 引入 `inbox_item_summary` 表 + 客户端侧缓存策略。判断信号：用户在 dogfood 期内手工抱怨。
- **prompt 模板硬编 vs 配置化** — V0 把 system message 写死在 AppService 常量，调整需要改代码 + 重启。当 prompt 进入第二轮迭代时（用户反馈"总结太长"/"太抽象"），应考虑把模板挪到 `appsettings.Llm:Prompts:InboxSummary`；但 V0 配置化会引入额外 IOptions 注入复杂度，得不偿失。
- **Temperature = 0.3 的稳定性** — 不是 0，也不是 1；选这个是经验值。如果用户反馈"同一条材料每次总结差很多"，先调到 0.1；如果反馈"总结太死板"，再升到 0.5。
- **错误码命名空间冲突** — `inbox.notFound` 是新前缀；要保证 API 层 `ResultHttpExtensions.ToHttpResult` 能把它映射到 404 而非默认的 422（**实施时要专门看一下**）。可能需要扩展 ResultHttpExtensions 识别 `inbox.notFound` → 404；或在 endpoint 内手工拆 Result 走 `TypedResults.NotFound(...)` 路径（与 `/api/llm/ping` 处理 LLM 错误一致）。建议后者，避免污染 ResultHttpExtensions。
- **InboxItem.Content 长度上限 4096** — `Content` 域规则已限到 4096 UTF-16 单元（约 1500-2000 中文字），单次 LLM 调用 prompt tokens 在 1500-3000 范围，对当前模型（gpt-4.1）安全。若未来 inbox 放宽长度上限（接长文档），需要在 ADR-031+ 引入"分段总结 / 截断策略"。
- **依赖 ILlmProvider 单 active provider** — ADR-028 §E1 锁定单一 active provider；总结调用不能选择 provider。如果未来出现"总结用 deepseek 成本最低、对话用 gpt-4.1"的产品需求，需要 ADR-031+ 改 ILlmProvider 注册形态（按场景多 provider）。
- **route constraint `:guid` 的容错** — 路径参数若不是合法 GUID 格式，ASP.NET 会直接返回 404 而不进 endpoint；这与"item 不存在"的 404 在 client 端表现一致，但 traceId / problem detail 不同。可接受；不在 V0 修复。

## 相关页面

- [ADR-028 LLM Provider V0](llm-provider-v0-openai-deepseek-abstraction.md)：定义 `ILlmProvider` 端口与错误映射表；本 ADR 透传 §H1 错误码。
- [ADR-029 Azure OpenAI 扩展](llm-provider-azure-openai-extension.md)：当 ActiveProvider=AzureOpenAI 时，本 ADR 自动用 Azure 后端（无需改动）。
- [ADR-026 Inbox V0 数据契约](inbox-v0-capture-and-list-contract.md)：定义 `InboxItem` 聚合与 `IInboxRepository`；本 ADR 通过仓储取材料。
- [ADR-014 第一版切片](mvp-first-slice-chat-inbox-read-side.md)：本 ADR 兑现"总结"动作。
- [ADR-023 Api 入口立面](api-entry-facade-and-v0-endpoints.md)：本 ADR 端点遵循其形态约定（`MapGroup` + `ToHttpResult` 或手工映射）。
- [ADR-004 重要动作分级](important-action-levels-and-confirmation.md)：本 ADR 总结动作属 L0，无须 user 确认。
- [ADR-002 选择题优先](options-over-elaboration.md)：总结产物可作为后续候选生成的输入；本 ADR 是其前置。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 即"方案先行"。
