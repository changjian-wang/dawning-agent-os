---
name: wiki-lint
description: |
  Use when: 用户请求健康检查、可读性体检、矛盾检测、孤立页扫描、frontmatter 校验、index 漂移检测
  Don't use when:
    - 提供新资料要归档（→ wiki-ingest）
    - 要回答问题（→ wiki-query）
    - 仅做单页编辑无系统性检查需求
  Inputs: 检查范围（全量 / 子目录 / 指定标签）
  Outputs: 结构化报告（阻断/重要/提示三档）、可选修复、docs/log.md 追加 lint 条目
  Success criteria: 未触碰 raw/、输出分档报告、修复后 updated 刷新、物理删除 0 个页
---

# wiki-lint

## Use when
- 用户请求健康检查、可读性体检、矛盾检测、孤立页扫描、frontmatter 校验
- 关键词：lint, 健康检查, 可读性, 矛盾, 孤立页, stale, frontmatter, 体检, 复查

## Don't use when
- 用户提供新资料要归档（→ `wiki-ingest`）
- 用户提问要回答（→ `wiki-query`）
- 仅做单页编辑无系统性检查需求

## Inputs
- 检查范围：全量 / 指定子目录 / 指定标签
- 是否同时执行修复（默认：先报告后确认）

## Steps

### 1. 扫描清单（按优先级）

| # | 检查项 | 判定 | 修复策略 |
|---|---|---|---|
| 1 | frontmatter 缺失/不完整 | 缺 `title/type/tags/sources/created/updated/status` 任一字段 | 补齐；缺 `sources` 时回 raw/ 找；找不到打 `status: stale` |
| 2 | 孤立页面 | 全 `docs/` 中无任何 `[[页名]]` 入站 | 在最相关页补 wikilink；确无价值则 `status: archived` |
| 3 | 矛盾页面 | 同主题不同结论 | 双向加 `> ⚠️ 与 [[X]] 存在分歧：…`；新主流结论页加 `status: active`，旧的标 `stale` |
| 4 | 过时声明（stale） | 被新资料取代 | 顶部加 `> 已被 [[Y]] 取代（YYYY-MM-DD）`；`status: stale` 或 `archived` |
| 5 | 缺失交叉引用 | 提到具名实体却未 wikilink | 补 `[[...]]` |
| 6 | 重复主题 | 多页讲同一件事 | 选 canonical 页合并；其余保留 frontmatter + 重定向占位 |
| 7 | 超长页（> 400 行 / 多主题） | 单页混杂 | 拆分；旧页保留 + wikilink 指向新页 |
| 8 | 索引漂移 | `docs/index.md` 与实际页面不一致 | 同步条目 |
| 9 | 命名违规 | 非 `kebab-case.md`、中文版未带 `.zh-CN` | 重命名（注意修所有反向链接） |
| 10 | raw/ 被改动 | git 显示 raw/ 有 modify | **立即回滚**，记录到 log |

### 2. 输出报告（先报告，再修复）

格式：

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
- 阻断项必须本次修
- 重要项征求用户意见
- 大量重复修复可批量

### 4. 追加 `docs/log.md`
```markdown
## [YYYY-MM-DD] lint | 健康检查
- 检查页面数：{n}
- 发现问题：{阻断 a / 重要 b / 提示 c}
- 修复：{列表}
- 待办：{列表}
```

## Success criteria
- [ ] 输出结构化报告（阻断/重要/提示三档）
- [ ] 未触碰 `docs/raw/`
- [ ] 修复后所有受影响页 `updated` 字段刷新
- [ ] 物理删除 0 个页面（用 `status: archived`）
- [ ] `docs/log.md` 已追加 lint 条目
- [ ] 双语页面同步检查

## Anti-patterns
- ❌ 边查边改不输出报告（用户失去审查机会）
- ❌ 物理删除"无价值"页面
- ❌ 修复时新增页面但不更新 `index.md`

## References
- 权威规则：[docs/SCHEMA.md](../../../docs/SCHEMA.md) §4.3、§8
- 模式说明：[docs/concepts/00-foundations/llm-wiki-pattern.zh-CN.md](../../../docs/concepts/00-foundations/llm-wiki-pattern.zh-CN.md) §3.3
