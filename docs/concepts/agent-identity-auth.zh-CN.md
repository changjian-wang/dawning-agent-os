---
title: "Agent 身份与认证：OAuth for Agents、Scoped Token、MCP Auth、Zero Trust Agent"
type: concept
tags: [security, auth, oauth, identity, zero-trust, mcp-auth, delegation, agent-identity]
sources: [concepts/agent-security.zh-CN.md, concepts/protocols-a2a-mcp.zh-CN.md]
created: 2026-04-18
updated: 2026-04-18
status: active
---

# Agent 身份与认证：OAuth for Agents、Scoped Token、MCP Auth、Zero Trust Agent

> 传统认证的预设：一个 User，一个 Token，访问一个 API。
> Agent 时代的新挑战：
> - **代理身份**：Agent 代表 User 做事，谁的权限？
> - **链式代理**：Agent A 调 Agent B 调 Agent C，权限如何传递？
> - **动态工具**：Agent 运行时决定调哪个 API，事前授权无法枚举
> - **长时任务**：Token 有效期内 Agent 还没做完
> - **不可信 Agent**：第三方 Agent 不能信任
> - **审计难题**：多跳之后谁做了什么
>
> 本文讨论 Agent 身份 / 认证 / 授权的系统化方案。

---

## 1. 问题空间

### 1.1 传统 Web Auth 的假设

```
User → Browser → Server
  (1 个身份，1 个 token，session 短)
```

### 1.2 Agent 场景的错位

```
User ──delegates──> Agent ──calls──> Tool / MCP / API
                       │
                       ├──calls──> Agent B (A2A)
                       │              └──calls──> Tool
                       └──calls──> Agent C
```

- **身份传播**：Tool 看到的是谁？
- **权限收缩**：Agent 不该拥有 User 全权
- **非交互刷新**：Agent 后台运行时 User 不在
- **长生命周期**：任务跨小时 / 天
- **可撤销**：User 想立即终止

---

## 2. Agent 身份类型

### 2.1 三种主要身份模型

| 模型 | 描述 | 场景 |
|------|------|------|
| **User-as-Agent** | Agent 纯代表 User | 个人助手 |
| **Agent-as-Service** | Agent 有自己身份（Service Account） | 后台作业 |
| **Hybrid (Delegation)** | Agent 有身份 + 附带 User 代理权 | 企业场景 |

### 2.2 Hybrid 示例

```json
{
  "sub": "agent-42",         // Agent 本体身份
  "act": {                   // Actor claim (RFC 8693)
    "sub": "user-alice",     // 实际用户
    "amr": ["mfa"]
  },
  "scope": "mail.read calendar.read",
  "aud": "tool-gateway"
}
```

---

## 3. OAuth 2.x 关键机制

### 3.1 Authorization Code + PKCE

Agent UI（桌面 / 手机）登录 User → 换 token。

### 3.2 Client Credentials

Agent 自己作为 client（无 User）→ Agent Service 账号。

### 3.3 Token Exchange (RFC 8693)

**Agent 场景最关键**：
- 输入：上游 token
- 输出：下游 token（更窄 scope / 换 audience）
- 支持 delegation & impersonation

### 3.4 Device Code Flow

Agent 在无浏览器设备（CLI / IoT）引导 User 登录。

### 3.5 Dynamic Client Registration (RFC 7591)

Agent 动态注册为 OAuth client。MCP 规范采用。

### 3.6 Rich Authorization Requests (RAR, RFC 9396)

描述精细授权（而不仅是 scope 字符串）：

```json
{
  "type": "payment_initiation",
  "locations": ["https://bank.example.com/..."],
  "instructedAmount": { "currency": "EUR", "amount": "123.45" },
  "creditorAccount": { "iban": "DE..." }
}
```

Agent 代付 / 代查 / 代订场景关键。

---

## 4. Scope 设计

### 4.1 传统 scope 的不足

```
scope=mail.read  ← 读所有邮件？
```

太粗了，Agent 可能只需读今天的某个客户邮件。

### 4.2 精细化策略

| 维度 | 例子 |
|------|------|
| 资源类型 | `mail`, `calendar`, `file` |
| 操作 | `read`, `write`, `delete` |
| 范围 | `own`, `shared`, `all` |
| 时间 | `today`, `last-30d` |
| 过滤 | 特定 folder / 标签 |
| 字段 | 只读 subject，不读 body |

