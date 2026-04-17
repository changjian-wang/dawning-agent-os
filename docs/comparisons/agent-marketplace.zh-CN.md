---
title: "Agent 生态经济学：Marketplace、GPTs、Agent Store、Skill 交易、定价模型"
type: comparison
tags: [marketplace, gpts, agent-store, pricing, economy, skill-market, monetization, m2m-payment]
sources: [concepts/skill-evolution.zh-CN.md, concepts/dawning-capability-matrix.zh-CN.md]
created: 2026-04-18
updated: 2026-04-18
status: active
---

# Agent 生态经济学：Marketplace、GPTs、Agent Store、Skill 交易、定价模型

> Agent 生态正在复刻移动互联网的"应用市场 + 开发者经济"：
> - GPTs Store（OpenAI）
> - Agent Space / AI Agents (Google)
> - Claude Agent Skills (Anthropic)
> - Copilot Studio / Agent Builder (Microsoft)
> - Poe / Character.ai
> - Hugging Face Spaces
> - Mastra / Dify / Langflow 模板市场
>
> 同时出现新形态：
> - Agent-to-Agent 交易（M2M payment）
> - Agent 订阅即服务（AaaS）
> - Skill NFT / 可信数字工匠
> - Autonomous commerce（Agent 代购、代订、代投）
>
> 本文梳理 Agent 经济形态、平台对比、定价模型、Dawning 策略。

---

## 1. 为什么 Agent 需要市场

### 1.1 应用市场的规律

1. 开发者规模化
2. 用户发现 + 信任
3. 分发标准化
4. 货币化通道

### 1.2 Agent 的新动因

- 长尾场景：难以自研，买现成更快
- 专业垂直：医疗 / 法律 / 金融有专业 agent
- 工具组合：一个 agent 背后可能上百个 skill
- 数据隔离：平台级沙箱

---

## 2. 主要平台对比

### 2.1 产品矩阵

| 平台 | 出品 | 形态 | 收费 | 分成 |
|------|------|------|------|------|
| **GPTs Store** | OpenAI | Chat GPT wrapper | 订阅 Plus / 按 usage | 70/30（规划） |
| **Google Agent Space** | Google | 企业内部 | 企业订阅 | 内部 |
| **Anthropic Claude Agent Skills** | Anthropic | Skill 包 | 随 Claude | 未开放 |
| **Microsoft Copilot Agents** | Microsoft | Teams / M365 | 企业订阅 | 合作模式 |
| **Poe** | Quora | 多模型聊天 | 用量 | 按创作者 |
| **Character.ai** | CAI | 人格 Agent | 订阅 | 不透明 |
| **Hugging Face Spaces** | HF | 应用托管 | 免费/付费 | 计算费 |
| **Replit Agents** | Replit | 开发 Agent | 订阅 | 自有 |
| **Salesforce AgentExchange** | Salesforce | 企业 CRM agents | 企业 | 类 AppExchange |
| **ServiceNow Agentic AI Store** | ServiceNow | ITSM agents | 企业 | 合作 |
| **Crew.AI Enterprise** | Crew | Crew 模板 | 企业 | 未 |
| **Mastra Agent Hub** | Mastra | OSS 模板 | 免费 | - |

### 2.2 面向企业 vs 消费者

| 类型 | 例子 |
|------|------|
| **消费级** | GPTs / Poe / Character.ai |
| **开发者** | Mastra / Langflow / HF |
| **企业内部** | Agent Space / Copilot / Salesforce |
| **垂直专业** | 医疗 / 法律专门市场（新兴） |

---

## 3. GPTs Store 深度剖析

### 3.1 GPTs 定义

- 系统 prompt + knowledge + actions (API) 的组合
- 无自主 agent loop（单轮扩展）
- 非真正 agent

### 3.2 优点

- 极低门槛（无代码）
- 海量流量（ChatGPT 用户）
- 快速发现

### 3.3 局限

- 能力弱（本质是 prompt 模板）
- 商业化不清晰
- 平台依赖

### 3.4 经济模型

- 2024 宣布创作者分成
- 落地缓慢
- 多数创作者未盈利

### 3.5 启示

- 市场需要真正能力，不是 prompt 包装
- Agent Store 应以"能做什么"为核心

---

## 4. Agent-to-Agent 交易

### 4.1 场景

- Agent A 完成用户任务时发现需专业能力
- 从 Agent Marketplace 调用 Agent B
- 自动计费 + 分成

### 4.2 关键基础设施

- **Agent Card 发现**（A2A Protocol）
- **标准支付**（x402 protocol / Stripe Agent）
- **信任评级**
- **审计**

### 4.3 x402 Payment Protocol

- Coinbase 2025 发布
- HTTP 402 状态码复用
- Agent 间机器可结算
- 常基于稳定币

### 4.4 Stripe Agent / PayPal Agent Toolkit

- 传统支付公司适配 Agent 场景
- SDK 让 Agent 发起 / 授权交易
- 合规 + 风控

### 4.5 挑战

- 信任：Agent 是否真做了？
- 争议：任务失败怎么赔？
- 合规：KYC / 税务

