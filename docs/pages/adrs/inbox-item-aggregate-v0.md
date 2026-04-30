---
title: ADR-021 第一聚合外形：InboxItem V0
type: adr
subtype: architecture
canonical: true
summary: V0 第一个业务聚合 InboxItem 外形锁定：强类型 InboxItemId、四态状态机（Pending → Tagged → Discarded | Promoted）、Result 模式错误流、四个领域事件、不感知 ActionLevel / ActionKind、暂不引入并发版本号。
tags: [memory, agent]
sources: []
created: 2026-04-30
updated: 2026-04-30
verified_at: 2026-04-30
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/important-action-levels-and-confirmation.md, pages/adrs/backend-architecture-equinox-reference.md, pages/adrs/architecture-test-assertion-strategy.md, pages/adrs/testing-stack-nunit-v0.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-30
adr_revisit_when: "Tag 上限 32 在 dogfood 中频繁被触达；或需要除 Discarded / Promoted 之外的第三种终态（如 Archived 待复看）；或 Promote 需要承载多目标（不仅是 Memory，还可能流向 Skill / 兴趣画像）；或 S3c 持久化时必须引入并发版本号；或聚合需要承载操作者身份审计信息；或 InboxItem 与其它聚合开始出现强引用导致跨聚合一致性诉求。"
---

# ADR-021 第一聚合外形：InboxItem V0

> V0 第一个业务聚合 InboxItem 外形锁定：强类型 InboxItemId、四态状态机（Pending → Tagged → Discarded | Promoted）、Result 模式错误流、四个领域事件、不感知 ActionLevel / ActionKind、暂不引入并发版本号。

## 背景

[ADR-014](mvp-first-slice-chat-inbox-read-side.md) 已把 MVP 第一刀切片定为"聊天 + agent inbox + 读侧整理"，并把 inbox 的角色明确为"用户主动投喂的待整理材料容器"——文本、链接、摘录、会话沉淀。但 ADR-014 是产品边界 ADR，并不规定这个 inbox 在领域代码里长什么样。

[ADR-018](backend-architecture-equinox-reference.md) 已选定 DDD 分层 + Result 模式 + MediatR 的工程骨架，[ADR-019](testing-stack-nunit-v0.md) 已选定 NUnit 作为测试栈，S1 / S2 已落地 `Domain.Core`（Entity / AggregateRoot / Result / DomainError / IDomainEvent）和 `Domain` 中的权限词表与值对象。但是 `Domain` 项目里**还没有任何业务聚合**——这有两个直接后果：

- [ADR-020](architecture-test-assertion-strategy.md) 把"必须引用上游层"这一类正向 layering 断言推迟到该层有具体类型绑定后再补，目前 Domain → Domain.Core 的正向断言因 Roslyn metadata pruning 仍处于"声明但运行期看不到"的状态。
- Memory Ledger 论旨（[ADR-014](mvp-first-slice-chat-inbox-read-side.md) 与长期记忆相关 ADR）的"分类仪式"只在文档里存在，没有任何聚合用代码表达"一条材料从 Pending 走到 Tagged 再走到 Promoted / Discarded"这个核心生命周期。

下一刀是把第一个真业务聚合落地。但聚合的几个外形决策一旦写进代码，迁移成本远大于权限词表那种纯值对象——ID 类型、状态机、错误模型、事件外形如果先写后改，会牵动后续 Application handler、持久化 schema、订阅者签名等多个层级。需要在写代码之前把这几条钉死。

[ADR-004](important-action-levels-and-confirmation.md) 已把 agent 动作分为 L0 / L1 / L2 / L3，S2 的 ActionKind 词表已把 `inbox.add` 标为 L1、`memory.write` 标为 L2、`memory.delete` 标为 L3。这构成本 ADR 的一个边界条件：聚合内部的状态转换不应直接感知 ActionLevel——分类是 `Domain.Services` 的事，聚合只发事件、由上层决定每个事件触发哪一级动作的确认流。

## 备选方案

**ID 类型**：

