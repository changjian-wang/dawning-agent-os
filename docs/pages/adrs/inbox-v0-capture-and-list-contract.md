---
title: ADR-026 Inbox V0 数据契约与捕获面：聚合形态、表结构、UUIDv7 主键与列表分页
type: adr
subtype: architecture
canonical: true
summary: V0 inbox 落地为单聚合 InboxItem（content / source / captured_at_utc / created_at_utc），主键采用 UUIDv7（替代 ULID 但保留时间排序属性），表 inbox_items 通过 schema migration 0002 建立；AppService IInboxAppService 暴露 CaptureAsync / ListAsync 两个用例；Api 暴露 POST /api/inbox 与 GET /api/inbox（limit+offset 分页，默认 limit=50 / max 200）；动作仍走 X-Startup-Token，不引入用户身份；InboxItemCaptured domain event 由聚合 raise 但 V0 不接 dispatcher（ADR-022 §10 待办仍未关闭）。
tags: [agent, engineering]
sources: []
created: 2026-05-02
updated: 2026-05-02
verified_at: 2026-05-02
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/mvp-input-boundary-no-default-folder-reading.md, pages/adrs/important-action-levels-and-confirmation.md, pages/adrs/sqlite-dapper-bootstrap-and-schema-init.md, pages/adrs/no-mediator-self-domain-event-dispatcher.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/desktop-process-supervisor-electron-dotnet-child.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-02
adr_revisit_when: "用户身份 / 多账户进入产品（G2 失效）；inbox 表行数 ≥ 100k 或单条 content 出现 ≥ 64KB（A2 字段上限失效）；列表查询出现 ≥ 3 个过滤条件（标签 / 时间范围 / 全文）使 limit+offset 不再够用，需要 cursor 分页；AppService 出现真实跨聚合编排需要 InboxItemCaptured 的 handler，触发 ADR-022 §10 的 dispatcher 实现；inbox 上线小批量文件上传 / 链接展开等扩展输入路径，要求新的字段或子表；UUIDv7 在 .NET BCL 出现破坏性变更（极不可能）；或 SQLite 的 TEXT 主键性能 / 排序与 32 字节 BLOB 形态出现可量化差距。"
---

# ADR-026 Inbox V0 数据契约与捕获面：聚合形态、表结构、UUIDv7 主键与列表分页

> V0 inbox 落地为单聚合 InboxItem（content / source / captured_at_utc / created_at_utc），主键采用 UUIDv7（替代 ULID 但保留时间排序属性），表 inbox_items 通过 schema migration 0002 建立；AppService IInboxAppService 暴露 CaptureAsync / ListAsync 两个用例；Api 暴露 POST /api/inbox 与 GET /api/inbox（limit+offset 分页，默认 limit=50 / max 200）；动作仍走 X-Startup-Token，不引入用户身份；InboxItemCaptured domain event 由聚合 raise 但 V0 不接 dispatcher（ADR-022 §10 待办仍未关闭）。

## 背景

[ADR-014](mvp-first-slice-chat-inbox-read-side.md) 把 MVP 第一版形态钉死为「聊天窗口 + agent inbox + 读侧整理」，但只锁了产品形态，没锁数据契约：inbox 这张表/聚合长什么样、字段范围、主键策略、排序规则、API 形状全都未定义。[ADR-024](sqlite-dapper-bootstrap-and-schema-init.md) 把持久化骨架打通，留下 `__schema_version` 通道与 `Persistence/Migrations/NNNN_*.sql` 嵌入资源约定，但 V0 阶段刻意把 Dapper、aggregate repository、`IUnitOfWork` 全部后置（F1/G1），明确说"等第一个 aggregate 实现时再回头"。[ADR-022](no-mediator-self-domain-event-dispatcher.md) 把 Application 层定型为 `IXxxAppService` 立面 + 自研 `IDomainEventDispatcher`，但 dispatcher 实现一直挂在 §10 待办未关。[ADR-025](desktop-process-supervisor-electron-dotnet-child.md) 把桌面进程监督打通，让 smoke 探针能调一个真实 endpoint 验证全栈通电——目前 smoke 只调 `/api/runtime/status`，没有任何"写一条数据再读出来"的真实业务路径。

