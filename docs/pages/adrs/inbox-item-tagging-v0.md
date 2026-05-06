---
title: ADR-031 Inbox 单条打标签 V0：IInboxTaggingAppService 端口、JSON 数组结构化输出与 POST /api/inbox/items/{id}/tags 端点
type: adr
subtype: architecture
canonical: true
summary: V0 inbox 打标签沿用 ADR-030 骨架（IInboxRepository + ILlmProvider 协调、不持久化、按需重生）但输出从自由文本升级为 JSON 数组（1-5 个开放集中文标签，2-12 字符／标签）；新增 IInboxTaggingAppService facade 与 InboxTaggingAppService 实现，prompt 用 system 强约束 JSON 模板（temperature=0.2，max_tokens=120），AppService 自行解析与正规化（trim / 去重 / 长度截断 / 数量截断到 5）；解析失败映射新错误码 inbox.taggingParseFailed → HTTP 422；新增 POST /api/inbox/items/{id:guid}/tags 端点；不引入持久化表 / 受控 tag 词表 / 标签层级 / 自动触发 / 批量端点 / Memory Ledger 写入 / structured output API（response_format=json_schema）/ tool calling。
tags: [agent, engineering, llm]
sources: []
created: 2026-05-05
updated: 2026-05-05
verified_at: 2026-05-05
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/inbox-item-summarize-v0.md, pages/adrs/llm-provider-v0-openai-deepseek-abstraction.md, pages/adrs/llm-provider-azure-openai-extension.md, pages/adrs/inbox-v0-capture-and-list-contract.md, pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/options-over-elaboration.md, pages/adrs/important-action-levels-and-confirmation.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-05
adr_revisit_when: "标签质量需要稳定（同一条 inbox item 多次拿到差异过大的标签集）；renderer 上线 inbox tag 视图后用户开始抱怨重复调用产生 LLM 费用，需要持久化或缓存；产品需要在标签上做检索 / 聚类 / 过滤（迫使持久化与索引）；用户开始抱怨标签语义重叠或过于宽泛（需要受控 tag 词表或聚类合并）；要让一组 inbox item 共享标签（迫使跨 item 上下文）；要把标签和 Memory Ledger 兴趣画像耦合（ADR-013 兴趣权重需要标签命中信号）；structured output（response_format=json_schema）在主流 provider 中稳定后，把字符串解析路径换成原生 schema 校验；tag 进入产品核心视图（迫使统一 ID / 同义词归一 / 多语支持）。"
---

# ADR-031 Inbox 单条打标签 V0：IInboxTaggingAppService 端口、JSON 数组结构化输出与 POST /api/inbox/items/{id}/tags 端点

> V0 inbox 打标签沿用 ADR-030 骨架但输出 JSON 数组（1-5 个开放集中文标签）；新增 `IInboxTaggingAppService` facade，AppService 自行解析与正规化；新错误码 `inbox.taggingParseFailed` 映射 HTTP 422；新增 `POST /api/inbox/items/{id:guid}/tags` 端点。

## 背景

[ADR-014 第一版切片](mvp-first-slice-chat-inbox-read-side.md) 把第一版 read-side 动作明确写为「总结、分类、打标签、生成候选整理方案」。[ADR-030](inbox-item-summarize-v0.md) 已经把"总结"通过 `IInboxSummaryAppService` 落地，验证了"取 inbox item → 拼 prompt → 调 ILlmProvider → 映射 Result"的骨架可以承载真实业务。

四个 read-side 动作中，下一个最窄、最可逆、最贴近 ADR-014 的入口是**打标签**（tag）：

- **比分类更窄** — 分类需要预先定义类目体系（封闭集），标签是开放集（LLM 自由生成中文短词），V0 不引入受控词表
- **比候选整理方案更可逆** — 整理方案隐含写动作建议（移到哪里、归到哪个文件夹），即使是只读的"建议"也需要更长的 prompt 与更复杂的输出 schema；标签纯粹是元数据
- **是分类与候选整理的前置铺垫** — 标签生成的"开放集 → 收敛"路径会暴露聚合 / 去重 / 同义词等真实问题，为后续封闭分类与候选生成提供经验
- **首次走结构化输出** — ADR-030 是自由文本，本 ADR 第一次让 LLM 输出 JSON；这条路径打通后，分类 / 候选整理都能复用同一个"system prompt 强约束 → AppService 解析 → 错误回退"模式

