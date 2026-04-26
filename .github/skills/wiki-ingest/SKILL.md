---
name: wiki-ingest
description: |
  Use when: 用户在 docs/raw/ 新增原始资料（论文/博文/repo/官方文档）需要摇入为 wiki 页面
  Don't use when:
    - 仅回答已有问题（→ wiki-query）
    - 仅做健康检查（→ wiki-lint）
    - 需要修改 docs/raw/（禁止）
  Inputs: docs/raw/ 下的新文件路径，可选指定要点
  Outputs: 新建/更新的 wiki 页面、更新的 docs/index.md、追加的 docs/log.md 条目
  Success criteria: 未修改 raw/、frontmatter 完整、所有声明可追溯、index 与 log 均已更新
---
# wiki-ingest

## Use when
- 用户在 `docs/raw/` 下新增了文件（论文、博文、repo 笔记、官方文档等）
- 关键词：摄入, ingest, 新资料, 加入 raw, 新论文, 新博文, 新增来源

## Don't use when
- 仅回答已有问题（→ `wiki-query`）
- 仅做健康检查或修复（→ `wiki-lint`）
- 修改 `docs/raw/` 内容（**禁止**，raw 不可变）

## Inputs
- `docs/raw/{category}/{file}.md` 中的新文件路径
- 用户对要点和方向的提示（可选）

## Steps

1. **读取原始资料**（只读）
   - 路径必须在 `raw/papers|articles|repos|official|meetings|assets/` 之一
   - 如不在，先要求用户归类，不要自动创建新顶层目录

2. **识别影响面**
   - 列出本资料涉及的实体（→ `entities/`）、概念（→ `concepts/`）、对比（→ `comparisons/`）、决策（→ `decisions/`）、综合（→ `synthesis/`）
   - 一次 ingest 通常触及 5–15 个页面，**不要只写一页**

3. **创建/更新 wiki 页面**
   - 每页必须有完整 YAML frontmatter：
     ```yaml
     ---
     title: ...
     type: entity | concept | comparison | decision | synthesis
     tags: [...]
     sources: [raw/{category}/{file}.md, ...]
     created: YYYY-MM-DD
     updated: YYYY-MM-DD
     status: draft | active
     ---
     ```
   - 正文骨架：`# 标题 → 一句话摘要 → 核心内容 → 交叉引用 → 来源`
   - 内部链接用 `[[页面名]]`；引用 raw 用相对路径 `[标题](../raw/.../x.md)`
   - 中文优先（`.zh-CN.md`），如已有英文版必须同步检查

4. **更新 `docs/index.md`**
   - 新增/修改条目，保持分类一致
   - `index.md` 是 wiki 的唯一入口，**不能漏更新**

5. **追加 `docs/log.md`**
   ```markdown
   ## [YYYY-MM-DD] ingest | {资料标题}
   - 来源：`raw/{category}/{file}.md`
   - 新建页面：{列表}
   - 更新页面：{列表}
   - 关键要点：{1-3 句话}
   ```

## Success criteria
- [ ] 未修改 `docs/raw/` 任何文件
- [ ] 所有新建/更新页面有完整 frontmatter
- [ ] 每个声明可追溯到 raw/ 资料
- [ ] 至少一处交叉引用（wikilink）
- [ ] `docs/index.md` 已更新
- [ ] `docs/log.md` 已追加 ingest 条目
- [ ] 双语版本同步（如适用）

## References
- 权威规则：[docs/SCHEMA.md](../../../docs/SCHEMA.md) §1–§5、§8
- 模式说明：[docs/concepts/00-foundations/llm-wiki-pattern.zh-CN.md](../../../docs/concepts/00-foundations/llm-wiki-pattern.zh-CN.md) §3.1