S6 进入实施前缺以下问题需要锁死：

- `InboxItem` 聚合的字段集合、长度上限、可空性。
- 主键策略（GUID v4 / UUIDv7 / ULID / 自增 INTEGER）。
- `inbox_items` 表的列定义、约束、索引。
- 列表分页方式（offset / cursor / 无分页）与排序规则。
- 去重策略（无 / 软去重日志 / 强 hash 去重）。
- domain event 的定义与 V0 是否真接 dispatcher。
- `IInboxRepository` 端口的归属层（Domain / Application/Abstractions）。
- AppService 用例切面：单个 `CaptureAsync` 还是 `Capture + List` 两个？
- API 端点形态：单一 `/api/inbox` 还是分 `/api/inbox/items`？是否需要 GET by id？
- 鉴权与用户身份：V0 是否需要引入 user 概念？
- 与 [ADR-004 重要性级别与确认机制](important-action-levels-and-confirmation.md) 的关系：`inbox.add` 是 L1 动作，但 V0 是否需要按 L1 走二次确认？

如果把这些问题留到代码阶段「边写边定」，就会重蹈 ADR-021 → ADR-022 / ADR-024 那种「实施期间反复改方向」的覆辙。本 ADR 在 ADR-014 / ADR-022 / ADR-024 / ADR-025 既有契约之上把 inbox V0 的形态钉死，作为 S6 实施的依据；按 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)，代码落地仍须在本 ADR 接受后才进行。

## 备选方案

字段范围（A 轴）：

- **A1** 最小集：`id`、`content`、`created_at`。
- **A2** A1 + `source`（来源标记，区分聊天捕获 / 文件捕获 / 主动粘贴）+ `captured_at_utc`（区分"创建时间"与"实际捕获时间"以适配未来后台批量导入）。
- **A3** A2 + `tags TEXT[]`（标签数组）+ `summary TEXT`（agent 生成摘要）+ `category TEXT`。

主键形态（B 轴）：

- **B1** GUID v4（`Guid.NewGuid()`）。完全随机，不可时间排序。
- **B2** ULID（26 字符 Crockford base32 字符串）。需要引入 `Ulid` NuGet（Cysharp 系或等价包）。时间排序友好。
- **B2'** UUIDv7（`Guid.CreateVersion7()`，.NET 9+/10）。与 ULID 等价的时间排序方案，BCL 原生提供，36 字符 canonical Guid 格式存储。
- **B3** 自增 INTEGER。最快插入，但不利于多机同步（虽然 V0 单机），且暴露行数语义。

列表分页（C 轴）：

- **C1** 一次返回全部行（无分页）。
- **C2** `limit + offset`（默认 limit=50，硬上限 200）。客户端简单，但深度翻页性能下降。
- **C3** Cursor based（`captured_at_utc + id` 复合游标）。客户端复杂，深度翻页稳定。

排序规则（D 轴）：

- **D1** 按 `captured_at_utc DESC, id DESC`（最新优先；id desc 作 tiebreaker 保证同毫秒内的稳定顺序）。
- **D2** 按 `created_at_utc DESC`（创建时间）。

去重策略（E 轴）：

- **E1** 不去重，由用户负责重复入箱的代价。
- **E2** AppService 在写前做"同 content + 同分钟"的软检测：发现重复时仍写入但写日志，告知未来 dedupe 可能在 UI 层。
- **E3** 数据库层强一致：`UNIQUE (content_hash, captured_at_utc_minute)`。

Domain event 与 dispatcher（F 轴）：

- **F1** 聚合 raise `InboxItemCaptured` 事件，AppService 写后通过日志记录"已 raise N 个事件"，然后 `ClearDomainEvents()`，**不接 dispatcher**。`IDomainEventDispatcher` 实现仍延续 ADR-022 §10 的 todo 形态。
- **F2** 不定义事件，等到真有 handler 再补。
- **F3** 一并实现自研 `DomainEventDispatcher` + DI 注册 + AppService 调度，关闭 ADR-022 §10 todo。

