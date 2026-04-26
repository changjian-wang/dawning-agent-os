---
framework: langgraph
version: v1.1.x
type: synthesis
module: channels
repo-path: libs/langgraph/langgraph/channels/
tags: [langgraph, channels, reducer, concurrency]
created: 2026-04-18
updated: 2026-04-18
status: active
subtype: internals
title: LangGraph — 04 Channels：Reducer 与并发安全的另一半
sources: []
---

# LangGraph — 04 Channels：Reducer 与并发安全的另一半

> 本文回答：State 字段为什么"会自己合并"？reducer 的合约是什么？什么时候必须显式标注？


## 目录 <!-- TOC-AUTOGEN -->

- [1. 范围](#1-范围)
- [2. 一句话回答](#2-一句话回答)
- [3. 类层次](#3-类层次)
- [4. `BaseChannel` 接口](#4-basechannel-接口)
- [5. 内置 Channel 全集](#5-内置-channel-全集)
- [6. 完整对照表](#6-完整对照表)
- [7. `add_messages` 深入](#7-addmessages-深入)
- [8. 自定义 Reducer 模板](#8-自定义-reducer-模板)
- [9. 版本号管理（与 Pregel 协作）](#9-版本号管理与-pregel-协作)
- [10. 与 Pydantic / dataclass 的互操作](#10-与-pydantic--dataclass-的互操作)
- [11. 性能与陷阱](#11-性能与陷阱)
- [12. 错误清单](#12-错误清单)
- [13. 与 Dawning 的对应](#13-与-dawning-的对应)
- [14. 阅读顺序](#14-阅读顺序)
- [15. 延伸阅读](#15-延伸阅读)
<!-- /TOC-AUTOGEN -->

> 重点路径：`langgraph/channels/{base,last_value,binop,topic,any_value,ephemeral_value,untracked_value}.py`。

---

## 1. 范围

| 在范围 | 不在范围 |
|--------|---------|
| `BaseChannel` 抽象与所有内置 channel | 节点写如何被调度 → [[03-pregel-runtime#5]] |
| Reducer 的合约（pure / 类型安全） | 序列化细节 → [[05-checkpointer#serde]] |
| 版本号语义 | StateGraph 的 schema → channel 翻译 → [[02-state-graph#5]] |

---

## 2. 一句话回答

> Channel 是"**带版本号、带合并语义的小型有状态盒子**"。
> 节点不直接读写 State，而是读写 Channel：
> - 读 = 拿到 Channel 当前值
> - 写 = 提交一个 candidate value
> - barrier 后 Pregel 调用 `channel.update([candidates])` 走 reducer 合并
> - 合并完版本号 +1，谁的"看见版本号"低于这个就会被唤醒。

---

## 3. 类层次

<!-- Channels 类层次 -->
````mermaid
classDiagram
    class BaseChannel~Value, Update, C~ {
        <<abstract>>
        +ValueType: type
        +UpdateType: type
        +update(values) bool
        +get() Value
        +checkpoint() C
        +from_checkpoint(c) Self
        +is_available() bool
        +consume() bool
    }

    class LastValue~Value~ {
        +value: Value
        +update(values) 单写覆盖
    }
    class BinaryOperatorAggregate~Value~ {
        +operator: Callable
        +update(values) reducer 折叠
    }
    class Topic~Value~ {
        +accumulate: bool
        +values: list
    }
    class AnyValue~Value~ {
        +update(values) 取一
    }
    class EphemeralValue~Value~ {
        +checkpoint() None
        +consume() 读完即清
    }
    class UntrackedValue~Value~ {
        +不 bump 版本号
    }

    BaseChannel <|-- LastValue
    BaseChannel <|-- BinaryOperatorAggregate
    BaseChannel <|-- Topic
    BaseChannel <|-- AnyValue
    BaseChannel <|-- EphemeralValue
    BaseChannel <|-- UntrackedValue
```
> 源文件：[`diagrams/channels-class.mmd`](../diagrams/channels-class.mmd)

---

## 4. `BaseChannel` 接口

```python
class BaseChannel(Generic[Value, Update, C]):
    @property
    def ValueType(self) -> type[Value]:        # 对外暴露的"读"类型
    @property
    def UpdateType(self) -> type[Update]:      # 节点写入的"单次 update" 类型

    def checkpoint(self) -> C | None:          # 序列化（持久化用），None=不入 checkpoint
    def from_checkpoint(self, c: C | None) -> Self:  # 反序列化

    def update(self, values: Sequence[Update]) -> bool:  # ← reducer 的入口
    def get(self) -> Value:                    # 读当前值，未初始化抛 EmptyChannelError
    def is_available(self) -> bool             # 是否能被读
    def consume(self) -> bool                  # 部分 channel 读完即清（Ephemeral）
```

**关键合约**：

| 合约 | 谁保证 | 违反后果 |
|------|--------|---------|
| `update` 只能由 Pregel barrier 单线程调用 | runner | 否则 reducer 并发不可重现 |
| `update` 返回 `True` 表示值变了（要 bump 版本） | channel 实现 | 否则节点不会被重新唤醒 |
| reducer 必须 pure | 用户 | replay/重启时结果不一致 |
| `checkpoint()` 必须可序列化 | 用户 | 不能跨进程恢复 |
| `from_checkpoint(checkpoint())` 等价 | 用户 | replay 失真 |

---

## 5. 内置 Channel 全集

### 5.1 `LastValue` —— 默认

```python
class LastValue(Generic[Value], BaseChannel[Value, Value, Value]):
    def update(self, values: Sequence[Value]) -> bool:
        if len(values) == 0:
            return False
        if len(values) > 1:
            raise InvalidUpdateError("LastValue can only receive one update at a time")
        self.value = values[-1]
        return True
```

| 项 | 行为 |
|---|------|
| 语义 | "后写覆盖前写"，且**同超步内只允许一个写者** |
| 编译触发 | State 字段无 `Annotated` reducer |
| 典型用途 | 普通字段：`step: int`、`current_user: User` |
| 踩坑 | 同超步两个节点都写同字段 → `InvalidUpdateError` |

### 5.2 `BinaryOperatorAggregate` —— Reducer 主力

```python
class BinaryOperatorAggregate(BaseChannel[Value, Value, Value]):
    def __init__(self, typ: type[Value], operator: Callable[[Value, Value], Value]):
        self.operator = operator

    def update(self, values: Sequence[Value]) -> bool:
        if not values:
            return False
        if not hasattr(self, "value") or self.value is MISSING:
            self.value = values[0]
            values = values[1:]
        for v in values:
            self.value = self.operator(self.value, v)
        return True
```

| 项 | 行为 |
|---|------|
| 语义 | 用二元 reducer 把 candidates 折叠成单值 |
| 编译触发 | `Annotated[T, reducer]` 写法 |
| 典型 reducer | `operator.add`（list 追加）、`add_messages`（消息合并）、自定义 |
| 关键约束 | reducer **必须满足结合律** —— 否则并发顺序影响结果 |

### 5.3 `Topic` —— pub-sub / fan-out

```python
class Topic(BaseChannel[Sequence[Value], Update, ...]):
    def __init__(self, typ, accumulate: bool = False):
        self.accumulate = accumulate

    def update(self, values: Sequence) -> bool:
        if self.accumulate:
            self.values = self.values + list(values)
        else:
            self.values = list(values)
        return True
```

| 项 | 行为 |
|---|------|
| 语义 | 多写者 / 多读者；`accumulate=True` 时跨超步累积 |
| 典型用途 | Send 目标的接收端；多生产者事件流 |
| 与 `BinaryOperatorAggregate` 区别 | 直接保留列表，不做 reducer 折叠 |

### 5.4 `AnyValue` —— 多源合并取一

| 项 | 行为 |
|---|------|
| 语义 | 多个写者都写，但读时只暴露任一（最后一个） |
| 典型用途 | 内部 channel：多个 trigger 表示"任一到达即可" |
| 用户极少直接用 | 主要服务于 `add_edge(["a","b"], "c")` 的 join 实现 |

### 5.5 `EphemeralValue` —— 单超步可见

```python
class EphemeralValue(BaseChannel[Value, Value, Value]):
    def checkpoint(self) -> Value | None:
        return None       # ← 不入 checkpoint
    def consume(self) -> bool:
        # 读完即清
        if hasattr(self, "value"):
            del self.value
            return True
        return False
```

| 项 | 行为 |
|---|------|
| 语义 | 当超步可见，下一超步消失，**不持久化** |
| 典型用途 | START channel（输入只用一次）、节点间一次性传递 |
| 性能价值 | 大对象（如搜索结果）走这里 → checkpoint 不膨胀 |

### 5.6 `UntrackedValue` —— 不参与版本管理

| 项 | 行为 |
|---|------|
| 语义 | 写入不 bump 版本号，读取永远拿最新 |
| 典型用途 | 配置类常量、写 cache 但不想触发节点重跑 |
| 极少用 | 多用于框架内部 |

---

## 6. 完整对照表

| Channel | 持久化 | 写者 | 触发下游 | 典型 schema 写法 |
|---------|-------|------|---------|----------------|
| `LastValue` | ✅ | 单 | ✅ | `x: int` |
| `BinaryOperatorAggregate` | ✅ | 多（折叠） | ✅ | `Annotated[list, operator.add]` |
| `Topic` | ✅ | 多（保留） | ✅ | 通常框架内部用 |
| `AnyValue` | ✅ | 多（取一） | ✅ | join 边内部 |
| `EphemeralValue` | ❌ | 单 | ✅ | START channel |
| `UntrackedValue` | ❌ | 多 | ❌ | 高频读 cache |

---

## 7. `add_messages` 深入

LangGraph 最常用的 reducer，几乎所有 chat 类 Agent 都靠它。

```python
def add_messages(left: list, right: list | dict) -> list:
    # 支持 right=dict 形式：{"role": "user", "content": "..."}
    if isinstance(right, dict):
        right = [right]
    coerced = convert_to_messages(right)

    # 关键：按 id 去重 + 按 tool_call_id 配对
    merged = list(left)
    for msg in coerced:
        idx = _find_by_id(merged, msg.id)
        if idx is None:
            merged.append(msg)
        else:
            merged[idx] = msg            # 同 id 替换
    return merged
```

**为什么不能用 `operator.add`**：
1. 没有 dedupe → message 重复
2. 没有 dict → BaseMessage 转换 → 类型混乱
3. 没有 tool_call_id 配对 → tool 消息错位

→ message 列表**必须**用 `add_messages`。

---

## 8. 自定义 Reducer 模板

```python
def merge_dicts(left: dict, right: dict) -> dict:
    """深合并两个 dict，不修改入参"""
    result = dict(left)
    for k, v in right.items():
        if k in result and isinstance(result[k], dict) and isinstance(v, dict):
            result[k] = merge_dicts(result[k], v)
        else:
            result[k] = v
    return result

class State(TypedDict):
    config: Annotated[dict, merge_dicts]
```

**Reducer 自检清单**：

- [ ] **结合律**：`f(f(a,b),c) == f(a,f(b,c))`
- [ ] **不修改入参**：返回新对象（dict copy / list 重建）
- [ ] **类型稳定**：`f(T, T) -> T`
- [ ] **可序列化**：返回值能被 msgpack 处理
- [ ] **幂等友好**：`f(a, a) == a` 时更安全（重试不出问题）

---

## 9. 版本号管理（与 Pregel 协作）

<!-- Channel 版本号与节点唤醒 -->
````mermaid
sequenceDiagram
    autonumber
    participant N1 as Node A
    participant N2 as Node B
    participant L as PregelLoop
    participant CH as Channel(messages)
    participant CK as Checkpoint

    Note over CK: channel_versions={messages:5}<br/>versions_seen={A:{messages:5}, B:{messages:5}}

    L->>L: prepare_next_tasks
    Note over L: A 看见 messages=5，等于 seen，不激活<br/>B 同理 → 都不跑 → END？
    Note over CK: 假设外部 update_state 注入新 message → channel_versions={messages:6}

    L->>L: prepare_next_tasks
    Note over L: A.seen[messages]=5 < channel_versions=6 → 激活 A
    L->>N1: run A
    N1-->>L: writes messages=[m1]

    L->>CH: apply_writes([m1])
    CH->>CH: reducer 合并
    CH-->>L: changed=true
    L->>CK: channel_versions[messages]=7
    L->>CK: versions_seen[A][messages]=7

    Note over L: 下超步：A 自己 seen=7=current 不激活<br/>B.seen=5 < 7 激活
    L->>N2: run B
    N2-->>L: writes messages=[m2]
    L->>CH: apply_writes([m2])
    L->>CK: channel_versions[messages]=8, versions_seen[B][messages]=8
```
> 源文件：[`diagrams/channels-versions.mmd`](../diagrams/channels-versions.mmd)

**两个版本号字典**（存于 checkpoint）：

```python
checkpoint = {
    "channel_versions": {"messages": 5, "step": 3, ...},        # 当前最新版本
    "versions_seen": {
        "agent": {"messages": 4, "step": 3, ...},               # agent 节点上次见的
        "tools": {"messages": 5, "step": 3, ...},
    }
}
```

- 节点 X 的触发条件：`channel_versions[t] > versions_seen[X][t]` 对**任一** trigger 成立
- `apply_writes` 完毕：被改的 channel `channel_versions[c] += 1`，但**不更新** seen
- 节点跑完：`versions_seen[X][t] = channel_versions[t]` for each trigger
- 这就是为什么"我自己写了字段，自己不会被唤醒" —— seen 跟着推进

`get_next_version` 默认是 `int + 1`，可注入：

```python
def vector_clock_next(prev, channel):
    return f"{node_name}-{int(prev.split('-')[1]) + 1}"

graph.compile(checkpointer=PostgresSaver(...),
              get_next_version=vector_clock_next)
```

---

## 10. 与 Pydantic / dataclass 的互操作

```python
from pydantic import BaseModel, Field
from typing import Annotated
from operator import add

class State(BaseModel):
    counter: Annotated[int, add] = 0
    items: Annotated[list[str], add] = Field(default_factory=list)
```

**注意点**：

| 项 | 行为 |
|---|------|
| Pydantic v2 校验 | 写入时自动校验（可能抛 `ValidationError`） |
| 默认值 | 必须提供（不然首次 read 时抛 `EmptyChannelError`） |
| `model_config = ConfigDict(arbitrary_types_allowed=True)` | 用自定义类型时需要 |
| `Annotated[T, reducer]` 写法 | 与 TypedDict 完全一致 |

> Dawning 对应建议：`IWorkingMemory` schema 直接用 .NET record + `[Reducer<T>]` attribute。

---

## 11. 性能与陷阱

| 项 | 说明 |
|---|------|
| Reducer 每次都被调用 | 高频字段（每超步都改）的 reducer 是热路径，避免 O(n²) 操作 |
| `add_messages` 的 dedupe 是 O(n×m) | 长对话（>1000 msg）会肉眼可见慢；考虑分段 / 总结 |
| `Topic(accumulate=True)` 永久增长 | 必须有清理策略（比如配 TTL 节点） |
| `EphemeralValue` 不进 checkpoint | 优势：省 IO；劣势：恢复后丢失 → 别用来存关键中间态 |
| `LastValue` 同超步多写报错 | 错误信息 `"LastValue can only receive one update at a time"`；用 reducer 替代 |
| Pydantic v2 校验开销 | 高频字段考虑 TypedDict |

---

## 12. 错误清单

| 错误 | 何时触发 | 解法 |
|------|---------|------|
| `EmptyChannelError` | 读未初始化的 channel | 在 schema 给默认值，或 `LastValue` 改 `EphemeralValue(initial=...)` |
| `InvalidUpdateError("LastValue can only receive one update")` | 同超步两个节点写同 `LastValue` 字段 | 改 reducer 字段，或拆超步 |
| `KeyError: 'channel'` | 节点写不存在的字段 | 拼写 / 字段未在 schema |
| reducer raise | 用户函数抛错 | 检查类型一致性 |

---

## 13. 与 Dawning 的对应

| LangGraph 概念 | Dawning 对应 |
|----------------|-------------|
| `BaseChannel` | `IMemoryChannel`（规划） |
| `LastValue` | 默认 `LastWriteWins` 策略 |
| `BinaryOperatorAggregate` | `IReducerStrategy<T>` |
| `add_messages` reducer | `MessageMergeReducer`（直接抄实现） |
| `Topic` (accumulate) | 与事件流 / `IAgentEventStream` 对齐 |
| `EphemeralValue` | `IScratchpadMemory` |
| `versions_seen` | Dawning checkpoint schema 字段 |
| `get_next_version` 可注入 | `IVersionStrategy` |

---

## 14. 阅读顺序

- 已读 → [[03-pregel-runtime]] 知道 reducer 在哪被调用
- 下一步 → [[05-checkpointer]] 看版本号怎么落盘
- 再下一步 → [[06-interrupt-hitl]] 看 channel 状态如何在 HITL 暂停时保留
- 想看 reducer 在多 Agent 协作中的体感 → [[cases/open-deep-research]]

---

## 15. 延伸阅读

- 源码：`libs/langgraph/langgraph/channels/`
- 官方 Conceptual Guide - Channels：<https://langchain-ai.github.io/langgraph/concepts/low_level/#channels>
- `add_messages` 源码：`libs/langgraph/langgraph/graph/message.py`
- [[02-state-graph#5]] schema → channel 的翻译规则
- [[../../concepts/state-persistence.zh-CN]] 状态持久化综述