本 ADR 决定先把"打标签"按 ADR-030 同等深度走完，确认 ADR-028/029/030 的端口契约能承载结构化输出，并把"LLM 解析失败"作为新错误码引入。

## 备选方案

**接口位置 / 实现位置**：

- 方案 A1：`Application/Interfaces/IInboxTaggingAppService` + `Application/Services/InboxTaggingAppService`，与 ADR-030 的 `IInboxSummaryAppService / InboxSummaryAppService` 完全对称（独立 AppService）。
- 方案 A2：把 `SuggestTagsAsync` 加到现有 `IInboxSummaryAppService`，复用 LLM 调用骨架。
- 方案 A3：抽出共用基类 `InboxLlmAppServiceBase`，让 Summary / Tagging 都继承。

**输出形态 / Prompt 策略**：

- 方案 B1：LLM 输出 JSON 数组（`["人工智能", "学习方法"]`），AppService 用 `JsonSerializer.Deserialize<string[]>` 解析。
- 方案 B2：LLM 输出逗号分隔字符串（`人工智能, 学习方法`），AppService 用 `Split(',')` + `Trim()` 解析。
- 方案 B3：LLM 输出 Markdown bullet 列表，AppService 用正则提取。
- 方案 B4：用 OpenAI structured output API（`response_format={"type":"json_schema", ...}`）让模型层面强制 schema。

**LLM 输出失败处理**：

- 方案 C1：解析失败 → 新错误码 `inbox.taggingParseFailed` → HTTP 422。
- 方案 C2：解析失败 → 重试 1 次（带"上次返回不是合法 JSON"的修正提示）。
- 方案 C3：解析失败 → fallback 到正则启发式从原文提取候选词。

**Tag 数量约束**：

- 方案 D1：硬约束 1-5 个，超界报错。
- 方案 D2：硬约束 1-5 个，>5 截断到前 5，<1 报错。
- 方案 D3：不约束数量，全靠 prompt 引导。

**API 端点形态**：

- 方案 E1：`POST /api/inbox/items/{id:guid}/tags`（与 `/summarize` 平级）。
- 方案 E2：`POST /api/inbox/items/{id:guid}/derive`，请求体 `{ "kinds": ["summary", "tags"] }` 一次返回多种派生数据。
- 方案 E3：`GET /api/inbox/items/{id:guid}/tags`（按"查询"语义）。

**持久化策略**：

- 方案 F1：不持久化（与 ADR-030 §决策 E1 完全一致），每次按需重生。
- 方案 F2：把标签写回 `inbox_item` 表的新列（schema migration v3）。
- 方案 F3：新建 `inbox_item_tag` 关联表，支持后续聚合查询。

## 被否决方案与理由

**方案 A2（合并到 IInboxSummaryAppService）**：

- 违反 ISP — 总结与打标签是不同语义动作；调用方只需要标签时不应携带"summarize"心智
- 失败隔离弱 — Tagging 集成测试 / prompt 调整会污染 Summary 回归
- 命名不再对称 — `IInboxSummaryAppService.SuggestTagsAsync` 听起来像 summary 的副产物，掩盖了二者的独立性

**方案 A3（抽共用基类）**：

- V0 阶段只有 2 个 AppService 共享 `IInboxRepository + ILlmProvider` 注入与"取 item / 调 LLM / 映射 Result"骨架，**第二次原则尚未触发**（[ADR-030 §决策 A1](inbox-item-summarize-v0.md) 同款判断）
- 抽基类会引入"模板方法 / 钩子方法"心智，降低可读性
- 待第三个同类 AppService（如分类）出现时再抽，而非现在

