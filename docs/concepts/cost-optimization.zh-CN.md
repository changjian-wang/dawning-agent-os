---
title: "Agent 成本优化：Prompt 压缩、模型路由、缓存分层与本地降级"
type: concept
tags: [cost, optimization, prompt-compression, model-router, caching, finops]
sources: [concepts/observability-deep.zh-CN.md, concepts/prompt-engineering-dspy.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agent 成本优化：Prompt 压缩、模型路由、缓存分层与本地降级

> LLM Agent 在生产环境最容易失控的就是**成本**。
> 一个没有优化过的 Agent，单次请求可能消耗 $0.05-$0.50，百万请求/天 = 破产。
>
> 本文梳理六类成本优化技术（压缩、路由、缓存、批处理、降级、预算），并给出 Dawning 的 FinOps 架构。

---

## 1. 成本构成分析

### 1.1 成本 = tokens × 单价 × 请求数

```
单次请求成本 =
  (input_tokens × input_price + output_tokens × output_price)
  × 循环轮数
  + embedding 成本（RAG）
  + tool 成本（部分外部工具收费）
```

### 1.2 典型 Agent 请求的 token 分布

| 部分 | 占比 | 优化空间 |
|------|------|---------|
| System Prompt | 5-15% | 高（缓存 + 精简） |
| Few-shot 示例 | 20-40% | 高（动态选择） |
| RAG 检索结果 | 20-40% | 高（重排 + 压缩） |
| 对话历史 | 10-30% | 中（摘要 + 截断） |
| 工具描述 | 5-20% | 中（按需暴露） |
| 用户 query | 1-5% | 低 |
| 输出 | 5-20% | 中（max_tokens） |

### 1.3 成本模型

```python
# gpt-4o 2026 Q1 价格参考
input_price = 0.0000025   # $2.50 / 1M tokens
output_price = 0.00001    # $10 / 1M tokens

# 一次典型 Agent 调用
input_tokens = 3500
output_tokens = 400
loop_iterations = 3

cost = (3500 * 0.0000025 + 400 * 0.00001) * 3
     = (0.00875 + 0.004) * 3
     = $0.038

# 百万请求/天
daily_cost = 1_000_000 * 0.038 = $38,000
```

---

## 2. 六类优化手段

```
┌──────────────────────────────────────────────┐
│  Cost = tokens × price × count               │
└─────┬────────┬───────────┬───────────────────┘
      │        │           │
      ▼        ▼           ▼
  ┌────────┐┌──────────┐┌────────────┐
  │  减少   ││  降单价   ││   减次数    │
  │ tokens ││ 模型路由  ││ 缓存/早停   │
  │ 压缩   ││ 本地降级  ││  批处理    │
  │ 截断   ││          ││  提前返回   │
  └────────┘└──────────┘└────────────┘
      │         │           │
      └────┬────┴─────┬─────┘
           ▼          ▼
     ┌──────────┐┌─────────────┐
     │ 预算控制  ││ 监控告警     │
     │ 硬上限    ││ 异常检测     │
     └──────────┘└─────────────┘
```

---

## 3. 减少 tokens（压缩）

### 3.1 Prompt Compression

**LLMLingua（Microsoft）**：用小模型判断哪些 token 可删除

```python
from llmlingua import PromptCompressor

compressor = PromptCompressor("NousResearch/Llama-2-7b-hf")
compressed = compressor.compress_prompt(
    original_prompt,
    rate=0.5,  # 压缩到原来 50%
    force_tokens=['!', '.', '?', '\n']
)
```

效果：30-50% token 减少，性能损失 < 5%。

**LongLLMLingua**：专为长文档优化，基于问题相关性压缩。

### 3.2 Context Distillation（小模型摘要）

```
长 RAG 结果（5000 tokens）
    │
    ▼ 本地小模型摘要
摘要（500 tokens）
    │
    ▼ 送入主 LLM
```

节省 90%，适用于摘要任务。不适用于需要精确字句的任务。

### 3.3 Semantic Filtering

RAG 检索后**二次过滤**：

```
Top-20 检索结果
    │
    ▼ Cross-encoder 重排
Top-5 高相关
    │
    ▼ LLM 相关性判定
保留 2-3 个
    │
    ▼ 送入主 LLM
```

### 3.4 历史对话摘要

```
每 N 轮对话后，用小模型摘要：

[10 轮原始对话，3000 tokens]
       ↓
[摘要：用户询问了 X，我回答了 Y。关键决定：Z。]
300 tokens + 最近 2 轮原文
```

### 3.5 工具描述瘦身

❌ 同时暴露 20 个工具（2000 tokens）
✅ 先用 embedding 选 Top-5 相关工具（500 tokens）

```csharp
var relevantTools = await toolRouter.SelectAsync(userQuery, k: 5);
var promptWithTools = BuildPrompt(relevantTools);
```

---

## 4. 降单价（模型路由）

### 4.1 分层模型策略

| 层级 | 用途 | 代表模型 |
|------|------|---------|
| **重型** | 复杂推理、代码生成 | GPT-4o / Claude 4 Opus |
| **中型** | 一般对话、简单推理 | GPT-4o mini / Claude 4 Sonnet |
| **轻型** | 分类、路由、摘要 | Haiku / gpt-4o-nano / Gemini Flash |
| **本地** | 高频低价值调用 | Llama 3.3 / Qwen 3 |

### 4.2 Router 设计

```
用户请求
   │
   ▼
[Complexity Classifier]  ← 小模型 or 规则
   │
   ├─► Simple → 轻型模型
   ├─► Medium → 中型模型
   └─► Hard   → 重型模型
```

**判定维度**：
- 请求长度
- 是否涉及代码 / 数学
- 是否需要工具调用
- 领域标签（安全 / 金融需要重型）
- 用户等级（VIP 优先重型）

### 4.3 代表实现

| 方案 | 说明 |
|------|------|
| **RouteLLM** | 开源路由器，学习过的 classifier |
| **Martian** | 商业 LLM 路由 |
| **OpenRouter** | 多供应商路由 + 降级 |
| **Portkey** | 路由 + 缓存 + 监控 |

### 4.4 成本对比

```
全用 GPT-4o：            $0.05 / request
轻/中/重 = 60/30/10：     $0.018 / request  ← 节省 64%
```

### 4.5 Fallback（降级链）

```yaml
models:
  primary: gpt-4o
  fallback_on_rate_limit: claude-sonnet-4
  fallback_on_error: gpt-4o-mini
  fallback_on_timeout: local-llama-3.3
```

---

## 5. 缓存（最大单项节省）

### 5.1 三层缓存

```
┌─────────────────────────────────┐
│  L1: Exact Match Cache          │
│  完全相同 Prompt → 直接返回      │
│  命中率 5-15%，节省 100%         │
└──────────────┬──────────────────┘
               │ miss
               ▼
┌─────────────────────────────────┐
│  L2: Semantic Cache             │
│  相似度 > 0.95 → 返回            │
│  命中率 15-30%，节省 ~100%       │
└──────────────┬──────────────────┘
               │ miss
               ▼
┌─────────────────────────────────┐
│  L3: Prompt Prefix Cache        │
│  LLM 原生缓存（Anthropic/OpenAI）│
│  命中率 50-80%，节省 50-90%      │
└──────────────┬──────────────────┘
               │ miss
               ▼
         真实 LLM 调用
```

### 5.2 L3 Prefix Caching

**Anthropic Cache Control**：

```json
{
  "messages": [...],
  "system": [
    {
      "type": "text",
      "text": "long system prompt",
      "cache_control": {"type": "ephemeral"}
    }
  ]
}
```

命中时 input 成本降 90%（$0.25 → $0.03 per 1M）。

**OpenAI Automatic Prefix Caching**：超过 1024 token 的 prefix 自动缓存，命中降价 50%。

### 5.3 Semantic Cache 实现

```csharp
public interface ISemanticCache
{
    Task<CachedResponse?> LookupAsync(string prompt);
    Task StoreAsync(string prompt, string response, TimeSpan ttl);
}

// 实现：
// 1. embedding(prompt) → 向量
// 2. 向量 DB 搜索 top-1
// 3. 相似度 > threshold → 返回缓存
```

**挑战**：
- 阈值难定（0.95 too strict, 0.85 too loose）
- 缓存污染（错误回答被缓存）
- Scope 隔离（A 用户的答案不能给 B）

### 5.4 缓存键设计

```
key = hash(
  system_prompt,
  user_query,
  scope.user_id | scope.team_id | null,
  model_name,
  version
)
```

**关键**：Scope 必须进入 key，否则跨用户泄漏。

---

## 6. 减次数

### 6.1 提前返回

```
Agent Loop:
  第1轮 LLM 给出高置信答案（logprobs > -0.5）
    → 跳过后续工具调用，直接返回
```

### 6.2 早停判定

```csharp
if (response.Confidence > 0.95 && !response.NeedsTools)
    return response;
```

### 6.3 批处理（Batching）

OpenAI / Anthropic 提供 Batch API：
- 非实时任务（离线摘要、分类、嵌入）
- 24 小时窗口
- 价格降 50%

```csharp
await llmProvider.BatchAsync(new[] {
    new ChatRequest(prompt1),
    new ChatRequest(prompt2),
    ...
});
```

### 6.4 Speculative Decoding（服务端）

部分 Provider 用小模型猜测 + 大模型验证，延迟/成本都降。用户透明。

---

## 7. 本地 LLM 降级

### 7.1 何时降级

| 场景 | 本地可胜任 |
|------|-----------|
| 分类、路由 | ✅ |
| 简单问答 | ✅（Llama 3.3 / Qwen 3） |
| 摘要 | ✅ |
| 翻译 | ✅ |
| 代码补全 | ⚠️（需 Codestral 级） |
| 复杂推理 | ❌ |
| 长文档理解 | ❌（context 有限） |

### 7.2 部署选项

| 方案 | 特点 |
|------|------|
| **Ollama** | 最简单，单机 |
| **vLLM** | 生产级，高吞吐 |
| **TensorRT-LLM** | NVIDIA 最快 |
| **TGI (HuggingFace)** | 开源生产级 |

### 7.3 成本对比

```
云 API (GPT-4o): $0.01 / 1K output tokens
自托管 (Llama 3.3 70B on A100):
  - 硬件折旧 + 电力 ≈ $0.001 / 1K tokens
  - 但需要基础吞吐量才能摊薄
```

**盈亏平衡点**：日请求量 > 100K 时自托管更经济（粗略）。

---

## 8. 预算控制

### 8.1 多层预算

```yaml
budget:
  per_request_max_cost: $0.10
  per_user_daily_max: $5
  per_tenant_monthly_max: $10000
  global_hourly_max: $1000
```

### 8.2 实现模式

```csharp
public interface ICostBudget
{
    Task<bool> CanAffordAsync(ScopeContext scope, decimal estimate);
    Task ChargeAsync(ScopeContext scope, decimal actual);
    Task<BudgetSnapshot> GetAsync(ScopeContext scope);
}

// 使用
if (!await budget.CanAffordAsync(scope, estimated))
    throw new BudgetExceededException();

var response = await llm.ChatAsync(...);

await budget.ChargeAsync(scope, response.Cost);
```

### 8.3 软 / 硬限制

| 阈值 | 动作 |
|------|------|
| 80% | 告警 |
| 90% | 降级到轻型模型 |
| 95% | 警告用户"已接近限额" |
| 100% | 拒绝请求，或进入只读模式 |

---

## 9. 监控与告警

### 9.1 关键指标

```
$/request (p50, p95, p99)
$/user/day
$/tenant/month
$/hour
Token 利用率（实际输出 / max_tokens）
Cache 命中率（L1, L2, L3）
模型分布（heavy vs light %）
```

### 9.2 异常检测

- 单个请求 cost > 5σ：触发审查
- 单用户/tenant 突增：配额收紧
- 模型路由偏移（light → heavy 占比激增）：告警

---

## 10. Dawning 的 FinOps 架构

### 10.1 组件

```
┌─────────────────────────────────────┐
│  Layer 1: LLM Router (Provider)     │
├─────────────────────────────────────┤
│  IModelRouter    ← 分层路由          │
│  ISemanticCache  ← L2 缓存           │
│  IPromptCache    ← L1 + L3 缓存      │
│  ITokenCounter   ← 精确计费          │
│  IPromptCompressor ← Prompt 压缩    │
├─────────────────────────────────────┤
│  Layer 7: Governance                │
├─────────────────────────────────────┤
│  ICostBudget     ← 预算              │
│  ICostReporter   ← 成本报告          │
└─────────────────────────────────────┘
```

### 10.2 配置

```csharp
services.AddAgentOSKernel()
    .AddModelRouter(r =>
    {
        r.AddTier("heavy", "gpt-4o");
        r.AddTier("medium", "claude-sonnet-4");
        r.AddTier("light", "gpt-4o-mini");
        r.AddTier("local", "ollama:llama3.3");
        r.Classifier = ServiceProvider.GetService<IComplexityClassifier>();
    })
    .AddSemanticCache(c =>
    {
        c.Threshold = 0.95;
        c.TTL = TimeSpan.FromMinutes(30);
        c.ScopeAware = true;
    })
    .AddCostBudget(b =>
    {
        b.PerRequestMax = 0.10m;
        b.PerUserDailyMax = 5m;
        b.Strategy = OnExceeded.DegradeToLight;
    });
```

### 10.3 成本 Trace 注入

每个 LLM Span 自动携带：

```
gen_ai.usage.input_tokens
gen_ai.usage.output_tokens
gen_ai.usage.cost_usd
dawning.cache.hit = true | false
dawning.cache.layer = L1 | L2 | L3
dawning.router.tier = heavy | medium | light
dawning.compression.ratio = 0.5
```

---

## 11. 组合优化案例

### 11.1 案例：客服 Agent

**优化前**（$0.08 / request）：
- 全 GPT-4o
- 完整 RAG 返回 3000 tokens
- 无缓存

**优化后**（$0.012 / request，节省 85%）：
- 意图分类：本地 Llama（$0.0001）
- 简单 FAQ：L2 缓存命中（$0，命中率 35%）
- 复杂问题：GPT-4o mini 先答
- 需要升级才用 GPT-4o
- RAG 经过 Cross-encoder 压缩到 800 tokens
- System Prompt 启用 Anthropic prefix caching

### 11.2 案例：代码 Agent

**优化前**（$0.25 / request）：
- Claude Opus 跑所有步骤
- 上下文完整历史

**优化后**（$0.08 / request）：
- 规划阶段：Claude Sonnet（足够）
- 代码生成：Opus（必要）
- 测试修复：Sonnet
- 历史摘要化（每 5 轮压缩）
- 相似问题 L2 缓存

---

## 12. 反模式

| 反模式 | 后果 |
|--------|------|
| 所有请求用最强模型 | 成本翻 10 倍 |
| 缓存不带 Scope | 跨用户泄漏 |
| 缓存 TTL 太长 | 答案过期 |
| max_tokens 不设 | 生成爆炸 |
| 工具描述全塞 | 每次多 2000 tokens |
| 无预算控制 | 一个异常脚本烧光余额 |
| 激进压缩 | 性能大幅下降 |
| Trace 里存完整 Prompt | 存储成本 > LLM 成本 |

---

## 13. 小结

> **成本优化不是单点，而是组合拳。**
>
> 压缩降 tokens，路由降单价，缓存降次数，预算控上限，监控补漏洞——
> 每一项单独 10-30%，组合起来可以做到 70-90% 成本下降，而性能损失 < 5%。
>
> Dawning Layer 1 + Layer 7 原生支持这些能力，业务代码不需要重新写。

---

## 14. 延伸阅读

- [[concepts/prompt-engineering-dspy.zh-CN]] — Prompt 优化（也减 tokens）
- [[concepts/observability-deep.zh-CN]] — 成本监控
- LLMLingua：<https://github.com/microsoft/LLMLingua>
- RouteLLM：<https://github.com/lm-sys/RouteLLM>
- OpenAI Prompt Caching：<https://platform.openai.com/docs/guides/prompt-caching>
- Anthropic Prompt Caching：<https://docs.anthropic.com/en/docs/build-with-claude/prompt-caching>