`IInboxRepository` 归属层（H 轴）：

- **H1** 放 `Dawning.AgentOS.Domain/Inbox/IInboxRepository.cs`：DDD 经典做法，端口 talks in domain types（`InboxItem`），由 Infrastructure 实现。
- **H2** 放 `Dawning.AgentOS.Application/Abstractions/Persistence/IInboxRepository.cs`：与 ADR-024 已落地的 `IDbConnectionFactory` / `ISchemaInitializer` 同框。
- **H3** 不定义 Repository 端口，AppService 直接注入 `IDbConnectionFactory` + Dapper 写 SQL。

API 端点切面（I 轴）：

- **I1** `POST /api/inbox`（capture）+ `GET /api/inbox`（list）。两个端点。
- **I2** I1 + `GET /api/inbox/{id}`（detail）+ `DELETE /api/inbox/{id}`。完整 CRUD。
- **I3** 单 `POST /api/inbox/commands`（命令模式 + body 区分 capture / list / delete）。

鉴权（J 轴）：

- **J1** 复用 [ADR-025](desktop-process-supervisor-electron-dotnet-child.md) 启动 token；引入 user 概念。
- **J2** J1 不变，**不**引入 user 概念，但保留 `created_by` 列空着以备未来扩展。
- **J3** 立刻引入最小 user model（即便 V0 单用户）。

ADR-004 动作分级是否走二次确认（K 轴）：

- **K1** V0 不走二次确认；`inbox.add` 在 V0 不算"破坏性"，且 dogfood 阶段强制每条都确认会让 inbox 失去当容器的便利。
- **K2** 走 ADR-004 L1 二次确认流程。

## 被否决方案与理由

**A1 最小集**：

- `created_at` 单字段无法表达"过去某个时间发生但今天才被 agent 捕获"——例如未来扩展会话沉淀 / 后台批量导入时，会需要显式区分聚合诞生时刻和材料原始时间。先留 `captured_at_utc` 比未来再做迁移便宜。
- 没有 `source` 字段会让 dogfood 阶段无法基于"哪些路径吃进来的"做诊断，必须靠日志反查。

**A3 加 tags / summary / category**：

- `tags`、`summary`、`category` 都是 agent 处理后的派生信息，应进入读侧整理（ADR-014 第 3 条 "动作范围 = 读侧整理"）的产物表，而不是 inbox 原始表。把它们塞进 inbox 表会让该表既要承载"未处理材料"又要承载"已处理结果"，违反单一职责。
- agent 生成 summary 是一个有 LLM 调用的副作用，把它放在 capture 路径会让"丢一条材料进 inbox"的延迟从毫秒变成秒。

**B1 GUID v4**：

- 完全随机意味着 SQLite B-tree 主键插入会引发 page split，写放大；虽然 V0 量小，但同样的代价用 UUIDv7 可以零额外代价规避。
- 不可时间排序意味着 D1 的 `captured_at_utc DESC` 必须依赖时间索引，无法降级到主键扫描。

**B2 ULID（要引入 NuGet）**：

- ULID 与 UUIDv7 在用户可观察的语义上完全等价（128 位、毫秒时间戳前缀、剩余随机），但 ULID 需要引入第三方 NuGet（Cysharp.Ulid 或同类），与 ADR-022 / ADR-024 一直保持的"BCL-first，依赖最少"取向冲突。
- 26 字符 Crockford base32 形式相比 36 字符 canonical Guid 字符串只省 10 字节存储，对 V0 inbox（千行级）无可观察收益。

**B3 自增 INTEGER**：

- 暴露表行数语义：客户端可以通过最大 id 推算系统活跃度。
- 未来若将 inbox 数据迁移到云后端或多设备同步，整数 id 在跨节点合并时容易冲突；UUIDv7 / ULID 天然分布式。

**C1 无分页**：

- inbox 是材料长期累积容器，dogfood 几周后行数过千是正常情况；不分页会让 UI 长列表加载阻塞。

**C3 Cursor 分页**：

