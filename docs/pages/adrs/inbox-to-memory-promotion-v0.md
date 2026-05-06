---
title: ADR-034 Inbox 显式沉淀进 Memory Ledger V0：IInboxToMemoryAppService 协调器、固定 scope=inbox 与 POST /api/inbox/items/{id}/promote-to-memory 端点
type: adr
subtype: architecture
canonical: true
summary: V0 给每条 inbox item 增加用户主动触发的「Save to Memory」按钮，把 inbox 原始 content 显式沉淀进 Memory Ledger（Source=InboxAction、isExplicit=true、confidence=1.0、sensitivity=Normal、scope 固定字符串 "inbox"）；新增 IInboxToMemoryAppService 跨聚合协调器（持有 IInboxRepository + IMemoryLedgerRepository、直接构造 MemoryLedgerEntry，不复用受 ADR-033 §B1 强约束的 MemoryLedgerAppService.CreateExplicitAsync）；新增 POST /api/inbox/items/{id:guid}/promote-to-memory 端点；inbox.notFound → 404 沿用；V0 不去重（重复点击 = 多条 ledger 行）、不持久化 summary / tags、不写来源回追列、不做 chat→ledger 自动写入；解锁 ADR-033 §决策 B 中保留的 MemorySource.InboxAction 枚举位的第一个写入路径。
tags: [agent, memory, engineering]
sources: []
created: 2026-05-06
updated: 2026-05-06
verified_at: 2026-05-06
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/memory-ledger-v0-schema-and-storage.md, pages/adrs/explicit-memory-ledger-mvp.md, pages/adrs/memory-privacy-and-user-control.md, pages/adrs/long-term-memory-as-core-capability.md, pages/adrs/inbox-v0-capture-and-list-contract.md, pages/adrs/inbox-item-summarize-v0.md, pages/adrs/inbox-item-tagging-v0.md, pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/backend-architecture-equinox-reference.md, pages/adrs/desktop-renderer-v0-native-html-and-ipc-bridge.md, pages/adrs/important-action-levels-and-confirmation.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-06
adr_revisit_when: "用户报告重复点击 Save 在 ledger 里产生多条同内容行造成噪声（迫使引入 source_inbox_item_id 列 + unique index + 已沉淀态 UI）；ADR-035 落地 summary / tags 持久化后用户开始要求「沉淀 summary」「沉淀 tags」（迫使把单按钮拆为多按钮或弹窗多选）；用户在 ledger 详情页要求「这条记忆来自哪个 inbox item」（迫使引入 source_inbox_item_id 列 + 反向查询）；用户开始要求 chat 也有同款 Save 按钮（迫使开 ADR-036 chat→ledger）；scope 固定字符串 inbox 在 dogfood 中暴露不足以区分（例如「我想看从 chat 沉淀的 vs 从 paper 沉淀的」），迫使新增 source_origin 列；产品需要「批量 promote」（迫使引入批量端点与限流策略）；产品需要「unpromote」反向操作（撤回 ledger 回 inbox），迫使设计反向状态机；用户要求按一次按钮就预先用 LLM 改写 inbox content（例如 summarize 后入 ledger），迫使把同步直写路径升级为 LLM 调度路径。"
---

# ADR-034 Inbox 显式沉淀进 Memory Ledger V0：协调器 + promote-to-memory 端点 + Save 按钮

> V0 给每条 inbox item 增加「Save to Memory」按钮，把 inbox 原始 content 显式沉淀进 Memory Ledger（`Source=InboxAction`、`scope="inbox"` 固定）；新增 `IInboxToMemoryAppService` 跨聚合协调器；新增 `POST /api/inbox/items/{id:guid}/promote-to-memory` 端点；不去重、不持久化 summary / tags、不引入回追列。是 [ADR-033](memory-ledger-v0-schema-and-storage.md) §决策 N 中保留的 `MemorySource.InboxAction` 枚举位的第一个写入路径。

## 背景

