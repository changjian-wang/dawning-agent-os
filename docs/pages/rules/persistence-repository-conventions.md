---
title: Rule 持久化仓储开发规范（Dawning.ORM.Dapper）
type: rule
subtype: convention
canonical: true
summary: dawning-agent-os 的 Infrastructure 持久化仓储统一采用 Dawning.ORM.Dapper 风格：持久化实体 + ORM 扩展 + Rehydrate 映射，禁止把 ORM 注解泄漏到 Domain 聚合。
tags: [agent, process]
sources: []
created: 2026-05-07
updated: 2026-05-07
verified_at: 2026-05-07
freshness: evergreen
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/persistence-repository-style-dawning-orm-dapper.md, pages/adrs/sqlite-dapper-bootstrap-and-schema-init.md, pages/adrs/inbox-v0-capture-and-list-contract.md]
part_of: [pages/hubs/agent-os.md]
level: 强制
---

# Rule 持久化仓储开发规范（Dawning.ORM.Dapper）

> dawning-agent-os 的 Infrastructure 持久化仓储统一采用 Dawning.ORM.Dapper 风格：持久化实体 + ORM 扩展 + Rehydrate 映射，禁止把 ORM 注解泄漏到 Domain 聚合。

## 规则

在 `src/Dawning.AgentOS.Infrastructure/Persistence/**` 下实现仓储时，必须遵守：

- 每个聚合对应独立持久化实体（PO），并使用 `[Table]`、`[ExplicitKey]`、`[Column]` 显式声明映射。
- Repository 默认使用 Dawning.ORM.Dapper 扩展方法（`InsertAsync`、`UpdateAsync`、`DeleteAsync`、`Builder` 链式查询等）。
- **单条按主键加载请使用 `Builder<TEntity>().Where(x => x.Id == idValue).FirstOrDefaultAsync()`，禁止使用 `connection.GetAsync<T>(id)`** —— 后者的实现走 `dynamic` CallSite，运行时 binder 会把返回值转回 `T?` 时抛 `RuntimeBinderException: Cannot implicitly convert type 'object' to 'T'`。Builder 路径是静态泛型，不存在该问题。
- Domain 聚合只能保留行为字段与行为方法，不得添加 ORM 注解，不得为了持久化放开不必要的 setter。
- Repository 读取路径必须通过 `Rehydrate(...)` 还原聚合，不能在加载路径触发领域事件。
- 时间与主键序列化规则必须与 migration 契约一致（当前 inbox/chat/memory 以 ISO-8601 字符串落库）。
- Dapper 与 Dawning.ORM.Dapper 允许在 Infrastructure 迁移期并存；Application/Domain/Api 仍禁止引用持久化包。

## 正例

- `InboxRepository`：`InboxItemEntity` 负责表映射，`InboxItem.Rehydrate(...)` 负责聚合恢复。
- 查询分页优先使用 `Builder<TEntity>().OrderBy(...).Skip(offset).Take(limit).ToListAsync()`，避免内联 SQL 常量；计数使用 `Builder<TEntity>().CountAsync()`。
- 单条按主键加载使用 `Builder<TEntity>().Where(x => x.Id == idValue).FirstOrDefaultAsync()`。

## 反例

- 在 Domain 聚合上直接贴 `[Table]`、`[Column]`。
- Repository 中持续新增拼接 SQL 字符串作为默认实现。
- 为了让 ORM 直灌对象，把聚合核心字段改成 public setter。
- 使用 `connection.GetAsync<T>(id)`（dynamic CallSite 运行时报错；改用 Builder.Where + FirstOrDefaultAsync）。

## 相关页面

- [ADR-036 持久化仓储风格统一：Infrastructure Repository 采用 Dawning.ORM.Dapper](../adrs/persistence-repository-style-dawning-orm-dapper.md)
- [ADR-024 SQLite/Dapper 通电](../adrs/sqlite-dapper-bootstrap-and-schema-init.md)
- [ADR-026 Inbox V0 数据契约与捕获面](../adrs/inbox-v0-capture-and-list-contract.md)
