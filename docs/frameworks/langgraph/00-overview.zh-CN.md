---
framework: langgraph
version: v1.1.x (2026-04 快照)
type: overview
repo: https://github.com/langchain-ai/langgraph
tags: [langgraph, pregel, state-graph, python, typescript]
created: 2026-04-18
updated: 2026-04-18
status: active
---

# LangGraph — 00 Overview

> 面向**有状态 Agent** 的底层编排引擎；Pregel 图模型 + Checkpoint 持久化 + Interrupt HITL 是三大招牌。
> 本文是源码解剖库对 LangGraph 的入口：介绍定位、仓库布局、版本节奏与依赖。
> 更细的模块切分见 [[01-architecture]]。

---

## 1. 定位

LangGraph 是 LangChain 团队 2024 年推出、2025 年走向 1.0 的 **agent orchestration framework**，核心卖点：

- **Graph as first-class** —— 显式建模节点 / 边 / 状态，拒绝"黑箱 agent"
- **Durable execution** —— 任意一步可 checkpoint、可 resume、可时间旅行
- **HITL native** —— `interrupt()` / `Command` 原语内建人机协作
- **Streaming 深度** —— token / step / update / custom 四种流式事件
- **与 LangChain 解耦** —— 运行时不强依赖 LangChain（LCEL 是另一条线）

对比定位：

| 框架 | 主要抽象 | 与 LangGraph 差异 |
|------|---------|-------------------|
| OpenAI Agents SDK | Loop + Handoff | LangGraph 更偏图 / 持久化 |
| MAF (Microsoft) | Workflow + Agent | LangGraph 更轻、面向开发者 |
| CrewAI | Crew / Role | LangGraph 更底层 |
| Temporal | Durable workflow | LangGraph 聚焦 LLM 场景、非通用工作流引擎 |

---

## 2. 仓库结构

仓库：<https://github.com/langchain-ai/langgraph>

顶层关键目录（2026-04 快照）：

```
libs/
├── langgraph/               # 主包：Pregel / StateGraph / compiled graph
│   └── langgraph/
│       ├── graph/           # StateGraph / MessageGraph / constants
│       ├── pregel/          # 执行引擎（BSP 风）
│       ├── channels/        # Channel 抽象（LastValue/Topic/BinaryOperator/...）
│       ├── checkpoint/      # Checkpoint 基类与序列化
│       ├── prebuilt/        # create_react_agent 等开箱模板
│       ├── types.py         # Command / Send / Interrupt / StreamMode
│       └── constants.py
├── checkpoint/              # 内存实现 (BaseCheckpointSaver)
├── checkpoint-postgres/     # Postgres checkpoint
├── checkpoint-sqlite/       # SQLite checkpoint
├── checkpoint-duckdb/       # DuckDB checkpoint
├── langgraph-supervisor/    # 多 Agent supervisor 模板
├── langgraph-swarm/         # Swarm 模式模板
├── cli/                     # langgraph dev / build / up
└── sdk-py / sdk-js/         # LangGraph Platform 客户端 SDK
```

JS/TS 版本在独立仓库 <https://github.com/langchain-ai/langgraphjs>，结构同构但命名略异。

---

## 3. 版本节奏与里程碑

| 版本 | 时间 | 里程碑 |
|------|------|------|
| 0.0.x | 2024 年初 | 初始图模型 + Channel 原型 |
| 0.1 | 2024 Q1 | StateGraph、Prebuilt agent |
| 0.2 | 2024 Q3 | Functional API、Subgraph |
| 0.3 | 2024 Q4 | Streaming 深化、Durability 改进 |
| 1.0 | 2025 中 | 公开稳定 API、语义锁定 |
| 1.1 | 2025-2026 | Context API / runtime 清理、性能优化 |

节奏：monorepo 多 package 独立发版，核心 `langgraph` 大约每 2-4 周一版。

---

## 4. 依赖

运行时必备：

- Python ≥ 3.10（1.x）
- `pydantic ≥ 2`（类型 / 序列化）
- `xxhash`（hash cache key）
- `ormsgpack`（checkpoint 序列化）
- `langchain-core`（可选但常见：与 LangChain Runnable 协议互通）

**零 LLM provider 依赖**：LangGraph 自己不绑定任何 LLM SDK；节点里你自由调用 `langchain_openai` / `anthropic` / 任意 HTTP。

---

## 5. 代码体量与关注度

以 `libs/langgraph/langgraph/` 为主：

- 核心 ~12 k 行 Python
- prebuilt/ ~1.5 k 行
- channels/ ~1 k 行
- checkpoint/（内存）~0.8 k 行
- Postgres / SQLite / DuckDB 各自 1-2 k 行

社区指标（2026-04 参考）：

- Stars: 28 k+
- PR contributors: 300+
- LangGraph Platform 客户：Klarna、Elastic、Replit、Uber、LinkedIn 等（公开案例）

---

## 6. 核心概念一览

