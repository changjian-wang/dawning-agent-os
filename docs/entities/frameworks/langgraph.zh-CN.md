# LangGraph 详细分析

> 面向有状态 Agent 的底层编排框架，以 Pregel 图模型为核心的持久执行引擎。

---

## 基本信息

| 属性 | 值 |
|------|-----|
| **官方名称** | LangGraph |
| **维护者** | LangChain Inc. |
| **仓库** | https://github.com/langchain-ai/langgraph |
| **文档** | https://docs.langchain.com/oss/python/langgraph/ |
| **语言** | Python（99.4%）+ JS/TS（LangGraph.js 独立仓库） |
| **许可证** | MIT |
| **Stars** | 28.6k |
| **贡献者** | 289 |
| **最新版本** | v1.1.6 |
| **发布次数** | 491 |
| **灵感来源** | Google Pregel、Apache Beam、NetworkX |

---

## 1. 定位与背景

LangGraph 是 LangChain 团队推出的底层有状态 Agent 编排框架。不同于 LangChain 的高层抽象，LangGraph 专注于提供图模型的执行引擎，让开发者对 Agent 的控制流、状态管理和执行持久性拥有完全控制权。

**核心理念**：Agent 编排 = 有状态图的执行

**信任客户**：Klarna、Replit、Elastic 等。

---

## 2. 架构设计

### 2.1 图模型

LangGraph 的核心是受 Google Pregel 启发的图执行模型：

```
┌─────────────────────────────────────────┐
│              State Graph                 │
│                                         │
│   ┌──────┐    ┌──────┐    ┌──────┐     │
│   │Node A│───→│Node B│───→│Node C│     │
│   └──────┘    └──┬───┘    └──────┘     │
│                  │                       │
│                  ▼                       │
│              ┌──────┐                   │
│              │Node D│                   │
│              └──────┘                   │
│                                         │
│   State: { messages, context, ... }     │
├─────────────────────────────────────────┤
│           Checkpoint Store               │
│   (SQLite / PostgreSQL / Custom)         │
└─────────────────────────────────────────┘
```

**三大基本要素**：
1. **节点（Nodes）**：函数或 Agent，接收状态、返回更新
2. **边（Edges）**：节点间的转换，支持条件边
3. **状态（State）**：共享字典，在节点间传递和累积

### 2.2 编译图

图在运行前需要**编译**，编译后的图是不可变的执行单元：

```python
from langgraph.graph import StateGraph, START, END

# 定义状态
class AgentState(TypedDict):
    messages: list
    next_agent: str

# 构建图
graph = StateGraph(AgentState)
graph.add_node("researcher", researcher_agent)
graph.add_node("writer", writer_agent)
graph.add_edge(START, "researcher")
graph.add_conditional_edges("researcher", route_function)
graph.add_edge("writer", END)

# 编译
app = graph.compile(checkpointer=memory)
```

### 2.3 子图（Subgraphs）

支持图的嵌套组合：
- 一个节点可以是另一个编译好的图
- 支持独立状态空间
- 支持跨子图的状态共享

---

## 3. 核心特性

### 3.1 持久执行（Durable Execution）

LangGraph 的核心差异化特性：

| 能力 | 描述 |
|------|------|
| **检查点** | 每次节点执行后自动保存状态 |
| **故障恢复** | 从最后一个检查点自动恢复 |
| **进程重启** | 跨进程重启保持工作流状态 |
| **长时间运行** | 支持运行数小时甚至数天的工作流 |

```python
# 使用检查点
from langgraph.checkpoint.sqlite import SqliteSaver

memory = SqliteSaver.from_conn_string(":memory:")
app = graph.compile(checkpointer=memory)

# 执行时提供线程 ID
config = {"configurable": {"thread_id": "thread-1"}}
result = app.invoke(input, config)
```

### 3.2 人机协同（Human-in-the-Loop）

在图的任意节点插入人工干预：

| 模式 | 描述 |
|------|------|
| **中断前** | 节点执行前暂停，等待人工确认 |
| **中断后** | 节点执行后暂停，人工审查结果 |
| **编辑状态** | 暂停时修改图的状态 |
| **时间旅行** | 回退到任意检查点重新执行 |

