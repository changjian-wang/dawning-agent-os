---
title: "Embedding 模型专题：选型、微调、多模态与国产模型"
type: concept
tags: [embedding, openai, voyage, bge, jina, cohere, finetuning, multimodal]
sources: [comparisons/vector-database-comparison.zh-CN.md, comparisons/rag-pipeline-comparison.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Embedding 模型专题：选型、微调、多模态与国产模型

> 向量数据库只是容器，**Embedding 模型才是灵魂**。
> 选错 embedding → RAG 再好的 retriever 也召不回相关内容。
>
> 本文梳理 2026 Q1 主流 embedding 模型（OpenAI/Voyage/BGE/Jina/Cohere/国产）、选型维度、微调路径，
> 以及 Dawning 的 embedding 抽象设计。

---

## 1. Embedding 的本质与作用

### 1.1 一句话

把文本 / 图像 / 音频映射到**固定维度向量**，使语义相似的内容向量相近。

### 1.2 在 Agent 里的角色

| 场景 | 用途 |
|------|------|
| RAG 检索 | 文档切片 → 向量库 → query 检索 |
| 记忆召回 | 历史对话 / 事实向量化 |
| Tool Router | 工具描述向量化 → 选最相关工具 |
| Skill Router | Skill 向量化 → 选最合适技能 |
| 语义缓存 | Prompt 向量化 → 相似命中 |
| Clustering | 用户反馈聚类 / 意图发现 |
| 相似度去重 | 重复检测 |

---

## 2. 核心选型维度

| 维度 | 要点 |
|------|------|
| **语种** | 英文 / 多语 / 中文 / 特定语 |
| **领域** | 通用 / 法律 / 医疗 / 代码 |
| **维度** | 384 / 512 / 768 / 1024 / 1536 / 3072 |
| **上下文长度** | 512 / 2K / 8K / 32K tokens |
| **精度** | MTEB / C-MTEB / BEIR benchmark 分数 |
| **成本** | $/M tokens 或自托管硬件 |
| **延迟** | 单查询 p50 |
| **吞吐** | batch embedding 速度 |
| **许可证** | 商用 OK 吗 |
| **部署** | API / 自托管 |
| **Matryoshka** | 支持维度截断不重训吗 |

---

## 3. 2026 Q1 主流模型

### 3.1 OpenAI 系

| 模型 | 维度 | 语种 | $/1M tokens | 特色 |
|------|------|------|------------|------|
| text-embedding-3-small | 512/1536 | 多 | $0.02 | 便宜，支持 Matryoshka |
| text-embedding-3-large | 256/1024/3072 | 多 | $0.13 | 高精度 |

**优势**：稳定、生态、Matryoshka 可降维。
**劣势**：闭源、数据出境、贵。

### 3.2 Voyage AI

Anthropic 系，2024-2025 快速崛起。

| 模型 | 维度 | 特色 |
|------|------|------|
| voyage-3-large | 1024 | 通用，精度顶尖 |
| voyage-3 | 1024 | 平衡 |
| voyage-code-3 | 1024 | 代码专用（强于 OpenAI） |
| voyage-law-2 | 1024 | 法律 |
| voyage-finance-2 | 1024 | 金融 |
| voyage-multilingual-2 | 1024 | 多语 |

**优势**：领域专用、MTEB 常年前列。
**劣势**：API-only。

### 3.3 Cohere

| 模型 | 特色 |
|------|------|
| embed-english-v3 | 英文优化 |
| embed-multilingual-v3 | 100+ 语 |
| embed-v4 (2025) | 多模态（图+文） |

**优势**：企业合规、Rerank 配套。

### 3.4 BGE（北京智源）

**开源王者**。Apache 2 授权。

| 模型 | 维度 | 特色 |
|------|------|------|
| bge-large-en-v1.5 | 1024 | 英文 |
| bge-m3 | 1024 | 多语 + 多粒度（稠密/稀疏/ColBERT） |
| bge-multilingual-gemma2 | 3584 | 最强多语 |
| bge-reranker-v2-m3 | — | 重排 |

**优势**：开源、性能领先、中文强。
**劣势**：自托管门槛。

### 3.5 Jina

| 模型 | 特色 |
|------|------|
| jina-embeddings-v3 | 8K 上下文，多语，长文优化 |
| jina-clip-v2 | 图文多模态 |
| jina-colbert-v2 | ColBERT 风格 |

### 3.6 Nomic

| 模型 | 特色 |
|------|------|
| nomic-embed-text-v2 | 开源，MoE，8K 上下文 |
| nomic-embed-vision-v1.5 | 视觉 |

### 3.7 Mistral

| 模型 | 特色 |
|------|------|
| mistral-embed | API 单一模型 |

### 3.8 国产 / 中文优化

| 模型 | 维度 | 特色 |
|------|------|------|
| **BGE**（智源） | 见上 | 中文开源首选 |
| **Qwen3-Embedding** | 768/1024/4096 | 2025 发布，Matryoshka |
| **m3e-base / large**（MokaAI） | 768/1024 | 中文专用 |
| **stella** | 1024 | 中文开源 |
| **GTE-Qwen2** | 768/1536 | 多语 |
| **DMeta-embedding** | 768 | 中文 |
| **text2vec-base-chinese** | 768 | 中文经典 |

### 3.9 代码 Embedding

| 模型 | 特色 |
|------|------|
| voyage-code-3 | API 最强 |
| CodeSage | 开源 |
| Salesforce SFR-Embedding-Code | 开源 |
| UniXcoder | 微软 |

---

## 4. 评测基准

### 4.1 MTEB（Massive Text Embedding Benchmark）

- 56 个英文任务
- Retrieval / Classification / Clustering / STS 等
- 榜单：<https://huggingface.co/spaces/mteb/leaderboard>

### 4.2 C-MTEB（中文）

- 35 个中文任务
- 国内模型对齐

### 4.3 BEIR

- Zero-shot 检索专项
- 跨 18 数据集

### 4.4 Benchmark 陷阱

- **过拟合榜**：有些模型针对 MTEB 调优
- **领域偏差**：通用榜上冠军 ≠ 你的领域强
- **必做**：用**你自己的数据集**测

---

## 5. Matryoshka Embedding

### 5.1 什么是

**训练时强制前 N 维也能独立用**：

```
1536 维完整向量
  → 前 256 维也能用（稍弱）
  → 前 512 维也能用（更好）
  → 前 1024 维也能用（接近完整）
```

### 5.2 好处

- **存储成本**：按需降维
- **查询速度**：粗召回用 256 维 → 精排用 1536 维
- **不重训**：截断即可

### 5.3 支持

- OpenAI text-embedding-3 系
- Nomic
- Voyage（部分）
- 自训 Matryoshka loss

### 5.4 使用

```python
# OpenAI
response = client.embeddings.create(
    input="...",
    model="text-embedding-3-large",
    dimensions=256  # 截断到 256
)
```

---

## 6. 多模态 Embedding

### 6.1 图文共向量空间

| 模型 | 特点 |
|------|------|
| CLIP（OpenAI 2021） | 鼻祖，多版本 |
| OpenCLIP | 开源社区复现 |
| SigLIP（Google） | CLIP 改进 |
| Cohere embed-v4 | 图文同向量 |
| Jina CLIP v2 | 多语图文 |
| ImageBind（Meta） | 6 模态（图/文/音/热/深度/IMU） |

### 6.2 应用

- 以文搜图
- 以图搜图
- 图文混合召回
- 跨模态去重

### 6.3 实践注意

- 图文向量归一化后直接 cosine
- 同一空间维度需一致
- 图像要统一预处理

---

## 7. 微调 Embedding

### 7.1 何时微调

| 场景 | 需要微调？ |
|------|----------|
| 通用领域 | ❌（现成够好） |
| 高度专业术语 | ⚠️（效果测一下再说） |
| 领域 Benchmark 落后 > 10% | ✅ |
| 查询-文档语言不同 | ⚠️ |

### 7.2 数据准备

- (query, positive) 对
- (query, positive, negative) 三元组（效果更好）
- 数据量：5K-50K 对足够起步

### 7.3 方法

| 方法 | 说明 |
|------|------|
| **InfoNCE loss** | 对比学习经典 |
| **Triplet loss** | 有明确 hard negative |
| **CoSENT** | 余弦敏感损失 |
| **LoRA / QLoRA** | 低秩适配（不动原参数） |
| **GritLM** | 生成 + 检索联合训练 |

### 7.4 工具

- Sentence-Transformers（Python）
- FlagEmbedding（智源）
- LLM2Vec
- HuggingFace PEFT

### 7.5 典型提升

- 通用 → 专业领域精调：Recall@10 提升 10-30%
- 少于 1K 样本：可能**变差**（慎之）

---

## 8. 重排（Rerank）

### 8.1 为什么需要

Embedding 召回 top-100 → Cross-encoder 重排到 top-10

### 8.2 主流 Reranker

| 模型 | 特点 |
|------|------|
| Cohere Rerank 3 | API 最强 |
| BGE Reranker v2 | 开源强 |
| Jina Reranker v2 | 多语 |
| voyage-rerank-2 | Anthropic 系 |
| ColBERT | 老牌，多向量 |

### 8.3 成本与价值

- 增加延迟（50-200ms / 100 docs）
- 准确率提升显著（10-30%）
- 关键场景（法律/医疗）必做

---

## 9. 成本对比（2026 Q1 参考）

| 模型 | 单价 | 百万 query 成本 |
|------|------|----------------|
| OpenAI 3-small | $0.02 / 1M tokens | $10-$30 |
| OpenAI 3-large | $0.13 / 1M tokens | $60-$200 |
| Voyage-3 | $0.05 / 1M tokens | $20-$80 |
| Cohere v3 | $0.10 / 1M tokens | $50-$150 |
| BGE 自托管（A100） | ~$0.3/hr | 折算 $5-$20 |
| Ollama 本地 | 硬件成本 | 极低 |

---

## 10. 延迟与吞吐

### 10.1 API

- Single query：100-300ms
- Batch 100：500ms-2s

### 10.2 自托管（A100）

- BGE-large：1000+ docs/s（batch）
- Latency：~10ms / doc（batch 内）

### 10.3 边缘

- Ollama + nomic-embed-text：~50ms / doc（CPU）
- 适合离线 / 边缘

---

## 11. 实践陷阱

| 坑 | 方案 |
|----|------|
| **切换模型不重建索引** | embedding 模型改了 → 必须全量重建 |
| **embedding/query 不同模型** | 必须对称 |
| **未归一化** | Cosine 计算错 |
| **维度过高** | 存储贵，召回未必更好（边际递减） |
| **通用榜冠军上头** | 领域上可能比专用模型差 |
| **短文本切太细** | Chunk < 32 tokens 基本没语义 |
| **长文本没截断** | 超 context length 行为未定义 |
| **国际化忽略** | 用英文 embedding 处理中文 → 召回垮 |
| **不测 Recall** | 盲目相信 MTEB 分数 |
| **忘记 instruction prefix** | BGE/GTE 等需要 `query: ...` / `passage: ...` |

---

## 12. Instruction-Tuned Embedding

部分模型要求 query/doc 加 instruction：

```python
# BGE / GTE / E5
query = "query: 什么是 Agent？"
doc = "passage: Agent 是..."

# Instructor / Promptagator
query = "Represent the question: 什么是 Agent？"
```

**忽视 instruction prefix** 会显著降准确率。

---

## 13. Dawning 的 IEmbeddingProvider

### 13.1 接口

```csharp
public interface IEmbeddingProvider
{
    string Model { get; }
    int Dimension { get; }
    int MaxInputTokens { get; }

    Task<ReadOnlyMemory<float>> EmbedAsync(
        string text,
        EmbeddingPurpose purpose = EmbeddingPurpose.Document,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        EmbeddingPurpose purpose,
        CancellationToken ct);
}

public enum EmbeddingPurpose { Query, Document, Code, Classification }
```

### 13.2 多 Provider 支持

```csharp
services.AddEmbeddingProvider(e =>
{
    e.AddOpenAI("text-embedding-3-small");
    e.AddVoyage("voyage-3");
    e.AddBGE("BAAI/bge-m3");  // 本地
    e.AddOllama("nomic-embed-text");
});
```

### 13.3 自动处理

- Instruction prefix（按模型）
- 截断到 MaxInputTokens
- 批量合并
- 归一化（可选）
- Matryoshka 维度截断

### 13.4 与 VectorStore 绑定

```csharp
services.AddRAG(rag =>
{
    rag.UseEmbedding("text-embedding-3-large", dimensions: 1024);
    rag.UseVectorStore<QdrantVectorStore>();
});
```

Dawning 会校验维度一致性。

---

## 14. 选型决策树

```
语言？
├─ 英文 ──► 通用: OpenAI 3-large / Voyage-3 / BGE-large-en
│          代码: voyage-code-3 / SFR-Embedding-Code
│          法律: voyage-law-2
│
├─ 中文 ──► 开源: BGE-m3 / Qwen3-Embedding / stella
│          商业: Voyage multilingual / OpenAI 3
│
└─ 多语 ──► BGE-m3 / Cohere multilingual v3 / Voyage multilingual

部署？
├─ SaaS 快起步 ──► OpenAI 3-small（便宜）/ 3-large（精度）
├─ 自托管生产 ──► BGE-m3 / Qwen3-Embedding
└─ 边缘 ──► nomic-embed-text / jina-embeddings v3

规模？
├─ < 10M ──► 随便，成本不关键
├─ 10M-1B ──► 考虑 Matryoshka + 自托管
└─ > 1B ──► 必须自托管，考虑量化

特殊需求？
├─ 多模态 ──► CLIP / SigLIP / Cohere v4 / Jina CLIP v2
├─ 超长文本 ──► Jina v3 (8K) / OpenAI 3 (8K)
└─ 代码 ──► voyage-code-3 / SFR-Embedding-Code
```

---

## 15. 测试清单（必做）

- [ ] 用你的数据集测 Recall@10
- [ ] Query / Doc prefix 正确
- [ ] 向量归一化
- [ ] 存模型 version 到 metadata
- [ ] embedding 一致性（全量同模型）
- [ ] 维度 × 数据量的存储成本估算
- [ ] 延迟符合 SLO
- [ ] 重排对比（with/without rerank）
- [ ] 多语混合场景（若适用）

---

## 16. 小结

> **Embedding 的选择比向量库更重要。**
>
> 选对 embedding：RAG 有了灵魂；
> 选错 embedding：向量库再贵都白搭。
>
> 2026 的格局：OpenAI 稳、Voyage 精、BGE 开源强、国产模型在中文上反超。
> Dawning 把 embedding 做成可插拔 Provider，换模型只动配置。

---

## 17. 延伸阅读

- [[comparisons/vector-database-comparison.zh-CN]] — 向量库
- [[comparisons/rag-pipeline-comparison.zh-CN]] — RAG 整体
- MTEB Leaderboard：<https://huggingface.co/spaces/mteb/leaderboard>
- Sentence-Transformers：<https://www.sbert.net/>
- FlagEmbedding（BGE）：<https://github.com/FlagOpen/FlagEmbedding>
- Voyage AI：<https://www.voyageai.com/>
- Jina：<https://jina.ai/embeddings/>
