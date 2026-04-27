---
name: wiki-lint
description: |
  Use when: 用户请求健康检查、可读性体检、矛盾检测、孤立页扫描、frontmatter 校验、受控 tag 校验、canonical 唯一性检查、链接路径检查、stale 检查
  Don't use when:
    - 提供新资料要归档（→ wiki-ingest）
    - 要回答问题（→ wiki-query）
    - 仅做单页编辑无系统性检查需求
  Inputs: 检查范围（全量 / 子目录 / 指定标签）
  Outputs: 结构化报告（阻断/重要/提示三档）、可选修复建议
  Success criteria: 未触碰 raw/、未手维 overview.md/log.md、输出分档报告、物理删除 0 个页
---

# wiki-lint

## Use when

- 用户请求健康检查、可读性体检、矛盾检测、孤立页扫描、frontmatter 校验。
- 关键词：lint, 健康检查, 可读性, 矛盾, 孤立页, stale, frontmatter, 体检, 复查。

## Don't use when

- 用户提供新资料要归档（→ `wiki-ingest`）。
- 用户提问要回答（→ `wiki-query`）。
- 仅做单页编辑，无系统性检查需求。

## Authority

- 操作前必须读取 `docs/PURPOSE.md` 与 `docs/SCHEMA.md`。
- 与本技能冲突时，以 `docs/SCHEMA.md` 为准。
- 不手动维护 `docs/overview.md` 与 `docs/log.md`；它们是脚本派生物。

## Inputs

- 检查范围：全量 / 指定子目录 / 指定标签。
- 是否同时执行修复：默认先报告；阻断项可直接修复，重要项先征求用户意见。

## Steps

### 1. 扫描清单（按优先级）

| # | 检查项 | 判定 | 修复策略 |
|---|---|---|---|
| 1 | frontmatter 缺失/不完整 | wiki 页缺 SCHEMA §4 必填字段；`rule/adr` 缺类型专属字段 | 补齐；缺来源且无法追溯时标记为重要问题，不编造 |
| 2 | raw/ 被改动 | git 显示 `docs/raw/` 有 modify/add/delete | 停止并报告；不要自行修改或回滚用户资料 |
| 3 | 受控枚举违规 | `type/subtype/tag/status/freshness/adr_status/level` 不在 SCHEMA 允许集合内 | 修改为既有枚举；确需新增先改 SCHEMA |
| 4 | canonical 冲突 | 同一主题多页 `canonical: true` | 选一页保留 canonical，其余改 false 并链接到 canonical |
| 5 | 链接字段无效 | `supersedes/related/part_of` 不是相对 `docs/` 的真实路径 | 改成真实路径；找不到则报告 |
| 6 | 孤岛页面 | 非 hub 页没有来自 hub 或同类型页的入站链接 | 在最相关 hub 或同类型页补链接；不删除页面 |
| 7 | 过时声明 | `freshness: volatile` 且 `verified_at` 超过 90 天，或被新资料取代 | 更新 `verified_at`；无法验证则 `status: stale` |
| 8 | archived 主入口 | `status: archived` 页面仍被作为主入口引用 | 改引用到 active/canonical 页；保留 archived 页 |
| 9 | superseded ADR 断链 | `adr_status: superseded` 未被新 ADR 的 `supersedes` 指向 | 在新 ADR 补 `supersedes`，或报告缺失的新 ADR |
| 10 | 规模失控 | 一级章节 > 8 或单页同时讲多个主题 | 拆分或转 hub；按内容判断，不按硬字数 |
| 11 | 命名违规 | wiki 页面文件名非 `kebab-case.md` | 重命名并修所有入站链接 |

### 2. 输出报告（先报告，再修复）

```markdown
## Lint 报告 [YYYY-MM-DD]

### 阻断（必须修）
- [ ] {问题} — {页面}

### 重要（建议修）
- [ ] ...

### 提示（可延后）
- [ ] ...
```

### 3. 修复执行

- 阻断项本次修复，除非需要人类提供缺失事实。
- 重要项先征求用户意见。
- 不物理删除页面；废弃内容用 `status: archived` + `archived_reason`。
- 不修改 `docs/raw/`。
- 不手动追加 `docs/log.md`，不手动维护 `docs/overview.md`。

## Success criteria

- [ ] 输出结构化报告（阻断/重要/提示三档）。
- [ ] 未触碰 `docs/raw/`。
- [ ] 未手维 `docs/overview.md` / `docs/log.md`。
- [ ] 修复后所有受影响 wiki 页 `updated` 字段刷新。
- [ ] 物理删除 0 个页面。

## Anti-patterns

- ❌ 边查边改不输出报告（用户失去审查机会）。
- ❌ 物理删除"无价值"页面。
- ❌ 手动维护 `overview.md` / `log.md`。
- ❌ 按旧版规则检查 `docs/index.md` 漂移或双语 twin 同步。
- ❌ 使用 Obsidian wikilink 作为 frontmatter 链接字段。

## References

- 权威范围：`docs/PURPOSE.md`
- 权威结构：`docs/SCHEMA.md`
