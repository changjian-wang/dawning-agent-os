---
framework: langgraph
version: v1.1.x
type: synthesis
module: streaming
repo-path: libs/langgraph/langgraph/pregel/{io,messages,debug}.py
tags: [langgraph, streaming, sse, websocket, stream-mode]
created: 2026-04-18
updated: 2026-04-18
status: active
subtype: internals
title: LangGraph — 07 Streaming：5 种 stream_mode 的源码解剖
sources: []
---

# LangGraph — 07 Streaming：5 种 stream_mode 的源码解剖

> 本文回答：`stream_mode="updates"` 和 `"messages"` 内部到底有什么区别？
> 怎么同时输出多种 mode？为什么 `astream_events` 会被推荐用作前端协议？


## 目录 <!-- TOC-AUTOGEN -->

- [1. 范围](#1-范围)
- [2. 一句话回答](#2-一句话回答)
- [3. 5 种 stream_mode 一览](#3-5-种-streammode-一览)
- [4. 多 mode 同时输出](#4-多-mode-同时输出)
- [5. 事件 schema 详解](#5-事件-schema-详解)
- [6. `astream_events` —— 推荐的前端协议](#6-astreamevents--推荐的前端协议)
- [7. 事件流的内部机制](#7-事件流的内部机制)
- [8. 子图 stream](#8-子图-stream)
- [9. 取消与背压](#9-取消与背压)
- [10. messages mode 与 LangSmith metadata](#10-messages-mode-与-langsmith-metadata)
- [11. 性能与陷阱](#11-性能与陷阱)
- [12. 错误清单](#12-错误清单)
- [13. 与 Dawning 的对应](#13-与-dawning-的对应)
- [14. 阅读顺序](#14-阅读顺序)
- [15. 延伸阅读](#15-延伸阅读)
<!-- /TOC-AUTOGEN -->

> 重点路径：`pregel/__init__.py` 的 `stream` / `astream`、`pregel/messages.py`、`pregel/debug.py`、`pregel/io.py`。

---

## 1. 范围

| 在范围 | 不在范围 |
|--------|---------|
| 5 种 `stream_mode` 的语义和事件 schema | LLM provider 的 token streaming → LangChain Core |
| `astream_events` v2 协议 | 部署/SSE/WebSocket → [[10-platform-integration]] |
| `get_stream_writer()` 自定义事件 | 节点函数细节 → [[02-state-graph]] |

---

## 2. 一句话回答

> Stream 是 Pregel runner 在不同 hook 点把事件推到一个 **`StreamProtocol` 队列**，
> 调用方迭代这个队列。
> 不同 `stream_mode` 对应不同的过滤器和事件 shape；多 mode 时事件以 `(mode, payload)` 元组发。

---

## 3. 5 种 stream_mode 一览

<!-- Stream Mode 对照 -->
````mermaid
flowchart TB
    Pregel[Pregel.stream] --> Runner[PregelRunner]

    Runner --> Hook1[节点开始]
    Runner --> Hook2[节点结束]
    Runner --> Hook3[LLM token]
    Runner --> Hook4[get_stream_writer 写]
    Runner --> Hook5[apply_writes 完成]

    Hook1 --> Mu[updates / debug.task]
    Hook2 --> Mu2[updates / debug.task_result]
    Hook3 --> Mm[messages]
    Hook4 --> Mc[custom]
    Hook5 --> Mv[values / debug.checkpoint]

    Mu & Mu2 & Mm & Mc & Mv --> Q[(StreamProtocol Queue)]
    Q --> Iter[Caller iter]

    classDef hook fill:#fff4e6,stroke:#f08c00
    classDef mode fill:#e7f5ff,stroke:#1971c2,color:#0b3d91
    class Hook1,Hook2,Hook3,Hook4,Hook5 hook
    class Mu,Mu2,Mm,Mc,Mv mode
```
> 源文件：[`diagrams/stream-modes.mmd`](../diagrams/stream-modes.mmd)

| Mode | 粒度 | 触发点 | 典型用途 |
|------|------|-------|---------|
| `values` | 整个 state | 每个 superstep 结束 | 简单 Notebook / API / 兼容老代码 |
| `updates` | 单节点输出 | 每个 node 完成 | 进度条 / 节点级 UI |
| `messages` | LLM 单 token | LLM provider 每发一个 chunk | Chat UI 流式渲染 |
| `custom` | 任意 | 节点内 `get_stream_writer().write(...)` | 业务事件（进度百分比 / 工具步骤） |
| `debug` | 内部细节 | task 选择、apply_writes 全程 | 框架调试 / 教学 |

---

## 4. 多 mode 同时输出

```python
for mode, payload in graph.stream(input, config,
                                   stream_mode=["updates", "messages"]):
    if mode == "updates":
        ...
    elif mode == "messages":
        token, meta = payload
        ...
```

- 单 mode 时 yield `payload`（不带 mode 元组）
- 多 mode 时 yield `(mode, payload)`
- async 同理：`astream(...)`

---

## 5. 事件 schema 详解

### 5.1 `values`

```python
{"counter": 3, "messages": [HumanMessage("hi"), AIMessage("hello")]}
```

直接 yield 当前 state 完整副本。

### 5.2 `updates`

```python
{"node_name": {"counter": 1, "messages": [AIMessage("hello")]}}
```

key 是节点名，value 是该节点本超步**写出的 dict**（不是合并后的 state）。

### 5.3 `messages`

```python
(AIMessageChunk(content="hel"), {"langgraph_node": "agent", "langgraph_step": 2, "langgraph_triggers": ["start"], "checkpoint_ns": "...", "ls_*": ...})
```

二元组 `(chunk, metadata)`，metadata 含丰富的上下文。**这是 Chat UI 最常用的 mode**。

### 5.4 `custom`

```python
"任意 JSON 可序列化值"
```

```python
def my_node(state):
    writer = get_stream_writer()
    writer({"progress": 0.5, "step": "downloading"})
    ...
```

### 5.5 `debug`

```python
{"type": "task" | "task_result" | "checkpoint", "step": N, "payload": {...}}
```

包含 task 选择、writes 应用、checkpoint 写入等内部事件。

---

## 6. `astream_events` —— 推荐的前端协议

```python
async for event in graph.astream_events(input, config, version="v2"):
    if event["event"] == "on_chain_start":
        ...
    elif event["event"] == "on_chat_model_stream":
        chunk = event["data"]["chunk"]
        ...
    elif event["event"] == "on_tool_end":
        ...
```

事件类型（v2）：

| event | 含义 |
|-------|------|
| `on_chain_start` / `on_chain_end` | 节点 / runnable 开始结束 |
| `on_chat_model_start` / `on_chat_model_stream` / `on_chat_model_end` | LLM 调用 |
| `on_tool_start` / `on_tool_end` | Tool 调用 |
| `on_retriever_start` / `on_retriever_end` | RAG 检索 |
| `on_chain_stream` | 节点流式输出 |
| `on_custom_event` | `dispatch_custom_event(...)` 触发 |

> **优势**：协议稳定，前端不依赖 graph 拓扑；适合作为 SSE/WebSocket payload。
> **代价**：事件量大，单 invoke 几十~几百个事件。

---

## 7. 事件流的内部机制

<!-- Stream 内部机制 -->
````mermaid
sequenceDiagram
    autonumber
    participant U as Caller
    participant P as Pregel.stream
    participant R as PregelRunner
    participant N as Node
    participant LLM as LLM Callback
    participant W as get_stream_writer
    participant Q as StreamProtocol Queue

    U->>P: stream(input, mode=[updates, messages])
    P->>R: tick
    R->>N: run node X
    N->>LLM: llm.invoke (streaming)
    LLM-->>Q: chunk1 -> push messages
    Q-->>U: yield (messages, chunk1)
    LLM-->>Q: chunk2 -> push messages
    Q-->>U: yield (messages, chunk2)
    N->>W: writer({"progress":0.5})
    W-->>Q: push custom
    Q-->>U: yield (custom, ...)
    N-->>R: return dict
    R-->>Q: push updates {node:dict}
    Q-->>U: yield (updates, ...)
    R->>R: apply_writes
    R-->>Q: push values (full state) — 仅当 values mode 开启
```
> 源文件：[`diagrams/stream-internals.mmd`](../diagrams/stream-internals.mmd)

- `Pregel.stream()` 创建 `StreamProtocol`（基于 deque + condition variable）
- `PregelRunner` 在以下 hook 推事件：
  - 节点开始 → `updates` (当前不推 dict，等节点结束推) / `debug.task`
  - 节点结束 → `updates` (推节点 dict) / `debug.task_result`
  - LLM provider 回调（通过 LangChain callback handler）→ `messages`
  - 节点内 `writer(...)` → `custom`
  - apply_writes 后 → `values` / `debug.checkpoint`

---

## 8. 子图 stream

子图的事件**默认不冒泡**到父图的 stream。需要显式开：

```python
for chunk in graph.stream(input, config, subgraphs=True):
    # chunk 形如 (("subgraph_ns",), payload) 或 (("subgraph_ns", "deeper"), payload)
    ns, payload = chunk
```

- `ns=()` 表示父图本身
- `ns=("section_42",)` 表示一级子图
- `ns=("section_42", "search")` 表示嵌套两层

> 多 mode + subgraphs 的事件 shape：`(ns, mode, payload)`。

---

## 9. 取消与背压

| 项 | 说明 |
|-----|------|
| 客户端断开 | async 模式下 generator 被 GC → `CancelledError` 传播到 runner → 当前 task 取消 |
| 客户端慢 | runner 仍按 superstep 节奏推；队列基于 deque 无界，**慢消费可能内存爆** |
| 解决慢消费 | 用有界队列（v1.x 在测试中）/ 客户端定期 ack |

---

## 10. messages mode 与 LangSmith metadata

每个 message chunk 的 metadata 字段：

| 字段 | 含义 |
|------|------|
| `langgraph_node` | 当前节点名 |
| `langgraph_step` | 第几个 superstep |
| `langgraph_triggers` | 触发当前节点的 channels |
| `langgraph_path` | 子图路径 |
| `checkpoint_ns` | 子图命名空间 |
| `ls_provider` | LLM provider |
| `ls_model_name` | 模型名 |
| `ls_temperature` | 温度等参数 |
| `tags` | 用户自定义 tags |

**前端用途**：按 node 分通道渲染 / 标记不同 Agent 的 token。

---

## 11. 性能与陷阱

| 项 | 说明 |
|---|------|
| `astream_events` 量大 | 万级事件/分钟；前端用 throttle / 按 event type 过滤 |
| `messages` mode 必须用支持流式的 LLM | 不支持时降级为最后一次 chunk |
| `values` mode 重复发整个 state | 大 state 时带宽爆炸；改 `updates` |
| Custom event 序列化失败 | 框架直接抛错；务必用 JSON 可序列化对象 |
| Sync stream 阻塞 | 当节点是 IO-bound 时换 async（`astream`） |

---

## 12. 错误清单

| 错误 | 触发 | 解法 |
|------|-----|------|
| `RuntimeError("get_stream_writer outside of node")` | 节点外调用 writer | 只在 node fn 内调用 |
| messages mode 没事件 | LLM 没开 streaming | provider 加 `streaming=True` 或换 chat client |
| async stream 卡住 | 节点是 sync 阻塞 | 用 `ainvoke` / 异步 LLM client |
| 多 mode 顺序乱 | 节点回调顺序受 LLM provider 影响 | 用 metadata 排序，不要依赖 yield 顺序 |

---

## 13. 与 Dawning 的对应

| LangGraph | Dawning |
|-----------|---------|
| `stream_mode="updates"` | `IAgentEventStream.NodeUpdates` |
| `stream_mode="messages"` | `IAgentEventStream.LlmTokens` |
| `stream_mode="custom"` | `IAgentEventStream.Custom` |
| `astream_events v2` | `Dawning.Streaming.EventEnvelope`（统一协议） |
| `subgraphs=True` | Dawning 默认开启子图事件 |
| Stream metadata | `EventEnvelope.Metadata` |
| Cancel 传播 | `CancellationToken` 全程贯穿 |

---

## 14. 阅读顺序

- 已读 → [[03-pregel-runtime#9]] 知道 stream 在 runner 哪些位置触发
- 已读 → [[06-interrupt-hitl]] 知道 interrupt 也是 stream event
- 下一步 → [[10-platform-integration]] 看 stream 在 SSE/WebSocket 上的协议
- 案例 → [[cases/open-deep-research#streaming]] / [[cases/linkedin-hr-agent#streaming]]

---

## 15. 延伸阅读

- 官方 Streaming：<https://langchain-ai.github.io/langgraph/concepts/streaming/>
- `astream_events v2` 设计：<https://blog.langchain.com/astream-events-v2/>
- 源码：`libs/langgraph/langgraph/pregel/{messages,debug,io}.py`
