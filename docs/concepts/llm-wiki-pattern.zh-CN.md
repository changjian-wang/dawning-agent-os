# LLM Wiki 模式：编译式知识管理

> 来源：[Andrej Karpathy — LLM Wiki](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)（2026-04-04）
>
> 本文分析 LLM Wiki 模式的核心思想，并映射到 Dawning Agent OS 的存储层（Memory Plane）设计方向。
>
> OS 类比：LLM Wiki = 文件系统。见 [[concepts/agent-os-architecture.zh-CN]]。

---

## 1. 核心理念：编译，而非检索

传统 RAG 每次查询从原始文档重新发现知识——没有积累，没有综合，没有交叉引用。

LLM Wiki 的核心转变：

> **知识被编译一次，然后增量维护。而非每次查询重新推导。**

LLM 阅读文档、提取关键信息、整合到现有 wiki 中——更新实体页面、修订摘要、标注矛盾、强化或挑战正在演进的综合分析。

关键隐喻：Obsidian 是 IDE；LLM 是程序员；Wiki 是代码库。

---

## 2. 三层架构

![LLM Wiki 三层架构](../images/concepts/01-llm-wiki-three-layer.png)

| 层 | 所有者 | 可变性 | 说明 |
|---|--------|--------|------|
| **Raw Sources** | 人类 | 不可变 | 真实来源（articles, papers, images, data） |
| **Wiki** | LLM | 持续更新 | 编译产物：摘要、实体页、概念页、比较、综述 |
| **Schema** | 人类 + LLM 共演 | 版本化更新 | 定义 wiki 结构、约定和工作流的配置文件 |

---

## 3. 三种操作

### 3.1 Ingest（摄入）

1. LLM 阅读原始资料
2. 与人类讨论关键要点
3. 在 wiki 中写摘要页
4. 更新 `index.md`
5. 更新相关实体页和概念页（一个资料可能触及 10–15 个页面）
6. 追加 `log.md` 条目

**人机分工**：人类负责策展来源和思考方向。LLM 负责摘要、交叉引用、归档和簿记。

### 3.2 Query（查询）

1. LLM 读取 `index.md` 找到相关页面
2. 深入阅读相关页面
3. 综合回答（附引用）
4. 好的回答回写为新 wiki 页面（知识复利）

> 探索产生的分析、比较、发现不应消失在聊天历史中——它们应回写到 wiki。**探索如同摄入的资料一样产生复利。**

### 3.3 Lint（健康检查）

定期维护：页面间矛盾检测、过时声明、孤立页面、缺失交叉引用、可通过 Web 搜索填补的数据空白、LLM 建议新的调查问题和来源。

---

## 4. 索引机制

| 文件 | 导向 | 用途 |
|------|------|------|
| `index.md` | 内容导向 | wiki 全目录，按分类组织。~100 资料规模下够用，无需 embedding |
| `log.md` | 时间线导向 | 追加式操作记录，格式如 `## [2026-04-02] ingest \| Title` |

