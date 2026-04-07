# LLM Wiki — Andrej Karpathy

> Gist | 2026-04-04 | 5000+ stars
> 状态：已摄入

## 来源信息

- **作者**：Andrej Karpathy
- **链接**：https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f
- **摘入日期**：2026-04-07

## 核心思想

1. **编译优于检索**：知识被编译一次然后增量维护，而非每次查询重新推导
2. **三层架构**：Raw Sources（不可变）→ Wiki（LLM 维护）→ Schema（人+LLM 共演化）
3. **三种操作**：Ingest（摄入）、Query（查询+回写）、Lint（健康检查）
4. **索引机制**：index.md（内容目录）+ log.md（操作日志）
5. **维护成本归零**：LLM 不厌烦、不遗忘交叉引用、一次能更新 15 个文件

## 有价值的工具提及

- [qmd](https://github.com/tobi/qmd)：本地 BM25 + 向量搜索 + LLM 重排
- Obsidian Web Clipper：浏览器文章转 Markdown
- Obsidian Graph View：可视化 wiki 连接关系
- Marp：Markdown 幻灯片
- Dataview：Obsidian 插件，基于 frontmatter 查询

## 引用此资料的 Wiki 页面

- `concepts/llm-wiki-pattern.zh-CN.md` — LLM Wiki 模式概念页
