---
title: ADR-028 LLM Provider V0：ILlmProvider 抽象、OpenAI/DeepSeek 兼容客户端与 /api/llm/ping smoke 端点
type: adr
subtype: architecture
canonical: true
summary: V0 LLM 接入落地为 Application/Abstractions/Llm 下的 ILlmProvider 端口（chat completion 一个非流式方法 + Result<T> 错误模型），实现放 Infrastructure/Llm/{OpenAi,DeepSeek} 共用 OpenAI-shaped JSON 客户端，appsettings.Llm 节切换单一 ActiveProvider，环境变量覆盖 ApiKey；ApiKey 缺失时 warn-but-start 而非 fail-fast；新增 GET /api/llm/ping 单 smoke 端点验证管子通；不引入 Microsoft.Extensions.AI / Semantic Kernel / OpenAI SDK / Polly；不做流式 / tool calling / 结构化输出 / 多 provider 路由 / 真实业务接入（inbox capture 等）/ OS keychain / 多模态 / token 计数 / 成本核算。
tags: [agent, engineering]
sources: []
created: 2026-05-03
updated: 2026-05-03
verified_at: 2026-05-03
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-desktop-stack-electron-aspnetcore.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/no-mediator-self-domain-event-dispatcher.md, pages/adrs/inbox-v0-capture-and-list-contract.md, pages/adrs/abstract-instruction-fallback.md, pages/adrs/options-over-elaboration.md, pages/adrs/important-action-levels-and-confirmation.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-03
adr_revisit_when: "出现第三个 provider（Anthropic / Gemini / 本地模型）且其 API 形态明显偏离 OpenAI ChatCompletion shape；renderer 上线 chat 形态需要流式（B 轴失效）；ActionClassifier / 抽象指令兜底 / 起草 / 候选生成等真实业务路径开始落地，需要 tool calling / structured output / 路由策略；出现按任务路由不同 provider 的产品需求（D 轴失效）；出现 token 计数 / 成本核算 / 速率配额监控需求；OS keychain 接入 ApiKey 加密存储；用户多账户进入产品（每用户独立 key）；Microsoft.Extensions.AI 离开 preview 且生态成型，本 ADR 的自维 HTTP 客户端需要被替换；DeepSeek API 在某项契约上与 OpenAI ChatCompletion 出现不兼容（共享客户端假设失效）；ApiKey 配置错误的失败模式从 dogfood 单用户演化到多用户场景（warn-but-start 不再适用）。"
---

# ADR-028 LLM Provider V0：ILlmProvider 抽象、OpenAI/DeepSeek 兼容客户端与 /api/llm/ping smoke 端点

> V0 LLM 接入落地为 Application/Abstractions/Llm 下的 ILlmProvider 端口（chat completion 一个非流式方法 + Result<T> 错误模型），实现放 Infrastructure/Llm/{OpenAi,DeepSeek} 共用 OpenAI-shaped JSON 客户端，appsettings.Llm 节切换单一 ActiveProvider，环境变量覆盖 ApiKey；ApiKey 缺失时 warn-but-start 而非 fail-fast；新增 GET /api/llm/ping 单 smoke 端点验证管子通；不引入 Microsoft.Extensions.AI / Semantic Kernel / OpenAI SDK / Polly；不做流式 / tool calling / 结构化输出 / 多 provider 路由 / 真实业务接入（inbox capture 等）/ OS keychain / 多模态 / token 计数 / 成本核算。

## 背景

[PURPOSE.md](../../PURPOSE.md) §2 yaml 字段 `mvp_default_llm_providers` 把第一版的 LLM 接入契约写死为「默认接入 GPT 与 DeepSeek；保留 provider 抽象，不绑定单一供应商」。这一条目前只是文字契约，代码层面尚未存在任何 LLM 端口、任何 HTTP 客户端、任何配置节——也就是说 PURPOSE 的产品契约目前与代码现实之间存在一条断点。

