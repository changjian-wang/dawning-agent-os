---
title: ADR-033 Memory Ledger V0：MemoryLedgerEntry 聚合 + SQLite 持久化 + 软删 + 显式写入端点 + Memory 视图
type: adr
subtype: architecture
canonical: true
summary: V0 显式 Memory Ledger 切片采用 `MemoryLedgerEntry` 聚合根（UUIDv7、状态机：Active/Corrected/Archived/SoftDeleted、显式 vs 推断、敏感度三档、置信度 0–1）+ SQLite 持久化（migration v4 新增 memory_entries 表 + (status, updated_at_utc DESC, id DESC) 索引）+ Application 层 `IMemoryLedgerAppService` facade（CreateExplicit / List / GetById / Update / SoftDelete）+ 5 个 RESTful 端点（POST /api/memory、GET /api/memory、GET /api/memory/{id}、PATCH /api/memory/{id}、DELETE /api/memory/{id} 软删）+ 桌面端"📒 memory"主视图（与现有 chat+inbox 视图通过 header 切换按钮平级，不挤进现有 65/35 分屏）。第一版只接受用户**显式写入**，禁止从 chat / inbox / LLM 抽取；Source 枚举保留 Conversation/InboxAction/Correction 占位但 V0 拒绝写入。兴趣画像权重 / 衰减字段（ADR-013）**不进 V0 schema**，将通过未来旁表 `memory_entries_signals` 加入。删除统一软删（status=SoftDeleted + deleted_at_utc），物理清理（30 天扫描）留后续 ADR。新错误码 `memory.notFound` → 404，`memory.invalidStatusTransition` → 422。
tags: [agent, memory, memory-design, privacy, engineering]
sources: []
created: 2026-05-06
updated: 2026-05-06
verified_at: 2026-05-06
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/explicit-memory-ledger-mvp.md, pages/adrs/memory-privacy-and-user-control.md, pages/adrs/long-term-memory-as-core-capability.md, pages/adrs/interest-profile-weighting-and-decay.md, pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/butler-positioning-and-subject-object-boundary.md, pages/adrs/important-action-levels-and-confirmation.md, pages/adrs/inbox-v0-capture-and-list-contract.md, pages/adrs/chat-v0-streaming-and-persistence.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/sqlite-dapper-bootstrap-and-schema-init.md, pages/adrs/desktop-renderer-v0-native-html-and-ipc-bridge.md, pages/adrs/desktop-process-supervisor-electron-dotnet-child.md, pages/adrs/backend-architecture-equinox-reference.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-06
adr_revisit_when: "Memory Ledger 在 dogfood 中被发现"无法支撑信息整理"（迫使引入 chat / inbox 自动写入路径或 LLM 抽取）；用户开始要求"agent 主动从对话学习"（迫使解锁 Source=Conversation 写入路径并定义推断写入规则）；ledger 量增长导致人工查看不可用（迫使引入分类、搜索、向量检索）；ADR-013 兴趣画像权重 / 衰减实施需要落地（迫使新增 memory_entries_signals 旁表 + ADR）；用户要求记忆参与 chat 上下文注入（迫使设计 Memory→Chat 注入策略与 token 预算）；软删 30 天回收策略需要真正生效（迫使引入定期清理任务 / IHostedService 扫描）；用户报告"自己写的记忆没有保护"（迫使引入加密 / 导出 / 审计）；多设备 / 云同步需求出现（迫使重新评估"本地优先"策略）；recall 误用导致信任问题（如 agent 引用过期或错误记忆做判断，迫使加强 confidence 与 status 的展示与降权机制）；UI 在 ledger 数量上千时性能不足（迫使虚拟滚动 / 分页 / 索引调优）。"
---

# ADR-033 Memory Ledger V0：聚合 + 持久化 + 显式写入 + 软删 + Memory 视图

> V0 显式 Memory Ledger 切片：`MemoryLedgerEntry` 聚合 + SQLite migration v4 + 状态机（Active / Corrected / Archived / SoftDeleted）+ 5 个 RESTful 端点 + 桌面 "📒 memory" 主视图（与 chat+inbox 视图平级，不挤进 65/35 分屏）。第一版只接受**用户显式写入**，禁止 chat / inbox / LLM 自动抽取。兴趣权重字段不进 V0 schema。删除走软删；物理清理留后续 ADR。

## 背景

[ADR-011 Memory MVP 采用显式记忆账本](explicit-memory-ledger-mvp.md) 在 4 月 28 日 accepted，但只把"做什么"定下来：第一版采用显式 Ledger，可解释 / 可查看 / 可编辑 / 可删除；向量检索后置；ledger 字段必须能表达 source / scope / content / explicit_or_inferred / confidence / sensitivity / status / created_at / 兴趣类的 weight / last_touched_at / decay_policy。