[PURPOSE.md](../../PURPOSE.md) 的 MVP 成功信号第一条是 **「Memory 被真实复用：新任务至少部分依赖历史分类、命名、偏好或纠错记录」**。截至 2026-05-06，[ADR-033](memory-ledger-v0-schema-and-storage.md) 把 Memory Ledger 落地（聚合 / SQLite migration v4 / 5 个 RESTful 端点 / 桌面 📒 视图），但 V0 切片**只允许用户从空白手敲**——`MemoryLedgerAppService.CreateExplicitAsync` 强制 `source = UserExplicit, isExplicit = true, confidence = 1.0`，DTO 不暴露这些字段。

同时 [ADR-026 Inbox V0](inbox-v0-capture-and-list-contract.md) / [ADR-030 单条总结](inbox-item-summarize-v0.md) / [ADR-031 单条打标签](inbox-item-tagging-v0.md) 已经让 inbox 跑起来：用户可以 capture / list / summarize / tag，但所有 LLM 产物都是**瞬时的**（关窗口就消失），inbox 与 Memory Ledger 之间没有任何数据流。

ADR-033 §决策 N 明确把 **"inbox → ledger 自动写入"** 列为 V0 不做项，但同时在 §决策 B1 保留了枚举位 `MemorySource.InboxAction = 3`，等待后续 ADR 解锁写入路径。本 ADR 是这条解锁的**最小、用户主动触发**版本——既不违反 ADR-033 §B1 "V0 仅显式写入" 红线（按钮点击就是显式行为），又解锁了第一条跨聚合数据流，让 MVP 成功信号第一条进入可观测状态。

V0 必须同时回答以下八个工程问题：

1. **写入源标记**：`Source` 字段填什么？继续标 `UserExplicit` 还是用保留的 `InboxAction`？
2. **写入内容**：把 inbox 的什么字段沉淀进 ledger？原始 content / summary / tags？
3. **scope 派生策略**：固定字符串 / 从 `inbox.source` 派生 / 用户输入？
4. **跨聚合协调位置**：扩 `IMemoryLedgerAppService` / 扩 `Inbox AppService` / 新建协调器？
5. **去重策略**：重复点击 Save 怎么办？后端拒绝 / 后端容忍 / 前端拒绝？
6. **端点位置**：挂在 `/api/inbox/...` 下还是 `/api/memory/...` 下？
7. **错误码 / HTTP 映射**：复用 `inbox.notFound` 还是另起 `memory.promotion.*`？
8. **桌面 UI 落点**：每条 inbox item 第三按钮 / Memory 视图导入面板？

用户已在方案先行讨论中拍板：scope 固定字符串 `"inbox"`（C1）；不去重（F1）；按钮挂 inbox actions 行（H1）；端点位置挂 inbox 命名空间（E1）；Source 标 `InboxAction`（A1）；只沉淀原始 content（B1，summary / tags 留给后续 ADR）。本 ADR 把这八个问题一次性定下来；落地按 4 个 commit 切分（见 §实施概要）。

## 备选方案

**A. 写入源标记**：

- 方案 A1：`Source = MemorySource.InboxAction`，`isExplicit = true`，`confidence = 1.0`
- 方案 A2：`Source = MemorySource.UserExplicit`（与现有 `MemoryLedgerAppService.CreateExplicitAsync` 一致），不区分来源
- 方案 A3：新增 `MemorySource.InboxPromotion` 枚举值

**B. 写入内容**：

- 方案 B1：仅 inbox 原始 content（`InboxItem.Content`），不读 LLM 产物
- 方案 B2：写入 LLM summary（要求 ADR-030 summary 先持久化）
- 方案 B3：写入 tags 拼接为字符串（要求 ADR-031 tags 先持久化）
- 方案 B4：弹窗让用户选 content / summary / tags

**C. scope 派生策略**：

- 方案 C1：固定字符串 `"inbox"`
- 方案 C2：从 `InboxItem.Source` 派生为 `"inbox.{source}"`（`source` 为空时回落 `"inbox"`）
- 方案 C3：弹窗让用户输入 scope

**D. 跨聚合协调位置**：

- 方案 D1：新建 `IInboxToMemoryAppService` 跨聚合协调器，持有 `IInboxRepository` + `IMemoryLedgerRepository`，**直接** new Domain 聚合 `MemoryLedgerEntry.Create(..., source: InboxAction, ...)` 然后 `AddAsync`
- 方案 D2：扩 `IMemoryLedgerAppService` 加 `CreateFromInboxAsync(inboxItemId, ct)` 方法
- 方案 D3：扩现有 inbox AppService（`InboxAppService` 等）持有 `IMemoryLedgerRepository`
- 方案 D4：在 Domain 层定义 `MemoryLedgerEntry.FromInboxItem(inbox, now)` 工厂

