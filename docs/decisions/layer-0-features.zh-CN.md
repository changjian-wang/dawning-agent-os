---
title: "Layer 0 功能清单：LLM Driver 抽象"
type: decision
tags: [layer-0, llm-driver, features, agent-os]
sources: [decisions/layer-0-requirements.zh-CN.md, decisions/success-criteria.zh-CN.md]
created: 2026-04-08
updated: 2026-04-17
status: active
---

# Layer 0 功能清单：LLM Driver 抽象

> 按功能域分组，每个功能标注优先级、复杂度、SC 映射。
>
> **注意**：Dawning Agent OS 是全新项目，所有功能均需从零实现。
> “参考”列表示 dawning-agents 中是否有可借鉴的设计，不代表代码复用。

## 优先级定义

| 优先级 | 含义 |
|--------|------|
| **P0** | 必须实现，阻塞后续 Layer |
| **P1** | 必须实现，SC 验收要求 |
| **P2** | 应该实现，提升完整度 |
| **P3** | 可延后，不阻塞任何 SC |

## 复杂度定义

| 复杂度 | 含义 |
|--------|------|
| **S** | 简单，< 100 行实现 |
| **M** | 中等，100–500 行 |
| **L** | 复杂，> 500 行或涉及多个组件 |

---

## 域 1：核心抽象（Abstractions）

> 定义在 `Dawning.AgentOS.Abstractions` 中，零外部依赖。

| # | 功能 | 优先级 | 复杂度 | SC | 参考 | 说明 |
|---|------|--------|--------|-----|------|------|
| F1.1 | `ILLMProvider` 接口 | P0 | S | 7.5 | ✅ | 3 方法：ChatAsync、ChatStreamAsync、ChatStreamEventsAsync + Name 属性 |
| F1.2 | `ChatMessage` record | P0 | S | — | ✅ | 四角色（system/user/assistant/tool）、ToolCalls、ToolCallId、factory methods |
| F1.3 | `ChatCompletionResponse` record | P0 | S | 7.4 | ✅ | Content、TokenUsage、FinishReason、ToolCalls |
| F1.4 | `ChatCompletionOptions` record | P0 | S | — | ✅ | Temperature、MaxTokens、Tools、ToolChoice、ResponseFormat、StopSequences |
| F1.5 | `ToolCall` record | P0 | S | — | ✅ | Id、FunctionName、Arguments |
| F1.6 | `ToolDefinition` record | P0 | S | — | ✅ | Name、Description、ParametersSchema + 从 ITool 构建的 factory |
| F1.7 | `StreamingChatEvent` 统一事件模型 | P0 | M | 7.2 | ⚠️ | 参考设计可借鉴，但需重新设计 5 种明确事件类型 — 见 F1.7a–F1.7e |
| F1.7a | — `TextDelta` 事件 | P0 | S | 7.2 | ✅ | 增量文本片段 |
| F1.7b | — `ToolCallRequested` 事件 | P0 | S | 7.2 | ⚠️ | LLM 决定调用工具（含 name + 参数增量拼接完成后触发） |
| F1.7c | — `ToolCallCompleted` 事件 | P0 | S | 7.2 | — | 工具执行结果回报（从 Layer 1 回传，Provider 层定义事件类型） |
| F1.7d | — `RunCompleted` 事件 | P0 | S | 7.2 | ⚠️ | 最终响应完成（含 FinishReason + 完整 TokenUsage） |
| F1.7e | — `Error` 事件 | P0 | S | 7.2 | — | Provider 级错误（HTTP 错误、超时、流中断等） |
| F1.8 | `ToolChoiceMode` 枚举 | P0 | S | — | ✅ | Auto、None、Required、SpecificFunction |
| F1.9 | `ResponseFormat` record | P1 | S | — | ✅ | Text、JsonObject、JsonSchema (Strict) |
| F1.10 | `TokenUsage` record | P0 | S | 7.4 | ✅ | PromptTokens、CompletionTokens、TotalTokens |
| F1.11 | `ModelCapabilities` record | P2 | S | — | — | 声明模型能力：supports_tool_use、supports_vision、supports_json_mode、max_context_window、supports_parallel_tools |
| F1.12 | `ProviderMetadata` record | P1 | S | 7.4 | — | ProviderName、ModelName、ApiVersion、Endpoint — 用于可观测性 |
| F1.13 | `CostEstimate` record | P1 | S | 7.4 | — | InputCostUsd、OutputCostUsd、TotalCostUsd、PricingModel |