S6 / S7 已经把"桌面壳 + 本地后端 + inbox 捕获 / 列表 + IPC 桥"这条骨架打通（[ADR-025](desktop-process-supervisor-electron-dotnet-child.md) / [ADR-026](inbox-v0-capture-and-list-contract.md) / [ADR-027](desktop-renderer-v0-native-html-and-ipc-bridge.md)），但所有动作都是**纯本地**的：捕获只往 SQLite 写一行，列表只把 SQLite 的行读出来。任何"理解用户意图、生成候选、起草、分类、打标"等需要 LLM 的能力——也就是 PURPOSE §4.1 的 [选择题优先](options-over-elaboration.md) / [抽象指令兜底](abstract-instruction-fallback.md) / [客观代笔](objective-drafting-style.md) / [动作分级](important-action-levels-and-confirmation.md)——都需要先有一个"管子"把 .NET 后端和某个 LLM provider 接通。

[Domain.Services.Permissions.ActionClassifier](../../../src/Dawning.AgentOS.Domain.Services/Permissions/ActionClassifier.cs) 当前是 V0 stub，分类逻辑写死。第一个真正消费 LLM 的业务路径很可能就是它——但本 ADR 的范围**不**包含让 ActionClassifier 真接 LLM，只包含"管子能通"。

S8 进入实施前需要锁死的问题：

- LLM 端口的抽象层级：放 Domain 还是 Application/Abstractions？
- 端口形状：单一 chat completion 方法，还是从 Day-1 就把 streaming / tool calling / structured output 都暴露出来？
- 实现是否引入 Microsoft.Extensions.AI / Semantic Kernel / OpenAI 官方 .NET SDK，还是自维 HttpClient 直发 JSON？
- 第一个消费者是谁——真接业务（如 ActionClassifier、inbox 摘要）还是只暴露 smoke 端点？
- 多 provider 注册方式：单 active provider 切换、keyed services 多注册、还是路由策略？
- ApiKey 的来源：appsettings、环境变量、OS keychain？
- ApiKey 缺失时启动行为：fail-fast 还是 warn-but-start？
- 错误模型：抛异常还是 Result\<T\> + DomainError（与 [ADR-026](inbox-v0-capture-and-list-contract.md) 对齐）？
- 弹性：是否引入 Polly 重试 / 熔断？是否走 IHttpClientFactory？

如果把这些问题留到代码阶段「边写边定」，就会重蹈 ADR-021 → ADR-022 那种「实施期间反复改方向」的覆辙。本 ADR 在 PURPOSE / ADR-022 / ADR-023 既有契约之上把 LLM provider V0 的形态钉死，作为 S8 实施的依据；按 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)，代码落地仍须在本 ADR 接受后才进行。

## 备选方案

抽象层级（A 轴）：

- **A1** 端口放 `Application/Abstractions/Llm/ILlmProvider.cs`，实现放 `Infrastructure/Llm/{OpenAi,DeepSeek}/`。与 [ADR-024](sqlite-dapper-bootstrap-and-schema-init.md) 的 `IDbConnectionFactory` / [ADR-026](inbox-v0-capture-and-list-contract.md) 的 `IInboxAppService` 同框（外部 IO 端口归 Application）。
- **A2** 端口放 `Domain.Services/Llm/ILlmProvider.cs`，与 `ActionClassifier` 同层。
- **A3** 端口放 `Domain/Llm/ILlmProvider.cs`，与 `IInboxRepository`（仍在 Domain/Inbox）同层。
- **A4** 单独开一个新项目 `Dawning.AgentOS.Llm`，放抽象 + 实现。

端口形状（B 轴）：