**E. 端点位置**：

- 方案 E1：`POST /api/inbox/items/{id:guid}/promote-to-memory` —— 挂 inbox 命名空间，与 `/summarize` `/tags` 同构
- 方案 E2：`POST /api/memory/from-inbox/{inboxId}` —— 挂 memory 命名空间
- 方案 E3：`POST /api/memory` body 加 `inboxItemId` 字段，复用现有创建端点

**F. 去重策略**：

- 方案 F1：不去重——每次点击都老老实实写一条新 ledger，允许重复行
- 方案 F2：后端去重——加 `source_inbox_item_id` 列 + unique index，重复触发返回 422 `inbox.alreadyPromoted`
- 方案 F3：前端去重——renderer 列表加载时查询哪些 inbox 已沉淀，按钮 disable
- 方案 F4：后端 idempotent 返回——重复触发返回**已存在**的那条 ledger，不写新行

**G. 错误码 / HTTP 映射**：

- 方案 G1：复用 [ADR-026](inbox-v0-capture-and-list-contract.md) `InboxErrors.ItemNotFoundCode`（`inbox.notFound` → 404）；其余字段校验沿用 ADR-033 错误码（`memory.content.tooLong` 等，理论上不会触发）
- 方案 G2：另起 `inbox.promotion.*` 错误命名空间
- 方案 G3：用 ADR-033 `memory.invalidStatusTransition` 类比

**H. 桌面 UI 落点**：

- 方案 H1：每条 inbox item 的 actions 行加第三按钮 "📒 Save"，与 `Summarize` / `Tags` 平级
- 方案 H2：Memory 视图加 "从 Inbox 导入" 按钮 + 选择列表
- 方案 H3：每条 inbox item 加复选框 + 顶部批量按钮

## 被否决方案与理由

**A2（仍标 UserExplicit）**：丢失溯源。ADR-033 §决策 B1 已为 `InboxAction` 保留枚举位，正是为了记录"哪些条目是从 inbox 流过来的"；如果第一个写入路径就把这个区分丢掉，未来想做 `?source=InboxAction` 维度筛选会做不到，且要做迁移把历史行回填，得不偿失。

**A3（新增 MemorySource.InboxPromotion）**：与 [ADR-033 §决策 B1](memory-ledger-v0-schema-and-storage.md) 已定的 4 档枚举（`UserExplicit / Conversation / InboxAction / Correction`）冲突。"InboxAction" 已经是一个对正交"动作来源"的描述（包括 promote / 未来的自动抽取），不需要细分。

**B2 / B3（写入 summary / tags）**：违反工程依赖顺序。ADR-030 / ADR-031 明确"不持久化"，summary / tags 都是关窗口即丢的瞬时产物；要先做这两块的持久化（属于 ADR-035 工作量）才能让本 ADR 触达。本 ADR 第一刀只解锁 content 路径，**让 dogfood 先看到 inbox→memory 闭环**，再决定 summary / tags 沉淀是否值得做。

**B4（弹窗多选）**：V0 不引入弹窗复杂度。renderer 现状是 [ADR-027](desktop-renderer-v0-native-html-and-ipc-bridge.md) 的原生 HTML + IIFE，没有现成的 modal 组件。一个简单按钮对应一个固定语义是最低工程成本的形态。

**C2（从 inbox.source 派生）**：会让 ledger 的 scope 维度被 inbox 的 source 维度污染。`InboxItem.Source` 是 [ADR-026](inbox-v0-capture-and-list-contract.md) 定义的自由 TEXT（≤64 字符，无受控词典），用户实际写入的会是 `"chat"` / `"Chat"` / `"chat "` / `"我从聊天来的"` / `"asdf"` / `""` 这些杂乱字符串；如果用 C2，所有这些杂乱值会变成 ledger.scope，导致：

