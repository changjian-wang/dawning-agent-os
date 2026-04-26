---
framework: langgraph
tier: 1
type: synthesis
chapter: 02
title: Hello World — 15 行跑起来
tags: [langgraph, tier-1, hello-world]
audience: 读完 01 的新人
reading-time: 20 min
created: 2026-04-21
updated: 2026-04-21
status: active
subtype: intuition
sources: []
---

# 02 · Hello World —— 15 行跑起来

> 前置：[[01-what-is-langgraph]]
> 目标：跑通第一个 LangGraph，**每一行都懂**。
> 本篇不涉及 HITL / stream / 子图。这些留到 [[04-tour-by-example]]。

---

## 1. 环境准备（.NET 工程师版）

```bash
# 推荐用 uv（Rust 写的新版 pip，非常快）
brew install uv
mkdir lg-hello && cd lg-hello
uv venv && source .venv/bin/activate
uv pip install langgraph
```

> 对照：这等价于 .NET 里 `dotnet new console && dotnet add package LangGraph`（假设存在）。
> 不装 LLM SDK 也能跑，因为 hello world 不调模型。

---

## 2. 最小可跑例子

新建 `hello.py`：

```python
# hello.py
from typing import TypedDict
from langgraph.graph import StateGraph, START, END

class State(TypedDict):        # ①
    question: str
    answer: str

def think(state: State):       # ②
    return {"answer": f"回答：{state['question']}"}

graph = StateGraph(State)      # ③
graph.add_node("think", think) # ④
graph.add_edge(START, "think") # ⑤
graph.add_edge("think", END)   # ⑥
app = graph.compile()          # ⑦

print(app.invoke({"question": "你好"}))   # ⑧
```

跑：

```bash
python hello.py
# {'question': '你好', 'answer': '回答：你好'}
```

---

## 3. 逐行解释

### ① 定义 State

```python
class State(TypedDict):
    question: str
    answer: str
```

**State** 是"所有节点共享的数据包"。这里声明了两个字段。

> **.NET 对照**：类似一个 `record State(string Question, string Answer)`。
> Python 用 `TypedDict`——本质是带类型提示的 dict，运行时就是 `dict`。

### ② 定义节点

```python
def think(state: State):
    return {"answer": f"回答：{state['question']}"}
```

**节点 = 一个函数**。签名：
- 入参：当前 state
- 返回：要更新的字段（部分字段即可）

**重点**：节点**返回的是 update**，不是完整 state。LangGraph 会帮你合并。

> **.NET 对照**：
> ```csharp
> public StateUpdate Think(State state)
>     => new() { Answer = $"回答：{state.Question}" };
> ```

### ③ 建图

```python
graph = StateGraph(State)
```

创建一个**图构建器**。`State` 类型告诉它"state 长什么样"。

> **.NET 对照**：`var graph = new WorkflowBuilder<State>();`

### ④⑤⑥ 声明节点和边

```python
graph.add_node("think", think)     # 有一个叫 "think" 的节点
graph.add_edge(START, "think")     # 开始就跑 think
graph.add_edge("think", END)       # think 跑完就结束
```

**START / END 是两个保留节点**，表示图的入口和出口。

> **.NET 对照**：
> ```csharp
> graph.AddNode("think", Think);
> graph.AddEdge(Start, "think");
> graph.AddEdge("think", End);
> ```

画出来是：

```
START  →  think  →  END
```

### ⑦ 编译

```python
app = graph.compile()
```

**编译**= 把图定义变成一个**可执行对象**。

- 编译前：`graph` 只是个描述（像 SQL 字符串）
- 编译后：`app` 能被 `.invoke() / .stream()` 调

> **.NET 对照**：`var app = graph.Build();`

### ⑧ 调用

```python
app.invoke({"question": "你好"})
```

传入 **初始 state**，拿到 **最终 state**。

运行时内部：
1. 初始化 state = `{"question": "你好", "answer": <empty>}`
2. 从 START 出发 → 跑 `think(state)` → 返回 `{"answer": "回答：你好"}`
3. 合并 → `{"question": "你好", "answer": "回答：你好"}`
4. think → END → 停
5. 返回最终 state

---

## 4. 加一个节点 —— 体会"图"

改 `hello.py`：

