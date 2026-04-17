---
title: "RAG Pipeline 专题：各家 RAG 实现的模块细节对比"
type: comparison
tags: [rag, retrieval, pipeline, architecture, comparison]
sources: [concepts/dawning-capability-matrix.zh-CN.md, comparisons/framework-modules-mapping.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# RAG Pipeline 专题：各家 RAG 实现的模块细节对比

> RAG（Retrieval-Augmented Generation）是连接 LLM 与外部知识的关键管道。不同框架对 RAG 的**模块切分**和**默认组合**差异很大，本文横向对照它们的做法，并给出 Dawning 的 Layer 2 实现方向。

---

## 1. RAG 管道的七个标准阶段

无论哪家框架，完整 RAG 管道都可拆成以下 7 个阶段：

```
┌─────────┐   ┌─────────┐   ┌─────────┐   ┌──────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐
│ 1. Load │──►│ 2. Split│──►│ 3. Embed│──►│ 4. Store │──►│ 5. Query│──►│ 6. Rank │──►│ 7. Gen  │
└─────────┘   └─────────┘   └─────────┘   └──────────┘   └─────────┘   └─────────┘   └─────────┘
  Loader       Splitter    Embedding     VectorStore    Retriever     Reranker       LLM
   原始文档     分块策略      向量化         持久化         召回           重排          生成
```

每个阶段都是独立可替换的扩展点。

| 阶段 | 关键决策 |
|------|---------|
| **1. Load** | 支持哪些源（PDF / Markdown / HTML / DB / API）？ |
| **2. Split** | 按字符 / 语义 / 结构（标题、代码块）切分？chunk_size / overlap？ |
| **3. Embed** | 使用哪个 Embedding 模型？多路（稠密 + 稀疏）？ |
| **4. Store** | 向量库选型？是否保留原文 metadata？ |
| **5. Query** | 相似度检索 / 混合检索（BM25 + Vector）/ 图谱检索？ |
| **6. Rank** | 是否用 Cross-Encoder 重排？Top-K 截断策略？ |
| **7. Gen** | 如何拼 Prompt？Context 窗口管理？引用标注？ |

---

## 2. LangChain / LangGraph

### 2.1 模块映射

| 阶段 | 模块 | 代表类 |
|------|------|--------|
| Load | `langchain_community.document_loaders` | `PyPDFLoader`, `WebBaseLoader`, 200+ loaders |
| Split | `langchain_text_splitters` | `RecursiveCharacterTextSplitter`, `MarkdownHeaderTextSplitter` |
| Embed | `langchain_core.embeddings` | `OpenAIEmbeddings`, `HuggingFaceEmbeddings` |
| Store | `langchain_community.vectorstores` | 70+ 实现（Chroma / Pinecone / Weaviate / PGVector...） |
| Query | `Retriever` 抽象 | `VectorStoreRetriever`, `MultiQueryRetriever`, `ParentDocumentRetriever` |
| Rank | `BaseDocumentCompressor` | `CohereRerank`, `LLMChainExtractor` |
| Gen | LCEL 链 | `create_retrieval_chain`, `create_stuff_documents_chain` |

### 2.2 特点

- **生态最全**：loaders 和 vectorstores 覆盖最广
- **组合式**：LCEL 把整个管道写成声明式表达式
- **Advanced RAG 支持度高**：Parent Document / Multi-Query / Self-Query / Hybrid / HyDE

### 2.3 典型示例

```python
chain = (
    {"context": retriever | format_docs, "question": RunnablePassthrough()}
    | prompt
    | llm
    | StrOutputParser()
)
```

---

## 3. Spring AI

### 3.1 模块映射

| 阶段 | 模块 | 代表类 |
|------|------|--------|
| Load | `DocumentReader` | `PagePdfDocumentReader`, `TikaDocumentReader` |
| Split | `DocumentTransformer` | `TokenTextSplitter` |
| Embed | `EmbeddingClient` | `OpenAiEmbeddingClient`, `OllamaEmbeddingClient` |
| Store | `VectorStore` | 9+ 实现（PGVector / Milvus / Redis / Qdrant / Weaviate...） |
| Query | `VectorStore.similaritySearch` | 返回 `List<Document>` |
| Rank | `DocumentPostProcessor`（扩展点） | — |
| Gen | `ChatClient.advisors(QuestionAnswerAdvisor)` | 一行集成 RAG |

### 3.2 特点

- **极简 API**：`ChatClient` 的 Advisor 机制把 RAG 压缩到一行
- **Spring Boot Starter**：自动装配所有组件
- **企业就绪**：所有 Vector Store 都支持连接池、事务、监控

### 3.3 典型示例

```java
ChatResponse response = chatClient.prompt()
    .advisors(new QuestionAnswerAdvisor(vectorStore, SearchRequest.defaults()))
    .user(question)
    .call()
    .chatResponse();
```

一行 `advisors()` 就是完整 RAG。

---

## 4. Microsoft Agent Framework / Semantic Kernel

### 4.1 模块映射

| 阶段 | SK 模块 | MAF 做法 |
|------|--------|---------|
| Load | 自己写（无内建） | 同上 |
| Split | `TextChunker` | 同上 |
| Embed | `IEmbeddingGenerator<string, Embedding<float>>`（MEAI） | 同上 |
| Store | `IVectorStore` + `IVectorStoreRecordCollection` | 同上 |
| Query | `VectorStoreRecordCollection.HybridSearch` | 同上 |
| Rank | 自己实现 | 同上 |
| Gen | Prompt 模板 + Plugin | `AIAgent` + `AIContextProvider` |

### 4.2 特点

- **SK 的 Connectors 生态**：Azure AI Search / Postgres / Redis / MongoDB / Qdrant / Weaviate / Chroma 等
- **新抽象 `IVectorStore`**（2024 引入）：对标 Spring AI VectorStore
- **MAF 的 `AIContextProvider`**：把 RAG 作为 Agent 的上下文注入点，而非独立 Chain
- **HybridSearch 原生支持**：稠密 + 稀疏融合

### 4.3 典型示例（.NET）

```csharp
var collection = vectorStore.GetCollection<string, Paragraph>("articles");
var results = await collection.HybridSearchAsync(
    queryEmbedding,
    ["keyword1", "keyword2"],
    top: 5);

// 注入到 Agent 上下文
agent.Context.AddProvider(new RagContextProvider(results));
```

---

## 5. LlamaIndex

### 5.1 模块映射（RAG 原生设计）

| 阶段 | 模块 |
|------|------|
| Load | `SimpleDirectoryReader`, 200+ LlamaHub loaders |
| Split | `NodeParser`（按语义/结构/窗口） |
| Embed | `BaseEmbedding` |
| Store | `VectorStoreIndex` |
| Query | `QueryEngine` / `Retriever`（**这是 LlamaIndex 的核心**） |
| Rank | `NodePostprocessor`（LLMRerank / CohereRerank / 时间衰减） |
| Gen | `ResponseSynthesizer`（compact / refine / tree_summarize） |

### 5.2 特点

- **RAG-first 框架**：RAG 是产品核心，而非附属模块
- **Advanced RAG 覆盖最深**：Sentence Window / Auto-Merging / Recursive / HyDE / Step-Back
- **Evaluation 内建**：Faithfulness / Relevancy / Answer Correctness

---

## 6. CrewAI / OpenAI Agents SDK

这两家**不内建完整 RAG 管道**：

- **CrewAI**：通过 `tool` 注入 RAG（用户自己接 LangChain 或 LlamaIndex）
- **OpenAI Agents SDK**：极简主义，RAG 通过 `File Search` 工具（内置）或自定义工具实现

---

## 7. 横向对比矩阵

| 框架 | Loaders | Vector Stores | Advanced RAG | Rerank | Hybrid Search | Eval | 内建一行 API |
|------|---------|---------------|--------------|--------|---------------|------|-------------|
| **LangChain** | 200+ | 70+ | ✅ 丰富 | ✅ | ✅ | ⚠️ LangSmith | ⚠️ LCEL 两行 |
| **LlamaIndex** | 200+ | 50+ | ✅ **最深** | ✅ | ✅ | ✅ 内建 | ⚠️ QueryEngine |
| **Spring AI** | 10+ | 9+ | ⚠️ Advisor 扩展 | ⚠️ | ⚠️ 部分 | ❌ | ✅ `.advisors()` |
| **SK / MAF** | 少 | 10+ | ⚠️ 靠用户 | ⚠️ | ✅ | ❌ | ❌ |
| **CrewAI** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **OpenAI SDK** | ❌ | ❌（FileSearch） | ❌ | ❌ | ❌ | ❌ | ⚠️ FileSearch |
| **Dawning（规划）** | 🟡 10+ | 🟡 5+ | 🟡 分层 | 🟡 | 🟡 | 🟡 | 🟡 Advisor |

---

## 8. Advanced RAG 模式速查

即使选了同一个框架，RAG 质量差距主要来自**是否用对了高阶模式**。

| 模式 | 解决什么问题 | 代表实现 |
|------|-------------|---------|
| **Multi-Query** | 单条 query 召回偏 | LangChain `MultiQueryRetriever` |
| **HyDE** | 问答语义不匹配 | 先让 LLM 生成假答案 → 用假答案做检索 |
| **Parent Document** | 小 chunk 召回准但上下文少 | 存小 chunk，召回时返回父段 |
| **Sentence Window** | 句子级精度 + 段落级上下文 | LlamaIndex `SentenceWindowNodeParser` |
| **Self-Query** | 结构化过滤（如时间/作者） | LLM 提取 filter → 向量库 WHERE 子句 |
| **Hybrid Search** | 语义+关键词混合 | BM25 + Vector 融合排序 |
| **Reranking** | Top-K 召回质量参差 | Cross-Encoder / Cohere Rerank 二次打分 |
| **RAG-Fusion** | 多查询融合排序 | 生成多 query → RRF 合并 |
| **GraphRAG** | 关系型知识（Microsoft 2024） | 知识图谱 + 社区摘要 |
| **Agentic RAG** | 查询需要规划 | Agent 决定"是否检索、检索什么" |

---

## 9. Dawning 的 RAG 设计方向

### 9.1 定位：Layer 2（存储层）+ Layer 1（工具）联动

```
Layer 2（存储）            Layer 1（工具/上下文注入）
┌────────────────┐        ┌─────────────────────┐
│ IVectorStore   │◄───────┤ IRagPipeline        │
│ IEmbedding     │        │  - IRetriever       │
│ IDocumentStore │        │  - IReranker        │
│ IChunker       │        │  - IContextComposer │
└────────────────┘        └──────────┬──────────┘
                                      │
                        Agent Loop 通过 `IContextProvider` 接入
```

### 9.2 默认管道（开箱即用）

```csharp
services.AddRagPipeline(rag =>
{
    rag.UseDocumentLoaders(loaders =>
    {
        loaders.AddPdf();
        loaders.AddMarkdown();
        loaders.AddWeb();
    });
    rag.UseRecursiveChunker(chunkSize: 512, overlap: 64);
    rag.UseEmbeddingProvider<OpenAIEmbeddingProvider>();
    rag.UseVectorStore(store => store.UsePgVector(...));
    rag.UseHybridRetriever(bm25Weight: 0.3);
    rag.UseReranker<CohereReranker>();
});

// Agent 中一行使用
agent.Context.AddProvider<RagContextProvider>();
```

### 9.3 Dawning 独有的 RAG 能力

| 能力 | 说明 | 来源 Layer |
|------|------|-----------|
| **Scope 感知检索** | 检索自动加 scope 过滤（global / team / session / private） | Layer 2 + Layer 7 |
| **PII 脱敏注入** | 检索结果进入 Prompt 前自动脱敏 | Layer 7 |
| **审计可追溯** | 每次 RAG 调用记录"检索了什么 → 用了什么 → 生成了什么" | Layer 7 |
| **技能驱动 RAG** | RAG 本身作为一种可演化的 Skill | Layer 5 |
| **MCP 资源即 RAG 源** | MCP Resources 自动加入 RAG 管道 | Layer 1 + Layer 2 |

### 9.4 不做的事

- **不内建 200+ Loaders**：交给社区或 MCP Server 提供
- **不做 RAG Eval 产品化**：输出 OpenTelemetry 事件，接入 Langfuse / Arize 等

---

## 10. 小结

| 观察 | 含义 |
|------|------|
| **LangChain / LlamaIndex 生态最全** | 做实验和 POC 首选 |
| **Spring AI 极简** | Java + Spring 项目首选，一行 `advisors` |
| **MAF / SK 的 `IVectorStore` 抽象在进化** | .NET 生态有跟进 Spring AI 的趋势 |
| **Dawning 的独特价值在治理 + Scope** | 不重复造 loader / vectorstore，而做 .NET 生态的"企业级 RAG 管道" |

> RAG 不是"把文档喂给 LLM"，而是一条 7 阶段的工程管道。
> 每个阶段都有工程决策，Dawning 的目标是**把这些决策做成 Layer 2 的可插拔抽象**——而不是硬编码一个 Retriever。

---

## 11. 延伸阅读

- [[concepts/dawning-capability-matrix.zh-CN]] — Layer 2 存储抽象概览
- [[comparisons/framework-modules-mapping.zh-CN]] — 各家"记忆 + 检索"模块位置对比
- LangChain RAG 文档：<https://python.langchain.com/docs/concepts/rag>
- LlamaIndex RAG 文档：<https://docs.llamaindex.ai/en/stable/understanding/rag/>
- Spring AI Advisors：<https://docs.spring.io/spring-ai/reference/api/chatclient.html#_advisors>