| 概念 | 角色 | 源码 |
|------|------|------|
| **StateGraph** | 构图 DSL（用户 API） | `graph/state.py` |
| **CompiledGraph / Pregel** | 运行时核心 | `pregel/__init__.py` |
| **Channel** | 节点间"通道"抽象 | `channels/*.py` |
| **Checkpointer** | 持久化接口 | `checkpoint/base.py` |
| **Command** | 跨节点跳转/带 state 更新 | `types.py` |
| **Send** | Map / Fan-out | `types.py` |
| **Interrupt** | HITL 暂停 | `types.py` |
| **Stream Mode** | `values` / `updates` / `messages` / `custom` / `debug` | `pregel/types.py` |

这些会在 [[01-architecture]] 之后的 `02-*`、`03-*` 逐一深入。

---

## 7. 典型心智模型

对用户：

```python
from langgraph.graph import StateGraph, START, END
from typing import TypedDict

class State(TypedDict):
    messages: list
    step: int

def plan(state): ...
def act(state): ...

graph = StateGraph(State)
graph.add_node("plan", plan)
graph.add_node("act", act)
graph.add_edge(START, "plan")
graph.add_edge("plan", "act")
graph.add_edge("act", END)
app = graph.compile(checkpointer=saver)

for event in app.stream({"messages": [], "step": 0}, config):
    ...
```

对实现：

1. `StateGraph.compile()` → 产出一个 `CompiledStateGraph (Pregel 子类)`
2. 运行时 Pregel 以 **BSP (Bulk Synchronous Parallel)** 方式一轮一轮推进：
   - Select 激活节点
   - 并行执行
   - 汇合 Channel 更新
   - 触发下一轮
3. 每个 superstep 末尾写 Checkpoint（若配置了 saver）
4. `interrupt()` 会把当前 superstep 冻结 → 用户 UI 决策 → `Command(resume=...)` 继续

---

## 8. 为什么值得深读（面向 Dawning）

1. **StateGraph vs Dawning Skill/Workflow** —— LangGraph 的"图 + state reducer"对我们 `IWorkflow`、`ISkillRouter` 的语义设计有参考
2. **Pregel BSP 调度** —— Dawning 如果做多节点并行，Pregel 的 barrier 模型是最成熟的参考
3. **Checkpointer 分层** —— `BaseCheckpointSaver` / `SerializerProtocol` 分离，是 Dawning `IWorkflowCheckpoint` 的绝佳模板
4. **Stream Modes** —— `values/updates/messages/custom/debug` 四档模型，可直接映射 Dawning `IAgentEventStream`
5. **Command / Send / Interrupt** —— 跨节点副作用的原语集，比单纯 "next node name" 强得多
6. **LangGraph Platform（商业）** —— 长时任务、assistants、cron、store，是企业级 Agent 后端的完整模板

---

## 9. 已知局限（先建立预期）

- **非通用工作流引擎**：无 SLA、无重试策略 DSL、无 Activity 隔离（对标 Temporal 差很多）
- **Graph 静态结构**：动态图只能用 Send / Subgraph 近似
- **并发模型有限**：多线程 / 异步混合容易踩坑
- **Checkpoint 数据量膨胀**：每 superstep 全量 state，超长对话需要裁剪策略
- **Python 主战场**：JS 版本滞后一段

---

## 10. 阅读顺序建议

建议按下列次序读完这个子目录：

1. [[01-architecture]] — 模块地图 + 整体调用链
2. [[02-state-graph]] — 构图 DSL 与编译
3. [[03-pregel-runtime]] — BSP 调度内核
4. [[04-channels]] — Channel 家族源码
5. [[05-checkpointer]] — 持久化分层与序列化
6. [[06-interrupt-hitl]] — Command / Interrupt / Resume
7. [[07-streaming]] — Stream Modes 实现
8. [[08-prebuilt-agents]] — create_react_agent / supervisor / swarm
9. [[09-subgraph-functional-api]] — 子图与 Functional API
10. [[10-platform-integration]] — LangGraph Platform（assistants / store / cron）

横向：

- [[_cross-module-comparison]] — LangGraph 模块 ↔ Dawning 全表映射
- [[cases/_cross-case-comparison]] — 4 个案例横向对比

案例（按复杂度排序）：

1. [[cases/klarna-customer-support]] — Supervisor + 多 Worker（客服域）
2. [[cases/open-deep-research]] — Send fan-out + 子图（研究域）
3. [[cases/replit-agent]] — Manager / Editor / Verifier 反馈环（代码生成域）
4. [[cases/linkedin-hr-agent]] — 多次 HITL + 长 thread（招聘域）

---

## 11. 延伸阅读

- [[../README]] — 源码解剖库说明
- [[../../entities/frameworks/langgraph.zh-CN]] — LangGraph Profile（非源码级）
- [[../../comparisons/maf-vs-langgraph.zh-CN]] — 与 MAF 对比
- [[../../comparisons/agent-framework-landscape.zh-CN]] — 框架全景
- [[../../concepts/state-persistence.zh-CN]] — 状态持久化综述
- 官方文档：<https://langchain-ai.github.io/langgraph/>
- 源码：<https://github.com/langchain-ai/langgraph>
- Pregel 论文：<https://research.google/pubs/pregel-a-system-for-large-scale-graph-processing/>