规模扩大后引入语义搜索（如 [qmd](https://github.com/tobi/qmd)：本地 BM25 + 向量搜索 + LLM 重排）。

---

## 5. 为什么有效

> **人类放弃维护 wiki 是因为维护成本增长快于价值。LLM 不厌烦、不遗忘交叉引用、一次能更新 15 个文件。维护成本近乎为零。**

这与 Vannevar Bush 1945 年的 [Memex](https://en.wikipedia.org/wiki/Memex) 高度一致——一个私有的、主动策展的知识存储。Bush 无法解决的问题（谁来做维护）被 LLM 解决了。

---

## 6. 应用场景

| 场景 | 描述 |
|------|------|
| 个人知识管理 | 目标、健康、自我提升 — 归档日记、文章、播客笔记 |
| 研究 | 数周/数月深入一个主题 — 渐进式构建综合 wiki |
| 读书 | 按章节归档，构建角色页、主题页、情节线索页 |
| 团队/企业 | 内部 wiki，由 Slack、会议纪录、项目文档喂养 |
| 竞品分析 | 持续积累竞品信息并保持结构化 |

---

## 7. 对 Dawning Agent OS 的映射

### 7.1 架构映射

LLM Wiki 三层架构直接映射到 Dawning 的存储层（Memory Plane）：

![LLM Wiki → Dawning Agent OS 架构映射](../images/concepts/02-llm-wiki-dawning-mapping.png)

### 7.2 与 Memento-Skills 的关系

LLM Wiki 的"编译式知识管理"与 Memento-Skills（arXiv:2603.18743）的技能自演化是同一洞察的两个面：

| 维度 | LLM Wiki | Memento-Skills |
|------|----------|----------------|
| 积累对象 | 结构化知识页面 | 结构化技能工件 |
| 增量更新 | Ingest 新资料 → 更新 wiki 页面 | 反思执行轨迹 → 更新技能 |
| 质量维护 | Lint → 矛盾 / 孤立 / 过时 | 质量门禁 → 回归 / 策略 / lint |
| 回写机制 | 好的 Query 回写为 wiki 页面 | 成功的执行模式回写为技能改进 |
| 索引/路由 | index.md → 语义搜索 | Skill Router → top-k 评分 |

### 7.3 记忆面的定位

LLM Wiki 模式是**知识层**的实现模式，与 Memento-Skills 的**技能层**并行互补：

```
记忆面 (Memory Plane)
├── 短期状态 (Short-term)      ← 会话级消息历史（→ 上下文管理）
├── 知识层 (Knowledge Layer)   ← LLM Wiki 模式
│   ├── Raw Sources            ← 不可变原始资料
│   ├── Compiled Wiki          ← 编译产物
│   └── Knowledge Pipeline     ← Ingest / Query / Lint
└── 技能层 (Skill Layer)       ← Memento-Skills 模式
    ├── Skill Registry         ← 技能工件仓库
    ├── Skill Router           ← 上下文感知技能选择
    └── Skill Evolution        ← 反思式技能改进
```

> 具体接口设计将在路线图对应阶段（SC-8 / SC-1.3）的 RFC 中定义。

### 7.4 实现优先级

> **状态：待定** — 长期记忆方案仍在评估中，以下阶段划分仅为参考方向，不纳入近期开发计划。

| 阶段 | 内容 | 路线图对齐 |
|------|------|-----------|
| Phase 1 | Raw Store + 基底编译管道 | SC-8.1 / SC-8.2 |
| Phase 2 | Compiled Store + Ingest/Query | SC-8.3 / SC-8.4 |
| Phase 3 | Lint + Schema 共演化 + 向量索引 | SC-8.5 / SC-1.3 |

### 7.5 规模化挑战

来自 Karpathy gist 评论区的关键问题：

| 挑战 | Dawning 应对方向 |
|------|----------------|
| 索引膨胀（100+ 页面溢出上下文） | 分层索引 + 语义搜索 |
| 知识形成标准 | 可配置重要性阈值 + 效用信号反馈 |
| 知识过期与矛盾传播 | 矛盾检测 + 版本化 + 溯源图 |
| 多 Agent 共享 | Scope 隔离 + RBAC |

---

## 8. 与现有工具对比

| 工具/模式 | 与 LLM Wiki 差异 |
|----------|-----------------|
| RAG | 每次查询重新检索，无编译无积累 |
| NotebookLM | 每次从源文档重推导，无持久化 wiki |
| Zettelkasten | 原子笔记 + 链接，但需人类手动维护 |
| [qmd](https://github.com/tobi/qmd) | BM25 + 向量 + LLM 重排，适合大规模 wiki 的搜索引擎 |
| [OMEGA](https://github.com/omega-memory/omega-memory) | 本地向量搜索 + 强度衰减 + 死知识修剪 |

---

## 延伸阅读

- [LLM 技术原理](llm-fundamentals.md) — Token、API、采样等基础概念
- [上下文管理](context-management.md) — 五种上下文管理流派 + 双层记忆架构
- [Agent Loop](agent-loop.md) — ReAct / Plan-and-Execute / Reflexion 执行模式

---

*最后更新：2026-04-11*