**方案 B2（逗号分隔字符串）**：

- 中文标签内含逗号是常见情况（如"机器学习, 深度学习"作为一个标签），split 会误切
- LLM 实际输出更易抖动（中文逗号 / 英文逗号 / 顿号 / 回车混用），解析鲁棒性弱
- 与未来 structured output（B4）路径不兼容

**方案 B3（Markdown bullet 列表）**：

- 解析需要正则，对 LLM 输出格式波动敏感（缩进 / `- ` vs `* ` vs `1. `）
- 比 JSON 更"自由"，违背"V0 第一次结构化输出要选最稳的形态"

**方案 B4（structured output API）**：

- 跨 provider 不一致：OpenAI 已支持 `response_format={"type":"json_schema", ...}`，DeepSeek 当前文档声明仅支持 `{"type":"json_object"}`（无 schema 约束），Azure OpenAI 取决于部署的 API version
- ADR-028 §B1 锁定的 `LlmRequest` 当前不含 `ResponseFormat` 字段，引入会触发 `ILlmProvider` 抽象扩展（破坏性 V0 简化原则）
- 方案 B1 用纯 system prompt 约束 + JSON 解析，在 GPT-4.1 / DeepSeek-V3 / qwen2.5 等主流模型上稳定性已经足够（实测无 schema 也极少出错）
- structured output 在 `adr_revisit_when` 列出，待主流 provider 行为收敛后再升级

**方案 C2（解析失败重试）**：

- 第二次仍可能失败，重试是有限次数赌博，不解决根本问题
- 重试翻倍 LLM 费用与延迟，对"L0 只读动作"成本失衡
- V0 的 LLM 在 GPT-4.1 / DeepSeek 上 JSON 输出失败率经验值 < 1%，不值得引入重试机制

**方案 C3（fallback 正则提取）**：

- 启发式提取的标签质量远低于 LLM 输出，会"成功但产物烂"
- 让客户端无法分辨"高质量 LLM 标签"与"降级正则标签"，体验更差
- 报错让用户重试比静默降级诚实

**方案 D1（硬约束 1-5，>5 报错）**：

- LLM 偶尔会返回 6-7 个标签是常态，把这个当错误是把"产物可用"映射为"失败"
- 截断到前 5 是无副作用的清理，比报错更宽容

**方案 D3（不约束数量）**：

- LLM 自由发挥可能给出 12+ 个标签，每个都很弱（"信息"、"内容"、"文本"这种通用词）
- 没有数量约束就没有"优先级"信号，标签变成噪声列表

**方案 E2（统一 derive 端点）**：

- "一次拿多种派生"在 V0 没有真实场景（renderer 当前是按钮触发单一动作）
- 多 kind 端点要处理"部分成功"语义（summary OK, tags failed → 整体 200 还是 207？），引入复杂度
- 跨 kind 复用 prompt 是过早抽象；每个 kind 的 prompt 模板会独立演化

**方案 E3（GET 端点）**：

- 非幂等（每次调用结果可能不同）
- 有副作用（产生 LLM token 计费）
- GET 语义违反 RFC 9110 §9.2.1 「safe method」要求
- 客户端 / 代理可能基于 GET 缓存（401/Cache-Control 规则不能完全覆盖），导致用户重试拿到旧结果

**方案 F2 / F3（持久化）**：

- 与 ADR-030 §决策 E1 同款判断 — V0 prompt / 数量上限 / 解析逻辑都会迭代，过早 schema lock 反而妨碍试错
- 持久化驱动信号尚未出现（renderer 抱怨费用 / 检索需求 / 跨 item 聚合）
- F3 还引入 N:M 关联表，比 ADR-030 缓存路径复杂一个数量级，应推迟到独立 ADR（ADR-032+）

## 决策

### A1：Facade + 独立 AppService（与 ADR-030 完全对称）

`Application/Interfaces/IInboxTaggingAppService` 声明 facade；`Application/Services/InboxTaggingAppService` 是唯一实现，依赖 `IInboxRepository` + `ILlmProvider`。**不**与 `IInboxSummaryAppService` 合并；**不**抽共用基类。