---

## 域 2：Provider 实现

> 每个 Provider 独立 NuGet 包，依赖 Abstractions。

| # | 功能 | 优先级 | 复杂度 | SC | 参考 | 说明 |
|---|------|--------|--------|-----|------|------|
| F2.1 | **Ollama Provider** | P0 | L | 7.1 | ✅ | 本地 HTTP、原生 function calling、NDJSON 流式 |
| F2.1a | — Ollama Chat (非流式) | P0 | M | — | ✅ | POST /api/chat |
| F2.1b | — Ollama Chat Stream (文本) | P0 | M | — | ✅ | POST /api/chat + stream:true |
| F2.1c | — Ollama Chat Stream Events (结构化) | P0 | M | 7.2 | ✅ | 解析 NDJSON → 统一事件模型 |
| F2.1d | — Ollama 模型列表与能力检测 | P2 | S | — | — | GET /api/tags → ModelCapabilities |
| F2.2 | **OpenAI Provider** | P0 | L | 7.1 | ✅ | 基于 OpenAI .NET SDK |
| F2.2a | — OpenAI Chat (非流式) | P0 | M | — | ✅ | |
| F2.2b | — OpenAI Chat Stream Events | P0 | M | 7.2 | ✅ | SSE → 统一事件模型 |
| F2.2c | — OpenAI Structured Output (Strict) | P1 | M | — | ⚠️ | `response_format: { type: "json_schema", strict: true }` |
| F2.2d | — OpenAI 并行 tool calling | P0 | S | — | ✅ | 单次响应多个 tool_calls |
| F2.3 | **OpenAI-Compatible Provider** | P1 | M | 7.1 | ✅ | 自定义 endpoint：DeepSeek、Qwen、Zhipu、Moonshot 等 |
| F2.4 | **Azure OpenAI Provider** | P0 | L | 7.1 | ✅ | Azure AD + API Key 双认证、deployment-based 路由 |
| F2.4a | — Azure AD 认证 (DefaultAzureCredential) | P0 | M | — | ✅ | |
| F2.4b | — Azure API Key 认证 | P0 | S | — | ✅ | |
| F2.4c | — Azure Deployment 路由 | P0 | S | — | ✅ | 按 deployment name 选择模型 |
| F2.5 | **Anthropic Provider** | P1 | L | 7.1 | — | Claude API，`tool_use` 格式适配 |
| F2.5a | — Anthropic Chat (非流式) | P1 | M | — | — | Messages API |
| F2.5b | — Anthropic Chat Stream Events | P1 | M | 7.2 | — | SSE event types 映射到统一事件模型 |
| F2.5c | — Anthropic tool_use → ToolCall 适配 | P1 | M | — | — | `tool_use` block → `ToolCall` record |
| F2.5d | — Anthropic extended thinking | P2 | M | — | — | 推理过程可见性 |

---

## 域 3：流式处理

> 将各 Provider 原始流转换为统一事件模型。