- **B1** 单一非流式 `Task<Result<LlmCompletion>> CompleteAsync(LlmRequest request, CancellationToken ct)`。`LlmRequest` 携带 messages / model / temperature / maxTokens。
- **B2** B1 + 流式 `IAsyncEnumerable<LlmChunk> StreamAsync(...)`。
- **B3** B1 + 流式 + tool calling（`Tools` / `ToolChoice` / `LlmCompletion.ToolCalls`）+ 结构化输出（`ResponseFormat`）。
- **B4** 抽象成更高层的 `IChat` / `IConversation`，把对话历史维护放在端口里。

实现路径（C 轴）：

- **C1** 自维 `HttpClient` + System.Text.Json，直发 OpenAI ChatCompletion JSON；DeepSeek 共用同一套 DTO（DeepSeek API 与 OpenAI 形态兼容）。
- **C2** 引入 [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI/) 抽象（仍 preview）+ `Microsoft.Extensions.AI.OpenAI` 适配器。
- **C3** 引入 `OpenAI` 官方 .NET SDK + DeepSeek 自维客户端。
- **C4** 引入 [Microsoft.SemanticKernel](https://www.nuget.org/packages/Microsoft.SemanticKernel)。

第一消费者（D 轴）：

- **D1** 仅新增 `GET /api/llm/ping` smoke 端点，调一次 8 token 的 chat completion 返回 `{provider, model, content, durationMs}`。inbox / ActionClassifier 不动。
- **D2** 同时让 `ActionClassifier` 真接 LLM，把现有 stub 替换为 LLM 驱动的分类。
- **D3** 同时让 inbox capture 入箱后异步生成 summary，把 LLM 接入和 inbox 增强一起做。

多 provider 注册方式（E 轴）：

- **E1** 单 active provider：appsettings 一个 `ActiveProvider` 字段，DI 按字段值注册一个 `ILlmProvider` 实例；调用方拿 `ILlmProvider`，不感知具体供应商。
- **E2** 多 provider keyed services：所有 provider 都注册，调用方按 `[FromKeyedServices("openai")]` 取。
- **E3** 路由策略：定义 `ILlmRouter`，根据任务类型（分类 / 起草 / 摘要等）选不同 provider。

ApiKey 配置来源（F 轴）：

- **F1** ASP.NET Core 默认配置链 + dotnet user-secrets：环境变量（`Llm__Providers__OpenAI__ApiKey`）覆盖 user-secrets（开发机器本地，文件在 `~/.microsoft/usersecrets/`，不进 git）覆盖 `appsettings.{Environment}.json` 覆盖 `appsettings.json`；两份 appsettings（含 Development）都是 committed 文件，只能放空占位。
- **F2** F1 + 启动期把 ApiKey 通过 DPAPI（Windows）/ macOS Keychain 加密落 SQLite。
- **F3** F1 + UI 设置面板从 renderer 通过 IPC 把 key 传到 main 再写配置文件。

ApiKey 缺失时启动行为（G 轴）：

- **G1** fail-fast：active provider 的 ApiKey 空白时启动失败，进程退出非零码。
- **G2** warn-but-start：启动正常，仅 `/api/llm/ping` 等 LLM 路径返回 `LlmErrors.AuthenticationFailed`；启动日志打 WARN。

错误模型（H 轴）：

- **H1** `Result<T>` + `DomainError`（与 [ADR-026](inbox-v0-capture-and-list-contract.md) inbox 错误模型一致）；定义 `LlmErrors` 静态类列出 `AuthenticationFailed / RateLimited / UpstreamUnavailable / InvalidRequest / Cancelled`。
- **H2** 抛 `LlmException` 派生异常族，由 controller / middleware 捕获翻译。
- **H3** 端口直接返回 `LlmCompletion?`，错误通过 out 参数 `out LlmError? error`。

弹性策略（I 轴）：

- **I1** `IHttpClientFactory` 命名 client（`"llm-openai"` / `"llm-deepseek"`）+ 默认 100s timeout + 不重试。`CancellationToken` 全链贯通。
- **I2** I1 + Polly 重试策略（429 / 5xx 指数退避，最多 3 次）+ 熔断（连续 5 次失败开路 30s）。
- **I3** I1 + 自维简单重试（不引 Polly）。

测试边界（J 轴）：

- **J1** 单元测试 mock `HttpMessageHandler` 喂入预录的 OpenAI / DeepSeek 响应（success / 401 / 429 / 500 / 网络失败 / 取消，6 case × 2 provider）+ 架构测试（`ILlmProvider` 必须在 Application.Abstractions，实现必须在 Infrastructure）+ Options 校验测试。
- **J2** J1 + live integration test（真调 OpenAI / DeepSeek API）。
- **J3** J1 + 录制重放（VCR）。

## 被否决方案与理由

**A2 / A3 / A4（端口归 Domain.Services / Domain / 独立项目）**：

- A2 把 LLM 当成 Domain.Services 的同伴会破坏 Domain.Services 的纯领域规则定位（`ActionClassifier` 是规则计算，不做外部 IO）；LLM 是有副作用 / 网络依赖 / 配置驱动的**外部端口**，按 [ADR-022 §6](no-mediator-self-domain-event-dispatcher.md) 与 [ADR-024](sqlite-dapper-bootstrap-and-schema-init.md) 已确立的"外部 IO 端口归 Application/Abstractions"惯例，应放 Application。
- A3 把端口塞进 Domain 会让 Domain 项目（目前零外部依赖）面临"是否要接 LLM 类型"的命名空间纠葛。Domain 的入站接口（`IInboxRepository`）服务于聚合的持久化，而 LLM 不是聚合的一部分。
- A4 单独开 `Dawning.AgentOS.Llm` 项目过早抽离：当下没有跨产品复用诉求，按 PURPOSE「framework 是产品成熟后的副产物」原则，先在 Application / Infrastructure 落地，等被复用 ≥ 2 次（例如 dawning-agent-os 之外的代码也要用）再考虑抽项目。

**B2 / B3 / B4（流式 / tool calling / structured output / 高层 Conversation 抽象）**：

- B2 流式只在 chat 形态 UI 上线后才真正有用；当前 renderer V0（[ADR-027](desktop-renderer-v0-native-html-and-ipc-bridge.md)）只有 inbox 单页表单，没有 chat 流式 UI 的消费者。在没有消费者的情况下提前实现流式会推动一个"Agent 的对话历史 / 消息流 / 中断协议"的设计大坑，远超 V0 范围。
- B3 tool calling / structured output 的真正消费者是 ActionClassifier、抽象指令兜底、候选生成——这些都是后续 ADR（ADR-029+）的范围。在 V0 把它们暴露在端口里，会让本 ADR 的形状被未来需求绑架（例如怎么定义 tool schema、是否要支持 strict JSON mode、是否要把 system prompt 提到端口外），而我们当前对这些问题没有信号。
- B4 `IChat` / `IConversation` 把对话历史管理放在端口里，会和"Memory Ledger / Working Memory"这些 PURPOSE §4.1 已锁死的产品语义冲突。对话历史不是 LLM provider 的责任。

**C2 / C3 / C4（Microsoft.Extensions.AI / OpenAI SDK / Semantic Kernel）**：

- C2 `Microsoft.Extensions.AI` 仍 preview，破坏性变更仍频繁；它确实是中长期方向，但绑死会把"它一变我就被迫跟"的运维债拖进 V0。本 ADR 留 `adr_revisit_when` 锚点：等它离开 preview 且生态成型再考虑替换。
- C3 `OpenAI` 官方 SDK 引入后，DeepSeek 又得自维一份适配，反而比共用 OpenAI-shaped JSON 客户端复杂；且 SDK 内部对 streaming / tool calling 的抽象会污染 V0 的最小端口。
- C4 SemanticKernel 是更高层的"plugin / planner / memory"框架，远超 V0 一个 chat completion 的需求；而且 SemanticKernel 的设计哲学（"AI orchestration"）与 PURPOSE「framework 是产品副产物」的反向预设冲突。

**D2 / D3（同时接业务）**：

- D2 让 ActionClassifier 真接 LLM 会触发"分类提示词形状、置信度阈值、动作分级映射"等一串新决策——按 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md) 这是另一个 ADR 的范围（很可能是 ADR-029）。在 LLM 管子尚未通的当下捆绑业务，会导致两条不相关的设计同时改，回归出错时根因不清。
- D3 inbox capture 异步生成 summary 会破坏 [ADR-026](inbox-v0-capture-and-list-contract.md) 的 "capture 路径毫秒级延迟" 契约，需要再起一个 ADR 讨论"派生数据存哪、谁触发、失败如何重试、UI 如何感知 summary 状态"，远超 V0 LLM 通电的范围。

