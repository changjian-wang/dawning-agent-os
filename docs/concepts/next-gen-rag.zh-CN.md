---
title: "下一代 RAG：GraphRAG、HippoRAG、Self-RAG、CRAG、HyDE、ColPali 深度解析"
type: concept
tags: [rag, graphrag, hipporag, self-rag, crag, hyde, colpali, agentic-rag, late-interaction]
sources: [comparisons/rag-pipeline-comparison.zh-CN.md, concepts/embedding-models.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 下一代 RAG：GraphRAG、HippoRAG、Self-RAG、CRAG、HyDE、ColPali 深度解析

> 基础 RAG（chunk → embed → 向量检索 → 拼到 prompt）已经是 2023 的事。
> 2024-2026 RAG 进入"agentic / graph / self-correcting / late-interaction / vision-native"多分支演化。
>
> 本文拆解下一代 RAG 的 8 条技术路线，覆盖模式、适用场景、工程代价、对 Dawning 的启发。

---

## 1. 为什么基础 RAG 不够

### 1.1 基础 RAG 的失败模式

```
Query: "2024 Q3 销售同比增长对我们利润率的影响"

基础 RAG：
1. chunk 召回 5 段文档
2. 可能召回：销售数据、利润公式、不相关报告
3. LLM 拼接生成答案
```

**常见问题**：

| 失败模式 | 原因 |
|---------|------|
| **召回相关但不精确** | chunk 粒度错了 |
| **丢失全局视角** | 单 chunk 看不到全文结构 |
| **多跳推理失败** | 需要 A→B→C 但只召回 A |
| **实体歧义** | "Apple" 是公司还是水果 |
| **幻觉** | 召回错也信 |
| **冗余重复** | top-k 内容高度相似 |
| **视觉丢失** | PDF 图表 OCR 后丢语义 |

---

## 2. 路线一：GraphRAG（微软）

### 2.1 核心思想

**把文档抽成知识图谱，再基于图回答**。

### 2.2 索引阶段

```
文档
  ↓ LLM 抽实体 + 关系
(Entity, Relation, Entity)
  ↓ 聚合
Knowledge Graph
  ↓ Community Detection (Leiden)
Communities (社区)
  ↓ LLM 每个社区生成摘要
Community Summaries
```

### 2.3 查询阶段

**Global Search**（全局问题）：
```
Query → 遍历所有 community summaries → map-reduce → 答案
```

适合："整个文档集的主要主题是什么？"

**Local Search**（具体问题）：
```
Query → 相关实体 → 图游走 → 邻居实体 + chunks → 答案
```

适合："X 和 Y 的关系是什么？"

### 2.4 优势

- 多跳推理强
- 全局性问题胜过基础 RAG
- 可解释（图可视化）

### 2.5 劣势

- **索引成本巨大**（每文档多次 LLM 抽取）
- 实体抽取质量限制上限
- Community 粒度调优复杂
- 增量更新困难

### 2.6 变体 / 相关项目

- **LightRAG**（港大）：简化版，双层检索
- **nano-graphrag**：轻量实现
- **GraphRAG-Python**（Neo4j）：Neo4j 集成
- **Microsoft GraphRAG** 原版

### 2.7 适用场景

- 调研报告、文献综述
- 企业全景问答
- 案例/事件分析
- 法律判例推理

**不适用**：
- 高频 FAQ
- 实时更新文档
- 简单关键词查找

---

## 3. 路线二：HippoRAG（仿海马体）

### 3.1 核心思想

受人脑海马体长期记忆启发：**先抽图 + Personalized PageRank 查询**。

### 3.2 算法

```
索引：
  文档 → 三元组 → 图
  
查询：
  Query → 抽实体 → 作为 PageRank 起点 → 图上扩散
  → 加权相关节点 → 召回对应文档
```

**关键创新**：用 Personalized PageRank 替代暴力图游走。

### 3.3 与 GraphRAG 对比

| 维度 | GraphRAG | HippoRAG |
|------|----------|----------|
| 索引 | Community 摘要 | 仅三元组 |
| 索引成本 | 极高 | 中 |
| 查询 | map-reduce | PageRank |
| 多跳 | 强 | 更强（数学严格） |
| 增量更新 | 难 | 较易 |

### 3.4 HippoRAG 2（2025）

- 改进 embedding 与 PPR 融合
- 支持更大规模
- 性能接近/超越 GraphRAG

---

## 4. 路线三：Self-RAG / 自反思 RAG

### 4.1 核心思想

**让模型决定：需要检索吗？检索得对吗？生成对吗？**

### 4.2 流程

```
Input
  ↓
[Retrieve?] (模型自判)
  ├─ No → 直接生成
  └─ Yes → 检索
         ↓
       [Relevant?] (模型自判每段)
         ├─ No → 丢弃
         └─ Yes → 生成
                ↓
              [Supported?] (生成是否有依据)
                ├─ No → 重生成 / 重检索
                └─ Yes → 
                       [Useful?]
                         → 输出
```

### 4.3 实现

- 训练 / 微调一个 critic 模型
- 用特殊 token（[RETRIEVE] / [REL] / [SUP] / [USE]）
- 生成过程中多次决策

### 4.4 优势

- 减少无用检索
- 检出 hallucination
- 可迭代改进

### 4.5 劣势

- 需要微调模型（或 few-shot）
- 延迟高
- 工程复杂

---

## 5. 路线四：Corrective RAG (CRAG)

### 5.1 核心思想

**检索后评估质量：好就用，差就换策略**。

### 5.2 流程

```
Query
  ↓ 检索
Docs
  ↓ Retrieval Evaluator (小模型)
     ├─ Correct → 用
     ├─ Ambiguous → 混合
     └─ Incorrect → Web 搜索兜底
  ↓
知识精炼（Knowledge Refinement）
  ├─ Decompose（切小）
  ├─ Filter（去噪）
  └─ Recompose（重组）
  ↓
Generate
```

### 5.3 与 Self-RAG 对比

| 维度 | Self-RAG | CRAG |
|------|----------|------|
| 评估者 | 大模型自判 | 专门 Evaluator |
| 兜底 | 重试 | Web 搜索 |
| 是否需要微调 | 需要 | 不一定 |
| 成本 | 高 | 中 |

---

## 6. 路线五：HyDE（Hypothetical Document Embeddings）

### 6.1 核心思想

**不要用 query 去匹配文档，用"假想答案"去匹配**。

### 6.2 流程

```
Query
  ↓ LLM 生成假想答案 (hypothetical document)
Hypothetical Answer
  ↓ Embed
Vector
  ↓ 向量检索
Real Docs
  ↓ 拼到 prompt（用真实文档不是假想）
Generate Final Answer
```

### 6.3 为什么有效

Query 和答案的 embedding 空间分布可能差异大。假想答案与真实答案分布一致，匹配更准。

### 6.4 优势

- 零训练
- 对冷启动有效
- 与其他技术可叠加

### 6.5 劣势

- 多一次 LLM 调用（延迟 + 成本）
- 假想质量差会误导
- 对良好设计的 embedding 收益递减

---

## 7. 路线六：RAG-Fusion / Multi-Query

### 7.1 核心思想

**一个 query 生成多个变体，分别检索后融合**。

### 7.2 流程

```
Query
  ↓ LLM 生成 3-5 个变体
[Q1, Q2, Q3, Q4, Q5]
  ↓ 分别向量检索
[Docs1, Docs2, ...]
  ↓ Reciprocal Rank Fusion (RRF)
Ranked Unique Docs
  ↓
Generate
```

### 7.3 RRF 公式

$$
\text{RRFscore}(d) = \sum_{q \in Q} \frac{1}{k + \text{rank}(d, q)}
$$

其中 `k=60`（经验值）。

### 7.4 优势

- 对 query 表述敏感问题好
- 可并行
- 不需要微调

### 7.5 劣势

- N 倍检索成本
- 需要好的变体生成

---

## 8. 路线七：ColPali / ColBERT v2（Late Interaction）

### 8.1 核心思想

**不把 document 压成一个向量，保留每个 token 向量，查询时 late interaction**。

### 8.2 ColBERT / ColBERT v2

```
文档 → BERT → 每个 token 一个向量 → 存储
查询 → BERT → 每个 token 一个向量
相似度 = Σ max_sim(query_token_i, doc_tokens)
```

**优势**：
- 保留细粒度语义
- MRR / nDCG 显著高于单向量

**劣势**：
- 存储 10-100x（每 token 一个向量）
- 检索要 MaxSim 计算

### 8.3 ColPali（2024）

**创新**：**直接用文档页面截图**，不 OCR。

```
PDF 页面图像
  ↓ Vision Encoder (PaliGemma)
  ↓ 每个 patch 一个向量
Patch Vectors
  ↓ 存储

Query (text)
  ↓ Text Encoder
  ↓ 每个 token 一个向量
Token Vectors
  ↓ Late Interaction
召回相关页面图像
  ↓ 传给 VLM (Claude/GPT-4o)
生成答案
```

### 8.4 ColPali 优势

- **零 OCR 管线** → 简化
- **视觉保真**（图表、公式、布局）
- 对复杂 PDF（金融报表 / 学术论文）表现惊艳

### 8.5 ColPali 代价

- 存储巨大
- 需要多向量检索能力（Vespa / Qdrant / LanceDB / PLAID）
- 需要 VLM 消费

### 8.6 相关项目

- **ColBERT** / **ColBERT v2**
- **ColPali** / **ColQwen2**
- **PLAID** 索引引擎
- **RAGatouille**（ColBERT 封装）

---

## 9. 路线八：Agentic RAG

### 9.1 核心思想

**让 Agent 决定如何检索**——不是固定管线。

### 9.2 典型流程

```
Agent 收到 Query
  ↓
Agent 判断：
  - 需要检索什么？
  - 用哪个 index？（多个数据源）
  - 用哪个工具？（向量、SQL、Web、图）
  - 是否需要多跳？
  - 结果够吗？要再检索吗？
  ↓
工具调用（retrieve_docs / sql_query / web_search / graph_query ...）
  ↓
Self-eval
  ↓
Generate or 继续检索
```

### 9.3 与传统 RAG 对比

| 维度 | 传统 RAG | Agentic RAG |
|------|----------|-------------|
| 检索策略 | 固定 | 动态 |
| 数据源 | 单一 | 多样（向量 / SQL / API / Web） |
| 多跳 | 弱 | 强 |
| 延迟 | 低 | 高 |
| 成本 | 低 | 高 |
| 可解释 | 低 | 高（trace 可见） |

### 9.4 工程要点

- 工具按数据源分（get_faq, query_sales_db, search_legal...）
- Query router 判断工具
- Reflection 循环（结果够不够）
- 预算（最多检索 N 次）

### 9.5 典型框架支持

- LangGraph Agentic RAG 模板
- LlamaIndex Agent + Query Engine
- CrewAI Retrieval Agent

---

## 10. 其他前沿技术

### 10.1 Reranking

- 基础 RAG 必加：**Cross-encoder rerank**（Cohere Rerank / bge-reranker / Voyage Rerank）
- 召回 top-50 → rerank top-5
- 显著提升精度

### 10.2 Contextual Retrieval（Anthropic）

- 为每个 chunk 生成"上下文前缀"
- chunk = "summary of its position in doc" + 原文
- 再 embed
- 相比原始 chunking 召回率 +35%

### 10.3 Recursive / Hierarchical Retrieval

- 先召回 summary → 定位到 section → 再召回 chunk
- 适合长文档

### 10.4 Query Decomposition

- 复杂 query → 子 queries
- 分别检索合并

### 10.5 RAPTOR

- 递归聚类 + 分层摘要树
- 不同粒度召回

### 10.6 Long Context vs RAG

- Gemini 2M / Claude 200K
- 对 MoE 长 context，是否还需要 RAG？
- 答案：**仍需**——成本、延迟、证据追溯不可替代

---

## 11. 选型决策树

```
Q: 文档量？
  < 100 页    → Long Context 直接塞
  100-10K    → 基础 RAG + Rerank + Contextual
  > 10K       → 需要更高级技术

Q: 查询类型？
  简单事实     → 基础 RAG + Rerank 足够
  多跳推理     → GraphRAG / HippoRAG / Agentic
  视觉文档     → ColPali / 文档 parser + VLM
  全局调研     → GraphRAG Global Search
  跨多源       → Agentic RAG

Q: 更新频率？
  静态         → GraphRAG 可行
  频繁         → 基础 RAG / HippoRAG 2
  流式         → 不要用 GraphRAG

Q: 延迟要求？
  < 500ms     → 基础 RAG + cache
  < 3s        → + Rerank / HyDE
  不敏感       → Agentic / Self-RAG

Q: 预算？
  低           → 基础 RAG
  中           → + Rerank + Contextual
  高           → GraphRAG / ColPali
```

---

## 12. 组合架构（生产级）

### 12.1 典型企业 RAG 架构

```
User Query
  ↓
Query Router (分类 / 改写)
  ├─ 简单 FAQ → 基础 RAG + Rerank
  ├─ 产品数据 → SQL Agent
  ├─ 法律条款 → GraphRAG
  ├─ 财务图表 → ColPali + VLM
  └─ 跨数据源 → Agentic RAG
  ↓
Result Assembly (去重 + 引证)
  ↓
Generator + Self-check
  ↓
Response + Citations
```

### 12.2 成本优化

- 简单 query 走便宜 pipeline
- Cache 层（semantic cache）
- 检索结果重用（共享 context）

---

## 13. 评估

### 13.1 核心指标

| 指标 | 说明 |
|------|------|
| **Context Recall** | 相关信息是否被召回 |
| **Context Precision** | 召回的是否相关 |
| **Faithfulness** | 答案是否忠于上下文 |
| **Answer Relevance** | 答案是否切题 |
| **Citation Accuracy** | 引用是否正确 |

### 13.2 基准

- **RAGAS**（综合）
- **ARES**
- **BEIR**（召回）
- **MultiHop-RAG**（多跳）
- **FinanceBench**（金融）
- **MMLongBench-Doc**（长文档）

见 [[concepts/agent-evaluation.zh-CN]]。

---

## 14. 向量库对各技术的支持

| 技术 | 需要能力 | 推荐向量库 |
|------|---------|-----------|
| 基础 RAG | 单向量 + filter | pgvector / Qdrant / Pinecone |
| GraphRAG | 图 + 向量 | Neo4j + pgvector / Kuzu |
| HippoRAG | 图 + 向量 | 同上 |
| ColBERT v2 | 多向量 late interaction | Vespa / Qdrant（v1.12+）/ LanceDB |
| ColPali | 多向量 + 视觉 | Vespa / Qdrant / Weaviate |
| Hybrid | BM25 + vector | Weaviate / Vespa / Qdrant / Elastic |

见 [[comparisons/vector-database-comparison.zh-CN]]。

---

## 15. Dawning RAG 策略

### 15.1 抽象层（Layer 3）

```csharp
public interface IRetriever
{
    Task<RetrievalResult> RetrieveAsync(
        string query,
        RetrievalOptions options,
        CancellationToken ct);
}

public interface IRetrievalPipeline
{
    Task<RetrievalResult> ExecuteAsync(
        string query,
        CancellationToken ct);
    // 可组合：Retriever + Reranker + Refiner
}
```

### 15.2 多策略适配

Dawning 不重造检索器，**适配主流实现**：

```
Dawning.RAG.Basic      → Qdrant / pgvector / Pinecone
Dawning.RAG.Graph      → LightRAG / GraphRAG.Net(wrap)
Dawning.RAG.Visual     → ColPali + Vespa
Dawning.RAG.Agentic    → 以 Dawning Agent Loop 组合多检索工具
Dawning.RAG.Hybrid     → BM25 + vector + rerank
```

### 15.3 Layer 7 治理对 RAG

- 数据权限过滤（Query 里自动注入租户 filter）
- 敏感信息脱敏（召回后）
- 引用强制（Policy 要求必须引证）
- Audit（谁在何时检索了什么）

### 15.4 Layer 6 Observability

- Trace 每次检索（query / 命中 docs / score）
- GenAI SemConv：`gen_ai.rag.query_count` 等扩展
- Langfuse / Phoenix 可视化

---

## 16. 实战建议

### 16.1 起步

```
基础 RAG + Rerank + Contextual Retrieval + Hybrid Search
= 覆盖 80% 场景
```

### 16.2 进阶触发条件

| 症状 | 升级方向 |
|------|---------|
| 多跳问题多 | Agentic RAG / GraphRAG |
| 全局摘要需求 | GraphRAG Global |
| PDF 图表重要 | ColPali |
| 幻觉频发 | Self-RAG / CRAG |
| Query 歧义多 | HyDE / RAG-Fusion |
| 跨数据源 | Agentic RAG |

### 16.3 不要盲目追新

- 每增加一层（rerank / HyDE / Agentic）都加延迟 + 成本
- 先测基础 RAG 失败在哪，再针对性升级

---

## 17. 小结

> **"RAG is not a technique, it's an architecture space"**（改编自 Dan Meyer）
> 下一代 RAG 已经分化为**多维组合**：
> - 召回：单向量 / 多向量 / 图 / 视觉
> - 改写：HyDE / Multi-Query / Decompose
> - 精炼：Rerank / Filter / Refine
> - 决策：固定管线 / Agentic
> - 自纠：Self-RAG / CRAG
>
> Dawning 的策略：**Layer 3 抽象 + 适配主流实现 + Layer 7 治理**，
> 让 RAG 演化不用换框架，只换策略。

---

## 18. 延伸阅读

- [[comparisons/rag-pipeline-comparison.zh-CN]] — 框架层 RAG 对比
- [[comparisons/vector-database-comparison.zh-CN]] — 向量库选型
- [[concepts/embedding-models.zh-CN]] — Embedding + Rerank 模型
- [[concepts/agent-evaluation.zh-CN]] — RAGAS 等评估
- [[concepts/multimodal-agents.zh-CN]] — ColPali 视觉 RAG
- GraphRAG: <https://github.com/microsoft/graphrag>
- LightRAG: <https://github.com/HKUDS/LightRAG>
- HippoRAG: <https://github.com/OSU-NLP-Group/HippoRAG>
- Self-RAG: <https://selfrag.github.io/>
- CRAG: <https://arxiv.org/abs/2401.15884>
- ColPali: <https://huggingface.co/vidore/colpali>
- Contextual Retrieval (Anthropic): <https://www.anthropic.com/news/contextual-retrieval>
- RAGAS: <https://docs.ragas.io/>