### 4.3 RAR / 交互式授权

Agent 运行时请求："我需要发这一封邮件，收件人 X，内容 Y" → User 一次性授权。

### 4.4 Just-in-Time 授权

- Agent 执行到需要的一步才请求授权
- 不预先授予大权限
- 失败重试不自动扩大

---

## 5. MCP Authorization（2025 Spec）

### 5.1 背景

早期 MCP（2024）只有 stdio 本地调用，无需 auth。
2025 增加 remote MCP（HTTP / SSE），必须 auth。

### 5.2 核心要求

1. **OAuth 2.1** 强制（ASVS 验证）
2. **Resource Server** classification（RFC 8707）：
   - MCP Server 声明自己是 resource server
   - Token audience 必须匹配
3. **Dynamic Client Registration**（RFC 7591）：
   - MCP Client 运行时注册
4. **Authorization Server Metadata**（RFC 8414）：
   - `/.well-known/oauth-authorization-server`
5. **Protected Resource Metadata**（RFC 9728）：
   - `/.well-known/oauth-protected-resource`

### 5.3 典型流程

```
1. MCP Client 连接 https://mcp.example.com
2. 401 响应 + WWW-Authenticate
3. Client 读 /.well-known/oauth-protected-resource
4. 得 Authorization Server URL
5. DCR 注册 + Auth Code Flow
6. 获得 access_token
7. MCP 请求带 Bearer token
```

### 5.4 Token 绑定

- 推荐 DPoP（RFC 9449）：证明持有密钥
- 防止 token 盗用

### 5.5 安全考量

- 避免 confused deputy（MCP Server 不能代客户越权）
- 细粒度 scope 到工具级
- Audit log 可追 token → user

---

## 6. A2A Authorization（Agent-to-Agent）

### 6.1 Google A2A Spec

- Agent Card 声明支持的 auth schemes
- 推荐 OAuth 2.x + OIDC
- 支持 API Key（简单）和 mTLS（企业）

### 6.2 链式代理传播

**选项 A：Token Exchange**
```
User → Agent A (token_U)
Agent A → /token-exchange → token_{A,for-B}
Agent A → Agent B (token_{A,for-B})
Agent B → /token-exchange → token_{B,for-Tool}
Agent B → Tool (token_{B,for-Tool})
```

每一跳都换 token：audience 收窄、scope 收窄、actor chain 记录。

**选项 B：JWT Chain**
- Agent A 签名"Agent B 可代我做 X"
- Tool 验证整条链

**选项 C：Short-lived Credential**
- STS 服务每跳签发 1 分钟 token

### 6.3 身份可视化

对 User 展示："Agent A 使用你的身份调用了 Agent B，执行了操作 Z"。

---

## 7. Zero Trust + Agent

### 7.1 Zero Trust 原则

- Never trust, always verify
- Least privilege
- Assume breach
- Micro-segmentation

### 7.2 应用到 Agent

| 原则 | 实现 |
|------|------|
| Never trust | 每次请求都验 token + 证书 |
| Always verify | 策略引擎每步判断 |
| Least privilege | JIT scope + 短 token |
| Micro-seg | Agent 间网络隔离 |
| Assume breach | 行为异常检测、自动收权 |

### 7.3 Policy Engine 集成

- OPA / Cedar / Oso
- 决策："此 Agent 可否执行此操作？"
- 输入：token, claims, resource, action, context
- 输出：allow / deny / require-mfa

### 7.4 SPIFFE / SPIRE

Workload identity（类似 Agent 间 mTLS）：
- SVID（SPIFFE Verifiable Identity Document）
- 自动轮换
- 多 runtime 通用

---

## 8. User → Agent 授权的 UX

### 8.1 授权屏幕

Agent 请求权限时展示：
- Agent 身份（名称 / 签名者 / 评级）
- 请求的精确权限
- 持续时间
- 可撤销方式

### 8.2 Consent Receipt

授权后颁发 Consent Receipt（可审计）。

### 8.3 运行时通知

- Agent 每次敏感操作通知 User
- HITL confirm 可选

### 8.4 撤销中心

- 一键撤销 Agent 所有权限
- 查看历史授权
- 查看操作日志

---

## 9. Tool / API 侧防护

### 9.1 Audience 校验

Token 的 `aud` 必须匹配目标 API。防止 token 转用。

### 9.2 Scope 校验

API 验证 scope 是否涵盖本次操作。

