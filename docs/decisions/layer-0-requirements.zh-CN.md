---
title: "Layer 0 需求说明：LLM Driver 抽象"
type: decision
tags: [layer-0, llm-driver, requirements, agent-os]
sources: [decisions/success-criteria.zh-CN.md, decisions/phase-0-overview.md, concepts/agent-os-architecture.zh-CN.md]
created: 2026-04-08
updated: 2026-04-17
status: active
---

# Layer 0 需求说明：LLM Driver 抽象

> 屏蔽 LLM 提供商差异，为上层内核执行引擎提供统一、可靠、可观测的模型访问层。
>
> OS 类比：硬件驱动程序。见 [[concepts/agent-os-architecture.zh-CN]]。

## 1. 问题定义

### 1.1 为什么需要 LLM Provider 抽象

LLM 是 Agent OS 的“硬件引擎”。不同提供商（OpenAI、Anthropic、Azure OpenAI、Ollama、DeepSeek、Qwen 等）的 API 在以下维度存在差异：

| 差异维度 | OpenAI | Anthropic | Ollama | Azure OpenAI |
|---------|--------|-----------|--------|-------------|
| 认证方式 | API Key | API Key | 无 / 自定义 | Azure AD + API Key |
| Tool 调用字段 | `tool_calls` | `tool_use` | `tool_calls` (兼容) | `tool_calls` |
| 流式协议 | SSE | SSE | NDJSON | SSE |
| Token 计数位置 | 响应体 / 流末尾 | 响应体 | 响应体 | 响应体 |
| 上下文窗口 | 128K–1M | 200K | 模型相关 | 同 OpenAI |
| 结构化输出 | `response_format` + Strict | 无原生 | 部分支持 | 同 OpenAI |
| 并行 tool call | ✅ | ✅ | ✅ | ✅ |
| 定价模型 | per-token | per-token | 免费(本地) | per-token |

**如果不抽象**：上层内核代码与具体提供商耦合，切换提供商需要改业务代码，无法测试。

### 1.2 用户场景

| 场景 | 描述 | 关键需求 |
|------|------|---------|
| **S1 本地开发** | 开发者用 Ollama 在笔记本上跑 Agent | 零配置启动、无需 API Key |
| **S2 生产部署** | 企业用 Azure OpenAI 运行高可用服务 | Azure AD 认证、故障降级、成本追踪 |
| **S3 多模型混用** | 不同 Agent 用不同模型（GPT-4o 做推理、GPT-4o-mini 做摘要） | 按 Agent 配置 Provider、按用途路由 |
| **S4 流式交互** | 用户看到实时打字效果 + 工具调用状态 | 结构化流式事件、tool call 增量拼接 |
| **S5 成本控制** | 企业限制每日 token 消耗 | 精确 token 计数、成本估算、预算执行 |
| **S6 Provider 故障** | 主 Provider 返回 429/500/503 或超时 | 自动降级到备用 Provider、透明重试 |
| **S7 新 Provider 集成** | 社区贡献者想添加 Mistral Provider | 实现 1 个接口 + 1 个 DI 扩展，不改 Core |
| **S8 测试** | 单元测试需要 Mock LLM 响应 | 接口可 Mock、支持确定性响应 |

### 1.3 约束条件

| 约束 | 说明 |
|------|------|
| **C1 纯 DI** | 所有服务通过构造函数注入，禁止 `new`、禁止静态工厂 |
| **C2 IHttpClientFactory** | 所有 HTTP 请求通过 `IHttpClientFactory` 创建 `HttpClient`，禁止直接 `new HttpClient()` |
| **C3 ILogger<T>** | 每个 Provider 使用结构化日志，support log scopes |
| **C4 IOptions<T>** | 配置通过 Options 模式注入，启动时验证 |
| **C5 CancellationToken** | 所有异步方法传播 `CancellationToken` |
| **C6 Abstractions 零依赖** | `ILLMProvider` 及相关 record 在 Abstractions 包中，不依赖任何第三方包 |
| **C7 异步流** | 流式方法返回 `IAsyncEnumerable<T>`，支持背压 |
| **C8 线程安全** | Provider 实例必须是线程安全的（通过 DI 注册为 Singleton 或 Scoped） |
| **C9 .NET 10 / C# 13** | 使用 file-scoped namespace、primary constructors、record types |

### 1.4 验收标准映射

本层的所有功能最终必须通过 SC-7（LLM Provider 层）5 项验收标准：

| SC 项 | 简述 | 对应功能域 |
|-------|------|-----------|
| SC-7.1 | 至少 3 个 Driver（本地 + 公共云 + 企业云） | Driver 实现 |
| SC-7.2 | 统一流式事件模型（5 种事件类型） | 流式抽象 |
| SC-7.3 | 故障自动降级（同一 Run 内切换） | 弹性机制 |
| SC-7.4 | Token 用量/延迟/成本采集，按 Run 聚合 | 可观测性 |
| SC-7.5 | 新 Driver = 1 接口 + 1 DI 扩展，不改 Core | 可扩展性 |