```python
# 在节点前中断
app = graph.compile(
    checkpointer=memory,
    interrupt_before=["sensitive_action"]
)
```

### 3.3 全面记忆系统

| 记忆类型 | 机制 | 用途 |
|---------|------|------|
| **短期记忆** | 线程状态（Thread State） | 当前对话上下文 |
| **长期记忆** | 跨线程存储（Cross-thread Store） | 跨会话的知识积累 |
| **检查点** | 状态快照 | 执行历史和恢复 |

### 3.4 Deep Agents（新）

2026 年新推出的高级 Agent 模式：
- 规划能力（Planning）
- 子 Agent 委派（Sub-agent delegation）
- 文件系统操作
- 适合复杂多步骤任务

### 3.5 流式传输

| 流式模式 | 描述 |
|---------|------|
| **节点输出流** | 每个节点完成后流式传输结果 |
| **Token 流** | LLM 生成过程中逐 Token 流式 |
| **事件流** | 图执行过程中的事件流 |

---

## 4. LangGraph 生态系统

### 4.1 与 LangChain 的关系

```
LangChain 生态：

LangGraph ←── 底层编排引擎
   │
LangChain ←── 集成库 + 可组合组件
   │
LangSmith ←── 可观测性 + 评估 + 部署
   │
Deep Agents ←── 高级 Agent 模式（规划 + 子 Agent）
```

**重要说明**：LangGraph 可独立于 LangChain 使用，但与 LangChain 生态集成时能力最强。

### 4.2 LangSmith 集成

| 能力 | 描述 |
|------|------|
| **追踪** | 可视化图执行轨迹 |
| **调试** | 状态转换详细日志 |
| **评估** | Agent 行为评估框架 |
| **部署** | 生产级部署平台 |
| **Studio** | 可视化 Agent 原型设计 |

### 4.3 LangSmith Deployment

用于生产部署的托管平台：
- 可扩展基础设施
- 为有状态长时间工作流设计
- Agent 发现、重用、配置
- 团队间共享

---

## 5. 工具集成

| 类型 | 描述 |
|------|------|
| **LangChain Tools** | 完整的 LangChain 工具生态 |
| **MCP 集成** | Model Context Protocol 工具 |
| **自定义函数** | 任意 Python 函数 |
| **API 调用** | 通过 LangChain 连接器 |

---

## 6. 部署模式

### 6.1 本地开发

```python
# 简单内存检查点
from langgraph.checkpoint.memory import MemorySaver
app = graph.compile(checkpointer=MemorySaver())
```

### 6.2 生产部署

| 选项 | 描述 |
|------|------|
| **LangSmith Deployment** | 托管平台（推荐） |
| **PostgreSQL 检查点** | 自托管 + 持久化 |
| **自定义检查点** | 实现 CheckpointSaver 接口 |

---

## 7. 与竞品对比

### vs. MAF
| 维度 | LangGraph | MAF |
|------|-----------|-----|
| 语言 | Python + JS | Python + .NET |
| 图模型 | Pregel 启发 | 自有图模型 |
| 持久执行 | ✅ 核心特性 | ✅ Durable Agents |
| 部署 | LangSmith Deploy | Azure Functions |
| 生态 | LangChain 绑定 | Azure 绑定 |

### vs. CrewAI
| 维度 | LangGraph | CrewAI |
|------|-----------|--------|
| 抽象级别 | 低层（图原语） | 高层（Crew/Agent/Task） |
| 灵活性 | 极高 | 中等（有约束） |
| 学习曲线 | 较陡 | 较平 |
| 速度 | 基准 | 5.76x 更快（特定场景） |

---

## 8. 优势与不足

### 优势
1. **持久执行** — 最成熟的检查点/恢复机制
2. **极致灵活** — 任意 DAG、条件边、循环、子图
3. **人机协同** — 图任意节点的中断/编辑/回退
4. **时间旅行** — 回退到任意检查点重新执行
5. **生态成熟** — 491 次发布、289 位贡献者
6. **Deep Agents** — 复杂任务的规划 + 子 Agent

### 不足
1. **LangChain 耦合** — 虽可独立使用，但集成时更佳
2. **复杂度高** — 简单场景的样板代码多
3. **Python 中心** — .NET 无原生支持
4. **无原生分布式** — 需 LangSmith Deploy 或自行实现
5. **概念多** — 节点、边、状态、通道、检查点、编译...