- V0 客户端是桌面单进程渲染，深度翻页 / 长会话场景不是 dogfood 必经路径；C2 的 limit+offset 在 ≤ 1000 行时性能完全够。
- Cursor 客户端实现复杂度（要维护 cursor 状态、处理边界条件、设计 cursor 编码格式）远高于 C2，超出 V0 必要范围。
- 当行数 ≥ 100k 或出现复合过滤需求时（见 `adr_revisit_when`）再升级到 C3。

**D2 按 created_at**：

- 与 D1 的差异只在"未来批量导入历史材料"时显现：D1 让"刚捕获的旧材料"排在最上，符合 inbox 作为待处理容器的语义；D2 会让旧材料默认沉底。

**E2 / E3 去重**：

- E2 软去重要求 AppService 在写前先 query，多了一次 round trip，且把"内容相同"判定逻辑分散到 AppService 与未来 UI 两处。
- E3 强一致 hash 在 V0 阶段没有清晰收益：用户主动粘贴重复内容是合法行为（"我就是想多丢一条"），数据库层硬拒会让 UX 反馈陡峭。
- dedupe 的合适落地点是未来读侧整理 agent，让 agent 提示"这条似乎与 #abc 重复，是否合并"——这是 ADR-014 已规划的 read-tag 类动作。

**F2 不定义事件**：

- 失去 ADR-022 已埋好的事件埋点，未来真要接 handler（例如 audit log、读侧整理触发器）时要回来改 Domain，违反 SRP。
- raise + clear 的开销在 V0 是零（list 始终为空 → forall 立刻返回），代价为零。

**F3 一并实现 dispatcher**：

- 把 dispatcher 实现塞进 ADR-026 会让一个 ADR 同时承载两个不相关决策（inbox 数据契约 + 跨聚合事件分发机制），后续 supersede 时拆解困难。
- ADR-022 §10 todo 关闭应是独立 ADR，至少包含：handler 注册形式、handler 异常隔离策略、async / sync 调度选择、单元测试用 stub 还是真实容器——这些都是与 inbox 无关的横切决策。
- F1 的"raise + log + clear"形态足以为未来 F3 留接口：聚合的事件队列 API（`DomainEvents` / `ClearDomainEvents()`）已在 `Domain.Core/AggregateRoot.cs` 落地，不需要 inbox 这边再做任何额外暴露。

**H2 / H3 仓储归属**：

- H2 把 aggregate repository 与 ADR-024 的 infra-level ports（`IDbConnectionFactory`、`ISchemaInitializer`、`IAppDataPathProvider`）混放，前者在 talk in domain types，后者在 talk in `DbConnection` / `string`，语义错位会让 `Application/Abstractions/Persistence/` 失去"infra port 集合"的身份。
- H3 把 SQL 字符串散布到 AppService 体内，破坏 Domain ↔ Infrastructure 的抽象层；且未来要替换 ORM（如从 Dapper 切到 Marten）时迁移面跨多个 AppService。
- H1 是 Eric Evans 原书 / Vaughn Vernon 红皮书 / Equinox 三处一致的做法，且与 `IInboxRepository` 在 V0 唯一的实现 `Dawning.AgentOS.Infrastructure.Persistence.Inbox.InboxRepository` 形成 1:1 端口/适配器。

**I2 完整 CRUD / I3 命令模式**：

- I2 的 GET by id 在 V0 没有客户端使用方（详情面板 V0 不存在），DELETE 也没有 UX 入口；提前实现等于 dead code。
- I3 命令模式属于 RPC over HTTP，违反 [ADR-023 §2](api-entry-facade-and-v0-endpoints.md) 的 RESTful + Map<Feature>Endpoints 立面约定。

**J1 / J3 立刻引入 user**：

- V0 是单用户桌面应用，user 概念会引入额外迁移、外键、scoping 谓词，且没有任何客户端能消费这层信息（桌面壳只有一个登录态）。
- 鉴权层 ADR-025 的 startup token 在 V0 已经把"从渲染进程 → API"的访问授权解决；引入 user 等于多做一层在 V0 没人受益的工作。