[ADR-007 记忆隐私与用户控制](memory-privacy-and-user-control.md) 锁死了几条隐私红线：conversation memory 是默认来源；agent 不主动扫描 user 电脑；高敏感推断不得自动升级稳定 user profile；推断必须标注。

[ADR-013 兴趣画像采用权重与时间衰减](interest-profile-weighting-and-decay.md) 进一步要求：兴趣 tags 不是永久身份标签，需要 weight + confidence + last_touched_at + decay_policy + 简单可解释规则。

[PURPOSE.md `mvp_first_slice`](../../PURPOSE.md) 把 Memory Ledger 列为 MVP 第一版必须包含的部分（"Memory Ledger 可查看 / 编辑 / 删除"），但截至 2026-05-06，[ADR-026 Inbox V0](inbox-v0-capture-and-list-contract.md)、[ADR-030 总结](inbox-item-summarize-v0.md)、[ADR-031 标签](inbox-item-tagging-v0.md)、[ADR-032 Chat V0](chat-v0-streaming-and-persistence.md) 都已落地，**唯独 Memory Ledger 还是空白**。MVP 第一版闭环就剩这一块。

V0 必须同时回答以下十三个工程问题：

1. **聚合形态**：Domain 模型是什么？是否复用 inbox / chat 的形态？
2. **写入来源（Source）**：第一版接受哪些 source 写入？conversation / inbox 自动抽取要不要进？
3. **scope 类型**：自由字符串还是受控枚举？
4. **explicit vs inferred 第一版策略**：V0 有没有"推断"路径？
5. **confidence 默认值**：用户显式写入时 confidence 是多少？
6. **sensitivity 枚举**：分几档？
7. **status 状态机与软删**：状态有几个？转移规则如何？软删用 status 还是单列 deleted_at？
8. **兴趣画像字段（weight / last_touched_at / decay_policy）是否进 V0 schema？**
9. **Repository 操作集**：除了 inbox 的 Add/List/GetById/Count，需要哪些新操作（Update / SetStatus）？
10. **API 形态**：几个 endpoint？错误映射？
11. **桌面 UI 落点**：挤进现有 65/35 分屏（第三栏），还是新增一个平级主视图？
12. **migration 版本号**：v4 是否冲突 / schema 表名是否冲突？
13. **物理删除（30 天扫描）**：V0 做不做？

本 ADR 一次性把这十三个问题定下来；落地按 4 个 commit 切分（见 §实施概要）。

用户已在方案先行讨论中明确：接受按 inbox / chat 同构方式实现；接受软删；接受 Memory 作为新平级视图；接受 V0 不做兴趣权重字段（留给 ADR-013 实施）；接受 V0 只允许显式写入。

## 备选方案

**A. 聚合形态**：

- 方案 A1：`MemoryLedgerEntry : AggregateRoot<Guid>`，UUIDv7 主键，`Create` 业务工厂 + `Rehydrate` 持久化工厂；与 [ADR-026](inbox-v0-capture-and-list-contract.md) `InboxItem` / [ADR-032](chat-v0-streaming-and-persistence.md) `ChatSession` 完全同构
- 方案 A2：`MemoryLedgerEntry` 不做聚合，仅 record + Repository 直接操作 row
- 方案 A3：把 ledger 嵌入某个父聚合（例如 `User`），作为子实体

**B. 写入来源**：

- 方案 B1：第一版仅接受 `UserExplicit` 写入（用户在 Memory 视图手动新增），其余 `Conversation / InboxAction / Correction` 枚举位**保留**但不接受写入
- 方案 B2：第一版同时支持 `UserExplicit + Conversation`（chat 一句"记下来"自动落 ledger）
- 方案 B3：第一版同时支持所有 source，包括 LLM 从对话主动抽取
- 方案 B4：第一版完全没有 Source 字段，由后续 ADR 引入

**C. scope 类型**：

- 方案 C1：自由 `TEXT`，约定 `global`（默认）/ 项目名 / 任务名等字符串，V0 不强校验
- 方案 C2：受控枚举（`Global / Project / Task`），扩展时改 schema
- 方案 C3：双字段 `scope_kind ENUM + scope_value TEXT`

**D. explicit vs inferred**：

- 方案 D1：保留 `is_explicit BOOLEAN NOT NULL`；V0 强制 `true`（因为只有 UserExplicit 写入路径），保留列为后续推断写入做准备
- 方案 D2：V0 完全省略此列，等到 Source=Conversation 实施时再加 migration
- 方案 D3：合并到 Source 枚举（约定 `Inferred*` 系列代表推断）

**E. confidence**：

- 方案 E1：`REAL NOT NULL DEFAULT 1.0`（0.0–1.0），V0 用户显式写入时默认 1.0；列保留为推断写入做准备
- 方案 E2：V0 不引入此列，等到推断写入时再加
- 方案 E3：用 INTEGER 0–100