### 9.3 Rate Limit + Quota

- 按 `sub`（Agent 本体）限流
- 按 `act.sub`（实际用户）限流
- 按 session 限流

### 9.4 Risk Scoring

- IP 异常？
- 操作频率异常？
- 是否敏感资源？
- 触发 step-up auth

### 9.5 HITL Hook

某些操作（如 `>= $1000` 支付）强制 User 确认。

---

## 10. 秘密管理（Agent 内部）

### 10.1 不能硬编码

- 禁止 prompt / 代码 / 环境文件里 hardcode
- 禁止 repo 泄露

### 10.2 推荐方案

- Vault / KMS / Secrets Manager
- Workload Identity（不分发静态凭据）
- 运行时注入（env / tmpfs）

### 10.3 Token 生命周期

- 短（< 1 小时）
- 自动刷新
- 泄露可立即吊销

### 10.4 Refresh Token 存哪

- 不能在 Agent 进程（memory 泄漏风险）
- 专用 secret store
- 每次换新 access token

---

## 11. Prompt Injection 防御（授权侧）

### 11.1 Dual LLM 模式

- Planner LLM（不接触不可信输入）决定哪些工具
- Executor LLM（可读不可信输入）执行
- 不可信输入不能改变 auth 决策

### 11.2 Token 不放 prompt

- Token / 密钥永不进 LLM 上下文
- 通过 sidecar / gateway 注入

### 11.3 最小化 tool output 回灌

- Tool 结果摘要后回 LLM
- 剔除可能的 injection payload

### 11.4 授权决定不由 LLM 做

- Policy Engine 决策
- LLM 只能"请求"，不能"授权"

---

## 12. 审计与合规

### 12.1 全链路日志

每次操作记录：
- Principal（User）
- Actor（Agent 本体 + 链）
- Resource
- Action
- Decision（policy 结果）
- Timestamp + TraceId

### 12.2 不可篡改

- Append-only log
- Hash chain
- 外部 SIEM

### 12.3 对账

- Agent 操作 vs User 预期
- 异常自动告警

### 12.4 法规

- GDPR：User 可要求导出 / 删除所有 Agent 代理记录
- SOC 2 / ISO 27001：控制点覆盖 Agent
- 金融 / 医疗行业特殊要求

---

## 13. 典型架构：Agent Auth Gateway

```
┌──────────────┐
│ User Browser │
└─────┬────────┘
      │ OIDC
┌─────▼────────┐
│  IDP         │ (Auth0 / Keycloak / Entra)
└─────┬────────┘
      │ id_token
┌─────▼────────┐
│  Agent Host  │
└─────┬────────┘
      │ token_exchange
┌─────▼──────────┐
│ Agent Auth GW  │ ← Policy Engine (OPA)
│ - 下发短 token │ ← Secret Store (Vault)
│ - 审计日志     │ ← Risk Engine
└─────┬──────────┘
      │ scoped token
┌─────▼────────┐  ┌──────────────┐  ┌──────────────┐
│  MCP Server  │  │  A2A Agent B │  │  SaaS API    │
└──────────────┘  └──────────────┘  └──────────────┘
```

---

## 14. 常见漏洞

| 漏洞 | 后果 | 修复 |
|------|------|------|
| Token 硬编码 | 凭据泄漏 | Vault |
| Scope 过宽 | 越权 | 精细化 + JIT |
| Token 不绑 audience | Token 转用 | `aud` 校验 |
| 无 Token Exchange | Agent 持 User 全权 | RFC 8693 |
| 无审计链 | 无法追责 | Actor chain 记录 |
| Long-lived token | 泄漏影响大 | < 1h + refresh |
| Confused Deputy | Agent 被利用越权 | Policy + audit |
| Prompt-injected auth | 攻击者诱 Agent 调用危险 API | HITL + Policy |
| No revocation | 撤销延迟 | Token 短 + 即时 revoke |
| Missing MFA step-up | 敏感操作无增强 | 条件式 MFA |

---

## 15. 行业方案

### 15.1 Anthropic Claude

- MCP 规范定义 auth（OAuth 2.1）
- Claude Desktop 支持 remote MCP with OAuth

### 15.2 OpenAI

- Custom GPT / Assistant 通过 OAuth 接外部 API
- Actions 支持 OAuth flow

### 15.3 Microsoft

