---
title: "技能演化专题：Layer 5 完整规约"
type: concept
tags: [skill-evolution, layer-5, reflection, gateway, rollout, self-improvement]
sources: [concepts/dawning-capability-matrix.zh-CN.md, concepts/memory-architecture.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 技能演化专题：Layer 5 完整规约

> **Layer 5 是 Dawning 相对所有主流框架的独有差异化能力**。
> 它回答一个问题：**Agent 能不能像人类一样，从经验中让自己变得更好？**
>
> 本文给出 Layer 5 的完整规约：从 Observation 到 Deployment 的五阶段流水线、Skill 生命周期、质量门禁、灰度与回滚、以及它与其他 Layer 的联动。

---

## 1. 为什么技能演化是必须的

### 1.1 现状：Agent 是"一次性编译"的

| 框架 | Agent 改进的方式 |
|------|-----------------|
| LangChain / LangGraph | 开发者手动改 Prompt / 改图结构 |
| MAF / SK | 开发者手动改 Plugin / Workflow |
| CrewAI | 开发者手动改 Agent role + backstory |
| OpenAI SDK | 开发者手动改 instructions |
| DSPy | **自动优化 Prompt**（但不演化技能） |
| 其他 | 开发者手动 |

→ **所有框架的"改进"都要求人在回路**。LLM 犯一次错、再犯一次错、再犯第一百次错——Agent 自己永远不会变好。

### 1.2 我们想要的

```
Agent 执行任务
    │
    ├─► 成功 ──► 提炼成功模式到 Skill Library
    │
    └─► 失败 ──► 反思失败原因 ──► 生成改进补丁 ──► 门禁验证 ──► 灰度发布
                                                                │
                                          （下次同类任务，行为改进）
```

---

## 2. Skill 的定义

### 2.1 什么是 Skill

> **Skill = Prompt + 工具依赖 + 成功案例 + 失败教训 + 版本号 + 门禁证据**

不是单纯的函数，也不是单纯的 Prompt。

### 2.2 Skill 的结构

```csharp
public record Skill
{
    public string Id { get; init; }                  // "weather-lookup"
    public SemanticVersion Version { get; init; }    // "1.2.3"
    public string Description { get; init; }
    public string Prompt { get; init; }              // 核心 Prompt 模板
    public ToolDependency[] Tools { get; init; }     // 需要的工具列表
    public SkillMetadata Metadata { get; init; }
    public SkillCase[] SuccessCases { get; init; }   // 典型成功案例
    public SkillCase[] FailureCases { get; init; }   // 失败教训
    public QualityGate[] Gates { get; init; }        // 门禁证据
    public SkillLifecycle Lifecycle { get; init; }   // Draft/Review/Active/Deprecated
    public MemoryScope Scope { get; init; }          // Global/Team/Session/Private
}
```

### 2.3 Skill vs Tool vs Agent

| 概念 | 粒度 | 演化 |
|------|------|------|
| Tool | 原子函数 | 人工 |
| Skill | 任务级能力（组合多个 Tool + Prompt 策略） | **可自动演化** |
| Agent | 整体角色（包含多个 Skill） | 通过 Skill 演化间接改进 |

---

## 3. 演化流水线：五阶段

```
┌───────────┐   ┌───────────┐   ┌───────────┐   ┌───────────┐   ┌───────────┐
│ 1. Observe│──►│ 2. Reflect│──►│ 3. Patch  │──►│ 4. Gate   │──►│ 5. Deploy │
└───────────┘   └───────────┘   └───────────┘   └───────────┘   └───────────┘
 收集执行轨迹     反思失败/成功    生成改进提案     门禁验证        灰度上线
```

---

## 4. 阶段 1：Observe（观察）

### 4.1 数据来源

| 来源 | Layer |
|------|-------|
| Agent 执行轨迹（tool_calls + 结果） | Layer 1 |
| Observation Memory（自动提取的模式） | Layer 2 |
| 用户反馈（👍 / 👎 / 评分） | 外部 |
| 下游审计事件（错误、超时、成本） | Layer 7 |

### 4.2 采集什么

```csharp
public record SkillExecutionTrace
{
    public string SkillId { get; init; }
    public SemanticVersion SkillVersion { get; init; }
    public DateTime StartedAt { get; init; }
    public TimeSpan Duration { get; init; }
    public TokenUsage Tokens { get; init; }
    public decimal Cost { get; init; }
    public ToolCall[] ToolCalls { get; init; }
    public SkillOutcome Outcome { get; init; }  // Success/Failure/Partial
    public string? ErrorKind { get; init; }      // HallucinateArg/ToolUnavailable/...
    public UserFeedback? Feedback { get; init; }
}
```

### 4.3 采样策略

- **100% 采集失败轨迹**（珍贵的学习信号）
- **10% 采集成功轨迹**（避免存储爆炸）
- **100% 采集用户反馈为负的**
- 敏感 Scope（private）只采集聚合指标，不采集内容

---

## 5. 阶段 2：Reflect（反思）

### 5.1 反思的两种触发

| 触发 | 频率 | 目标 |
|------|------|------|
| **周期性** | 每天 / 每周 | 持续改进，长尾优化 |
| **事件驱动** | 失败率突增 / 用户差评 | 紧急修复 |

### 5.2 反思 Prompt 模板

```
你是一个 Skill 质量分析师。分析以下执行轨迹：

【当前 Skill 定义】
{skill_prompt}

【最近 50 次执行轨迹】
{traces}

【失败案例】
{failure_cases}

请分析：
1. 失败的根本模式是什么？（参数幻觉 / 工具选择错误 / Prompt 歧义 / ...）
2. 成功案例有什么共同模式？
3. 建议的改进方向：
   - [ ] 修改 Prompt
   - [ ] 增加 / 移除工具
   - [ ] 调整工具调用顺序约束
   - [ ] 增加校验步骤
4. 预期改进效果（可量化）
```

### 5.3 输出：Reflection Report

```csharp
public record ReflectionReport
{
    public string SkillId { get; init; }
    public string RootCause { get; init; }
    public ImprovementProposal[] Proposals { get; init; }
    public decimal ExpectedSuccessRateDelta { get; init; }
    public decimal ExpectedCostDelta { get; init; }
    public int EvidenceCount { get; init; }  // 基于多少条轨迹
}
```

---

## 6. 阶段 3：Patch（补丁生成）

### 6.1 补丁类型

| 类型 | 示例 |
|------|------|
| **Prompt Patch** | 修改 Prompt 模板（增加约束、澄清歧义） |
| **Tool Patch** | 增加 / 移除 / 替换依赖工具 |
| **Flow Patch** | 修改 Tool 调用顺序约束 |
| **Case Patch** | 增加新的 Few-shot 示例 |

### 6.2 补丁表达（diff 风格）

```json
{
  "skillId": "weather-lookup",
  "fromVersion": "1.2.3",
  "toVersion": "1.3.0-rc1",
  "patches": [
    {
      "type": "PromptPatch",
      "op": "replace",
      "path": "/prompt/system",
      "value": "询问天气时，若用户未指定城市，必须先用 request_clarification 工具询问。"
    },
    {
      "type": "ToolPatch",
      "op": "add",
      "tool": "request_clarification"
    },
    {
      "type": "CasePatch",
      "op": "add",
      "case": {
        "input": "今天天气怎么样？",
        "expected_first_action": "request_clarification(question='请问您想查询哪个城市？')"
      }
    }
  ]
}
```

### 6.3 补丁作者：LLM + Human-in-the-loop

- **Tier 1（自动）**：Prompt/Case 级补丁，LLM 自动生成，自动进入门禁
- **Tier 2（半自动）**：Tool/Flow 级补丁，LLM 生成但需 Human 审核
- **Tier 3（手动）**：全新 Skill 创建，Human 编写 LLM 辅助

---

## 7. 阶段 4：Gate（质量门禁）

### 7.1 门禁是硬性阻塞

**没有通过门禁的补丁，不能部署**。

### 7.2 标准门禁集

| 门禁 | 验证什么 | 失败处理 |
|------|---------|---------|
| **Unit Test** | Skill Case 逐条跑通 | 阻塞 |
| **Regression Test** | 旧成功案例仍成功 | 阻塞 |
| **Security Scan** | Prompt 注入、敏感词 | 阻塞 |
| **PII Check** | 补丁不引入 PII 泄漏 | 阻塞 |
| **Cost Guard** | 预期成本不超阈值 | 警告 |
| **Latency Guard** | 预期延迟不超阈值 | 警告 |
| **A/B Eval** | 离线对比新旧版本（LLM-as-Judge） | 阻塞（胜率 < 55%） |

### 7.3 门禁证据链

每个通过的门禁都产生**可审计的证据**：

```csharp
public record QualityGateEvidence
{
    public string GateName { get; init; }
    public GateResult Result { get; init; }
    public Dictionary<string, object> Metrics { get; init; }
    public string ExecutorVersion { get; init; }
    public DateTime ExecutedAt { get; init; }
    public string ArtifactUri { get; init; }  // 详细日志
}
```

证据写入 Skill Registry，永不删除——**技能的"免疫系统"**。

---

## 8. 阶段 5：Deploy（灰度部署）

### 8.1 灰度策略

```
Canary (1%)  ──►  Shadow (10%)  ──►  A/B (50%)  ──►  Full (100%)
    │                 │                   │                │
    └─ 失败率上升     └─ 关键指标劣化    └─ A/B 胜率不足    └─ 稳定运行
         回滚               回滚               回滚            升级为 Active
```

### 8.2 灰度的路由层

灰度不是"所有请求随机"，而是根据 Scope 精确路由：

| Scope | 建议灰度策略 |
|-------|-------------|
| private | 可激进（影响面小） |
| session | 中等 |
| team | 保守（影响团队协作） |
| global | 最保守（双周期 + 人工批准） |

### 8.3 回滚触发

自动回滚条件（全部可配置）：
- 失败率相对旧版本上升 > 5%
- 平均成本上升 > 20%
- P99 延迟上升 > 30%
- 用户差评率 > 阈值
- 审计触发安全事件

### 8.4 版本共存

```
Skill "weather-lookup"
├── v1.2.3 (Active)    ← 老版本继续运行
├── v1.3.0-rc1 (Canary 1%)
└── v1.1.0 (Deprecated) ← 有 deprecation 告警，仍然可用 30 天
```

---

## 9. 与其他 Layer 的联动

```
┌────────────────────────────────────────────────────────┐
│                    Layer 5                             │
│   ┌──────────────────────────────────────────┐         │
│   │  Observe ► Reflect ► Patch ► Gate ► Deploy│        │
│   └──────────────────────────────────────────┘         │
│         ▲          ▲        ▲         ▲       ▲        │
│         │          │        │         │       │        │
└─────────┼──────────┼────────┼─────────┼───────┼────────┘
          │          │        │         │       │
   Layer 1:       Layer 2:  Layer 0:  Layer 7: Layer 4:
   Execution     Observation LLM for  Policy   Skill Router
   Trace         Memory      Patches  Engine   (热切换)
```

### 9.1 与 Layer 2（Observation Memory）

Observation Memory 是 Observe 阶段的首选数据源。

### 9.2 与 Layer 0（LLM Provider）

Reflect 和 Patch 阶段用**低成本后台模型**（如 gpt-4o-mini）。

### 9.3 与 Layer 7（治理）

- Gate 阶段的 PII / Security 检查由 Policy Engine 执行
- Deploy 的每个阶段记录审计日志

### 9.4 与 Layer 4（Skill Router）

Router 按 Skill 版本和灰度规则路由请求——**部署不需要重启**。

---

## 10. 对比：DSPy 的自动优化

DSPy 是唯一一个在自动优化上已有实现的框架。对比：

| 维度 | DSPy | Dawning Layer 5 |
|------|------|----------------|
| 优化对象 | **Prompt（few-shot 示例 + instruction）** | Skill（含 Prompt + Tool + Flow + Case） |
| 优化方法 | 编译器（Bootstrap / MIPRO） | 观察 + 反思 + 补丁 + 门禁 |
| 运行时 | 编译后固定 | **运行时持续演化** |
| 门禁 | Metric 函数 | 多维门禁（Unit / Regression / Security / PII / A-B） |
| 部署 | 重新编译 | 灰度 + 版本共存 + 回滚 |
| 治理 | 无 | Policy Engine + Audit Trail |
| 工程化成熟度 | 研究级 | 生产级（Layer 5 设计目标） |

→ DSPy 证明了方法论可行，Dawning 做工程化落地。

---

## 11. API 示例

### 11.1 DI 注册

```csharp
services.AddSkillEvolution(evo =>
{
    evo.UseLLMForReflection(model: "gpt-4o-mini");
    evo.WithObservationWindow(days: 7);
    evo.AddGate<UnitTestGate>();
    evo.AddGate<RegressionGate>();
    evo.AddGate<PIIGate>();
    evo.AddGate<ABEvalGate>(minWinRate: 0.55m);
    evo.WithRolloutStrategy(ro =>
    {
        ro.CanaryPercent = 1;
        ro.ShadowPercent = 10;
        ro.AbPercent = 50;
        ro.AutoRollbackOn(failureRateIncrease: 0.05m);
    });
});
```

### 11.2 手动触发反思

```csharp
var report = await evolutionEngine.ReflectAsync(
    skillId: "weather-lookup",
    window: TimeSpan.FromDays(3),
    ct);

if (report.ExpectedSuccessRateDelta > 0.02m)
{
    var patch = await evolutionEngine.GeneratePatchAsync(report, ct);
    var evidence = await evolutionEngine.RunGatesAsync(patch, ct);
    if (evidence.AllPassed)
    {
        await evolutionEngine.DeployAsync(patch, ct);
    }
}
```

### 11.3 查询当前 Skill 版本与演化历史

```csharp
var history = await skillRegistry.GetEvolutionHistoryAsync("weather-lookup");
foreach (var entry in history)
{
    Console.WriteLine($"{entry.Version} - {entry.Status} - {entry.SuccessRate:P}");
}
```

---

## 12. 风险与防范

| 风险 | 防范 |
|------|------|
| **自演化放飞** | 必须通过 Gate；global/team Scope 必须人工审批 |
| **成本飞涨** | Cost Guard + 预算上限 |
| **对抗样本触发劣化** | Regression Gate 守住旧案例 |
| **补丁循环振荡** | 补丁冷却期 + 最大补丁频率限制 |
| **审计合规** | 全链路 Evidence 链，满足 SOC2 / ISO 27001 |

---

## 13. 路线图

| 阶段 | 能力 |
|------|------|
| **M0 / Phase 0** | Skill 数据结构 + Skill Registry + 手动版本管理 |
| **M1** | Observe 阶段（Trace 采集 + 聚合） |
| **M2** | Reflect 阶段（LLM 反思报告） |
| **M3** | Patch 生成 + Unit/Regression Gate |
| **M4** | Security/PII/AB Gate + 灰度部署 |
| **M5** | 自动回滚 + Skill Marketplace |

---

## 14. 小结

> Layer 5 是 Dawning 相对所有 Agent 框架的**结构性差异化**。
>
> 别人的 Agent 是"静态脚本 + 人肉改进"，
> Dawning 的 Agent 是"动态技能 + 自我演化 + 工程级保障"。
>
> 这不是一个功能，这是 **Agent OS 从 Framework 分叉出来的原因**。

---

## 15. 延伸阅读

- [[concepts/dawning-capability-matrix.zh-CN]] — 16 接口中 Layer 5 的四个
- [[concepts/memory-architecture.zh-CN]] — Observation Memory 作为 Layer 5 输入
- [[comparisons/agent-os-vs-frameworks]] — 为什么 Framework 做不到这一点
- DSPy：<https://github.com/stanfordnlp/dspy>
- Reflexion 论文：<https://arxiv.org/abs/2303.11366>
