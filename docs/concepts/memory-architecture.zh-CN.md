---
title: "Memory 架构深度：双层记忆 + 四级 Scope 隔离"
type: concept
tags: [memory, scope, isolation, architecture, working-memory, long-term-memory]
sources: [concepts/dawning-capability-matrix.zh-CN.md, comparisons/framework-modules-mapping.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Memory 架构深度：双层记忆 + 四级 Scope 隔离

> 记忆是 Agent 与**无状态 LLM 调用**的核心分界线。
> 本文梳理主流框架的记忆模型，说明 Dawning 双层记忆 + 四级 Scope 隔离的工程细节，以及这为什么是 Layer 2 的核心差异化能力。

---

## 1. 为什么记忆难？

LLM 本身**无状态**——每次 API 调用都是独立的。Agent 的"记忆"是框架工程出来的幻觉。

| 挑战 | 含义 |
|------|------|
| **上下文窗口有限** | 128K / 1M token 看似大，但多轮会话 + RAG 结果很容易爆 |
| **成本随长度增长** | Input token 是按 token 计费的，历史越长越贵 |
| **相关性衰减** | LLM 对长上下文的"中间部分"注意力会下降（Lost in the Middle） |
| **多 Agent 共享** | Agent A 学到的东西怎么让 Agent B 看到 |
| **隐私隔离** | 用户 A 的记忆不能泄漏给用户 B |
| **持久化** | 进程重启后记忆如何恢复 |

---

## 2. 通用记忆模型：双层架构

几乎所有生产级框架都采用**双层记忆**：

```
┌──────────────────────────────────┐
│   Working Memory（短期 / 工作）    │
│   - 当前会话上下文                  │
│   - 最近 N 条消息                  │
│   - 临时变量、工具结果               │
│   - TTL 短（小时级）                │
│   - 存储：内存 / Redis             │
└──────────────┬───────────────────┘
               │  提炼 / 摘要 / 蒸馏
               ▼
┌──────────────────────────────────┐
│   Long-Term Memory（长期）         │
│   - 跨会话持久化知识                 │
│   - 用户偏好、实体档案               │
│   - 向量化可检索                    │
│   - TTL 长（永久 / 月级）            │
│   - 存储：向量库 / 图库 / 结构化 DB  │
└──────────────────────────────────┘
```

---

## 3. 主流框架记忆模型对比

### 3.1 LangChain / LangGraph

| 层 | 实现 |
|----|------|
| Working | `ConversationBufferMemory`, `ConversationSummaryMemory`, LangGraph `CheckpointSaver` |
| Long-Term | `VectorStoreRetrieverMemory`, 自建 |
| Scope | ⚠️ 靠 `thread_id` 区分，无正式 Scope |

### 3.2 Semantic Kernel / MAF

| 层 | 实现 |
|----|------|
| Working | `ChatHistory`, `AgentThread` |
| Long-Term | `IVectorStore`, 第三方（Mem0 / Letta） |
| Scope | ⚠️ 通过 `CollectionName` 人为隔离 |

### 3.3 CrewAI

| 层 | 实现 |
|----|------|
| Working | `ShortTermMemory` |
| Long-Term | `LongTermMemory` + `EntityMemory`（实体记忆） |
| 统一 | `UnifiedMemorySystem`（新） |
| Scope | ⚠️ Crew 级隔离，用户级靠外部 |

### 3.4 Agno / Mastra

| 层 | 实现 |
|----|------|
| Working | 显式 Working Memory（可编辑的结构化状态） |
| Long-Term | 语义 Memory + Observation Memory（观察记忆） |
| Scope | ⚠️ Session 级隔离 |

### 3.5 Mem0 / Letta（专项记忆库）

| 能力 | 说明 |
|------|------|
| User Memory | 跨会话持久化的用户档案 |
| Session Memory | 会话级工作状态 |
| Agent Memory | Agent 自身的经验 |
| 自动提炼 | LLM 后台总结 + 去重 + 冲突消解 |

### 3.6 Dawning

| 层 | 实现 | 说明 |
|----|------|------|
| Working | `IWorkingMemory` | 会话级，TTL 可配 |
| Long-Term | `ILongTermMemory` + `IVectorStore` | 跨会话持久化 |
| Entity | `IEntityMemory` | 用户/组织/技能档案 |
| Observation | `IObservationMemory` | 从交互中学习模式 |
| **Scope** | **四级命名空间** | **全框架独有** |

---

## 4. Dawning 的核心差异：四级 Scope 隔离

### 4.1 四级 Scope 定义

```
┌─────────────────────────────────────────────────┐
│  global    ←  所有 Agent、所有用户可见            │
├─────────────────────────────────────────────────┤
│  team      ←  同一团队/组织/租户内可见            │
├─────────────────────────────────────────────────┤
│  session   ←  同一会话内可见（多 Agent 共享）      │
├─────────────────────────────────────────────────┤
│  private   ←  单个 Agent / 单个用户独占           │
└─────────────────────────────────────────────────┘
```

| Scope | 典型内容 | 写入者 | 读取者 |
|-------|---------|-------|-------|
| `global` | 公司知识库、产品文档、公共 FAQ | 管理员 | 所有人 |
| `team` | 项目 wiki、团队约定、客户档案 | 团队成员 | 团队成员 |
| `session` | 当前会话的中间结果、临时共享数据 | 本次会话内的 Agent | 本次会话内的 Agent |
| `private` | 用户个人偏好、Agent 独有技能 | 本人 / 本 Agent | 本人 / 本 Agent |

### 4.2 Scope 的三个工程意义

1. **安全隔离**：跨 Scope 访问必须经策略引擎审批（Layer 7）
2. **检索边界**：RAG 检索自动只在允许的 Scope 中进行
3. **成本控制**：不同 Scope 可以有不同的存储后端（global 便宜大容量、private 高性能小容量）

### 4.3 API 示例

```csharp
public interface ILongTermMemory
{
    Task SaveAsync(
        string key,
        object value,
        MemoryScope scope,
        ScopeContext context,
        CancellationToken ct = default);

    Task<T?> GetAsync<T>(
        string key,
        MemoryScope scope,
        ScopeContext context,
        CancellationToken ct = default);

    IAsyncEnumerable<MemoryEntry> SearchAsync(
        string query,
        MemoryScopeFilter filter,  // 可跨多个 scope，受策略引擎拦截
        ScopeContext context,
        CancellationToken ct = default);
}

public record ScopeContext(
    string? UserId,
    string? TeamId,
    string? SessionId,
    string? AgentId);

public enum MemoryScope { Global, Team, Session, Private }
```

### 4.4 策略执行（Layer 7 交互）

```csharp
// 保存时：策略引擎校验写入权限
await policyEngine.EnforceAsync(new SaveMemoryIntent
{
    Scope = MemoryScope.Team,
    TeamId = "alpha",
    UserId = currentUser,
    DataClassification = DataClass.Internal
});

// 检索时：过滤返回结果
var filter = await policyEngine.FilterScopesAsync(
    requestedScopes: [Global, Team, Private],
    context: currentContext);
// 结果可能只返回 [Global, Private]（用户不在任何 team 中）
```

---

## 5. Working Memory 的工程细节

### 5.1 核心问题：消息窗口管理

```
历史全量   │──────────────────────────────────────────────►│
                                                            │
                     ┌─────────────────────────────────┐
                     │  当前 Prompt 窗口（传给 LLM 的）    │
                     └─────────────────────────────────┘
                          ▲                ▲
                          │                │
                    被保留的核心       动态插入的
                    （系统/任务）     （检索/摘要）
```

### 5.2 三种主流策略

| 策略 | 工作原理 | 适合场景 |
|------|---------|---------|
| **FIFO 截断** | 超过窗口后丢弃最老消息 | 简单场景 |
| **摘要压缩** | 旧消息用 LLM 摘要替换 | 长会话、信息密度高 |
| **重要性加权** | 保留 embeddings 相似度高或 LLM 标注为"重要"的消息 | 复杂推理任务 |

### 5.3 Dawning 的工作记忆设计

```csharp
services.AddWorkingMemory(wm =>
{
    wm.UseRedis(builder.Configuration.GetConnectionString("Redis"));
    wm.WithCompaction(strategy =>
    {
        strategy.MaxTokens = 4096;
        strategy.UseSummary(summarizer: "gpt-4o-mini");
        strategy.PreservePinned();  // 标记为 Pinned 的消息永不压缩
    });
    wm.WithTtl(TimeSpan.FromHours(24));
});
```

---

## 6. Long-Term Memory 的工程细节

### 6.1 三种存储形态

| 形态 | 适合内容 | 后端 |
|------|---------|------|
| **向量存储** | 非结构化文本、语义检索 | PGVector / Qdrant / Milvus |
| **结构化** | 用户档案、偏好、实体属性 | Postgres / SQLite |
| **图谱** | 实体关系、知识图谱 | Neo4j / AgensGraph |

### 6.2 记忆提炼流水线

```
新消息进入 Working Memory
        │
        ▼
  Working Memory 接近压缩阈值
        │
        ▼
  LLM 提炼（Memory Extractor）
  ┌──────────────────────────┐
  │ 1. 识别"值得记住"的内容   │
  │ 2. 去重（vs 已有记忆）    │
  │ 3. 冲突检测（如偏好变更） │
  │ 4. 选择目标 Scope        │
  │ 5. 写入 Long-Term Memory │
  └──────────────────────────┘
```

### 6.3 冲突消解示例

```
已有记忆（Private Scope）：
  "用户偏好 Python"

新观察：
  "用户说：我现在在学 Rust"

冲突消解 LLM：
  → 更新记忆："用户主力 Python，正在学习 Rust（2026-04）"
  → 保留时间戳，允许未来再次演进
```

---

## 7. Entity Memory（实体记忆）

### 7.1 什么是 Entity Memory

对"谁 / 什么"建立档案，而非对"事件"：

```
Entity: User "alice"
├── 基本属性: name, email, role, team
├── 偏好: language=en, theme=dark, tone=casual
├── 技能图谱: python(expert), rust(beginner), kubernetes(intermediate)
└── 交互历史摘要: 主要询问 DevOps / 过去 30 天偏向晚上活跃
```

### 7.2 CrewAI 的启发

CrewAI 有 `EntityMemory` 作为一等公民。Dawning 采纳这个设计，但：

- 结合 Scope：`global` 存公共实体（如客户数据），`private` 存 Agent 自己的实体视角
- 支持字段级权限：某些字段只在 `team` 可见

---

## 8. Observation Memory（观察记忆）

### 8.1 来源：Agno / Mastra

Agno 和 Mastra 引入了 Observation Memory 概念：

> Agent 从每次交互中**自动提取可复用的模式**，而非只存对话。

### 8.2 与普通 Long-Term Memory 的区别

| 维度 | Long-Term Memory | Observation Memory |
|------|------------------|--------------------|
| 粒度 | 事件、事实 | 模式、教训、策略 |
| 触发 | 主动写入 | 自动提取 |
| 用途 | 回忆 | 改进未来行为 |
| 示例 | "用户昨天问了天气" | "当用户问天气时，先问城市往往减少来回" |

### 8.3 与 Layer 5 的联动

Observation Memory 是 Layer 5（技能演化）的输入：

```
Observation Memory ──► Skill Reflection ──► Skill Patch ──► Gateway ──► Deploy
  (what worked)        (how to improve)     (code diff)     (tests)    (gradual rollout)
```

这条链路是 Dawning 技能自演化的核心机制。

---

## 9. 持久化与恢复

### 9.1 Working Memory 持久化

| 选择 | 说明 |
|------|------|
| 不持久化 | 进程重启丢失——只适合 POC |
| Redis | 快速、TTL 原生、集群支持 |
| Checkpoint 模式 | 每个 step 存快照，支持时间旅行 |

### 9.2 Long-Term Memory 持久化

天然持久化（向量库 / 结构化 DB）。

### 9.3 跨进程 Agent 迁移

```
Agent 在 Node-A 运行到 step 42
        │
        ▼
  检查点写入 ICheckpointStore
        │
        ▼
  Node-A 崩溃 / 需要伸缩
        │
        ▼
  Node-B 读取最新检查点
        │
        ▼
  Agent 从 step 42 无感继续
```

这需要 Working Memory **全部进入 Checkpoint**，而不仅仅是应用状态。

---

## 10. Dawning Memory Plane 总体架构

```
┌─────────────────────────────────────────────────────────┐
│                Agent                                    │
│                  │                                      │
│        ┌─────────┴──────────┐                           │
│        ▼                    ▼                           │
│   IWorkingMemory      ILongTermMemory                   │
│   (Redis / In-Mem)    (Vector + Structured + Graph)     │
│        │                    │                           │
│        └─────────┬──────────┘                           │
│                  │                                      │
│        ┌─────────▼──────────┐                           │
│        │  Scope Resolver    │─── ScopeContext           │
│        └─────────┬──────────┘                           │
│                  │                                      │
│        ┌─────────▼──────────┐                           │
│        │  Policy Engine     │─── Layer 7                │
│        │  (RBAC / PII)      │                           │
│        └─────────┬──────────┘                           │
│                  │                                      │
│        ┌─────────▼──────────┐                           │
│        │  Audit Trail       │                           │
│        └────────────────────┘                           │
└─────────────────────────────────────────────────────────┘
```

**每次记忆读写都经过 Scope Resolver → Policy Engine → Audit Trail 三道关**，这是其他框架没有的。

---

## 11. 设计原则总结

| # | 原则 | 理由 |
|---|------|------|
| 1 | **双层而非单层** | Working 关注性能，Long-Term 关注持久与语义 |
| 2 | **Scope 是一等公民** | 多租户 / 隐私合规 / 成本控制的地基 |
| 3 | **策略引擎在数据通路上** | 不是事后审计，而是事前拦截 |
| 4 | **Entity 和 Observation 分开** | 档案 vs 经验模式，功能和存储不同 |
| 5 | **持久化到 Checkpoint** | 为跨进程迁移和故障恢复提供基础 |
| 6 | **提炼是 LLM 驱动的** | 低成本后台模型（gpt-4o-mini 级别）处理 |
| 7 | **冲突保留历史** | 不直接覆盖，带时间戳保留演进轨迹 |

---

## 12. 延伸阅读

- [[concepts/dawning-capability-matrix.zh-CN]] — Layer 2 完整接口清单
- [[comparisons/framework-modules-mapping.zh-CN]] — 各框架记忆模块位置
- [[comparisons/rag-pipeline-comparison.zh-CN]] — RAG 与记忆的边界
- Mem0：<https://github.com/mem0ai/mem0>
- Letta (原 MemGPT)：<https://github.com/letta-ai/letta>
