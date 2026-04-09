# Dawning Agent Framework — Wiki 索引

> 企业级分布式多 Agent 协作框架，支持技能自演化（Memento-Skills）。
>
> 📋 本索引由 LLM 在每次 Ingest 操作时自动更新。
> 📅 操作日志见 [[log]]。⚙️ 模式定义见 [[SCHEMA]]。

---

## 架构决策 (`decisions/`)

| 页面 | 说明 | 状态 |
|------|------|------|
| [[decisions/roadmap.zh-CN|分层学习路线图]] | Layer 0–7 分层学习路径 + 90 天计划 + KPI + 风险 | active |
| [[decisions/success-criteria.zh-CN|成功标准清单]] | SC-1 ~ SC-10，49 项验收标准 | active |
| [[decisions/phase-0-overview|Phase 0 概览]] | 技术栈、架构决策、核心原则（历史文档） | historical |
| [[decisions/layer-0-requirements.zh-CN\|L0 需求说明]] | LLM Provider 层问题定义、场景、约束 | active |
| [[decisions/layer-0-features.zh-CN\|L0 功能清单]] | 7 大功能域、63 项功能、优先级与 SC 映射 | active |
| [[decisions/layer-0-tech-spec.zh-CN\|L0 技术规格]] | API 设计、数据模型、降级策略、DI 注册 | draft |

## 实体页 (`entities/`)

### Agent 框架 (`entities/frameworks/`)

| 页面 | 框架 | 语言 | 状态 |
|------|------|------|------|
| [[entities/frameworks/microsoft-agent-framework.zh-CN\|MAF]] | Microsoft Agent Framework | .NET / Python | active |
| [[entities/frameworks/semantic-kernel.zh-CN\|SK]] | Semantic Kernel | .NET / Python / Java | active |
| [[entities/frameworks/langgraph.zh-CN\|LangGraph]] | LangGraph | Python / JS | active |
| [[entities/frameworks/crewai.zh-CN\|CrewAI]] | CrewAI | Python | active |
| [[entities/frameworks/openai-agents-sdk.zh-CN\|OpenAI SDK]] | OpenAI Agents SDK | Python | active |
| [[entities/frameworks/google-adk.zh-CN\|Google ADK]] | Google Agent Development Kit | Python | active |

## 概念页 (`concepts/`)

| 页面 | 说明 | 状态 |
|------|------|------|
| [[concepts/llm-wiki-pattern.zh-CN\|LLM Wiki 模式]] | Karpathy 编译式知识管理 → Memory Plane 映射 | active |
| [[concepts/llm-fundamentals\|LLM 基础]] | 大语言模型基础知识 | active |

## 对比分析 (`comparisons/`)

| 页面 | 说明 | 状态 |
|------|------|------|
| [[comparisons/agent-framework-landscape\|框架全景分析（英文）]] | 18 个框架横向对比 | active |
| [[comparisons/agent-framework-landscape.zh-CN\|框架全景分析（中文）]] | 18 个框架横向对比 | active |

## 深度阅读 (`readings/`)

### MAF 源码解析 (`readings/frameworks/maf/`)

| 页面 | 说明 | 状态 |
|------|------|------|
| [[readings/frameworks/maf/00-overview.zh-CN\|MAF 项目结构全景]] | 仓库结构、25+ NuGet 包依赖树、核心类型总览、设计决策 | active |
| [[readings/frameworks/maf/01-abstractions.zh-CN\|MAF Abstractions 层]] | AIAgent/Session/Response/AIContext/Decorator 完整类型分析 | active |
| [[readings/frameworks/maf/02-agent-lifecycle.zh-CN\|MAF Agent 生命周期]] | ChatClientAgent 创建/配置/运行/会话管理、AIAgentBuilder 管道 | active |
| *03-llm-provider* | LLM Provider 层：IChatClient 适配 | planned |
| *04-tool-system* | Function Calling、MCP、工具系统 | planned |
| *05-workflow-graph* | 图工作流引擎 | planned |
| *06-multi-agent* | 多 Agent 编排、A2A 协议 | planned |
| *07-memory* | 记忆/上下文管理 | planned |
| *08-skills* | Agent Skills 系统 | planned |
| *09-observability* | OpenTelemetry、DevUI | planned |
| *10-deployment* | Durable Agents、Azure Functions 托管 | planned |

## 综合分析 (`synthesis/`)

| 页面 | 说明 | 状态 |
|------|------|------|
| *尚无页面* | — | — |

## 原始资料 (`raw/`)

| 资料 | 类型 | 摘入日期 |
|------|------|---------|
| [[raw/papers/memento-skills-2603.18743\|Memento-Skills]] | 论文 | 2026-04-07 |
| [[raw/articles/karpathy-llm-wiki\|Karpathy LLM Wiki]] | 博文 | 2026-04-07 |

---

## 统计

- 📄 Wiki 页面：17
- 📁 原始资料：2
- 📅 最后操作：2026-04-09 readings-maf-lifecycle

---

*最后更新：2026-04-09*