**J2 留 created_by 字段空着**：

- 字段空着不写值，未来要回填时仍要做 migration；与"V0 不引入"在迁移成本上等价但代码更脏。直接不加，未来 ADR 触发时一并 ALTER TABLE。

**K2 二次确认**：

- ADR-004 把 `inbox.add` 划到 L1（无副作用 / 可逆 / 用户主动发起），V0 桌面 dogfood 期强制每条都弹"是否确认入箱"会让 inbox 反向变成阻塞。
- ADR-014 第 4 条已说"不对每条普通记忆都强制弹窗确认"；inbox capture 比 memory write 更轻，自然不应抬到比 memory write 还高的确认门槛。

## 决策

采用 A2 + B2'（UUIDv7）+ C2 + D1 + E1 + F1 + H1 + I1 + J2 + K1 的组合。

### 1. 聚合 InboxItem（Domain 层）

`src/Dawning.AgentOS.Domain/Inbox/InboxItem.cs` 是 `AggregateRoot<Guid>` 的子类，字段：

| 字段 | 类型 | 来源 | 约束 |
|---|---|---|---|
| `Id` | `Guid` | 来自 `AggregateRoot<Guid>` 基类，由 `Capture()` 工厂用 `Guid.CreateVersion7()` 生成 | 非 `Guid.Empty`，由基类校验 |
| `Content` | `string` | 调用方 | 非空、非空白、长度 ≤ 4096 字符（按 `string.Length` 计 UTF-16 code units） |
| `Source` | `string?` | 调用方 | 可空；若提供则非空白、长度 ≤ 64 字符 |
| `CapturedAtUtc` | `DateTimeOffset` | 调用方传入（一般来自 `IClock.UtcNow`） | UTC（offset = `TimeSpan.Zero`） |
| `CreatedAt` | `DateTimeOffset` | 来自 `Entity<TId>` 基类 | 与 `CapturedAtUtc` 同值（V0 阶段二者重合，预留分裂空间） |

工厂方法签名：

```csharp
public static InboxItem Capture(string content, string? source, DateTimeOffset capturedAtUtc);
```

工厂职责：
1. 校验输入（content 非空 / 长度上限；source 非空白 / 长度上限）；违反抛 `ArgumentException`，**不**返回 `Result<T>`——按 ADR-022 / `Domain.Core` 的现有约定，invariant 违反是 programming error，业务校验交给 AppService 用 `Result<T>` 返回。
2. 生成 UUIDv7：`Guid.CreateVersion7(capturedAtUtc)` 让聚合 id 与捕获时间锚定。
3. 调用基类构造器，传入 `id` 与 `createdAt = capturedAtUtc`。
4. raise `InboxItemCaptured`（见 §3）。

### 2. 仓储端口 IInboxRepository（Domain 层）

`src/Dawning.AgentOS.Domain/Inbox/IInboxRepository.cs`：

```csharp
public interface IInboxRepository
{
    Task AddAsync(InboxItem item, CancellationToken cancellationToken);

    Task<IReadOnlyList<InboxItem>> ListAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<long> CountAsync(CancellationToken cancellationToken);
}
```

约束：
- 端口 talks in `InboxItem`，不暴露 `DbConnection` / Dapper / SQL 字符串。
- `ListAsync` 实现按 D1 排序：`ORDER BY captured_at_utc DESC, id DESC`。
- `CountAsync` 用于 list 响应里的 `total`，让客户端知道是否还有下一页（ADR-024 §K1 已禁止 Application 直接 query SQL，本端口正是绕开此约束的合规通道）。

### 3. Domain event InboxItemCaptured

`src/Dawning.AgentOS.Domain/Inbox/InboxItemCaptured.cs`：

```csharp
public sealed record InboxItemCaptured(
    Guid InboxItemId,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset OccurredOn) : IDomainEvent;
```

V0 没有 handler 订阅；`InboxAppService` 在写后通过 `ILogger` 记录"已 raise N 个事件"然后调 `aggregate.ClearDomainEvents()`。dispatcher 实现仍属 ADR-022 §10 待办，本 ADR 不一并关闭。

