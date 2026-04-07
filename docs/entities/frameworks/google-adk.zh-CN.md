# Google ADK (Agent Development Kit) 详细分析

> Google 的代码优先 Agent 框架，原生 A2A 协议支持，多语言（Python + Java + Go）。

---

## 基本信息

| 属性 | 值 |
|------|-----|
| **官方名称** | Google Agent Development Kit (ADK) |
| **维护者** | Google |
| **仓库** | https://github.com/google/adk-python |
| **文档** | https://google.github.io/adk-docs/ |
| **语言** | Python（主）+ Java + Go |
| **许可证** | Apache-2.0 |
| **Stars** | 18.8k |
| **贡献者** | 261 |
| **最新版本** | v1.28.1 |
| **LLM 优化** | Gemini（优化）/ 模型无关 |

---

## 1. 定位与背景

Google ADK 是 Google 在 2025 年推出的代码优先 Agent 开发框架。它原生优化了 Gemini 模型的能力，但也支持其他 LLM 提供商。ADK 是首个原生集成 A2A（Agent-to-Agent）协议的框架之一。

**核心理念**：
- **代码优先**：开发者用代码定义 Agent，而非低代码平台
- **模块化层级**：Agent 通过 `sub_agents` 组成层级结构
- **A2A 原生**：与其他框架/平台的 Agent 互操作
- **评估驱动**：内置 `adk eval` 评估框架

**Google 生态位置**：ADK → Vertex AI Agent Engine → Google Cloud

---

## 2. 架构设计

### 2.1 层级式 Agent 架构

```
┌─────────────────────────────────────┐
│           Root Agent                │
│  (Coordinator / Orchestrator)       │
│                                     │
│  ┌──────────────┐ ┌──────────────┐  │
│  │  Sub-Agent A │ │  Sub-Agent B │  │
│  │  (Researcher)│ │  (Writer)    │  │
│  │              │ │              │  │
│  │  ┌─────────┐│ │  ┌─────────┐ │  │
│  │  │Sub-Agent││ │  │  Tools   │ │  │
│  │  │  A.1    ││ │  └─────────┘ │  │
│  │  └─────────┘│ │              │  │
│  └──────────────┘ └──────────────┘  │
├─────────────────────────────────────┤
│        Session Management            │
│   (State, History, Context)          │
├─────────────────────────────────────┤
│     Model Layer (Gemini / Others)    │
└─────────────────────────────────────┘
```

### 2.2 Agent 定义

```python
from google.adk.agents import Agent

# 定义子 Agent
researcher = Agent(
    name="researcher",
    model="gemini-2.0-flash",
    description="Expert at finding information",
    instruction="You are a research specialist...",
    tools=[google_search, web_scraper],
)

writer = Agent(
    name="writer",
    model="gemini-2.0-flash",
    description="Expert at writing reports",
    instruction="You write comprehensive reports...",
)

# 定义根 Agent（协调者）
coordinator = Agent(
    name="coordinator",
    model="gemini-2.0-flash",
    description="Coordinates research and writing tasks",
    instruction="Route tasks to the appropriate agent.",
    sub_agents=[researcher, writer],  # 层级组合
)
```

### 2.3 双模式定义

| 模式 | 描述 | 适用场景 |
|------|------|---------|
| **代码优先** | Python/Java/Go 代码定义 | 开发者、复杂逻辑 |
| **Agent Config** | YAML/JSON 无代码定义 | 低代码、快速原型 |

---

## 3. 核心特性

### 3.1 工具系统

ADK 提供丰富的工具类型：

| 工具类型 | 描述 |
|---------|------|
| **Google Search** | 内置 Google 搜索能力 |
| **MCP 工具** | Model Context Protocol 集成 |
| **OpenAPI 工具** | 从 OpenAPI 规范自动生成 |
| **自定义函数** | 任意 Python/Java/Go 函数 |
| **工具确认** | HITL 工具执行审批 |

### 3.2 A2A 协议（Agent-to-Agent）

ADK 是 A2A 协议的原生支持者之一：

```
Agent A (ADK)  ←── A2A Protocol ──→  Agent B (MAF)
                                      Agent C (Pydantic AI)
                                      Agent D (Any A2A-compatible)
```

**A2A 能力**：
- 跨框架 Agent 发现
- 标准化通信格式
- Agent 能力声明
- 异步任务交换

### 3.3 会话管理

| 能力 | 描述 |
|------|------|
| **Session State** | 会话级状态管理 |
| **History** | 自动会话历史 |
| **Session Rewind** | 会话回溯到任意点 |
| **Context Window** | 智能上下文裁剪 |

### 3.4 评估框架

ADK 独有的内置评估系统：

