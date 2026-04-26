---
framework: langgraph
tier: 1
type: synthesis
chapter: 03
title: 心智模型 —— 图 / State / Channel / Checkpoint
tags: [langgraph, tier-1, mental-model, channel, checkpoint]
audience: 读完 02 的新人
reading-time: 30 min
created: 2026-04-21
updated: 2026-04-21
status: active
subtype: intuition
sources: []
---

# 03 · 心智模型 —— 图 / State / Channel / Checkpoint

> 前置：[[02-hello-world]]
> 目标：把 LangGraph 的 **4 个核心概念**彻底理顺。读完后看任何 LangGraph 代码都能说出"它在做什么"。
> 本篇全是**概念 + 比喻 + 对照**，源码在 tier-3。

---

## 0. 为什么需要这 4 个概念

回忆 [[02-hello-world]]：

```python
class State(TypedDict): ...
def node(state): ...
graph.add_node(...); graph.add_edge(...)
app = graph.compile()
app.invoke(...)
```

这些背后其实是 4 个概念在支撑：

| 概念 | 解决什么 |
|------|---------|
| **图** | 流程结构（谁跑，跑到谁） |
| **State** | 数据共享（谁读什么，谁写什么） |
| **Channel** | 字段合并规则（多个节点同时写怎么办） |
| **Checkpoint** | 持久化（崩了怎么恢复） |

顺序推荐：图 → State → Channel → Checkpoint（由浅入深）。

---

## 1. 概念一：图（Graph）

### 1.1 本质

**节点 + 边**。

```
START → A → B → END
         ↓
         C
```

- **节点**：一件事（一个函数）
- **边**：一个节点跑完**可能**去哪
- **START / END**：特殊保留节点，入口和出口

### 1.2 LangGraph 的图和数学的图 有点不一样

| 数学的图 | LangGraph 的图 |
|---------|---------------|
| 顶点和边完全对称 | **有向**（START → END） |
| 遍历规则你自己定 | **数据流驱动**（下一节 State 讲） |
| 可以无限 | 可以**有环**（循环），但有 `recursion_limit` 兜底 |

### 1.3 三种边

```python
graph.add_edge("A", "B")                        # 静态：A 完必 B
graph.add_conditional_edges("A", route_fn, ...) # 条件：route_fn 决定
# Send：发给多个目标（用于 fan-out）  —— 04 会讲
```

### 1.4 比喻

**像一张工厂车间的流水线图**：

- 每个工位（节点）干一道工序
- 传送带（边）决定下一道工序是谁
- 可以有分叉、合并、返工环路

### 1.5 Dawning 对照

```csharp
IWorkflowBuilder<TState> builder;
builder.AddNode("A", A);
builder.AddEdge("A", "B");
builder.AddConditionalEdges("A", Route, ...);
var workflow = builder.Build();
```

语义一一对应，只是 C# 更强类型。

---

## 2. 概念二：State（状态）

### 2.1 本质

**所有节点共享的数据包**。它:

- 在 `invoke(input)` 时被初始化
- 每个节点读写它
- 最后作为返回值

### 2.2 为什么不用函数参数传递？

图可能有：
- 分叉（A 的结果要给 B 和 C）
- 合并（B 和 C 的结果都要给 D）
- 循环（A → B → A → B ...）
- 跨节点远距离依赖（A 写的，后面 E 才读）

用函数参数传根本串不起来。用**共享 state**：

```python
def A(state):
    return {"x": 1}    # 写 state.x

def B(state):
    return {"y": state["x"] + 1}   # 读 state.x，写 state.y

def C(state):
    return {"z": state["x"] + 10}  # 读 state.x，写 state.z

def D(state):
    return {"result": state["y"] + state["z"]}   # 读 state.y/z
```

所有节点都读写**同一个 state**，图结构只管"谁先谁后"。

### 2.3 节点"返回的是 update，不是完整 state"

```python
def A(state):
    return {"x": 1}    # ← 只是一个 update
```

LangGraph 会做：

```
旧 state: {x: 0, y: 0, z: 0}
A 返回:   {x: 1}
合并:    {x: 1, y: 0, z: 0}
```

**为什么这么设计**：