```csharp
// Application/Interfaces/IInboxTaggingAppService.cs
public interface IInboxTaggingAppService
{
    Task<Result<InboxItemTags>> SuggestTagsAsync(
        Guid itemId,
        CancellationToken cancellationToken
    );
}
```

### B1：返回记录形态

```csharp
// Application/Inbox/InboxItemTags.cs
public sealed record InboxItemTags(
    Guid ItemId,
    IReadOnlyList<string> Tags,    // 1-5 个开放集中文标签
    string Model,                   // 实际使用的模型 ID（来自 LlmCompletion.Model）
    int? PromptTokens,
    int? CompletionTokens,
    TimeSpan Latency
);
```

**字段说明**：

- `Tags` 用 `IReadOnlyList<string>` 而非 `string[]`：跨语言序列化时 JSON 表现一致，但调用方无法通过反射写入。
- 不返回 inbox item 本身：调用方已知 `itemId`，返回内容聚焦"标签相关字段"（与 ADR-030 §决策 B1 同款判断）。

### C1：实现位置不跨进 Infrastructure

`InboxTaggingAppService` 在 Application 层；其依赖（`IInboxRepository` + `ILlmProvider`）都是 Application 已知端口。**不**新建 `Infrastructure/Inbox/LlmInboxTagger`（与 ADR-030 §决策 C1 同款）。

### D1：Prompt 策略

- **System message**（中文，固定模板，V0 写死在 AppService 常量）：

  ```
  你是一个信息整理助手。读取用户提供的材料，输出 1-5 个用于归类的中文短标签。
  严格遵循以下规则：
  1. 只返回一个 JSON 数组，不要任何前后缀文字、不要 markdown 代码块标记。
  2. 数组每个元素是 2-12 字符的中文短词或短词组，不含空格、不含标点。
  3. 标签之间语义不重叠（"机器学习"和"深度学习"任选其一，不同时给）。
  4. 优先具体名词或主题词，避免"信息"、"内容"、"文本"这类无信息量的通用词。
  示例输出：["人工智能", "学习方法", "效率工具"]
  ```

- **User message** = `inboxItem.Content`，原样投喂；不附加 source、不附加捕获时间（与 ADR-030 §决策 D1 同款）。

- **`LlmRequest` 参数**：
  - `Model = null`（让 provider 用各自配置的默认模型）
  - `Temperature = 0.2`（比 ADR-030 的 0.3 更低；标签需要更稳定，跑两次差距越小越好）
  - `MaxTokens = 120`（5 个 12 字符标签 ≈ 60 中文字 ≈ 90-120 tokens，留余量）

### D2：解析与正规化

AppService 收到 LLM 返回的纯文本后按以下流程处理：

1. **trim 外层空白与代码块标记**：去除 `\u200b`、`\r`、首尾 ` ```json` / ` ``` ` 等常见噪声（即使 system prompt 已要求不加，仍要兜底容错）。
2. **`JsonSerializer.Deserialize<string[]>`**：失败 → 返回 `inbox.taggingParseFailed`。
3. **逐元素清理**：`Trim()` → 去掉零宽字符 → 内部多空格折叠为单空格 → 全角逗号 / 顿号 / 句号视为分隔（罕见 LLM 错误把一个标签写成 `"a，b"` 这种）→ 拆分后展平。
4. **过滤**：丢弃空字符串 / 全空白 / 长度 > 12 字符的元素。
5. **去重**：保留首次出现顺序；忽略大小写差异（标准化为原大小写存储）。
6. **数量截断**：保留前 5 个；若清理后剩 0 个 → 返回 `inbox.taggingParseFailed`。

**为什么不在 prompt 里报错而在 AppService 兜底**：LLM 不可信，AppService 是最后一道防线。`taggingParseFailed` 只在"清理后仍 0 个标签"时返回，宽容度高。

### E1：不持久化

