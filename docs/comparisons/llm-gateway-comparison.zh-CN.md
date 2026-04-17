---
title: "企业 LLM 网关：API 聚合、限流、计费、审计"
type: concept
tags: [llm-gateway, api-gateway, rate-limit, billing, audit, portkey, litellm, kong-ai]
sources: [concepts/cost-optimization.zh-CN.md, concepts/agent-security.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 企业 LLM 网关：API 聚合、限流、计费、审计

> 企业接入 LLM 不是"给每个团队一个 OpenAI API Key"那么简单。
> 需要统一的**LLM 网关**：聚合多供应商、统一 SDK、限流、计费、审计、合规、降级。
>
> 本文梳理 LLM 网关核心能力、主流方案（LiteLLM、Portkey、Kong AI、Azure APIM AI Gateway），以及 Dawning 与网关的协作模式。

---

## 1. 为什么需要 LLM 网关

### 1.1 没有网关的混乱

```
团队 A ──► OpenAI (各自 Key)
团队 B ──► Anthropic (各自 Key)
团队 C ──► Azure OpenAI (共享 Key，出问题互相甩锅)
团队 D ──► 本地 Ollama (没监控)
```

**问题**：
- 成本无法归属
- 无法统一限流
- 各团队重复造轮子（retry、fallback）
- 审计零散
- 合规难（PII / 数据出境）
- 无法统一切换模型版本

### 1.2 网关带来的秩序

```
团队 A ─┐
团队 B ─┼──► LLM Gateway ──► OpenAI / Anthropic / Azure / Local
团队 C ─┤      │
团队 D ─┘      ├── 限流 / 配额
               ├── 路由 / Fallback
               ├── 缓存
               ├── 审计 / 成本归属
               ├── PII 脱敏
               └── 指标采集
```

---

## 2. 核心能力清单

| 能力 | 说明 |
|------|------|
| **统一 API** | 用 OpenAI 兼容格式调用所有供应商 |
| **多供应商路由** | 按模型名、成本、延迟、可用性路由 |
| **Fallback 链** | 主失败自动降级 |
| **限流** | per-key / per-user / per-model / per-tenant |
| **配额** | 预算、tokens、RPS |
| **缓存** | 完全匹配 / 语义 / Prefix |
| **成本归属** | 按 tenant / team / user / project |
| **审计** | 完整请求响应日志 |
| **PII 脱敏** | 入站/出站过滤 |
| **密钥管理** | 用户拿虚拟 key，网关管真实 key |
| **A/B 测试** | 流量按比例分到不同模型 |
| **可观测性** | Metrics / Logs / Traces |
| **Prompt Guard** | 注入 / 越狱检测 |
| **内容审核** | 违规内容拒绝 |

---

## 3. 主流方案对比

| 方案 | 开源 | 语言 | 部署 | 特色 |
|------|------|------|------|------|
| **LiteLLM** | ✅ | Python | 单机/K8s | 100+ 供应商适配，社区最大 |
| **Portkey** | 部分 | Node | SaaS/Self-host | UI 好，Guardrails 丰富 |
| **Kong AI Gateway** | ✅ | Lua | Kong 插件 | 生态 Kong 原生 |
| **Azure APIM AI Gateway** | ❌ | — | Azure 托管 | Azure 生态原生 |
| **AWS Bedrock Gateway** | ❌ | — | AWS 托管 | Bedrock 集成 |
| **Helicone** | ✅ | TS | SaaS/Self-host | 可观测性优先 |
| **OpenRouter** | ❌ | — | SaaS | 大量小众模型聚合 |
| **Martian** | ❌ | — | SaaS | 动态路由 + 成本优化 |

---

## 4. LiteLLM 详解

### 4.1 定位

**开源事实标准**，2024-2026 被大量企业采用。

### 4.2 核心能力

```python
# 客户端用 OpenAI 格式调任何供应商
from litellm import completion

completion(model="gpt-4o", messages=[...])
completion(model="claude-sonnet-4", messages=[...])
completion(model="vertex_ai/gemini-2.5-pro", messages=[...])
completion(model="ollama/llama3.3", messages=[...])
```

### 4.3 Proxy 模式（生产部署）

```yaml
# config.yaml
model_list:
  - model_name: gpt-4o
    litellm_params:
      model: openai/gpt-4o
      api_key: os.environ/OPENAI_KEY

  - model_name: claude-sonnet
    litellm_params:
      model: anthropic/claude-sonnet-4
      api_key: os.environ/ANTHROPIC_KEY

router_settings:
  routing_strategy: latency-based-routing
  fallbacks:
    - gpt-4o: [claude-sonnet, azure-gpt-4]

general_settings:
  master_key: sk-master-xxx
  database_url: postgres://...

litellm_settings:
  cache: true
  cache_params:
    type: redis
    host: redis
  success_callback: ["langfuse", "prometheus"]
```

### 4.4 Virtual Key

```bash
# 管理员创建虚拟 key 分发给团队 A
curl -X POST http://gateway:4000/key/generate \
  -H "Authorization: Bearer sk-master-xxx" \
  -d '{
    "team_id": "team-a",
    "max_budget": 100,
    "rpm_limit": 100,
    "models": ["gpt-4o", "claude-sonnet"]
  }'
# 返回 sk-team-a-xxx
```

团队 A 用这个 key，只能调允许模型，超预算自动拒绝。

### 4.5 与 Dawning 集成

Dawning 的 `ILLMProvider` 直接指向 LiteLLM Proxy 的 OpenAI 兼容端点：

```csharp
services.AddLLMProvider(llm =>
{
    llm.BaseUrl = "http://litellm:4000";
    llm.ApiKey = "sk-team-a-xxx";  // virtual key
    llm.DefaultModel = "gpt-4o";
});
```

---

## 5. Portkey 详解

### 5.1 定位

商业友好，UI + Guardrails 强。

### 5.2 核心特色

**Config-driven routing**：

```json
{
  "strategy": { "mode": "fallback" },
  "targets": [
    { "virtual_key": "openai-vk", "weight": 0.7 },
    { "virtual_key": "anthropic-vk", "weight": 0.3 }
  ]
}
```

**Guardrails**：
- PII 检测
- Prompt 注入检测
- 内容审核
- JSON Schema 验证
- 可链式组合

**Prompt Library**：
- 版本化 Prompt 模板
- 环境隔离（dev/staging/prod）
- A/B 测试

---

## 6. Kong AI Gateway

### 6.1 定位

**Kong 插件**。已有 Kong 栈的企业首选。

### 6.2 能力

- `ai-proxy`：请求转发 + 格式转换
- `ai-request-transformer`：入站改写
- `ai-response-transformer`：出站改写
- `ai-prompt-template`：Prompt 模板
- `ai-semantic-cache`：语义缓存
- `ai-rate-limiting`：token 级限流
- `ai-prompt-guard`：注入防护

### 6.3 与 LiteLLM 的差异

| 维度 | Kong AI | LiteLLM |
|------|---------|---------|
| 生态 | Kong API Gateway 原生 | 独立 |
| 语言 | Lua（扩展难） | Python（扩展容易） |
| 性能 | 高（OpenResty） | 中 |
| 配置 | Kong DB/declarative | YAML |
| 适合 | 已有 Kong 企业 | 绿地项目 |

---

## 7. Azure APIM AI Gateway

### 7.1 定位

Azure 生态原生，2024 GA。

### 7.2 能力

- **Token-based rate limiting**：按 tokens 限流（非 RPS）
- **Semantic caching**：内置
- **Load balancing**：多 Azure OpenAI 实例分流
- **Emit token metrics**：自动推到 Azure Monitor
- **Managed Identity 认证**：无需管 Key

### 7.3 配置示例（APIM Policy）

```xml
<policies>
  <inbound>
    <azure-openai-token-limit
        tokens-per-minute="50000"
        counter-key="@(context.Subscription.Id)" />
    <azure-openai-semantic-cache-lookup
        score-threshold="0.05" />
  </inbound>
  <outbound>
    <azure-openai-semantic-cache-store
        duration="60" />
    <emit-metric name="TotalTokens" />
  </outbound>
</policies>
```

---

## 8. 自建 vs 托管权衡

| 维度 | 自建（LiteLLM/Kong） | 托管（Portkey/APIM） |
|------|---------------------|--------------------|
| 初始成本 | 高（部署运维） | 低 |
| 长期成本 | 低（只有基础设施） | 高（按调用计费） |
| 数据合规 | 完全可控 | 看供应商承诺 |
| 扩展性 | 需要自己做 | 供应商搞定 |
| 生态集成 | 手工 | 开箱即用 |
| 可定制性 | 高 | 中 |

**经验法则**：
- 创业公司 / 中小企业：先 Portkey / LiteLLM（托管或 SaaS）
- 大型企业 / 金融医疗 / 政府：自建 LiteLLM + Kong
- Azure 深度用户：APIM AI Gateway

---

## 9. 网关功能与 Dawning 职责划分

### 9.1 哪些放网关、哪些放框架？

| 能力 | 网关 | 框架（Dawning） | 说明 |
|------|------|---------------|------|
| 多供应商适配 | ✅ | ✅ | 框架也做，但网关统一所有客户端 |
| 限流 | ✅ | ⚠️ | 网关做集中式，框架做 per-agent |
| 缓存 L1/L2 | ✅ | ✅ | 最好网关做（集群共享） |
| Prefix 缓存 | ✅ | — | 供应商侧 |
| Fallback | ✅ | ✅ | 两处都可 |
| PII 脱敏 | ⚠️ | ✅ | 框架更懂语义 |
| Prompt 构造 | ❌ | ✅ | 框架职责 |
| 记忆 / RAG | ❌ | ✅ | 框架职责 |
| 工具调用编排 | ❌ | ✅ | 框架职责 |
| Skill 演化 | ❌ | ✅ | 框架职责 |
| 业务审计 | ⚠️ | ✅ | 网关看 API，框架看业务 |
| 成本归属 | ✅ | ✅ | 双维度 |

**原则**：
- **横切基础设施放网关**（认证、限流、缓存、监控）
- **业务语义放框架**（Prompt、记忆、工具、演化）

### 9.2 协作模式

```
┌──────────────────────┐
│  Dawning Agent App    │
│  - 业务 Agent 逻辑     │
│  - Layer 2 记忆        │
│  - Layer 4 Skill       │
│  - Layer 7 业务策略    │
└──────────┬───────────┘
           │ OpenAI-compatible API
           ▼
┌──────────────────────┐
│  LLM Gateway          │
│  - 多供应商路由        │
│  - 限流 / 缓存        │
│  - 基础设施审计        │
└──────────┬───────────┘
           │
           ▼
  ┌────────┴────────┐
  ▼        ▼        ▼
OpenAI  Anthropic  Azure
```

---

## 10. 限流设计（深）

### 10.1 传统 RPS 限流的不足

```
每分钟 100 次请求 —— 但
 - 请求 1: 100 tokens
 - 请求 100: 100,000 tokens
```

成本 1000 倍差距。纯 RPS 限流无效。

### 10.2 Token-based Rate Limiting

```
"每分钟最多 1M input tokens + 500K output tokens"
```

**实现**：
- 估算（请求前按模型算平均）
- 实测（响应后更新计数器）
- 预留（请求时占用名额，完成后释放）

### 10.3 多维度限流

```yaml
rate_limits:
  - per: user
    max_rpm: 60
    max_tpm: 100000
  - per: team
    max_rpm: 600
    max_tpm: 1000000
    max_cost_per_day: 500
  - per: model
    gpt-4o:
      max_rpm: 300    # 防止单模型打爆
    claude-sonnet:
      max_rpm: 500
```

### 10.4 失败策略

| 触发 | 动作 |
|------|------|
| 软限额（80%） | 告警 |
| 硬限额（100%） | 返回 429 + Retry-After |
| 严重超限 | 临时 ban + 人工介入 |

---

## 11. 计费 / 成本归属

### 11.1 数据模型

```sql
CREATE TABLE llm_usage (
    id              uuid PRIMARY KEY,
    timestamp       timestamptz,
    tenant_id       text,
    team_id         text,
    user_id         text,
    project_id      text,
    model           text,
    input_tokens    int,
    output_tokens   int,
    cached_tokens   int,
    cost_usd        numeric(10,6),
    trace_id        text,
    latency_ms      int
);
```

### 11.2 计费粒度

- **Tenant**：对外账单
- **Team**：内部 chargeback
- **User**：人头分摊
- **Project**：成本中心
- **Trace**：单请求追溯

### 11.3 报表

- 每日成本曲线（按维度切分）
- Top 10 贵的请求
- Top 10 贵的用户 / 团队
- 模型成本占比
- 缓存命中节省金额

---

## 12. 审计日志

### 12.1 日志内容

每次调用必须记录：

```json
{
  "request_id": "...",
  "trace_id": "...",
  "timestamp": "...",
  "tenant_id": "...",
  "user_id": "...",
  "model": "gpt-4o",
  "prompt_hash": "sha256:...",
  "prompt_preview_redacted": "[PII_REDACTED] 帮我查订单...",
  "response_hash": "sha256:...",
  "response_preview_redacted": "...",
  "input_tokens": 1523,
  "output_tokens": 411,
  "cost_usd": 0.008,
  "latency_ms": 2800,
  "cache_hit": false,
  "route": "openai-primary",
  "fallbacks_used": [],
  "pii_detected": ["email"],
  "prompt_injection_score": 0.02,
  "policy_decisions": ["allow"]
}
```

### 12.2 合规要求

- **完整性**：所有请求无遗漏
- **不可篡改**：WORM 存储
- **保留期限**：金融 7 年，医疗 6 年
- **检索能力**：按 user / trace_id / date 查询

### 12.3 PII 处理

- 原文**不入**常规日志
- PII 单独加密存储（独立密钥）
- 访问审计（谁看过原文也要记录）

---

## 13. Dawning 网关交付物

### 13.1 推荐组合

```
客户端 (Dawning Agent)
    │
    │ OpenAI-compatible API + virtual key
    ▼
LiteLLM Proxy (企业统一网关)
    │
    ├── 多供应商路由
    ├── 虚拟 key 管理
    ├── 基础限流 / 配额
    ├── Redis 语义缓存
    └── Langfuse 审计
    │
    ▼
真实 LLM 供应商
```

### 13.2 Dawning 提供

- `Dawning.Agents.Gateway.LiteLLM` — LiteLLM 兼容 Provider（默认）
- `Dawning.Agents.Gateway.Portkey` — Portkey 适配
- `Dawning.Agents.Gateway.Kong` — Kong AI 适配
- `Dawning.Agents.Gateway.AzureAPIM` — APIM 适配
- 统一 `IGatewayClient` 抽象

### 13.3 DI 注册

```csharp
services.AddAgentOSKernel()
    .AddLLMProvider(llm =>
    {
        llm.UseGateway<LiteLLMGateway>(gw =>
        {
            gw.BaseUrl = "http://litellm:4000";
            gw.VirtualKey = Configuration["LLM:VirtualKey"];
            gw.DefaultModel = "gpt-4o";
        });
    });
```

---

## 14. 反模式

| 反模式 | 后果 |
|--------|------|
| 客户端直连 LLM 供应商 | 无审计无限流 |
| 所有团队共享一个真实 Key | 无法归属 |
| 只按 RPS 限流 | 成本失控 |
| Prompt 原文入日志 | PII 泄漏 + 存储爆炸 |
| 网关做业务逻辑 | 难维护难演化 |
| 框架做基础设施 | 重复造轮子 |
| 没有 Fallback | 单点故障 |
| 缓存不带 Scope | 跨用户泄漏 |

---

## 15. 小结

> **LLM 网关是企业 Agent 落地的"前置必要条件"**。
>
> 没有网关 → 成本失控、审计缺失、合规风险、多供应商混乱；
> 有了网关 → 框架只专注业务语义，基础设施统一治理。
>
> Dawning 的策略是**集成**而非**重造**：主动拥抱 LiteLLM/Portkey/Kong 等生态，把框架的精力留给 Layer 2-7 的业务能力。

---

## 16. 延伸阅读

- [[concepts/cost-optimization.zh-CN]] — 网关内缓存与路由
- [[concepts/agent-security.zh-CN]] — 网关 Prompt Guard
- [[concepts/observability-deep.zh-CN]] — 网关 OTel 接入
- LiteLLM：<https://docs.litellm.ai/>
- Portkey：<https://portkey.ai/docs>
- Kong AI Gateway：<https://docs.konghq.com/hub/kong-inc/ai-proxy/>
- Azure APIM AI Gateway：<https://learn.microsoft.com/azure/api-management/genai-gateway-capabilities>