**E2 / E3（多 provider keyed / 路由策略）**：

- E2 keyed services 在调用方有"我现在到底用哪个 provider"的语义需求时才必要；V0 没有这种需求——所有调用都"用配置中指定的那个"。提前 keyed 会让调用方代码出现 `[FromKeyedServices(...)]`，强迫调用方关心一个它本不该关心的事实。
- E3 路由策略需要先有"任务类型分类"才能定义路由规则；当前唯一的"任务"是 smoke ping，没有真实任务可以路由。

**F2 / F3（OS keychain / UI key 设置）**：

- F2 接 DPAPI / Keychain 是"key 跨重启 / 跨用户安全"的范围，需要先回答：是否多用户、是否需要导出 / 导入 / 同步 key、key 轮换策略、密钥派生方案。这些问题在 V0 单用户 dogfood 阶段没有信号。
- F3 UI 设置面板要求 renderer 上线 settings 视图——按 [ADR-027 §决策 C1](desktop-renderer-v0-native-html-and-ipc-bridge.md) renderer V0 是单页 inbox，没有 settings 视图；提前做 settings 会触发 renderer 形态变更。

**G1（fail-fast）**：

- 让"启动桌面壳"被"是否填了 LLM key"这个**与启动无关的配置**绑死，违反 [ADR-026](inbox-v0-capture-and-list-contract.md) 已确立的"inbox 等本地能力不应被 LLM 配置阻塞"契约——dogfood 阶段用户应该能先打开桌面、看到 inbox、捕获几条材料，然后再去配 key。fail-fast 会让首次启动失败，需要用户在没有任何 UI 引导的情况下手动改 appsettings 才能再启，体验糟糕。
- warn-but-start（G2）的代价是：调用 `/api/llm/ping` 时返回 `AuthenticationFailed`。这个错误是显式、可观察、可调试的——比启动崩溃更友好。

