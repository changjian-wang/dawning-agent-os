---
title: "Agent 部署架构：Serverless、Container、Edge 与 Hybrid"
type: concept
tags: [deployment, serverless, container, kubernetes, edge, stateful]
sources: [concepts/observability-deep.zh-CN.md, concepts/agent-loop.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agent 部署架构：Serverless、Container、Edge 与 Hybrid

> Agent 不是纯无状态服务：有长对话、长任务、流式响应、工具执行、HITL 等待。
> 传统 Web 服务部署模式不够用，需要重新思考 **stateful long-running** 工作负载的部署。
>
> 本文梳理四类部署模式、核心挑战（状态/流式/HITL/超时）、与 Dawning 的分布式运行时（Host/Worker/Sidecar）映射。

---

## 1. Agent 工作负载特征

| 特征 | 对部署的影响 |
|------|-------------|
| **长运行** | 单次任务可能 30s-30min |
| **有状态** | 对话历史、Agent 内存 |
| **流式** | SSE / WebSocket 长连接 |
| **HITL** | 等待用户审批可能数小时 |
| **不均匀** | CPU 空闲但等 LLM |
| **突发** | 用户可能同时发起 100 个任务 |
| **需后台** | Batch 任务、定时 Agent |
| **多协议** | HTTP / gRPC / A2A / MCP |

---

## 2. 四种部署模式

```
┌─────────────────┬──────────────────┬─────────────────┬─────────────────┐
│   Serverless    │    Container     │      Edge       │     Hybrid      │
├─────────────────┼──────────────────┼─────────────────┼─────────────────┤
│ Lambda/Cloud    │  K8s/ECS/Docker  │  Cloudflare     │   混合模式        │
│ Functions       │                  │  Workers / CDN  │                 │
├─────────────────┼──────────────────┼─────────────────┼─────────────────┤
│ + 零运维         │ + 完全控制         │ + 低延迟          │ + 灵活            │
│ + 按需计费       │ + 长运行           │ + 合规           │                 │
│ - 15min 超时     │ - 运维成本         │ - 资源有限         │                 │
│ - Cold Start    │ - 利用率低          │ - 状态难         │                 │
│ - Stateful 难    │ + 状态友好         │                 │                 │
└─────────────────┴──────────────────┴─────────────────┴─────────────────┘
```

---

## 3. Pattern 1：Serverless（AWS Lambda / Cloud Functions）

### 3.1 适合

- 短任务 Agent（< 15 min）
- 无状态或状态外置
- 突发流量
- 事件驱动（Webhook → Agent）

### 3.2 挑战

**挑战 1：执行时间上限**
- AWS Lambda: 15 min
- Cloud Functions Gen2: 60 min
- Azure Functions Premium: 无限（但需专属计划）

**解决**：
- 分解为多个 Lambda（Step Functions 编排）
- 超时前保存状态到外部（DynamoDB / Redis）
- 续跑下一个 Lambda

**挑战 2：Cold Start**
- 首次调用 500ms-5s 启动
- Agent 还要加载 LLM client / 向量引擎

**解决**：
- Provisioned Concurrency（预留实例，贵）
- SnapStart（Java/Python 预初始化）
- 最小化依赖（.NET AOT 编译）

**挑战 3：流式响应**
- Lambda 有限支持 streaming（AWS 2023 起）
- 需用 Lambda Function URL + streaming invoke

**挑战 4：HITL 等待**
- 不能在 Lambda 里等待用户审批
- 必须用 Step Functions Wait + Callback

### 3.3 架构模式

```
User Request
    │
    ▼
API Gateway ──► Lambda (plan) ──► Step Functions
                                     │
                  ┌──────────────────┼──────────────────┐
                  ▼                  ▼                  ▼
              Lambda             Lambda             Lambda
              (llm_call)         (tool_exec)        (eval)
                  │                  │                  │
                  └──► DynamoDB (state) ◄───┘
                                     │
                                     ▼
                               SQS / EventBridge
                                     │
                                     ▼
                              Callback → User
```

### 3.4 .NET / Dawning on Lambda

```csharp
public class Function
{
    private readonly IAgent _agent;

    public Function()
    {
        var services = new ServiceCollection();
        services.AddAgentOSKernel()
            .AddOpenAI(...)
            .AddDynamoDBStateStore();
        _agent = services.BuildServiceProvider().GetRequiredService<IAgent>();
    }

    public async Task<Response> Handler(Request req, ILambdaContext ctx)
    {
        var state = await LoadState(req.SessionId);
        var result = await _agent.StepAsync(state, req.Input);
        await SaveState(req.SessionId, result.NewState);
        return result.Response;
    }
}
```

---

## 4. Pattern 2：Container（K8s / ECS）

### 4.1 适合

- 长运行 Agent（持续对话 / 后台任务）
- 多 Agent 协作（需 Service Mesh）
- 需要本地 LLM（GPU 资源）
- 严格合规（数据留在特定网络）

### 4.2 关键模式

**Pod 架构**：

```
Agent Pod
├── Container: Agent App (.NET / Python)
├── Sidecar: OTel Collector
├── Sidecar: Policy Agent (OPA)
└── Init: Skill Loader
```

**Deployment / StatefulSet**：
- 无状态 Agent → Deployment
- 有"会话粘性"的 Agent → StatefulSet（可选）
- 通常推荐**状态外置**（Redis/DB），用 Deployment

### 4.3 资源规划

| 资源 | 推荐 |
|------|------|
| CPU | 0.5-2 core（Agent 主要等 LLM） |
| Memory | 512MB-2GB |
| 启动时间 | < 10s |
| 副本数 | HPA 基于 QPS / CPU |

**GPU 节点**（本地 LLM）：
- 专属 node pool
- Taint/Toleration
- Node Selector

### 4.4 Service Mesh

Istio / Linkerd 提供：
- mTLS Agent 间通信
- 金丝雀路由（Skill Evolution Canary）
- 细粒度流控

### 4.5 Autoscaling

```yaml
# HPA based on custom metric
metrics:
  - type: Pods
    pods:
      metric:
        name: active_agent_sessions
      target:
        type: AverageValue
        averageValue: "20"

# KEDA for event-driven
triggers:
  - type: rabbitmq
    metadata:
      queueName: agent-tasks
      queueLength: "5"
```

---

## 5. Pattern 3：Edge（Cloudflare Workers / Fastly）

### 5.1 适合

- 超低延迟（< 50ms global）
- 轻量任务（简单 RAG / 分类）
- 成本敏感
- 数据合规（区域落地）

### 5.2 限制

- CPU 时间严格限制（50-100ms）
- 内存受限（128MB）
- 无持久存储（用 Durable Objects / KV）
- 语言限制（JS/TS/Rust WASM）

### 5.3 Cloudflare Workers AI

```typescript
export default {
  async fetch(request, env) {
    const response = await env.AI.run(
      '@cf/meta/llama-3.3-70b',
      { messages: [...] }
    );
    return Response.json(response);
  }
}
```

### 5.4 适用场景

| 场景 | 理由 |
|------|------|
| Prompt Guard / Injection 检测 | 入口就挡 |
| 意图分类 / 路由 | 小模型 + 边缘 |
| 简单 FAQ | 命中边缘缓存 |
| Agent 网关 | 协议转换、限流 |

### 5.5 与后端 Agent 配合

```
User ──► Edge Worker ──判断──► 
                            ├─► 简单 → 边缘响应
                            └─► 复杂 → 后端 Agent
```

---

## 6. Pattern 4：Hybrid

### 6.1 动机

现实场景往往是**组合**：

```
┌─────────────────────────────────────────────┐
│  Edge Worker (入口)                          │
│  - 认证 / 限流 / 缓存 / Prompt Guard         │
└────────┬────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────┐
│  Container (核心 Agent)                      │
│  - 长运行会话                                 │
│  - 工具执行                                   │
│  - 本地 LLM (GPU Pod)                        │
└────────┬────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────┐
│  Serverless (Offload)                        │
│  - Batch Eval                                │
│  - Skill 离线编译                             │
│  - 报告生成                                   │
└─────────────────────────────────────────────┘
```

### 6.2 关键挑战

- **协议一致**：A2A / MCP 贯穿各层
- **状态同步**：统一状态存储（通常 Redis + DB）
- **观测统一**：OTel Collector 汇总
- **部署工具**：Terraform + GitOps + Helm

---

## 7. 核心挑战深解

### 7.1 长运行会话（Stateful Sessions）

**问题**：用户连续对话，第 2 轮请求可能路由到不同实例。

**方案**：

| 方案 | 说明 |
|------|------|
| **Sticky Session** | LB 把同一用户固定到同一实例。脆弱。 |
| **External State**（推荐） | 状态存 Redis/DB，任何实例都能继续 |
| **Actor Model** | 每会话是一个 Actor（MAF / Orleans / Akka） |

Dawning 选择：External State + 可选 Orleans Actor。

### 7.2 流式响应

**问题**：SSE / WebSocket 长连接跨代理、LB、ingress 容易断。

**方案**：
- 代理要支持 HTTP/2 / WS
- LB `timeout` 调大（600s+）
- Client 端支持重连（传 `last_event_id`）

### 7.3 HITL 等待

**问题**：Agent 调用 `SendEmailTool` 需要 manager 审批，可能数小时。

**错误方案**：在内存里等（实例重启即丢）。

**正确方案**：

```
Agent 遇到 HITL:
  │
  ▼
保存完整状态到 DB（session.status = "waiting_approval"）
  │
  ▼
向 Approval Service 提交 Request
  │
  ▼
Agent 进程结束，资源释放
  │
  │   ...几小时后...
  │
  ▼
Manager 点击审批
  │
  ▼
Approval Service 发 Webhook
  │
  ▼
任意 Agent 实例接单 → 从 DB 加载 state → 继续执行
```

### 7.4 超时与重试

- LLM 调用超时：30s-120s
- Tool 调用超时：10s-60s
- 整体任务超时：需按业务设置
- 所有重试必须**幂等**（Tool 设计关键）

---

## 8. Dawning 的三面体运行时

### 8.1 Host / Worker / Sidecar

```
┌─────────────────────────────────────────────┐
│  Dawning.AgentOS.Host                        │
│  - HTTP/gRPC API                             │
│  - 会话管理                                   │
│  - 对外协议（A2A / MCP）                      │
│  - 实时响应                                   │
└────────────┬────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────┐
│  Dawning.AgentOS.Worker                      │
│  - 后台任务（Skill 编译、Eval、Batch）         │
│  - 定时 Agent                                │
│  - 任务队列消费                               │
└─────────────────────────────────────────────┘
       ▲
       │ Message Bus / Queue
       │
┌──────┴──────────────────────────────────────┐
│  Dawning.AgentOS.Sidecar                     │
│  - 策略执行（OPA）                            │
│  - 遥测聚合                                   │
│  - Secret 注入                                │
│  - 可与主 App 同 Pod 或独立 Daemon            │
└─────────────────────────────────────────────┘
```

**设计动机**：三种工作负载需求完全不同——实时、批处理、横切关注。

### 8.2 部署模式映射

| 模式 | Host | Worker | Sidecar |
|------|------|--------|---------|
| **Serverless** | Lambda | SQS + Lambda | Lambda Extension |
| **Container** | Deployment | Deployment/KEDA | Sidecar Container |
| **Edge** | Worker | — | Durable Object |
| **Hybrid** | Container | Serverless | Sidecar |

---

## 9. CI/CD

### 9.1 GitOps 推荐

```
Git Repo
  ├── src/                ← 代码
  ├── deploy/
  │   ├── helm/           ← Helm chart
  │   ├── terraform/      ← 基础设施
  │   └── skills/         ← Skill 版本清单
  └── .github/workflows/  ← CI

ArgoCD / Flux 监听 Git → 自动部署
```

### 9.2 Pre-deploy Check

- 单元测试 pass
- Eval 回归 pass（离线数据集）
- 安全扫描（Garak）
- 镜像签名
- Skill 签名验证

### 9.3 渐进式发布

```yaml
# Flagger / Argo Rollouts
strategy:
  canary:
    steps:
      - setWeight: 10
      - pause: 10m
      - analysis:            # eval 验证
          templates: [llm-quality]
      - setWeight: 50
      - pause: 30m
      - setWeight: 100
```

---

## 10. 多 Region / 多 Cloud

### 10.1 动机

- 合规（GDPR / 中国数据不出境）
- 低延迟（用户就近）
- HA（单 region 故障）

### 10.2 架构

```
Region A (EU)         Region B (US)         Region C (CN)
  ├── Host              ├── Host              ├── Host
  ├── Worker            ├── Worker            ├── Worker
  ├── LLM (Azure OpenAI ├── LLM (OpenAI)      ├── LLM (DeepSeek/Qwen)
  │     EU)             │                     │
  └── State (local)     └── State (local)     └── State (local)
       │                     │                     │
       └───── Global State Sync（可选） ───────────┘
```

### 10.3 LLM 供应商多活

```csharp
services.AddLLMProvider(llm =>
{
    llm.AddPrimary("azure-openai-eu");
    llm.AddFallback("openai");
    llm.AddFallback("anthropic");
    llm.Strategy = FallbackStrategy.OnError;
});
```

---

## 11. 成本与 SLA 权衡

| 需求 | 推荐 |
|------|------|
| 最低成本 | Serverless + 突发计费 |
| 最低延迟 | Edge + 就近 region |
| 最大灵活 | K8s + 自建控制面 |
| 最高合规 | 私有 K8s + 本地 LLM |
| 最快上线 | 托管服务（Azure AI Agent Service / Vertex AI Agent Builder） |

---

## 12. 部署清单

- [ ] 状态外置到 Redis / DB
- [ ] 幂等 Tool 设计（支持重试）
- [ ] 流式支持 HTTP/2 / WS
- [ ] HITL 通过 Webhook + DB 恢复
- [ ] OTel Collector 部署
- [ ] PolicyEngine sidecar 或集中
- [ ] Secret 通过 Vault / CSI 注入
- [ ] Health check endpoint
- [ ] Graceful shutdown（drain 在途会话）
- [ ] 水平扩展压测
- [ ] 跨 Region Failover 演练
- [ ] 成本告警 + 预算硬上限

---

## 13. 小结

> **Agent 部署的核心矛盾是"长运行 + 无状态设计"。**
>
> 状态外置、流式跨代理、HITL 续跑、GPU 节点管理、跨 Region 合规——
> 这些问题传统 Web 框架不处理，Agent OS 必须原生支持。
>
> Dawning 的 Host / Worker / Sidecar 三面体模型，让同一代码能适配 Serverless / Container / Edge / Hybrid 四种模式。

---

## 14. 延伸阅读

- [[concepts/observability-deep.zh-CN]] — 部署可观测性
- [[concepts/multi-agent-patterns.zh-CN]] — 多 Agent 跨进程部署
- [[concepts/protocols-a2a-mcp.zh-CN]] — 跨 Region Agent 协议
- AWS Lambda Streaming：<https://docs.aws.amazon.com/lambda/latest/dg/configuration-response-streaming.html>
- Kubernetes KEDA：<https://keda.sh/>
- Cloudflare Workers AI：<https://developers.cloudflare.com/workers-ai/>
- Argo Rollouts：<https://argo-rollouts.readthedocs.io/>