### 4. SQLite 表 inbox_items

迁移文件 `src/Dawning.AgentOS.Infrastructure/Persistence/Migrations/0002_create_inbox_items.sql`：

```sql
CREATE TABLE inbox_items (
    id              TEXT NOT NULL PRIMARY KEY,
    content         TEXT NOT NULL,
    source          TEXT NULL,
    captured_at_utc TEXT NOT NULL,
    created_at_utc  TEXT NOT NULL
);

CREATE INDEX ix_inbox_items_captured_at
    ON inbox_items (captured_at_utc DESC, id DESC);
```

存储约定：
- `id`: 36 字符 canonical Guid 字符串（`xxxxxxxx-xxxx-7xxx-yxxx-xxxxxxxxxxxx`），UUIDv7 版本位为 `7`。
- `captured_at_utc` / `created_at_utc`: ISO-8601 round-trip 字符串（与 `__schema_version.applied_at` 同格式），永远 `Z` 结尾。
- `source`: 允许 NULL；非 NULL 时已由聚合保证非空白且 ≤ 64 字符。
- 不加 `CHECK` 约束：长度上限由聚合保证（domain primary），SQLite 层重复校验只增维护负担。
- 索引方向与 D1 排序一致，让 `ORDER BY captured_at_utc DESC, id DESC LIMIT ? OFFSET ?` 走索引扫描而非排序。

### 5. 仓储实现 InboxRepository（Infrastructure 层）

`src/Dawning.AgentOS.Infrastructure/Persistence/Inbox/InboxRepository.cs` 用 Dapper 实现 `IInboxRepository`。本 ADR 同时落地 ADR-024 §F1 / §G1 中说的"等第一个 Aggregate Repository 时再引入 Dapper"——`Dapper` NuGet 包入 `Dawning.AgentOS.Infrastructure.csproj`（不入 Application / Domain / Api）。架构测试 `Application_DoesNotReferencePersistencePackages` / `Api_DoesNotReferencePersistenceOrORMPackages` 已经在禁止 Dapper 出现在错误层，本 ADR 不放松。

V0 实现策略：
- `AddAsync`：单条 INSERT，用命名参数（`@id`, `@content`, `@source`, `@capturedAtUtc`, `@createdAtUtc`）。
- `ListAsync`：`SELECT ... FROM inbox_items ORDER BY captured_at_utc DESC, id DESC LIMIT @limit OFFSET @offset`。`InboxItem` 通过 `InboxItem.Rehydrate(...)` 静态工厂从行数据重建，**不 raise 事件**（rehydrate 是技术加载，不是业务行为）。
- `CountAsync`：`SELECT COUNT(*) FROM inbox_items`。

### 6. AppService IInboxAppService（Application 层）

`src/Dawning.AgentOS.Application/Interfaces/IInboxAppService.cs`：

```csharp
public interface IInboxAppService
{
    Task<Result<InboxItemSnapshot>> CaptureAsync(
        CaptureInboxItemRequest request,
        CancellationToken cancellationToken);

    Task<Result<InboxListPage>> ListAsync(
        InboxListQuery query,
        CancellationToken cancellationToken);
}
```

DTO 约定（`src/Dawning.AgentOS.Application/Inbox/`）：

- `CaptureInboxItemRequest(string Content, string? Source)`：纯输入；`CapturedAtUtc` 由 AppService 用 `IClock.UtcNow` 自己补，不接受客户端传时间。
- `InboxItemSnapshot(Guid Id, string Content, string? Source, DateTimeOffset CapturedAtUtc, DateTimeOffset CreatedAt)`：单条输出。
- `InboxListQuery(int Limit, int Offset)`：list 输入。AppService 校验 `limit ∈ [1, 200]` 与 `offset ≥ 0`，违规返回 `Result.Failure` with field-level errors（→ HTTP 400）。
- `InboxListPage(IReadOnlyList<InboxItemSnapshot> Items, long Total, int Limit, int Offset)`：list 输出。

`InboxAppService` 实现职责：