1. ADR-033 未来想给 scope 引入受控词典时迁移工作量翻倍；
2. ledger 的 `scope` 维度（"这条记忆**关于什么主题**"）和 inbox 的 source 维度（"这条 inbox 输入**从哪个交互渠道来**"）本来正交，硬塞同一个字段会让两个维度互相牵扯；
3. 未来真要支持"按沉淀来源筛选 ledger"时，正确设计应是新增一列 `source_origin TEXT NULL`（值是 inbox.source 的拷贝），而不是污染 scope。

**C3（弹窗输入 scope）**：与 B4 同理，V0 不做弹窗。且让用户每次点击都要决定 scope 会显著增加交互成本，违反"管家形态"产品定位。

**D2（扩 MemoryLedgerAppService）**：会破坏 [ADR-033 §决策 B1](memory-ledger-v0-schema-and-storage.md) 的"V0 仅显式写入"假设，让 memory 立面接受 inbox 这一额外语义。`MemoryLedgerAppService.CreateExplicitAsync` 当前的 contract 是"用户显式写一条记忆"——把 inbox 流入路径塞进去就要在内部分支化（"这次是从 inbox 来的吗？是的话不强制 UserExplicit"），AppService 立面会逐渐变成所有写入路径的开关大杂烩。新建协调器更干净。

**D3（扩 inbox AppService）**：违反 [ADR-021 后端架构 EquinoxProject 参照](backend-architecture-equinox-reference.md) 的边界——inbox 不应该知道 memory 存在。如果未来出现 chat→ledger，按 D3 又要让 chat AppService 知道 memory；最终所有源都互相耦合。新建跨聚合协调器是干净的方向。

**D4（Domain 工厂 FromInboxItem）**：把"inbox 流入"语义渗进 Domain。Domain 层不应该知道有 inbox 这种聚合存在；`MemoryLedgerEntry.Create(source: InboxAction, ...)` 已经够用——Domain 层只暴露通用 source 参数，由 Application 层决定填什么。

**E2（挂 memory 命名空间）**：语义错位。沉淀这件事是**用户在 inbox 上做的动作**，不是 memory 主动从 inbox 拉。HTTP 路径应反映"对哪个资源做什么"——`POST /api/inbox/items/{id}/promote-to-memory` 读起来一目了然。

**E3（POST /api/memory body 加 inboxItemId）**：与 D2 同理，会让 `POST /api/memory` 端点的 contract 包含两条不同写入路径（直接写 / 从 inbox 流入），增加测试矩阵和文档负担。

**F2（后端去重）**：工程代价不成比例：

1. 必须 migration v5（`memory_entries` 加 `source_inbox_item_id` 列 + unique partial index）；
2. Domain 聚合 `MemoryLedgerEntry` 要加 `SourceInboxItemId : Guid?` 属性 + 工厂参数 + Rehydrate 路径 + 单元测试；
3. AppService 要先查 `IMemoryLedgerRepository.FindByInboxSourceAsync(inboxId)` 才能决定写或拒绝（多一次 SQL 查询）；
4. 新增错误码 `inbox.alreadyPromoted` → 422 + 至少 3 个测试用例；
5. UI 要表达"已沉淀"态，按钮初始要查询 ledger 决定 Save / Saved（每次列表刷新都要 N 次查询或后端 join）。

而**收益**只是阻止用户手抖产生重复 ledger 行——重复行用户**显式可见、可编辑、可删除**（ADR-033 §决策 J1），用户看到自己手抖点 delete 即可。V0 不做去重；ledger 噪声真正发生时触发本 ADR 复议。

**F3（前端去重）**：把"是否已沉淀"作为状态散布到客户端是错位的——后端是单一真相源；前端依赖后端去重逻辑做 UI 决策，等于把 F2 的所有工程代价照搬一遍，且更脆弱（多客户端、多窗口同时操作）。

**F4（idempotent 返回）**：表面上是温和方案，实际语义诡异——"用户期望写一条新记忆（可能是为了重新开始），后端却返回 30 天前的旧 id"。容易让用户误以为按钮坏了。`returns新创建` + `不去重` 的语义是最直白的。

**G2（另起 inbox.promotion.* 命名空间）**：V0 错误码集已经够多；本路径只可能出错 "inbox 不存在"（404），完全可以复用现有 `inbox.notFound`。新错误码族需要的语义（"沉淀本身的失败"）暂未出现。

**G3（用 memory.invalidStatusTransition 类比）**：状态转移与 inbox 沉淀语义不相关。

