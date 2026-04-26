---
framework: langgraph
tier: 1
type: synthesis
chapter: 01
title: LangGraph 是什么，解决什么问题
tags: [langgraph, tier-1, intro]
audience: 新人 / .NET 背景 / 想快速建立心智模型
reading-time: 15 min
created: 2026-04-21
updated: 2026-04-21
status: active
subtype: intuition
sources: []
---

# 01 · LangGraph 是什么，解决什么问题

> tier-1 第一篇。**零前置**。读完你会知道：
> - 为什么大家在纯 LLM 调用之上还要再叠一层 LangGraph
> - 它解决的是**哪一类问题**，不解决哪一类
> - 何时该用、何时不该用

---

## 1. 从一个真实场景说起

需求：做一个"**研究 Agent**"。

- 用户发一句："调研下 LangGraph 的主要竞品"
- Agent 要做的事：
  1. 先理解问题 → 如果不清楚，反问用户
  2. 拆成几个子问题 → 搜索引擎查
  3. 每个子问题的结果让 LLM 写一段
  4. 最后合并成一篇报告
  5. 期间如果 token 成本太高要暂停让人审核
  6. 用户可以随时关闭浏览器，明天回来继续

如果你只会"调 LLM API"，这个需求让你写出来的代码会**迅速失控**：

```python
# 伪代码，你会写成这样：
messages = [...]
response = llm(messages)
if "not clear" in response:
    # 反问 → 但怎么"暂停"函数等人回答？
    ...
sub_questions = parse(response)
results = []
for q in sub_questions:           # 能不能并行？
    search_result = search(q)
    section = llm([..., search_result])
    results.append(section)
    # 中途崩了怎么办？
    # 想看进度怎么办？
    # token 超预算想停怎么办？
report = llm([..., results])
```

问题一大堆：

| 问题 | 朴素方案的痛点 |
|------|-------------|
| 控制流乱 | if/for 嵌套越来越深 |
| 状态散 | `messages`/`sub_questions`/`results` 各种变量散落 |
| 断电就没 | 进程崩了全丢 |
| 想暂停等人工 | Python 函数怎么"暂停"？ |
| 想看实时进度 | 要自己搭 pub/sub |
| 并行调 LLM | 要自己写 async 编排 |

这些问题**每做一个 Agent 就要重新踩一遍**。于是有人把通用模式抽出来，就是 LangGraph。

---

## 2. LangGraph 的定位（一句话）

> **LangGraph = 一个让你用"图"的形式描述多步 LLM 流程、并自带持久化 / 暂停恢复 / 流式输出的运行时。**

拆关键词：

| 关键词 | 含义 |
|-------|------|
| **图** | 节点 = 一步要做的事，边 = 可能的下一步 |
| **描述** | 声明式：你写"图长什么样"，不写"怎么跑" |
| **运行时** | `.invoke()` 交给它，它帮你跑 |
| **持久化** | 每一步的中间结果自动存库 |
| **暂停恢复** | 任意一步能停，换个进程还能继续 |
| **流式输出** | 进度 / token / 中间结果可以实时推给前端 |

---

## 3. 三种做法对比

同样是第 1 节的研究 Agent，三种技术选择：

### ① 裸 LLM 调用

```
优点：简单，一个文件搞定
缺点：上面列的问题全部要自己处理
适合：一次性脚本、demo
```

### ② LangChain 链（LCEL）

```
优点：有 Runnable 协议，能拼 pipeline
缺点：主要是"线性/DAG"抽象，循环/条件/暂停恢复弱
适合：RAG、一问一答、简单多步
```

### ③ LangGraph

```
优点：原生支持循环、条件、并行、HITL、checkpoint、stream
缺点：心智模型复杂一点（就是我们要学的）
适合：多步 Agent、带人工审核、长任务、多 Agent 协作
```

**对标 .NET 生态**：

| 概念 | Python / LangGraph | .NET 对应 |
|------|-------------------|----------|
| 裸 LLM 调用 | `openai.chat.completions.create` | `IChatClient.CompleteAsync` |
| 轻量 pipeline | LangChain LCEL | Dawning `ISkillPipeline`（规划） |
| 带状态的图编排 | LangGraph | Dawning `IWorkflow`（规划） |
| 通用工作流引擎 | Temporal | Temporal.NET / Dapr Workflow |

LangGraph 的定位：**比 LCEL 重，比 Temporal 轻，专攻 LLM 场景**。

---

## 4. 它**不**解决什么

经常被误用的地方：

| 想用 LangGraph 做 | 建议 |
|------------------|------|
| 通用后端工作流（ETL / 审批流 / 定时任务） | 用 Temporal / Airflow，不是 LangGraph |
| 简单 RAG（一次检索一次生成） | 直接 LangChain 或手写，别上 LangGraph |
| 要求严格 SLA（如金融交易超时重试） | Temporal 更合适，LangGraph 没这些原语 |
| 极端高并发（百万 QPS） | LangGraph 不是为此设计的；放轻量模型 + 网关 |
| 复杂业务规则引擎 | 用规则引擎（Drools 等） |

**LangGraph 的甜蜜点**：

- 调用 2-20 次 LLM
- 有分支 / 循环 / 并行
- 需要持久化 / 时间旅行 / HITL
- 对话长、任务跨小时/天

---

## 5. 凭什么是"图"

为什么不是"脚本"、"状态机"、"流水线"？

因为 LLM 驱动的流程有这些特征：

- **有循环**（不停反思、修正，直到达标）→ 图可以有环，DAG 不行
- **有条件分支**（LLM 决定下一步） → 图原生支持
- **有 fan-out / fan-in**（派多个 worker 并行） → 图天然能画
- **状态要共享**（不同节点读写同一批字段） → 图 + shared state 很合适
- **要可视化/审计**（让人看到它怎么想的） → 图结构直接可渲染

状态机可以做第 3 条但不易做 fan-out；脚本能做但没结构。图是**最小能覆盖这些需求的抽象**。

---

## 6. LangGraph 长什么样（预览）

完整版在下一篇 [[02-hello-world]]，这里先混个眼熟：

```python
from langgraph.graph import StateGraph, START, END
from typing import TypedDict

class State(TypedDict):
    question: str
    answer: str

def think(state):
    return {"answer": f"回答：{state['question']}"}

graph = StateGraph(State)
graph.add_node("think", think)
graph.add_edge(START, "think")
graph.add_edge("think", END)
app = graph.compile()

print(app.invoke({"question": "你好"}))
# {'question': '你好', 'answer': '回答：你好'}
```

7 行逻辑代码画出了一张图：`START → think → END`。

---

## 7. 本篇结论（记住这几条）

1. **LangGraph 不是 LLM 包装器**，是**带状态的图运行时**
2. 它的核心价值是**循环 / 条件 / 并行 / 持久化 / HITL / 流式**一揽子解决
3. 它有**甜蜜点**，不是什么都能做
4. 写代码前先**画图**（纸上都行），符合图思维就用它

---

## 8. 下一步

- 下一篇 → [[02-hello-world]] 15 行代码跑起来
- 跳过细节直接看源码？→ [[../tier-3-internals/02-state-graph]]
- 跟其他框架对比？→ [[../../../comparisons/maf-vs-langgraph]]
