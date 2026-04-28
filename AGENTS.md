# Agent Instructions — Dawning Agent OS

> 这是 dawning-agent-os 的产品 monorepo。`docs/` 是内置 LLM-Wiki，用来保存决策记忆、外部资料消化和产品 / 架构边界。
> 本文件只做路由。**与权威规则冲突时，权威规则胜出。**

## 角色

- **人类**：策展原始资料（往 `docs/raw/` 放论文 / 博文 / 仓库分析 / 官方文档），并拍板产品 / 架构关键决策。
- **Agent**：维护 `docs/` 下的 LLM-Wiki；在代码实现阶段，也可按已落地 ADR 在 `docs/` 外实现产品代码。
- **本文件**：告诉你（agent）该去哪里读规则、不能做什么。

## docs/ 写操作前的强制阅读顺序

1. [`docs/PURPOSE.md`](./docs/PURPOSE.md) — 确认本次操作涉及的内容是否在 wiki 收录范围内。**不在范围则停止并告诉人类。**
2. [`docs/SCHEMA.md`](./docs/SCHEMA.md) — 结构契约：目录、页面类型、front matter、模板、流程、红线。

如果上述任一文件与本文件冲突，以那两份为准。本文件只负责把你导向那两份。

## 代码写操作前的路由

- 先读 [`docs/PURPOSE.md`](./docs/PURPOSE.md) 的产品契约与当前 thesis。
- 再读相关 ADR，特别是 MVP、Memory、权限 / 动作边界相关决策。
- `docs/SCHEMA.md` 只约束 `docs/` 的 wiki 结构，不约束应用代码目录。
- 后续若出现代码工程规则，应新增对应 rule / ADR，而不是把代码规范塞进 SCHEMA。

## 硬红线入口

> 本节只保留薄摘要；完整红线以 `docs/SCHEMA.md §10` 为准。

- `docs/` 写操作前先读 `docs/PURPOSE.md` 与 `docs/SCHEMA.md`。
- 不修改 `docs/raw/`，不手维 `docs/overview.md` / `docs/log.md`。
- 不写无法追溯到 `sources` 的事实性判断；查不到当前结论时返回「未收录」，不要硬编。
- 不创建违反 SCHEMA 契约的页面；类型、枚举、目录、模板、生命周期、拓扑必须按 SCHEMA。
- 不物理删除 wiki 页；退役走 `status: archived` + `archived_reason`，取代关系走 `supersedes`。
- 不确定时停下来问人类。

## 不在范围内的事

- 输出绕过 SCHEMA 结构的"长答案"——有持续价值的回答应回写为页面。
- 把 `docs/SCHEMA.md` 当成应用代码目录规范；它只约束 LLM-Wiki。

## 不确定时

停下来问人类，不要自行扩张规则、目录或类型。

---

*更新：2026-04-28 | 仓库形态见 `docs/pages/adrs/repository-shape-product-monorepo-with-wiki.md`。关于 wiki 结构：见 `docs/SCHEMA.md`。关于产品方向：见 `docs/PURPOSE.md`。*