**H2（Memory 视图导入面板）**：UI 重，且让用户离开当前位置（必须切到 Memory 视图、找到 inbox 列表、勾选）。违反"管家在你身边"产品定位——管家应该在用户当前位置上提供动作，而不是要求用户跳到管家的视图里去。

**H3（批量复选框 + 顶部批量按钮）**：[ADR-033 §决策 N](memory-ledger-v0-schema-and-storage.md) 明确把"批量操作"列为 V0 不做项；同款节制适用于本 ADR。dogfood 阶段单条点击是更可控的形态。

## 决策

### 决策 A1（写入源标记 = MemorySource.InboxAction）

新建 ledger 行：

```
source       = MemorySource.InboxAction
isExplicit   = true
confidence   = 1.0
sensitivity  = MemorySensitivity.Normal
status       = MemoryStatus.Active
```

`isExplicit = true` 的语义：用户**显式**点击了 Save 按钮（不是 LLM 推断、不是 agent 自动抽取）。`confidence = 1.0` 的语义：用户已经检阅过 inbox 内容并主动选择沉淀，置信度满分。

### 决策 B1（写入内容 = inbox 原始 content）

只读 `InboxItem.Content`；不读 LLM 产物。`MemoryLedgerEntry.Content = inboxItem.Content`。

`inboxItem.Content` 长度上限是 [ADR-026](inbox-v0-capture-and-list-contract.md) 的 4096，与 [ADR-033](memory-ledger-v0-schema-and-storage.md) `MemoryLedgerEntry.MaxContentLength = 4096` 一致，理论上不会触发 `memory.content.tooLong`，但 Application 层仍按 ADR-033 既有校验路径走（防御性）。

### 决策 C1（scope = 固定字符串 "inbox"）

无论 `inboxItem.Source` 是什么值，新建 ledger 的 `scope` 一律填字符串 `"inbox"`。

未来想区分"沉淀的 inbox 来自哪个 source"时，正确的迁移方向是新增一列 `source_origin TEXT NULL`（值是 `inboxItem.Source` 的拷贝），而不是污染 scope。该需求不在 V0 范围内，触发条件已写入 frontmatter `adr_revisit_when`。

### 决策 D1（新建跨聚合协调器 IInboxToMemoryAppService）

新建 Application 层接口与实现：

```csharp
// src/Dawning.AgentOS.Application/Interfaces/IInboxToMemoryAppService.cs
public interface IInboxToMemoryAppService
{
    Task<Result<MemoryEntryDto>> PromoteAsync(Guid inboxItemId, CancellationToken ct);
}

// src/Dawning.AgentOS.Application/Services/InboxToMemoryAppService.cs
public sealed class InboxToMemoryAppService(
    IClock clock,
    IInboxRepository inboxRepository,
    IMemoryLedgerRepository memoryRepository
) : IInboxToMemoryAppService
{
    public async Task<Result<MemoryEntryDto>> PromoteAsync(Guid inboxItemId, CancellationToken ct)
    {
        var inbox = await inboxRepository.GetByIdAsync(inboxItemId, ct);
        if (inbox is null)
            return Result<MemoryEntryDto>.Failure(InboxErrors.ItemNotFound(inboxItemId));

        var entry = MemoryLedgerEntry.Create(
            content: inbox.Content,
            scope: PromotionScope,            // = "inbox"
            source: MemorySource.InboxAction, // 关键差异
            isExplicit: true,
            confidence: 1.0,
            sensitivity: MemorySensitivity.Normal,
            createdAtUtc: clock.UtcNow
        );

        await memoryRepository.AddAsync(entry, ct);
        return Result<MemoryEntryDto>.Success(ToDto(entry));
    }
}
```

约束：

- 协调器**直接**调 Domain 工厂 `MemoryLedgerEntry.Create`，不经过 `MemoryLedgerAppService.CreateExplicitAsync`（后者强制 `UserExplicit`，是 ADR-033 §B1 的产物，不破坏）。
- 协调器**不修改** `MemoryLedgerAppService` 现有方法签名；ADR-033 的所有测试与契约保持原样。
- DTO 映射 `MemoryEntryDto` 复用 ADR-033 现有定义；不为本路径新建 DTO。
- 由 ADR-033 既有自动 DI 扫描（`IXxxAppService` / `XxxAppService` 反射注册）自动注册，无需手动 `AddScoped`。