- 代码更短
- 节点并行时多个 update 好合并
- 跟 Redux / React 的 reducer 一个味道

### 2.4 比喻

**像 Google Docs 协作文档**：

- 文档（state）是所有人共享的
- 每个人（节点）只编辑自己那部分字段
- 保存时自动 merge

### 2.5 Dawning 对照

```csharp
public record MyState(
    string Question,
    string Answer);

public record MyStateUpdate(string? Answer = null);

public MyStateUpdate A(MyState state)
    => new() { Answer = "hi" };
```

C# 强类型区分 state 和 update，比 Python 更安全。

---

## 3. 概念三：Channel（通道）—— state 的内部结构

### 3.1 本质

> **State 里的每一个字段，在运行时是一个 Channel。**

```python
class State(TypedDict):
    messages: list   # → channel "messages"
    step: int        # → channel "step"
    result: str      # → channel "result"
```

每个 channel 存了：

```
Channel "messages":
  value   = [...]      # 当前值
  reducer = ???        # 合并规则（新旧怎么合）
  version = 3          # 版本号（我变过 3 次了）
```

**为什么不直接用 dict？** 因为 `dict[field] = new_value` 太朴素——当两个节点**同时**写同一字段，要怎么合并？靠 channel 的 **reducer**。

### 3.2 Reducer（合并规则）

默认 reducer：**last_value**（后写覆盖前写）。

```python
class State(TypedDict):
    count: int    # 默认 last_value
```

如果两个节点同轮都写 `count`：
```
A returns {count: 5}
B returns {count: 7}
→ 合并后 count = 7 或 5（顺序未定义，危险）
```

**所以要给能被并发写的字段指定 reducer**：

```python
from operator import add
from typing import Annotated

class State(TypedDict):
    count:    Annotated[int,  add]        # 相加
    messages: Annotated[list, add]        # 追加
```

合并变成：
```
A returns {count: 5}
B returns {count: 7}
→ count = 0 + 5 + 7 = 12  (确定性)
```

**常见 reducer**：

| reducer | 含义 | 典型字段 |
|---------|------|---------|
| `last_value`（默认） | 后写覆盖 | 单步状态、结果 |
| `add` | 相加 / 列表追加 | 累计、消息列表 |
| `add_messages` | 智能消息合并（去重、tool_call） | 对话历史 |
| 自定义函数 | 任意合并逻辑 | 业务特定 |

### 3.3 版本号（Version）

每个 channel 内部还藏了一个**只增不减的数字**：

```
初始:      count.version = 0
A 写过:    count.version = 1
B 又写:    count.version = 2
```

**用途**：判断"数据变没变过"。每个节点记着"我上次看到的版本号"，用它决定下一轮要不要跑自己。

> 详细的"版本号 → 激活判定"机制见 [[../../../concepts/dataflow-channel-version]]。
> 本篇只要记住："**channel 有版本号，用来决定下一轮谁被激活**"。

### 3.4 比喻

**像 Git 仓库的分支合并**：

- 每个字段（channel） = 一个分支
- reducer = merge 策略（fast-forward / rebase / octopus merge）
- version = commit 序号
- 节点 = 开发者各自 push

### 3.5 Dawning 对照

规划中（`IWorkingMemory<T>`）：

```csharp
[ConcatList]   // reducer attribute
public List<Message> Messages { get; init; }

[LastValue]    // 默认
public int Step { get; init; }

[Sum]          // 相加
public int Count { get; init; }
```

C# 用属性（attribute）声明 reducer，比 Python 的 `Annotated` 更显式。

---

## 4. 概念四：Checkpoint（检查点）

### 4.1 本质

> **每一轮结束后，把当前 state 的完整快照保存到持久化存储。**

运行流程：

```
Superstep 1:  state 变化 → 写 checkpoint 到 Postgres
Superstep 2:  state 变化 → 写 checkpoint 到 Postgres
...
```

每个 checkpoint 记录：

```json
{
  "thread_id":     "user-42-session-7",
  "superstep":     5,
  "channel_values":   { "messages": [...], "step": 3 },
  "channel_versions": { "messages": 4,    "step": 2 },
  "next_tasks":       ["agent_node"]
}
```

### 4.2 为什么需要

