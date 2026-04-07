# Agent 框架全景分析

> 截至 2026 年 4 月，市面上所有主流 AI Agent 框架的全面调研。
> 目的：为 Dawning Agent Framework 的架构决策提供依据。

---

## 目录

1. [概述](#1-概述)
2. [框架目录](#2-框架目录)
3. [详细分析](#3-详细分析)
4. [跨框架对比矩阵](#4-跨框架对比矩阵)
5. [架构模式分析](#5-架构模式分析)
6. [对 Dawning 的关键启示](#6-对-dawning-的关键启示)

---

## 1. 概述

AI Agent 框架领域在 2025-2026 年经历了快速整合。关键趋势：

- **微软整合**：AutoGen → 维护模式；Semantic Kernel Agents + 全新 Microsoft Agent Framework (MAF) 作为企业级继任者。
- **多语言扩展**：Google ADK (Python + Java + Go)、MAF (.NET + Python)、Semantic Kernel (C# + Python + Java)。.NET 生态在大多数框架中仍然被低估。
- **协议标准化**：A2A（Agent-to-Agent）和 MCP（Model Context Protocol）正在成为 Agent 互操作和工具集成的事实标准。
- **工作流即图**：几乎所有生产级框架现在都将 Agent 编排建模为有状态图（LangGraph、MAF Workflows、CrewAI Flows、Pydantic AI Graph）。
- **记忆成熟化**：短期（会话）+ 长期（语义/向量）记忆已成基线；前沿方向是自改进记忆（技能演化、观察记忆）。
- **代码优先 Agent**：smolagents 和 DSPy 代表了 Agent 从发出工具调用 JSON 向编写和执行代码的转变。

**Dawning 的机会**：没有任何 .NET 原生框架同时提供：分布式三面架构 + 技能自演化 + 企业级 DI 模式。这是我们的差异化定位。

---

## 2. 框架目录

### 2.1 第一梯队 — 主流生产级框架（★★★★★）

| # | 框架 | 维护者 | 语言 | Stars | 许可证 | 状态 |
|---|------|--------|------|-------|--------|------|
| 1 | **Microsoft Agent Framework (MAF)** | Microsoft | Python + .NET | 9k | MIT | **活跃**（v1.0 .NET, v1.0 Python） |
| 2 | **Semantic Kernel** | Microsoft | C# + Python + Java | 27.7k | MIT | **活跃**（v1.41+） |
| 3 | **LangGraph** | LangChain | Python + JS/TS | 28.6k | MIT | **活跃**（v1.1.6） |
| 4 | **CrewAI** | CrewAI Inc | Python | 48.2k | MIT | **活跃**（v1.13.0） |
| 5 | **OpenAI Agents SDK** | OpenAI | Python + JS/TS | 20.6k | MIT | **活跃**（v0.13.5） |
| 6 | **Google ADK** | Google | Python + Java + Go | 18.8k | Apache-2.0 | **活跃**（v1.28.1） |

### 2.2 第二梯队 — 重要框架（★★★★）

| # | 框架 | 维护者 | 语言 | Stars | 许可证 | 状态 |
|---|------|--------|------|-------|--------|------|
| 7 | **Agno**（原 Phidata） | Agno AGI | Python | 39.2k | Apache-2.0 | **活跃**（v2.5.14） |
| 8 | **smolagents** | Hugging Face | Python | 26.5k | Apache-2.0 | **活跃**（v1.24.0） |
| 9 | **Pydantic AI** | Pydantic | Python | 16.1k | MIT | **活跃**（v1.77.0） |
| 10 | **DSPy** | Stanford NLP | Python | 33.5k | MIT | **活跃**（v3.1.3） |
| 11 | **Mastra** | Mastra AI（原 Gatsby） | TypeScript | 22.7k | Apache-2.0 + EE | **活跃**（v1.16.0） |
| 12 | **AG2**（AutoGen 分支） | AG2AI | Python | 4.4k | Apache-2.0 | **活跃**（v0.11.5） |

### 2.3 第三梯队 — 研究/利基框架（★★★）

| # | 框架 | 维护者 | 语言 | Stars | 许可证 | 状态 |
|---|------|--------|------|-------|--------|------|
| 13 | **MetaGPT** | FoundationAgents | Python | 66.7k | MIT | **放缓**（v0.8.1，最后发布 2024 年 4 月） |
| 14 | **AutoGen**（原版） | Microsoft | Python + .NET | 56.8k | MIT | **维护模式** → MAF |
| 15 | **LlamaIndex Agents** | LlamaIndex | Python + TS | ~37k | MIT | 活跃（以 RAG 为核心） |
| 16 | **Haystack** | deepset | Python | ~18k | Apache-2.0 | 活跃（以 Pipeline 为核心） |
| 17 | **Camel** | CAMEL-AI | Python | ~20k | Apache-2.0 | 活跃（学术研究） |
| 18 | **SuperAGI** | SuperAGI | Python | ~15k | MIT | 放缓 |

### 2.4 基础设施（非 Agent 框架）

| # | 工具 | 角色 | Stars |
|---|------|------|-------|
| 19 | **LiteLLM** | LLM 网关/路由（100+ 提供商） | 42.4k |
| 20 | **Dify** | LLMOps 平台 + 可视化 Agent 构建器 | ~55k |
| 21 | **LangSmith** | LangChain/LangGraph 可观测性 + 评估 | 商业 |
| 22 | **Pydantic Logfire** | Pydantic AI 的 OpenTelemetry 可观测性 | 商业 |

---

## 3. 详细分析

### 3.1 Microsoft Agent Framework (MAF)

**定位**：AutoGen 的企业级继任者。首个同时官方支持 .NET + Python 的框架。

| 维度 | 详情 |
|------|------|
| **架构** | Agent → Workflow（基于图）→ Hosting。三层：Agent 定义、编排、部署 |
| **多 Agent** | 基于图的工作流，支持流式、检查点、人机协同、时间旅行 |
| **记忆** | Foundry Memory 集成（云端）；通过检查点保存会话状态 |
| **工具** | 原生函数工具、MCP 集成、中间件管道 |
| **分布式** | A2A 协议支持、Azure Functions 托管、Durable Agents、Durable Workflows |
| **可观测性** | 内置 OpenTelemetry 集成 |
| **LLM 支持** | Azure OpenAI、OpenAI、Foundry；可扩展提供商 |
| **独特优势** | 声明式 Agent（YAML/JSON）、DevUI 调试界面、从 SK 和 AutoGen 的迁移路径 |
| **不足** | 非常新（v1.0 刚发布）；示例与 Azure 耦合紧密；社区小于前任 |

**对 Dawning 的启示**：.NET 领域最直接的竞品。研究其工作流图模型和 A2A 集成。声明式 Agent 模式值得考虑。

---

### 3.2 Semantic Kernel

**定位**：微软的模型无关 SDK，用于构建 AI Agent。成熟，企业广泛采用。

| 维度 | 详情 |
|------|------|
| **架构** | 以 Kernel 为中心：Kernel → Plugins → Planners → Agent。插件生态是核心优势 |
| **多 Agent** | 通过插件组合实现多 Agent；Agent 即插件模式 |
| **记忆** | 向量数据库集成（Azure AI Search、Elasticsearch、Chroma、Qdrant 等） |
| **工具** | 插件系统：原生代码函数、Prompt 模板、OpenAPI 规范、MCP |
| **分布式** | 无原生分布式运行时；依赖外部托管 |
| **可观测性** | OpenTelemetry 集成 |
| **LLM 支持** | OpenAI、Azure OpenAI、Hugging Face、NVIDIA NIM、Ollama 等 |
| **独特优势** | 最丰富的 .NET 原生体验；Process Framework 处理复杂业务流程；43 个 NuGet 包 |
| **不足** | Agent 层添加较晚；正在被 MAF 部分取代 |

**对 Dawning 的启示**：研究其 Plugin 架构（最接近我们的 ITool/IToolRegistry）。其 .NET API 设计模式是黄金标准。

---

### 3.3 LangGraph

**定位**：面向有状态 Agent 的底层编排框架。LangChain Agent 底层的"图引擎"。

| 维度 | 详情 |
|------|------|
| **架构** | 受 Pregel 启发的图模型：节点（函数/Agent）+ 边（转换）+ 状态（共享字典）。编译后的图是执行单元 |
| **多 Agent** | 任意 DAG 编排；子图；条件分支；允许循环 |
| **记忆** | 持久执行 + 检查点；短期（线程状态）+ 长期（跨线程存储） |
| **工具** | LangChain 工具生态；MCP 集成 |
| **分布式** | LangSmith Deployment 用于生产托管；检查点支持故障恢复 |
| **可观测性** | LangSmith 集成（追踪、调试、评估） |
| **LLM 支持** | 所有 LangChain 支持的模型（100+） |
| **独特优势** | 持久执行（故障恢复）；任意图节点上的人机协同；时间旅行调试；Deep Agents（新）——规划 + 子 Agent |
| **不足** | 复杂度上限高；简单场景样板代码多；与 LangChain 耦合 |

**对 Dawning 的启示**：其检查点/持久执行模型直接映射到我们的 SC-1.2（运行面）。研究其状态持久化层用于检查点存储设计。

---

### 3.4 CrewAI

**定位**：快速、独立的多 Agent 框架。Stars 数最高的纯 Agent 框架。双模式：Crews（自主）+ Flows（事件驱动）。

| 维度 | 详情 |
|------|------|
| **架构** | 双模式：**Crews**（Agent + Task + Process）自主协作；**Flows**（事件驱动，`@start`/`@listen`/`@router` 装饰器）精确控制 |
| **多 Agent** | 基于角色的 Crew，顺序/层级进程；动态委派 |
| **记忆** | 统一记忆系统（新）；短期 + 长期 + 实体记忆 |
| **工具** | CrewAI Tools 包；任意 Python 函数；LangChain 工具导入 |
| **分布式** | Crew 控制面（商业）用于监控/管理；无原生分布式 Worker 模型 |
| **可观测性** | 内置追踪；Crew 控制面用于企业监控 |
| **LLM 支持** | 通过 LiteLLM：100+ 提供商 |
| **独特优势** | YAML 配置 Agent/任务；Crew AMP 企业套件；事件驱动 Flows 支持 `or_`/`and_` 逻辑运算符 |
| **不足** | 仅 Python；生产功能需要商业控制面；无原生分布式执行 |

**对 Dawning 的启示**：其 Crew（自主）+ Flow（确定性）双模式设计优雅。我们的 Orchestrator 应支持两种模式。YAML 配置 Agent 定义的方式值得研究。

---

### 3.5 OpenAI Agents SDK

**定位**：OpenAI 出品的轻量级、提供商无关的框架。极简 API 设计哲学。

| 维度 | 详情 |
|------|------|
| **架构** | Agent → Runner → Trace。极其简约：Agent（指令 + 工具 + 移交）→ Runner（执行循环） |
| **多 Agent** | Handoff（Agent 间控制权转移）；Agent 即工具模式 |
| **记忆** | Sessions 自动会话历史管理；Redis Session 持久化 |
| **工具** | 函数工具、托管工具、MCP 集成；Guardrails 输入/输出验证 |
| **分布式** | 无原生分布式运行时 |
| **可观测性** | 内置追踪 UI；追踪导出 |
| **LLM 支持** | OpenAI Responses + Chat Completions + 100+ 通过 LiteLLM/any-llm 适配器 |
| **独特优势** | 极致简约（< 100 行即可运行 Agent）；实时语音 Agent；Guardrails 作为一等公民；内置人机协同 |
| **不足** | 无工作流/图引擎；无持久执行；无分布式模型；仅 Python |

**对 Dawning 的启示**：Dawning.Agents 的设计灵感来自此 SDK 的极简风格。Handoff 模式已在我们代码中。Guardrails 概念应成为控制面 Policy Store 的一部分。

---

### 3.6 Google ADK（Agent Development Kit）

**定位**：Google 的代码优先 Agent 框架，针对 Gemini 优化但模型无关。多语言（Python + Java + Go）。

| 维度 | 详情 |
|------|------|
| **架构** | Agent → sub_agents 层级。父 Agent 协调子 Agent。代码优先 + Agent Config（无代码 YAML） |
| **多 Agent** | 层级式 sub_agent 组合；协调者模式 |
| **记忆** | 基于会话的状态管理；Vertex AI 集成用于持久状态 |
| **工具** | Google Search、MCP 工具、OpenAPI 规范、自定义函数；工具确认（HITL） |
| **分布式** | A2A 协议集成；Cloud Run 部署；Vertex AI Agent Engine 扩展 |
| **可观测性** | 内置开发 UI；评估框架（`adk eval`） |
| **LLM 支持** | Gemini（优化），模型无关 |
| **独特优势** | 内置评估框架；A2A 协议原生支持；自定义服务注册表；会话回溯；三种语言 |
| **不足** | Gemini 优化（其他模型为二等公民）；与 Google Cloud 强耦合 |

**对 Dawning 的启示**：其 A2A 协议集成正是我们 SC-1.4 异步契约应兼容的。评估框架和会话回溯是控制面应纳入的特性。

---

### 3.7 Agno（原 Phidata）

**定位**：Agentic 软件的运行时。构建 → 运行 → 管理范式。AgentOS 产品化重点突出。

| 维度 | 详情 |
|------|------|
| **架构** | 三层：**框架**（agents/teams/workflows）+ **运行时**（FastAPI 后端，无状态，会话级隔离）+ **控制面**（AgentOS UI） |
| **多 Agent** | 带技能的团队；协调式多 Agent 系统 |
| **记忆** | 历史感知 Agent；知识库；按用户会话隔离 |
| **工具** | 100+ 集成；MCP 工具；自定义函数 |
| **分布式** | 无状态运行时，水平可扩展；AgentOS 管理 |
| **可观测性** | 原生追踪；AgentOS UI 监控 |
| **LLM 支持** | 所有主流提供商（OpenAI、Anthropic、Google 等） |
| **独特优势** | AgentOS 完整控制面；无状态运行时设计；自学习 Agent（Pal、Dash、Scout、Gcode）；观察记忆 |
| **不足** | 仅 Python；AgentOS 为商业/托管 |

**对 Dawning 的启示**：**架构最接近的同行**。其框架 + 运行时 + 控制面映射到我们的三面架构。观察记忆和自学习 Agent 概念与 Memento-Skills 愿景一致。需深入研究。

---

### 3.8 smolagents（Hugging Face）

**定位**：极简的"用代码思考的 Agent"。Code Agent 编写 Python 动作而非 JSON 工具调用。

| 维度 | 详情 |
|------|------|
| **架构** | CodeAgent（写 Python）+ ToolCallingAgent（写 JSON）。核心逻辑约 1000 行 |
| **多 Agent** | 多 Agent 层级；托管 Agent |
| **记忆** | 对话历史；无原生长期记忆 |
| **工具** | MCP 工具、LangChain 工具、HF Spaces 作为工具、Hub 工具共享 |
| **分布式** | 无原生分布式模型；沙盒执行（E2B、Docker、Modal、Pyodide+Deno） |
| **可观测性** | 最小化；依赖外部日志 |
| **LLM 支持** | transformers、Ollama、HF 推理提供商、LiteLLM、OpenAI、Anthropic |
| **独特优势** | 代码即动作范式（减少 30% 步骤，更高基准分数）；通过 HF Hub 共享 Agent；沙盒执行选项；CLI 工具 |
| **不足** | 记忆有限；无持久化；无企业特性；代码执行安全隐患 |

**对 Dawning 的启示**：代码 Agent 范式值得作为替代执行模式支持。其基准数据（Code Agent 优于 Tool-Call Agent）应纳入我们的 Agent 类型设计考量。

---

### 3.9 Pydantic AI

**定位**："FastAPI 体验" 的 Agent 开发。类型安全、DI 原生、生产级。

| 维度 | 详情 |
|------|------|
| **架构** | Agent[DepsType, OutputType] — 泛型、完全类型化。RunContext 用于 DI。Capability 抽象用于可组合扩展 |
| **多 Agent** | Agent 即插件；图支持复杂编排 |
| **记忆** | 通过 Capability；持久执行用于状态持久化 |
| **工具** | 装饰器函数 + 类型安全 DI；MCP；延迟工具 + 人工审批 |
| **分布式** | 持久执行（API 故障/重启后恢复）；A2A 协议支持 |
| **可观测性** | Pydantic Logfire（OpenTelemetry）；支持替代 OTel 后端 |
| **LLM 支持** | ~30+ 提供商原生支持；自定义模型接口 |
| **独特优势** | 完全静态类型安全；Capability 抽象（工具+钩子+指令的可组合包）；AgentSpec（YAML/JSON Agent 定义）；pydantic_evals（系统化评估）；持久执行 |
| **不足** | 仅 Python；图支持较新；生态小于 LangGraph/CrewAI |

**对 Dawning 的启示**：**API 设计哲学最一致**。其 `Agent[DepsType, OutputType]` 泛型模式 + RunContext DI 正是我们的 DI 优先原则。Capability 抽象是我们打包技能包的方式。研究其类型安全模式用于 C# 实现。

---

### 3.10 DSPy

**定位**："编程——而非提示——大模型"。声明式模块 + 自动 Prompt 优化。

| 维度 | 详情 |
|------|------|
| **架构** | 模块（Predict、ChainOfThought、ReAct）→ 签名（输入/输出 Schema）→ 优化器（Prompt 调优器） |
| **多 Agent** | 非 Agent 中心；聚焦可组合的 LLM 模块 |
| **记忆** | 非核心概念；无状态模块 |
| **工具** | ReAct 模块支持工具；通用函数集成 |
| **分布式** | 无原生分布式模型 |
| **可观测性** | 评估驱动开发；优化追踪 |
| **LLM 支持** | 通过适配层实现模型无关 |
| **独特优势** | **自动 Prompt 优化** —— 定义你想要什么（签名），DSPy 自动找到最佳 Prompt。自改进管道。GEPA 算法（反思式 Prompt 演化优于强化学习） |
| **不足** | 不是严格意义的 Agent 框架；学习曲线陡；与其他框架范式不同 |

**对 Dawning 的启示**：**技能自演化的关键参考**。DSPy 的自动 Prompt 优化和 GEPA 算法（反思式 Prompt 演化）直接指导我们的 Memento-Skills 实现。将声明式意图编译为优化 Prompt 的理念是 SC-7 的基础。

---

### 3.11 Mastra

**定位**：TypeScript 原生 Agent 框架，来自 Gatsby 团队。全栈 Agent 开发。

| 维度 | 详情 |
|------|------|
| **架构** | Agents + Workflows（基于图，`.then()/.branch()/.parallel()`）+ Storage + Memory |
| **多 Agent** | 团队编排；基于工作流的协调 |
| **记忆** | 对话历史；工作记忆；语义召回；观察记忆 |
| **工具** | MCP 服务器（创建 + 消费）；100+ 集成 |
| **分布式** | 部署为独立服务器；多平台部署器 |
| **可观测性** | 内置评估；可观测性包（OTel Bridge） |
| **LLM 支持** | 40+ 提供商通过 AI SDK |
| **独特优势** | TypeScript 原生；MCP 服务器编写；观察记忆（类人召回）；auth/RBAC（企业版）；React/Next.js 集成 |
| **不足** | 仅 TypeScript；企业功能在 EE 许可证之后 |

**对 Dawning 的启示**：其观察记忆概念与我们记忆面的长期知识服务一致。TypeScript 生态上下文不同，但其工作流 API 设计（`.then()/.branch()/.parallel()`）简洁清晰。

---

### 3.12 AG2（AutoGen 分支）

**定位**：AutoGen 的开源社区分支。"AI Agent 的 AgentOS"。志愿者维护。

| 维度 | 详情 |
|------|------|
| **架构** | ConversableAgent 作为构建块；GroupChat 模式；正在走向 beta v1.0 架构 |
| **多 Agent** | 群聊（自动/手动/轮询选择）；蜂群；嵌套聊天；顺序聊天 |
| **记忆** | 对话历史；可教学 Agent |
| **工具** | register_function 模式；MCP 集成；A2A 支持 |
| **分布式** | 无原生分布式运行时 |
| **可观测性** | 基本日志 |
| **LLM 支持** | 通过配置文件；多模型 |
| **独特优势** | 丰富的编排模式（9+ 种模式菜谱）；与 AutoGen 向后兼容 |
| **不足** | 社区维护；正在过渡到新架构；规模小于 MAF |

**对 Dawning 的启示**：其群聊编排模式（AutoPattern、RoundRobinPattern 等）是我们 Orchestrator 设计的有用参考。

---

### 3.13 MetaGPT

**定位**："软件公司即多 Agent 系统"。SOP 驱动的多 Agent 协作。

| 维度 | 详情 |
|------|------|
| **架构** | 基于角色的 Agent（PM、架构师、工程师、QA）遵循 SOP（标准操作流程） |
| **多 Agent** | 预定义角色团队；通过共享环境传递消息；SOP 驱动排序 |
| **记忆** | 共享工作区；基于文档的上下文 |
| **工具** | 代码执行；网络搜索；文件管理 |
| **分布式** | 无原生分布式模型 |
| **可观测性** | 最小化 |
| **LLM 支持** | OpenAI、Azure、Ollama、Groq 等 |
| **独特优势** | `Code = SOP(Team)` 哲学；AFlow（自动 Agent 工作流生成，ICLR 2025 口头报告）；数据解释器 |
| **不足** | 最后发布 v0.8.1（2024 年 4 月）；开发似乎放缓；与软件开发领域强耦合 |

**对 Dawning 的启示**：SOP 即代码的概念对领域特定 Agent 模板有参考价值。AFlow 论文中的自动工作流生成可指导我们的动态编排。

---

## 4. 跨框架对比矩阵

### 4.1 核心能力

| 能力 | MAF | SK | LangGraph | CrewAI | OpenAI SDK | Google ADK | Agno | smolagents | Pydantic AI | DSPy | Mastra |
|------|-----|----|----|--------|---------|---------|------|------------|------------|------|--------|
| **多 Agent** | ✅ 图 | ✅ 插件 | ✅ 图 | ✅ Crew | ✅ Handoff | ✅ 层级 | ✅ 团队 | ✅ 层级 | ✅ 图 | ❌ | ✅ 工作流 |
| **短期记忆** | ✅ | ✅ | ✅ | ✅ | ✅ Session | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| **长期记忆** | ✅ Foundry | ✅ 向量 | ✅ Store | ✅ 统一 | ❌ | ⚠️ Vertex | ✅ | ❌ | ✅ Capability | ❌ | ✅ 语义 |
| **持久执行** | ✅ | ❌ | ✅ | ⚠️ 检查点 | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ 挂起 |
| **MCP 支持** | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| **A2A 协议** | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |
| **人机协同** | ✅ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ 审批 | ❌ | ✅ 延迟 | ❌ | ✅ 挂起 |
| **代码执行** | ⚠️ | ❌ | ⚠️ | ❌ | ❌ | ✅ 沙盒 | ❌ | ✅ 沙盒 | ❌ | ❌ | ❌ |
| **护栏** | ✅ 中间件 | ⚠️ | ⚠️ | ⚠️ | ✅ 原生 | ⚠️ | ✅ | ❌ | ⚠️ | ❌ | ⚠️ |
| **评估框架** | ⚠️ Labs | ❌ | ⚠️ LangSmith | ❌ | ✅ Trace | ✅ `adk eval` | ✅ AgentOS | ❌ | ✅ pydantic_evals | ✅ 核心 | ✅ |
| **自动优化** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ 核心 | ❌ |

### 4.2 语言与生态

| 框架 | Python | .NET/C# | TypeScript | Java | Go | Stars | 贡献者 |
|------|--------|---------|------------|------|-----|-------|--------|
| **MAF** | ✅ | ✅ | ⚠️ UI | ❌ | ❌ | 9k | 121 |
| **Semantic Kernel** | ✅ | ✅ | ⚠️ | ✅ | ❌ | 27.7k | 435 |
| **LangGraph** | ✅ | ❌ | ✅ | ❌ | ❌ | 28.6k | 289 |
| **CrewAI** | ✅ | ❌ | ❌ | ❌ | ❌ | 48.2k | 302 |
| **OpenAI SDK** | ✅ | ❌ | ✅ | ❌ | ❌ | 20.6k | 240 |
| **Google ADK** | ✅ | ❌ | ❌ | ✅ | ✅ | 18.8k | 261 |
| **Agno** | ✅ | ❌ | ❌ | ❌ | ❌ | 39.2k | 428 |
| **smolagents** | ✅ | ❌ | ❌ | ❌ | ❌ | 26.5k | 208 |
| **Pydantic AI** | ✅ | ❌ | ❌ | ❌ | ❌ | 16.1k | 425 |
| **DSPy** | ✅ | ❌ | ❌ | ❌ | ❌ | 33.5k | 394 |
| **Mastra** | ❌ | ❌ | ✅ | ❌ | ❌ | 22.7k | 400 |
| **AG2** | ✅ | ❌ | ❌ | ❌ | ❌ | 4.4k | 179 |

### 4.3 生产就绪度

| 框架 | 分布式 | 水平扩展 | 状态持久化 | OTel | 企业支持 |
|------|--------|----------|-----------|------|----------|
| **MAF** | ✅ A2A + Durable | ✅ Azure | ✅ 检查点 | ✅ | ✅ Microsoft |
| **Semantic Kernel** | ⚠️ 手动 | ⚠️ 手动 | ✅ Process 状态 | ✅ | ✅ Microsoft |
| **LangGraph** | ⚠️ LangSmith Deploy | ✅ Deploy | ✅ 检查点 | ⚠️ LangSmith | ✅ LangChain |
| **CrewAI** | ❌ | ⚠️ 控制面 | ✅ JSONB 检查点 | ✅ 遥测 | ✅ AMP 套件 |
| **OpenAI SDK** | ❌ | ❌ | ⚠️ Redis Sessions | ✅ 追踪 | ❌ |
| **Google ADK** | ✅ A2A | ✅ Cloud Run / Vertex | ✅ Session 存储 | ⚠️ | ✅ Google Cloud |
| **Agno** | ✅ 无状态 RT | ✅ 水平 | ✅ DB 持久化 | ✅ 追踪 | ✅ AgentOS |
| **Pydantic AI** | ✅ 持久执行 | ⚠️ | ✅ 状态持久化 | ✅ Logfire | ⚠️ |

---

## 5. 架构模式分析

### 5.1 Agent 定义模式

| 模式 | 使用框架 | 描述 |
|------|---------|------|
| **代码优先类** | SK、smolagents、AG2 | Agent 定义为类 + 方法/装饰器 |
| **配置对象** | OpenAI SDK、Google ADK、Agno | Agent = `Agent(name, instructions, tools)` |
| **泛型类型化** | Pydantic AI | `Agent[DepsType, OutputType]` 完全类型推断 |
| **YAML/声明式** | CrewAI、MAF、Google ADK、Pydantic AI | Agent 在配置文件中定义，无需代码 |
| **SOP 驱动** | MetaGPT | Agent = 角色 + 预定义 SOP 步骤 |

**Dawning 建议**：以配置对象起步（如 OpenAI SDK 简洁性）+ 泛型类型化（如 Pydantic AI 类型安全）。后续添加 YAML/声明式。

### 5.2 编排模式

| 模式 | 使用框架 | 描述 |
|------|---------|------|
| **顺序管道** | CrewAI、MetaGPT | A → B → C，每个传递输出给下一个 |
| **有状态图 (DAG)** | LangGraph、MAF、Pydantic AI、Mastra | 节点 + 边 + 状态。最灵活 |
| **层级子 Agent** | Google ADK、smolagents | 父 Agent 委派给子 Agent |
| **移交链** | OpenAI SDK、MAF | Agent 将控制权移交给另一个 Agent |
| **群聊** | AG2、AutoGen | 多个 Agent 在共享对话中 |
| **Crew + Flow** | CrewAI | 自主 Crew 嵌入确定性 Flow |
| **动态 (LLM 决策)** | LangGraph、AG2 | LLM 决定下一步调度哪个 Agent |

**Dawning 建议**：实现有状态图作为核心原语（如 LangGraph）。将顺序、移交和层级构建为图预设。通过基于 LLM 的边决策支持动态路由。

### 5.3 记忆架构模式

| 模式 | 使用框架 | 描述 |
|------|---------|------|
| **仅消息列表** | OpenAI SDK、smolagents、AG2 | 内存中的对话历史 |
| **会话 + 向量存储** | SK、LangGraph、CrewAI、Google ADK | 短期会话 + 长期向量检索 |
| **双层（工作 + 长期）** | Agno、Mastra | 显式工作记忆 + 语义长期记忆 |
| **观察记忆** | Agno、Mastra | Agent 从交互中学习，逐步建立理解 |
| **自动优化** | DSPy | Prompt 根据评估自动优化 |
| **Scope 隔离** | Dawning（计划中）、AgentOS | 四级命名空间：global / team / session / private |

**Dawning 建议**：我们的双层 + Scope 隔离设计领先于大多数框架。添加观察记忆（自动从交互中提取模式）作为 Memento-Skills 的基础。

### 5.4 工具集成模式

| 模式 | 使用框架 | 描述 |
|------|---------|------|
| **装饰器函数** | Pydantic AI、OpenAI SDK、SK、Google ADK | `@tool` 装饰器标注 Python/C# 函数 |
| **MCP 服务器** | 大多数（2025+） | 连接到任何 MCP 兼容的工具服务器 |
| **Agent 即工具** | OpenAI SDK、SK、MAF | 将 Agent 包装为另一个 Agent 的工具 |
| **代码执行** | smolagents、Google ADK、AG2 | Agent 编写和执行代码作为动作 |

**Dawning 建议**：支持全部四种模式。装饰器函数用于简单工具，MCP 用于生态互操作，Agent 即工具用于多 Agent，代码执行作为高级模式。

### 5.5 分布式运行时模式

| 模式 | 使用框架 | 描述 |
|------|---------|------|
| **仅进程内** | OpenAI SDK、smolagents、DSPy | 单进程，无分布式 |
| **云托管** | LangGraph (Deploy)、CrewAI (AMP) | 供应商管理基础设施 |
| **持久任务** | MAF (Durable Agents)、LangGraph | 检查点 + 跨进程重启恢复工作流 |
| **A2A 协议** | MAF、Google ADK、Pydantic AI | 标准化 Agent 间通信 |
| **无状态运行时** | Agno | FastAPI 后端，会话级隔离，水平可扩展 |

**Dawning 建议**：结合持久任务（弹性）+ A2A 协议（互操作）+ 无状态 Worker（扩展性）。这就是我们 SC-1.2 运行面的设计。

---

## 6. 对 Dawning 的关键启示

### 6.1 竞争优势（差异化定位）

| 优势 | 为何独一无二 |
|------|------------|
| **.NET 原生三面分布式架构** | MAF 和 SK 是 .NET 但没有三面模型。其他都是 Python/TS |
| **技能自演化（Memento-Skills）** | 仅 DSPy 做自动优化，但针对 Prompt 而非技能。无框架有 Agent 设计 Agent 的能力 |
| **纯 DI 架构** | SK 使用 Kernel 抽象；MAF 接近但更新。我们可以是最干净的 DI 优先设计 |
| **Scope 隔离记忆 + RBAC** | Agno 有会话隔离但没有四级 Scope 记忆 + 策略执行 |

### 6.2 必备功能（2026 年基线）

基于本次调研，以下是任何严肃 Agent 框架的**最低要求**：

1. **MCP 支持** —— 通用工具集成协议
2. **A2A 协议** —— Agent 间互操作标准
3. **持久执行** —— 检查点，故障恢复
4. **人机协同** —— 审批工作流，延迟工具
5. **基于图的编排** —— 支持条件边的 DAG
6. **OpenTelemetry** —— 分布式追踪，可观测性
7. **多提供商 LLM** —— 至少：OpenAI、Azure OpenAI、Ollama、Anthropic
8. **评估框架** —— 基准测试、回归测试、质量门禁

### 6.3 设计灵感（从各框架中精选）

| 来源框架 | 借鉴内容 | 映射到 Dawning |
|---------|---------|---------------|
| **Pydantic AI** | `Agent[Deps, Output]` 泛型类型 + Capability 抽象 | IAgent 泛型、技能包 |
| **OpenAI Agents SDK** | 极简 API、Handoff、Guardrails | Agent 定义、Handoff、Policy Store |
| **LangGraph** | 持久执行、检查点、状态图 | 运行面检查点存储 |
| **Agno** | 框架 + 运行时 + 控制面分离 | 三面架构 |
| **CrewAI** | Crew（自主）+ Flow（确定性）双模式 | Orchestrator 预设 |
| **DSPy** | 自动 Prompt 优化、GEPA 算法 | Memento-Skills 演化引擎 |
| **Google ADK** | A2A 集成、评估框架、会话回溯 | Agent 间协议、评估触发器 |
| **MAF** | .NET 图工作流、中间件管道、声明式 Agent | 工作流引擎、中间件、Agent 配置 |
| **Semantic Kernel** | Plugin 架构、NuGet 打包、.NET 模式 | IToolRegistry、包结构 |
| **smolagents** | 代码即动作范式（减少 30% 步骤） | CodeAgent 执行模式 |

### 6.4 需避免的反模式

| 反模式 | 出现在 | 教训 |
|--------|--------|------|
| LLM 提供商锁定 | Google ADK（Gemini 优先） | 始终先设计提供商无关 |
| 框架耦合 | LangGraph ↔ LangChain | 保持抽象与实现清晰分离 |
| 单体单进程 | OpenAI SDK、smolagents | 从第一天就为分布式设计，即使 MVP 是进程内的 |
| 前期概念过多 | LangGraph（节点、边、状态、通道） | 从简单开始，逐步增加复杂性 |
| 商业功能锁定 | CrewAI AMP、Agno AgentOS | 保持所有核心分布式功能开源 |

---

*文档版本：1.0*
*最后更新：2026-04-07*
*作者：AI 辅助分析，服务于 Dawning Agent Framework*
