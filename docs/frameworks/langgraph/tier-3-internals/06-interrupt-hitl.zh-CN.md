---
framework: langgraph
version: v1.1.x
type: synthesis
module: interrupt-hitl
repo-path: libs/langgraph/langgraph/{types.py,pregel/}
tags: [langgraph, interrupt, hitl, command, resume]
created: 2026-04-18
updated: 2026-04-18
status: active
subtype: internals
title: LangGraph — 06 Interrupt 与 HITL：暂停、决策、续跑
sources: []
---

# LangGraph — 06 Interrupt 与 HITL：暂停、决策、续跑

> 本文回答：`interrupt(value)` 在节点里抛出后，到底发生了什么？前端怎么拿到值、怎么 `resume`？为什么 LinkedIn / Klarna 都用它做 HITL？


## 目录 <!-- TOC-AUTOGEN -->

- [1. 范围](#1-范围)
- [2. 一句话回答](#2-一句话回答)
- [3. 三种暂停机制对比](#3-三种暂停机制对比)
- [4. `interrupt()` 的源码](#4-interrupt-的源码)
- [5. 一次 HITL 的完整生命周期](#5-一次-hitl-的完整生命周期)
- [6. `Command` 的三个参数](#6-command-的三个参数)
- [7. 节点重跑的副作用问题](#7-节点重跑的副作用问题)
- [8. 多次 interrupt（同一节点）](#8-多次-interrupt同一节点)
- [9. 子图中的 interrupt](#9-子图中的-interrupt)
- [10. 静态中断：`interrupt_before` / `interrupt_after`](#10-静态中断interruptbefore--interruptafter)
- [11. 前端对接协议](#11-前端对接协议)
- [12. 错误清单](#12-错误清单)
- [13. 性能与陷阱](#13-性能与陷阱)
- [14. 与 Dawning 的对应](#14-与-dawning-的对应)
- [15. 阅读顺序](#15-阅读顺序)
- [16. 延伸阅读](#16-延伸阅读)
<!-- /TOC-AUTOGEN -->

> 重点路径：`langgraph/types.py`（`interrupt` / `Command` / `Send`）、`pregel/runner.py`（捕获）、`pregel/loop.py`（落 checkpoint）。

---

## 1. 范围

| 在范围 | 不在范围 |
|--------|---------|
| `interrupt()` 函数语义 | Pregel 调度细节 → [[03-pregel-runtime]] |
| `Command(resume=...)` 续跑路径 | Checkpoint schema → [[05-checkpointer]] |
| `interrupt_before` / `interrupt_after` 的静态中断 | 子图嵌套 interrupt 行为 → [[09-subgraph-functional-api#interrupt]] |
| 与前端的协议 | 部署 / Studio UI → [[10-platform-integration]] |

---

## 2. 一句话回答

> `interrupt(value)` 在节点里**抛出 `GraphInterrupt`**，runner 捕获后把 `value` 写进 checkpoint 的 `pending_writes`，
> 把当前 task 标记为 `interrupted`，loop 优雅退出。
> 前端拿到 `value`（含暂停位置）展示给用户，
> 用户决策后，`graph.invoke(Command(resume=decision), config)` 让 loop 从同一节点重跑，
> 而 `interrupt()` 这次**不抛**，**返回** `decision`。

---

## 3. 三种暂停机制对比

| 机制 | 触发位置 | 粒度 | 用途 |
|------|---------|------|------|
| `interrupt(value)` 函数 | 节点内任意位置 | 动态 / 携带值 | HITL 主力 |
| `interrupt_before=["x"]` 编译参数 | 节点 X 之前 | 静态 | 调试 / 固定确认点 |
| `interrupt_after=["x"]` 编译参数 | 节点 X 之后 | 静态 | 调试 / 看输出 |

> **生产建议**：`interrupt()` 是产品功能，`interrupt_before/after` 是开发工具。

---

## 4. `interrupt()` 的源码

```python
# langgraph/types.py
def interrupt(value: Any) -> Any:
    """暂停当前节点，把 value 暴露给客户端。下次 invoke 带 Command(resume=...) 时返回 resume 值。"""
    config = get_config()                            # 由 Pregel 注入到 contextvar
    scratchpad = config["configurable"][CONFIG_KEY_SCRATCHPAD]

    # 1. 看 scratchpad 是否已经有这次 interrupt 的 resume 值
    idx = scratchpad.interrupt_counter()
    resumes = scratchpad.resume_values()             # 来自 Command(resume=...)
    if idx < len(resumes):
        return resumes[idx]                          # ← 第二次进入：直接返回 resume 值

    # 2. 第一次进入：抛 GraphInterrupt（runner 会捕获）
    raise GraphInterrupt([Interrupt(value=value, resumable=True, ns=...)])
```

**两个关键点**：

- 节点函数被 **重跑**（resume 后整个节点重新执行）
- 用 `interrupt_counter` 区分"第几次 interrupt"——同一节点可以多次 `interrupt()`，按调用顺序对应 resume 列表

---

## 5. 一次 HITL 的完整生命周期

<!-- interrupt 与 HITL 生命周期 -->
````mermaid
sequenceDiagram
    autonumber
    participant FE as 前端
    participant API as API
    participant G as Graph
    participant N as Node Fn
    participant S as Scratchpad
    participant CK as Checkpointer
    participant U as User

    FE->>API: invoke(input, config)
    API->>G: stream(input, config)
    G->>N: 第一次执行 node
    N->>S: scratchpad.interrupt_counter()=0
    N->>N: interrupt(draft) 抛 GraphInterrupt
    G->>CK: put(checkpoint, pending_writes=[Interrupt(draft)])
    G-->>API: stream event: interrupt(draft)
    API-->>FE: SSE event:interrupt
    FE-->>U: 渲染审阅 UI

    Note over U: 审阅，决定 approve/edit
    U->>FE: 提交决策
    FE->>API: invoke(Command(resume=decision), config)
    API->>G: 加载 checkpoint
    G->>N: 第二次执行 node（同一 task）
    N->>S: scratchpad.interrupt_counter()=0
    N->>S: resumes=[decision]
    N-->>N: interrupt() 不抛, 返回 decision
    N->>N: 继续节点逻辑
    N-->>G: 返回 dict / Command
    G->>CK: apply_writes + put(checkpoint)
    G-->>API: stream events 继续
    API-->>FE: 完成
```
> 源文件：[`diagrams/interrupt-lifecycle.mmd`](../diagrams/interrupt-lifecycle.mmd)

---

## 6. `Command` 的三个参数

```python
class Command:
    update: dict | None = None       # 等价于节点返回 dict
    goto: str | list[str] | Send | list[Send] | None = None  # 跳转
    resume: Any = None               # 喂给 interrupt() 的返回值
```

三种典型组合：

| 用法 | 含义 |
|------|-----|
| `Command(update={"x": 1})` | 单纯更新 state |
| `Command(goto="next_node")` | 单纯跳转 |
| `Command(resume="approved")` | 传给暂停的 interrupt() |
| `Command(resume="approved", update={"reviewer":"alice"})` | 续跑同时打补丁 |

---

## 7. 节点重跑的副作用问题

```python
def draft_outreach(state):
    db.insert("draft_logs", ...)        # ← 副作用！
    draft = llm.invoke(...)              # ← 副作用 + 钱
    decision = interrupt({"draft": draft})
    return {...}
```

**问题**：resume 后整个节点重跑 → DB 多写一条 + 多花 LLM 钱。

**解法**：

| 方法 | 说明 |
|------|-----|
| 把副作用移到 `interrupt()` **之后** | 最简单，前提是用户能接受副作用在确认后才发生 |
| 用 `cache_policy` | 第二次跑命中 cache 跳过节点函数 |
| 副作用本身幂等 | 如 `INSERT ... ON CONFLICT IGNORE` |
| 拆节点 | "做副作用 → interrupt → 用决策" 拆三个节点 |

> **黄金法则**：`interrupt()` 之前的代码必须**纯（pure）或幂等**。

---

## 8. 多次 interrupt（同一节点）

```python
def confirm_steps(state):
    for step in state["steps"]:
        approved = interrupt({"step": step, "ask": "approve?"})
        if not approved:
            return Command(goto="abort")
    return {"all_approved": True}
```

第一次跑：第一个 `interrupt` 抛出，前端拿到 `step[0]`。
resume 第一次：`Command(resume=True)`，重跑节点，第一个 interrupt 返回 True，第二个 interrupt 抛出。
依此类推。

> **scratchpad 自动管理 resume 列表的下标**，用户无需关心。

---

## 9. 子图中的 interrupt

子图节点 `interrupt()` → `GraphInterrupt` **冒泡到父图** → 落到父图 checkpoint。

恢复时父图 invoke `Command(resume=...)`，会沿 `checkpoint_ns` 路由回子图同位置：

```
父图 thread_id=abc
└─ 子图 ns="abc:section_42"
   └─ interrupt(...)   ← resume 回到这里
```

**要点**：

- 父图 `get_state(config, subgraphs=True)` 才能拿到子图的暂停态
- `update_state(target_subgraph_config, ...)` 也支持子图寻址
- 子图 + Send fan-out 同时 interrupt 时，**多个 interrupt 并存**于父 checkpoint，`Command(resume=[...])` 用列表对齐

---

## 10. 静态中断：`interrupt_before` / `interrupt_after`

```python
graph = builder.compile(
    checkpointer=PostgresSaver(...),
    interrupt_before=["approve_node"],
    interrupt_after=["draft_node"],
)
```

**与 `interrupt()` 的关键差异**：

| 维度 | `interrupt()` 函数 | `interrupt_before/after` |
|------|-------------------|------------------------|
| 触发 | 运行时动态 | 编译时静态 |
| 携带值 | 任意 | 无（只暴露当时 state） |
| 节点是否执行 | before：未执行 / after：已执行 | 函数：在中间 |
| 续跑 | `Command(resume=value)` | `invoke(None, config)` 即可 |
| 用途 | 产品级 HITL | 调试 / Studio breakpoint |

---

## 11. 前端对接协议

LangGraph Studio / Platform 暴露的 REST 协议（自托管也用同一套）：

| 接口 | 含义 |
|------|------|
| `POST /threads/{thread_id}/runs` | 启动一次 invoke，body 含 input + config |
| `GET /threads/{thread_id}/state` | 拿当前 state + 暂停信息（`tasks` 字段含 `interrupts: [Interrupt(...)]`） |
| `POST /threads/{thread_id}/runs` body=`{command:{resume:...}}` | 续跑 |
| `GET /threads/{thread_id}/history` | 历史链 |
| `POST /threads/{thread_id}/state` | `update_state(...)` 等价 |
| WebSocket / SSE `/runs/stream` | 实时 stream（含 `event:interrupt`） |

**关键事件**：客户端订阅 stream 时，收到 `event:interrupt` 立即停止等待，渲染审阅 UI。

---

## 12. 错误清单

| 错误 | 触发 | 解法 |
|------|-----|------|
| `interrupt() called without checkpointer` | 没配 checkpointer | 必须配（HITL 需要持久化） |
| resume 后行为异常 | 节点有副作用，重跑不幂等 | 见 §7 |
| `Command(resume=...)` 无效 | 没在暂停状态 / config 不匹配 | `get_state()` 确认 `tasks[].interrupts` 非空 |
| 多 interrupt 顺序错乱 | 节点内 `interrupt` 顺序与 resume 列表不一致 | 不要在 `interrupt` 调用处加条件分支 |
| 子图 interrupt 不可恢复 | 父图配置 ns 不匹配 | `get_state(subgraphs=True)` 取真实 config |

---

## 13. 性能与陷阱

| 项 | 说明 |
|---|------|
| 每个 `interrupt()` 写 1 笔 checkpoint | 高频 HITL 的成本主要在 DB |
| Resume 重跑节点 | LLM 调用会重复，注意 cache_policy |
| `Interrupt.value` 必须可序列化 | 不要塞 lambda / DB 连接对象 |
| 长时间挂起的 thread | 客户端不来 resume → 资源占用；建议 TTL 归档 |
| 多人协作 | 同一 thread 同时 resume 会冲突 → actor lock |

---

## 14. 与 Dawning 的对应

| LangGraph | Dawning |
|-----------|---------|
| `interrupt(value)` | `IHitlGate.RequestAsync(value, ct)` |
| `GraphInterrupt` | `WorkflowSuspendedException`（内部） |
| `Command(resume=...)` | `IWorkflowControl.ResumeAsync(decision)` |
| `interrupt_before/after` | `IWorkflowDebugger.SetBreakpoint(node)` |
| `Interrupt.resumable` | `SuspensionToken.CanResume` |
| 多 interrupt scratchpad | `IInterruptCounter` |
| 前端 SSE / WS | Dawning Hub 推 `WorkflowEvent.Interrupt` |
| `update_state(as_node=...)` 修补 | `IWorkflowControl.PatchAsync(nodeName, values)` |

---

## 15. 阅读顺序

- 已读 → [[03-pregel-runtime#8]] interrupt 在 runtime 落点
- 已读 → [[05-checkpointer]] 知道 pending_writes 是什么
- 案例 → [[cases/linkedin-hr-agent.zh-CN]] 强 HITL 实例
- 下一步 → [[07-streaming]] interrupt 事件如何流向前端
- 进阶 → [[09-subgraph-functional-api]] 子图 interrupt 的命名空间

---

## 16. 延伸阅读

- 官方 HITL 概念：<https://langchain-ai.github.io/langgraph/concepts/human_in_the_loop/>
- How-to：<https://langchain-ai.github.io/langgraph/how-tos/human_in_the_loop/>
- 源码：`libs/langgraph/langgraph/types.py`
- [[../../concepts/hitl-pattern.zh-CN]]（待写）