1. `CaptureAsync`：
   - 校验请求 DTO；空白 content 返回 `Result<InboxItemSnapshot>.Failure("content.required", "...", field: "content")`。
   - 业务校验 OK 后调 `InboxItem.Capture(...)`；invariant 违反（聚合层抛 `ArgumentException`）按程序错误传播，不被 AppService 转 `Result.Failure`。
   - `await _repository.AddAsync(item, cancellationToken)`。
   - 取出 `item.DomainEvents`，`_logger.LogDebug("inbox capture raised {Count} domain events: {Events}", ...)`，然后 `item.ClearDomainEvents()`。
   - 返回 `Result<InboxItemSnapshot>.Success(snapshot)`。
2. `ListAsync`：
   - 校验 `query.Limit ∈ [1, 200]`、`query.Offset ≥ 0`；越界返回 field-level Failure。
   - 并发执行 `_repository.ListAsync(...)` 与 `_repository.CountAsync(...)`（两次独立 connection 即可，V0 无并发写）。
   - 组装 `InboxListPage` 并返回 `Result<InboxListPage>.Success(...)`。

注：repository 注入按 `Dawning.AgentOS.Application.DependencyInjection.AddApplication()` 现有的"`I*AppService` → `*AppService` 自动注册"已经覆盖；`IInboxRepository` 实现 → 接口的注册由 `AddInfrastructure()` 显式 `services.AddScoped<IInboxRepository, InboxRepository>()` 完成。

### 7. API 端点 InboxEndpoints（Api 层）

`src/Dawning.AgentOS.Api/Endpoints/Inbox/InboxEndpoints.cs` 是静态类，扩展方法 `MapInboxEndpoints`：

```text
POST /api/inbox        body: { content: string, source: string? }
                       response: 200 { id, content, source, capturedAtUtc, createdAt }
                                 400 ProblemDetails 校验错（content 必填）
                                 422 ProblemDetails 业务规则错（V0 暂未触发）
                                 401 ProblemDetails 缺/错 token（中间件）

GET  /api/inbox?limit=50&offset=0
                       response: 200 { items: [...], total, limit, offset }
                                 400 ProblemDetails 参数越界
                                 401 ProblemDetails 缺/错 token（中间件）
```

约束：
- 路由写 `/api/inbox`，不写 `/api/inbox/items`。
- 所有错误转换走 `ResultHttpExtensions.ToHttpResult` 的现成机制（ADR-023 §4）。
- 不返回 `total` 之外的分页元信息（如 `nextOffset` / `hasMore`）；客户端用 `offset + items.Count < total` 自行判定。

### 8. 鉴权与用户身份

V0 沿用 [ADR-025](desktop-process-supervisor-electron-dotnet-child.md) 的 startup token；`X-Startup-Token` 中间件保护 `/api/inbox` 与 `/api/runtime` 一视同仁，不引入 user / tenant / scope 概念。表里**不**预留 `created_by` 列（见 J2 否决）。

### 9. 测试覆盖范围

- `Dawning.AgentOS.Domain.Tests/Inbox/InboxItemTests.cs`：聚合工厂校验（content 空 / 过长 / source 长度 / `Guid.Empty` 不可能、id v7 版本位 = 7、event raise 次数 = 1、`Rehydrate` 不 raise）。
- `Dawning.AgentOS.Application.Tests/Services/InboxAppServiceTests.cs`：成功路径、空 content 校验、limit/offset 越界、repository 抛异常时不被 AppService 吞掉（直接传播为 5xx）。
- `Dawning.AgentOS.Infrastructure.Tests/Persistence/Inbox/InboxRepositoryTests.cs`：用 in-memory shared SQLite 跑 add → list → count → list with offset。
- `Dawning.AgentOS.Api.Tests/Endpoints/Inbox/InboxEndpointsTests.cs`：HTTP 集成测试（POST 正常 / POST 缺 token 401 / GET 正常 / GET limit=0 → 400）。
- `Dawning.AgentOS.Architecture.Tests/LayeringTests.cs` 增量：
  - `Domain_InboxAggregate_LivesUnderInboxNamespace`（pin 命名空间）。
  - 不放松任何已有禁止规则；Dapper 仍只允许出现在 Infrastructure。

