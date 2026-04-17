---
title: "MAF Workflow vs LangGraph：两大图编排引擎头对头"
type: comparison
tags: [workflow, langgraph, maf, state-machine, orchestration]
sources: [comparisons/agent-framework-landscape.zh-CN.md, comparisons/framework-modules-mapping.zh-CN.md, concepts/agent-loop.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# MAF Workflow vs LangGraph：两大图编排引擎头对头

> 2025-2026 年，"Agent = 图"已成为生产级框架的事实共识。
> LangGraph（LangChain 生态）和 MAF Workflow（Microsoft 生态）是两个最成熟的代表，设计思路有惊人的相似之处，但也在关键维度上分道扬镳。
>
> 本文做一次彻底的头对头对比，作为 Dawning 编排层（Layer 3）设计的关键输入。

---

## 1. 共识：为什么都选"图"

单线 Chain 无法表达真实工作流。当 Agent 需要：

- **条件分支**：根据 LLM 输出走不同路径
- **循环**：重试、反思、多轮验证
- **并发**：多个子任务并行
- **汇聚**：多路结果合并
- **暂停**：等待人工 / 外部事件

→ **这些都是图论的基本结构**。于是两大框架不约而同选了"有状态图"作为核心原语。

### 1.1 共同基础：Pregel 模型

两者都深受 Google Pregel / BSP（Bulk Synchronous Parallel）启发：

```
Superstep N:
  所有节点并行接收消息 → 并行执行 → 并行发送消息
        │                 │                │
        └─────── 全局同步屏障 ───────────────┘
Superstep N+1:
  ...
```

这带来几个共同特性：
- **确定性执行**（只要 seed 和状态相同）
- **天然支持检查点**（每个 superstep 边界都是可序列化的）
- **并发安全**（superstep 内无竞争）

---

## 2. 核心概念对照

| 概念 | LangGraph | MAF Workflow |
|------|-----------|--------------|
| 图 | `StateGraph` | `Workflow` |
| 节点 | `Node`（任意 callable） | `Executor`（实现接口） |
| 边 | `Edge`（固定）/ `ConditionalEdge`（条件） | `Edge` / `RouterExecutor` |
| 状态 | `State`（TypedDict / Pydantic / `MessagesState`） | `WorkflowContext.State`（强类型） |
| 入口 | `START` 虚拟节点 | `Workflow.Root` |
| 出口 | `END` 虚拟节点 | `WorkflowContext.CompleteAsync` |
| 检查点 | `CheckpointSaver` | `ICheckpointManager` |
| 中断 | `interrupt_before` / `interrupt_after` | `WorkflowContext.RequestInputAsync` |
| 子图 | 嵌套 `StateGraph` | 嵌套 `Workflow` |
| 流式 | `astream_events` | `WorkflowRun.StreamEventsAsync` |

### 2.1 节点定义对比

**LangGraph（Python）**：

```python
from langgraph.graph import StateGraph, START, END
from typing import TypedDict

class State(TypedDict):
    messages: list
    query: str

def classify(state: State) -> State:
    # 节点就是普通函数
    intent = llm.invoke(f"分类: {state['query']}")
    return {"messages": state["messages"] + [intent]}

graph = StateGraph(State)
graph.add_node("classify", classify)
graph.add_edge(START, "classify")
graph.add_conditional_edges("classify", route_by_intent)
```

**MAF Workflow（.NET）**：

```csharp
public sealed class Classifier : Executor
{
    public override async ValueTask ExecuteAsync(WorkflowContext ctx)
    {
        var query = ctx.State.Get<string>("query");
        var intent = await chatClient.CompleteAsync($"分类: {query}");
        ctx.State.Set("intent", intent.Text);
        await ctx.SendMessageAsync(intent.Text);
    }
}

var workflow = new WorkflowBuilder()
    .AddExecutor<Classifier>()
    .AddEdge<Classifier, Router>()
    .Build();
```

### 2.2 语法风格差异

| 维度 | LangGraph | MAF |
|------|-----------|-----|
| 节点形态 | 函数式（纯函数优先） | OO（继承 `Executor`） |
| 状态更新 | **返回 diff 合并**（reducer 风格） | 显式 `ctx.State.Set` |
| 依赖注入 | 全局 `config` | 构造函数 DI |
| 类型安全 | TypedDict / Pydantic 注解 | 编译期强类型 |
| IDE 支持 | Python-level | Roslyn 完整 |

---

## 3. 状态模型：reducer vs 可变容器

### 3.1 LangGraph：Reducer 风格（函数式）

```python
class State(TypedDict):
    messages: Annotated[list, add_messages]  # 用 reducer 合并
    counter: Annotated[int, operator.add]    # 相加

# 节点返回 diff，由 reducer 合并到总状态
def node(state):
    return {"counter": 1}  # counter += 1
```

优势：
- 节点纯函数，易测试
- 并发节点更新同一字段由 reducer 确定合并语义
- 状态变迁可回放

### 3.2 MAF：可变 State 容器（OO）

```csharp
// 节点直接修改 State
ctx.State.Set("counter", ctx.State.Get<int>("counter") + 1);
```

优势：
- 对 .NET 开发者更直觉（普通对象读写）
- 强类型 API + IDE 提示
- 更少样板（不用写 reducer）

劣势：
- 并发写入需要开发者自己加锁（MAF 通过 "superstep 内单 executor" 回避大多数场景）

---

## 4. 条件路由

### 4.1 LangGraph

```python
def route(state):
    if state["intent"] == "refund":
        return "refund_node"
    elif state["intent"] == "ask":
        return "rag_node"
    return END

graph.add_conditional_edges("classify", route, {"refund_node": "refund_node", "rag_node": "rag_node", END: END})
```

路由函数返回"下一个节点名字"。

### 4.2 MAF

```csharp
builder.AddEdge<Classifier, Refund>(when: state => state.Get<string>("intent") == "refund");
builder.AddEdge<Classifier, Rag>(when: state => state.Get<string>("intent") == "ask");
```

条件是边上的 predicate，更像"工作流连线"而非函数返回字符串。

**主观评价**：MAF 的边 predicate 对静态分析友好；LangGraph 的 route 函数更灵活（可在其中做复杂计算）。

---

## 5. 检查点与持久化

### 5.1 LangGraph

```python
from langgraph.checkpoint.postgres import PostgresSaver

checkpointer = PostgresSaver.from_conn_string("...")
app = graph.compile(checkpointer=checkpointer)

# 按 thread_id 天然 scope
config = {"configurable": {"thread_id": "user-123"}}
result = app.invoke(input, config)
```

检查点存储后端丰富：Postgres / SQLite / Memory / Redis / 自定义。

### 5.2 MAF

```csharp
var workflow = builder
    .UseCheckpointManager<AzureBlobCheckpointManager>()
    .Build();

var run = await workflow.StartAsync(input);
await run.PersistAsync();  // 显式持久化
```

检查点后端：Azure Blob / Cosmos DB / 自定义。

### 5.3 恢复语义

| 维度 | LangGraph | MAF |
|------|-----------|-----|
| 恢复粒度 | superstep 边界 | superstep 边界 |
| 时间旅行 | ✅ 可恢复到任意 superstep | ⚠️ 默认最后一个 |
| 分支调试 | ✅ Fork 检查点 | ⚠️ 限制多 |
| 跨进程恢复 | ✅ 任何拿到 thread_id 的进程都能接管 | ✅ Durable Agent 原生支持 |

---

## 6. 人机协同（HITL）

### 6.1 LangGraph

```python
app = graph.compile(
    checkpointer=checkpointer,
    interrupt_before=["human_review"]  # 执行到这个节点前暂停
)

# 第一次调用：到 human_review 前暂停
app.invoke(input, config)

# 人工编辑状态
app.update_state(config, {"approved": True})

# 继续
app.invoke(None, config)
```

### 6.2 MAF

```csharp
public sealed class HumanReview : Executor
{
    public override async ValueTask ExecuteAsync(WorkflowContext ctx)
    {
        var request = new InputRequest { Kind = "approval", ... };
        var response = await ctx.RequestInputAsync(request);  // 暂停直到收到响应
        ctx.State.Set("approved", response.Approved);
    }
}
```

**设计差异**：
- LangGraph 是**外部驱动**：`interrupt_before` 让图停下，外部代码决定何时继续
- MAF 是**内部驱动**：`RequestInputAsync` 让图显式挂起，等待外部输入 SDK 调用

---

## 7. 并发与并行

### 7.1 LangGraph

```python
graph.add_edge("start", "branch_a")
graph.add_edge("start", "branch_b")   # 两条边从同一节点出发 = 并行
graph.add_edge("branch_a", "merge")
graph.add_edge("branch_b", "merge")   # 汇聚到 merge 节点
```

汇聚语义由 State 的 reducer 控制（比如 messages 用 `add_messages` 合并两路消息）。

### 7.2 MAF

```csharp
builder
    .AddEdge<Start, BranchA>()
    .AddEdge<Start, BranchB>()
    .AddEdge<BranchA, Merge>()
    .AddEdge<BranchB, Merge>()
    .UseFanInStrategy<Merge>(FanInStrategy.WaitAll);
```

汇聚策略显式可选：`WaitAll` / `WaitAny` / `WaitQuorum`。

**对比**：MAF 的 FanIn 策略更显式；LangGraph 需要开发者通过 reducer + 状态字段推断汇聚行为。

---

## 8. 流式输出

### 8.1 LangGraph

```python
async for event in app.astream_events(input, version="v2"):
    kind = event["event"]  # "on_chain_start" / "on_chat_model_stream" / ...
```

粒度极细：每个节点进入/退出、LLM 每个 token、工具调用开始/结束都有事件。

### 8.2 MAF

```csharp
await foreach (var ev in run.StreamEventsAsync(ct))
{
    switch (ev)
    {
        case ExecutorStarted s: ...
        case MessageSent m: ...
        case WorkflowCompleted c: ...
    }
}
```

强类型事件 + pattern matching，.NET 友好。

---

## 9. 分布式与扩展

| 维度 | LangGraph | MAF |
|------|-----------|-----|
| 单进程 | ✅ | ✅ |
| 多进程（检查点恢复） | ✅ | ✅ |
| Durable Agent（真正分布式工作流） | ⚠️ LangSmith Deploy 商业 | ✅ **内建 Durable Executor** |
| A2A 集成 | ⚠️ 社区 | ✅ 原生 |
| 无服务器部署 | ⚠️ LangGraph Cloud | ✅ Azure Container Apps / Functions |

**优势集中化**：MAF 在"分布式 + 持久化 + 云原生"上领先；LangGraph 在"灵活性 + 生态"上领先。

---

## 10. 调试与可观测性

### 10.1 LangGraph

| 工具 | 能力 |
|------|------|
| **LangSmith**（商业） | 全链路追踪、时间旅行调试、Playground |
| **LangGraph Studio**（本地 IDE） | 图可视化、交互式运行 |
| `astream_events` | 事件流 |

### 10.2 MAF

| 工具 | 能力 |
|------|------|
| **AgentFramework 调试器**（内建） | 图可视化、单步 |
| **OpenTelemetry** | 原生导出（Span / Metric / Log） |
| **Foundry Tracing** | Azure AI Foundry 集成 |
| `StreamEventsAsync` | 事件流 |

### 10.3 差异

| 维度 | LangGraph | MAF |
|------|-----------|-----|
| 可视化 | ✅ LangGraph Studio | ✅ 内建 |
| 追踪 | LangSmith（独立商业） | OpenTelemetry（厂商无关） |
| 云集成 | LangChain Cloud | Azure Foundry |

---

## 11. 子图与复用

### 11.1 LangGraph：嵌套 StateGraph

```python
sub_graph = StateGraph(SubState)
...
compiled_sub = sub_graph.compile()

main_graph.add_node("sub", compiled_sub)  # 当作普通节点
```

### 11.2 MAF：嵌套 Workflow

```csharp
var subWorkflow = new WorkflowBuilder()...Build();
mainBuilder.AddSubWorkflow(subWorkflow);
```

**都支持**，语义几乎一致。

---

## 12. 生态与集成

| 维度 | LangGraph | MAF |
|------|-----------|-----|
| 语言 | Python + JS/TS | .NET + Python |
| LLM 支持 | LangChain 全家桶（100+） | OpenAI / Azure / MEAI 抽象 |
| 工具生态 | LangChain Tools + MCP | 函数装饰器 + MCP + A2A |
| RAG | LangChain RAG 全栈 | `IVectorStore`（新） |
| 记忆 | `MessagesState` + 外部 | `ChatHistory` + 外部 |
| 安装 | `pip install langgraph` | `dotnet add package Microsoft.Agents.*` |

---

## 13. 横向对比总表

| 维度 | LangGraph | MAF Workflow |
|------|-----------|--------------|
| 生态 | Python 优势 | .NET 原生 + Python |
| 状态模型 | Reducer（函数式） | 可变容器（OO） |
| 节点定义 | 纯函数 | Executor 继承 |
| 条件路由 | 路由函数 | 边 predicate |
| 检查点后端 | 多样（Postgres/SQLite/...） | Azure 系 + 自定义 |
| HITL | 外部驱动（`interrupt_before`） | 内部驱动（`RequestInputAsync`） |
| 并发汇聚 | reducer 推断 | 显式 FanIn 策略 |
| 流式事件 | 最细粒度 | 强类型 |
| 分布式 | LangGraph Cloud（商业） | Durable 原生 |
| A2A | 社区 | ✅ 原生 |
| 可视化 | LangGraph Studio | 内建 |
| 适合人群 | Python AI 工程师 / 研究者 | .NET 企业开发者 |

---

## 14. 对 Dawning Layer 3 的启示

### 14.1 吸收：两家共同的优秀设计

| 设计 | 来源 | Dawning 采纳 |
|------|------|-------------|
| 有状态图作为核心原语 | 两家 | ✅ `IWorkflowEngine` |
| Pregel / superstep 确定性 | 两家 | ✅ |
| 检查点 = superstep 边界 | 两家 | ✅ Layer 6 `ICheckpointStore` |
| 并发 + 汇聚 | 两家 | ✅ |
| 子图嵌套 | 两家 | ✅ |
| HITL 挂起 | 两家 | ✅ |

### 14.2 吸收：MAF 的特性（更契合 .NET）

| 设计 | 原因 |
|------|------|
| **强类型状态容器** | Roslyn + 编译期检查 |
| **显式 FanIn 策略** | 语义清晰，不依赖隐式 reducer |
| **Durable Agent 原生** | 企业级要求 |
| **内部驱动 HITL** | 与 DI 和异步更契合 |
| **OpenTelemetry 原生** | 厂商无关，对接现有观测 |

### 14.3 吸收：LangGraph 的特性（互补）

| 设计 | 原因 |
|------|------|
| **时间旅行调试** | 开发体验 |
| **最细粒度事件流** | 前端流式 UI |
| **Reducer 思想** | 并发写冲突时的合并语义 |

### 14.4 Dawning 的增量创新

| 能力 | 其他两家 | Dawning |
|------|---------|---------|
| Scope 感知（四级隔离） | ❌ | ✅ Layer 2 + Layer 7 联动 |
| 技能演化 | ❌ | ✅ Layer 5 独立流水线 |
| 策略引擎挡在路径上 | ❌ | ✅ Layer 7 前置 |
| A2A 为一等公民 | MAF 有，LangGraph 无 | ✅ 与 MCP 并列 |
| 技能市场（未来） | ❌ | 🔵 探索中 |

---

## 15. 选型指南（对用户）

| 你的场景 | 推荐 |
|---------|------|
| Python 团队、研究和实验为主 | **LangGraph** |
| Python 团队、需要云托管 | **LangGraph + LangGraph Cloud** |
| .NET 团队、Azure 生态 | **MAF** |
| .NET 团队、多云 / 需要更深企业治理 | **Dawning**（未来） |
| 跨语言互操作 | **A2A 协议**跨越三者 |

---

## 16. 小结

> LangGraph 和 MAF Workflow 都证明了："Agent = 有状态图"是生产级的正确方向。
>
> 差异更多是**生态和工程传统**：
> - LangGraph = Pythonic 函数式 + LangChain 生态
> - MAF = .NET OO + Azure / Microsoft 生态
>
> 对 Dawning 来说，Layer 3 的正确做法是：
> 1. **吸收两家共识**（Pregel、检查点、并发、HITL）
> 2. **选择 MAF 风格的强类型 API**（契合 .NET 习惯）
> 3. **保留 LangGraph 的开发体验精华**（时间旅行、细粒度事件）
> 4. **叠加 Dawning 独有的 Layer 2/5/7 联动**（Scope、演化、治理）

---

## 17. 延伸阅读

- [[concepts/agent-loop.md]] — 状态图编排模式（§2.4）
- [[comparisons/framework-modules-mapping.zh-CN]] — MAF / LangGraph 模块地图
- [[concepts/dawning-capability-matrix.zh-CN]] — Layer 3 接口
- [[concepts/skill-evolution.zh-CN]] — Layer 5 与 Layer 3 的联动
- LangGraph 官方文档：<https://langchain-ai.github.io/langgraph/>
- MAF 官方文档：<https://learn.microsoft.com/en-us/agent-framework/>