每次调用即时生成；不缓存；不写表；**非幂等**（重新调可能拿到不同标签）。理由与 ADR-030 §决策 E1 一致：

1. **窄而可逆** — V0 标签质量、prompt 模板、模型选择都会迭代；过早持久化反而妨碍试错。
2. **schema 锁定成本** — N:M 关联表（如 `inbox_item_tag`）改 prompt / 加字段需要 migration；当前阶段 prompt 形态尚未稳定。
3. **L0 动作零成本** — 标签生成是只读，重试无副作用；用户重新点"标签"按钮拿新结果是可接受 UX。
4. **持久化推迟到 ADR-032+** — 出现持久化驱动信号（renderer 抱怨费用 / 检索需求 / 跨 item 聚合）时再开新 ADR。

### F1：错误模型

复用 [ADR-028 §H1](llm-provider-v0-openai-deepseek-abstraction.md) 与 [ADR-030 §决策 F1](inbox-item-summarize-v0.md) 的错误码体系，**新增一条**：

| 场景 | 错误码 | HTTP 状态 |
| --- | --- | --- |
| inbox item 不存在 | `inbox.notFound` | 404（与 ADR-030 共用） |
| **LLM 返回内容无法解析为有效标签数组** | **`inbox.taggingParseFailed`** | **422** |
| LLM 401/403 | `llm.authenticationFailed` | 401 |
| LLM 429 | `llm.rateLimited` | 429 |
| LLM 408/5xx | `llm.upstreamUnavailable` | 502 |
| LLM 其它 4xx | `llm.invalidRequest` | 400 |
| LLM HttpRequestException | `llm.upstreamUnavailable` | 502 |
| `OperationCanceledException` | 透传，不映射 | — |

`InboxErrors` 静态类追加：

```csharp
public static class InboxErrors
{
    public const string ItemNotFoundCode = "inbox.notFound";
    public const string TaggingParseFailedCode = "inbox.taggingParseFailed";

    public static DomainError ItemNotFound(Guid itemId) =>
        new(Code: ItemNotFoundCode, Message: $"Inbox item '{itemId}' not found.", Field: null);

    public static DomainError TaggingParseFailed(string detail) =>
        new(Code: TaggingParseFailedCode, Message: $"Failed to parse tags from LLM output: {detail}", Field: null);
}
```

**为什么 422 而非 502**：解析失败本质是"上游产生的内容不符合预期 schema"，更接近 RFC 9110 §15.5.21 的 `Unprocessable Content`（语义错误）而非 `Bad Gateway`（连接错误）。客户端可以基于 422 决定"提示用户重试"，基于 502 决定"提示后端临时不可用"——二者用户文案不同。

### G1：API 端点

```
POST /api/inbox/items/{id:guid}/tags
```

- **路径参数** `id` = inbox item UUIDv7（route constraint `:guid` 让格式错误自然 404）
- **请求体** = 空（V0 没有可调参数）
- **成功响应**（200）：

  ```json
  {
    "itemId": "01976e08-...",
    "tags": ["人工智能", "学习方法", "效率工具"],
    "model": "gpt-4.1-2025-04-14",
    "promptTokens": 156,
    "completionTokens": 28,
    "durationMs": 980
  }
  ```

- **错误响应**：与 ADR-030 §G1 同形态——ProblemDetails，extensions 含 `code` 字段。`inbox.taggingParseFailed` 走 `TypedResults.UnprocessableEntity(...)` 路径（与 `/summarize` 处理 `inbox.notFound` 的手工映射方式一致，避免污染 `ResultHttpExtensions`）。

**HTTP 动词选 POST**（同 ADR-030 §决策 G1）：

- 非幂等
- 有副作用（产生 LLM token 计费）
- 是动作语义，不是资源查询

### H1：DI 注册

沿用 `Application` 项目的 `AddApplication()` 反射扫描——`InboxTaggingAppService` 命名匹配 `IXxxAppService` / `XxxAppService` 模式，自动 Scoped 注册，**不需要手工 `AddScoped` 行**（与 ADR-030 §决策 H1 同款）。