**H2 / H3（异常 / out 参数）**：

- H2 异常路径与 [ADR-026](inbox-v0-capture-and-list-contract.md) inbox 的 `Result<T>` 不一致，调用方需要两套错误处理代码（一处 try/catch、一处 `result.IsFailure`），破坏 Application 层的统一性。
- H3 out 参数模式不是 .NET 现代惯用法，会让 AppService 调用站点 verbose（`provider.Complete(req, out var err)`）。

**I2 / I3（Polly / 自维重试）**：

- I2 引入 Polly 之前需要先回答：429 / 5xx 哪些应该重试、重试间隔、是否区分幂等 vs 非幂等、是否需要熔断、是否需要限流。LLM 调用 V0 没有这些信号；smoke ping 失败让调用方自己决定。
- I3 自维重试在不引入新依赖的同时增加了实现负担，且效果不如 Polly；不如直接不重试，让重试在更高层（业务路径）按需求实现。

**J2 / J3（live test / VCR）**：

- J2 live test 要钱、不稳定（rate limit / API 偶发抖动 / 模型版本变更）、要在 CI 配真 secret，与 V0 dogfood 阶段不匹配。
- J3 VCR 录制重放需要引入额外测试基础设施（如 WireMock / scriban），而 mock `HttpMessageHandler` 已能覆盖 6 种关键路径。