**F. sensitivity**：

- 方案 F1：三档枚举 `Normal / Sensitive / HighSensitive`（与 ADR-007 保持一致）
- 方案 F2：两档 `Normal / Sensitive`，第一版简化
- 方案 F3：自由 tag 字符串

**G. status 状态机 + 软删**：

- 方案 G1：状态枚举 `Active / Corrected / Archived / SoftDeleted` + 单列 `deleted_at_utc TEXT NULL`（仅在 SoftDeleted 时填）；删除 = 设 status + 设 deleted_at；恢复 = `Restore` 方法将 status 改回 Active 并清 deleted_at
- 方案 G2：仅用 `is_deleted BOOLEAN`，不引入完整状态机
- 方案 G3：物理删除（DELETE row），不做软删
- 方案 G4：用 status 单列覆盖软删（不另设 deleted_at_utc 列），用 updated_at_utc 充当软删时间

**H. 兴趣画像字段**：

- 方案 H1：V0 **不进** schema；ADR-013 实施时新增旁表 `memory_entries_signals (entry_id FK, weight REAL, last_touched_at TEXT, decay_policy TEXT)`，避免破坏性 ALTER 主表
- 方案 H2：V0 主表加 `weight REAL NULL, last_touched_at TEXT NULL, decay_policy TEXT NULL`，列保留为兴趣画像备用
- 方案 H3：V0 主表加 `signals_json TEXT NULL`，所有兴趣相关字段塞 JSON

**I. Repository 操作集**：

- 方案 I1：`Add / GetById / Update / SetStatus / List(statusFilter?, limit, offset) / Count(statusFilter?)`；`SetStatus` 单独走以避免 PATCH 路径误改 source / created_at
- 方案 I2：只有 `Add / GetById / Update / List / Count`，软删走 Update 路径
- 方案 I3：完全 CRUD（`Add / Update / Delete`），不区分 SetStatus

**J. API 形态**：

- 方案 J1：5 个端点
  - `POST /api/memory` — 创建（body: content, scope?, sensitivity?）
  - `GET /api/memory?limit&offset&status` — 列表，默认过滤 SoftDeleted
  - `GET /api/memory/{id}` — 单条
  - `PATCH /api/memory/{id}` — 更新 content / scope / sensitivity / status
  - `DELETE /api/memory/{id}` — 软删
- 方案 J2：4 个端点（合并 GET /{id} 进 List 的客户端过滤）
- 方案 J3：JSON-RPC 风格单端点
- 方案 J4：把 Status 转移做成单独子资源 `POST /api/memory/{id}/archive` / `restore`

**K. 桌面 UI 落点**：

- 方案 K1：现有 chat+inbox 65/35 分屏不动；header 加切换按钮"💬 chat & inbox" / "📒 memory"，renderer 通过 hash route + 容器显示/隐藏切换两个主视图
- 方案 K2：在现有分屏中再切一刀（左 chat 50% / 中 inbox 25% / 右 memory 25%）
- 方案 K3：Memory 作为独立 BrowserWindow（双窗口）
- 方案 K4：Memory 作为 chat / inbox 内的弹窗（modal）

**L. migration 版本号**：

- 方案 L1：`0004_create_memory_entries.sql` —— 沿用 [ADR-024](sqlite-dapper-bootstrap-and-schema-init.md) §I1 嵌入资源 + `__schema_version` 表机制；与已有 0001/0002/0003 平级
- 方案 L2：把 memory 表合进 0003 chat 迁移
- 方案 L3：跳号（0010+）以预留中间空间

**M. 物理删除（30 天扫描）**：

- 方案 M1：V0 不做；只标记 SoftDeleted + deleted_at_utc，扫描清理留后续 ADR
- 方案 M2：V0 启动时扫一次 deleted_at < now-30d 的行并物理删
- 方案 M3：V0 用 IHostedService 后台周期扫描

## 被否决方案与理由

**A2 / A3（不做聚合 / 嵌入父聚合）**：违反 [ADR-021 后端架构 EquinoxProject 参照](backend-architecture-equinox-reference.md) 的 DDD 分层，破坏与 inbox / chat 的对称性，未来扩展（如领域事件、状态机封装）会很难。

**B2 / B3（V0 即接受 Conversation 自动写入 / LLM 抽取）**：违反 [ADR-007 §决策](memory-privacy-and-user-control.md)"推断性记忆必须标注为推断"。V0 还没有 chat→ledger 的写入策略（要不要 LLM 主动判断、判断成本、用户能否撤销）；如果第一版就开自动写入，会产生大量"没法解释为什么记下"的条目。先把显式路径走通再说。

**B4（不要 Source 字段）**：未来加 source 时要 migration 改表，浪费机会窗口；ADR-011 §决策已把 source 列为"必须能表达"的字段。

