---
framework: langgraph
version: v1.1.x
type: synthesis
module: checkpointer
repo-path: libs/checkpoint*/
tags: [langgraph, checkpoint, persistence, durability]
created: 2026-04-18
updated: 2026-04-18
status: active
subtype: internals
title: LangGraph — 05 Checkpointer：持久化、durability 与时间旅行
sources: []
---

# LangGraph — 05 Checkpointer：持久化、durability 与时间旅行

> 本文回答：`checkpointer=PostgresSaver(...)` 之后到底发生了什么？schema 是什么？怎么"时间旅行"回到任意超步？


## 目录 <!-- TOC-AUTOGEN -->

- [1. 范围](#1-范围)
- [2. 一句话回答](#2-一句话回答)
- [3. 模块结构](#3-模块结构)
- [4. `BaseCheckpointSaver` 接口](#4-basecheckpointsaver-接口)
- [5. Checkpoint Schema](#5-checkpoint-schema)
- [6. 三元寻址：`(thread_id, checkpoint_ns, checkpoint_id)`](#6-三元寻址threadid-checkpointns-checkpointid)
- [7. PostgresSaver 表结构](#7-postgressaver-表结构)
- [8. 写入流程](#8-写入流程)
- [9. 时间旅行（Time Travel）](#9-时间旅行time-travel)
- [10. 序列化（Serde）](#10-序列化serde)
- [11. AsyncPostgresSaver 与连接池](#11-asyncpostgressaver-与连接池)
- [12. 不同 Saver 选型](#12-不同-saver-选型)
- [13. 与 Dawning 的对应](#13-与-dawning-的对应)
- [14. 错误清单](#14-错误清单)
- [15. 性能优化清单](#15-性能优化清单)
- [16. 阅读顺序](#16-阅读顺序)
- [17. 延伸阅读](#17-延伸阅读)
<!-- /TOC-AUTOGEN -->

> 重点路径：`langgraph-checkpoint/`、`langgraph-checkpoint-postgres/`、`langgraph-checkpoint-sqlite/`、`langgraph-checkpoint-duckdb/`。

---

## 1. 范围

| 在范围 | 不在范围 |
|--------|---------|
| `BaseCheckpointSaver` 接口 + 4 个内置实现 | 执行模型 → [[03-pregel-runtime#7]] |
| Checkpoint schema（持久化字段） | reducer 内部 → [[04-channels]] |
| `thread_id` / `checkpoint_id` / `checkpoint_ns` | HITL 暂停语义 → [[06-interrupt-hitl]] |
| `get_state` / `update_state` / `get_state_history` API | LangGraph Platform 托管 store → [[10-platform-integration]] |

---

## 2. 一句话回答

> Checkpointer 是 Pregel 的"**持久化适配器**"。每个超步前后写一笔 checkpoint（含 channel 值 + 版本号 + pending writes），
> 重启时按 `(thread_id, checkpoint_ns, checkpoint_id)` 拉回最近一笔继续跑。
> Checkpoint 之间通过 `parent_checkpoint_id` 串成历史链，支持回到任意超步重跑（time travel）。

---

## 3. 模块结构

<!-- Checkpoint 模块结构 -->
````mermaid
flowchart TB
    Abs["langgraph-checkpoint<br/>BaseCheckpointSaver · MemorySaver · Serde"]

    SQ["langgraph-checkpoint-sqlite<br/>SqliteSaver / AsyncSqliteSaver"]
    PG["langgraph-checkpoint-postgres<br/>PostgresSaver / AsyncPostgresSaver"]
    DD["langgraph-checkpoint-duckdb<br/>DuckDBSaver"]
    PT["LangGraph Platform Store<br/>(托管, 闭源)"]

    Abs --> SQ
    Abs --> PG
    Abs --> DD
    Abs --> PT

    classDef abs fill:#e7f5ff,stroke:#1971c2,color:#0b3d91
    classDef impl fill:#fff4e6,stroke:#f08c00
    classDef ext fill:#f3f0ff,stroke:#5f3dc4
    class Abs abs
    class SQ,PG,DD impl
    class PT ext
```
> 源文件：[`diagrams/checkpoint-modules.mmd`](../diagrams/checkpoint-modules.mmd)

| 包 | 内容 |
|----|------|
| `langgraph-checkpoint` | 抽象 + `MemorySaver` + `Serde` 协议 + 类型 |
| `langgraph-checkpoint-sqlite` | `SqliteSaver` / `AsyncSqliteSaver`，单机文件 |
| `langgraph-checkpoint-postgres` | `PostgresSaver` / `AsyncPostgresSaver`，生产首选 |
| `langgraph-checkpoint-duckdb` | `DuckDBSaver`，分析 / 嵌入式 |
| Platform Store | LangGraph Platform 托管，闭源 |

---

## 4. `BaseCheckpointSaver` 接口

```python
class BaseCheckpointSaver(Generic[V]):
    serde: SerializerProtocol = JsonPlusSerializer()

    # 读
    def get(self, config) -> Checkpoint | None: ...
    def get_tuple(self, config) -> CheckpointTuple | None: ...
    def list(self, config, *, filter=None, before=None, limit=None) -> Iterator[CheckpointTuple]: ...

    # 写
    def put(self, config, checkpoint, metadata, new_versions) -> RunnableConfig: ...
    def put_writes(self, config, writes, task_id, task_path="") -> None: ...

    # 删
    def delete_thread(self, thread_id) -> None: ...

    # async 镜像（aget / aget_tuple / alist / aput / aput_writes / adelete_thread）
```

**两类写**：

| 方法 | 写什么 | 何时调 |
|------|-------|-------|
| `put(config, checkpoint, metadata, new_versions)` | **整个** checkpoint 快照 | 每个 superstep 起点 + 终点 |
| `put_writes(config, writes, task_id)` | 单个 task 的 writes | task 完成时立刻写（保证 interrupt 不丢） |

> **设计动机**：`put_writes` 让"任务完成"的事实**先于** apply_writes 落盘，这样即使中途崩溃，下次启动时 loop 能识别"task X 已完成，writes 在这"，不会重跑。

---

## 5. Checkpoint Schema

```python
class Checkpoint(TypedDict):
    v: int                                       # schema version (现 v=4)
    id: str                                       # ULID / UUIDv7
    ts: str                                       # ISO timestamp
    channel_values: dict[str, Any]                # 各 channel 当前值（已序列化前）
    channel_versions: dict[str, V]                # 各 channel 当前版本号
    versions_seen: dict[str, dict[str, V]]        # {node: {channel: version}}
    pending_sends: list[Send]                     # 上一超步留下的 Send
    pending_writes: list[PendingWrite]            # task 完成但未 apply 的 writes
    updated_channels: list[str] | None            # 上次 apply_writes 涉及的字段（debug 用）

class CheckpointMetadata(TypedDict):
    source: Literal["input", "loop", "update", "fork"]
    step: int
    parents: dict[str, str]                       # checkpoint_ns -> parent_checkpoint_id
```

`source` 字段含义：

| source | 含义 |
|--------|------|
| `input` | 第一笔，`step=-1`，仅含输入 |
| `loop` | 正常超步产物 |
| `update` | 用户调 `update_state(...)` 产生 |
| `fork` | 从历史某点 `update_state(checkpoint_id=...)` 产生分支 |

---

## 6. 三元寻址：`(thread_id, checkpoint_ns, checkpoint_id)`

```python
config = {
    "configurable": {
        "thread_id": "user-42",            # 必填：会话 / 任务唯一 ID
        "checkpoint_ns": "",                # 子图命名空间（嵌套子图自动填）
        "checkpoint_id": "01JZ...XYZ",      # 不填 = 最新；填 = 时间旅行
    }
}
```

| 维度 | 用途 | 默认 |
|------|-----|------|
| `thread_id` | 隔离不同会话 / 任务 | 必填 |
| `checkpoint_ns` | 子图命名空间，形如 `"outer:inner"` | `""`（顶层） |
| `checkpoint_id` | 时间旅行游标 | 最新一笔 |

> **Postgres 索引设计**：复合主键 `(thread_id, checkpoint_ns, checkpoint_id)`，常用查询 `WHERE thread_id=? AND checkpoint_ns=? ORDER BY checkpoint_id DESC LIMIT 1`。

---

## 7. PostgresSaver 表结构

```sql
CREATE TABLE checkpoints (
    thread_id TEXT NOT NULL,
    checkpoint_ns TEXT NOT NULL DEFAULT '',
    checkpoint_id TEXT NOT NULL,
    parent_checkpoint_id TEXT,
    type TEXT,                            -- serializer type
    checkpoint JSONB NOT NULL,
    metadata JSONB NOT NULL DEFAULT '{}',
    PRIMARY KEY (thread_id, checkpoint_ns, checkpoint_id)
);

CREATE TABLE checkpoint_blobs (
    thread_id TEXT NOT NULL,
    checkpoint_ns TEXT NOT NULL DEFAULT '',
    channel TEXT NOT NULL,
    version TEXT NOT NULL,
    type TEXT NOT NULL,
    blob BYTEA,                           -- 大对象单独存
    PRIMARY KEY (thread_id, checkpoint_ns, channel, version)
);

CREATE TABLE checkpoint_writes (
    thread_id TEXT NOT NULL,
    checkpoint_ns TEXT NOT NULL DEFAULT '',
    checkpoint_id TEXT NOT NULL,
    task_id TEXT NOT NULL,
    idx INT NOT NULL,
    channel TEXT NOT NULL,
    type TEXT,
    blob BYTEA NOT NULL,                  -- put_writes 单 task 的 writes
    task_path TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (thread_id, checkpoint_ns, checkpoint_id, task_id, idx)
);

CREATE TABLE checkpoint_migrations (version BIGINT PRIMARY KEY);
```

**关键设计**：

- **`checkpoints`** 存 metadata + 索引信息
- **`checkpoint_blobs`** 按 `(channel, version)` 单独存大对象 → 多个 checkpoint 共享同一个 channel value 不重复写
- **`checkpoint_writes`** 存 task 级 writes，每 task 完成就 insert，保障 interrupt 不丢
- **`checkpoint_migrations`** 跟踪 schema 版本，启动时自动 apply

---

## 8. 写入流程

<!-- Checkpoint 写入流程 -->
````mermaid
sequenceDiagram
    autonumber
    participant L as PregelLoop
    participant S as Saver
    participant DB as Postgres

    Note over L: superstep N 开始
    L->>S: put(config, checkpoint_A, metadata, new_versions)
    S->>DB: INSERT checkpoints (A)
    S->>DB: INSERT checkpoint_blobs (变化的 channel × version)

    par 并发执行 tasks
        L->>L: task1.run
        L->>S: put_writes(config, writes1, task1_id)
        S->>DB: INSERT checkpoint_writes
        L->>L: task2.run
        L->>S: put_writes(config, writes2, task2_id)
        S->>DB: INSERT checkpoint_writes
    end

    L->>L: barrier · apply_writes(in-memory)
    L->>S: put(config, checkpoint_B, metadata, new_versions)
    S->>DB: INSERT checkpoints (B)
    S->>DB: INSERT checkpoint_blobs (变化的 channel × version)

    Note over L,DB: 一个超步 = 4~6 次 DB 交互；<br/>未变 channel 不重写 blob
```
> 源文件：[`diagrams/checkpoint-write.mmd`](../diagrams/checkpoint-write.mmd)

每个 superstep 大致 6 次 DB 交互：

| 时机 | 操作 | 表 |
|------|-----|----|
| superstep 开始前 | `put(checkpoint A)` | checkpoints + 新增 channel blobs |
| 每个 task 完成 | `put_writes(task_id, writes)` | checkpoint_writes |
| barrier 后 apply_writes | （内存层操作，无 DB） | - |
| superstep 结束 | `put(checkpoint B)` | checkpoints + 新增 channel blobs（仅变化的） |

**优化**：
- `new_versions` 参数告诉 saver "这次哪些 channel 的版本变了"，只对它们 insert blob
- 未变化 channel 的 blob 复用旧记录（外键关系靠 `(channel, version)` 维护）

---

## 9. 时间旅行（Time Travel）

```python
# 1. 拉历史
history = list(graph.get_state_history(config))
# 每个 entry 是 StateSnapshot(values, next, config, metadata, created_at, parent_config, tasks)

# 2. 选一个旧 checkpoint
target = history[3]   # 第 4 新

# 3. 从那里 fork（产生 source="fork" 的新 checkpoint）
new_config = graph.update_state(
    target.config,             # 注意：用历史 config，含老 checkpoint_id
    {"counter": 999},          # 想改的字段
    as_node="my_node",         # 模拟该节点的写入
)

# 4. 从 fork 出的新 checkpoint 继续跑
graph.invoke(None, new_config)
```

**关键**：

- 历史链由 `parent_checkpoint_id` 形成 DAG（fork 会分叉）
- 一个 thread 可以有任意多分支
- 删 thread (`delete_thread`) 时连带删除所有分支
- LangGraph Studio 直接可视化这个 DAG

---

## 10. 序列化（Serde）

```python
class SerializerProtocol(Protocol):
    def dumps(self, obj: Any) -> bytes: ...
    def loads(self, data: bytes) -> Any: ...
    def dumps_typed(self, obj: Any) -> tuple[str, bytes]: ...
    def loads_typed(self, data: tuple[str, bytes]) -> Any: ...
```

**默认实现 `JsonPlusSerializer`**：

| 类型 | 编码 |
|------|-----|
| 标量 / dict / list | JSON |
| `BaseMessage` 子类 | `("msg", json bytes)` |
| Pydantic v2 model | `("pyd", json bytes)` |
| `numpy.ndarray` | msgpack（`msgspec.msgpack`） |
| 任意 picklable | `("pickle", pickle bytes)`（最后兜底） |

**踩坑**：

| 问题 | 原因 | 解法 |
|------|-----|------|
| 历史 checkpoint 加载失败 | 用了 pickle，而类定义改了 | 用 Pydantic / TypedDict，避免 pickle |
| 跨语言读 checkpoint | 默认 serde 含 pickle 不可读 | 自定义 `SerializerProtocol` 强制 JSON |
| 二进制大对象慢 | 全 JSON 编码膨胀 | 改 msgpack serde（社区有现成包） |

---

## 11. AsyncPostgresSaver 与连接池

```python
from langgraph.checkpoint.postgres.aio import AsyncPostgresSaver
from psycopg_pool import AsyncConnectionPool

async with AsyncConnectionPool(
    conninfo="postgresql://...",
    max_size=20,
    kwargs={"autocommit": True, "row_factory": dict_row},
) as pool:
    saver = AsyncPostgresSaver(pool)
    await saver.setup()        # 跑 migration
    graph = builder.compile(checkpointer=saver)
    async for chunk in graph.astream(input, config):
        ...
```

**生产清单**：

- 必须用 connection pool（每超步 6 次查询，无池会卡瓶颈）
- `autocommit=True` —— saver 自己管事务
- `setup()` 幂等，可启动时调；或用迁移工具
- `pool.max_size` 至少 = 并发 thread 数 × 2

---

## 12. 不同 Saver 选型

| Saver | 适合 | 不适合 | 备注 |
|-------|-----|-------|------|
| `MemorySaver` | 单测 / dev / Notebook | 多进程 / 重启 | 进程退出即清 |
| `SqliteSaver` | 单机 demo / CLI 工具 | 高并发 / 跨主机 | 文件锁 → 写并发差 |
| `PostgresSaver` | **生产首选** | 嵌入式 / 边缘 | 需 PG ≥ 13；推荐 ≥ 15 |
| `DuckDBSaver` | 分析 / 嵌入式 / 单机大量历史查询 | 高并发写 | 列式存储，分析查询快 |
| Platform Store | 完全托管，零运维 | 自托管需求 | 仅 LangGraph Platform |

---

## 13. 与 Dawning 的对应

| LangGraph 概念 | Dawning 对应 | 备注 |
|----------------|-------------|------|
| `BaseCheckpointSaver` | `IWorkflowCheckpoint`（规划） | 接口几乎可复用 |
| `(thread_id, ns, id)` 三元寻址 | `WorkflowKey { ThreadId, Namespace, CheckpointId }` | 直接抄 |
| `put` + `put_writes` 双写 | 同样建议双写：保证 task 完成事实先落盘 | |
| `parent_checkpoint_id` DAG | Dawning 历史链 | time travel 必备 |
| `SerializerProtocol` | `IStateSerializer<T>` | JSON / msgpack / 自定义 |
| Postgres 表结构 | Dawning.Workflow.Postgres 包参考 | 直接抄 schema |
| `setup()` 自动 migration | Dawning 用 EF Core / Dapper migration | |
| Connection pool | Npgsql `NpgsqlDataSource` | |

---

## 14. 错误清单

| 错误 | 触发 | 解法 |
|------|-----|------|
| `EmptyChannelError` after restart | checkpoint 加载但 schema 加了新字段 | 给字段默认值，或写迁移代码 |
| 历史 checkpoint 反序列化失败 | 类定义变更 | 实现兼容 serde；或定期清旧 checkpoint |
| `put` 慢 | 单条 INSERT 没用 prepared statement | 升级 saver 版本（v1.1.x 已优化）；或换 pool |
| 时间旅行 update_state 不生效 | 没传旧 `checkpoint_id` | 必须用 history 里的 config，不能手拼 |
| 重启后 task 重跑 | `put_writes` 异常 / 表不存在 | 跑 `setup()`；检查权限 |

---

## 15. 性能优化清单

| 优化 | 收益 | 代价 |
|------|------|------|
| 大字段改 `EphemeralValue` | checkpoint 体积 -50%~90% | 不能跨重启恢复 |
| 老 checkpoint 定期清 | DB 体积可控 | 失去时间旅行能力 |
| 批量 history 查询用 `list(filter=..., limit=...)` | 避免全表扫 | - |
| 自定义 `get_next_version`（更紧凑 ID） | 索引更小 | 与默认实现互不兼容 |
| msgpack serde | dump/load 快 2x | 不可人读 |
| 子图独立 thread_id | 父子图各自压力分散 | 失去统一 history |

---

## 16. 阅读顺序

- 已读 → [[03-pregel-runtime#7]] 知道 checkpoint 何时被写
- 已读 → [[04-channels]] 知道版本号是什么
- 下一步 → [[06-interrupt-hitl]] 看 checkpoint 怎么支撑 HITL
- 想看真实 schema 演化 → 仓库 `libs/checkpoint-postgres/langgraph/checkpoint/postgres/migrations/`

---

## 17. 延伸阅读

- 官方 Persistence 概念：<https://langchain-ai.github.io/langgraph/concepts/persistence/>
- 时间旅行教程：<https://langchain-ai.github.io/langgraph/how-tos/persistence/time-travel/>
- 源码：`libs/checkpoint*`
- [[../../concepts/state-persistence.zh-CN]]
- [[01-architecture#7]] 持久化分层