## 决策

按上述备选方案的轴序号给出决策，每条决策都基于 PURPOSE / 已落地 ADR 收紧, 不发散：

1. **A1：端口放 `Application/Abstractions/Llm/ILlmProvider.cs`，实现放 `Infrastructure/Llm/{OpenAi,DeepSeek}/`**。与 [ADR-024](sqlite-dapper-bootstrap-and-schema-init.md) / [ADR-026](inbox-v0-capture-and-list-contract.md) 既有"外部 IO 端口归 Application"惯例对齐；不开新项目。
2. **B1：单一非流式 `Task<Result<LlmCompletion>> CompleteAsync(LlmRequest, CancellationToken)`**。`LlmRequest` 字段：`Messages: IReadOnlyList<LlmMessage>` + `Model: string?` + `Temperature: double?` + `MaxTokens: int?`。`LlmMessage` = `(LlmRole Role, string Content)`。`LlmRole` 枚举：`System / User / Assistant`（不含 `Tool`，因 V0 不做 tool calling）。`LlmCompletion` 字段：`Content: string` + `Model: string` + `PromptTokens: int?` + `CompletionTokens: int?` + `Latency: TimeSpan`。
3. **C1：自维 `HttpClient` + `System.Text.Json` 直发 OpenAI ChatCompletion JSON**；DeepSeek 共用同一套 DTO。在 `Infrastructure/Llm/Common/OpenAiCompatibleClient.cs` 中实现共享 HTTP / JSON 逻辑，`OpenAiLlmProvider` 与 `DeepSeekLlmProvider` 分别注入它并提供各自的 `BaseUrl` / `ApiKey` / `Model`。不引入 Microsoft.Extensions.AI / OpenAI SDK / Semantic Kernel。
4. **D1：仅新增 `GET /api/llm/ping` smoke 端点**。调用 `CompleteAsync(messages=[{Role=User, Content="ping"}], MaxTokens=8)`，返回 `{provider: "OpenAI"|"DeepSeek", model: "...", content: "...", durationMs: 123}`。该端点要求 `X-Startup-Token`（与 [ADR-025](desktop-process-supervisor-electron-dotnet-child.md) / [ADR-026](inbox-v0-capture-and-list-contract.md) 一致）。inbox / ActionClassifier 行为不动。
5. **E1：单 active provider，appsettings 切换**。配置形状：

   ```jsonc
   "Llm": {
     "ActiveProvider": "OpenAI",
     "Providers": {
       "OpenAI":   { "ApiKey": "", "BaseUrl": "https://api.openai.com/v1", "Model": "gpt-4o-mini" },
       "DeepSeek": { "ApiKey": "", "BaseUrl": "https://api.deepseek.com",  "Model": "deepseek-chat" }
     }
   }
   ```

   DI 在 `Infrastructure.AddInfrastructure(...)` 内按 `ActiveProvider` 字符串注册一个具体 `ILlmProvider`；调用方拿 `ILlmProvider`，不感知具体供应商。