```python
from typing import TypedDict
from langgraph.graph import StateGraph, START, END

class State(TypedDict):
    question: str
    draft: str
    answer: str

def draft(state):
    return {"draft": f"草稿：{state['question']}"}

def polish(state):
    return {"answer": f"润色：{state['draft']}"}

graph = StateGraph(State)
graph.add_node("draft", draft)
graph.add_node("polish", polish)
graph.add_edge(START, "draft")
graph.add_edge("draft", "polish")
graph.add_edge("polish", END)
app = graph.compile()

print(app.invoke({"question": "你好"}))
# {'question': '你好', 'draft': '草稿：你好', 'answer': '润色：草稿：你好'}
```

图变成：

```
START  →  draft  →  polish  →  END
```

**关键点**：
- `draft` 写了 `draft` 字段
- `polish` 读了 `draft` 字段，写了 `answer` 字段
- **数据通过 state 传递**，不是函数参数串联

这就是**图编排**的味道——你不用关心 `draft` 的返回怎么传给 `polish`，两者都靠 state。

---

## 5. 加一个条件 —— 体会"分支"

```python
from typing import TypedDict, Literal
from langgraph.graph import StateGraph, START, END

class State(TypedDict):
    question: str
    answer: str

def classify(state) -> dict:
    if "?" in state["question"]:
        return {"answer": "这是个问题"}
    return {"answer": "这是个陈述"}

def route(state) -> Literal["short", "long"]:
    return "short" if len(state["answer"]) < 10 else "long"

def short(state):
    return {"answer": state["answer"] + "（短）"}

def long(state):
    return {"answer": state["answer"] + "（长）"}

graph = StateGraph(State)
graph.add_node("classify", classify)
graph.add_node("short", short)
graph.add_node("long", long)
graph.add_edge(START, "classify")
graph.add_conditional_edges("classify", route, ["short", "long"])  # 条件边
graph.add_edge("short", END)
graph.add_edge("long", END)
app = graph.compile()

print(app.invoke({"question": "你好吗？"}))
# {'question': '你好吗？', 'answer': '这是个问题（短）'}
```

图：

```
              ┌── short ──┐
START → classify           ├→ END
              └── long ───┘
```

**新语法**：

```python
graph.add_conditional_edges("classify", route, ["short", "long"])
```

- `"classify"` 结束后
- 调用 `route(state)` 函数
- 根据返回值（必须是 `"short"` 或 `"long"` 之一）决定跳哪个

> **.NET 对照**：
> ```csharp
> graph.AddConditionalEdges("classify", Route, new[] { "short", "long" });
> ```

---

## 6. 真·调 LLM 的版本

如果你想看真的调模型的例子：

```bash
uv pip install langchain-openai
export OPENAI_API_KEY=...
```

```python
from typing import TypedDict
from langgraph.graph import StateGraph, START, END
from langchain_openai import ChatOpenAI

llm = ChatOpenAI(model="gpt-4o-mini")

class State(TypedDict):
    question: str
    answer: str

def answer(state):
    resp = llm.invoke(state["question"])
    return {"answer": resp.content}

graph = StateGraph(State)
graph.add_node("answer", answer)
graph.add_edge(START, "answer")
graph.add_edge("answer", END)
app = graph.compile()

print(app.invoke({"question": "简单介绍 Pregel 算法"}))
```

**重点**：LangGraph 本身**不绑定 LLM**。节点里怎么调完全是你的自由。

---

## 7. 常见疑问

**Q：为什么要声明 `State` 类型？**
因为 LangGraph 要根据它判断"字段怎么合并"（后面 [[03-mental-model]] 会讲）。

**Q：节点能返回整个 state 吗？**
可以（`return state`），但建议只返回"改动的字段"——更清晰，性能更好。

**Q：节点能是 async 吗？**
可以：`async def think(state): ...` + `await app.ainvoke(...)`。

**Q：图能有循环吗？**
能。下一篇会讲。

**Q：多个节点能并行吗？**
能。两个节点从同一个上游出发，就会并行。

---

## 8. 本篇结论

- **写一个 LangGraph 应用 = 写 state schema + 节点函数 + add_node/add_edge + compile**
- **数据靠 state 传**，不是参数串联
- **流程靠图描述**，不是 if/for 堆砌
- **LangGraph 自己不调 LLM**，节点里你自由调

---

## 9. 下一步

- 下一篇 → [[03-mental-model]] 把"图 / state / channel / checkpoint" 4 个核心概念彻底讲透
- 急着看源码 → [[../tier-3-internals/02-state-graph]]
