---
title: "企业 Agent 落地路线图：从 PoC 到生产的 12 个阶段"
type: concept
tags: [enterprise, adoption, roadmap, poc, production, maturity-model]
sources: [concepts/deployment-architectures.zh-CN.md, concepts/agent-evaluation.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 企业 Agent 落地路线图：从 PoC 到生产的 12 个阶段

> 大多数企业 Agent 项目死在"PoC 到生产"的鸿沟里——demo 惊艳，生产翻车。
> 不是技术难，是**工程成熟度**不够：eval 缺、观测缺、治理缺、演化缺。
>
> 本文给出 12 阶段成熟度模型，每阶段的目标、产出、常见失败与 Dawning 的对应能力。

---

## 1. 为什么需要路线图

### 1.1 常见误区

| 误区 | 后果 |
|------|------|
| "Demo 好用就能上" | 生产 3 天翻车 |
| "先做大的" | 投入巨大，失败即沉没 |
| "先买框架再说" | 框架不解决业务问题 |
| "先规划完美架构" | 永远无法开始 |
| "Agent 越复杂越好" | Debug 地狱 |
| "人人都能用 AI Agent" | 没有专门团队 → 烂尾 |

### 1.2 成熟度五段

```
L0 探索   →   L1 PoC   →   L2 Pilot   →   L3 Production   →   L4 Platform
（学习）   （可行性）    （小规模真实）    （规模化运营）      （多业务复用）
```

---

## 2. 12 阶段路线图

```
L0 探索期                   L1 PoC                L2 Pilot              L3 Production         L4 Platform
├── 1. 识别场景             ├── 4. 搭 PoC         ├── 7. Dataset         ├── 10. 规模化        ├── 12. 能力平台
├── 2. 搭认知               ├── 5. 基线 Eval      ├── 8. 限流/治理       ├── 11. 演化/运营
└── 3. 小组成立             └── 6. 红队测试        └── 9. 可观测性
```

---

## 3. 阶段 1：识别场景（Identify）

### 3.1 目标

找到 **1-2 个高价值、可评估、低风险**的场景。

### 3.2 评估维度

```
Value × Feasibility × Risk = Priority

Value：
  - 节省人力小时数？
  - 提升收入 / 客户满意度？
  - 业务战略意义？

Feasibility：
  - 有数据吗？
  - 任务是否可界定？
  - 指标是否可量化？

Risk：
  - 出错代价多大？
  - 合规 / 法务红线？
  - 可逆吗？
```

### 3.3 黄金起步场景

| 场景 | 原因 |
|------|------|
| 内部知识问答 | 风险低、价值高、易评估 |
| 客服 FAQ 辅助 | 有历史工单 |
| 代码评审辅助 | 开发者技术接受度高 |
| 营销内容草稿 | 容错高 |
| 数据报表生成 | 结构化输入 |

### 3.4 避免起步场景

- 自动化金融决策
- 医疗诊断
- 法律判决
- 完全取代人工的前台客服（2026 仍有困难）

---

## 4. 阶段 2：搭认知（Learn）

### 4.1 谁要懂

- **业务方**：能讲清楚痛点
- **产品经理**：能把痛点转成指标
- **AI 工程师**：技术选型与实施
- **数据工程师**：RAG / Dataset
- **安全/合规**：尽早介入

### 4.2 学习路径

- 基础：本知识库前 10 篇
- 实践：用 OpenAI Playground + ChatGPT 亲手做
- 案例：竞品 / 同行业 Agent 产品

### 4.3 避坑

- 不要只看 Demo，要看**失败案例**
- 不要相信"5 分钟搭个 Agent"
- 不要跳过可观测性与 eval

---

## 5. 阶段 3：小组成立（Team）

### 5.1 最小编制

```
产品经理 × 1
AI 工程师 × 1-2
后端工程师 × 1
数据 / Prompt 工程师 × 1
SRE / 运维 × 0.5
安全顾问 × 0.5
业务专家（兼职）× 1-2
```

### 5.2 组织形态

- **嵌入式**：Agent 小组嵌入业务线
- **平台式**：中心 AI 平台组，业务线对接
- **混合**：中心平台 + 业务线分布

大企业推荐**混合**：平台组负责框架与治理，业务线负责场景。

---

## 6. 阶段 4：搭 PoC（Proof of Concept）

### 6.1 目标

用 **2-4 周**快速验证技术可行性。

### 6.2 PoC 范围

- 单一场景 + 单一 Agent
- 10-50 个测试用例
- 本地或 sandbox 环境
- 不追求生产级性能

### 6.3 技术栈（最小）

```
LLM: OpenAI GPT-4o-mini 或同级
框架: Dawning Starter / LangGraph / SK（选一个）
向量库: pgvector / Chroma
前端: 简单 Web UI（可用 Streamlit / Gradio）
```

### 6.4 关键产出

- Demo
- 成本估算
- 关键指标基线
- 风险清单

### 6.5 失败信号

- 核心指标 < 50%（可能场景不 fit）
- 成本远超预期
- LLM 无法稳定处理核心意图

→ **不要盲目继续**，回到阶段 1 重新识别。

---

## 7. 阶段 5：基线评估（Baseline Eval）

### 7.1 目标

建立**可重复的量化指标**。

### 7.2 必备

- Golden Set 50-200 条（人工构造，见 [[concepts/dataset-building.zh-CN]]）
- 3-5 个核心指标（accuracy / completeness / safety / cost / latency）
- 自动跑 + 结果对比报告

### 7.3 工具

- Langfuse / LangSmith / Braintrust
- 也可以 Excel + Python 起步

### 7.4 Definition of Ready

进入下阶段的门槛：

- [ ] Golden Set ≥ 50 条
- [ ] 至少一次完整 eval 报告
- [ ] 核心指标 ≥ 70%
- [ ] 人工 review 通过

---

## 8. 阶段 6：红队测试（Red Team）

### 8.1 目标

找出 Agent 的**失败模式 + 安全漏洞**。

### 8.2 维度

- Prompt 注入
- Jailbreak
- 错误信息 / 幻觉
- 权限越界
- PII 泄漏
- 跨用户泄漏

### 8.3 工具

- PyRIT (Microsoft)
- Garak
- 人工对抗样本

### 8.4 产出

- 已知失败模式清单（带示例）
- 防御措施优先级
- 不可接受风险 → 阻断上线

---

## 9. 阶段 7：Dataset 工程化（Dataset）

### 9.1 目标

从"50 条手写"升级到**生产级 dataset 工程**。

### 9.2 工作

- 合成扩展到 500-2000 条
- 建立 dev/test/holdout split
- 版本化管理
- 标注准则文档化
- 建立 Trace → Dataset 流水线

见 [[concepts/dataset-building.zh-CN]]。

---

## 10. 阶段 8：限流 / 治理（Governance）

### 10.1 目标

确保 Agent 上线后**不失控**。

### 10.2 清单

- [ ] 每请求成本硬上限
- [ ] 每用户每日预算
- [ ] RPS / TPS 限流
- [ ] 危险工具审批（HITL）
- [ ] Policy Engine 集成（OPA）
- [ ] PII 脱敏
- [ ] Audit Trail
- [ ] Secrets Vault

---

## 11. 阶段 9：可观测性（Observability）

### 11.1 目标

**看得见 Agent 在干什么**。

### 11.2 清单

- [ ] OTel Trace 全覆盖
- [ ] GenAI SemConv 属性完整
- [ ] Langfuse / Phoenix / Grafana 任选一
- [ ] 关键 KPI Dashboard（RPS / 延迟 / 成本 / 成功率 / Token / 步数）
- [ ] 安全事件 Alert
- [ ] 成本突增 Alert
- [ ] Trace → Eval 反向回流

见 [[concepts/observability-deep.zh-CN]]。

---

## 12. 阶段 10：Pilot 上线（Pilot）

### 12.1 目标

**小规模真实用户**使用，验证生产表现。

### 12.2 策略

- 10-100 个 Beta 用户
- Canary 部署（< 10% 流量）
- 有明显的"实验功能"标识
- 反馈通道畅通（内嵌 👍/👎 + 自由文本）
- 随时可回滚

### 12.3 成功标准

- 主要指标不低于 PoC
- 用户反馈 NPS > 30
- 无安全事件
- 成本可控
- 支持团队响应得过来

### 12.4 常见失败

- 比 PoC 差很多 → 分布 shift，回去扩 dataset
- 成本失控 → 优化 prompt + 缓存 + 路由
- 高频投诉某类问题 → 修 + 回归

---

## 13. 阶段 11：规模化（Scale）

### 13.1 目标

从 Pilot 扩展到**全量生产**。

### 13.2 技术准备

- 水平扩展压测
- 跨 Region / HA
- 容灾 / 降级预案
- 成本优化（见 [[concepts/cost-optimization.zh-CN]]）
- 多 LLM 供应商 Fallback

### 13.3 运营准备

- 7x24 监控
- On-call 流程
- SOP / Runbook
- 用户反馈 SLA
- 数据合规审计

---

## 14. 阶段 12：演化 / 平台化（Evolve / Platform）

### 14.1 目标

- 从"一个 Agent"到"Agent 工厂"
- 沉淀可复用能力

### 14.2 Agent 演化

- Skill 版本化
- Canary + 自动回滚
- Reflection 流水线
- 定期 dataset 更新

见 [[concepts/skill-evolution.zh-CN]]。

### 14.3 平台化

```
中心 AI 平台 = 
  Dawning Kernel +
  统一 LLM 网关 +
  共享向量库 +
  Dataset Hub +
  Eval Platform +
  Observability Stack +
  Skill Marketplace（内部）
  
业务线 = 
  Agent 定义（YAML/Code）+ 
  业务数据 +
  业务工具
```

### 14.4 关键原则

- 能力下沉（横切放平台）
- 业务上浮（语义放业务）
- Skill 市场化（内部流通）
- 数据资产化（dataset 是资产）

---

## 15. 跨阶段的横切关注

### 15.1 成本

- L0-L1: $10-100/月
- L2 Pilot: $1K-10K/月
- L3 Production: $10K-1M/月
- L4 Platform: 按多租户成本模型

### 15.2 时间

- L0→L1: 2-4 周
- L1→L2: 1-3 月
- L2→L3: 3-6 月
- L3→L4: 12-24 月

### 15.3 人员

- L0-L1: 3-5 人
- L2: 5-10 人
- L3: 10-30 人
- L4: 30-100+ 人

---

## 16. 成熟度自检清单

**L1 Ready**：
- [ ] 场景价值清晰
- [ ] Demo 跑通
- [ ] 成本数量级估算

**L2 Ready**：
- [ ] Golden Set
- [ ] 基线 eval
- [ ] 红队通过
- [ ] 限流 / 审批

**L3 Ready**：
- [ ] Dataset 工程化
- [ ] 可观测性完备
- [ ] Pilot 成功标准达成
- [ ] 容灾预案
- [ ] 合规审核

**L4 Ready**：
- [ ] 2+ 场景复用同一平台
- [ ] Skill 演化闭环
- [ ] Dataset / Eval 平台化
- [ ] 内部开发者自助

---

## 17. 常见陷阱

| 阶段 | 陷阱 |
|------|------|
| L0 | 跟风上 Agent，没找到真痛点 |
| L1 | PoC 做完就直接上生产 |
| L2 | Pilot 反馈不搭回路 |
| L3 | 上线后不演化，质量衰减 |
| L4 | 平台化过早，重复造轮子 |

---

## 18. Dawning 的阶段适配

| 阶段 | Dawning 能力 |
|------|-------------|
| L1 PoC | Starter 模板、Ollama 本地、单 Agent 示例 |
| L2 Pilot | OTel 接入、限流、预算、Canary |
| L3 Production | 多 Region、分布式 Worker、Skill 版本、Fallback |
| L4 Platform | Skill Registry、Multi-Tenant、A2A/MCP 跨团队、Dataset Hub |

Dawning 的 8 层架构每层都在不同阶段发挥作用——

- L0-L1 只用 Layer 0-3（Core Agent）
- L2 加 Layer 6-7（接入外部 + 治理）
- L3 加 Layer 5（演化）+ 完整可观测
- L4 加 Layer 5 全部 + Multi-Tenant + A2A

---

## 19. 失败案例教训

### 19.1 典型失败模式

1. **"Demo 驱动"综合症**：一遍过的 demo 被当生产证据
2. **"Prompt Whisperer"依赖**：一个人懂 prompt，他离职就崩
3. **"单 Agent 做一切"**：Agent 长到无法 debug
4. **"RAG 银弹"**：所有问题扔 RAG，召回差就加数据
5. **"反馈黑洞"**：👎 数据进 DB 就没人看
6. **"合规补丁"**：上线后发现 PII，临时打补丁
7. **"无 Eval 就升级"**：换模型全凭感觉
8. **"Gold 不更新"**：生产问题不回流，eval 永远乐观

### 19.2 成功共性

- 小切入、快迭代、重测量
- 业务专家全程参与
- 数据流水线先行
- 可观测性不妥协
- 演化与治理配套

---

## 20. 小结

> **Agent 不是"装一次就好"的软件，是"持续运营"的生命体。**
>
> 从 L0 到 L4，每一阶段的失败都可以复制前人教训少踩坑。
> Dawning 把 12 阶段的关注点沉淀为 8 层能力，从 L1 就能起步，
> 一步一步长到 L4——不用换框架，只换使用深度。

---

## 21. 延伸阅读

- [[concepts/dawning-capability-matrix.zh-CN]] — 8 层能力全景
- [[concepts/dataset-building.zh-CN]] — L2 核心
- [[concepts/observability-deep.zh-CN]] — L3 核心
- [[concepts/skill-evolution.zh-CN]] — L3-L4 演化
- [[concepts/cost-optimization.zh-CN]] — L3 规模化前置
- [[concepts/agent-security.zh-CN]] — 各阶段安全
- Gartner AI Maturity Model
- Andrew Ng AI Transformation Playbook
