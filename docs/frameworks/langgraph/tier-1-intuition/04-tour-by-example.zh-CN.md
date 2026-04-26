---
framework: langgraph
tier: 1
type: synthesis
chapter: 04
title: 一个逐步演进的例子 —— 把所有概念用一遍
tags: [langgraph, tier-1, tour, example]
audience: 读完 03 的新人
reading-time: 45 min
created: 2026-04-21
updated: 2026-04-21
status: active
subtype: intuition
sources: []
---

# 04 · 逐步演进 —— 把所有概念用一遍

> 前置：[[03-mental-model]]
> 目标：从最简单例子出发，每一步加一个需求，带出一个新概念。
> 读完你能**写**（不是只读）一个带 HITL + stream + 子图的 Agent。


## 目录 <!-- TOC-AUTOGEN -->

- [场景](#场景)
- [V1：最简单的线性流程](#v1最简单的线性流程)
- [V2：加一个润色步骤](#v2加一个润色步骤)
- [V3：加条件分支](#v3加条件分支)
- [V4：并行翻译多个段（fan-out）—— 引入 Reducer](#v4并行翻译多个段fan-out-引入-reducer)
- [V5：加 HITL —— 人工审核敏感词](#v5加-hitl--人工审核敏感词)
- [V6：加流式输出](#v6加流式输出)
- [V7：抽成子图，复用](#v7抽成子图复用)
- [V8：串起所有功能](#v8串起所有功能)
- [回顾](#回顾)
- [下一步](#下一步)
<!-- /TOC-AUTOGEN -->

---

## 场景

我们做一个"**简易翻译 Agent**"：

- 用户给一段中文
- Agent 翻译成英文
- 逐步加需求：润色、人工审核、流式进度、拆子图复用

每步都是一个可跑的 Python 文件。

---

## V1：最简单的线性流程

**需求**：中文 → 翻译 → 输出。

```python
# v1.py
from typing import TypedDict
from langgraph.graph import StateGraph, START, END

class State(TypedDict):
    chinese: str
    english: str

def translate(state):
    # 模拟翻译（真实场景调 LLM）
    return {"english": f"[EN] {state['chinese']}"}

graph = StateGraph(State)
graph.add_node("translate", translate)
graph.add_edge(START, "translate")
graph.add_edge("translate", END)
app = graph.compile()

print(app.invoke({"chinese": "你好世界"}))
# {'chinese': '你好世界', 'english': '[EN] 你好世界'}
```

**用到**：图、state、节点。**没用到**：channel reducer、checkpoint、HITL、stream。

---

## V2：加一个润色步骤

**需求**：翻译完后再润色一遍。

```python
# v2.py 差异部分
class State(TypedDict):
    chinese: str
    draft: str          # 新增
    english: str

def translate(state):
    return {"draft": f"[EN raw] {state['chinese']}"}

def polish(state):
    return {"english": state["draft"].replace("raw", "polished")}

graph = StateGraph(State)
graph.add_node("translate", translate)
graph.add_node("polish", polish)      # 新增
graph.add_edge(START, "translate")
graph.add_edge("translate", "polish") # 新增
graph.add_edge("polish", END)
```

图：`START → translate → polish → END`

**用到**：前面的 + **通过 state 传递中间结果** (`draft`)。

---

## V3：加条件分支

**需求**：如果原文太长（>50 字），先拆段再翻译。

```python
# v3.py
from typing import TypedDict, Literal

class State(TypedDict):
    chinese: str
    segments: list
    english: str

def decide(state) -> Literal["split", "direct"]:
    return "split" if len(state["chinese"]) > 50 else "direct"

def split(state):
    # 简化：按句号拆
    return {"segments": state["chinese"].split("。")}

def direct(state):
    return {"english": f"[EN] {state['chinese']}"}

def translate_segments(state):
    translated = [f"[EN] {s}" for s in state["segments"]]
    return {"english": " ".join(translated)}

graph = StateGraph(State)
graph.add_node("split", split)
graph.add_node("direct", direct)
graph.add_node("translate_segments", translate_segments)

# 条件边：从 START 根据 decide 决定去哪
graph.add_conditional_edges(START, decide, ["split", "direct"])
graph.add_edge("split", "translate_segments")
graph.add_edge("translate_segments", END)
graph.add_edge("direct", END)

app = graph.compile()

print(app.invoke({"chinese": "你好"}))
# → 走 direct
print(app.invoke({"chinese": "很长的句子" * 20}))
# → 走 split → translate_segments
```

图：

```
              ┌── direct ────────────────┐
START → decide                            ├→ END
              └── split → translate_segs ─┘
```

**新用到**：**条件边**（`add_conditional_edges`）。

---

## V4：并行翻译多个段（fan-out）—— 引入 Reducer

**需求**：V3 里 `translate_segments` 一个一个翻太慢。希望**每段一个节点**并行翻，最后合起来。

这里会第一次遇到 **channel reducer**。

```python
# v4.py
from typing import TypedDict, Annotated
from langgraph.graph import StateGraph, START, END
from langgraph.types import Send
from operator import add

class State(TypedDict):
    chinese: str
    segments: list
    translated: Annotated[list, add]   # ← 关键：用 reducer add，多节点并行写时自动追加
    english: str

def split(state):
    return {"segments": state["chinese"].split("。")}

# 用 Send fan-out 到多个 worker
def dispatch(state):
    return [Send("translate_one", {"segment": s}) for s in state["segments"]]

# worker：输入是一小段
def translate_one(payload):
    # payload 是 Send 传过来的小 dict
    return {"translated": [f"[EN] {payload['segment']}"]}

def merge(state):
    return {"english": " ".join(state["translated"])}

graph = StateGraph(State)
graph.add_node("split", split)
graph.add_node("translate_one", translate_one)
graph.add_node("merge", merge)

graph.add_edge(START, "split")
graph.add_conditional_edges("split", dispatch, ["translate_one"])  # fan-out
graph.add_edge("translate_one", "merge")  # 所有 worker 跑完再到 merge
graph.add_edge("merge", END)

app = graph.compile()
print(app.invoke({"chinese": "句子一。句子二。句子三"}))
```

图：

```
                   ┌ translate_one ┐
START → split ────▶ translate_one ─┼─▶ merge → END
                   └ translate_one ┘
```

**新用到**：
- `Annotated[list, add]`：声明 `translated` 字段的 reducer 是 `add`（追加），避免多 worker 并行写冲突
- `Send`：同一节点被排**多份独立任务**，每份独立 state
- **fan-in**：LangGraph 自动等所有 `translate_one` 跑完才进 `merge`（BSP barrier）

---

## V5：加 HITL —— 人工审核敏感词

**需求**：润色结果里如果包含某些词（比如涉及人名），要**停下来让人审核**。

这里引入 **checkpoint** 和 `interrupt()`。

```python
# v5.py
from typing import TypedDict
from langgraph.graph import StateGraph, START, END
from langgraph.types import interrupt, Command
from langgraph.checkpoint.memory import MemorySaver

class State(TypedDict):
    chinese: str
    draft: str
    english: str
    approved: bool

def translate(state):
    return {"draft": f"[EN] {state['chinese']}"}

def review(state):
    if "人名" in state["chinese"]:
        # 暂停，等人决定
        decision = interrupt({"draft": state["draft"], "ask": "批准输出吗？"})
        return {"approved": decision == "yes"}
    return {"approved": True}

def finalize(state):
    if state["approved"]:
        return {"english": state["draft"]}
    return {"english": "(已撤回)"}

graph = StateGraph(State)
graph.add_node("translate", translate)
graph.add_node("review", review)
graph.add_node("finalize", finalize)
graph.add_edge(START, "translate")
graph.add_edge("translate", "review")
graph.add_edge("review", "finalize")
graph.add_edge("finalize", END)

# 必须有 checkpointer 才能 interrupt/resume
app = graph.compile(checkpointer=MemorySaver())

config = {"configurable": {"thread_id": "t1"}}

# 第一次：可能在 review 处暂停
result = app.invoke({"chinese": "翻译涉及人名的一句话"}, config)
print("第一次结果:", result)
# → 看到 result.get("__interrupt__") 有值，说明需要审核

# 模拟前端展示给 HR，HR 点了"批准"
result = app.invoke(Command(resume="yes"), config)
print("恢复后:", result)
# → 从 review 内部那一行继续，decision = "yes"
```

**新用到**：
- `interrupt(value)`：暂停，把 `value` 暴露给外部
- `Command(resume=...)`：把人的输入塞回来，从原地继续
- `MemorySaver()`：checkpoint 后端（这里用内存；生产换 Postgres）
- `thread_id`：标识一次会话

**关键领悟**：`interrupt()` 像个 **可跨进程、可跨几天的阻塞调用**。中间可以关进程、换机器，只要 `thread_id` 一样就能接上。

---

## V6：加流式输出

**需求**：前端想实时看到进度（每个节点跑完就推一条）。

```python
# v6.py
# 图结构同 V2 （translate → polish）

app = graph.compile()

# 不用 invoke，用 stream
for event in app.stream({"chinese": "你好"}, stream_mode="updates"):
    print(event)
# 输出：
# {'translate': {'draft': '[EN raw] 你好'}}
# {'polish':    {'english': '[EN polished] 你好'}}
```

**stream_mode** 决定"推什么粒度"：

| mode | 内容 |
|------|------|
| `values` | 每轮完整 state |
| `updates` | **每个节点的增量**（最常用） |
| `messages` | LLM 每个 token（聊天 UI） |
| `custom` | 节点内手动 emit 的任意事件 |

多 mode 叠加：

```python
for mode, event in app.stream(input, stream_mode=["updates", "messages"]):
    ...
```

**领悟**：stream 不改变图逻辑，只是**把跑的过程暴露给调用方**。

---

## V7：抽成子图，复用

**需求**：V2 的 "translate → polish" 经常被其他流程复用，抽成子图。

```python
# v7.py
# 第一步：定义"翻译子图"
class TransState(TypedDict):
    chinese: str
    english: str

def translate(state):
    return {"english": f"[EN raw] {state['chinese']}"}

def polish(state):
    return {"english": state["english"].replace("raw", "polished")}

trans_graph = StateGraph(TransState)
trans_graph.add_node("translate", translate)
trans_graph.add_node("polish", polish)
trans_graph.add_edge(START, "translate")
trans_graph.add_edge("translate", "polish")
trans_graph.add_edge("polish", END)
trans = trans_graph.compile()   # 子图

# 第二步：父图复用子图
class OuterState(TypedDict):
    chinese: str
    english: str
    summary: str

def summarize(state):
    return {"summary": f"摘要：{state['english'][:5]}..."}

outer = StateGraph(OuterState)
outer.add_node("translate_pipeline", trans)   # ← 子图当节点
outer.add_node("summarize", summarize)
outer.add_edge(START, "translate_pipeline")
outer.add_edge("translate_pipeline", "summarize")
outer.add_edge("summarize", END)
app = outer.compile()

print(app.invoke({"chinese": "你好"}))
```

**新用到**：**子图作节点**。父子图共享字段（`chinese`、`english`）的情况下，直接嵌入就行。

---

## V8：串起所有功能

一个完整版（示意，代码略）：

```
START
 ├ 条件分支（短/长）
 │
 ├ 长文本路径：
 │   split → Send fan-out →
 │     [translate_one × N] → barrier → merge → review_subgraph
 │
 ├ 短文本路径：
 │   translate → polish → review_subgraph
 │
 ├ review_subgraph（子图）:
 │   detect_sensitive → [interrupt 人工] → finalize
 │
END

+ PostgresCheckpointer（生产）
+ stream_mode=["updates", "messages", "custom"]
+ thread_id 管理会话
```

这就是一个**真·生产 Agent 的骨架**。LangGraph 的 4 个核心概念全用到：

| 概念 | 本例哪里用 |
|------|---------|
| 图 | 整体结构 + 条件边 + fan-out |
| State | `chinese/segments/translated/english/approved/summary` |
| Channel（+reducer） | `translated: Annotated[list, add]` 处理并行写 |
| Checkpoint | `PostgresCheckpointer` + `thread_id` + `interrupt` |

---

## 回顾

从 V1 到 V8 你建立了：

| 版本 | 新增概念 | 应对需求 |
|------|---------|---------|
| V1 | 图 / state / 节点 | 最简流程 |
| V2 | 中间结果 state | 多步 pipeline |
| V3 | 条件边 | 分支决策 |
| V4 | Reducer / Send / fan-out | 并发加速 |
| V5 | Checkpoint / interrupt / resume | 人工审核 / 长任务 |
| V6 | Stream modes | 实时进度 |
| V7 | 子图 | 复用 |
| V8 | 全部综合 | 生产级 |

---

## 下一步

读完这 4 篇 tier-1，你已经建立完整的**心智模型**。接着：

- **想看更大图 / 真实业务骨架** → [[../tier-2-architecture/00-overview]]（框架全景）
- **想看完整模块调用关系** → [[../tier-2-architecture/01-architecture]]
- **想看源码怎么写的** → tier-3-internals/ 目录按需读
- **想看真实企业案例** → [[../cases/README.zh-CN|cases/]]（klarna / open-deep-research / replit-agent / linkedin-hr-agent）
- **想看 LangGraph vs Dawning 映射** → [[../cross-module-comparison]]