---

## 5. 定价模型

### 5.1 经典模型

| 模型 | 说明 | 适合 |
|------|------|------|
| **按订阅** | 月/年固定 | SaaS、个人助手 |
| **按 token** | LLM 成本透传 | 开发者 API |
| **按任务** | 每次执行 | 具体任务型 |
| **按结果** | 成功才付 | 高价值 (合同审阅) |
| **按价值分成** | 客户收益分一部分 | 投研、销售 agent |
| **Freemium** | 基础免费 + 高级付 | 大众市场 |

### 5.2 Agent 特有变体

- **按 step 收**（每次 tool call）
- **按 minute 收**（长时任务时长）
- **按数据处理量**
- **按节省的成本**（客户视角 ROI）

### 5.3 定价难点

- LLM 成本浮动
- 任务成本高方差（简单 vs 难）
- 失败重试费谁付
- 用户难懂技术计价

### 5.4 实践建议

- 消费：订阅为主 + 简单 metering
- 企业：按订阅 + 额度 + 超额累进
- 开发者 API：按 token / step
- 保留 commit discount

---

## 6. Skill 交易（新兴）

### 6.1 Skill 作为商品

- 独立可复用单元
- 可组合（一个 Agent 装多个 Skill）
- 可签名（防篡改）
- 可收费

### 6.2 "Skill NFT" 想法

- 不推荐真上 NFT / 区块链（多数场景无需）
- 但"可转让、可证明、可追溯"的数字工匠概念有价值
- 签名 + 注册中心足够

### 6.3 Anthropic Skills

- Skill = 文件包（markdown 指令 + 资源）
- 可随 Claude 分发
- 目前非付费市场，但基础设施在

### 6.4 Microsoft Declarative Agents

- Teams 集成的 Declarative Agent 规范
- 通过 agent package (.zip) 分发

### 6.5 OpenAI Assistants + Apps SDK

- 2025 新 Apps SDK 允许在 ChatGPT 中分发
- App = Agent-like 扩展

### 6.6 未来形态

- 中心化市场（主流）
- 去中心化注册（小众）
- 企业私有市场（重要）

---

## 7. 企业内部 Agent Marketplace

### 7.1 价值

- 大企业有数百 Agent
- 员工找不到 / 重复开发
- 需要治理 + 发现中心

### 7.2 核心能力

- Catalog / Discovery
- Permission / RBAC
- Cost allocation
- Usage metrics
- Approval workflow
- Version & Lifecycle

### 7.3 产品

- **Google Agent Space**
- **Microsoft Copilot Studio Agent Catalog**
- **Salesforce AgentExchange**
- **ServiceNow Agentic AI Store**
- **自建** (基于 Azure API Center / AWS Bedrock AgentCore / 自研)

### 7.4 实施建议

- 先有治理，再有市场
- Policy as Code
- 与 IDP / SSO 集成
- 成本透明

---

## 8. 信任与质量

### 8.1 评级维度

- 准确度（benchmark / user rating）
- 安全（policy / policy 违规历史）
- 稳定（uptime / 错误率）
- 成本可预测
- 隐私（数据流向）

### 8.2 认证机制

- 平台审核（App Store 风）
- 第三方审计
- 社区投票
- 实名 + 责任主体

### 8.3 防滥用

- 反复制（签名）
- 反刷量（风控）
- 反恶意（prompt injection 检测）

---

## 9. 发现机制

### 9.1 分类 / 标签

- 传统目录
- 适合浏览

### 9.2 场景驱动

- "我想做 X" → 推荐 Agent
- NLQ 发现

### 9.3 Agent-to-Agent 发现

- Agent Card（A2A）
- 能力声明
- 其他 Agent 自动发现

### 9.4 AI 推荐

- 根据用户历史 + 任务类型
- 动态组合多 Agent

---

## 10. 实体用例

### 10.1 个人消费者

- 订阅聚合：Perplexity Pro + 多 Agent
- 按需使用：财务分析 Agent ¥5/次

### 10.2 小团队

- Dify / Langflow 搭建 + 内部用
- 或买 SaaS Agent（Sales / Support）

### 10.3 大企业

- 内部 Agent Space + 接入公司知识
- 统一治理 + 成本分摊
- 混合：内部 + 外部精选 Agent

### 10.4 开发者

- 上架 GPTs / Skills / Apps
- 或 API-first 供 Agent 消费

---

## 11. 经济学分析

### 11.1 价值链

```
LLM Provider (OpenAI / Anthropic)
   ↓ 成本
Agent Framework (LangGraph / Mastra / Dawning)
   ↓ 易用性
Agent Developer (公司 / 个人)
   ↓ 垂直能力
Marketplace / Platform (GPTs / Agent Space)
   ↓ 发现 + 分发
End User (消费者 / 企业)
```

### 11.2 毛利分布

- LLM Provider：高（算力规模）
- Framework：中（开源多、商业化难）
- Developer：分化（垂直爆款 vs 长尾）
- Platform：高（分成 + 流量）
- User：节省（vs 自研成本）

### 11.3 谁赢