```bash
# 运行评估
adk eval my_agent --dataset eval_dataset.json

# 评估维度
# - 任务完成率
# - 响应质量
# - 工具使用准确性
# - 延迟和成本
```

### 3.5 开发 UI

内置的可视化开发界面：
- 实时 Agent 交互
- 会话历史查看
- 工具调用追踪
- Agent 状态检查

### 3.6 人机协同

| 模式 | 描述 |
|------|------|
| **工具确认** | 工具执行前要求人工确认 |
| **结果审查** | Agent 输出的人工审查 |
| **中断 & 恢复** | 暂停执行等待人工输入 |

---

## 4. 多语言支持

### 4.1 Python（主要）

```python
from google.adk.agents import Agent
agent = Agent(name="assistant", model="gemini-2.0-flash", ...)
```

### 4.2 Java

```java
Agent agent = Agent.builder()
    .name("assistant")
    .model("gemini-2.0-flash")
    .build();
```

### 4.3 Go

```go
agent := adk.NewAgent("assistant", adk.WithModel("gemini-2.0-flash"))
```

**注意**：Python SDK 最成熟，Java 和 Go 正在追赶。

---

## 5. 部署与扩展

### 5.1 本地开发

```bash
# 安装
pip install google-adk

# 启动开发服务器（带 UI）
adk web
```

### 5.2 Cloud Run 部署

直接部署到 Google Cloud Run，支持容器化和自动扩展。

### 5.3 Vertex AI Agent Engine

Google Cloud 的托管 Agent 运行时：
- 自动扩展
- 负载均衡
- 监控和日志
- 企业级 SLA

### 5.4 自定义服务注册

Agent 可以注册到自定义服务注册表，支持发现和路由。

---

## 6. LLM 提供商

| 提供商 | 支持级别 |
|--------|---------|
| **Gemini** | ✅ 一等公民（优化） |
| **OpenAI** | ✅ 适配器 |
| **Anthropic** | ✅ 适配器 |
| **其他** | ✅ 通过 LiteLLM |

**注意**：Gemini 模型获得最佳性能和功能支持。其他模型为二等公民。

---

## 7. 与竞品对比

### vs. MAF
| 维度 | Google ADK | MAF |
|------|-----------|-----|
| 生态锁定 | Google Cloud | Azure |
| 语言 | Python + Java + Go | Python + .NET |
| A2A | ✅ 原生 | ✅ 原生 |
| 评估 | ✅ `adk eval` | ⚠️ Labs |
| 部署 | Cloud Run / Vertex | Azure Functions |

### vs. OpenAI Agents SDK
| 维度 | Google ADK | OpenAI SDK |
|------|-----------|------------|
| 复杂度 | 中等 | 极低 |
| 多 Agent | 层级式 | Handoff 式 |
| 评估 | ✅ 内置 | ❌ |
| A2A | ✅ | ❌ |
| 语音 | ❌ | ✅ |

---

## 8. 优势与不足

### 优势
1. **A2A 协议原生支持** — 跨框架 Agent 互操作
2. **内置评估框架** — `adk eval` 系统化评估
3. **三语言支持** — Python + Java + Go
4. **开发 UI** — 可视化开发和调试
5. **会话回溯** — 回退到任意会话点
6. **Agent Config** — 无代码 YAML 定义
7. **工具确认** — 原生 HITL 支持
8. **Google Cloud 集成** — Vertex AI、Cloud Run 无缝部署

### 不足
1. **Gemini 优化** — 其他模型为二等公民
2. **Google Cloud 强耦合** — 生产部署依赖 Google 生态
3. **无 .NET 支持** — 无法满足 .NET 开发者
4. **无持久执行** — 不如 LangGraph 的检查点机制
5. **社区相对较新** — 2025 年才推出
6. **Java/Go SDK 未完全成熟** — 功能落后于 Python

---

## 9. 对 Dawning 的启示

| 借鉴点 | 详情 | 映射到 Dawning |
|--------|------|---------------|
| A2A 协议集成 | 原生 Agent 间互操作标准 | SC-1.4 异步契约 |
| 评估框架 | `adk eval` 内置评估 | 控制面评估触发器 |
| 层级式 Agent | sub_agents 树形组合 | Agent 组合模式 |
| 会话回溯 | Session Rewind | 调试/审计能力 |
| Agent Config | YAML 无代码定义 | 声明式 Agent 配置 |
| 工具确认 | HITL 工具审批 | 人机协同设计 |
| 开发 UI | 可视化交互界面 | DevUI 开发工具 |

**关键洞察**：

1. **A2A 协议是必须的**。Google ADK 和 MAF 都已原生支持。Dawning 必须在 Month 1 就设计 A2A 兼容的通信层，否则将无法与其他框架的 Agent 互操作。