- 方案 A1：`Guid` 直接作 ID。
- 方案 A2：`readonly record struct InboxItemId(Guid Value)` 强类型 ID，包装 `Guid`。
- 方案 A3：数据库 `long` 自增作 ID。

**状态机**：

- 方案 B1：四态 `Pending → Tagged → Discarded | Promoted`，`Pending → Discarded` 也允许，`Discarded / Promoted` 终态不可再转。
- 方案 B2：二态 `Active / Discarded`，标签和"是否已转入记忆"用其它字段表达。
- 方案 B3：无状态字段，只用 tags 表达"是否已分类"，"是否已转入记忆"通过下游聚合存在来推断。

**Promote 前置条件**：

- 方案 C1：必须先 `Tag` 再 `Promote`，强制两步。
- 方案 C2：`Pending` 直接可 `Promote`，跳过分类步骤。
- 方案 C3：`Promote` 接收 `tags` 参数，把分类合并进 promote 一次完成。

**事件 payload**：

- 方案 D1：`InboxItemTagged` 携带全量 tags（可独立重放）。
- 方案 D2：`InboxItemTagged` 携带增量 tag delta（add / remove）。
- 方案 D3：V0 不发事件，等到 S3b（Application + MediatR）接入再补。

**并发控制**：

- 方案 E1：V0 不带并发版本号，等 S3c 持久化层再决定乐观并发模型。
- 方案 E2：V0 就在聚合上加 `int Version` / `byte[] RowVersion`。

## 被否决方案与理由

**A1 / A3 非强类型 ID**：

- `Guid` 直作 ID 在编译期没有任何类型保护：`InboxItem` 的 ID 与未来 `MemoryNote` 的 ID 都是 `Guid`，参数顺序写错、跨聚合 ID 误用都不会被编译器发现。
- `long` 自增 ID 在桌面 SQLite + 单写者场景没有可解决的问题，反而把 ID 生成绑死在数据库回写时机上，跨聚合事件 payload 必须等 round-trip 才能拿到 ID，与 V0 偏好的"业务方法立即返回 raised event"心智冲突。
- 方案 A2 强类型 ID 的额外成本只在 JSON 序列化与 EF / Dapper 转换上；S3c 持久化时再加 type handler，V0 不付这个代价。

**B2 / B3 弱状态模型**：

- B2 二态丢掉"已分类但未转入记忆"这个中间态——而这正是 [ADR-014](mvp-first-slice-chat-inbox-read-side.md) "读侧整理优先" 想保护的核心仪式：分类是值，转入记忆是后续动作。两者合一会让用户感觉"打了个标就被记住了"，与 Memory Ledger 的可见性诉求冲突。
- B3 无状态字段在代码层就丢失了"还在收件箱里"的语义；查询"待处理 inbox"会被迫回到下游聚合反推，违背聚合自治。

**C2 / C3 跳过分类的 Promote**：

- 直接违反 [ADR-014](mvp-first-slice-chat-inbox-read-side.md) "读侧整理优先"的产品红线——分类即价值。
- C3 让 Promote 既改分类又改状态，事件流难以解释（一个事件还是两个？事件顺序？），下游订阅者难以单独消费"分类完成"这件事。

**D2 增量 tag delta**：

- 重放需要先恢复历史 tags 再 apply delta，复杂度高于 V0 需要解决的问题。
- 桌面单设备场景无并发冲突合并需求，delta 模型的核心收益（跨设备合并）拿不到。

**D3 V0 不发事件**：

- S3b（Application + MediatR）接入时聚合代码会被改第二次：业务方法签名要从 `Result Tag(...)` 重写为同时维护内部事件队列。
- `Domain.Core/AggregateRoot` 已经把 `Raise(IDomainEvent)` 立好，等于本期不用第一时间就会再来一刀；不如同期落定。

**E2 V0 加并发版本号**：

- V0 是单设备桌面 App，本地 SQLite 单写者，没有并发写入路径，加版本号是为了将来不存在的并发场景预先付代码与 schema 复杂度。
- S3c 持久化时如果选 SQLite `RowVersion` 或自增版本号，是 schema 层的事，与聚合内字段是两个问题；提前在聚合内加版本号会污染 Domain。

