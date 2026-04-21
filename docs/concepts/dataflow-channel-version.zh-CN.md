---
type: concept
title: Dataflow 编排基础：Channel、版本号、激活判定
tags: [concept, dataflow, pregel, bsp, channels, version, langgraph]
related-frameworks: [langgraph, temporal, flink]
created: 2026-04-21
updated: 2026-04-21
status: active
---

# Dataflow 编排基础：Channel、版本号、激活判定

> 阅读前置：无。本文是为看 LangGraph / Pregel / Flink 等**数据流驱动**运行时的入门铺垫。
> 目标：读完后再看 [[../frameworks/langgraph/01-architecture#运行时]] 或 [[../frameworks/langgraph/03-pregel-runtime]] 不再卡在术语上。

---

## 0. 本文解决的问题

很多 Agent / workflow 框架的运行时文档都会说：

> "每一轮先从 channel 版本选出激活节点..."

如果你没有 Pregel / dataflow 背景，这句话每个字都认识但连起来看不懂。
本文把它拆成 5 个最小前置概念，一步步搭起来。

---

## 1. 图（Graph）= 节点 + 边

最基础。一张有向图由：

- **节点（Node）**：做事的单元（一个函数、一个 Agent、一个 LLM 调用）
- **边（Edge）**：节点 X → 节点 Y，表示"X 之后可能轮到 Y"

```
START → plan → act → END
         ↑      │
         └──────┘   (act 可回到 plan)
```

**为什么不直接"按边顺序跑就行"？**

因为现代编排引擎要支持：
- **并行**：多节点同时跑
- **条件分支**：跑到哪由数据决定，不是写死的
- **fan-out / fan-in**：一变多、多合一
- **可中断可恢复**：随时停、随时续

顺序执行模型不够用，必须换一种思路 → 数据流驱动。

---

## 2. State（状态）= 所有节点共享的数据包

```python
class State(TypedDict):
    messages: list
    step: int
    result: str
```

- 所有节点读同一个 state
- 节点写入时，返回要更新的字段

```python
def plan(state: State) -> dict:
    return {"step": state["step"] + 1}
```

**state 不是一个大对象从头传到尾**，而是一堆"字段"拼出来的。下一节讲每个字段其实是什么。

---

## 3. Channel = state 的一个字段 + 合并规则

这是**核心概念**。state 里每个字段在运行时实际上是一个 **Channel（通道）**。

```
class State(TypedDict):
    messages: list          ← 编译成 channel "messages"
    step: int               ← 编译成 channel "step"
```

每个 channel 内部：

```
Channel "messages":
  value    = [...]          # 当前值
  reducer  = add_messages   # 合并规则（新写入怎么合到旧值）
  version  = 3              # 版本号（下一节细讲）
```

**reducer 有几种**：

| reducer | 语义 |
|---------|------|
| `last_value` | 新值覆盖旧值（默认） |
| `add` / `append` | 新值追加 |
| `max` / `merge_dict` | 按业务语义合并 |

> "channel" 在不同框架叫法不同：LangGraph 叫 Channel，Flink 叫 Stream，Akka 叫 Mailbox。本质都是"带合并规则的字段"。

---

## 4. 版本号 = 一个单调递增的整数

每个 channel 内部藏了一个版本号：

```
初始:            messages.version = 0
A 写了一次:      messages.version = 1
B 写了一次:      messages.version = 2
A 又写了一次:    messages.version = 3
```

规则：
- 起始 = 0
- **每次被写一次 → +1**
- 只增不减

**版本号解决什么问题**？

> "这份数据从上次我看过到现在，**有没有变过**？"

光看 value 不可靠（两次写的内容可能相同）。版本号是最简单、最可靠的"变化标记"。

---

## 5. 订阅 & 记忆：节点关心哪些 channel

从图结构编译出每个节点的订阅关系：

```
graph.add_edge("plan", "act")
graph.add_edge("act", "verify")
```

推断出：

```
节点 act    订阅 messages          (因为 plan → act 会写 messages)
节点 verify 订阅 result            (因为 act → verify 会写 result)
```

每个节点还记着**"我上次看到的版本"**：

```
节点 act 的记忆:
  seen_messages_version = 2
```

就像一个员工记着"我上次看这份文档是 v3"。

---

## 6. 把 5 点拼起来：激活判定

现在能回答**"这一轮该跑谁"**：

> **对每个节点：我订阅的 channel 的当前版本 > 我上次见过的版本？**
> - 是 → 激活 ✅（本轮跑它）
> - 否 → 休眠 ❌（跳过）

### 例子

```
╔══════════════════════════════════════════╗
║  第 3 轮开始                              ║
║                                          ║
║  Channel 当前版本:                        ║
║    messages.version = 4                  ║
║    result.version   = 0                  ║
║                                          ║
║  各节点记忆:                              ║
║    act:    seen_messages = 3             ║
║    verify: seen_result   = 0             ║
╚══════════════════════════════════════════╝

  act:    4 > 3 ? 是 → 激活 ✅
  verify: 0 > 0 ? 否 → 跳过 ❌

→ 本轮只跑 act
→ act 跑完写 result → result.version 升到 1
→ 下一轮 verify 就会被激活
```

---

## 7. 一轮（superstep）的完整流程

```
┌──────────────────────────────────────────┐
│ 一个 superstep                            │
│                                          │
│ ① 选激活节点                               │
│    扫描所有 channel 版本号                 │
│    找出"订阅版本 > 记忆版本"的节点         │
│                                          │
│ ② 并行执行                                │
│    所有激活节点读同一个 state 快照          │
│    并发跑，写入先缓冲，不立即修改 channel   │
│                                          │
│ ③ Barrier（同步屏障）                      │
│    等所有节点 return                       │
│                                          │
│ ④ 合并 + 版本号 +1                         │
│    按 reducer 合并所有写入                 │
│    被写的 channel 版本号 +1               │
│    各节点更新自己的"记忆版本"              │
│                                          │
│ ⑤ 持久化（可选）                           │
│    落 checkpoint                          │
│                                          │
│ 如果还有激活节点候选 → 下一轮              │
│ 否则 → 图收敛，结束                        │
└──────────────────────────────────────────┘
```

这就是 [[#BSP]] 模型的核心。

---

## 8. 类比：Google Docs 协作

想象一份多人协作文档：

| 文档类比 | 编排引擎 |
|---------|---------|
| 文档的每个**章节** | channel |
| "章节最后修改时间" | channel 版本号 |
| **审阅人**订阅某些章节 | 节点订阅 channel |
| "我上次看到时是 XX 时间" | 节点的记忆版本 |
| 系统：章节改了才叫你 | 激活判定 |

每轮系统扫一遍："章节改过吗？改了通知对应审阅人。"

---

## 9. 为什么这么设计

对比**按边顺序跑**的朴素模型，数据流驱动带来：

| 能力 | 为什么靠版本号能做到 |
|------|-------------------|
| 并行 | A/B 都订阅 messages → 同轮激活 |
| 条件跳转 | 没写结果 channel → 下游不被激活 |
| fan-out | 一个节点写多个 channel → 多个下游同轮激活 |
| 可中断恢复 | checkpoint 保存所有版本号 → 恢复时接着比较 |
| 收敛检测 | 没节点被激活 → 图自然停 |

---

## 10. BSP（Bulk Synchronous Parallel）

上面第 7 节的流程模型，正式名字叫 **BSP**：

- **Bulk**：批量（同一轮多节点一起跑）
- **Synchronous**：同步（有 barrier）
- **Parallel**：并行（同轮节点彼此不等）

来源：Google Pregel 论文（2010），用于大规模图计算。
被 LangGraph 借来做 Agent 编排。

---

## 11. 映射到具体框架

| 概念 | LangGraph | Flink | Pregel | Akka |
|------|-----------|-------|--------|------|
| 节点 | Node | Operator | Vertex | Actor |
| 字段 + reducer | Channel | State (keyed) | Vertex value | Actor state |
| 版本号 | `channel_versions` | checkpoint barrier | superstep 号 | message seq |
| 激活判定 | `prepare_next_tasks` | watermark 推进 | vote to halt | 邮箱非空 |
| 持久化点 | Checkpoint | Savepoint | superstep 结束 | 持久化 Actor |

---

## 12. 延伸阅读

- Pregel 论文：<https://research.google/pubs/pregel-a-system-for-large-scale-graph-processing/>
- LangGraph 运行时源码解剖：[[../frameworks/langgraph/03-pregel-runtime]]
- Channel 家族拆解：[[../frameworks/langgraph/04-channels]]
- Checkpoint 分层：[[../frameworks/langgraph/05-checkpointer]]
- 状态持久化综述：[[state-persistence]]

---

## 13. 读完以后你应该能看懂这句话

> "每 superstep 先从 channels 版本选出激活节点（类 Pregel 超步语义）；节点并行跑；跑完同步屏障，一起写 channels → 落 checkpoint。"

如果还不行，回到第 3-6 节再过一遍。