### I1：测试覆盖

V0 必须验证：

1. **单元测试**（`Application.Tests/Services/InboxTaggingAppServiceTests`，mock `IInboxRepository` + `ILlmProvider`）：
   - `SuggestTagsAsync_ReturnsSuccess_WhenLlmReturnsValidJson`
   - `SuggestTagsAsync_ReturnsInboxNotFound_WhenItemMissing`
   - `SuggestTagsAsync_PropagatesLlmAuthenticationFailedError`
   - `SuggestTagsAsync_PropagatesLlmRateLimitedError`
   - `SuggestTagsAsync_PropagatesLlmUpstreamError`
   - `SuggestTagsAsync_PassesContentAsUserMessage`（验证 prompt 拼装：system 模板固定 + user message 等于 inbox content）
   - `SuggestTagsAsync_TrimsCodeBlockMarkers`（输入 `` ```json\n["a"]\n``` `` → 解析成功）
   - `SuggestTagsAsync_TruncatesToFiveTags`（LLM 返回 7 个 → 取前 5）
   - `SuggestTagsAsync_DeduplicatesTags`（LLM 返回 `["a", "a", "b"]` → `["a", "b"]`）
   - `SuggestTagsAsync_ReturnsTaggingParseFailed_WhenJsonInvalid`
   - `SuggestTagsAsync_ReturnsTaggingParseFailed_WhenAllTagsFiltered`
   - `SuggestTagsAsync_PropagatesCancellation`

2. **API 端点测试**（`Api.Tests/Endpoints/InboxTaggingEndpointTests`，`WebApplicationFactory<Program>` + mock `IInboxTaggingAppService`）：
   - 200 success path
   - 404 inbox.notFound
   - 422 inbox.taggingParseFailed
   - 502 llm.upstreamUnavailable
   - 401 llm.authenticationFailed

3. **架构测试**：现有 `LayeringTests` 与 `Application_InterfacesFolder_OnlyContainsInterfaces` 等无需调整（新接口落在 `Application.Interfaces`，新实现落在 `Application.Services`，符合既有规则；与 ADR-030 同款）。

## 实施概要

1. **Application 层（新增）**：
   - `Application/Interfaces/IInboxTaggingAppService.cs`
   - `Application/Inbox/InboxItemTags.cs`
   - `Application/Inbox/InboxErrors.cs`（追加 `TaggingParseFailedCode` 常量与 `TaggingParseFailed` 工厂方法；不新增文件）
   - `Application/Services/InboxTaggingAppService.cs`（依赖 `IInboxRepository` + `ILlmProvider`）
   - `Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs` 不变（命名约定自动注册）

2. **Api 层（新增）**：
   - `Api/Endpoints/Inbox/InboxTaggingEndpoints.cs`（注册 `POST /api/inbox/items/{id:guid}/tags`），或扩展现有 `InboxEndpoints.MapInboxEndpoints` 增一个 sub-route——按当前 `/summarize` 在 `InboxEndpoints` 内还是独立文件决定，保持一致即可
   - `Api/Program.cs` 不变

3. **测试（新增）**：
   - `Application.Tests/Services/InboxTaggingAppServiceTests.cs`（12 个测试）
   - `Api.Tests/Endpoints/InboxTaggingEndpointTests.cs`（5 个测试）

4. **构建 + 测试 + 提交链**：
   - `dotnet build` 0/0 ✅
   - `dotnet test` 全绿 ✅（预计 +17 测试 → 213 总）
   - `feat(api+app)` scope 一次提交（ADR-031 文档单独 docs(adr) 提交）
   - 实测：用 `curl` 调真实 OpenAI / Azure / DeepSeek 验证 JSON 输出在三家 provider 上稳定

