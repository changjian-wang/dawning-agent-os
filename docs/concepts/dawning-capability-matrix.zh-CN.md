---
title: "Dawning 能力矩阵：统一抽象 + 多后端"
type: concept
tags: [architecture, capability, matrix, di, providers]
sources: [concepts/agent-os-architecture.zh-CN.md, concepts/protocols-a2a-mcp.zh-CN.md, comparisons/agent-framework-landscape.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Dawning 能力矩阵：统一抽象 + 多后端

> 设计灵感来自 Spring AI 的"一个接口 + 多种实现"模式，但 Dawning 覆盖范围更广——不止 Framework 抽象，而是整个 Agent OS 内核。
>
> 本文用**左（价值主张）— 中（核心组件）— 右（可插拔后端）**三栏视图，完整呈现 Dawning 的能力矩阵。

---

## 1. 对标：Spring AI 的启发

![Spring AI 架构](../images/frameworks/architecture/Spring-AI.png)

Spring AI 在 Java 生态以**统一抽象层 + 供应商无关**的模式获得了广泛采用：

- **左**：价值主张（API 统一、降低成本、Spring 集成……）
- **中**：12 个核心组件（Chat Client、Embedding、Vector Store、RAG、Function Calling……）
- **右**：可切换后端（OpenAI / Azure / Anthropic / HuggingFace + PGVector / Milvus / ElasticSearch）

Dawning 借鉴这个模式，但**走得更远**：不仅是 Framework 层的抽象，而是一整套 Agent OS 内核。

---

## 2. Dawning 能力矩阵总览

```
┌─────────────────────┬──────────────────────────────────┬─────────────────────────┐
│    价值主张           │           核心组件                  │       可插拔后端           │
│    (WHY)            │            (WHAT)                 │        (HOW)            │
├─────────────────────┼──────────────────────────────────┼─────────────────────────┤
│ .NET 原生            │ ILLMProvider                      │ OpenAI / Azure /         │
│ DI 优先              │ IEmbeddingProvider                │ Anthropic / Ollama /     │
│ 供应商无关           │ IToolRegistry                     │ 自定义                    │
│ 配置驱动             │ IMcpClient                        │                          │
│ 四级 Scope 隔离       │ IAgentDirectory                   │ Redis / RabbitMQ /       │
│ 技能自演化            │ IMessageBus                       │ Azure Service Bus /      │
│ A2A + MCP 内建       │ IWorkingMemory                    │ 内存                      │
│ 分布式三面体          │ ILongTermMemory                   │                          │
│ 企业级治理            │ IVectorStore                      │ PGVector / Milvus /      │
│ 持久化执行            │ ICheckpointStore                  │ Qdrant / ChromaDB        │
│ 技能市场              │ ISkillRegistry                    │                          │
│ 开源 + MIT           │ ISkillRouter                      │ 文件系统 / 数据库 /        │
│                     │ IPolicyEngine                     │ 远程 Registry            │
│                     │ IAuditTrail                       │                          │
│                     │ IDomainEventBus                   │ OpenTelemetry /          │
│                     │ ITelemetry                        │ Application Insights     │
└─────────────────────┴──────────────────────────────────┴─────────────────────────┘
```

---

## 3. 左栏：价值主张（12 条）

| # | 主张 | 含义 |
|---|------|------|
| 1 | **.NET 原生** | 不是 Python 移植，充分利用 Roslyn / Source Generator / Native AOT |
| 2 | **DI 优先** | 所有服务构造函数注入，无静态工厂、无 Kernel 单例 |
| 3 | **供应商无关** | LLM / 向量库 / 消息总线都可热插拔，零改动切换 |
| 4 | **配置驱动** | appsettings.json / 环境变量驱动行为，无需改代码 |
| 5 | **四级 Scope 隔离** | global / team / session / private 的记忆和工具隔离 |
| 6 | **技能自演化** | 反思 → 补丁 → 门禁 → 灰度 → 回滚（Layer 5） |
| 7 | **A2A + MCP 内建** | 两个生态协议作为 Layer 1 / Layer 6 一等公民 |
| 8 | **分布式三面体** | 知识面 / 运行面 / 治理面，异步契约 + 幂等 |
| 9 | **企业级治理** | RBAC / 审计日志 / PII 脱敏 / 策略引擎 |
| 10 | **持久化执行** | 检查点 + 故障恢复 + 热迁移 |
| 11 | **技能市场** | 技能即包，语义化版本 + 依赖解析 + 灰度发布 |
| 12 | **开源 + MIT** | 零厂商锁定，可自托管、可商用、可 fork |

---

## 4. 中栏：核心组件（16 个抽象接口）

按 Layer 分组呈现。

### 4.1 Layer 0 — 驱动层

| 接口 | 职责 | 并行项目 |
|------|------|---------|
| `ILLMProvider` | 统一 LLM 调用（Chat + Stream + Function Calling） | Spring AI `ChatClient` |
| `IEmbeddingProvider` | 文本 → 向量 | Spring AI `EmbeddingClient` |
| `IImageProvider` | 图像生成 / 视觉理解 | Spring AI `ImageClient` |
| `ISpeechProvider` | STT / TTS | — |

### 4.2 Layer 1 — 系统调用层

| 接口 | 职责 |
|------|------|
| `IAgent` | Agent 实体，封装 Loop + State |
| `IAgentLoop` | ReAct / Plan-and-Execute / 状态图 可插拔执行策略 |
| `IToolRegistry` | 工具注册与发现（装饰器 / MCP / Agent-as-Tool） |
| `IMcpClient` | MCP 协议客户端 |
| `IToolInvocationPipeline` | 工具调用管道（鉴权、限流、缓存、重试） |

### 4.3 Layer 2 — 存储层

| 接口 | 职责 | 并行项目 |
|------|------|---------|
| `IWorkingMemory` | 短期记忆（会话上下文） | LangGraph `CheckpointSaver` |
| `ILongTermMemory` | 长期记忆（语义检索） | Mem0 / Letta |
| `IVectorStore` | 向量存储与相似度检索 | Spring AI `VectorStore` |
| `IEntityMemory` | 实体记忆（用户/组织/技能档案） | CrewAI Entity Memory |
| `IObservationMemory` | 观察记忆（从交互中自动提取模式） | Agno / Mastra |

### 4.4 Layer 3 — 编排层

| 接口 | 职责 |
|------|------|
| `IOrchestrator` | 多 Agent 编排（顺序 / 并行 / 条件 / 补偿） |
| `IWorkflowEngine` | 状态图引擎（节点 + 边 + 状态） |
| `IHandoffProtocol` | Agent 间移交 |

### 4.5 Layer 4 — 技能路由层

| 接口 | 职责 |
|------|------|
| `ISkillRouter` | 多信号评分路由（语义 + 使用频率 + 成本 + 成功率） |
| `ISkillRegistry` | 技能仓库（本地 / 远程 / 市场） |
| `ISkillLoader` | 按需加载技能代码 |

### 4.6 Layer 5 — 技能演化层

| 接口 | 职责 |
|------|------|
| `ISkillReflection` | 反思执行轨迹，生成改进补丁 |
| `ISkillGateway` | 质量门禁（单测 / 回归 / 安全扫描） |
| `ISkillDeployment` | 灰度发布 / 回滚 / 废弃 |

### 4.7 Layer 6 — IPC 层

| 接口 | 职责 |
|------|------|
| `IMessageBus` | 异步消息总线（本地 + 分布式） |
| `IAgentDirectory` | Agent 注册与发现（A2A Agent Card） |
| `IA2AClient` / `IA2AServer` | A2A 协议 |
| `ICheckpointStore` | 检查点持久化 |

### 4.8 Layer 7 — 治理层

| 接口 | 职责 |
|------|------|
| `IPolicyEngine` | 策略执行（RBAC / 数据分级 / 合规） |
| `IAuditTrail` | 审计日志（谁在何时做了什么） |
| `IPIIRedactor` | PII 脱敏 |
| `IRateLimiter` | 速率限制 |
| `ITelemetry` | 可观测性（OpenTelemetry） |

---

## 5. 右栏：可插拔后端清单

### 5.1 LLM Provider

| 后端 | 状态 | 包名 |
|------|------|------|
| OpenAI | ✅ | `Dawning.Agents.LLM.OpenAI` |
| Azure OpenAI | ✅ | `Dawning.Agents.LLM.Azure` |
| Ollama | ✅ | `Dawning.Agents.LLM.Ollama` |
| Anthropic | 🟡 规划中 | `Dawning.Agents.LLM.Anthropic` |
| Google Vertex | 🟡 规划中 | `Dawning.Agents.LLM.Google` |
| HuggingFace | 🟡 规划中 | `Dawning.Agents.LLM.HuggingFace` |

### 5.2 向量存储

| 后端 | 状态 |
|------|------|
| PGVector | 🟡 规划中 |
| Qdrant | 🟡 规划中 |
| Milvus | 🟡 规划中 |
| ChromaDB | 🟡 规划中 |
| Azure AI Search | 🟡 规划中 |
| 内存（测试） | ✅ |

### 5.3 消息总线

| 后端 | 状态 |
|------|------|
| 内存（单进程） | ✅ |
| RabbitMQ | 🟡 规划中 |
| Azure Service Bus | 🟡 规划中 |
| Redis Streams | 🟡 规划中 |
| Kafka | 🟡 规划中 |

### 5.4 检查点 / 状态持久化

| 后端 | 状态 |
|------|------|
| 内存 | ✅ |
| Redis | 🟡 规划中 |
| PostgreSQL | 🟡 规划中 |
| SQLite | 🟡 规划中 |

### 5.5 技能注册表

| 后端 | 状态 |
|------|------|
| 本地文件系统 | 🟡 规划中 |
| 数据库 | 🟡 规划中 |
| 远程 Registry（HTTP） | 🟡 规划中 |
| OCI 镜像仓库 | 🔵 探索中 |

### 5.6 可观测性

| 后端 | 状态 |
|------|------|
| OpenTelemetry（OTLP） | ✅ |
| Application Insights | 🟡 规划中 |
| Prometheus | 🟡 规划中 |
| Langfuse | 🔵 探索中 |

---

## 6. 对比：Dawning vs Spring AI vs MAF

| 维度 | Spring AI | MAF | Dawning |
|------|-----------|-----|---------|
| 生态 | Java / Spring Boot | .NET + Python | .NET |
| LLM 抽象 | ✅ `ChatClient` | ✅ `IChatClient` | ✅ `ILLMProvider` |
| Embedding 抽象 | ✅ | ✅ | ✅ |
| Vector Store 抽象 | ✅ 9+ 实现 | ⚠️ 有但少 | 🟡 规划 3+ 实现 |
| RAG Pipeline | ✅ 内建 | ⚠️ 手动组装 | 🟡 规划（Layer 2） |
| Agent Loop | ❌ 轻量 | ✅ | ✅ 可插拔策略 |
| 多 Agent 编排 | ❌ | ✅ Workflows | ✅ 4 种原语 |
| A2A 协议 | ❌ | ✅ 原生 | ✅ 原生 |
| MCP 协议 | 🟡 实验性 | ✅ 原生 | ✅ 原生 |
| 技能演化 | ❌ | ❌ | ✅ Layer 5 独有 |
| 四级 Scope | ❌ | ❌ | ✅ 独有 |
| 分布式三面体 | ❌ | ⚠️ Durable Agent | ✅ 三面架构独有 |
| 技能市场 | ❌ | ❌ | 🔵 探索中 |

---

## 7. 注册一条链：完整示例

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    // ── Layer 0: 驱动 ───────────────────────────────
    .AddLLMProvider(llm => {
        llm.UseOpenAI(builder.Configuration.GetSection("OpenAI"));
        llm.UseFallback<OllamaProvider>();
    })
    .AddEmbeddingProvider<OpenAIEmbeddingProvider>()

    // ── Layer 1: 系统调用 ──────────────────────────
    .AddAgentOSKernel()
    .AddMcpClient(mcp => {
        mcp.AddStdioServer("filesystem", "...");
        mcp.AddSseServer("github", "https://mcp.github.com/sse");
    })

    // ── Layer 2: 存储 ──────────────────────────────
    .AddMemoryPlane(memory => {
        memory.UseRedisWorkingMemory(builder.Configuration.GetConnectionString("Redis"));
        memory.UseLLMWikiLongTermMemory();
        memory.UsePgVectorStore(builder.Configuration.GetConnectionString("Postgres"));
    })

    // ── Layer 3: 编排 ──────────────────────────────
    .AddOrchestrator()

    // ── Layer 4-5: 技能 ────────────────────────────
    .AddSkillRouter()
    .AddSkillEvolution()

    // ── Layer 6: IPC ──────────────────────────────
    .AddMessageBus(bus => bus.UseRabbitMq("amqp://..."))
    .AddA2AServer(a2a => a2a.PublishAgentCard(...))

    // ── Layer 7: 治理 ──────────────────────────────
    .AddPolicyEngine()
    .AddAuditTrail()
    .AddOpenTelemetry();
```

**整条链路仍保持"极简 API + 合理默认值 + 一行注册"的核心原则。**

---

## 8. 小结

> Spring AI 证明了**统一抽象 + 多后端**的模式在 Java 生态的成功。
> Dawning 把这个模式扩展到整个 Agent OS 栈——16 个抽象接口 × N 个后端实现 × 8 层 OS 架构。
>
> 结果：一个项目从 Hello World 到生产级分布式 Agent 系统，**不需要重写代码，只需要换配置**。