6. **F1：环境变量 → dotnet user-secrets → `appsettings.{Environment}.json` → `appsettings.json`，仅此**。环境变量名遵循 .NET 默认双下划线分隔：`Llm__ActiveProvider`、`Llm__Providers__OpenAI__ApiKey`、`Llm__Providers__DeepSeek__ApiKey`。开发机器上推荐 `dotnet user-secrets`（存 `~/.microsoft/usersecrets/`，不进 git）；两份 appsettings 文件（包括 `appsettings.Development.json`）都是 committed 文件，其 `ApiKey` 字段必须保持空字符串占位（不得 commit 任何 key）。`appsettings.json` 提供默认结构（`Llm.ActiveProvider`、`Llm.Providers.OpenAI.{BaseUrl,Model}`、`Llm.Providers.DeepSeek.{BaseUrl,Model}`）。
7. **G2：warn-but-start**。`IValidateOptions<LlmOptions>` 仅校验"`ActiveProvider` 字符串落在 `OpenAI`/`DeepSeek` 集合内"——这是结构性错误，应该 fail-fast；但**不**校验 ApiKey 非空。ApiKey 为空时，`OpenAiLlmProvider` / `DeepSeekLlmProvider` 在调用 `CompleteAsync` 时立即返回 `Result<LlmCompletion>.Failure(LlmErrors.AuthenticationFailed("API key is not configured for active provider"))`，并在启动时通过 `ILogger<LlmOptions>` 打 `LogWarning` 提示。
8. **H1：`Result<T>` + `DomainError`**。新增 `Application/Abstractions/Llm/LlmErrors.cs` 静态类：

   - `AuthenticationFailed(string detail)` → 401 / API key 缺失。
   - `RateLimited(string detail)` → 429。
   - `UpstreamUnavailable(string detail)` → 5xx / 网络失败 / DNS 失败。
   - `InvalidRequest(string detail)` → 4xx（非 401/429）/ 参数错误。
   - `Cancelled()` → `OperationCanceledException` 翻译。

   `OperationCanceledException`（被 `CancellationToken` 触发）仍按 .NET 惯例向上抛，由 framework / middleware 处理；不自行翻译为 `Result.Failure`。
9. **I1：`IHttpClientFactory` 命名 client + 默认 100s timeout + 不重试**。`AddHttpClient("llm-openai", c => c.BaseAddress = new Uri(opts.BaseUrl))` 与 `"llm-deepseek"` 各自命名；`CancellationToken` 全链路贯通。**不引入 Polly**；任何重试需求由调用方在更高层按业务规则决定。
10. **J1：mock + 架构 + Options 测试，不接 live**。
    - 单元测试覆盖 6 case × 2 provider = 12 路径：success / 401 / 429 / 500 / 网络失败（`HttpRequestException`）/ 取消。
    - 架构测试扩展 `LayeringTests.cs`：`ILlmProvider` 类型必须在 `Dawning.AgentOS.Application` assembly；`OpenAiLlmProvider` / `DeepSeekLlmProvider` 必须在 `Dawning.AgentOS.Infrastructure` assembly。
    - Options 校验测试：`ActiveProvider` 不在白名单时 IOptions 解析失败；空 ApiKey 时 IOptions 解析成功（确保 G2 不被 fail-fast）。

文件清单（本 ADR 接受后将创建 / 修改）：

```
src/Dawning.AgentOS.Application/Abstractions/Llm/
  ILlmProvider.cs           [new]
  LlmRequest.cs             [new] (record + LlmMessage record + LlmRole enum)
  LlmCompletion.cs          [new] (record)
  LlmErrors.cs              [new] (static DomainError factory)

src/Dawning.AgentOS.Infrastructure/Llm/
  LlmOptions.cs             [new] (Options + IValidateOptions)
  Common/OpenAiCompatibleClient.cs   [new]
  OpenAi/OpenAiLlmProvider.cs        [new]
  DeepSeek/DeepSeekLlmProvider.cs    [new]

src/Dawning.AgentOS.Infrastructure/DependencyInjection/
  InfrastructureServiceCollectionExtensions.cs   [modify: register LLM]

src/Dawning.AgentOS.Api/
  Endpoints/LlmEndpoints.cs                       [new]
  appsettings.json                                [modify: add Llm section]
  Program.cs                                      [modify: MapLlmEndpoints]

tests/Dawning.AgentOS.Infrastructure.Tests/Llm/
  OpenAiLlmProviderTests.cs                       [new]
  DeepSeekLlmProviderTests.cs                     [new]
  LlmOptionsValidationTests.cs                    [new]

tests/Dawning.AgentOS.Architecture.Tests/
  LayeringTests.cs                                [modify: ILlmProvider 归属断言]
```