**C2 / C3（受控枚举 scope）**：MVP 还不知道 scope 应该有几档；先用自由字符串收集真实样本，定下来再 migrate。

**D2（不引入 is_explicit）**：与 B 同理，未来加列要 migration；现在加一列 `BOOLEAN NOT NULL DEFAULT 1` 几乎零成本。

**D3（合并进 Source 枚举）**：把"明确 vs 推断"和"来源"两个正交维度耦合进同一个枚举，未来扩展会乱（例如 user 在 chat 中显式说"记下"是 explicit + Conversation，按 D3 就要造 `ExplicitConversation` 这种丑名）。

**E2（不引入 confidence）**：与 B / D 同理。

**E3（INTEGER 0–100）**：精度浪费 + 类型不统一。`REAL` 在 SQLite 上零成本，且与 ADR-013 兴趣权重的 `weight REAL` 同构。

**F2（sensitivity 两档）**：违反 ADR-007"高敏感推断不得自动升级"——这要求至少能区分"敏感"和"高敏感"。

**F3（自由 tag）**：会变成隐私红线的逃逸通道（用户随便写个 tag 就绕过敏感度判断）。枚举强制三档与 ADR-007 一致。

**G2 / G3（仅 is_deleted / 物理删除）**：违反 [PURPOSE.md L3 红线](../../PURPOSE.md)"删除默认走软删 + 30 天恢复窗口"；物理删除即使在 V0 也不允许，因为这是产品红线而不是工程偏好。

**G4（不另设 deleted_at_utc）**：扫描清理 30 天前的软删时无法只用 status；要么扫全表，要么扫 `updated_at_utc` 但那会和"内容更新"语义冲突（用户改了一次就把删除计时重置）。`deleted_at_utc` 单列是干净的。

**H2 / H3（V0 主表加兴趣字段 / signals_json）**：兴趣权重需要"被使用频次"信号驱动衰减；V0 还没有 chat / inbox → ledger 的注入路径，权重信号无源。空列或空 JSON 既无用又会让 ADR-013 实施时陷入"是改这一列还是新加表"的纠结。一次到位最干净的方式是 ADR-013 实施时新加 `memory_entries_signals` 旁表，主表保持纯净。

**I2（软删走 Update 路径）**：风险点是 PATCH 端点错误地把 source / created_at 也改了，因为 Update 接受全字段。把 SetStatus 单独提出来，让 PATCH 只能改"业务允许改"的字段。

**I3（CRUD 不区分 SetStatus）**：与 I2 同理，外加 `Delete` 名字会让人误以为物理删。

**J2 / J3（合并 GET /{id} 进 List / JSON-RPC）**：违反 [ADR-023 API 入口与 V0 端点](api-entry-facade-and-v0-endpoints.md) RESTful 风格。

**J4（Status 转移走子资源）**：理论上更优雅但 V0 工程量太大，且 PATCH 已经能完成同样的事（PATCH body 里改 status）。等到状态转移规则真的复杂（例如需要审批 / 批量），再开新 ADR 走子资源。

**K2 / K3 / K4（三栏 / 双窗口 / 弹窗）**：

- K2：当前 1100×720 分辨率下 chat 已经 715px，inbox 385px，再切一刀任何一栏都跌破最小宽度
- K3：双窗口违反 [ADR-027](desktop-renderer-v0-native-html-and-ipc-bridge.md) "single window V0"约束
- K4：modal 影响 ledger 的可查阅性（用户想边看 chat 边查 memory），且违反"管家形态而非工具"的产品定位

**L2（合进 0003）**：违反 [ADR-024 §I1](sqlite-dapper-bootstrap-and-schema-init.md) "每条迁移单文件"约束，且 0003 已经在用户机器上 applied，再改文件会破坏幂等性。

**L3（跳号）**：违反 ADR-024 §I1 "顺序 NNNN"约定。

**M2 / M3（V0 启动扫描 / IHostedService 后台清理）**：物理清理涉及"恢复窗口结束后用户能否找回"——如果误删 30 天但用户 31 天才发现，V0 是否要给恢复机制？这是产品决策点，不是工程决策点，不应在 V0 ADR 里悄悄上线。等 dogfood 跑一段时间后再开新 ADR。

## 决策

### 决策 A1（聚合形态 = MemoryLedgerEntry 聚合根）

`MemoryLedgerEntry : AggregateRoot<Guid>`，UUIDv7 主键，`Create` 业务工厂 + `Rehydrate` 持久化工厂；与 `InboxItem` / `ChatSession` 完全同构。

聚合内部封装状态转移方法（不对外暴露 setter）：