| # | 功能 | 优先级 | 复杂度 | SC | 参考 | 说明 |
|---|------|--------|--------|-----|------|------|
| F3.1 | `StreamingAccumulator` | P0 | M | 7.2 | ✅ | 从 ToolCallDelta 增量拼接完整 ToolCall |
| F3.2 | SSE 解析器 | P0 | M | 7.2 | ✅ | OpenAI / Azure / Anthropic SSE 格式解析 |
| F3.3 | NDJSON 解析器 | P0 | M | 7.2 | ✅ | Ollama 换行分隔 JSON 解析 |
| F3.4 | 流中断恢复 | P2 | L | — | — | 网络中断时重建流连接 |
| F3.5 | 流式背压控制 | P1 | S | — | ✅ | `IAsyncEnumerable` 天然支持 |
| F3.6 | 流式超时检测 | P1 | S | 7.3 | — | 配置 `StreamIdleTimeout`，超时触发 Error 事件 |

---

## 域 4：弹性与降级

> Provider 级故障处理。

| # | 功能 | 优先级 | 复杂度 | SC | 参考 | 说明 |
|---|------|--------|--------|-----|------|------|
| F4.1 | **Provider 降级链** | P1 | L | 7.3 | — | 主 Provider 失败时自动切换到下一个 Provider |
| F4.1a | — 可触发降级的条件 | P1 | M | 7.3 | — | HTTP 429/500/502/503/504、超时、流中断 |
| F4.1b | — 降级链配置 | P1 | S | 7.3 | — | `FallbackChain: [AzureOpenAI, OpenAI, Ollama]` |
| F4.1c | — 降级事件记录 | P1 | S | 7.3 | — | 记入运行追踪：原始错误、降级目标、结果 |
| F4.2 | **重试策略** | P1 | M | 7.3 | ⚠️ | 指数退避 + 抖动、可配置最大重试次数和退避上限 |
| F4.3 | **超时配置** | P1 | S | 7.3 | ⚠️ | 按 Provider 配置连接超时、响应超时、流式空闲超时 |
| F4.4 | **熔断器** | P2 | M | — | — | 连续失败超阈值时暂时跳过该 Provider |
| F4.5 | **降级指标上报** | P1 | S | 7.3 | — | 降级次数、降级成功率、降级延迟增加量 |

---

## 域 5：可观测性

> Token / 延迟 / 成本可追踪。

| # | 功能 | 优先级 | 复杂度 | SC | 参考 | 说明 |
|---|------|--------|--------|-----|------|------|
| F5.1 | **每次调用 TokenUsage 记录** | P0 | S | 7.4 | ✅ | PromptTokens、CompletionTokens 在 Response 和 StreamingEvent 中 |
| F5.2 | **每次调用延迟计时** | P1 | S | 7.4 | — | 请求开始 → 首 token (TTFT) → 最后 token，三个时间点 |
| F5.3 | **成本估算** | P1 | M | 7.4 | — | 按模型价格表 × token 数计算 USD 成本 |
| F5.3a | — 价格表配置 | P1 | S | 7.4 | — | 按模型配置 input_price/output_price（$/1M tokens） |
| F5.3b | — 价格表可更新 | P2 | S | — | — | 支持 `IOptions<T>` 热更新或配置文件 |
| F5.4 | **按 Run 聚合** | P1 | M | 7.4 | — | 一次 Agent Run 可能多次调用 LLM → 聚合总 token/成本/延迟 |
| F5.5 | **结构化日志** | P0 | S | — | ✅ | ILogger<T> + log scopes (ProviderName, Model, RunId) |
| F5.6 | **OpenTelemetry 集成点** | P2 | M | — | — | 每次 LLM 调用创建 span（属于 Layer 7，此处预留 hook） |

---

## 域 6：配置与 DI

> 启动配置和依赖注入。

