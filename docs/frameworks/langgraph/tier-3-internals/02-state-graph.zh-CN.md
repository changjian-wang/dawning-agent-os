---
framework: langgraph
version: v1.1.x
type: synthesis
module: state-graph
repo-path: libs/langgraph/langgraph/graph/
tags: [langgraph, state-graph, dsl, compile]
created: 2026-04-18
updated: 2026-04-18
status: active
subtype: internals
title: LangGraph — 02 StateGraph：构图 DSL 的源码解剖
sources: []
---

# LangGraph — 02 StateGraph：构图 DSL 的源码解剖

> 本文回答：用户写的 `StateGraph(...).add_node().add_edge().compile()` 在源码里**到底发生了什么**？
>
> 重点路径：`langgraph/graph/state.py`、`langgraph/graph/graph.py`、`langgraph/_compile.py`。


## 目录 <!-- TOC-AUTOGEN -->

- [1. 范围与边界](#1-范围与边界)
- [2. 用户视角：最小例子](#2-用户视角最小例子)
- [3. 类层次](#3-类层次)
- [4. 构图阶段：`add_node` / `add_edge` 在记什么](#4-构图阶段addnode--addedge-在记什么)
- [5. State schema → Channels 的翻译](#5-state-schema--channels-的翻译)
- [6. `compile()`：从账本到 Pregel](#6-compile从账本到-pregel)
- [7. 运行时 API：CompiledStateGraph 暴露什么](#7-运行时-apicompiledstategraph-暴露什么)
- [8. 子图：StateGraph 节点可以是另一个 CompiledGraph](#8-子图stategraph-节点可以是另一个-compiledgraph)
- [9. 与 `Graph` / `MessageGraph` 的关系](#9-与-graph--messagegraph-的关系)
- [10. 常见错误与编译期校验](#10-常见错误与编译期校验)
- [11. 性能与陷阱](#11-性能与陷阱)
- [12. 与 Dawning 的对应](#12-与-dawning-的对应)
- [13. 阅读顺序](#13-阅读顺序)
- [14. 延伸阅读](#14-延伸阅读)
<!-- /TOC-AUTOGEN -->

---

## 1. 范围与边界

| 在范围 | 不在范围（见对应模块） |
|--------|----------------------|
| StateGraph 类的 API 表面 | Pregel 调度细节 → [[03-pregel-runtime]] |
| `compile()` 把 DSL → Pregel 的过程 | Channel 内部 reducer → [[04-channels]] |
| State schema → Channel 的翻译规则 | Checkpoint 持久化 → [[05-checkpointer]] |
| Edge 路由的几种形态（普通 / 条件 / Send） | HITL `interrupt` → [[06-interrupt-hitl]] |

---

## 2. 用户视角：最小例子

```python
from typing import TypedDict, Annotated
from operator import add
from langgraph.graph import StateGraph, START, END

class State(TypedDict):
    counter: Annotated[int, add]   # reducer = +
    last: str

def step1(s: State) -> dict:
    return {"counter": 1, "last": "step1"}

def step2(s: State) -> dict:
    return {"counter": 2, "last": "step2"}

g = StateGraph(State)
g.add_node("a", step1)
g.add_node("b", step2)
g.add_edge(START, "a")
g.add_edge("a", "b")
g.add_edge("b", END)
app = g.compile()                  # ← 编译
print(app.invoke({"counter": 0, "last": ""}))
# {'counter': 3, 'last': 'step2'}
```

> 关注三件事：`StateGraph(State)` 怎么解析 schema、`add_*` 怎么记账、`compile()` 怎么构造 Pregel。

---

## 3. 类层次

<!-- StateGraph 类层次 -->
````mermaid
classDiagram
    class Runnable {
        <<interface>>
        +invoke()
        +stream()
        +ainvoke()
        +astream()
    }
    class Graph {
        +nodes: dict
        +edges: set
        +branches: dict
        +add_node()
        +add_edge()
        +add_conditional_edges()
        +compile()
    }
    class StateGraph {
        +schema: type
        +channels: dict[BaseChannel]
        +managed: dict[ManagedValue]
        +input_schema, output_schema
        +waiting_edges: set
        +compile()
    }
    class Pregel {
        +nodes: dict[PregelNode]
        +channels: dict[BaseChannel]
        +checkpointer
        +invoke() / stream()
    }
    class CompiledGraph {
        +builder: Graph
        +attach_node()
        +attach_edge()
        +attach_branch()
    }
    class CompiledStateGraph {
        +get_state()
        +update_state()
        +get_state_history()
        +get_graph()
    }
    class MessageGraph {
        <<MessagesState 预设>>
    }

    Runnable <|.. Pregel
    Graph <|-- StateGraph
    StateGraph <|-- MessageGraph
    Pregel <|-- CompiledGraph
    CompiledGraph <|-- CompiledStateGraph
    StateGraph ..> CompiledStateGraph : compile()
```
> 源文件：[`diagrams/state-graph-class.mmd`](../diagrams/state-graph-class.mmd)

- **`Graph`**：纯结构（节点 + 边），不带 state schema。`MessageGraph` 走这条
- **`StateGraph`**：在 `Graph` 之上叠加"State schema → Channels"翻译
- **`CompiledGraph`**：编译后产物，**本质是一个 Pregel 子类**。它把 graph 元数据转成 Pregel 的 nodes/channels
- **`CompiledStateGraph`**：在 `CompiledGraph` 上额外暴露 `update_state / get_state / get_state_history` 等 state 视图 API

> 关键洞察：`CompiledGraph is-a Pregel`。这就是为什么 `app.invoke()` 直接走 Pregel —— 没有中间转译层。

---

## 4. 构图阶段：`add_node` / `add_edge` 在记什么

`StateGraph` 是个**只攒账本**的类，编译前不做任何运算。账本字段：

| 字段 | 类型 | 含义 |
|------|------|------|
| `nodes` | `dict[str, StateNodeSpec]` | 节点名 → (runnable, metadata, input schema, retry policy, cache policy) |
| `edges` | `set[tuple[str, str]]` | 普通有向边 |
| `branches` | `dict[str, dict[str, BranchSpec]]` | 条件路由 |
| `channels` | `dict[str, BaseChannel]` | 从 schema 推出来的 channel 实例 |
| `managed` | `dict[str, ManagedValueSpec]` | `Annotated[T, ManagedValue]` 标记的运行时注入 |
| `schemas` | `dict[type, dict[str, BaseChannel]]` | input/output/state schema 三种 |
| `waiting_edges` | `set[tuple[tuple[str, ...], str]]` | "等多个节点都完成才走"的 join 边 |
| `compiled` | `bool` | 防重复 compile |

### 4.1 `add_node` 的形态

```python
g.add_node("a", step1)                                   # 函数节点
g.add_node("b", subgraph_app)                            # Runnable / 子图
g.add_node(step1)                                        # 函数名作为节点名
g.add_node("c", step3, input_schema=PartialState)       # 局部 schema
g.add_node("d", step4, retry_policy=RetryPolicy(...))   # 重试策略
g.add_node("e", step5, cache_policy=CachePolicy(...))   # 节点级缓存
g.add_node("f", step6, defer=True)                       # 延后执行
g.add_node("g", step7, destinations=("h", "i"))          # 静态出度声明（可视化用）
```

源码片段（简化）：

```python
def add_node(self, node_or_name, action=None, *, metadata=None,
             input_schema=None, retry_policy=None, cache_policy=None,
             defer=False, destinations=None):
    name, runnable = self._normalize(node_or_name, action)
    if name in self.nodes:
        raise ValueError(f"Node `{name}` already present")
    if name in (START, END):
        raise ValueError(f"`{name}` is reserved")
    self.nodes[name] = StateNodeSpec(
        runnable=coerce_to_runnable(runnable),
        metadata=metadata,
        input=input_schema or self.schema,
        retry_policy=retry_policy,
        cache_policy=cache_policy,
        ends=destinations,
        defer=defer,
    )
```

### 4.2 `add_edge` vs `add_conditional_edges`

| API | 语义 | 编译后落点 |
|-----|------|-----------|
| `add_edge("a", "b")` | a 完成 → b 必跑 | `edges` |
| `add_edge(["a", "b"], "c")` | a **和** b 都完成 → c 跑（join） | `waiting_edges` |
| `add_conditional_edges("a", router, {"x":"b","y":"c"})` | a 完成 → 调 `router(state)` 决定 | `branches` |
| 节点函数返回 `Command(goto="x")` | 运行时动态跳转，不需提前声明 | 不入账本 |
| 节点函数返回 `Send("x", payload)` | fan-out，不入账本 | 见 [[03-pregel-runtime#send]] |

### 4.3 `add_sequence`（v1.x 加入的糖）

```python
g.add_sequence([step1, step2, step3])
# 等价于 add_node + add_edge 三连
```

---

## 5. State schema → Channels 的翻译

这是 `StateGraph` 的灵魂步骤。发生在 `__init__` 而不是 `compile()`：

```python
def __init__(self, state_schema=None, config_schema=None, *,
             input_schema=None, output_schema=None):
    self.schema = state_schema
    self.input_schema = input_schema or state_schema
    self.output_schema = output_schema or state_schema
    self.channels, self.managed, ... = _get_channels(state_schema)
```

`_get_channels` 的核心规则：

| schema 字段写法 | 翻译为 |
|----------------|--------|
| `x: int` | `LastValue[int]`（"后写覆盖前写"） |
| `x: Annotated[list, add_messages]` | `BinaryOperatorAggregate[list, add_messages]` |
| `x: Annotated[T, ManagedValue]` | 不进 channel，进 `managed`，运行时注入 |
| 没显式 reducer 的 list/dict | 仍然 `LastValue`（不会自动 merge！） |

> **常见踩坑**：把 `messages: list[Message]` 当成"会自动追加"，结果每次都被覆盖。一定要写 `Annotated[list, add_messages]` 才进 reducer。

支持的 schema 形式：

| 形式 | 备注 |
|------|------|
| `TypedDict` | 最常用 |
| `Pydantic BaseModel v2` | 自动校验，但 reducer 要用 `Annotated` |
| `dataclass` | 同上 |
| 三 schema 分离 | `StateGraph(State, input_schema=Input, output_schema=Output)` |

---

## 6. `compile()`：从账本到 Pregel

<!-- StateGraph.compile() 编译时序 -->
````mermaid
sequenceDiagram
    autonumber
    participant U as User
    participant SG as StateGraph
    participant V as _validate
    participant CSG as CompiledStateGraph
    participant PN as PregelNode
    participant CH as Channels

    U->>SG: compile(checkpointer, interrupt_before, ...)
    SG->>V: 校验节点/边/孤立点
    V-->>SG: ok
    SG->>CSG: 构造 CompiledStateGraph(builder=self, channels={**schema, START: Ephemeral})

    loop 每个 add_node 记录
        SG->>CSG: attach_node(key, NodeSpec)
        CSG->>PN: 包装 runnable, retry, cache, writers
        CSG->>CH: 注册 key__inbox channel
    end

    loop 每条普通边 (a, b)
        SG->>CSG: attach_edge(a, b)
        CSG->>PN: b.triggers += a__done
    end

    loop 每条 join 边 ((a,b), c)
        SG->>CSG: attach_edge((a,b), c)
        CSG->>CH: 注册 __join_a_b channel
        CSG->>PN: c.triggers += __join_a_b
    end

    loop 每个 conditional branch
        SG->>CSG: attach_branch(src, name, BranchSpec)
        CSG->>CH: 注册 BranchChannel(router, mapping)
    end

    CSG->>CSG: 挂 checkpointer / store / cache / debug
    CSG-->>U: CompiledStateGraph (Pregel ready)
```
> 源文件：[`diagrams/state-graph-compile.mmd`](../diagrams/state-graph-compile.mmd)

### 6.1 编译做的 6 件事

1. **校验**
   - 所有 `add_edge` 引用的节点都存在
   - 没有节点被孤立（`validate=True` 时检查）
   - 没有循环依赖以外的死代码
   - START 至少有一条出边
2. **节点装配**：把 `StateNodeSpec` 包装成 `PregelNode`，写入 `Pregel.nodes`
3. **触发器写入**：每个节点订阅其入边对应的"触发 channel"。条件边的触发由 `BranchChannel` 实现
4. **预算 channels**：合并 schema 推出的 channels + 每个边/branch 引入的内部 channels
5. **挂中断点**：`interrupt_before` / `interrupt_after` 的节点登记到 Pregel 的中断列表
6. **挂 checkpointer / store / cache / debug** 等运行时配置

简化代码：

```python
def compile(self, checkpointer=None, *, store=None, cache=None,
            interrupt_before=None, interrupt_after=None,
            debug=False, name="LangGraph"):
    if self.compiled:
        raise RuntimeError("StateGraph already compiled")
    self._validate(interrupt_before, interrupt_after)

    compiled = CompiledStateGraph(
        builder=self,
        schema_to_mapper={},
        config_type=self.config_schema,
        nodes={},
        channels={
            **self.channels,
            START: EphemeralValue(self.input_schema),
        },
        input_channels=START,
        stream_mode="updates",
        output_channels=END,
        stream_channels=...,
        checkpointer=checkpointer,
        store=store,
        cache=cache,
        interrupt_before_nodes=interrupt_before or [],
        interrupt_after_nodes=interrupt_after or [],
        debug=debug,
        name=name,
    )

    for key, node in self.nodes.items():
        compiled.attach_node(key, node)
    for start, end in self.edges:
        compiled.attach_edge(start, end)
    for starts, end in self.waiting_edges:
        compiled.attach_edge(starts, end)
    for src, branches in self.branches.items():
        for name_, branch in branches.items():
            compiled.attach_branch(src, name_, branch)

    self.compiled = True
    return compiled
```

### 6.2 `attach_node` 关键步骤

```python
def attach_node(self, key, node):
    # 1. 包装为 PregelNode（语义：channel-driven actor）
    self.nodes[key] = PregelNode(
        triggers=[],                        # 由 attach_edge 填充
        channels=node.input_channels,       # 这个节点要读的 channels
        mapper=node.input_mapper,
        writers=[ChannelWrite(...)],        # 默认写回 state channels
        bound=node.runnable,
        retry_policy=node.retry_policy,
        cache_policy=node.cache_policy,
        metadata=node.metadata,
    )
    # 2. 自身需要的 internal channels
    self.channels[f"{key}__inbox"] = EphemeralValue(node.input)
```

### 6.3 `attach_edge` 三种形态

| 边类型 | 编译动作 |
|--------|---------|
| 普通 `(a, b)` | 把 b 的 triggers 加上 `a__done` channel；a 完成时往该 channel 写入 |
| join `((a, b), c)` | 引入 `__join_a_b__` channel，a/b 都写完才触发 c |
| conditional `(a, router)` | 引入 `BranchChannel`，运行时按 router 返回值动态写入下游触发 |

### 6.4 START / END 的特殊处理

| 名字 | 角色 |
|------|------|
| `START` | 整个图的输入 channel；`compile` 时被注册为 `EphemeralValue` |
| `END` | 整个图的输出 channel；任何节点 `add_edge(x, END)` 表示终止流 |

它们不是真节点，是哨兵字符串（`"__start__"` / `"__end__"`），底层是 channel。

---

## 7. 运行时 API：CompiledStateGraph 暴露什么

| API | 用途 | 底层 |
|-----|------|------|
| `invoke(input, config)` | 同步跑到结束 | `Pregel.invoke` |
| `stream(input, config, stream_mode=...)` | 同步流式 | `Pregel.stream` |
| `ainvoke / astream` | 异步版 | `Pregel.aXxx` |
| `get_state(config)` | 当前 state 快照 | `checkpointer.get_tuple` |
| `get_state_history(config)` | 历史超步列表 | `checkpointer.list` |
| `update_state(config, values, as_node=...)` | 手动改 state（HITL 修补） | 写一笔 checkpoint |
| `get_graph(xray=False)` | 静态图导出（mermaid / png） | 走 `_render_graph` |
| `with_config(...)` | 绑定默认 config | `RunnableBindingBase` |

---

## 8. 子图：StateGraph 节点可以是另一个 CompiledGraph

```python
inner = StateGraph(InnerState).add_node(...).compile()
outer = StateGraph(OuterState).add_node("inner", inner).add_edge(START, "inner").compile()
```

源码上靠两点支撑：

1. `add_node` 接受任何 `Runnable`，子图本身就是 `Runnable`
2. 父子图共享 `checkpointer` 时，按 `checkpoint_ns` 分层命名（`outer:inner` 这样的层级 ID）

注意点：

- 父子 schema 字段如果同名，**不会自动合并**，靠子图入参/出参做映射
- `update_state` 时要传 `subgraphs=True` 才能拿到子图历史
- 子图的中断会冒泡到父图

详细见 [[09-subgraph-functional-api]]。

---

## 9. 与 `Graph` / `MessageGraph` 的关系

| 类 | 何时用 | 本文档关心 |
|----|-------|-----------|
| `Graph` | 不需要 state schema，纯函数流 | 概念上是 StateGraph 的子集 |
| `MessageGraph` | message-only Agent，自动 `Annotated[list, add_messages]` | StateGraph 的语法糖 |
| `StateGraph` | 几乎所有现代写法 | ✅ 主线 |

`MessageGraph` 实质：

```python
class MessagesState(TypedDict):
    messages: Annotated[list[AnyMessage], add_messages]

MessageGraph = lambda: StateGraph(MessagesState)   # 简化
```

---

## 10. 常见错误与编译期校验

| 错误 | 触发条件 | 解决 |
|------|---------|------|
| `Node X already present` | 重复 `add_node` | 改名或先 `remove` |
| `Cannot add edge to entry point END` | `add_edge(END, x)` | END 是终点不能出边 |
| `START needs an outgoing edge` | 忘了 `add_edge(START, ...)` | 至少一条入图边 |
| `Branch destination Y not found` | conditional 的 mapping 写错节点 | 校对节点名 |
| `Cannot have channel X with reducer mismatch` | 多处推断出冲突 reducer | 统一用 `Annotated` 标准写法 |
| `compile called twice` | 重复 compile 同一 builder | 不允许，新建 builder |
| `Found unreachable node Z` | `validate=True` 时孤立节点 | 加边或删节点 |

---

## 11. 性能与陷阱

| 项 | 说明 |
|---|------|
| 编译开销 | 全在内存，O(节点数 + 边数)；可缓存 `app` 复用 |
| Reducer 副作用 | reducer 会被并发调用，必须 **pure**，禁止改入参 |
| `Annotated[list, add]` vs `add_messages` | `add` 不去重不识别 `tool_call_id`；message 列表必须用 `add_messages` |
| Pydantic v2 schema | 校验有开销；高频调用考虑 TypedDict |
| `defer=True` 节点 | 推迟到该层最后执行；常见于"汇总"节点 |
| `cache_policy` | 命中时跳过节点函数，但仍写 channels；要保证 reducer 幂等 |
| `destinations=` | 只是元数据，**不**强制运行时只能去这些节点；用于 `get_graph()` 可视化 |

---

## 12. 与 Dawning 的对应

| LangGraph 概念 | Dawning 对应 | 备注 |
|----------------|-------------|------|
| `StateGraph` | `IWorkflowBuilder`（规划） | 同样是 builder pattern |
| `add_node` | `IWorkflowBuilder.AddStep` | Dawning 现以 attribute + DI 自动发现为主 |
| `add_conditional_edges` | `IWorkflowBuilder.Branch` | 路由函数等价 |
| `compile()` | `IWorkflowEngine.Compile` | Dawning 编译目标可以是 Temporal / 自建 |
| State schema → Channels | Dawning `IWorkingMemory` schema | reducer 待对齐 |
| START/END 哨兵 | Dawning `WorkflowEdges.Start/End` | 直接抄 |
| `update_state` | Dawning `IWorkflowControl.PatchState` | HITL 修补必备 |
| `get_state_history` | Dawning `IWorkflowHistory` | 走 checkpointer 适配器 |

---

## 13. 阅读顺序

- 想懂"图怎么跑" → [[03-pregel-runtime]]
- 想懂"reducer 为什么要 pure" → [[04-channels]]
- 想懂"checkpoint 在哪写" → [[05-checkpointer]]
- 想懂"interrupt 怎么暴露给前端" → [[06-interrupt-hitl]]
- 想懂"prebuilt 怎么用 StateGraph 搭" → [[08-prebuilt-agents]]

---

## 14. 延伸阅读

- 官方 API：<https://langchain-ai.github.io/langgraph/reference/graphs/>
- Conceptual Guide - Low Level：<https://langchain-ai.github.io/langgraph/concepts/low_level/>
- Source：`libs/langgraph/langgraph/graph/state.py`
- [[01-architecture]] §3 一次运行的生命周期
- [[../cross-module-comparison/state-model.zh-CN]]（待写）