### 10. 升级 smoke probe

`apps/desktop/scripts/smoke.ts` 在现有 runtime status 探针后追加：

```ts
// POST /api/inbox
const captureResponse = await fetch(`${baseUrl}/api/inbox`, {
  method: "POST",
  headers: { "Content-Type": "application/json", "X-Startup-Token": token },
  body: JSON.stringify({ content: `smoke probe @ ${new Date().toISOString()}`, source: "smoke" }),
});
// expect 200 + body.id matches UUIDv7 regex

// GET /api/inbox
const listResponse = await fetch(`${baseUrl}/api/inbox?limit=10&offset=0`, {
  headers: { "X-Startup-Token": token },
});
// expect 200, body.total >= 1, body.items[0].id === capture.id
```

Smoke 仍要求 PASS 才算 ADR-026 实施完成，对齐 ADR-025 §7 的 K1 约定。

## 影响

正向：
- inbox V0 数据契约一次定型，避免 dogfood 期反复迁移。
- 选择 UUIDv7 让"主键 = 时间索引前缀"的好处落到桌面单文件 SQLite 上，零额外依赖。
- AppService / Repository / Migration / Endpoint 四层一一对应，未来加 `tags` / `summary` 字段时只动 migration + DTO，不动其他层。
- smoke probe 从"调 status"升级到"写一条数据再读出来"，是 ADR-025 进入到 ADR-014 第一刀第一切片的真实承接。
- 为 ADR-022 §10（dispatcher 实现）保留了第一个真实事件源（`InboxItemCaptured`），未来该 ADR 落地时可以立刻挂第一个 handler 验证。

代价：
- Domain 层第一次出现"talk in domain types 的 repository 端口"，Architecture.Tests 要新增对 `IInboxRepository` 命名空间的 pin。
- Infrastructure 第一次引入 `Dapper` NuGet；中央包目录 `Directory.Packages.props` 增条 `<PackageVersion Include="Dapper" />`。
- `IDomainEventDispatcher` 仍未落地，会让本 ADR 看起来"差最后一公里"——但合并后 ADR-022 §10 todo 仍可独立推进，形态不变。
- limit+offset 在未来行数 ≥ 100k 时会成为瓶颈，需要按 `adr_revisit_when` 走 cursor 分页升级路径。

复议：
- 见 front matter `adr_revisit_when`。

## 复议触发条件

`adr_revisit_when` 已写入 front matter（见 SCHEMA §4.3.2 / §6.0），本节不重复。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：本 ADR 的产品契约依据。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-014 MVP 第一版切片：聊天 + inbox + 读侧整理](mvp-first-slice-chat-inbox-read-side.md)：定义 inbox 在 MVP 中的产品形态。
- [ADR-012 MVP 输入边界：不默认读取用户文件夹](mvp-input-boundary-no-default-folder-reading.md)：约束 V0 输入路径，与本 ADR 的 source 字段语义对齐。
- [ADR-004 重要性级别与确认机制](important-action-levels-and-confirmation.md)：`inbox.add` 的 L1 分级依据；K1 决策对其的解释见 §K。
- [ADR-024 SQLite/Dapper 通电](sqlite-dapper-bootstrap-and-schema-init.md)：本 ADR 第一次消费 ADR-024 §F1 / §G1 的"等第一个 aggregate repository 落 Dapper"约定。
- [ADR-022 去 MediatR：自研领域事件分发器与 AppService 立面](no-mediator-self-domain-event-dispatcher.md)：F1 决策依赖 ADR-022 §10 待办尚未关闭这一事实。
- [ADR-023 Api 入口立面](api-entry-facade-and-v0-endpoints.md)：endpoint 形态与 ResultHttpExtensions 的复用依据。
- [ADR-025 桌面进程监督](desktop-process-supervisor-electron-dotnet-child.md)：smoke probe 升级路径与 startup token 鉴权来源。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 的合规依据。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