| 方法 | 前置 status | 后置 status | 副作用 |
|---|---|---|---|
| `Create` | — | `Active` | 工厂；UUIDv7 生成；置 created/updated；不 raise event（V0 暂无 dispatcher，与 inbox 同款） |
| `UpdateContent(string)` | `Active / Corrected` | 不变 | 改 content；触发 updated_at |
| `UpdateScope(string)` | `Active / Corrected` | 不变 | 改 scope；触发 updated_at |
| `UpdateSensitivity(Sensitivity)` | `Active / Corrected` | 不变 | 改 sensitivity；触发 updated_at |
| `MarkCorrected()` | `Active` | `Corrected` | "用户纠正过这条"标记 |
| `Archive()` | `Active / Corrected` | `Archived` | "暂时不用但保留"；触发 updated_at |
| `SoftDelete(at)` | `Active / Corrected / Archived` | `SoftDeleted` | 设 deleted_at_utc；触发 updated_at |
| `Restore()` | `SoftDeleted` | `Active` | 清 deleted_at_utc；触发 updated_at |

非法转移（如 `SoftDeleted` 直接 `Archive`、或对 `SoftDeleted` 调用 `UpdateContent`）抛 `InvalidOperationException`，由 Application 层捕获转 `memory.invalidStatusTransition` 错误码。

### 决策 B1（V0 仅 UserExplicit 写入）

`Source` 枚举值序声明：

```csharp
public enum MemorySource
{
    UserExplicit = 1,
    Conversation = 2,
    InboxAction = 3,
    Correction = 4,
}
```

V0 Application 层 `CreateExplicitAsync` 强制 `source = UserExplicit, isExplicit = true`，request DTO 不暴露这两个字段。其余三个枚举位**保留** schema 兼容性，待后续 ADR 解锁写入路径。

### 决策 C1（scope = 自由 TEXT）

`scope TEXT NOT NULL DEFAULT 'global'`，最大长度 128 字符。V0 不强校验内容；约定值由 dogfood 收敛。

### 决策 D1 / E1（is_explicit + confidence 入 schema）

```sql
is_explicit  INTEGER NOT NULL DEFAULT 1   -- 1=true, 0=false
confidence   REAL    NOT NULL DEFAULT 1.0
```

V0 写入路径强制 `is_explicit=1, confidence=1.0`；Domain 层接受任意值（为后续推断写入路径做准备）。

### 决策 F1（sensitivity 三档枚举）

```csharp
public enum MemorySensitivity
{
    Normal = 1,
    Sensitive = 2,
    HighSensitive = 3,
}
```

写入时未指定默认 `Normal`。`HighSensitive` 在 PATCH 端点上额外要求"必须显式 user 操作"（即 V0 阶段任何对 sensitivity 的修改都来自 user，无需特殊处理；待 chat 自动写入 ADR 实施时由该 ADR 加规则）。

### 决策 G1（status 四档 + 软删双列）

```csharp
public enum MemoryStatus
{
    Active = 1,
    Corrected = 2,
    Archived = 3,
    SoftDeleted = 4,
}
```

```sql
status         INTEGER NOT NULL DEFAULT 1
deleted_at_utc TEXT    NULL                -- 仅在 status=SoftDeleted 时填
```

软删 = `SetStatus(SoftDeleted) + SetDeletedAt(now)`；`Restore()` = `SetStatus(Active) + SetDeletedAt(null)`。

### 决策 H1（兴趣画像字段不进 V0）

V0 主表 `memory_entries` 不含 `weight / last_touched_at / decay_policy`。ADR-013 实施时通过 migration v5 新增旁表：

```sql
CREATE TABLE memory_entries_signals (
    entry_id        TEXT NOT NULL PRIMARY KEY,
    weight          REAL NOT NULL,
    last_touched_at TEXT NOT NULL,
    decay_policy    TEXT NOT NULL,
    FOREIGN KEY (entry_id) REFERENCES memory_entries(id) ON DELETE CASCADE
);
```

旁表与主表 1:1，但只在"该条 entry 是兴趣画像类"时建行；CASCADE 让 SoftDelete 不主动级联（仅状态机），但物理删（未来）级联清理。

### 决策 I1（Repository 操作集）

`IMemoryLedgerRepository`（Domain 层）：

```csharp
Task AddAsync(MemoryLedgerEntry entry, CancellationToken ct);
Task<MemoryLedgerEntry?> GetByIdAsync(Guid id, CancellationToken ct);
Task UpdateAsync(MemoryLedgerEntry entry, CancellationToken ct);
Task<IReadOnlyList<MemoryLedgerEntry>> ListAsync(
    MemoryStatus? statusFilter, int limit, int offset, CancellationToken ct);
Task<long> CountAsync(MemoryStatus? statusFilter, CancellationToken ct);
```

`UpdateAsync` 写入全部可变列（content / scope / sensitivity / status / deleted_at_utc / updated_at_utc）；`Add / Update` 区分由 Application 层负责。不单独提 `SetStatusAsync` —— 状态转移在 Domain 聚合上完成后通过 `UpdateAsync` 落库即可，已经把"误改不可变字段"风险关在 Domain 内（聚合不暴露 source / created_at / is_explicit / confidence 的 setter）。

