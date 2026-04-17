# Dawning Agent OS — Wiki 索引

> AI Agent 的操作系统：微内核 + 三面体架构 + 技能自演化（Memento-Skills）。
>
> 📋 本索引由 LLM 在每次 Ingest 操作时自动更新。
> 📅 操作日志见 [[log]]。⚙️ 模式定义见 [[SCHEMA]]。

---

## OS 架构 (`concepts/`)

| 页面 | 说明 | 状态 |
|------|------|------|
| [[concepts/agent-os-architecture.zh-CN\|Agent OS 架构]] | **核心文档**：为什么是 OS 而不是 Framework、微内核设计、三面体→子系统映射、命名空间 | active |
| [[concepts/llm-fundamentals\|LLM 技术原理]] | Token、API、采样、Function Calling 等基础概念（内核前置知识） | active |
| [[concepts/context-management\|上下文管理]] | 五种上下文管理流派对比 + 存储层记忆架构设计 | active |
| [[concepts/agent-loop\|Agent Loop]] | ReAct / Plan-and-Execute / Reflexion 内核执行模式 | active |
| [[concepts/llm-wiki-pattern.zh-CN\|LLM Wiki 模式]] | Karpathy 编译式知识管理 → 存储层（Memory Plane）映射 | active |

## 架构决策 (`decisions/`)

| 页面 | 说明 | 状态 |
|------|------|------|
| [[decisions/roadmap.zh-CN\|分层构建路线图]] | Layer 0–7 分层构建路径：驱动→内核→存储→调度→包管理→IPC→安全 | active |
| [[decisions/success-criteria.zh-CN\|成功标准清单]] | SC-1 ~ SC-10，49 项验收标准 | active |
| [[decisions/phase-0-overview\|Phase 0 概览]] | 技术栈、架构决策、核心原则（历史文档，Agent Framework 时期） | historical |
| [[decisions/layer-0-requirements.zh-CN\|L0 需求说明]] | LLM Driver 层问题定义、场景、约束 | active |
| [[decisions/layer-0-features.zh-CN\|L0 功能清单]] | 7 大功能域、63 项功能、优先级与 SC 映射 | active |
| [[decisions/layer-0-tech-spec.zh-CN\|L0 技术规格]] | API 设计、数据模型、降级策略、DI 注册 | draft |

## 竞品研究 (`entities/`)

### Agent 框架 (`entities/frameworks/`)

| 页面 | 框架 | 语言 | 状态 |
|------|------|------|------|
| [[entities/frameworks/microsoft-agent-framework.zh-CN\|MAF]] | Microsoft Agent Framework | .NET / Python | active |
| [[entities/frameworks/semantic-kernel.zh-CN\|SK]] | Semantic Kernel | .NET / Python / Java | active |
| [[entities/frameworks/langgraph.zh-CN\|LangGraph]] | LangGraph | Python / JS | active |
| [[entities/frameworks/crewai.zh-CN\|CrewAI]] | CrewAI | Python | active |
| [[entities/frameworks/openai-agents-sdk.zh-CN\|OpenAI SDK]] | OpenAI Agents SDK | Python | active |
| [[entities/frameworks/google-adk.zh-CN\|Google ADK]] | Google Agent Development Kit | Python | active |

## 对比分析 (`comparisons/`)

| 页面 | 说明 | 状态 |
|------|------|------|
| [[comparisons/agent-os-vs-frameworks\|Agent OS vs Frameworks]] | **新** 为什么 OS 而不是 Framework——定位差异化分析 | active |
| [[comparisons/agent-framework-landscape\|框架全景分析（英文）]] | 18 个框架横向对比（作为"为什么框架不够"的背景） | active |
| [[comparisons/agent-framework-landscape.zh-CN\|框架全景分析（中文）]] | 18 个框架横向对比 | active |

## 深度阅读 (`readings/`)

### MAF 源码解析 (`readings/frameworks/maf/`)

| 页面 | 说明 | 状态 |
|------|------|------|
| [[readings/frameworks/maf/00-overview.zh-CN\|MAF 项目结构全景]] | 仓库结构、25+ NuGet 包依赖树、核心类型总览、设计决策 | active |
| [[readings/frameworks/maf/01-abstractions.zh-CN\|MAF Abstractions 层]] | AIAgent/Session/Response/AIContext/Decorator 完整类型分析 | active |
| [[readings/frameworks/maf/02-agent-lifecycle.zh-CN\|MAF Agent 生命周期]] | ChatClientAgent 创建/配置/运行/会话管理、AIAgentBuilder 管道 | active |
| *03-llm-provider* | LLM Driver 层：IChatClient 适配 | planned |
| *04-tool-system* | Function Calling、MCP、工具系统 | planned |
| *05-workflow-graph* | 图工作流引擎（调度器参考） | planned |
| *06-multi-agent* | 多 Agent 编排、A2A 协议 | planned |
| *07-memory* | 记忆/上下文管理（存储层参考） | planned |
| *08-skills* | Agent Skills 系统（包管理器参考） | planned |
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

- 📄 Wiki 页面：19（+2：agent-os-architecture、agent-os-vs-frameworks）
- 📁 原始资料：2
- 📅 最后操作：2026-04-17 restructure — Agent Framework → Agent OS

---

*最后更新：2026-04-17*
