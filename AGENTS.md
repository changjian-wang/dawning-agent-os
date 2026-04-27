# Agent Instructions — Dawning Agent OS Wiki

> 这是一个 LLM-Wiki 仓库。Agent（任何模型 / 任何工具）在执行写操作前，**必须**按本文件指引读取权威规则。
> 本文件只做路由。**与权威规则冲突时，权威规则胜出。**

## 角色

- **人类**：策展原始资料（往 `docs/raw/` 放论文 / 博文 / 仓库分析 / 官方文档）。
- **Agent**：读资料 → 编译 / 维护 `docs/pages/` 下的 wiki 页面。
- **本文件**：告诉你（agent）该去哪里读规则、不能做什么。

## 写操作前的强制阅读顺序

1. [`docs/PURPOSE.md`](./docs/PURPOSE.md) — 确认本次操作涉及的内容是否在 wiki 收录范围内。**不在范围则停止并告诉人类。**
2. [`docs/SCHEMA.md`](./docs/SCHEMA.md) — 结构契约：目录、页面类型、frontmatter、模板、流程、红线。

如果上述任一文件与本文件冲突，以那两份为准。本文件只负责把你导向那两份。

## 硬红线（与 `docs/SCHEMA.md §10` 一致；详细规则在 SCHEMA）

- 永远不修改 `docs/raw/` 下的任何文件。
- 永远不创建无完整 frontmatter 的 wiki 页。
- 永远不发明新的 `type` / `subtype` / `tag`，须先改 `docs/SCHEMA.md`。
- 永远不删除 wiki 页；用 `status: archived` + `archived_reason` 替代。
- 永远不新增顶层目录，须先改 `docs/SCHEMA.md`。
- 不收录 `docs/PURPOSE.md` 范围之外的资料。
- 不手动维护 `docs/overview.md` / `docs/log.md`，它们是脚本派生物。

## 不在范围内的事

- 修改本仓库的应用代码（本仓库目前是文档与研究知识库，不是源代码项目）。
- 输出绕过 SCHEMA 结构的"长答案"——有持续价值的回答应回写为页面。

## 不确定时

停下来问人类，不要自行扩张规则、目录或类型。

---

*更新：2026-04-27 | 关于具体规则细节：见 `docs/SCHEMA.md`。关于 wiki 范围：见 `docs/PURPOSE.md`。*