- 基础模型：集中（前 3）
- 平台：集中（前 3-5）
- 开发者：长尾 + 爆款
- 框架：混战中

---

## 12. 监管与合规

### 12.1 消费者保护

- 透明（谁做的 / 用了什么模型）
- 撤销订阅
- 数据删除权

### 12.2 未成年人

- 内容审核
- 年龄验证
- Character.ai 类产品重点监管

### 12.3 虚假广告

- Agent 能力不得夸大
- 真实测试数据

### 12.4 版权

- Agent 用的 knowledge 合法性
- 生成内容归属

### 12.5 税务

- Agent 收入归属
- 跨境 + 数字服务税

---

## 13. 挑战

| 挑战 | 现状 |
|------|------|
| 货币化未跑通 | GPTs 3 年未盈利多数创作者 |
| 质量参差 | 缺统一评估 |
| 发现难 | SEO 搬到 AgentO |
| 依赖平台 | 平台改规则 = 生死 |
| 基础设施缺 | 标准化刚起步（A2A / MCP）|
| 交易结算 | x402 早期 |
| 信任 | 评级 / 审计不成熟 |

---

## 14. 可能的商业模式创新

### 14.1 Agent-as-Subscription

- 专业 Agent 包月（例：Research Agent ¥99/月）

### 14.2 Agent-as-Service

- API 付费（按 step / task）

### 14.3 Agent-Based Consulting

- 咨询 + 配套 Agent 交付

### 14.4 Success-Based

- 销售 Agent：成单分成
- 投研 Agent：alpha 分成

### 14.5 Agent 互换（以物易物）

- Agent A 提供工具 X，换 Agent B 提供工具 Y
- 实验阶段

---

## 15. Dawning 对 Marketplace 的适配

### 15.1 不做什么

- Dawning 不自建市场
- 不搞 NFT / 币
- 不做支付

### 15.2 做什么

- **Skill 包规范**：Dawning Skill Package（Markdown + 签名）
- **Agent Card**：遵循 A2A 规范
- **Skill Registry**：可接 HTTP / OCI / 私有
- **MCP Marketplace 客户端**：消费 MCP servers
- **Cost / Usage 可观测**：Layer 7 标准 meter
- **企业内 Agent Catalog 适配**：接 Azure / Google Agent Space

### 15.3 接口

```csharp
public interface IAgentCatalog
{
    Task<IReadOnlyList<AgentCard>> SearchAsync(string query, CancellationToken ct);
    Task<AgentCard> GetAsync(string id, CancellationToken ct);
    Task<AgentHandle> InstallAsync(string id, InstallOptions opt, CancellationToken ct);
}

public interface ISkillRegistry
{
    Task RegisterAsync(SkillPackage pkg, CancellationToken ct);
    Task<Skill> ResolveAsync(string name, string? version, CancellationToken ct);
}
```

### 15.4 支付适配

- 不内建
- 提供 `IPaymentProvider` 抽象
- 具体实现接 Stripe Agent / x402 / PayPal Agent Toolkit / 企业内部计费

---

## 16. 趋势预测（2026-2028）

- **2026**：企业内 Agent Catalog 普及；消费级仍 GPTs/Poe/Character 三强
- **2027**：垂直 Agent 市场出现（医疗 / 法律 / 金融）
- **2028**：A2A + x402 成熟，Agent 间经济 Usable
- **长线**：Agent 成为新一代开发单位，经济规模万亿级

---

## 17. 小结

> Agent 经济学核心问题不是"能不能做"，而是：
> - **谁信任谁**（Trust）
> - **谁付谁钱**（Payment）
> - **谁分谁多少**（Revenue Share）
> - **谁对质量负责**（Accountability）
>
> 基础设施正在成型（A2A / MCP / x402 / Agent Catalog）。
> Dawning 做好**接入层与治理**（Skill 包规范、Agent Card、Cost 观测、IPaymentProvider 抽象），
> 让应用方无痛参与多市场，不被单一平台绑定。

---

## 18. 延伸阅读

- [[concepts/skill-evolution.zh-CN]] — Skill 概念演进
- [[concepts/protocols-a2a-mcp.zh-CN]] — A2A / MCP
- [[concepts/cost-optimization.zh-CN]] — 成本治理
- [[concepts/agent-identity-auth.zh-CN]] — 身份认证（交易前提）
- [[concepts/enterprise-roadmap.zh-CN]] — 企业落地
- GPTs Store: <https://openai.com/chatgpt/store>
- Google Agent Space: <https://cloud.google.com/products/agentspace>
- Anthropic Agent Skills: <https://www.anthropic.com/news/agent-skills>
- Microsoft Declarative Agents: <https://learn.microsoft.com/microsoft-365-copilot/extensibility/>
- Salesforce AgentExchange: <https://www.salesforce.com/agentforce/agentexchange/>
- x402 Protocol: <https://www.x402.org/>
- Stripe Agent Toolkit: <https://stripe.com/newsroom/news/ai-agents>
- PayPal Agent Toolkit: <https://developer.paypal.com/community/blog/agent-toolkit/>
- A2A Protocol: <https://a2a-protocol.org/>