### 决策 J1（5 个 RESTful 端点）

```
POST   /api/memory                         body: { content, scope?, sensitivity? }
GET    /api/memory?limit&offset&status     默认过滤 SoftDeleted
GET    /api/memory/{id}
PATCH  /api/memory/{id}                    body: { content?, scope?, sensitivity?, status? }
DELETE /api/memory/{id}                    软删
```

错误码：

| 场景 | 错误码 | HTTP |
|---|---|---|
| 内容空 / 超长 | `memory.content.required / memory.content.tooLong` | 400 |
| scope 超长 | `memory.scope.tooLong` | 400 |
| sensitivity / status 枚举值非法 | `memory.sensitivity.invalid / memory.status.invalid` | 400 |
| limit / offset 越界 | `memory.limit.outOfRange / memory.offset.outOfRange` | 400 |
| id 找不到 | `memory.notFound` | 404 |
| 状态转移非法 | `memory.invalidStatusTransition` | 422 |

`POST / GET` 用 [`ResultHttpExtensions.ToHttpResult`](../../src/Dawning.AgentOS.Api/Results) 自动映射；`GET/{id} / PATCH / DELETE` 走 [ADR-030 / ADR-031](inbox-item-summarize-v0.md) 的 manual switch（不能让 404 / 422 变 422 一刀切）。

### 决策 K1（桌面新增"📒 memory"主视图，与 chat+inbox 视图平级）

renderer 改造：

- 顶部 header 增加 view-switcher：两个 button（💬 chat & inbox / 📒 memory），active 状态高亮
- 把现有 `<div class="split">` 包进 `<div id="view-chat-inbox" class="view">`
- 新增 `<div id="view-memory" class="view">`：左侧条目列表（按 updated_at_utc DESC）+ 右侧详情/编辑面板 + 顶部"+ 新记忆"按钮 + "显示已删除"toggle
- 切换 view 用 hash route（`#chat-inbox` / `#memory`） + 隐藏/显示对应 `.view`，不引入框架
- 新建条目：右侧面板进入"新建模式"，提供 content textarea + scope input + sensitivity select + 保存/取消
- 编辑条目：选中列表项后右侧进入"编辑模式"，可改 content / scope / sensitivity / status；底部"软删"按钮（confirm 后调 DELETE）
- 软删的项默认隐藏；toggle 打开后软删条目以淡灰色显示，编辑模式下提供"恢复"按钮（PATCH status=Active）

preload bridge 新增 `window.agentos.memory.{ create, list, getById, update, softDelete, restore }`；main 进程添加对应 6 个 IPC handler，全部走 `runtime.token` + fetch + readBodyAsResult，与现有 inbox / chat handler 同构。

### 决策 L1（migration v4）

新文件 `src/Dawning.AgentOS.Infrastructure/Persistence/Migrations/0004_create_memory_entries.sql`：

```sql
-- ADR-033 §决策 G1 / H1 — Memory Ledger V0 schema.
CREATE TABLE memory_entries (
    id              TEXT    NOT NULL PRIMARY KEY,
    content         TEXT    NOT NULL,
    scope           TEXT    NOT NULL DEFAULT 'global',
    source          INTEGER NOT NULL,                   -- MemorySource enum
    is_explicit     INTEGER NOT NULL DEFAULT 1,
    confidence      REAL    NOT NULL DEFAULT 1.0,
    sensitivity     INTEGER NOT NULL DEFAULT 1,         -- MemorySensitivity enum
    status          INTEGER NOT NULL DEFAULT 1,         -- MemoryStatus enum
    created_at_utc  TEXT    NOT NULL,
    updated_at_utc  TEXT    NOT NULL,
    deleted_at_utc  TEXT    NULL
);

CREATE INDEX ix_memory_entries_status_updated
    ON memory_entries (status, updated_at_utc DESC, id DESC);
```

启动序列由 [ADR-024 §I1](sqlite-dapper-bootstrap-and-schema-init.md) 现有 `SqliteSchemaInitializer` 自动应用；空库新装 + 已有用户 v3 数据库都会自动升级到 v4。

### 决策 M1（V0 不做 30 天物理清理）

软删后行永久保留；恢复永远可行。30 天扫描清理任务留后续 ADR。

复议触发条件 `adr_revisit_when` 已在 frontmatter 列出"软删 30 天回收策略需要真正生效"。

### 决策 N（不在 V0 范围内的事）

> 本节统一列出 V0 刻意不做的事；触发条件出现时开新 ADR。