## 决策

采用方案 A2 + B1 + C1 + D1 + E1 的组合。

### 1. 强类型 ID

```csharp
public readonly record struct InboxItemId(Guid Value)
{
    public static InboxItemId New() => new(Guid.NewGuid());
}
```

`default(InboxItemId)` 视为未初始化，由 `Entity<TId>` 的"id default 即编程错误"约定兜底。V0 不写 JSON / Dapper / EF 的 type handler，等 S3c 持久化层落地时再加，并不污染 Domain。

### 2. 状态机

```csharp
public enum InboxItemStatus
{
    Pending = 0,
    Tagged = 1,
    Discarded = 2,
    Promoted = 3,
}
```

合法转换：

- `Pending → Tagged`（Tag）
- `Pending → Discarded`（Discard）
- `Tagged → Discarded`（Discard）
- `Tagged → Promoted`（Promote）
- `Discarded` / `Promoted` 终态，所有进入终态的尝试返回 `InboxItemErrors.Terminal` 失败结果。

枚举序数被钉死，未来插入新值不得改动现有数字（与 ActionLevel 同款约定）。

### 3. 字段（V0）

| 字段 | 类型 | 不变量 |
|---|---|---|
| `Id` | `InboxItemId` | 继承 `Entity<InboxItemId>`，default 即编程错误 |
| `CreatedAt` | `DateTimeOffset` | 继承 `Entity<InboxItemId>` |
| `Source` | `string` | 非空白；构造时校验 |
| `Content` | `string` | 非空白；构造时校验 |
| `Status` | `InboxItemStatus` | 受第 2 节状态机约束 |
| `Tags` | `IReadOnlyCollection<string>` | 全小写 ASCII / 数字 / 连字符；去重；V0 上限 32 个，单个 tag ≤ 64 字符 |
| `DiscardReason` | `string?` | 仅 `Discarded` 状态非空，其余状态为 null |

`Tags` 上限 32 是防御性数字（防止 LLM 误生成数千个 tag 把内存撑爆），不是产品约束；触达即说明分类策略有问题，进入复议触发条件。

### 4. 业务方法（V0）

所有方法返回 `Result` / `Result<T>`。聚合内部不变量违反（如 `Source` 为空白）通过 `Result.Failure` 表达，不抛异常。

```csharp
public static Result<InboxItem> Create(
    InboxItemId id, DateTimeOffset createdAt,
    string source, string content);

public Result Tag(IReadOnlyCollection<string> tags, DateTimeOffset at);
public Result Discard(string reason, DateTimeOffset at);
public Result Promote(DateTimeOffset at);
```

错误码统一在 `InboxItemErrors` 静态目录中暴露（命名遵循 `inbox-item.{kind}` 规范，与 `DomainError.Code` 对齐）：

- `inbox-item.source-required` / `inbox-item.content-required`
- `inbox-item.tags-required` / `inbox-item.tag-invalid` / `inbox-item.tag-limit-exceeded`
- `inbox-item.reason-required`
- `inbox-item.must-be-tagged-first`（Promote 时状态非 Tagged）
- `inbox-item.terminal`（在终态尝试转换）

### 5. 领域事件

四个 `sealed record` 实现 `IDomainEvent`，全部由对应业务方法在状态转换成功后 `Raise`：

```csharp
public sealed record InboxItemCreated(
    InboxItemId Id, string Source, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InboxItemTagged(
    InboxItemId Id, IReadOnlyCollection<string> Tags, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InboxItemDiscarded(
    InboxItemId Id, string Reason, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InboxItemPromoted(
    InboxItemId Id, DateTimeOffset OccurredOn) : IDomainEvent;
```

`InboxItemTagged.Tags` 携带**全量** tag 集合（不是增量 delta），事件可独立重放，下游订阅者无需依赖前序状态。

### 6. 与权限词表的关系