| # | 功能 | 优先级 | 复杂度 | SC | 参考 | 说明 |
|---|------|--------|--------|-----|------|------|
| F6.1 | **Provider Options 类** | P0 | S | — | ✅ | 每个 Provider 一个 Options record，含 Endpoint、ApiKey、Model、超时等 |
| F6.2 | **Options 启动验证** | P0 | S | — | ✅ | `IValidatableOptions`，启动时验证必填字段 |
| F6.3 | **DI 扩展方法** | P0 | S | 7.5 | ✅ | `AddOllamaProvider()`、`AddOpenAIProvider()`、`AddAzureOpenAIProvider()` |
| F6.4 | **全局默认 Provider 配置** | P1 | S | — | — | 注册多个 Provider 时，指定默认 Provider |
| F6.5 | **按名称解析 Provider** | P1 | M | — | — | `ILLMProviderFactory.GetProvider("azure-gpt4o")` 按名称获取特定 Provider |
| F6.6 | **环境变量覆盖** | P0 | S | — | ✅ | `LLM__ProviderType=AzureOpenAI` 覆盖 appsettings |
| F6.7 | **Keyed DI 支持** | P2 | S | — | — | .NET 8+ Keyed Services：`[FromKeyedServices("openai")] ILLMProvider` |

---

## 域 7：测试基础设施（Harness Engineering）

> **Harness Engineering**（测试夹具工程）：为 Provider 层构建可复用的测试基础设施。
> 当存在 4+ 个 Provider 实现时，Harness 确保：
> - **一次编写，N 次验证** — 契约测试写一套，所有 Provider 跑同一套用例
> - **快速反馈** — InMemory Provider 让单元测试毫秒级完成，不依赖真实 LLM API
> - **确定性** — Recording Provider 消除 LLM 非确定性输出对 CI 的影响

| # | 功能 | 优先级 | 复杂度 | SC | 参考 | 说明 |
|---|------|--------|--------|-----|------|------|
| F7.1 | **Provider 契约测试套件** | P1 | L | 7.5 | — | 每个 Provider 实现必须通过的标准测试用例集 |
| F7.1a | — 非流式 Chat 基本功能 | P1 | M | 7.5 | — | 发送消息 → 收到有效响应 |
| F7.1b | — 流式 Chat 事件完整性 | P1 | M | 7.5 | — | 所有事件类型都正确触发 |
| F7.1c | — Tool Call 往返 | P1 | M | 7.5 | — | 发送 tool definitions → LLM 返回 tool_calls → 回传结果 |
| F7.1d | — 并行 Tool Call | P1 | M | 7.5 | — | 验证单次响应包含 >= 2 个 tool_calls |
| F7.1e | — TokenUsage 准确性 | P1 | S | 7.5 | — | PromptTokens + CompletionTokens = TotalTokens |
| F7.1f | — 错误处理 | P1 | M | 7.5 | — | 无效 API Key → 明确异常；无效模型 → 明确异常 |
| F7.2 | **InMemory Mock Provider** | P0 | M | — | — | 返回预设响应，支持配置 tool_calls、streaming、delay |
| F7.3 | **Recording Provider** | P2 | M | — | — | 代理模式：转发真实请求并记录请求/响应，用于 replay 测试 |

---

## 功能统计

| 优先级 | 数量 | 说明 |
|--------|------|------|
| P0 | 27 | 必须实现，阻塞后续 Layer |
| P1 | 26 | 必须实现，SC 验收要求 |
| P2 | 10 | 应该实现，提升完整度 |
| P3 | 0 | — |
| **总计** | **63** | 全部从零实现 |

| 参考状态 | 数量 | 说明 |
|----------|------|------|
| ✅ 可借鉴设计 | 25 | dawning-agents 中有已验证的设计可参考 |
| ⚠️ 需重新设计 | 5 | 参考设计存在不足，需改进 |
| — 无参考 | 33 | 全新设计 |

---

## 交叉引用

- [[decisions/layer-0-requirements.zh-CN]] — 需求说明
- [[decisions/layer-0-tech-spec.zh-CN]] — 技术规格
- [[decisions/success-criteria.zh-CN]] — SC-7 验收标准
- [[decisions/roadmap.zh-CN]] — Layer 0 概览