## 2. 参考实现分析（dawning-agents）

> **重要**：dawning-agent-os 是**全新项目**，不依赖 dawning-agents。
> dawning-agents 仅作为学习参考，用于了解哪些设计已被验证可行、哪些是已知不足。

### 2.1 dawning-agents 中已验证可行的设计

| 设计 | 验证结论 | 新项目参考价值 |
|------|---------|--------------|
| `ILLMProvider` 三方法接口 | ✅ 可行 | ChatAsync / ChatStreamAsync / ChatStreamEventsAsync 的三层方法划分被证明实用 |
| `ChatMessage` 四角色 record | ✅ 可行 | system / user / assistant / tool 角色 + factory methods 使用体验好 |
| `StreamingChatEvent` 增量模型 | ✅ 可行 | ContentDelta + ToolCallDelta 的增量拼接方案有效 |
| `ToolDefinition` JSON Schema | ✅ 可行 | 用 JSON Schema 描述工具参数是各 Provider 通用的方式 |
| Provider 独立 NuGet 包 | ✅ 可行 | Ollama / OpenAI / Azure 独立包，按需引用 |
| 原生 HttpClient + IHttpClientFactory | ✅ 可行 | 不绑定第三方 SDK，保持灵活性 |

### 2.2 dawning-agents 中的已知不足（新项目需解决）

| 不足 | 影响 | 新项目目标 |
|------|------|-----------|
| 无 Driver 故障降级 | 主 Driver 故障时 Agent Run 直接失败 | SC-7.3：同一 Run 内自动切换 |
| 无成本估算 | 无法追踪和控制 LLM 调用费用 | SC-7.4：按模型价格表估算 USD |
| 无按 Run 聚合指标 | 单次调用有 token 记录，但无 Run 级汇总 | SC-7.4：token/延迟/成本 Run 级聚合 |
| 无 Driver 契约测试 | 新 Driver 实现质量无法自动验证 | SC-7.5：标准测试套件 |
| 无 Anthropic Driver | 缺少 Claude 系列模型支持 | SC-7.1：4+ Driver 覆盖 |
| 流式事件类型不够明确 | 混合使用字段判断，代码不够清晰 | 5 种明确事件类型 |
| 无上下文窗口管理 | 上层需要自行控制 token 用量 | 自动检测 + 占比报告 |
| 无模型能力声明 | 上层无法查询模型是否支持特定功能 | ModelCapabilities 配置声明 |

## 3. 非目标（Layer 0 不做）

| 不做 | 原因 | 归属层 |
|------|------|-------|
| Agent 执行循环 | Layer 1 职责 | Layer 1 |
| 工具注册与分发 | Layer 1 职责 | Layer 1 |
| 记忆召回注入 prompt | Layer 2 职责 | Layer 2 |
| 多 Agent 编排 | Layer 3 职责 | Layer 3 |
| 技能路由 | Layer 4 职责 | Layer 4 |
| RBAC 鉴权 | Layer 7 职责 | Layer 7 |

## 4. 关键技术问题（需在 Tech Spec 中回答）

| # | 问题 | 影响 |
|---|------|------|
| Q1 | 统一事件模型用 5 种独立 record vs 1 个 record + 判别字段？ | API 人体工学、序列化性能 |
| Q2 | Provider 降级是 Decorator 模式还是独立 FallbackProvider？ | 架构复杂度、可测试性 |
| Q3 | 成本估算的价格表如何更新？硬编码 vs 配置 vs 远程？ | 维护成本、准确性 |
| Q4 | 上下文窗口管理是 Provider 职责还是独立服务？ | 模块边界 |
| Q5 | 模型能力探测是编译时（配置声明）还是运行时（API 查询）？ | 灵活性 vs 可靠性 |
| Q6 | Anthropic `tool_use` 格式差异如何在 Abstractions 层无感？ | 抽象完整性 |
| Q7 | Streaming 错误（中途断连）如何恢复？ | 可靠性 |
| Q8 | 结构化输出在不支持 Strict mode 的 Provider 上如何兜底？ | 跨 Provider 一致性 |

## 交叉引用

- [[decisions/success-criteria.zh-CN]] — SC-7 完整验收标准
- [[decisions/roadmap.zh-CN]] — Layer 0 概览
- [[decisions/phase-0-overview]] — 技术栈与核心概念定义
- [[decisions/layer-0-features.zh-CN]] — 功能清单
- [[decisions/layer-0-tech-spec.zh-CN]] — 技术规格