- **chat → ledger 自动写入** —— 用户在 chat 一句"记下来"自动落 ledger（需要 LLM tool call 或前端按钮 + 写入端点解锁 Source=Conversation）
- **ledger → chat prompt 注入** —— V0 chat 的 system prompt 不读 ledger（需要设计 token 预算 / 召回策略）
- **inbox → ledger 自动写入** —— 例如"从 inbox 总结生成记忆"
- **LLM 主动从对话抽取记忆** —— 推断写入路径需要可解释、可撤销的 UI
- **向量检索 / embedding** —— 留 ADR-013 实施 + 后续 RAG ADR
- **兴趣画像权重 / 衰减** —— ADR-013 实施时通过旁表 `memory_entries_signals` + 简单可解释规则（不做黑盒推荐）
- **30 天物理清理** —— 见决策 M1
- **加密存储** —— V0 用 SQLite 默认明文（与 inbox / chat 一致）；用户机器物理安全前提下；ADR-007 提到"云同步、跨设备需要单独决策"，加密同理
- **导出 / 导入** —— V0 不提供 JSON / CSV 导出按钮
- **审计日志** —— V0 不写 ledger 操作流水（与 inbox / chat 一致）
- **跨会话搜索 / 全文搜索** —— V0 不引入 SQLite FTS5
- **批量操作** —— 不做批量软删 / 批量归档
- **多用户 / 权限隔离** —— 单用户单进程
- **CASCADE 子资源** —— 没有引用 memory_entries 的子表（兴趣旁表是未来事）

## 影响

**正向影响**：

- [PURPOSE.md `mvp_first_slice`](../../PURPOSE.md) Memory Ledger 项闭环：可查看、可编辑、可删除、可恢复；MVP 第一版接近全部完成
- DDD 分层骨架第三次验证（继 inbox / chat 之后）：相同的聚合根 + UUIDv7 + Repository + AppService + RESTful 端点对称性
- ADR-024 §I1 schema_version 机制经历第二次"非首次升级"路径（v3→v4），与 chat ADR 一致
- 软删 + status 状态机给后续聚合（如未来"任务"、"提醒"）做出可参考的形态；inbox 的 capture-only 模式不再是唯一范本
- `memory_entries_signals` 旁表设计让 ADR-013 兴趣画像实施时不需要破坏性 ALTER 主表
- 桌面 view-switcher 让"主视图平级切换"模式落地；后续若再加新功能区（如设置 / 帮助）有现成模板

**代价 / 风险**：

- 状态机有 4 档 + 5 条转移规则，单元测试覆盖矩阵较大（9 条合法 + 7 条非法 = 16 条用例起步）
- `deleted_at_utc` 列只有在 SoftDeleted 时有值，违反"列要么 NOT NULL 要么默认值"的强一致原则；接受此 trade-off 因为它语义清晰且只有一处使用
- PATCH 端点接受多个可选字段（content / scope / sensitivity / status），DTO 设计需要区分"未传"和"传 null"——V0 用 record + nullable + 全空跳过的写法解决（与 inbox 不同，inbox 没有 PATCH）
- view-switcher 切换两个主视图会让 renderer 总代码量再涨一倍；index.html 接近 1500 行；触发条件是"renderer 单文件超过 2000 行"则需要切分模块（开新 ADR 引入打包工具 / 模块化）
- 用户在 chat 中说"记下来"这件事 V0 做不到；这是已知 dogfood 摩擦点，但比"草率引入自动写入路径"风险小
- Memory 第一版没有搜索 / 排序 / 过滤之外的 UI 增强；如果 dogfood 出现"想找某条记忆但记不清内容"，会触发 `adr_revisit_when` 中的"搜索需求"
- 软删后的行永久保留，长期看磁盘占用线性增长；ADR-024 SQLite 默认大小估算下 1000 条 ledger ≈ 1MB，10000 条 ≈ 10MB，V0 不会成为问题；超过 100,000 条触发清理 ADR
- 32-bit 架构下 confidence REAL 与 weight REAL 的精度差异（IEEE 754 double）在简单可解释规则下不构成问题，但若未来上算法权重排序需要稳定排序，要在 ADR-013 实施时再确认

## 复议触发条件

`adr_revisit_when` 已写入 front matter（见 [SCHEMA §4.3.2 / §6.0](../../SCHEMA.md)），本节不重复。

## 实施概要

落地按 4 个 commit 切分，**每个 commit 都必须建立在前一个 commit 之上**：

### M1 — Domain + Application + Infrastructure（一个 commit）

```
feat(domain+application+infra): memory ledger v0 schema and storage per ADR-033
```

新建 / 修改：

- `src/Dawning.AgentOS.Domain/Memory/`：
  - `MemoryLedgerEntry.cs`（聚合根，9 个状态转移方法）
  - `MemorySource.cs / MemorySensitivity.cs / MemoryStatus.cs`
  - `IMemoryLedgerRepository.cs`
