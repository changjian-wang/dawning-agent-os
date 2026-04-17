---
title: "工作流引擎 vs Agent：Temporal、Restate、Dapr Workflow、Airflow、Prefect 与 Agent 的边界"
type: comparison
tags: [workflow, temporal, restate, dapr, airflow, prefect, dagster, agent-vs-workflow, durable-execution]
sources: [concepts/state-persistence.zh-CN.md, concepts/multi-agent-patterns.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 工作流引擎 vs Agent：Temporal、Restate、Dapr Workflow、Airflow 与 Agent 的边界

> "什么时候用 Agent，什么时候回到 Workflow？"
> 这是 2025-2026 企业落地最常见的决策困惑。
> 把所有事都做成 Agent → 又贵又慢又难调试；
> 把所有事都做成 Workflow → 失去 LLM 自适应价值。
>
> 本文厘清工作流引擎谱系、与 Agent 的本质区别、混合架构模式、Dawning 的位置。

---

## 1. 概念辨析

### 1.1 一句话定义

| 概念 | 定义 |
|------|------|
| **Workflow** | 预定义步骤的可靠编排（每步做什么、何时做、出错怎样都已设计） |
| **Agent** | LLM 自主决定下一步（每步做什么由模型即时推理决定） |
| **Durable Workflow** | Workflow + 持久化（崩溃可恢复、长运行） |
| **Agent Workflow** | Agent 步骤被 workflow 引擎管理（融合） |

### 1.2 决策光谱

```
完全确定 ──────────────────────────────────► 完全自主

Cron Job ─ Airflow ─ Temporal ─ LangGraph ─ ReAct Agent ─ Devin
                       │                       │
                       │                       │
              Durable Workflow         Autonomous Agent
```

### 1.3 关键差异

| 维度 | Workflow | Agent |
|------|----------|-------|
| 步骤来源 | 编程定义 | LLM 决定 |
| 可预测性 | 高 | 低 |
| 可审计 | 强 | 中 |
| 适应性 | 弱 | 强 |
| 调试 | 易 | 难 |
| 成本 | 低 | 高 |
| 延迟 | 可控 | 高 |
| 适合任务 | 已知流程 | 模糊 / 探索 |

---

## 2. 工作流引擎谱系

### 2.1 数据 / ETL 派

| 引擎 | 出品 | 特点 |
|------|------|------|
| **Apache Airflow** | Apache | 老牌，DAG 调度 |
| **Prefect** | Prefect | Python，开发者友好 |
| **Dagster** | Dagster | 数据资产模型 |
| **Luigi** | Spotify | 早期 |
| **Mage** | Mage | 现代化 |
| **Argo Workflows** | CNCF | K8s 原生 |

### 2.2 通用编排派

| 引擎 | 特点 |
|------|------|
| **Temporal** | Durable Execution 王者 |
| **Restate** | 轻量、RPC 风格、新兴 |
| **Cadence** | Uber，Temporal 前身 |
| **Conductor** | Netflix |
| **Camunda** | BPMN 派 |
| **Zeebe** | Camunda 现代化 |
| **AWS Step Functions** | AWS 托管 |
| **Azure Durable Functions** | Azure 托管 |
| **Google Cloud Workflows** | GCP |

### 2.3 Serverless / 事件派

| 引擎 | 特点 |
|------|------|
| **Inngest** | TypeScript-first，事件驱动 |
| **Trigger.dev** | TS / 长任务 |
| **Hatchet** | Postgres 持久 |
| **Defer** | TS Serverless |

### 2.4 微服务 / Sidecar 派

| 引擎 | 特点 |
|------|------|
| **Dapr Workflow** | K8s sidecar，多语言 |
| **Cadence Java/Go** | 多语言 |

### 2.5 BPMN / 业务派

| 引擎 | 特点 |
|------|------|
| **Camunda 8** | BPMN 标准 |
| **Flowable** | 开源 BPMN |
| **Activiti** | Java |

### 2.6 Agent 原生派

| 引擎 | 特点 |
|------|------|
| **LangGraph** | Agent + Checkpointer |
| **CrewAI Flows** | Agent + Pipeline |
| **Microsoft Agent Framework Workflows** | Agent + Process |
| **Dawning Workflow（规划）** | Agent OS workflow 层 |

---

## 3. Temporal 深度剖析

### 3.1 设计哲学

**"Workflow as Code"** + Durable Execution：

```python
@workflow.defn
class OrderWorkflow:
    @workflow.run
    async def run(self, order: Order) -> str:
        await workflow.execute_activity(charge_card, order)
        await workflow.execute_activity(reserve_inventory, order)
        await workflow.sleep(timedelta(days=1))  # 可靠延时
        await workflow.execute_activity(ship, order)
```

### 3.2 关键概念

- **Workflow**：长任务编排代码
- **Activity**：可重试单步
- **Signal**：外部事件
- **Query**：只读查询 state
- **Timer**：可靠定时（小时/天/月）
- **Child Workflow**：嵌套
- **Versioning**：代码升级兼容

### 3.3 Determinism 约束

Workflow 代码必须确定性：
- 不能直接 `time.now()`（用 `workflow.now()`）
- 不能直接 random（用 workflow random）
- 不能直接调 API（包成 Activity）

### 3.4 多语言

- Python / Go / Java / TypeScript / .NET / PHP / Ruby

### 3.5 部署

- Self-hosted (Cassandra / Postgres / MySQL)
- Temporal Cloud (托管)

### 3.6 在 Agent 场景的价值

```
Workflow:
  - 长任务编排框架
  - 每个 Activity 是 Agent step (LLM call / tool / etc.)
  - 自动重试、可恢复、HITL signal
  
Agent step inside Activity:
  - LLM 调用
  - 工具执行
  - state checkpoint
```

**模式**：把 Agent 步骤包装成 Activity，Workflow 管编排。

---

## 4. Restate 深度剖析

### 4.1 设计哲学

**Durable Execution + RPC 风格**：

```typescript
@service({ name: "agent" })
export class AgentService {
  @handler
  async run(ctx: Context, request: Request): Promise<Response> {
    const plan = await ctx.run("plan", () => generatePlan(request));
    
    for (const step of plan.steps) {
      await ctx.run(`step-${step.id}`, () => executeStep(step));
    }
    
    return await ctx.run("finalize", () => finalize());
  }
}
```

### 4.2 与 Temporal 对比

| 维度 | Temporal | Restate |
|------|----------|---------|
| 范式 | Workflow + Activity | Service + Handler |
| 部署 | 复杂（多组件） | 单二进制 |
| 持久化 | DB | 内置 RocksDB |
| 学习曲线 | 中-高 | 低 |
| 成熟度 | 成熟 | 新兴 |
| 多语言 | 多 | TS / Java / Python / Go |

### 4.3 优势

- **极简部署**
- **天然 RPC 心智**
- **轻量**
- 现代化设计

### 4.4 劣势

- 生态比 Temporal 小
- 大规模生产案例少

---

## 5. Dapr Workflow 深度剖析

### 5.1 设计

K8s Sidecar 风格：

```
Application (任意语言)
  ↕ HTTP/gRPC
Dapr Sidecar
  ├── Workflow Engine
  ├── State Store
  ├── Pub/Sub
  └── ...
```

### 5.2 写法

```csharp
public class OrderWorkflow : Workflow<Order, string>
{
    public override async Task<string> RunAsync(WorkflowContext ctx, Order order)
    {
        await ctx.CallActivityAsync("ChargeCard", order);
        await ctx.CallActivityAsync("ReserveInventory", order);
        await ctx.CreateTimer(TimeSpan.FromHours(1));
        return await ctx.CallActivityAsync<string>("Ship", order);
    }
}
```

### 5.3 优势

- K8s 原生
- 多语言
- 与 Dapr 其他能力（State / Pub/Sub / Bindings）一体
- CNCF 项目

### 5.4 劣势

- 须用 Dapr 全家桶
- 比 Temporal 功能少
- 调度算法不如 Temporal 成熟

---

## 6. Airflow / Prefect / Dagster 深度剖析

### 6.1 数据派定位

主要用于 **批处理 / ETL / ML pipeline**：

```python
@dag(schedule="@daily")
def etl_pipeline():
    @task
    def extract(): ...
    
    @task
    def transform(data): ...
    
    @task
    def load(data): ...
    
    load(transform(extract()))
```

### 6.2 与通用 Workflow 区别

- 主要 batch（定时调度）
- 强调数据 lineage / 资产
- 通常**不强求** durable per-task crash recovery
- 不擅长长时单实例

### 6.3 Agent 在数据栈中的角色

- 不是替代 Airflow
- 而是**Airflow Task 内调用 Agent**
- 例：每日数据 pipeline 中一步是"Agent 总结昨日异常"

---

## 7. LangGraph 作为"Agent Workflow 引擎"

### 7.1 定位

**不是通用 Workflow，是 Agent 编排专用 + Checkpointer**：

```python
graph = StateGraph(State)
graph.add_node("retrieve", retrieve_fn)
graph.add_node("agent", agent_fn)
graph.add_node("tool", tool_fn)
graph.add_conditional_edges("agent", router)
graph.compile(checkpointer=PostgresSaver())
```

### 7.2 与 Temporal 对比

| 维度 | LangGraph | Temporal |
|------|-----------|----------|
| 范围 | Agent 专用 | 通用 |
| 心智 | 状态图 | Workflow + Activity |
| 长任务 | 有 checkpointer | 原生支持 |
| HITL | interrupt | signal |
| 多语言 | Python only | 多语言 |
| 大规模 | 中 | 强 |
| 学习曲线 | 低（如果懂图） | 中-高 |

### 7.3 何时选 LangGraph 而非 Temporal

- 团队 Python
- 主要做 Agent
- 不需要 month-level 任务
- 想要轻量

### 7.4 何时选 Temporal

- 多语言团队
- 长 / 复杂业务流程（不只 Agent）
- 需要工业级 SLA
- 已有 Temporal 经验

---

## 8. Workflow vs Agent 决策框架

### 8.1 关键问题

```
1. 流程是否已知？
   是 → Workflow
   否 → Agent

2. 步骤数固定？
   是 → Workflow
   否 → Agent (允许动态)

3. 决策点是否需要 LLM 推理？
   不需要 → Workflow
   需要 → Agent (或 Workflow + Agent task)

4. 是否需要审计每一步因果？
   严格 → Workflow（更可解释）
   宽松 → Agent

5. 单任务时长？
   秒-分钟 → Agent / Lambda 都可
   小时-天 → Durable Workflow
   月级 → Temporal 专长
```

### 8.2 维度组合

| 流程已知 | 长时 | 推荐 |
|---------|------|------|
| ✅ | ❌ | 普通函数 / Lambda |
| ✅ | ✅ | Temporal / Restate / Dapr |
| ❌ | ❌ | Agent |
| ❌ | ✅ | Agent + Durable Workflow（混合） |

---

## 9. 混合架构模式

### 9.1 Pattern A：Workflow as Skeleton, Agent as Step

```
Temporal Workflow:
  - Activity 1: 数据检索 (传统)
  - Activity 2: Agent 推理 (LLM)
  - Activity 3: HITL 审批 (signal)
  - Activity 4: 执行操作 (传统)
  - Activity 5: 通知 (传统)
```

**适合**：业务流程清晰，但中间需要 LLM 智能。

### 9.2 Pattern B：Agent as Orchestrator, Workflow as Tool

```
Agent:
  decide("now I need to run the daily report pipeline")
  → trigger_workflow(airflow_dag_id)
  → wait for completion
  → continue with results
```

**适合**：Agent 主导，偶尔触发既定 pipeline。

### 9.3 Pattern C：双层

```
Top: Temporal Workflow (业务流程)
  ├── Activity: Agent A
  │     └── LangGraph subflow
  ├── Activity: Agent B
  │     └── ReAct loop
  └── Activity: Traditional API
```

**适合**：大型业务，多个 Agent 协作。

### 9.4 Pattern D：Event-Driven Hybrid

```
Event Bus (Kafka)
  ↓
- Workflow Listener (做规则的)
- Agent Listener (做模糊的)
合作处理同一事件
```

---

## 10. 何时不要用 Agent

### 10.1 反 Agent 信号

- 输入完全结构化
- 输出完全确定
- 步骤<5 且固定
- 严格 SLA
- 严格审计
- 高频低利润
- 出错不可逆且无 HITL

### 10.2 例子

| 任务 | 用什么 |
|------|-------|
| 用户注册 | 普通代码 |
| 每日报表 | Cron + 脚本 |
| 数据 ETL | Airflow |
| 订单处理 | Workflow (Temporal) |
| 支付重试 | Workflow |
| 客服意图分类 | LLM 单 call (不算 Agent) |
| 客服对话 | Agent |
| 故障调查 | Agent |
| 报告写作 | Agent |
| 代码生成 | Agent |

---

## 11. Workflow 中调用 LLM 的最佳实践

### 11.1 Activity 包装

```python
@activity.defn
async def llm_call(prompt: str) -> str:
    return await openai.chat.completions.create(...)
```

- 自动重试（限流）
- 超时
- 持久化结果
- 可重放

### 11.2 幂等

LLM 不幂等（结果有随机），但 Activity 重启时 Temporal **不重新调用** LLM——
直接读取上次成功的结果（这就是 Durable 价值）。

### 11.3 避免 Workflow 内直接 LLM call

- Workflow 必须确定性
- LLM 是非确定性
- 必须包成 Activity

### 11.4 长任务进度

- Workflow 用 Heartbeat 报进度
- Activity 长跑加 heartbeat
- 用户可 query 当前 state

---

## 12. Agent 步骤的可靠性补丁

### 12.1 重试

```
LLM 失败 → Activity retry policy
工具失败 → Activity retry policy
工具超时 → Activity timeout
```

### 12.2 兜底

```
Plan A: GPT-4o
  ↓ 失败
Plan B: Claude
  ↓ 失败
Plan C: 默认行为 / HITL
```

### 12.3 部分成功

- 多步任务，已完成部分要保留
- Checkpoint per step
- 失败重启从最近 checkpoint

### 12.4 取消 / 中止

- 用户取消信号
- 全局预算耗尽
- 健康检查失败

Workflow 引擎都支持 cancel + cleanup hook。

---

## 13. 可观测性融合

### 13.1 Trace 一体

- Workflow 一个 trace
- 每 Activity 一个 span
- Activity 内 Agent loop 是嵌套 spans

### 13.2 OTel 推荐

Temporal / Dapr / Restate 都有 OTel adapter。
配合 GenAI SemConv 可统一 dashboard：

```
Workflow.duration
  ├─ Activity[plan].duration
  ├─ Activity[llm].gen_ai.tokens
  ├─ Activity[tool.X].error_rate
  └─ Activity[hitl].wait_duration
```

---

## 14. 选型综合矩阵

| 场景 | 首选 | 备选 |
|------|------|------|
| Python ML pipeline | Prefect / Dagster | Airflow |
| 大规模 ETL | Airflow / Argo | Dagster |
| 多语言长 workflow | Temporal | Restate / Dapr |
| K8s sidecar 风 | Dapr Workflow | Argo |
| BPMN 业务流程 | Camunda 8 | Flowable |
| Serverless TS workflow | Inngest / Trigger.dev | Hatchet |
| Pure Agent (Python) | LangGraph | CrewAI Flows |
| Pure Agent (.NET) | Dawning / MAF | — |
| Agent + 业务流程 | Temporal + Agent | Dapr + LangGraph |
| Agent on AWS | Step Functions + Bedrock | Temporal Cloud |
| Agent on Azure | Durable Functions + Azure OpenAI | MAF Workflows |

---

## 15. Dawning 的位置

### 15.1 Dawning 不是 Workflow 引擎

- 不取代 Temporal
- 不取代 Airflow

### 15.2 Dawning 是什么

- Layer 0-3：Agent Kernel
- Layer 5：Skill / 演化
- Layer 6-7：观测 + 治理

### 15.3 与 Workflow 的整合

#### 15.3.1 Dawning Agent 作为 Temporal Activity

```csharp
[Workflow]
public class OrderProcessingWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(Order order)
    {
        await Workflow.ExecuteActivityAsync(
            (Activities a) => a.AnalyzeOrderAsync(order),
            new() { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        // AnalyzeOrderAsync 内部调用 Dawning Agent
    }
}

public class Activities
{
    public async Task<Analysis> AnalyzeOrderAsync(Order order)
    {
        var agent = _provider.GetAgent("OrderAnalyzer");
        return await agent.RunAsync(order);
    }
}
```

#### 15.3.2 Dawning 内置 Workflow 抽象（规划）

```csharp
public interface IAgentWorkflow
{
    Task<TResult> ExecuteAsync<TResult>(
        WorkflowContext ctx,
        CancellationToken ct);
}

public interface IAgentWorkflowEngine
{
    Task<TResult> StartAsync<TWorkflow, TResult>(
        TWorkflow workflow,
        CancellationToken ct);
}

// Adapters:
//   Dawning.Workflow.Temporal
//   Dawning.Workflow.Dapr
//   Dawning.Workflow.InMemory (开发)
```

不重造引擎，**适配主流**。

#### 15.3.3 Layer 7 协同

Workflow 的关键点（HITL / 高风险 step）走 Dawning IPolicyEngine 评估。

---

## 16. 失败案例教训

### 16.1 "Agent 替代 Workflow"

某团队把订单处理写成 Agent：
- LLM 决定下一步
- Agent 偶尔忘了发货
- 客户投诉
- 切回 Workflow，Agent 只做"异常分析"

**教训**：**确定流程不要 Agent**。

### 16.2 "Workflow 硬塞 LLM"

某 Airflow DAG 直接在 Python operator 里 `openai.create()`：
- 失败重试机制弱
- 长 prompt 反复重算
- 成本失控

**教训**：LLM 调用包成 Activity / Operator，并加预算。

### 16.3 "Workflow 不持久化"

用普通 Cron + Python 跑 Agent：
- 机器重启 → 全部丢失
- 长任务不可靠

**教训**：长任务必须 Durable。

---

## 17. 趋势

### 17.1 2026-2027 可期

- **Agent + Workflow 标准融合**：主流框架内置
- **Temporal Agent SDK** 类官方产品
- **Dapr Agents** 已发布（2025），更深整合
- **GenAI 友好的 Workflow UI**（看 trace 像看对话）
- **Cost-aware Workflow scheduling**：根据预算选模型 / step

### 17.2 Dawning 路线

- Layer 4 加入 IAgentWorkflowEngine 抽象
- 提供 Temporal / Dapr / Restate 三个适配
- Skill 可定义为 Workflow（多步可靠 Skill）

---

## 18. 小结

> Workflow 与 Agent **不是对手，是搭档**：
> - Workflow 给可靠性、长时、确定性
> - Agent 给智能、自适应、模糊任务
>
> 选错的代价：把 Workflow 做成 Agent → 不可靠；把 Agent 做成 Workflow → 失去价值。
>
> Dawning 的策略：**不重造 Workflow 引擎，适配主流；让 Agent 可以是 Activity，让 Activity 可以驱动 Agent**——
> 让 Layer 1 的 Agent 在企业级编排里跑得稳。

---

## 19. 延伸阅读

- [[concepts/state-persistence.zh-CN]] — Durable Execution 详解
- [[concepts/multi-agent-patterns.zh-CN]] — 多 Agent 编排
- [[concepts/deployment-architectures.zh-CN]] — 部署架构
- [[concepts/enterprise-roadmap.zh-CN]] — 企业落地阶段
- Temporal: <https://docs.temporal.io/>
- Restate: <https://restate.dev/>
- Dapr Workflow: <https://docs.dapr.io/developing-applications/building-blocks/workflow/>
- Airflow: <https://airflow.apache.org/>
- Prefect: <https://www.prefect.io/>
- Dagster: <https://dagster.io/>
- Inngest: <https://www.inngest.com/>
- LangGraph Persistence: <https://langchain-ai.github.io/langgraph/concepts/persistence/>
- Camunda 8: <https://camunda.com/platform-8/>