### 决策 E1（端点位置 = inbox 命名空间）

新增端点挂 `InboxEndpoints.cs`（已经承载 `/summarize` / `/tags`）：

```
POST /api/inbox/items/{id:guid}/promote-to-memory
```

请求体：空（id 已在路径上）。响应：200 + `MemoryEntryDto`（与 ADR-033 `POST /api/memory` 同构）。错误映射沿用 [ADR-030 §决策 K1](inbox-item-summarize-v0.md) / [ADR-031](inbox-item-tagging-v0.md) 的 manual switch 模板（不能让 404 / 422 经 `ToHttpResult` 一刀切成 422）。

### 决策 F1（不去重）

后端不查重；同一 inbox item 反复点击 Save 会产生多条 ledger 行（id 不同、content 相同）。

可控的弱引导：UI 在第一次成功响应后**短暂**展示绿色提示 `已保存到 Memory（id=xxxxxxxx）`（id 8 位前缀），按钮文字微弱褪色（**不 disable**，允许重复点击 = 重复沉淀，符合"用户可以反复沉淀"的语义）。重复点击不报错，正常返回新的 `MemoryEntryDto`。

ledger 噪声触发条件已写入 frontmatter `adr_revisit_when`，达到时按 §被否决方案与理由 F2 列出的 5 项工程代价**一次性**升级到去重路径。

### 决策 G1（错误码复用现有）

| 场景 | code | HTTP | 来源 |
|---|---|---|---|
| inbox 不存在 | `inbox.notFound` | 404 | [ADR-026](inbox-v0-capture-and-list-contract.md) |
| inbox.content 超长（理论上不可能） | `memory.content.tooLong` | 400 | [ADR-033 §决策 J1](memory-ledger-v0-schema-and-storage.md) |
| token 缺失 | （[ADR-026 §J2](inbox-v0-capture-and-list-contract.md) startup token 中间件） | 401 | 现有契约 |

不引入新错误码族 `inbox.promotion.*`。

### 决策 H1（桌面 UI = 第三按钮 "📒 Save"）

renderer 改造（基于现有 [ADR-027](desktop-renderer-v0-native-html-and-ipc-bridge.md) 原生 HTML + IIFE）：

- 每条 inbox item 的 actions 行新增第三按钮 `"📒 Save"`，平级 `Summarize` / `Tags`
- preload bridge 新增 `window.agentos.inbox.promoteToMemory(itemId): Promise<InboxIpcResult<MemoryEntryDto>>`
- main 进程新增 `agentos:inbox:promoteToMemory` IPC handler，POST 到 `${runtime.baseUrl}/api/inbox/items/${id}/promote-to-memory`，附 startup token
- 成功：临时绿色提示条 `已保存到 Memory（id=xxxxxxxx）`，按钮褪色但不 disable
- 失败：红色错误条（沿用现有 `formatProblem` 工具），按钮恢复

新增 IPC handler 的契约与现有 `agentos:inbox:summarize` / `agentos:inbox:suggestTags` 同构。

### 决策 I（不在 V0 范围内的事）

> 与 [ADR-033 §决策 N](memory-ledger-v0-schema-and-storage.md) 同款；触发条件出现时开新 ADR。

- **summary / tags 持久化与沉淀** —— 留 ADR-035。本 ADR 仅沉淀原始 content。
- **沉淀去重 / 已沉淀态 UI / 来源回追列** —— 见 §被否决方案 F2 / 决策 F1。触发条件已在 frontmatter。
- **chat → ledger 自动写入** —— 留 ADR-036。需要 LLM tool call 或前端按钮 + 写入端点解锁 `Source=Conversation`。
- **反向操作（unpromote）** —— 从 ledger 撤回到 inbox。无明显产品压力。
- **批量沉淀 / 复选框** —— [ADR-033 §决策 N](memory-ledger-v0-schema-and-storage.md) 已统一节制。
- **沉淀时跑 LLM 改写** —— 例如沉淀前先 summarize。属于异步路径与同步直写路径的混合，超出 V0 切片。
- **跨设备同步 ledger / 加密导出** —— 与 ADR-033 §决策 N 一致。

