---
title: ADR-029 LLM Provider 扩展：Azure OpenAI 支持
type: adr
subtype: architecture
canonical: true
summary: 扩展 ADR-028 支持 Azure OpenAI 作为第三个 provider，共用 OpenAI 兼容客户端，通过 appsettings.Llm:ActiveProvider 字段切换；不改变 ILlmProvider 抽象、Result<T> 错误模型、warn-but-start 启动行为或 /api/llm/ping smoke 端点形态。
tags: [agent, engineering, llm]
sources: []
created: 2026-05-03
updated: 2026-05-03
verified_at: 2026-05-03
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/llm-provider-v0-openai-deepseek-abstraction.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-03
adr_revisit_when: "Microsoft 改动 Azure OpenAI API，使其不再兼容 OpenAI ChatCompletion shape；用户反馈 Azure 连接不稳定需要专项重试策略；出现需要 Azure SDK 特定特性的产品需求（如托管身份、私有端点）。"
---

# ADR-029 LLM Provider 扩展：Azure OpenAI 支持

> 扩展 ADR-028 支持 Azure OpenAI 作为第三个 provider，复用现有 OpenAI 兼容客户端架构，通过 `appsettings.Llm:ActiveProvider` 字段在 `"OpenAI"`、`"DeepSeek"`、`"AzureOpenAI"` 间切换。

## 背景

[ADR-028](llm-provider-v0-openai-deepseek-abstraction.md) 已定义 ILlmProvider 端口与 OpenAI/DeepSeek 两个实现。[OpenAiCompatibleClient](../../../src/Dawning.AgentOS.Infrastructure/Llm/Common/OpenAiCompatibleClient.cs) 的设计允许任何 OpenAI ChatCompletion 形态兼容的服务（包括 Azure OpenAI）共用同一套 HTTP/JSON 适配器——只需提供不同的 `BaseUrl` 和 `ApiKey`。

Azure OpenAI 在 endpoint 配置方面的独特性：
- 接受 `api-key` Header（而非 `Authorization: Bearer`）
- 支持 API Key 和令牌认证两种方式
- 暴露特定的 `deployment` 概念，映射到模型 ID

为保持架构简洁且支持 V0 dogfood 对 Azure 的需求，本 ADR 决定：

1. **复用 OpenAiCompatibleClient** — 因为 Azure OpenAI 的 ChatCompletion 端点（`POST {endpoint}/openai/deployments/{deployment-id}/chat/completions`）在 URL shape 之外与 OpenAI API 形态兼容。
2. **添加 AzureOpenAiLlmProvider** — 与 OpenAiLlmProvider / DeepSeekLlmProvider 平级，负责 URL 构造与 auth header 差异。
3. **扩展 LlmOptions** — 新增 `AzureOpenAI` section，字段为 `ApiKey`、`Endpoint`、`DeploymentId`。
4. **不改变 ILlmProvider 或错误模型** — 保持 ADR-028 的契约不变。

## 决策

### A1：端口与实现分层不变

保持 `Application/Abstractions/Llm/ILlmProvider` 及 `Infrastructure/Llm/{Provider}/` 的结构，新增 `Infrastructure/Llm/AzureOpenAi/AzureOpenAiLlmProvider.cs`。

### B1：方法签名不变

`Task<Result<LlmCompletion>> CompleteAsync(LlmRequest request, CancellationToken ct)` 保持不变。

### C1：共用 OpenAiCompatibleClient

Azure OpenAI 的 ChatCompletion 端点虽然 URL 格式不同，但返回体（`{ model, choices[0].message.content, usage }`）与 OpenAI 兼容。`AzureOpenAiLlmProvider` 负责：
- 将部署 ID 注入 URL 路径（`/openai/deployments/{deployment-id}/chat/completions`）
- 追加 `?api-version={api-version}` 查询参数（**Azure 强制要求**，缺失时 endpoint 返回 HTTP 404）
- 使用 `api-key` Header 而非 `Authorization: Bearer`

### E1：单 active provider 模式保持

`appsettings.Llm:ActiveProvider` 可取值 `"OpenAI"`、`"DeepSeek"`、`"AzureOpenAI"`，DI 层按此选择唯一 `ILlmProvider` 实例。

### F1：配置优先级保持

