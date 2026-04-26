---
name: wiki-query
description: |
  Use when: 用户对 wiki 内容提问、要求综合 / 对比 / 综述 已有页面
  Don't use when:
    - 提供了新原始资料要归档（→ wiki-ingest）
    - 要求修复 wiki 健康问题（→ wiki-lint）
    - 问题与 wiki 内容无关
  Inputs: 用户问题或综合需求
  Outputs: 带引用的回答；如有持久价值，回写到 synthesis/comparisons/decisions 或更新现有页
  Success criteria: 所有判断附引用、已评估回写价值、docs/log.md 追加 query 条目、未修改 raw/
---

# wiki-query

## Use when
- 用户对 wiki 内容提出问题，或要求综合/对比已有页面
- 关键词：查询 wiki, 回答, 综合, 综述, query wiki, 对比, 比较, 找一下

## Don't use when
- 用户提供了新的原始资料要归档（→ `wiki-ingest`）
- 用户要求修复 wiki 健康问题（→ `wiki-lint`）
- 问题与 wiki 内容无关（直接回答即可，无需走流程）

## Inputs
- 用户的问题或综合需求

## Steps

1. **从 `docs/index.md` 入手定位**
   - 不要直接 grep 整个 `docs/`，先看 index 分类
   - 必要时辅以关键词检索 `entities/ concepts/ comparisons/ synthesis/`

2. **深读相关页面**
   - 顺着 wikilink 扩展 1–2 跳
   - 对每个引用回到 `sources` 字段，必要时回查 `raw/`

3. **带引用综合回答**
   - 每个关键判断标注来源页面：`见 [[页面名]]` 或 `[来源](../docs/raw/.../x.md)`
   - 区分"wiki 已有结论 / 本次推论 / 未覆盖"
   - 如发现矛盾，明确指出并触发 `wiki-lint` 待办

4. **判断是否回写**（关键步骤，**不要跳过**）
   - 若回答包含**新综合 / 新对比 / 新决策 / 持久结论** → 必须回写
   - 回写规则：
     - 新综合 → `synthesis/`
     - 新对比 → `comparisons/`
     - 新决策 → `decisions/`
     - 对已有页的补充 → 直接更新该页（`updated` 字段同步）
   - 回写页面必须满足 `wiki-ingest` 的 frontmatter 与可追溯要求

5. **追加 `docs/log.md`**
   ```markdown
   ## [YYYY-MM-DD] query | {问题摘要}
   - 查阅页面：{列表}
   - 回答要点：{1-3 句话}
   - 回写页面：{列表，若无写"无"}
   ```

## Success criteria
- [ ] 回答中所有判断都附引用
- [ ] 已识别"是否需要回写"并执行
- [ ] 回写页面（如有）满足 frontmatter 规范
- [ ] `docs/log.md` 已追加 query 条目
- [ ] 未修改 `docs/raw/`

## Anti-patterns
- ❌ 不查 index 直接全文搜索
- ❌ 答完就走，不评估回写价值（**探索成果会丢失**）
- ❌ 回写时省略 frontmatter 或来源

## References
- 权威规则：[docs/SCHEMA.md](../../../docs/SCHEMA.md) §4.2
- 模式说明：[docs/concepts/00-foundations/llm-wiki-pattern.zh-CN.md](../../../docs/concepts/00-foundations/llm-wiki-pattern.zh-CN.md) §3.2（"探索如同摄入的资料一样产生复利"）