## 影响

**正向影响**：

- [PURPOSE.md](../../PURPOSE.md) MVP 成功信号第一条**进入可观测状态**：用户产生的 inbox 条目可以**显式**沉淀进 Memory Ledger，第一条跨聚合数据流打通。
- ADR-033 §决策 B 中保留的 `MemorySource.InboxAction` 枚举位的**第一个写入路径**落地，证明 ADR-033 的"枚举位预留 + 路径分批解锁"策略可行。
- `IInboxToMemoryAppService` 跨聚合协调器**首次落地**，给后续 chat→ledger（ADR-036 候选）/ inbox→其他下游 提供可参考形态。
- 没有新增 schema migration（ledger schema 不变）、没有破坏 ADR-033 / ADR-026 / ADR-030 / ADR-031 的任何契约；**纯增量解锁**。
- DDD 分层骨架第四次复用：相同的 AppService + 测试结构 + 端点 + UI IPC 同构。
- "管家在你身边"产品定位获得最直接的 UI 验证（Save 按钮就在用户当前位置）。

**代价 / 风险**：

- 不去重 → ledger 可能出现内容相同的重复行；缓解：用户可见 + 可手动删（ADR-033 §决策 J1 软删）；触发条件已写入 frontmatter `adr_revisit_when`。
- scope 固定 `"inbox"` → 所有沉淀条目挤进同一 scope，无法在 ledger 视图按"从 chat 沉淀 / 从 paper 沉淀"细分；可接受，dogfood 阶段 ledger 总条目数预期 < 100。
- 跨聚合协调器引入了一个**不属于任何聚合**的 AppService；与 [ADR-021](backend-architecture-equinox-reference.md) DDD 分层兼容（Application 层本来就是协调层），但新人理解时需要先看 ADR 才能定位文件。
- 用户可能误解"沉淀"= "移动"（即沉淀后 inbox 条目消失）；本 V0 沉淀不影响 inbox（inbox 行原样保留）；UI 提示需要清晰，否则会出现"沉淀完了想找 inbox 怎么找不到"的认知摩擦。
- 沉淀按钮挤在 actions 行第三个，actions 区域宽度可能在小窗口下溢出；需要 dogfood 时观察 UI 表现，必要时改为换行布局。
- 没有写"来源回追列"（`source_inbox_item_id`）→ 用户在 ledger 详情页看不到"这条记忆来自哪个 inbox"；触发条件已写入 frontmatter。
- 重复点击允许产生重复 ledger 行 → 与"软删保留 30 天"策略叠加时（ADR-033 §决策 M1）会有更长的恢复窗口噪声；当物理清理 ADR 落地时需要复评估。

## 复议触发条件

`adr_revisit_when` 已写入 front matter（见 [SCHEMA §4.3.2 / §6.0](../../SCHEMA.md)），本节不重复。

## 实施概要

落地按 4 个 commit 切分，**每个 commit 都必须建立在前一个 commit 之上**；遵循 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)。

### M1 — Application 协调器与单元测试（一个 commit）

```
feat(application): inbox-to-memory promotion service per ADR-034
```

新建：

- `src/Dawning.AgentOS.Application/Interfaces/IInboxToMemoryAppService.cs`
- `src/Dawning.AgentOS.Application/Services/InboxToMemoryAppService.cs`（持有 `IClock + IInboxRepository + IMemoryLedgerRepository`，常量 `PromotionScope = "inbox"`）

测试同 commit（`tests/Dawning.AgentOS.Application.Tests/Services/InboxToMemoryAppServiceTests.cs`）：

- ✅ 成功路径：inbox exists → ledger AddAsync called once → returns `MemoryEntryDto`
- ✅ inbox 不存在 → 返回 `inbox.notFound` 错误，未调 ledger AddAsync（strict mock）
- ✅ 重复沉淀允许：连续两次 Promote 同一 inbox id → 都成功，AddAsync 调用 2 次（不同 entry id）
- ✅ source 落点：捕获 AddAsync 入参，断言 `entry.Source == InboxAction && entry.IsExplicit && entry.Confidence == 1.0`
- ✅ scope 落点：断言 `entry.Scope == "inbox"`
- ✅ content 透传：断言 `entry.Content == inbox.Content`（不被 trim、不被改写）
- ✅ null 入参守卫：构造器 + `PromoteAsync` 各路径
- ✅ 取消传播：`OperationCanceledException` 不被吞

