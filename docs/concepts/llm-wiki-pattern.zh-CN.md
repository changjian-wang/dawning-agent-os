# LLM Wiki 模式：编译式知识管理

> 来源：[Andrej Karpathy — LLM Wiki](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)（2026-04-04 发布，5000+ stars）
>
> 本文分析 LLM Wiki 模式的核心思想，并将其映射到 Dawning Agent Framework 的记忆面（Memory Plane）和技能自演化（Memento-Skills）设计中。

---

## 1. 核心理念：编译，而非检索

传统 RAG 在每次查询时从原始文档重新发现知识——没有积累，没有综合，没有交叉引用。每次提问都是从零开始。

LLM Wiki 的核心转变：

> **知识被编译一次，然后增量维护。而非每次查询重新推导。**

LLM 不只是索引原始文档供日后检索，而是**阅读文档、提取关键信息、将其整合到现有 wiki 中**——更新实体页面、修订主题摘要、标注新数据与旧观点的矛盾、强化或挑战正在演进的综合分析。

关键隐喻：

```
Obsidian 是 IDE；LLM 是程序员；Wiki 是代码库。
```

## 2. 三层架构

```
┌─────────────────────────────────────────────┐
│              Schema（模式定义）                │
│  CLAUDE.md / AGENTS.md — 结构约定、工作流规则    │
├─────────────────────────────────────────────┤
│              Wiki（编译产物）                   │
│  Markdown 文件集 — 摘要、实体页、概念页、综述      │
│  index.md（内容目录）+ log.md（操作日志）         │
├─────────────────────────────────────────────┤
│           Raw Sources（原始资料）               │
│  不可变的文章、论文、数据文件 — LLM 只读不写       │
└─────────────────────────────────────────────┘
```

| 层 | 所有者 | 可变性 | 说明 |
|---|--------|--------|------|
| **Raw Sources** | 人类 | 不可变 | 真实来源（articles, papers, images, data） |
| **Wiki** | LLM | 持续更新 | 编译产物：摘要、实体页、概念页、比较、综述 |
| **Schema** | 人类 + LLM 共演 | 版本化更新 | 定义 wiki 结构、约定和工作流的配置文件 |

## 3. 三种操作

### 3.1 Ingest（摄入）

导入新资料时的流程：

1. LLM 阅读原始资料
2. 与人类讨论关键要点
3. 在 wiki 中写摘要页
4. 更新 `index.md`
5. 更新相关实体页和概念页（**一个资料可能触及 10-15 个 wiki 页面**）
6. 追加 `log.md` 条目

> **关键洞察**：人类负责策展资料来源、探索方向和提出好问题。LLM 负责一切苦力活——摘要、交叉引用、归档和簿记。

### 3.2 Query（查询）

查询 wiki 时的流程：

1. LLM 读取 `index.md` 找到相关页面
2. 深入阅读相关页面
3. 综合回答（附引用）
4. **好的回答可以回写为新 wiki 页面**（知识复利）

> **关键洞察**：探索产生的分析、比较、发现不应消失在聊天历史中——它们是有价值的，应回写到 wiki 中。**你的探索如同摄入的资料一样，在知识库中产生复利。**

### 3.3 Lint（健康检查）

定期维护：

- 页面间矛盾检测
- 过时声明（已被新资料取代）
- 孤立页面（无入站链接）
- 重要概念缺少独立页面
- 缺失交叉引用
- 可通过 Web 搜索填补的数据空白
- LLM 建议新的调查问题和资料来源

## 4. 索引机制

| 文件 | 导向 | 用途 |
|------|------|------|
| `index.md` | 内容导向 | wiki 全目录，按分类组织，每页附链接和一行摘要。**~100 资料规模下够用，无需 embedding RAG** |
| `log.md` | 时间线导向 | 追加式操作记录。条目格式如 `## [2026-04-02] ingest \| Article Title`，可用 grep 解析 |

