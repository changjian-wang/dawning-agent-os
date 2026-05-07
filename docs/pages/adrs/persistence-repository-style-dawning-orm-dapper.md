---
title: ADR-036 持久化仓储风格统一：Infrastructure Repository 采用 Dawning.ORM.Dapper
type: adr
subtype: architecture
canonical: true
summary: 为解决 Infrastructure 层仓储手写 SQL 扩散、风格不一致和跨仓库维护割裂问题，dawning-agent-os 决定将仓储实现统一到 dawning 现有风格：持久化实体（[Table]/[Column]/[ExplicitKey]）+ Dawning.ORM.Dapper 扩展方法（Get/Insert/Builder/Count）+ 聚合 Rehydrate 映射；Domain 仍保持行为模型，不向 ORM 注解泄漏；同时在 dawning 仓库推进 Dawning.ORM.Dapper 单目标 net9 发布，以 NuGet 包方式供 agent-os 消费。
tags: [agent, engineering]
sources: []
created: 2026-05-07
updated: 2026-05-07
verified_at: 2026-05-07
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/sqlite-dapper-bootstrap-and-schema-init.md, pages/adrs/inbox-v0-capture-and-list-contract.md, pages/adrs/chat-v0-streaming-and-persistence.md, pages/adrs/memory-ledger-v0-schema-and-storage.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-07
adr_revisit_when: "Dawning.ORM.Dapper 完成 net9 新版本发布且 agent-os 升级后出现兼容性回归；Repository 迁移后出现显著性能回退（同数据集 P95 > 20%）；Domain 聚合因持久化风格被迫暴露 setter 或丢失行为封装；跨仓库包版本治理出现频繁冲突；或未来确认需要改用完全不同 ORM（如 EF Core）时。"
---

# ADR-036 持久化仓储风格统一：Infrastructure Repository 采用 Dawning.ORM.Dapper

> 为解决 Infrastructure 层仓储手写 SQL 扩散、风格不一致和跨仓库维护割裂问题，dawning-agent-os 决定将仓储实现统一到 dawning 现有风格：持久化实体（[Table]/[Column]/[ExplicitKey]）+ Dawning.ORM.Dapper 扩展方法（Get/Insert/Builder/Count）+ 聚合 Rehydrate 映射；Domain 仍保持行为模型，不向 ORM 注解泄漏；同时在 dawning 仓库推进 Dawning.ORM.Dapper 单目标 net9 发布，以 NuGet 包方式供 agent-os 消费。

## 背景

ADR-024/ADR-026 落地后，agent-os 的 `InboxRepository`、`ChatSessionRepository`、`MemoryLedgerRepository` 在 Infrastructure 层持续采用“Dapper + 手写 SQL 字符串常量”模式。该模式在 MVP 早期可用，但随着功能切片增长，出现三个问题：

- 仓储代码中 SQL 文本持续累加，查询拼接、分页、排序等样板重复增加。
- 与 dawning 主仓库的数据访问风格割裂：dawning 已长期采用 `Dawning.ORM.Dapper` 的持久化实体 + ORM 扩展方法模式。
- 跨仓库维护成本上升：同一组织内两套风格并存，review 与迁移成本变高。

同时，PURPOSE 已明确 MVP 技术形态中“数据访问采用 Dawning.ORM.Dapper + Microsoft.Data.Sqlite”。本 ADR 负责把该方向从“文档契约”落地为“代码规范与实现基线”。

## 备选方案

- A. 维持现状：继续使用 Dapper + 手写 SQL。
- B. 全量切换为 EF Core。
- C. 统一采用 Dawning.ORM.Dapper，Domain 不贴 ORM 注解，Infrastructure 持久化实体负责映射。
- D. 直接把 Domain 聚合改造成 ORM 可直接映射的贫血模型（公开 setter / 无行为约束）。

## 被否决方案与理由

### A. 维持现状：否决

该路径无法解决风格割裂和样板 SQL 累积问题，且与 dawning 既有实践不一致。

### B. 全量切换 EF Core：否决

超出当前 MVP 迁移预算，会引入迁移机制、跟踪行为、额外抽象成本，与当前“最小依赖 + 快速演进”阶段不匹配。

### D. Domain 直接 ORM 化：否决

会破坏聚合行为封装，违背当前 Domain 以行为为中心、通过 `Capture`/`Rehydrate` 区分业务动作与持久化还原的模型边界。

## 决策

### D1. Repository 风格统一到 Dawning.ORM.Dapper

Infrastructure 仓储默认采用以下形态：

- 每个聚合对应一个持久化实体（PO），放在 Infrastructure 的 Persistence 子命名空间。
- 持久化实体使用 `[Table]`、`[ExplicitKey]`、`[Column]` 注解绑定表/列。
- 仓储中优先使用 `GetAsync/InsertAsync/Builder/Count` 等 ORM 扩展方法，不再以内联 SQL 常量作为默认实现。

### D2. Domain 与 Persistence 严格分离

- Domain 聚合保持行为模型，不引入 ORM 注解。
- 仓储通过 `ToEntity` / `MapEntity` 映射，并统一走 `Rehydrate(...)` 恢复聚合，避免加载路径触发领域事件。

### D3. 迁移策略

- 先落 `InboxRepository` 作为模板实现。
- 其后按增量方式迁移 `ChatSessionRepository`、`MemoryLedgerRepository`。
- 在迁移未完成阶段允许 Dapper 与 Dawning.ORM.Dapper 共存于 Infrastructure，仅限该层。

### D4. 包治理

- `Dawning.ORM.Dapper` 由 dawning 仓库发布到 NuGet。
- agent-os 通过 NuGet 包引用消费，不建立跨仓库 ProjectReference。
- 版本治理遵循“先发布，再升级消费方”的顺序，避免引用漂移。

## 影响

### 正向影响

- Repository 代码结构与 dawning 主仓库一致，review 成本降低。
- Infrastructure 中 SQL 样板大幅减少，可维护性提升。
- Domain 聚合边界保持稳定，不因持久化技术侵入。

### 代价与风险

- 迁移期内存在“双栈并存”复杂度（Dapper + Dawning.ORM.Dapper）。
- 需要维护跨仓库包发布节奏（发布凭据、版本推进、安装窗口）。

## 复议触发条件

参见 front matter `adr_revisit_when` 字段，正文不重复。

## 相关页面

- [ADR-024 SQLite/Dapper 通电](sqlite-dapper-bootstrap-and-schema-init.md)
- [ADR-026 Inbox V0 数据契约与捕获面](inbox-v0-capture-and-list-contract.md)
- [ADR-032 Chat V0：分屏 + SSE + 持久化](chat-v0-streaming-and-persistence.md)
- [ADR-033 Memory Ledger V0](memory-ledger-v0-schema-and-storage.md)
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)