环境变量 > dotnet user-secrets > appsettings.{Environment}.json > appsettings.json。`LLM_PROVIDERS_AZUREOPENAI_APIKEY` / `LLM_PROVIDERS_AZUREOPENAI_ENDPOINT` / `LLM_PROVIDERS_AZUREOPENAI_DEPLOYMENTID` / `LLM_PROVIDERS_AZUREOPENAI_APIVERSION` 覆盖配置文件。`ApiVersion` 留空时回退到 `LlmAzureOpenAiProviderOptions.DefaultApiVersion`（当前 `2024-10-21`，跟随 Azure GA 节奏维护）。

### G1：启动行为保持

- ActiveProvider 必须是三者之一，否则 IValidateOptions 在启动时 fail-fast。
- ApiKey/Endpoint/DeploymentId 中任一缺失时 warn-but-start（同 ADR-028 §G2），调用时返回 `llm.authenticationFailed` / `llm.invalidRequest`。

### H1：错误映射表保持

HTTP 状态码 → `llm.*` 错误码的映射表不变；Azure 返回的 4xx/5xx 遵循同样映射。

### I1：命名 HttpClient 与 BaseAddress

新增命名 HttpClient `"llm-azure-openai"`，BaseAddress 设为 `{Endpoint}/openai/deployments/{DeploymentId}`。

## 实施概要

1. **LlmOptions 扩展** —— `Providers.AzureOpenAI` section（ApiKey、Endpoint、DeploymentId、ApiVersion 四字段；ApiVersion 留空时使用 `LlmAzureOpenAiProviderOptions.DefaultApiVersion`）。

2. **AzureOpenAiLlmProvider** —— `internal sealed` 类实现 `ILlmProvider`，依赖 `IHttpClientFactory` + `IOptionsMonitor<LlmOptions>`，调用 OpenAiCompatibleClient 前改写请求 HTTP Header（`api-key` 替代 Bearer）。

3. **OpenAiCompatibleClient 调整** —— 支持自定义 auth header 构造（而非硬编 Bearer）；或 AzureOpenAiLlmProvider 直接改请求。

4. **DI 注册** —— `InfrastructureServiceCollectionExtensions.AddLlm()` 中新增 `AddHttpClient("llm-azure-openai", ...)` 并在 `ResolveActiveProvider()` 返回 `AzureOpenAiLlmProvider` 实例。

5. **appsettings.json** ——
   ```json
   "Llm": {
     "ActiveProvider": "OpenAI",
     "Providers": {
       "OpenAI": { "ApiKey": "", "BaseUrl": "...", "Model": "..." },
       "DeepSeek": { "ApiKey": "", "BaseUrl": "...", "Model": "..." },
       "AzureOpenAI": { "ApiKey": "", "Endpoint": "", "DeploymentId": "", "ApiVersion": "" }
     }
   }
   ```

6. **测试** —— `AzureOpenAiLlmProviderTests` 覆盖成功、401、429、500、HttpRequestException、cancellation 等场景（沿 ADR-028 的模式）。

## 不在本 ADR 范围内

- 真实业务接入（ActionClassifier 等）仍由后续 ADR 决定。
- Azure 专项特性如托管身份、私有端点、RBAC、Key Vault 集成留给未来。
- 改动 ILlmProvider 抽象、Result<T> 错误模型或 /api/llm/ping 端点。
- 引入 Azure SDK（Azure.AI.OpenAI）；本 ADR 仍用 HttpClient + 手工 JSON。

## 折衷与风险

- **V0 方案** — 不直接用 Microsoft 官方 Azure.AI.OpenAI SDK，而是依赖"Azure OpenAI API 与 OpenAI 兼容"这一假设。若该假设破裂（如微软改动 API 形态），需要升级 SDK 并修订此 ADR。
- **auth header 差异** — Azure `api-key` vs OpenAI `Bearer` 的差异目前由 AzureOpenAiLlmProvider 单独处理；若未来有第四个 provider 引入更多差异，应考虑抽象出"auth 策略"接口。
- **URL 构造** — Azure 的 `/openai/deployments/{id}/chat/completions?api-version={ver}` 格式由 AzureOpenAiLlmProvider 在每次请求时拼装；生产环境务必验证 Endpoint、DeploymentId、ApiVersion 三者组合的正确性，特别是 ApiVersion 必须匹配部署支持的版本范围。