当 wiki 增长到数百页以上时，需要引入语义搜索（如 [qmd](https://github.com/tobi/qmd)：本地 BM25 + 向量搜索 + LLM 重排）。

## 5. 为什么有效

> **人类放弃维护 wiki 是因为维护成本增长快于价值。LLM 不厌烦、不遗忘交叉引用、一次能更新 15 个文件。维护成本近乎为零，wiki 因此得以持续维护。**

这个理念与 Vannevar Bush 1945 年提出的 [Memex](https://en.wikipedia.org/wiki/Memex) 高度一致——一个私有的、主动策展的知识存储，文档之间的关联与文档本身同样有价值。Bush 的愿景比 Web 更接近 LLM Wiki。他无法解决的问题（谁来做维护）被 LLM 解决了。

## 6. 应用场景

| 场景 | 描述 |
|------|------|
| 个人知识管理 | 目标、健康、心理、自我提升 — 归档日记、文章、播客笔记 |
| 研究 | 数周/数月深入一个主题 — 论文、报告、渐进式构建综合 wiki |
| 读书 | 按章节归档，构建角色页、主题页、情节线索页 |
| 团队/企业 | 内部 wiki，由 Slack 线程、会议纪录、项目文档、客户通话喂养 |
| 竞品分析 | 持续积累竞品信息并保持结构化 |

---

## 7. 对 Dawning Agent Framework 的映射

### 7.1 架构映射

LLM Wiki 的三层架构可以直接映射到 Dawning 的三面分布式架构：

```
LLM Wiki                      Dawning Agent Framework
─────────────                  ──────────────────────────
Schema（模式定义）     ───→     控制面 / IMemorySchema
Wiki（编译产物）       ───→     记忆面 / Compiled Knowledge Store
Raw Sources（原始资料）───→     记忆面 / Raw Knowledge Store
Ingest（摄入）         ───→     IKnowledgeCompiler.IngestAsync()
Query（查询）          ───→     IKnowledgeQuery.SearchAsync()
Lint（健康检查）       ───→     IKnowledgeMaintenance.LintAsync()
index.md（内容索引）   ───→     IKnowledgeIndex（小规模 → IVectorStore 大规模）
log.md（操作日志）     ───→     审计日志 / IKnowledgeLog
```

### 7.2 接口设计启示

从 LLM Wiki 模式提炼出的记忆面接口：

```
记忆面 (Memory Plane)
├── Raw Store（原始存储）
│   └── IKnowledgeSourceStore          # 原始资料的不可变存储
│       ├── AddSourceAsync()           # 添加原始资料
│       ├── GetSourceAsync()           # 读取原始资料
│       └── ListSourcesAsync()         # 列出资料
│
├── Compiled Store（编译产物）
│   └── ICompiledKnowledgeStore        # Wiki 页面的可变存储
│       ├── UpsertPageAsync()          # 创建/更新页面
│       ├── GetPageAsync()             # 读取页面
│       ├── SearchPagesAsync()         # 语义搜索页面
│       └── GetLinksAsync()            # 获取交叉引用
│
├── Knowledge Pipeline（知识管道）
│   ├── IKnowledgeCompiler             # Ingest 操作
│   │   └── IngestAsync()             # 读取原始资料 → 编译到 wiki
│   ├── IKnowledgeQuery                # Query 操作
│   │   └── SearchAsync()             # 查询 wiki → 综合回答
│   └── IKnowledgeMaintenance          # Lint 操作
│       └── LintAsync()               # 矛盾检测、孤立页、过时声明
│
├── Indexing（索引）
│   ├── IKnowledgeIndex                # 内容索引（小规模用 Markdown 目录）
│   └── IVectorStore                   # 语义搜索（大规模用向量数据库）
│
└── Schema（模式定义）
    └── IKnowledgeSchema               # 定义 wiki 结构、分类、约定
        ├── GetCategoriesAsync()       # 获取页面分类
        ├── GetConventionsAsync()      # 获取命名/链接约定
        └── EvolveAsync()             # 模式共演化（人 + LLM）
```

### 7.3 与 Memento-Skills 的关系

LLM Wiki 的"编译式知识管理"模式与 Memento-Skills（arXiv:2603.18743）的技能自演化在本质上是同一个洞察的两个面：

| 维度 | LLM Wiki | Memento-Skills |
|------|----------|----------------|
| **积累什么** | 结构化知识页面 | 结构化技能工件 |
| **增量更新** | Ingest 新资料 → 更新 wiki 页面 | 反思执行轨迹 → 更新技能 Markdown |
| **质量维护** | Lint 检查→矛盾/孤立/过时 | 质量门禁→回归/策略/lint |
| **回写机制** | 好的 Query 回答回写为 wiki 页面 | 成功的执行模式回写为技能改进 |
| **知识复利** | wiki 越用越丰富 | 技能越用越好 |
| **版本化** | Git 版本历史 | 技能修订注册表 |
| **索引/路由** | index.md → 语义搜索 | Skill Router → top-k 评分 |

**关键洞察**：Dawning 的记忆面应将 LLM Wiki 模式视为**知识层**的实现模式，与 Memento-Skills 的**技能层**并行但互补：

```
记忆面 (Memory Plane)
├── 短期状态 (Short-term State)    ← 会话级消息历史
├── 知识层 (Knowledge Layer)       ← LLM Wiki 模式
│   ├── Raw Sources               ← 不可变原始资料
│   ├── Compiled Wiki             ← 编译产物（实体页、概念页、综述）
│   └── Knowledge Pipeline        ← Ingest / Query / Lint
└── 技能层 (Skill Layer)           ← Memento-Skills 模式
    ├── Skill Registry            ← 技能工件仓库
    ├── Skill Router (Read)       ← 上下文感知技能选择
    └── Skill Evolution (Write)   ← 反思式技能改进
```

### 7.4 实现优先级

考虑到路线图中记忆面（SC-1.3 / SC-8）的设计，LLM Wiki 模式的集成应分阶段进行：

| 阶段 | 内容 | 与路线图对齐 |
|------|------|-------------|
| **Phase 1 (Day 1-30)** | Raw Store + 基底编译管道 | SC-8.1 / SC-8.2 |
| **Phase 2 (Day 31-60)** | Compiled Store + Ingest/Query | SC-8.3 / SC-8.4 |
| **Phase 3 (Day 61-90)** | Lint + Schema 共演化 + 向量索引 | SC-8.5 / SC-1.3 |

### 7.5 社区反馈中的关键挑战

Karpathy 的 gist 评论区（发布数小时内即有大量实现尝试）暴露了几个规模化挑战，对 Dawning 设计有直接参考价值：

| 挑战 | 描述 | Dawning 应对 |
|------|------|-------------|
| **索引膨胀** | `index.md` 在 100+ 页面后溢出上下文窗口 | IVectorStore 语义搜索 + IKnowledgeIndex 分层索引 |
| **知识形成标准** | 什么值得存入 wiki？什么是噪音？ | 可配置的重要性阈值 + 效用信号反馈 |
| **知识过期** | 会话 9 推翻会话 3 的结论时如何传播 | 矛盾检测 + 版本化 + 溯源图 |
| **数据溯源** | 坏信息如何追溯到源头 | Git 版本历史 + CorrelationId + 源引用链 |
| **评估指标** | 如何衡量 wiki 条目是否真正有用 | 召回率追踪 + 强度衰减 + 死知识修剪 |
| **多 Agent 共享** | 多个 Agent 共建共享知识库 | Scope 隔离（SC-2.4）+ RBAC（SC-9.1）|

---

## 8. 与现有工具的对比

| 工具/模式 | 类型 | 与 LLM Wiki 差异 |
|----------|------|-----------------|
| RAG | 检索增强生成 | 每次查询重新发现知识，无编译无积累 |
| NotebookLM | Google 产品 | 每次从源文档重推导，无持久化 wiki |
| OpenClaw Memory | Agent 记忆 | `MEMORY.md` + `~/.openclaw/`，类似但缺乏编译层和 Lint |
| Zettelkasten | 笔记方法 | 原子笔记 + 链接，但需人类手动维护 |
| Obsidian + LLM | 工具组合 | LLM Wiki 的推荐实现方式之一 |
| [qmd](https://github.com/tobi/qmd) | 搜索引擎 | BM25 + 向量 + LLM 重排，适合大规模 wiki |
| [OMEGA](https://github.com/omega-memory/omega-memory) | 记忆系统 | 本地向量搜索 + 强度衰减 + 死知识修剪 |

---

## 9. 总结

LLM Wiki 模式的三个核心贡献：

1. **维护成本归零的洞察**：人类放弃知识库不是因为不想维护，而是维护成本太高。LLM 消除了这个瓶颈。
2. **编译优于检索**：一次编译、增量维护，优于每次查询从零检索。知识产生复利。
3. **人机分工**：人类负责策展和思考；LLM 负责摘要、交叉引用、归档和簿记。

对 Dawning Agent Framework 而言，LLM Wiki 模式是记忆面知识层的理想实现模式，与 Memento-Skills 的技能层互补，共同构成"越用越聪明"的自演化 Agent 系统。

---

*文档版本：1.0 | 最后更新：2026-04-07*