- Entra Agent ID（2024 公布）
- Copilot Agent 注册到 Entra
- 使用 Entra 颁发 token

### 15.4 Google

- A2A Protocol 中定义 auth
- Vertex AI Agent Builder 集成 Google Identity

### 15.5 Okta / Auth0

- Okta for AI Agents（2025）
- 专为 Agent 场景的 identity 服务

---

## 16. Dawning 身份与认证策略

### 16.1 Layer 7 Principal & Auth

```csharp
public interface IPrincipalContext
{
    ClaimsPrincipal User { get; }
    AgentIdentity Agent { get; }
    ActorChain Chain { get; }
}

public interface ITokenBroker
{
    Task<string> GetTokenAsync(
        string audience,
        string[] scopes,
        ActorChain chain,
        CancellationToken ct);
}
```

### 16.2 Token Exchange 内建

- 默认每次 tool call / A2A call 换 token
- audience 自动设为目标
- scope 按声明收窄

### 16.3 IToolRegistry 与 Scope 声明

```csharp
[Tool("mail.send")]
[RequiredScopes("mail.send:own")]
public async Task SendMailAsync(...) { }
```

注册时校验 scope；运行时 broker 申请。

### 16.4 Policy Engine 集成

```csharp
public interface IPolicyEngine
{
    Task<PolicyDecision> EvaluateAsync(
        PolicyInput input,
        CancellationToken ct);
}
```

- 默认 Dawning.Policy.Cedar / Dawning.Policy.OPA

### 16.5 Secret Broker

- `Dawning.Secrets.Vault` / `.AzureKeyVault` / `.AwsSecretsManager`
- Agent 只通过接口取，不直接持久化

### 16.6 MCP Auth 适配

- Dawning.MCP.Client 支持 OAuth 2.1 + DCR
- 自动发现 metadata endpoints

### 16.7 A2A Auth 适配

- Dawning.A2A 支持 OIDC / mTLS
- Token Exchange 封装

### 16.8 审计

- `IAgentEventStream` 每步 emit auth 决策
- `Dawning.Audit` 写入 append-only 存储

### 16.9 HITL

- `IHitlGate` 接口
- 敏感操作（policy 标记）强制 confirm
- Confirm 后生成 one-time scope token

---

## 17. 开发者清单

启动新 Agent 项目必须回答：

- [ ] User 如何登录？
- [ ] Agent 本体是否有独立身份？
- [ ] 每个工具需要什么最小 scope？
- [ ] 是否有跨 Agent 调用？如何传身份？
- [ ] Token 存哪？有效期？
- [ ] 如何撤销？
- [ ] 敏感操作如何 step-up？
- [ ] 如何审计？谁能看？
- [ ] 如何防 prompt injection 篡改授权？
- [ ] 合规要求（GDPR/SOX/HIPAA）是什么？

---

## 18. 小结

> Agent 时代的 Auth 不是更复杂，是**更精细**。
> - Token Exchange 让每一跳都有自己的"护照"
> - JIT + 精细 scope 让最小权限真正落地
> - Policy Engine 让授权不被 LLM 劫持
> - Actor Chain 让多跳可审计
> - HITL 让高危操作有人把关
>
> Dawning 的选择：**Layer 7 身份/策略原生**，复用成熟 IDP（Entra / Okta / Auth0），不自建。

---

## 19. 延伸阅读

- [[concepts/agent-security.zh-CN]] — 安全深度
- [[concepts/protocols-a2a-mcp.zh-CN]] — MCP / A2A
- [[concepts/ai-compliance.zh-CN]] — 合规
- [[concepts/observability-deep.zh-CN]] — 可观测
- OAuth 2.1: <https://oauth.net/2.1/>
- RFC 8693 Token Exchange: <https://www.rfc-editor.org/rfc/rfc8693>
- RFC 9396 RAR: <https://www.rfc-editor.org/rfc/rfc9396>
- RFC 9449 DPoP: <https://www.rfc-editor.org/rfc/rfc9449>
- MCP Authorization: <https://spec.modelcontextprotocol.io/specification/authorization/>
- Microsoft Entra Agent ID: <https://learn.microsoft.com/entra/>
- Okta for AI Agents: <https://www.okta.com/ai/>
- Auth0 GenAI: <https://auth0.com/ai>
- SPIFFE: <https://spiffe.io/>
- OPA: <https://www.openpolicyagent.org/>
- Cedar: <https://www.cedarpolicy.com/>
