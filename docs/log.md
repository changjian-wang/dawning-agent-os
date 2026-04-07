# 操作日志

> 追加式记录。每次 Ingest / Query / Lint 操作追加一条。
> 格式：`## [日期] 操作类型 | 标题`

---

## [2026-04-07] init | Wiki 结构初始化

- 操作：从平面文档结构迁移到 LLM Wiki 三层架构
- 模式参考：[Karpathy LLM Wiki](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)
- 创建 SCHEMA.md：定义 wiki 结构约定、操作工作流、命名规范
- 创建目录结构：raw/（6 子目录）、entities/、concepts/、comparisons/、decisions/、synthesis/
- 迁移现有文档到对应分类

## [2026-04-07] ingest | arXiv 2603.18743 Memento-Skills

- 来源：`raw/papers/memento-skills-2603.18743.md`（待补充原文）
- 贡献页面：`decisions/roadmap-90-days.zh-CN.md`（路线图基于该论文设计）
- 关键要点：Agent-designing-agent、Stateful Prompts、Read-Write Reflective Learning

## [2026-04-07] ingest | 6 大 Tier-1 Agent 框架

- 来源：GitHub 源码分析（MAF、SK、LangGraph、CrewAI、OpenAI SDK、Google ADK）
- 新建页面：`entities/frameworks/` 下 6 个框架实体页
- 更新页面：`comparisons/agent-framework-landscape.zh-CN.md`
- 关键要点：MAF 25+ NuGet 包、SK Abstractions/Core 分离、LangGraph Pregel 引擎 + checkpoint 三件套、CrewAI Flow 装饰器、OpenAI SDK 扁平极简、Google ADK ~25 模块最丰富

## [2026-04-07] ingest | Karpathy LLM Wiki 模式

- 来源：`raw/articles/karpathy-llm-wiki.md`（待补充原文）
- 新建页面：`concepts/llm-wiki-pattern.zh-CN.md`
- 关键要点：编译优于检索、三层架构（Raw/Wiki/Schema）、Ingest/Query/Lint 三种操作、维护成本归零的洞察