聚合**不**感知 [ADR-004](important-action-levels-and-confirmation.md) 的 ActionLevel / ActionKind。聚合的职责是表达状态机与不变量；动作分类与确认流是 Application 层（结合 `Domain.Services.IActionClassifier`）的职责。聚合发出的事件是状态事实（Created / Tagged / Discarded / Promoted），不是动作语义（read.tag / inbox.add / memory.write）。这条边界由架构测试 `Domain` 不引用 `Domain.Services` 守住（[ADR-020](architecture-test-assertion-strategy.md) 已固化该断言）。

### 7. V0 显式不做

- **不**做并发版本号 / 乐观并发；S3c 持久化时再决定。
- **不**做跨聚合引用：`InboxItemPromoted` 只携带 `InboxItemId`，**不**携带 target memory id；下游怎么消费 promote 事件由 Application / Memory 聚合自己决定。
- **不**承载操作者身份（actor / user id）；当前桌面单用户场景下没有审计需求，进入多用户或对外写入时再加（与 [ADR-004](important-action-levels-and-confirmation.md) 复议触发条件一致）。
- **不**做 ORM rehydration 测试；基类 `AggregateRoot.Rehydrate_DoesNotRaiseEvents` 已覆盖契约。
- **不**写 JSON / Dapper / EF type handler；等 S3c 持久化层落地。

## 影响

**正向影响**：

- 写入 [ADR-020](architecture-test-assertion-strategy.md) 推迟的"Domain → Domain.Core 正向 layering 断言"在 S3a 闭环 commit 中可以稳定加上——`InboxItem` 派生自 `AggregateRoot<InboxItemId>` 后引用立刻在 emitted metadata 中出现。
- Application 层（S3b）有了第一个 handler 目标：`CreateInboxItemCommand` / `TagInboxItemCommand` / `DiscardInboxItemCommand` / `PromoteInboxItemCommand`。
- Infra.Data 层（S3c）有了第一个持久化目标，schema 决策可以单独 ADR 化（聚合外形已稳定）。
- Memory Ledger 论旨（[ADR-014](mvp-first-slice-chat-inbox-read-side.md)）首次有代码表达：分类是状态转换、转入记忆是事件触发。
- 公共表面积清晰、可枚举：`InboxItem` / `InboxItemId` / `InboxItemStatus` / 4 个事件 / `InboxItemErrors`。

**代价 / 风险**：

- 强类型 ID 在 S3c 持久化时需要写 type handler，比 `Guid` 直接作 ID 多一刀工作量。
- 状态机硬编码在 enum + Result 检查里，不像 state machine 库那样可视化；规则膨胀（例如加第三个终态）需要改聚合代码 + 测试 + 事件 payload，不是配置。
- Tag 上限 32 是 V0 防御值，dogfood 中可能被频繁触达，需要根据真实分布调整。
- "不感知 ActionLevel"是 V0 边界，将来 Promote 真的要写记忆时，是否在聚合内 Raise 一个标志事件还是仅由 Application 层处理需要再讨论。

## 复议触发条件

`adr_revisit_when` 已写入 front matter（见 SCHEMA §4.3.2 / §6.0），本节不重复。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：本 ADR 服务的 MVP 第一版切片方向。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-004 重要性级别与确认机制](important-action-levels-and-confirmation.md)：定义 L0–L3 动作分级，本 ADR 显式不在聚合内感知。
- [ADR-014 MVP 第一版切片](mvp-first-slice-chat-inbox-read-side.md)：定义 inbox 在产品中的角色。
- [ADR-018 后端架构参考 Equinox](backend-architecture-equinox-reference.md)：定义 DDD 分层、Result 模式与 MediatR。
- [ADR-019 测试栈 NUnit V0](testing-stack-nunit-v0.md)：本 ADR 的测试约定来源。
- [ADR-020 架构测试断言策略](architecture-test-assertion-strategy.md)：S3a 闭环 commit 将基于本 ADR 解锁 Domain → Domain.Core 正向 layering 断言。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 是 S3a 实现前的方案先行产物。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