- `src/Dawning.AgentOS.Application/Memory/`：
  - `CreateMemoryEntryRequest.cs / UpdateMemoryEntryRequest.cs / MemoryEntryDto.cs / MemoryEntryListPage.cs / MemoryListQuery.cs`
  - `MemoryErrors.cs`（NotFoundCode / InvalidStatusTransitionCode）
- `src/Dawning.AgentOS.Application/Services/MemoryLedgerAppService.cs` + `Interfaces/IMemoryLedgerAppService.cs`
- `src/Dawning.AgentOS.Application/DependencyInjection/`：注册 AppService
- `src/Dawning.AgentOS.Infrastructure/Persistence/Memory/MemoryLedgerRepository.cs`
- `src/Dawning.AgentOS.Infrastructure/Persistence/Migrations/0004_create_memory_entries.sql`（嵌入资源）
- `src/Dawning.AgentOS.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`：注册 Repository

测试同 commit：

- `tests/Dawning.AgentOS.Domain.Tests/Memory/`：聚合 + 状态机 + UUIDv7 + 不可变字段保护（≥16 用例）
- `tests/Dawning.AgentOS.Application.Tests/Memory/`：5 个端点对应的 AppService 方法 × 错误码全集（≥18 用例）
- `tests/Dawning.AgentOS.Infrastructure.Tests/Persistence/Memory/`：Dapper Repo SQL 正确性 + 软删过滤 + 分页（≥10 用例）
- `tests/Dawning.AgentOS.Architecture.Tests/`：现有 LayeringTests 自动覆盖（无需新增）

### M2 — Api endpoints + 集成测试（一个 commit）

```
feat(services-api): memory ledger 5 endpoints per ADR-033
```

- `src/Dawning.AgentOS.Api/Endpoints/Memory/MemoryEndpoints.cs`（5 endpoints + manual error switch）
- `src/Dawning.AgentOS.Api/Program.cs`：调用 `MapMemoryEndpoints()`
- `tests/Dawning.AgentOS.Api.Tests/Endpoints/Memory/`：5 端点 × 2-3 用例（happy path + 错误）≥12 用例

### M3 — Desktop view-switcher + Memory pane UI（一个 commit）

```
feat(apps-desktop): memory pane and view-switcher per ADR-033
```

- `apps/desktop/src/preload.ts`：新增 `chat` 同款的 `memory` 块（6 方法）
- `apps/desktop/src/main.ts`：6 个 IPC handler
- `apps/desktop/src/renderer/index.html`：
  - header 加 view-switcher（两 button）
  - 现有 `.split` 包进 `#view-chat-inbox`
  - 新增 `#view-memory`：列表 + 详情面板 + 工具栏
  - JS：`memory` 模块（与 `chat` 模块同款 IIFE 风格）
- `apps/desktop/scripts/smoke.ts`：新增 `probeMemoryLifecycle`（POST → GET list → PATCH → DELETE → GET 应过滤）

### M4 — 文档与 changelog（如需要）

V0 阶段 changelog 由 `docs/log.md` 自动生成（[SCHEMA §8.7](../../SCHEMA.md)），不手维。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：本 ADR 对应的 Memory MVP 必含项与 L3 软删红线。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-007 记忆隐私与用户控制](memory-privacy-and-user-control.md)：记忆来源 / 隐私 / 推断标注红线。
- [ADR-011 Memory MVP 采用显式记忆账本](explicit-memory-ledger-mvp.md)：直接父 ADR；本 ADR 是其 V0 实施版。
- [ADR-013 兴趣画像采用权重与时间衰减](interest-profile-weighting-and-decay.md)：决议把兴趣字段留给该 ADR 实施时通过 `memory_entries_signals` 旁表加入。
- [ADR-014 MVP 第一版切片：聊天 + inbox + 读侧整理](mvp-first-slice-chat-inbox-read-side.md)：定义第一版必须包含 Memory Ledger。
- [ADR-021 后端架构 EquinoxProject 参照](backend-architecture-equinox-reference.md)：DDD 分层骨架；本 ADR 第三次复用。
- [ADR-024 SQLite + Dapper 引导与 Schema 初始化](sqlite-dapper-bootstrap-and-schema-init.md)：migration v4 沿用此机制。
- [ADR-026 Inbox V0 数据契约](inbox-v0-capture-and-list-contract.md)：聚合形态 / Repository 模式 / RESTful 端点对称源。
- [ADR-032 Chat V0 流式与持久化](chat-v0-streaming-and-persistence.md)：第二次复用 DDD 分层；migration v3 范本。
- [ADR-027 桌面渲染层 V0：原生 HTML + IPC bridge](desktop-renderer-v0-native-html-and-ipc-bridge.md)：renderer 不引入框架；view-switcher 用 hash route + 容器切换。
- [ADR-023 API 入口与 V0 端点](api-entry-facade-and-v0-endpoints.md)：5 端点的 RESTful 风格、错误码 / HTTP 映射依据。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 是该规则的产物。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
