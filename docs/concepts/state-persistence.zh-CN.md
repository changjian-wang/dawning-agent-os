---
title: "Agent 状态持久化与断点恢复：Checkpoint、Durable Execution、长任务架构"
type: concept
tags: [state, persistence, checkpoint, durable-execution, langgraph, temporal, letta, memgpt, recovery]
sources: [concepts/memory-architecture.zh-CN.md, concepts/deployment-architectures.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agent 状态持久化与断点恢复：Checkpoint、Durable Execution、长任务架构

> Agent 挂了怎么办？
> - 普通 Chatbot：重试就行。
> - Deep Research Agent 跑 30 分钟挂了：30 分钟的工作和 $10 的 token 全没了。
> - 客服工单 Agent 处理一半断网：客户白等。
> - 后台批量 Agent 机器迁移：全部失败。
>
> 本文梳理 Agent 状态持久化的模型、工程模式、主流实现（LangGraph Checkpoint、Temporal、Letta/MemGPT），以及 Dawning 分布式架构的状态设计。

---

## 1. 为什么需要持久化

### 1.1 失败场景

| 场景 | 后果 |
|------|------|
| 进程 crash / OOM | 对话中断，用户要重来 |
| 机器维护 / K8s 迁移 | Agent 被杀 |
| LLM API 限流 / 超时 | 中途失败 |
| 工具调用失败（网络） | 整个任务要回滚 |
| 用户长任务中途暂停 | 几小时后想接着做 |
| 审批等待（HITL） | Agent 卡等 |
| 跨 region 故障切换 | 用户会话丢失 |

### 1.2 状态的三个层次

```
L1: Session State       (对话短期上下文)
     |
L2: Agent Run State     (一次任务的完整 trace + 中间 state)
     |
L3: Long-Term Memory    (跨任务的知识、事实、历史)
```

L1/L2 在本文重点讨论；L3 见 [[concepts/memory-architecture.zh-CN]]。

---

## 2. 持久化模型

### 2.1 Stateless（无状态）

```
每次请求携带全部 context → 服务端只算，不存
```

**代表**：基础 Chat API。

**优劣**：
- ✅ 简单
- ✅ 扩展容易
- ❌ 长历史昂贵
- ❌ 客户端压力大

### 2.2 Session 粘性（Stateful Sticky）

```
会话绑定到单机 → 内存里存 context
```

**优劣**：
- ✅ 性能好
- ❌ 机器挂了数据丢
- ❌ 负载均衡困难

### 2.3 Checkpoint（快照）

```
关键节点存快照（DB / 对象存储）
  → 重启可恢复
```

**主流**：LangGraph Checkpointer、Dapr Actor State。

### 2.4 Event Sourcing（事件溯源）

```
每个事件追加到 event log
  → 任意时刻重放可重建 state
```

**代表**：Letta Agent Server、Dawning IAgentEventStream。

### 2.5 Durable Execution（持久执行）

```
整个工作流被引擎管理
  → 每一步的 input/output 入持久化队列
  → crash 后自动从上次成功处继续
```

**代表**：Temporal、Restate、Dapr Workflow。

---

## 3. LangGraph Checkpointer 深度剖析

### 3.1 设计

LangGraph 把 Agent 建模为**状态图**：

```
节点 = 函数 (state → new_state)
边   = 条件 / 流程控制
State = TypedDict
```

每次图中节点执行后，**State 被持久化**：

```python
graph.compile(checkpointer=PostgresSaver(...))

# 每次运行
result = graph.invoke(
    {"input": "..."},
    config={"configurable": {"thread_id": "user-123"}})

# 恢复
result = graph.invoke(
    None,  # 从 checkpoint 继续
    config={"configurable": {"thread_id": "user-123"}})
```

### 3.2 Checkpoint 内容

- 完整 state（对话 + 工作变量）
- 当前节点位置
- 全部历史 checkpoint（可时光回溯）
- pending tasks

### 3.3 存储后端

- InMemory (开发)
- SQLite
- Postgres
- Redis
- 自定义 Checkpointer

### 3.4 关键能力

- **Thread**：独立持久会话
- **Interrupt**：HITL 暂停点（审批）
- **Time Travel**：回到任意 checkpoint 重放
- **Human-in-loop 修改 state**：审批人可改

### 3.5 局限

- 每步存储成本（大 state 贵）
- Thread 粒度不够细（想要用户 + 任务双维度）
- 跨机未显式分区（靠外部 DB 分担）

---

## 4. Temporal Durable Execution

### 4.1 设计理念

**把工作流代码当作确定性函数**：
- 每次 I/O（activity）结果都记录
- crash 后 workflow 从 event log 重建 state
- 代码"看起来"像普通函数，实际是可恢复的

### 4.2 例子

```go
func AgentWorkflow(ctx workflow.Context, input string) (string, error) {
    // 每个 activity 结果都被持久化
    context := workflow.ExecuteActivity(ctx, RetrieveContext, input).Get(...)
    plan    := workflow.ExecuteActivity(ctx, Plan, context).Get(...)
    
    for _, step := range plan.Steps {
        result := workflow.ExecuteActivity(ctx, ExecuteStep, step).Get(...)
        // 即使这里 crash，重启后会从上一个 activity 继续
    }
    
    return workflow.ExecuteActivity(ctx, Finalize, ...).Get(...)
}
```

### 4.3 核心原语

- **Workflow**：长任务的编排代码
- **Activity**：可失败重试的单步
- **Signal**：外部事件注入（用户消息、审批）
- **Timer**：可靠定时（小时/天级）
- **Child Workflow**：子流程

### 4.4 与 Agent 的融合

```
Workflow = Agent Run
  Activity: call_llm
  Activity: call_tool
  Activity: retrieve
  Signal: user_input / approval
  Timer:   wait_for_external_event
```

### 4.5 优势

- 天然支持小时 / 天级长任务
- 故障恢复
- 可观测（Temporal Web UI）
- 多语言（Go / Java / Python / TS / .NET）

### 4.6 劣势

- 学习曲线
- 代码需遵守确定性约束
- 多一层基础设施

### 4.7 对手 / 替代

- **Restate**：类似理念，更轻量，RPC 风格
- **Dapr Workflow**：K8s 原生
- **Azure Durable Functions**：Azure 专用
- **Cadence**：Uber 原版，Temporal 前身

---

## 5. Letta / MemGPT 的 Agent as Stateful Service

### 5.1 核心理念

**"Agent as a State Machine"** —— MemGPT 论文 2023 提出：

- Agent 拥有**持久身份**
- State 在 DB 中持续存在
- 服务器端 run loop
- 客户端只发消息

### 5.2 状态结构

```
Letta Agent:
  ├── Core Memory (系统 prompt-like，常驻)
  ├── Recall Memory (完整消息历史)
  ├── Archival Memory (向量库长期记忆)
  ├── Tools
  └── Model Settings
```

### 5.3 API 特点

```
POST /agents              → 创建持久 Agent
POST /agents/{id}/messages → 发消息 (状态自动更新)
GET  /agents/{id}/state   → 获取状态
PUT  /agents/{id}/memory  → 修改长期记忆
```

**关键**：客户端不传历史，服务器管。

### 5.4 优势

- 真正的持久 Agent
- 跨设备一致
- 适合"Agent 作为服务"产品（AI 助手）

### 5.5 劣势

- 服务器侧复杂度高
- 无状态扩展能力打折
- 迁移 / 备份成本

---

## 6. OpenAI Assistants v2 的状态模型

### 6.1 服务器侧 Thread

```
POST /threads            → 创建 thread
POST /threads/{id}/messages
POST /threads/{id}/runs  → 跑 Assistant
```

- **Thread**：对话历史服务器管
- **Run**：一次 Agent 执行，可分步
- **Step**：Run 内的每个 tool/message

### 6.2 与 Letta 对比

| 维度 | Letta | OpenAI |
|------|-------|--------|
| 持久身份 | ✅ Agent 是实体 | 部分（Assistant 是配置，Thread 是会话） |
| 长期记忆 | ✅ 多层 | ❌ 需用户自管 |
| 开源 | ✅ | ❌ |
| 企业部署 | ✅ | ❌ |

---

## 7. 状态切片维度

### 7.1 维度矩阵

```
Tenant (组织)
  ├── User (用户)
  │     ├── Session (会话)
  │     │     └── Run (一次 Agent 任务)
  │     │           └── Step (一步)
  │     └── Long-term Memory
  └── Shared Memory
```

### 7.2 存储选型

| 维度 | 热 | 冷 |
|------|----|----|
| Session/Run | Redis / Postgres | — |
| Long-term facts | Postgres / pgvector | S3 + 归档 |
| Event log | Kafka / Postgres WAL | S3 Parquet |
| Checkpoints | Postgres / Redis | S3 |
| Artifacts（生成文件） | S3 / MinIO | S3 Glacier |

---

## 8. Checkpoint vs Event Sourcing

### 8.1 对比

| 维度 | Checkpoint | Event Sourcing |
|------|-----------|----------------|
| 存什么 | 当前 state 快照 | 所有事件 |
| 恢复方式 | 加载快照 | 重放事件 |
| 存储大小 | 中 | 大（随时间增长） |
| 查询 | 当前快 | 历史快 |
| 审计 | 弱 | 强（天然全历史） |
| 时光回溯 | 需多版本 | 原生 |
| 实现复杂度 | 低 | 高 |

### 8.2 混合方案

```
Event Log (全部事件，append-only)
     ↓
Snapshot (每 N 事件生成一次)
     ↓
读取 = Snapshot + 后续 events
```

Kafka + Postgres WAL 都这么干。

### 8.3 对 Agent 的启发

- **每 step 生成 event**（LLMCall, ToolInvocation, MemoryUpdate, Decision）
- **每 N step 或关键节点 checkpoint**
- Event log 支持审计、回放、Dataset 构建

---

## 9. 长任务特有挑战

### 9.1 LLM API 限流 / 超时

- 单请求有 timeout（2-10 分钟）
- 长任务 = 多次 LLM 调用
- 任一失败需可重试
- **幂等性**：避免重复副作用

### 9.2 工具失败

- 外部 API 间歇失败
- 重试策略：exponential backoff + 熔断（Polly）
- 工具级幂等 token（避免重复下单）

### 9.3 副作用

**关键**：Durable Execution 重启时会"重放"，但不能重放**副作用**。

解决：
- 副作用封在 Activity 里
- Activity 结果被 checkpoint
- 重启时读 checkpoint，不再调用

### 9.4 用户暂停

```
Agent 问："下一步要不要批准？"
  ↓ 设置 interrupt / signal
Agent 暂停，state 持久化
  ↓ 用户数小时后回来
唤起 → 注入用户响应 → 继续
```

**前提**：状态必须持久。

### 9.5 跨时区 / 跨天

- 长任务可能横跨时间段
- 需可靠定时器
- 时区处理

---

## 10. HITL（Human-in-the-Loop）持久化

### 10.1 典型场景

```
Agent 准备执行高风险操作（如转账）
  → 暂停，通知审批人
  → 审批人批/驳（可能几小时）
  → Agent 继续
```

### 10.2 要求

- State 完全持久
- Timer（超时自动处理）
- 外部 signal 通道（email/Slack 回调）
- 审批结果记录

### 10.3 实现

- Temporal `Await(signal)` 原生
- LangGraph `interrupt_before` + `resume`
- Dawning 规划 `IApprovalWorkflow`

---

## 11. 分布式 Agent 的状态挑战

### 11.1 典型部署

```
Load Balancer
     ↓
Agent Pods (N 个副本)
     ↓ 共享
Redis / Postgres (State)
```

### 11.2 粘性 vs 无粘性

**粘性（Sticky session）**：
- 同用户路由到同 pod
- 本地缓存快
- 挂了数据失（需 backup）

**无粘性**：
- 每次从 state store 加载
- 扩展灵活
- 每次延迟

**推荐**：Redis session + 定期 Postgres 持久化。

### 11.3 并发控制

同一会话同时两个请求：
- 乐观锁（version 字段）
- 分布式锁（Redis）
- 串行化（同 thread 串行处理）

LangGraph 用 thread_id + checkpoint version 做乐观并发。

### 11.4 跨 region HA

- Primary / Replica Postgres
- 多 region Redis (Redis Enterprise)
- Event log 跨 region 复制（Kafka MirrorMaker）
- Failover 演练

---

## 12. 状态迁移与升级

### 12.1 模式演化

```
Agent v1 的 state 字段 
  ↓ 升级到 v2
Schema 变了？
  ↓
需要迁移脚本 / 版本化 state
```

### 12.2 策略

- **State Schema 版本号**
- **向前兼容**（新字段可选）
- **Migration Task**（一次性迁移存量）
- **双写过渡**（新旧读同写）

### 12.3 Prompt 版本化影响

Prompt 变了，重放旧 events 结果不同——
这是 Durable Execution 的经典问题。

Temporal 方案：
- Workflow 版本标记
- 老 workflow 用老代码路径
- 新 workflow 用新代码路径

---

## 13. 可观测性与状态

### 13.1 Trace 关联

- 每 Run 的全部 step 一条 trace
- 恢复后接续同一 trace
- Span attributes：`run.id` / `checkpoint.id` / `thread.id`

### 13.2 Dashboard

- 长时任务在途数量
- 卡住等审批的数量
- 失败重试次数
- 每 Run 平均时长、成本

### 13.3 调试

- 时光回溯：任意 checkpoint 重新运行
- Replay with patched prompt：做回归
- 状态 diff（每步 state 变化）

---

## 14. 方案选型决策

```
任务时长？
  秒级         → Stateless / Session
  分钟级       → Checkpoint
  小时/天级    → Durable Execution
  
需要 HITL？
  No          → Checkpoint 可能够
  Yes         → Durable + Signal
  
跨 region HA？
  No          → 本地持久化
  Yes         → 多区域复制 + Event log
  
审计需求？
  低           → Snapshot only
  强           → Event Sourcing
  
团队熟悉？
  Python      → LangGraph Checkpointer
  Go / 多语言  → Temporal
  .NET        → Dawning + Postgres + 规划 Durable
  云原生       → Dapr Workflow
```

---

## 15. Dawning 分布式状态架构

### 15.1 架构总览

```
┌─────────────────────────────────────────┐
│  Host Mode                              │
│  ┌───────────────────────────────┐     │
│  │  Agent Runtime                 │     │
│  │  └─ IAgentStateStore           │     │
│  │       │                         │     │
│  │       ├─ SessionStore (Redis)   │     │
│  │       ├─ RunStore (Postgres)    │     │
│  │       ├─ EventLog (Postgres/Kafka)│   │
│  │       └─ CheckpointStore (Postgres/S3)│
│  └───────────────────────────────┘     │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│  Worker Mode                            │
│  ┌───────────────────────────────┐     │
│  │  Agent Worker                  │     │
│  │  └─ 共享 IAgentStateStore      │     │
│  │     （相同接口、相同后端）        │     │
│  └───────────────────────────────┘     │
└─────────────────────────────────────────┘
```

### 15.2 核心接口（规划）

```csharp
public interface IAgentStateStore
{
    Task<AgentState?> GetAsync(string threadId, CancellationToken ct);
    Task SaveAsync(string threadId, AgentState state, CancellationToken ct);
    Task<IReadOnlyList<AgentState>> GetHistoryAsync(string threadId, CancellationToken ct);
}

public interface IAgentCheckpointer
{
    Task<string> CheckpointAsync(AgentContext ctx, CancellationToken ct);
    Task<AgentContext> RestoreAsync(string checkpointId, CancellationToken ct);
}

public interface IAgentEventStream  // 已存在
{
    Task AppendAsync(AgentEvent evt, CancellationToken ct);
    IAsyncEnumerable<AgentEvent> ReplayAsync(string runId, CancellationToken ct);
}
```

### 15.3 多实现

```
Dawning.State.Redis
Dawning.State.Postgres
Dawning.State.EfCore
Dawning.State.Dapr     (走 Dapr Actor)
Dawning.Workflow.Temporal  (适配 Temporal)
```

### 15.4 与 MCP/A2A 协同

- A2A 任务跨 Agent 转移时传递 state pointer
- MCP 工具调用幂等 token 避免副作用重复
- 跨语言状态通过 Event Stream 共享

### 15.5 治理

- 敏感 state 加密（Layer 7 IDataProtection）
- PII 脱敏入 state 前处理
- State 保留期限（GDPR）
- 审计 state 读写

### 15.6 Scope 与持久化

见 [[concepts/memory-architecture.zh-CN]]：

- Session Scope → Redis
- Task Scope → Postgres
- User Scope → Postgres + 向量库
- Global Scope → 只读 KB

---

## 16. 实战样例：长研究任务

```
Agent 接收："研究 2026 AI Agent 框架并写 10 页报告"

执行：
  Step 1: Plan (10 subtasks)                   → Checkpoint 1
  Step 2: 检索 (5 searches)                     → Checkpoint 2
  Step 3: 读取 / 总结 (50 docs)                 → Checkpoint 3
     [Worker crash] ← Host 继续
  Step 4: 分主题综合                             → Checkpoint 4
  Step 5: 生成章节 (10 chapters, parallel)      → Checkpoint 5
  Step 6: 写导论 / 结论                         → Checkpoint 6
  Step 7: 给用户预览 → wait signal              → Pause
  
  [2 小时后用户回来]
  Step 8: 按反馈修改                            → Checkpoint 7
  Step 9: 输出 PDF                             → Done

Telemetry:
  - 总耗时、总 token、总成本
  - 每 step 的 latency / cost
  - Crash 次数 / 恢复次数
  - 审批等待时间
```

---

## 17. 常见陷阱

### 17.1 Check point 太大

- 每步存 50MB 对话历史 → 存储 / 带宽爆炸
- **对策**：增量 checkpoint + 摘要 + 引用

### 17.2 副作用重复

- Agent 重启后又发了一遍邮件
- **对策**：幂等 token + Activity 隔离

### 17.3 跨版本失败

- 升级后老 state schema 不认
- **对策**：version + migration

### 17.4 锁死 deadlock

- 并发请求同 thread
- **对策**：排他锁 + queue

### 17.5 成本失控

- 长任务无预算 → 跑 3 天花 $500
- **对策**：成本 hard cap → 触发暂停

### 17.6 State 泄漏

- 返回 state 给客户端（含敏感字段）
- **对策**：DTO 白名单输出

---

## 18. 性能优化

### 18.1 增量 checkpoint

只存变更部分（delta）。

### 18.2 异步持久化

写 checkpoint 不阻塞 agent 进展（但有丢失风险）。

### 18.3 批量 event

事件先聚合到缓冲，批量 flush。

### 18.4 冷热分离

近期 state 在 Redis，历史归档 S3。

### 18.5 压缩

JSON → Protobuf / Cap'n Proto / MessagePack。

---

## 19. 小结

> Agent 要"生产可靠"，不是 agent loop 跑通就算——
> **crash 能复活、HITL 能等、长任务不弃单、迁移不丢单** 才算。
>
> 状态持久化不是可选，是 Agent OS 的地基：
> - LangGraph Checkpointer：Python Agent 首选
> - Temporal：多语言长任务王者
> - Letta / OpenAI Assistants：Agent-as-Service 模型
> - Dawning：Layer 0 抽象 + 多实现 + Event Stream + Scope，可选 Durable 适配器
>
> 选型关键：**任务时长 × HITL × HA × 审计** 四维。

---

## 20. 延伸阅读

- [[concepts/memory-architecture.zh-CN]] — Scope / 两层记忆
- [[concepts/deployment-architectures.zh-CN]] — Host/Worker/Sidecar
- [[concepts/observability-deep.zh-CN]] — Trace 与 state
- [[concepts/multi-agent-patterns.zh-CN]] — 跨 Agent state 传递
- LangGraph Checkpointer: <https://langchain-ai.github.io/langgraph/concepts/persistence/>
- Temporal: <https://docs.temporal.io/>
- Restate: <https://restate.dev/>
- Letta (MemGPT): <https://github.com/letta-ai/letta>
- Dapr Workflow: <https://docs.dapr.io/developing-applications/building-blocks/workflow/>
- OpenAI Assistants API: <https://platform.openai.com/docs/assistants>
- MemGPT 论文: <https://arxiv.org/abs/2310.08560>