## 影响

**本 ADR 关闭的开放问题：**

- PURPOSE §2 yaml `mvp_default_llm_providers` 字段从文字契约变成代码契约。
- 第一条"本地后端调外部 LLM"的链路打通，后续 ADR（ActionClassifier 真接 LLM、抽象指令兜底、候选生成、起草）有了起点。
- 错误模型与 [ADR-026](inbox-v0-capture-and-list-contract.md) 的 `Result<T>` 路径统一。

**本 ADR 不关闭的开放问题（明确推迟到后续 ADR）：**

- 流式 chat（消费者：renderer chat 形态 UI，未来）。
- Tool calling / structured output（消费者：ActionClassifier、候选生成）。
- 多 provider 路由 / per-task provider 选择（消费者：成本敏感的批量任务）。
- ApiKey 加密存储与 UI 设置面板（消费者：dogfood 阶段之后的真实多用户）。
- Token 计数 / 成本核算 / 速率配额（消费者：可观测性）。
- Polly 重试 / 熔断（消费者：稳定性敏感场景）。

**本 ADR 引入的代码债：**

- `OpenAiCompatibleClient` 假设 DeepSeek API 与 OpenAI 形态完全兼容。这一假设在测试中通过 mock 验证，但**未通过 live 调用验证**——首次真调时若发现差异，需要在 client 内部增加 hook 点，不开新抽象。
- `appsettings.json` 内 `ApiKey: ""` 的占位字段会让 secret-scanning 工具误报"empty secret"，可接受；CI 应跳过此误报或 baseline。
- `/api/llm/ping` 端点会暴露真实供应商延迟；它是开发期端点，不进 user-facing 表面。renderer 不调用该端点。

**对既有 ADR 的影响：**

- [ADR-022](no-mediator-self-domain-event-dispatcher.md) §10（DomainEventDispatcher 真实现）**仍未关闭**——本 ADR 不触发 dispatcher 落地。
- [ADR-023](api-entry-facade-and-v0-endpoints.md) 端点表新增 `GET /api/llm/ping`；不变更已有端点。
- [ADR-026](inbox-v0-capture-and-list-contract.md) inbox 路径不动；inbox capture 不接 LLM。
- [ADR-027](desktop-renderer-v0-native-html-and-ipc-bridge.md) renderer 不动；renderer 不调用 `/api/llm/ping`。

## 复议触发条件

见 front matter `adr_revisit_when`。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：§2 yaml `mvp_default_llm_providers` 产品契约。
- [ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端](mvp-desktop-stack-electron-aspnetcore.md)：钉死后端形态。
- [ADR-022 去 MediatR：自研领域事件分发器与 AppService 立面](no-mediator-self-domain-event-dispatcher.md)：Application 层立面契约。
- [ADR-023 Api 入口立面：AppService 接入与 V0 端点形态](api-entry-facade-and-v0-endpoints.md)：端点形态约定。
- [ADR-024 SQLite/Dapper 通电：连接工厂、Schema 引导与 V0 持久化骨架](sqlite-dapper-bootstrap-and-schema-init.md)：外部 IO 端口归 Application 的先例。
- [ADR-026 Inbox V0 数据契约与捕获面](inbox-v0-capture-and-list-contract.md)：`Result<T>` 错误模型先例。
- [ADR-009 抽象指令兜底机制](abstract-instruction-fallback.md)：未来真消费者之一。
- [ADR-002 选择题优先于问答题](options-over-elaboration.md)：未来真消费者之一。
- [ADR-004 重要性级别与确认机制](important-action-levels-and-confirmation.md)：未来真消费者之一（ActionClassifier 真接 LLM 时）。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 落地前置门槛。