5. **renderer UI（同 commit 或紧随其后）**：
   - `apps/desktop/src/preload.ts` 新增 `inbox.suggestTags(itemId)` 桥
   - `apps/desktop/src/main.ts` 新增 `agentos:inbox:suggestTags` IPC handler
   - `apps/desktop/src/renderer/index.html` inbox 列表每项追加"标签"按钮，点击后调用桥 → 显示标签徽章
   - 与 ADR-030 §G1 UI 落地路径一致（"端点上线即可，UI 走同 commit 或紧随其后的 commit"）

## 不在本 ADR 范围内

- **持久化** — 不写 `inbox_item_tag` 表；不引入缓存层；不在 `inbox_item` 表加 `tags` 列；推迟到 ADR-032+。
- **受控 tag 词表** — V0 是开放集，每次 LLM 自由生成；不引入"已存在标签优先复用"逻辑；不引入 tag 同义词归一。
- **标签层级 / 父子关系** — 标签是扁平 string 数组，没有 parent / category。
- **Memory Ledger 联动** — 标签结果不写记忆；ADR-013 兴趣画像权重的"标签命中信号"路径走另一份 ADR。
- **structured output API** — 不用 `response_format={"type":"json_schema", ...}`；用 system prompt 强约束 + AppService 解析（决策 §D1 / §D2）。
- **tool calling** — 不让 LLM 调工具拿到候选标签集后选择；纯文本生成。
- **流式响应** — 与 ADR-030 §B1 一致，用非流式 `ILlmProvider.CompleteAsync`。
- **多轮上下文** — 标签只看单条 inbox content，不参考其它 inbox item、不参考 chat 历史。
- **自动触发** — capture inbox item 时**不**自动调用打标签；user 必须显式发起。
- **批量端点** — 不做 `/api/inbox/items/tags?ids=...`；一次一条。
- **可调参数** — 请求体为空；user 不能调 temperature / max_tokens / 期望标签数量。
- **多语言标签** — 用户投喂英文材料时 prompt 仍要求中文标签输出；多语言策略走另一份 ADR。

## 影响

**正向影响**：

- ADR-014 第二个 read-side 动作落地，距离 MVP 第一版闭环更近一步。
- 首次走通"system prompt 强约束 + AppService 解析"路径，分类 / 候选整理可复用同一模式。
- 新错误码 `inbox.taggingParseFailed` 把"LLM 输出 schema 错误"作为一类独立错误，让客户端可以基于状态码做不同响应（重试 vs 报错）。
- `InboxErrors` 累积成"inbox 域错误码字典"，未来 inbox.* 错误有统一着陆点。
- 单元测试中"trim 代码块标记 / 去重 / 截断到 5 / 解析失败"四个变种把"AppService 防御性数据清理"作为可复用骨架沉淀下来。
- 与 ADR-030 完全对称的 facade + service + endpoint + test 结构降低了 review 成本与认知负担。

**代价 / 风险**：

- **prompt 模板硬编 vs 配置化** — 与 ADR-030 §折衷与风险 同款判断；V0 写死，配置化推迟。
- **Temperature = 0.2 的稳定性** — 标签需要更稳；如果用户反馈"同一条材料每次标签差很多"，先调到 0.1。
- **JSON 解析失败率** — GPT-4.1 / DeepSeek-V3 / qwen2.5 上经验 < 1%，但极端材料（含大量代码 / JSON 片段本身）可能让 LLM 误把材料当成模板回声。监控信号：API 422 比例。
- **开放集标签的语义重叠** — 同一条 item 第一次标"AI"、第二次标"人工智能"是常见情况；V0 不解决，等持久化后再做同义词归一。
- **依赖 ILlmProvider 单 active provider** — 与 ADR-030 §折衷与风险 同款；不能选用便宜 provider 专跑标签。
- **route constraint `:guid` 容错** — 与 ADR-030 同款；非 GUID 路径直接 404。
- **AppService 里手写 JSON 解析与正规化** — V0 是 ~30 行简单逻辑，但出 bug 的可能性非零；通过 12 个针对性单元测试覆盖。
- **422 状态码与现有错误体系冲突** — 当前 `Result<T>.ToHttpResult` 默认把 `Failure` 映射到 422；这次 `inbox.taggingParseFailed` 也是 422，但路径必须在 endpoint 内手工 switch（参考 `/summarize` 处理 `inbox.notFound` 的形态），不要污染 `ResultHttpExtensions`。**实施时要专门看一下** `InboxEndpoints` 当前的错误映射代码，沿用同一形态。