≥ 8 用例。

### M2 — Api 端点与集成测试（一个 commit）

```
feat(services-api): inbox promote-to-memory endpoint per ADR-034
```

修改：

- `src/Dawning.AgentOS.Api/Endpoints/Inbox/InboxEndpoints.cs`：新增 `MapPost("/items/{id:guid}/promote-to-memory", ...)`（manual error switch；沿用 ADR-030 / ADR-031 模板）

测试（`tests/Dawning.AgentOS.Api.Tests/Endpoints/Inbox/InboxPromoteEndpointTests.cs`）：

- ✅ 200 happy path：先 POST `/api/inbox` 捕获真实行 → POST `/promote-to-memory` → 断言 200 + `MemoryEntryDto`
- ✅ 200 重复沉淀：连续两次 Promote → 都 200，返回不同 ledger id
- ✅ 404 missing inbox：随机 guid → 404 + `inbox.notFound`
- ✅ 401 token 缺失：不带 X-Startup-Token → 401
- ✅ 沉淀写入正确：Promote 后 `GET /api/memory` 应找到该条 ledger（content 相同 / source 投影 / scope == inbox）

≥ 5 用例。

### M3 — 桌面 Save 按钮 + IPC 桥（一个 commit）

```
feat(apps-desktop): inbox save-to-memory button per ADR-034
```

修改：

- `apps/desktop/src/preload.ts`：新增 `inbox.promoteToMemory(itemId)` + `MemoryEntryDto` interface
- `apps/desktop/src/main.ts`：新增 `agentos:inbox:promoteToMemory` IPC handler
- `apps/desktop/src/renderer/index.html`：每条 inbox item 加第三按钮 `📒 Save`；成功提示条 + 错误条；按钮文字成功后变 `📒 Saved`（不 disable）
- `apps/desktop/scripts/smoke.ts`：新增 `probeInboxPromotion`（capture inbox → promote → list memory 应找到）

### M4 — 文档与 changelog（如需要）

V0 阶段 changelog 由 `docs/log.md` 自动生成（[SCHEMA §8.7](../../SCHEMA.md)），不手维。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：本 ADR 直接服务于 MVP 成功信号"Memory 被真实复用"。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-007 记忆隐私与用户控制](memory-privacy-and-user-control.md)：用户显式触发 = 满足"推断 vs 显式"红线；本 ADR 不引入推断写入路径。
- [ADR-011 Memory MVP 采用显式记忆账本](explicit-memory-ledger-mvp.md)：本 ADR 是其"显式可见 / 可编辑 / 可删除"约束在跨聚合写入路径上的具体落地。
- [ADR-014 MVP 第一版切片](mvp-first-slice-chat-inbox-read-side.md)：定义 inbox 与 Memory Ledger 同属 MVP 第一版；本 ADR 是两者打通。
- [ADR-021 后端架构 EquinoxProject 参照](backend-architecture-equinox-reference.md)：跨聚合协调器位于 Application 层；本 ADR 第四次复用 DDD 分层。
- [ADR-023 API 入口与 V0 端点](api-entry-facade-and-v0-endpoints.md)：5 端点 RESTful 风格 / 错误码映射依据。
- [ADR-026 Inbox V0 数据契约](inbox-v0-capture-and-list-contract.md)：`InboxItem.Content` 字段来源 / `inbox.notFound` 错误码来源。
- [ADR-027 桌面渲染端 V0](desktop-renderer-v0-native-html-and-ipc-bridge.md)：renderer 不引入框架；新按钮 + IPC handler 沿用现有形态。
- [ADR-030 Inbox 单条总结 V0](inbox-item-summarize-v0.md)：端点 manual error switch 模板来源。
- [ADR-031 Inbox 单条打标签 V0](inbox-item-tagging-v0.md)：端点 + 测试模板来源；与本 ADR 平级。
- [ADR-033 Memory Ledger V0](memory-ledger-v0-schema-and-storage.md)：直接父 ADR；本 ADR 解锁其 §决策 B 中保留的 `MemorySource.InboxAction` 枚举位。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 是该规则的产物。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