| 场景 | 没 checkpoint | 有 checkpoint |
|------|--------------|--------------|
| 进程崩了 | 全丢 | 从上次续跑 |
| 想暂停让人审 | 只能整个任务扔掉 | `interrupt()` 冻结当前状态，恢复时秒到位 |
| 想看历史（debug） | 没得看 | 每步都是一个快照 |
| 时间旅行（fork） | 不行 | 从任意历史点分叉 |
| 多进程 / 多实例共享 | 各自为政 | 靠 thread_id 协同 |

### 4.3 开关

要不要 checkpoint 是**可选的**：

```python
from langgraph.checkpoint.memory import MemorySaver
# or
# from langgraph.checkpoint.postgres import PostgresSaver

saver = MemorySaver()
app = graph.compile(checkpointer=saver)

config = {"configurable": {"thread_id": "t1"}}
app.invoke({"question": "hi"}, config)   # 每轮自动写 checkpoint
```

**关键点**：
- 没传 `checkpointer` → 纯内存，跑完忘光
- 传了 → 每轮自动写盘 + `thread_id` 标识会话

### 4.4 恢复长什么样

```python
# 第一次跑（假设到中途 interrupt）
result = app.invoke({"question": "..."}, config)
# → 暂停了

# 时光停一天。新进程起来。
# 用同一个 thread_id 接着跑：
from langgraph.types import Command
result = app.invoke(Command(resume="yes"), config)
# → 从 interrupt 那一步继续，前面所有算过的都不重算
```

### 4.5 比喻

**像游戏的"自动存档"**：

- 每过一关（superstep）系统自动存档
- 退出游戏没事，下次读档接着打
- 想从第 3 关重来（time travel）也行

### 4.6 Dawning 对照

规划中：

```csharp
var workflow = builder.Build(options => {
    options.Checkpointer = new PostgresCheckpointer(conn);
});

var config = new WorkflowConfig { ThreadId = "user-42" };
await workflow.InvokeAsync(input, config, ct);
```

接口形态一致。

---

## 5. 四者关系全景图

```
┌─────────────────────────────────────────────────────────┐
│                        图 (Graph)                        │
│   声明"谁是节点、谁连谁"                                  │
│                                                         │
│   ┌────────┐    ┌────────┐    ┌────────┐                │
│   │ node A │ →  │ node B │ →  │ node C │                │
│   └────┬───┘    └────┬───┘    └────┬───┘                │
│        │             │             │                     │
│        │ 读写         │ 读写         │ 读写                │
│        ▼             ▼             ▼                     │
│   ┌───────────────────────────────────────┐             │
│   │             State (数据包)             │             │
│   │   编译成 ⇨                             │             │
│   │   ┌──────────┐ ┌──────────┐ ┌────────┐│             │
│   │   │ channel  │ │ channel  │ │channel ││             │
│   │   │ messages │ │   step   │ │ result ││  ← 含版本号 │
│   │   │ +reducer │ │ +reducer │ │+reducer││             │
│   │   └──────────┘ └──────────┘ └────────┘│             │
│   └───────────────────────────────────────┘             │
│                     │                                    │
│                     │ 每轮末尾整个快照                    │
│                     ▼                                    │
│   ┌───────────────────────────────────────┐             │
│   │        Checkpoint (持久化存储)         │             │
│   │    Postgres / SQLite / Memory         │             │
│   └───────────────────────────────────────┘             │
└─────────────────────────────────────────────────────────┘
```

- **图**管结构
- **State** 管数据
- **Channel**（state 的内部）管合并
- **Checkpoint** 管持久化

---

## 6. 记忆锚点（一句话各一）

> - **图** = 节点 + 边的声明
> - **State** = 节点间共享的数据包
> - **Channel** = state 的一个字段 + reducer + 版本号
> - **Checkpoint** = 每轮 state 的持久化快照

---

## 7. 下一步

- 下一篇 → [[04-tour-by-example]] 从最简单例子一步步加需求，把上面 4 个概念**全部用到**
- 想直接看"激活节点"的版本号机制 → [[../../../concepts/dataflow-channel-version]]
- 源码级细节 → [[../tier-3-internals/04-channels]] / [[../tier-3-internals/05-checkpointer]]