2. **内置评估是竞争优势**。大多数框架没有原生评估能力。Dawning 的控制面应包含类似 `adk eval` 的评估触发器和基准测试系统（SC-1.4）。

3. **层级式 Agent** 是最自然的多 Agent 组织方式。Dawning 的 Agent 注册表应支持树形层级结构，不仅是扁平列表。

---

## 10. 源码结构解析

### 10.1 仓库地址

https://github.com/google/adk-python

### 10.2 源码目录 (`src/google/adk/`)

Google ADK 拥有所有框架中**最丰富的模块划分**：

```
src/google/adk/
├── agents/                # 🔵 Agent 核心
│   └── (Agent 基类、LLMAgent、层级式 sub_agents)
├── flows/                 # 🟢 执行流引擎
│   └── (LLM 调用流、工具调用流、回调钩子)
├── sessions/              # 💾 会话管理
│   └── (InMemorySessionService, VertexAI Session)
├── memory/                # 🧠 记忆系统
│   └── (短期/长期记忆、API 客户端)
├── tools/                 # 🛠️ 工具系统
│   └── (函数工具、MCP 工具、Google Search、OpenAPI)
├── skills/                # 📚 技能系统
│   └── (Script 技能、GCS 访问)
├── models/                # 🟣 模型抽象
│   └── (Gemini、OpenAI、自定义模型、Agent Registry)
├── a2a/                   # 🟠 A2A 协议
│   └── (A2AAgentExecutor、传输层)
├── auth/                  # 🔒 认证
│   └── (OAuth、Service Account、Consent)
├── evaluation/            # 📊 评估框架
│   └── (adk eval 实现、基准测试)
├── telemetry/             # 📈 OpenTelemetry 可观测性
│   └── (Span、追踪、Agent Registry 关联)
├── artifacts/             # 📁 制品管理
│   └── (文件输出、持久化)
├── events/                # 📨 事件系统
│   └── (Event 模型、Pydantic 定义)
├── errors/                # ❌ 错误定义
├── features/              # ✨ 功能开关
│   └── (Session 浅拷贝等配置)
├── planners/              # 🗺️ 规划器
│   └── (PlanReAct 规划器)
├── optimization/          # ⚡ 优化系统
│   └── (数据类型、基类)
├── platform/              # ☁️ 平台集成
│   └── (Durable Runtime 支持)
├── plugins/               # 🔌 插件系统
│   └── (BigQuery Logger 等)
├── integrations/          # 🔗 第三方集成
│   └── (Secret Manager 等)
├── code_executors/        # 💻 代码执行
│   └── (K8s Sandbox、本地执行)
├── environment/           # 🌍 环境管理
├── dependencies/          # 📦 依赖声明
├── apps/                  # 🖥️ 应用层
│   └── (Compaction、上下文管理)
├── examples/              # 📘 内置示例
├── cli/                   # 🛠️ CLI 工具（adk web/eval/deploy）
├── utils/                 # 🔧 工具函数
├── runners.py             # 🔵 Runner 执行器
├── version.py             # 版本
├── __init__.py            # 导出
└── py.typed               # 类型标记
```

### 10.3 核心模块分析

| 模块 | 职责 | 独特性 |
|------|------|-------|
| `agents/` | Agent 类 + 层级 sub_agents | 层级式组合 |
| `flows/` | LLM 交互流程控制 | 回调式执行流 |
| `sessions/` | 会话状态管理 | Vertex AI 集成 |
| `a2a/` | Agent-to-Agent 协议 | 原生 A2A |
| `evaluation/` | 评估框架 | 内置 `adk eval` |
| `skills/` | 技能包 | Script 执行 |
| `planners/` | 任务规划 | PlanReAct |
| `optimization/` | 性能优化 | 数据类型优化 |
| `platform/` | 平台抽象 | Durable Runtime |
| `code_executors/` | 代码执行沙盒 | K8s Sandbox |

### 10.4 架构洞察

1. **模块数量最多**：~25 个顶层模块，远超其他框架 — 全功能设计
2. **A2A 独立包**：`a2a/` 作为独立模块，包含 AgentExecutor 和传输层
3. **评估框架内置**：`evaluation/` 直接集成在核心包中，而非独立工具
4. **技能系统**：`skills/` 支持 Script 技能执行和 GCS 文件访问
5. **规划器**：`planners/` 包含 PlanReAct 规划器 — 大多数框架没有内置规划
6. **代码执行沙盒**：`code_executors/` 支持 K8s Agent Sandbox — 企业级隔离
7. **平台抽象层**：`platform/` 提供 Durable Runtime 支持 — 正在补齐持久执行
8. **Auth 独立**：`auth/` 包含完整的认证系统（OAuth、Consent） — 企业级安全

---

*文档版本：1.1 | 最后更新：2026-04-07*