---

## 9. 对 Dawning 的启示

| 借鉴点 | 详情 | 映射到 Dawning |
|--------|------|---------------|
| 检查点存储 | 可插拔的 CheckpointSaver 接口 | SC-1.2 持久检查点存储 |
| 图编译模式 | 构建图 → 编译 → 执行 | Orchestrator 图模型 |
| 条件边 | 基于状态的动态路由 | 动态编排 |
| 子图 | 图的嵌套组合 | Agent 组合模式 |
| 中断机制 | interrupt_before / interrupt_after | 人机协同设计 |
| 时间旅行 | 检查点回退 + 重新执行 | 调试和审计 |
| 线程 ID | 多线程状态隔离 | 会话隔离 |

**关键洞察**：LangGraph 的检查点系统是所有框架中最完善的。Dawning 的运行面应采用类似的可插拔 CheckpointStore 设计（InMemory → SQLite → PostgreSQL → Redis）。

---

## 10. 源码结构解析

### 10.1 仓库地址

https://github.com/langchain-ai/langgraph

### 10.2 库目录 (`libs/`)

LangGraph 采用 monorepo，每个库是独立可发布的 Python 包：

```
libs/
├── langgraph/                  # 🔵 核心库（langgraph 主包）
│   └── langgraph/
│       ├── graph/              # 图定义（StateGraph, MessageGraph）
│       ├── pregel/             # Pregel 执行引擎（核心调度器）
│       ├── channels/           # 状态通道（消息累积、值覆盖等）
│       ├── managed/            # 托管值（跨节点共享状态）
│       ├── store/              # 长期存储接口
│       └── types.py            # 核心类型定义
├── checkpoint/                 # 💾 检查点核心抽象
│   └── langgraph/checkpoint/
│       ├── base.py             # BaseCheckpointSaver 接口
│       ├── memory.py           # MemorySaver（内存实现）
│       └── serde/              # 序列化/反序列化
├── checkpoint-sqlite/          # 💾 SQLite 检查点实现
│   └── langgraph/checkpoint/sqlite/
├── checkpoint-postgres/        # 💾 PostgreSQL 检查点实现
│   └── langgraph/checkpoint/postgres/
├── checkpoint-conformance/     # 🧪 检查点一致性测试套件
├── prebuilt/                   # 🟢 预构建组件
│   └── (create_react_agent, ToolNode 等高层抽象)
├── cli/                        # 🛠️ CLI 工具
├── sdk-py/                     # 📦 Python SDK（客户端）
└── sdk-js/                     # 📦 JavaScript SDK（客户端）
```

### 10.3 核心模块分析

| 模块 | 职责 | 关键类 |
|------|------|-------|
| `graph/` | 图定义和编译 | `StateGraph`, `MessageGraph`, `CompiledGraph` |
| `pregel/` | Pregel 引擎执行 | `Pregel`, `PregelNode`, `PregelLoop` |
| `channels/` | 状态通道 | `LastValue`, `BinaryOperator`, `Topic` |
| `checkpoint/` | 检查点抽象 | `BaseCheckpointSaver`, `Checkpoint`, `CheckpointMetadata` |
| `prebuilt/` | 高层预构建 | `create_react_agent`, `ToolNode`, `InjectedState` |

### 10.4 架构洞察

1. **检查点三件套**：`checkpoint`（抽象）+ `checkpoint-sqlite`（开发）+ `checkpoint-postgres`（生产）— 经典的可插拔存储模式
2. **checkpoint-conformance**：检查点一致性测试套件，确保所有实现行为一致 — Dawning 应借鉴此模式
3. **Pregel 引擎**：核心调度器在 `pregel/` 目录，是整个框架的心脏
4. **通道系统**：`channels/` 定义状态如何在节点间传递和聚合，这是 LangGraph 独有的概念
5. **预构建分离**：高层 API（如 `create_react_agent`）在独立的 `prebuilt/` 包中，核心保持精简
6. **SDK 双语言**：同时提供 Python 和 JS 客户端 SDK，用于连接 LangSmith 部署

---

*文档版本：1.1 | 最后更新：2026-04-07*