## 复议触发条件

- **持久化触发** — 出现以下任一信号即开 ADR-032+：
  - renderer dogfood 期内用户手工抱怨"每次刷新都重新生成标签太慢 / 太贵"
  - 产品需要"按标签筛选 inbox 列表"或"标签云"视图
  - 标签结果开始喂给 ADR-013 兴趣画像（迫使持久化以做命中统计）
  - 跨 item 聚合需求出现（"我所有跟 X 标签相关的 item"）
- **structured output 升级触发** — DeepSeek / Azure / 主流国产模型在主版本中正式支持 `response_format={"type":"json_schema", ...}` 且行为收敛（不再随版本变化），把 prompt 强约束路径换成原生 schema 校验；同时考虑把 `LlmRequest` 加 `ResponseFormat` 字段（破坏性变更，需要新 ADR）。
- **受控词表触发** — 用户开始抱怨"标签太散 / 同一概念多种说法"，引入 `tag_dictionary` 表与"已存在标签优先复用 + 同义词归一"逻辑（独立 ADR）。
- **prompt 模板配置化触发** — 同 ADR-030：标签模板进入第二轮迭代（用户反馈"标签太抽象 / 太具体 / 不够中文"），把 system message 挪到 `appsettings.Llm:Prompts:InboxTagging`。
- **跨 provider prompt 分流触发** — 用 deepseek 跑标签发现 JSON 输出明显不如 OpenAI，需要按 provider 用不同 system prompt（独立 ADR，与 ADR-028 §E1 单 active provider 假设一并复议）。
- **multi-item 标签上下文触发** — 产品要求"基于 user 已有 inbox 标签历史，新 item 标签优先复用旧词"（迫使从 stateless → stateful，独立 ADR）。
- **标签数量上限触发** — 用户反馈"5 个不够 / 太多"，调整 D1 数量上限（直接改 ADR 或开新 ADR 视影响面而定）。
- **多语言触发** — 用户大量投喂英文 / 日文材料并希望标签按材料语言输出，引入语言检测 + 多语言 prompt 分流（独立 ADR）。

## 相关页面

- [ADR-030 Inbox 单条总结 V0](inbox-item-summarize-v0.md)：本 ADR 的姊妹决策；`IInboxSummaryAppService` 与 `IInboxTaggingAppService` 同骨架，错误码体系与端点形态完全对称。
- [ADR-028 LLM Provider V0](llm-provider-v0-openai-deepseek-abstraction.md)：定义 `ILlmProvider` 端口与错误映射表；本 ADR 透传 §H1 错误码并新增 `inbox.taggingParseFailed`。
- [ADR-029 Azure OpenAI 扩展](llm-provider-azure-openai-extension.md)：当 `ActiveProvider=AzureOpenAI` 时本 ADR 自动用 Azure 后端（无需改动）。
- [ADR-026 Inbox V0 数据契约](inbox-v0-capture-and-list-contract.md)：定义 `InboxItem` 聚合与 `IInboxRepository`；本 ADR 通过仓储取材料。
- [ADR-014 第一版切片](mvp-first-slice-chat-inbox-read-side.md)：本 ADR 兑现"打标签"动作，距离闭环 read-side 四个动作还差"分类"与"候选整理方案"。
- [ADR-023 Api 入口立面](api-entry-facade-and-v0-endpoints.md)：本 ADR 端点遵循其形态约定。
- [ADR-004 重要动作分级](important-action-levels-and-confirmation.md)：本 ADR 打标签动作属 L0，无须 user 确认。
- [ADR-002 选择题优先](options-over-elaboration.md)：标签可作为后续候选生成的输入；本 ADR 是其前置铺垫之一。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 即"方案先行"。
