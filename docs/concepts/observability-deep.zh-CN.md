---
title: "Agent 可观测性深度：OpenTelemetry、OpenInference 与 GenAI Semantic Conventions"
type: concept
tags: [observability, opentelemetry, openinference, tracing, genai-semconv]
sources: [concepts/agent-loop.md, concepts/agent-evaluation.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agent 可观测性深度：OpenTelemetry、OpenInference 与 GenAI Semantic Conventions

> 传统 APM 对 Agent 几乎失效：Agent 的"一次调用"可能是几十次 LLM 请求 + 工具调用 + 记忆查询。
> 需要专为 LLM / Agent 设计的 Trace 语义：Span 类型、属性约定、事件结构。
>
> 本文梳理 OpenTelemetry GenAI SemConv、OpenInference（Arize）、LangSmith/Langfuse 的 Trace 模型，
> 以及 Dawning 的可观测性设计。

---

## 1. Agent 可观测性的独特挑战

| 挑战 | 传统 APM 失效原因 |
|------|-----------------|
| 嵌套深 | 一次请求 → 10+ LLM 调用 → 数十工具调用 |
| 数据大 | 单 Span payload 可达 MB 级（完整 Prompt + Context） |
| 非确定性 | 同样输入可能不同路径 |
| 语义化 | 需要理解 "这是一次 tool call"，不是"一次 HTTP 请求" |
| 多模态 | 图片 / 音频 / 视频 需要特殊处理 |
| 流式 | 响应是流式增量，如何归属到 Span？ |
| 成本 | 每个 Span 都要关联 token / $ |

---

## 2. 三个标准

### 2.1 OpenTelemetry GenAI Semantic Conventions

CNCF 官方标准（2024 年启动，2026 稳定化）。

**核心 Span 类型**：

| Span 名称 | 用途 |
|----------|------|
| `gen_ai.chat` | 一次 LLM 调用 |
| `gen_ai.embeddings` | 向量化调用 |
| `gen_ai.execute_tool` | 工具执行 |
| `gen_ai.invoke_agent` | Agent 调用（含循环） |
| `gen_ai.create_agent` | Agent 创建 |

**核心 Attribute**：

```
gen_ai.system = "openai"
gen_ai.request.model = "gpt-4o"
gen_ai.response.model = "gpt-4o-2024-11-20"
gen_ai.request.max_tokens = 2000
gen_ai.request.temperature = 0.7
gen_ai.usage.input_tokens = 1523
gen_ai.usage.output_tokens = 411
gen_ai.response.finish_reasons = ["stop"]
gen_ai.conversation.id = "conv-abc"
gen_ai.agent.name = "support-agent"
gen_ai.operation.name = "chat"
```

**事件（Events）**：

```json
{
  "name": "gen_ai.user.message",
  "attributes": {
    "gen_ai.system": "openai",
    "content": "帮我查订单状态"
  }
}
```

### 2.2 OpenInference（Arize Phoenix）

Arize 开源的 LLM 追踪规范，比 OTel GenAI 更早，生态更成熟。

**核心概念 `SpanKind`**：

| Kind | 用途 |
|------|------|
| `LLM` | LLM 调用 |
| `CHAIN` | 编排链路 |
| `AGENT` | Agent 循环 |
| `TOOL` | 工具执行 |
| `RETRIEVER` | RAG 检索 |
| `EMBEDDING` | 向量化 |
| `RERANKER` | 重排序 |
| `GUARDRAIL` | 安全过滤 |
| `EVALUATOR` | 评估器 |

**属性命名**：

```
llm.model_name = "gpt-4o"
llm.invocation_parameters = {...}
llm.input_messages = [...]
llm.output_messages = [...]
llm.token_count.prompt = 1523
llm.token_count.completion = 411

tool.name = "search_web"
tool.description = "..."
tool.parameters = {...}

retrieval.documents = [
  {"document.id": "...", "document.score": 0.87, "document.content": "..."}
]
```

### 2.3 Langfuse / LangSmith 自有模型

各自有完整 Trace 模型，但都**正在向 OTel GenAI 靠拢**。

| 平台 | 原生 | OTel 兼容性 |
|------|------|------------|
| LangSmith | 自有 | 2025 年开始支持 |
| Langfuse | 自有 | ✅ 原生 OTel 端点 |
| Arize Phoenix | OpenInference | ✅ OTel-native |
| Braintrust | 自有 | 部分支持 |
| Helicone | 代理式 | 不适用 |

---

## 3. Trace 结构设计

### 3.1 Agent 一次完整调用的 Trace

```
agent.handle_user_message (AGENT span, duration=8.2s)
├── agent.retrieve_memory (CHAIN span, 200ms)
│   └── vector.search (RETRIEVER span, 180ms)
├── agent.loop_iteration[0] (CHAIN span, 3.5s)
│   ├── llm.chat (LLM span, 2.8s, tokens=1523/411)
│   └── tool.search_web (TOOL span, 650ms)
├── agent.loop_iteration[1] (CHAIN span, 4.2s)
│   ├── llm.chat (LLM span, 3.9s, tokens=2015/203)
│   └── tool.read_document (TOOL span, 280ms)
├── agent.loop_iteration[2] (CHAIN span, 450ms)
│   └── llm.chat (LLM span, 420ms, tokens=2234/89, finish=stop)
├── memory.persist (CHAIN span, 50ms)
└── output.filter (GUARDRAIL span, 30ms)
```

### 3.2 关键设计决策

**Q1：Prompt / 消息放 Attribute 还是 Event？**

- OTel GenAI：Event（避免 Attribute 大小限制）
- OpenInference：Attribute（便于查询）
- **建议**：两者兼顾——小的放 Attribute，大的放 Event，超大的存 object storage 只存引用

**Q2：流式响应如何追踪？**

```
options:
A. 生成完才 End Span（简单，但没 TTFT）
B. TTFT 单独记 Event（推荐）
C. 每个 chunk 一个 Event（过度精细）
```

**Q3：Tool 调用失败如何记录？**

```csharp
using var activity = source.StartActivity("gen_ai.execute_tool");
activity?.SetTag("gen_ai.tool.name", toolName);
try {
    ...
} catch (Exception ex) {
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.RecordException(ex);
    throw;
}
```

---

## 4. 与 Metrics / Logs 的联动

### 4.1 GenAI Metrics（OTel 标准）

| Metric | 类型 | 用途 |
|--------|------|------|
| `gen_ai.client.token.usage` | Histogram | Token 使用分布 |
| `gen_ai.client.operation.duration` | Histogram | 延迟分布 |
| `gen_ai.server.request.duration` | Histogram | 服务端延迟 |
| `gen_ai.server.time_per_output_token` | Histogram | TPOT |
| `gen_ai.server.time_to_first_token` | Histogram | TTFT |

### 4.2 结构化日志

```json
{
  "timestamp": "2026-04-17T10:23:45Z",
  "severity": "INFO",
  "body": "Tool call completed",
  "attributes": {
    "trace_id": "...",
    "span_id": "...",
    "gen_ai.tool.name": "search_web",
    "duration_ms": 650,
    "cost_usd": 0.002
  }
}
```

关键：**trace_id / span_id 联动**，可从日志跳到 Trace。

### 4.3 成本维度

```
每个 LLM Span 必须有:
- gen_ai.usage.input_tokens
- gen_ai.usage.output_tokens
- gen_ai.usage.cost_usd（自计算或从 Provider 拿）

每个 Trace 汇总:
- 总 tokens
- 总 cost
- p50/p95 延迟
```

---

## 5. 可视化工具对比

| 工具 | 优势 | 劣势 |
|------|------|------|
| **Langfuse** | 开源，UI 好，Eval 集成 | 规模中 |
| **Arize Phoenix** | 开源，OpenInference 原生，离线分析强 | 企业级偏弱 |
| **LangSmith** | LangChain 原生，Playground 好 | 绑定 LangChain 生态 |
| **Braintrust** | Eval + Playground 强 | 闭源商业 |
| **Datadog LLM Obs** | 企业级 APM | 成本高 |
| **New Relic AI Monitoring** | 企业级 APM | 集成范围有限 |
| **Grafana + Tempo + Loki** | 自建 | 需要大量工作 |

---

## 6. Dawning 的可观测性设计

### 6.1 原则

1. **OTel 优先**：不自造协议，输出 OTel 标准
2. **GenAI SemConv 对齐**：属性命名完全按 OTel 标准
3. **OpenInference 兼容输出**：可同时发往 Arize
4. **零侵入**：业务代码不调用 Activity API，由 Kernel 透明注入
5. **Scope 感知**：每个 Span 携带 UserId/TeamId/OrgId
6. **成本就地**：每个 LLM Span 含 tokens + $

### 6.2 架构

```
Agent 执行
    │
    ▼
[IAgentInvoker]  ──启动 AGENT Span──► OTel SDK
    │
    ▼
[ILLMProvider]   ──启动 LLM Span──► OTel SDK
    │
    ▼
[IToolRegistry]  ──启动 TOOL Span──► OTel SDK
    │
    ▼
[IWorkingMemory] ──启动 RETRIEVER Span──► OTel SDK
    │
    └─────► Exporter ──► OTLP ──► Collector ──► Langfuse/Phoenix/Jaeger
```

### 6.3 关键组件

```csharp
// 内置 ActivitySource
public static class DawningActivitySources
{
    public static readonly ActivitySource Agent =
        new("Dawning.Agent", "0.1.0");
    public static readonly ActivitySource LLM =
        new("Dawning.LLM", "0.1.0");
    public static readonly ActivitySource Tool =
        new("Dawning.Tool", "0.1.0");
    public static readonly ActivitySource Memory =
        new("Dawning.Memory", "0.1.0");
    public static readonly ActivitySource Policy =
        new("Dawning.Policy", "0.1.0");
}

// 注入属性
activity?.SetTag("gen_ai.system", "openai");
activity?.SetTag("gen_ai.request.model", "gpt-4o");
activity?.SetTag("gen_ai.usage.input_tokens", inputTokens);
activity?.SetTag("gen_ai.usage.output_tokens", outputTokens);
activity?.SetTag("dawning.scope.user_id", scope.UserId);
activity?.SetTag("dawning.scope.team_id", scope.TeamId);
```

### 6.4 DI 注册

```csharp
services.AddAgentOSKernel()
    .AddObservability(obs =>
    {
        obs.EnableOpenTelemetry();
        obs.OtlpEndpoint = "http://collector:4317";
        obs.EnableOpenInferenceSemantics();  // 同时输出 OI 属性
        obs.ExcludeAttributes = ["gen_ai.prompt"];  // PII 脱敏
        obs.LargePayloadThreshold = 64_000;
        obs.LargePayloadStore = s3ObjectStore;
    });
```

### 6.5 关键 KPI 仪表盘

```
应用层:
  - RPS
  - 成功率
  - p50/p95/p99 延迟
  - 单用户并发

LLM 层:
  - Token 使用率
  - $/hour
  - 模型分布
  - TTFT p50/p95

Agent 层:
  - 平均循环轮数
  - 失败 Agent 排行
  - Tool 使用频率
  - Tool 失败率

安全层:
  - Prompt Injection 拦截率
  - Policy 拒绝次数
  - PII 脱敏次数
  - 跨 Scope 尝试

演化层:
  - Skill 版本分布
  - Canary 成功率
  - Gate 通过率
```

---

## 7. 数据量与采样

### 7.1 问题

生产环境 Agent 每天可能产生 TB 级 Trace 数据。

### 7.2 策略

| 策略 | 说明 |
|------|------|
| **Head-based sampling** | 在入口决定采样率（简单，但漏掉异常） |
| **Tail-based sampling** | 结束后决定（能保留全部异常，但要缓存） |
| **Error-first** | 100% 保留错误，正常 10% 采样 |
| **Slow-first** | 100% 保留慢请求 |
| **Budget-based** | 高成本请求 100%，低成本采样 |
| **Span-level filtering** | 丢弃冗余 Span（保留 AGENT + LLM，丢 TOOL 子 span） |

**Dawning 默认**：Tail-based + Error-first + Slow-first 混合。

---

## 8. 隐私与合规

### 8.1 敏感数据处理

| 类型 | 处理 |
|------|------|
| Prompt / 输出 | PII 脱敏后保留，或整体丢弃 |
| Embeddings | 不记录到 Trace（体积大且无检索价值） |
| 凭证 | **必须**在进入 Trace 前 redact |
| 用户 ID | 保留，但与 PII 分离存储 |

### 8.2 GDPR 支持

- **被遗忘权**：支持按 UserId 删除历史 Trace
- **数据本地化**：Trace 数据可配置留在用户所在 region
- **审计访问**：Trace 访问本身也要审计

---

## 9. Dawning 与主流工具对接

### 9.1 Langfuse

```csharp
obs.AddLangfuse(lf =>
{
    lf.PublicKey = "...";
    lf.SecretKey = "...";
    lf.Host = "https://cloud.langfuse.com";
});
```

### 9.2 Arize Phoenix（OpenInference）

```csharp
obs.AddArizePhoenix(ph =>
{
    ph.Endpoint = "http://localhost:6006";
});
```

### 9.3 自建 Grafana 栈

```csharp
obs.AddOtlpExporter(ot =>
{
    ot.Endpoint = "http://otel-collector:4317";
});
// Collector 配置: exporters: [tempo, loki, prometheus]
```

---

## 10. 与 Eval 的联动

Trace ↔ Eval 双向联动（详见 [[concepts/agent-evaluation.zh-CN]]）：

```
Trace 生产 ──► Dataset 构造 ──► Offline Eval ──► 回归报告
                                                  │
                                                  ▼
Trace 标注 ◄── 人工审核 ◄── 异常 Trace 采样 ◄────┘
```

关键能力：
- 从 Trace 一键加入 Dataset
- Eval 结果反写到原 Trace
- 版本对比（A/B Trace 并排）

---

## 11. 调试实战

### 11.1 "Agent 为什么选这个工具？"

→ 展开 LLM Span → 查看 `gen_ai.prompt` Event → 看完整 context

### 11.2 "为什么这次回答比平时慢？"

→ 对比 Trace 瀑布图 → 看是哪个 Span 占比异常 → 通常是 retriever 或特定 tool

### 11.3 "跨 Scope 泄漏在哪一步？"

→ 按 `dawning.scope.user_id` 过滤 → 找到不匹配的 Span → 追溯 parent

### 11.4 "LLM 成本突增定位"

→ Metrics 看 `gen_ai.client.token.usage` by model → Trace 过滤最高的几条 → 分析 prompt 组成

---

## 12. 小结

> **可观测性不是"打点"，而是"让 Agent 透明"。**
>
> OTel GenAI SemConv + OpenInference 提供了领域语义，
> Langfuse / Phoenix / LangSmith 提供了 UI 和分析能力，
> 但真正难的是**在代码深处把 Span 结构搭对**——Dawning 的答案是 Kernel 透明注入，业务代码零感知。

---

## 13. 延伸阅读

- [[concepts/agent-evaluation.zh-CN]] — Trace 与 Eval 的联动
- [[concepts/dawning-capability-matrix.zh-CN]] — IObservability 接口
- OTel GenAI SemConv：<https://opentelemetry.io/docs/specs/semconv/gen-ai/>
- OpenInference：<https://github.com/Arize-ai/openinference>
- Langfuse Docs：<https://langfuse.com/docs>
- Arize Phoenix：<https://docs.arize.com/phoenix>
