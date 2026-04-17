---
title: "向量数据库对比：Pinecone、Weaviate、Qdrant、Milvus、pgvector、Chroma、LanceDB"
type: comparison
tags: [vector-database, pinecone, weaviate, qdrant, milvus, pgvector, chroma, lancedb]
sources: [comparisons/rag-pipeline-comparison.zh-CN.md, concepts/memory-architecture.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 向量数据库对比：Pinecone、Weaviate、Qdrant、Milvus、pgvector、Chroma、LanceDB

> 向量数据库是 RAG 与 Agent 记忆的基础设施。
> 选型错误 → 要么性能拉垮，要么规模撞墙，要么成本失控。
>
> 本文对比 7 款主流方案，覆盖 SaaS / 开源 / 嵌入式，并给出 Dawning 的抽象与选型建议。

---

## 1. 核心能力维度

| 维度 | 要点 |
|------|------|
| **索引类型** | HNSW / IVF / ScaNN / DiskANN / Product Quantization |
| **过滤能力** | 标签过滤、范围、复杂 where |
| **Hybrid Search** | 向量 + BM25 + 重排 |
| **多模态** | 图像/音频嵌入支持 |
| **规模** | 亿级向量能否撑住 |
| **延迟** | p50 / p99 |
| **写入吞吐** | 实时增量 vs 批量 |
| **部署** | SaaS / 自托管 / 嵌入式 |
| **成本** | 按维度 / 查询数 / 存储计费 |
| **生态** | SDK / LangChain / LlamaIndex 集成 |

---

## 2. 方案总览

| 产品 | 类型 | 特色 | 许可证 |
|------|------|------|-------|
| **Pinecone** | SaaS | 零运维、serverless | 闭源 |
| **Weaviate** | 开源 + SaaS | 模块化，内置向量化 | BSD-3 |
| **Qdrant** | 开源 + SaaS | Rust 实现，快 | Apache 2 |
| **Milvus / Zilliz** | 开源 + SaaS | 亿级规模 | Apache 2 |
| **pgvector** | PostgreSQL 扩展 | 与关系库同库 | PostgreSQL |
| **Chroma** | 开源 + SaaS | 开发体验第一 | Apache 2 |
| **LanceDB** | 嵌入式 | 本地 / 边缘 | Apache 2 |
| **Vespa** | 开源 | 大厂级搜索+向量 | Apache 2 |
| **Elasticsearch / OpenSearch** | 开源 | 全文 + 向量 | Elastic / Apache |
| **Redis** | 开源 | 快，已有栈复用 | RSAL |
| **Azure AI Search** | SaaS | 企业合规 + 向量 | 闭源 |
| **Turbopuffer** | SaaS | 对象存储后端，超便宜 | 闭源 |

---

## 3. Pinecone

### 3.1 定位

**Serverless 向量 DB 首选**。零运维。

### 3.2 特色

- Pod-based（旧）和 Serverless（新，2024）两种模型
- Serverless 按存储 + 查询计费
- 多租户 namespace
- Hybrid search（Sparse + Dense）
- 稳定性高，企业客户最多

### 3.3 限制

- 闭源，vendor lock-in
- 自建部署不支持
- 规模超大时成本不占优
- 冷启动延迟（Serverless）

### 3.4 适合

- 中小规模 SaaS
- 快速上线
- 企业预算充足

### 3.5 价格感（2026 Q1）

- Storage: ~$0.33 / GB / month
- Writes: ~$4 / 1M writes
- Queries: ~$4 / 1M reads

---

## 4. Weaviate

### 4.1 定位

**模块化 + 内置向量化**。开源、云。

### 4.2 特色

- 可选**内置 vectorizer**（Weaviate 直接调 OpenAI 生成 embedding）
- **多模态模块**（CLIP / ImageBind）
- **Hybrid Search 原生**
- **Generative modules**：直接在 DB 层做 RAG
- GraphQL 查询
- Python / Go / Java / TS SDK 完整

### 4.3 架构

- HNSW 索引
- 分片 + 副本
- Raft 一致性

### 4.4 特色能力

```graphql
{
  Get {
    Document(nearText: {concepts: ["AI agent"]}) {
      title
      _additional { score }
    }
  }
}
```

### 4.5 适合

- 需要多模态
- 希望 DB 兼任 embedding layer
- GraphQL 已在栈内

---

## 5. Qdrant

### 5.1 定位

**Rust 实现，性能派**。开源、云。

### 5.2 特色

- Rust 原生，低延迟
- **Payload filtering 强**（类似 ES）
- 稀疏向量支持（BM25 / SPLADE）
- HNSW + Quantization
- **Scroll API**（大批量遍历）
- SDK：Python / TS / Rust / Go / Java
- Cloud / 自托管都 OK

### 5.3 性能特点

- 单节点能撑千万向量
- p99 低延迟
- 内存效率好（Quantization）

### 5.4 适合

- 追求性能
- 需要复杂过滤
- 想自托管

---

## 6. Milvus / Zilliz

### 6.1 定位

**亿级规模专家**。开源（Milvus），云（Zilliz）。

### 6.2 架构

- 存算分离（类似大数据栈）
- 对象存储（MinIO/S3）+ Pulsar/Kafka + etcd
- 多索引（HNSW/IVF/DiskANN）
- 分布式原生

### 6.3 特色

- 支持 **10 亿+ 向量**
- GPU 加速可选
- Hybrid Search
- Range Search（距离范围）
- Multi-vector（一条记录多向量）

### 6.4 复杂度代价

- 部署重（依赖多组件）
- 小规模用"杀鸡用牛刀"
- 运维难度高

### 6.5 适合

- 亿级 / 十亿级规模
- 有运维团队
- Zilliz 托管避开复杂性

---

## 7. pgvector

### 7.1 定位

**已有 Postgres 栈的最优解**。

### 7.2 特色

- PostgreSQL 扩展（`CREATE EXTENSION vector`）
- 向量与业务数据**同库事务**
- 支持 HNSW（0.5+）和 IVFFlat
- 完整 SQL 生态
- 云厂商原生支持（AWS RDS / GCP Cloud SQL / Azure / Supabase / Neon）

### 7.3 用法

```sql
CREATE TABLE documents (
  id bigserial PRIMARY KEY,
  content text,
  embedding vector(1536),
  user_id uuid,
  created_at timestamptz
);

CREATE INDEX ON documents USING hnsw (embedding vector_cosine_ops);

-- 查询
SELECT content
FROM documents
WHERE user_id = '...' AND created_at > NOW() - INTERVAL '30 days'
ORDER BY embedding <=> '[...]'
LIMIT 10;
```

### 7.4 优势

- 与事务、过滤、关系联动
- 团队已懂 SQL
- 无额外基础设施

### 7.5 局限

- 规模 < 专用 VDB（千万级是舒适区上限）
- 写放大（HNSW 在 PG 写入较慢）
- 向量维度高时内存占用大

### 7.6 适合

- 中小规模
- 已有 Postgres
- 强过滤需求

---

## 8. Chroma

### 8.1 定位

**开发者友好 + 嵌入式友好**。

### 8.2 特色

- pip install 秒用
- 本地 / 客户端模式 / Server 模式
- Python 原生，LangChain 默认选项之一
- 简洁 API
- 2024 推 Chroma Cloud

### 8.3 劣势

- 生产稳定性弱于 Qdrant/Weaviate
- 大规模不如专业方案
- 历史上有重大破坏变更

### 8.4 适合

- 原型 / PoC
- 小规模应用
- 教学

---

## 9. LanceDB

### 9.1 定位

**嵌入式向量 DB**（类似 SQLite 之于关系库）。

### 9.2 特色

- **无服务器**，库形式嵌入
- 基于 Lance 列式格式（类 Arrow）
- 本地 / S3 / GCS 后端
- 支持过滤 + 全文 + 向量
- Rust 核心 + Python/TS/Rust SDK

### 9.3 优势

- 零部署
- 适合边缘 / 桌面应用
- 数据格式可被 Parquet 生态读取

### 9.4 适合

- 桌面 AI 应用（Obsidian 插件风格）
- 边缘部署
- Data Lake 内向量层

---

## 10. Vespa

### 10.1 定位

**Yahoo 出品，大厂级混合搜索**。

### 10.2 特色

- 向量 + BM25 + ML ranking 一体
- TB 级数据
- 复杂 ranking pipeline
- 稳定性好（运行 10+ 年）

### 10.3 劣势

- 学习曲线陡
- 部署复杂
- 小规模不划算

### 10.4 适合

- 搜索产品
- 已有深度 ranking 需求
- 十亿级以上

---

## 11. Elasticsearch / OpenSearch

### 11.1 定位

**复用已有搜索栈**。

### 11.2 特色

- 全文 + 向量统一
- 复杂聚合
- 成熟生态
- kNN 插件

### 11.3 劣势

- 向量性能不如专用 VDB
- 资源重
- 许可证复杂（Elastic vs OpenSearch）

### 11.4 适合

- 已有 ES 栈
- 混合搜索场景
- 合规要求自建

---

## 12. Redis

### 12.1 定位

**已有 Redis 的副业**。

### 12.2 特色

- RediSearch 模块支持向量
- 极低延迟
- 内存型，贵

### 12.3 适合

- 热数据向量（最近 1h-24h）
- 缓存层
- 已有 Redis 集群

---

## 13. 新秀：Turbopuffer

### 13.1 定位

**对象存储后端 + 极低成本**。

### 13.2 创新

- 向量存 S3
- 冷热分层
- 成本比 Pinecone 低 10-100x
- 延迟稍高（冷查询）

### 13.3 适合

- 大量不频繁查询的向量
- 成本敏感
- 归档场景

---

## 14. 横向对比矩阵

| 方案 | 规模上限 | 部署 | Hybrid | 多模态 | 过滤 | 成本（相对） |
|------|---------|------|--------|--------|------|-----------|
| Pinecone | 10亿 | SaaS | ✅ | ⚠️ | ✅ | 高 |
| Weaviate | 10亿 | 双 | ✅ | ✅ | ✅ | 中 |
| Qdrant | 10亿 | 双 | ✅ | ⚠️ | ✅✅ | 中 |
| Milvus | 100亿 | 双 | ✅ | ⚠️ | ✅ | 低（自托管） |
| pgvector | 1000万~1亿 | PG | ✅ | ❌ | ✅✅✅ | 低 |
| Chroma | 千万 | 双 | ⚠️ | ❌ | ✅ | 低 |
| LanceDB | 千万~亿 | 嵌入 | ✅ | ⚠️ | ✅ | 极低 |
| Vespa | 100亿 | 自建 | ✅✅ | ⚠️ | ✅ | 高（运维） |
| ES | 1亿 | 双 | ✅ | ⚠️ | ✅ | 中 |
| Redis | 千万 | 双 | ⚠️ | ❌ | ✅ | 高 |
| Turbopuffer | 10亿 | SaaS | ⚠️ | ❌ | ✅ | 极低 |

---

## 15. 性能 benchmark（参考量级）

**数据集：1M 768 维向量，HNSW，top-10**

| 方案 | p50 (ms) | p99 (ms) | QPS |
|------|---------|---------|-----|
| Qdrant | 5-10 | 25 | 2000+ |
| Weaviate | 8-15 | 35 | 1500 |
| Milvus | 6-12 | 30 | 2000 |
| pgvector (HNSW) | 10-20 | 60 | 800 |
| Pinecone | 20-40 | 100 | 依 plan |
| Chroma | 15-30 | 80 | 500 |

（仅供参考，版本/硬件/参数影响巨大）

---

## 16. 选型决策树

```
规模？
├─ < 1M → Chroma / LanceDB / pgvector
├─ 1M-100M → Qdrant / Weaviate / pgvector / Pinecone
├─ 100M-1B → Milvus / Qdrant / Pinecone / Weaviate
└─ > 1B → Milvus / Vespa / Turbopuffer

已有栈？
├─ Postgres → pgvector (优先考虑)
├─ Elastic → ES + kNN
├─ Redis → RediSearch（缓存层可用）
└─ 无      → 继续往下

首要关注？
├─ 零运维 → Pinecone / Zilliz Cloud / Weaviate Cloud
├─ 性能极致 → Qdrant (Rust)
├─ 多模态 → Weaviate (模块化)
├─ 成本极低 → Turbopuffer / LanceDB
├─ 嵌入式 → LanceDB / Chroma
└─ 企业合规 → Azure AI Search / Zilliz / Vespa
```

---

## 17. Dawning 的抽象

### 17.1 ILongTermMemory / IVectorStore

```csharp
public interface IVectorStore
{
    Task UpsertAsync(IEnumerable<VectorRecord> records, CancellationToken ct);
    Task<IReadOnlyList<SearchHit>> SearchAsync(
        ReadOnlyMemory<float> query,
        int topK,
        VectorFilter? filter = null,
        CancellationToken ct = default);
    Task DeleteAsync(IEnumerable<string> ids, CancellationToken ct);
    Task<IReadOnlyList<VectorRecord>> FetchAsync(IEnumerable<string> ids, CancellationToken ct);
}

public record VectorFilter(
    ScopeContext Scope,
    IReadOnlyDictionary<string, object>? Tags = null,
    DateTimeOffset? After = null,
    DateTimeOffset? Before = null);
```

### 17.2 后端包

- `Dawning.Agents.Vector.Pgvector`
- `Dawning.Agents.Vector.Qdrant`
- `Dawning.Agents.Vector.Weaviate`
- `Dawning.Agents.Vector.Pinecone`
- `Dawning.Agents.Vector.Milvus`
- `Dawning.Agents.Vector.Chroma`
- `Dawning.Agents.Vector.LanceDB`
- `Dawning.Agents.Vector.InMemory`（测试用）

### 17.3 统一 DI

```csharp
services.AddAgentOSKernel()
    .AddVectorStore<QdrantVectorStore>(o =>
    {
        o.Endpoint = "http://qdrant:6333";
        o.Collection = "dawning_memory";
        o.DistanceMetric = Distance.Cosine;
    });
```

切换后端只改配置，业务代码不动。

### 17.4 Scope 原生

所有 VDB 后端必须支持：

```csharp
filter.Scope = new ScopeContext(UserId: "alice", TeamId: "team-a");
// 等价于底层过滤 WHERE user_id='alice' AND team_id='team-a'
```

---

## 18. 工程实践

### 18.1 Embedding 一致性

- **同一套向量同一个模型**：切 embedding model = 重建索引
- 存模型 version 到 metadata
- 支持双写过渡

### 18.2 Chunking

- 定长 vs 语义 chunking
- Overlap 10-20%
- 存原文 + chunk 的关联
- Chunk id 结构化（`doc_id:chunk_idx`）

### 18.3 Hybrid Search

- Dense + BM25 + rerank
- 权重可调
- 关键场景（法律/医疗）提升显著

### 18.4 过滤前 vs 过滤后

- Pre-filter：先 WHERE 再 ANN（结果准但性能差）
- Post-filter：先 ANN 再 WHERE（快但可能 top-k 不够）
- 大多 VDB 用两者混合

### 18.5 重建索引

- 参数调整 / 模型切换要重建
- 双缓冲：新索引构建期间旧索引服务
- 灰度切流

---

## 19. 常见坑

| 坑 | 说明 |
|----|------|
| 索引太小 M/ef | Recall 差；HNSW 建议 M≥16, ef_search≥64 |
| 向量未归一化 | Cosine 计算错误 |
| 一 collection 吃所有 | 多租户时跨租泄漏 |
| 不存 metadata | 无法过滤 |
| 忘删除 | 老向量污染结果 |
| embedding 模型换了没重建 | 完全对不上 |
| pgvector 没 HNSW | 0.4 前仅 IVFFlat，性能差 |
| Milvus 小数据 | 复杂度远超收益 |
| Chroma 生产用 | 不稳定历史多 |

---

## 20. 小结

> **向量数据库没有"最好"，只有"最合适"。**
>
> 规模 × 生态 × 成本 × 运维是四维权衡。
> 大多数起步场景 pgvector / Chroma / Qdrant 足够；
> 规模起飞后 Milvus / Qdrant / Weaviate 撑住；
> 极致场景 Vespa / Turbopuffer / 自研。
>
> Dawning 的 `IVectorStore` 抽象让业务与后端解耦——换 VDB 只动配置。

---

## 21. 延伸阅读

- [[comparisons/rag-pipeline-comparison.zh-CN]] — RAG 管道整体
- [[concepts/memory-architecture.zh-CN]] — Agent 记忆 + Scope
- ANN Benchmarks：<https://github.com/erikbern/ann-benchmarks>
- Pinecone：<https://www.pinecone.io/>
- Weaviate：<https://weaviate.io/>
- Qdrant：<https://qdrant.tech/>
- Milvus：<https://milvus.io/>
- pgvector：<https://github.com/pgvector/pgvector>
- LanceDB：<https://lancedb.com/>
