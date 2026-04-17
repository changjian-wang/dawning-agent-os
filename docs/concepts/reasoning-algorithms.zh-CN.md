---
title: "Agent 推理算法：ReAct、Plan-Execute、Reflexion、ToT、LATS、RAP"
type: concept
tags: [reasoning, react, plan-execute, reflexion, tree-of-thought, lats, rap, algorithm]
sources: [concepts/agent-loop.md, concepts/prompt-engineering-dspy.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agent 推理算法：ReAct、Plan-Execute、Reflexion、ToT、LATS、RAP

> "Agent 循环"只是最外层结构，内部用什么**推理算法**决定能力天花板。
> ReAct 是起点，Plan-Execute 提高结构性，Reflexion 加入自我修正，Tree-of-Thought / LATS 引入树搜索，RAP 引入世界模型。
>
> 本文梳理六大推理算法的思路、适用场景、复杂度与 Dawning 的算法可插拔设计。

---

## 1. 为什么需要不同算法

| 任务 | 简单循环够吗 |
|------|------------|
| 客服问答 | ✅ ReAct 够 |
| 多步规划（订机票酒店） | ⚠️ 需要 Plan-Execute |
| 失败后自我改进 | ❌ 需要 Reflexion |
| 数学 / 代码 / 博弈 | ❌ 需要 ToT / LATS |
| 长程规划 + 世界建模 | ❌ 需要 RAP |

**核心观察**：越复杂任务，越需要**搜索 + 评估 + 回溯**，而不只是贪心前进。

---

## 2. 算法谱系图

```
                      ┌─ ReAct          (2022) 贪心，LLM=策略
        贪心单路径 ────┤
                      └─ Plan-Execute   (2023) 先计划后执行
                             │
                      ┌─ Reflexion      (2023) 加自反思修正
   线性+反思 ─────────┤
                      └─ Self-Refine    (2023) 循环精修输出
                             │
                      ┌─ Tree-of-Thought (2023) 多分支探索
    搜索+评估 ────────┤
                      ├─ Graph-of-Thought (2023) 节点可复用
                      ├─ LATS           (2023) ToT + MCTS
                      └─ RAP            (2023) 世界模型 + MCTS
```

---

## 3. ReAct（Reasoning + Acting）

### 3.1 核心思想

交替输出 **Thought / Action / Observation**：

```
Thought: 用户问 X，我需要先搜索。
Action: search("X")
Observation: [搜索结果...]
Thought: 结果说 Y。还需要查 Z。
Action: search("Z")
Observation: ...
Thought: 已有足够信息，可以回答。
Answer: ...
```

### 3.2 优势

- 简单直接
- Reasoning 与 Acting 交织
- 易调试
- 所有框架默认支持

### 3.3 局限

- **贪心**：一旦走错难回头
- **短视**：不考虑全局最优
- **循环风险**：容易卡死 A→B→A
- **探索弱**：只走一条路径

### 3.4 最佳实践

- 设 `max_steps` 上限
- 加 reflection 节点周期性检查
- 失败时清空部分上下文重试

### 3.5 代表框架

LangChain ReAct Agent、MAF ChatCompletionAgent、OpenAI Assistants、绝大多数"简单 Agent"。

---

## 4. Plan-and-Execute

### 4.1 核心思想

**先完整规划，再逐步执行**：

```
阶段 1 (Plan):
  LLM → [step1, step2, step3, step4, step5]

阶段 2 (Execute):
  for step in plan:
      result = worker.run(step)
  
阶段 3 (Replan, 可选):
  如果某步失败或结果意外 → 重新 planning
```

### 4.2 优势

- **全局视角**：规划阶段看到全局
- **高效**：单次 planning 比每步都推理便宜
- **可并行**：独立步骤可并发
- **结构清晰**：便于审计

### 4.3 局限

- Plan 可能不完美
- Plan 期间缺少 observation（盲规划）
- 需要 replanning 机制应对变化

### 4.4 变体

| 变体 | 说明 |
|------|------|
| **Static Plan** | 一次规划到底 |
| **Hierarchical Plan** | 粗规划 → 精规划 |
| **Plan + Replan** | 失败后局部重新规划（LangGraph plan-and-execute） |
| **ADaPT** | 任务难则递归分解 |

### 4.5 代表

LangGraph Plan-Execute、Deep Agents、AutoGPT、BabyAGI（早期）。

---

## 5. Reflexion

### 5.1 核心思想

**失败后自我反思，把反思写入 memory 再试**：

```
尝试 1: 失败 (reward=0)
    │
    ▼
Reflect: "我为什么失败？因为没检查 X 条件。"
    │
    ▼
反思写入 memory
    │
    ▼
尝试 2: 带着反思再做 → 可能成功
    │
    ▼
（直到成功 or 达到上限）
```

### 5.2 关键组件

- **Actor**：尝试任务的 LLM
- **Evaluator**：给出 reward（0/1 or 评分）
- **Self-Reflector**：失败时产出反思文本
- **Memory**：保存反思用于下次尝试

### 5.3 优势

- 从失败中学习
- 无需 fine-tune（pure prompt-level）
- HumanEval / AlfWorld 等 benchmark 显著提升

### 5.4 局限

- 依赖可信 Evaluator（真实场景常缺）
- Reflection 可能自我强化偏见
- 只在**同一任务多次尝试**场景有效

### 5.5 适用

- 代码生成（测试失败 → 修改）
- 数学（verify 失败 → 重推）
- 游戏 / 环境任务（明确 reward）

---

## 6. Self-Refine

### 6.1 核心思想

**生成 → 自我评价 → 精修 → 循环**（单任务迭代）：

```
Draft 0
   ↓ self-critique
Critique 0
   ↓ refine
Draft 1
   ↓
...直到收敛
```

### 6.2 vs Reflexion

| 维度 | Self-Refine | Reflexion |
|------|-------------|-----------|
| 目标 | 精修同一输出 | 跨 episode 学习 |
| 信号 | LLM 自己评 | 外部 reward |
| 场景 | 写作 / 摘要 | 任务完成 |

### 6.3 注意

- 需控制迭代次数（收敛/发散）
- 可能越改越差（负改进）
- 配合 **stop criterion**（质量无提升即停）

---

## 7. Tree-of-Thought (ToT)

### 7.1 核心思想

**把推理当成树搜索**：每步生成多个候选，评估，挑最好的继续。

```
           Root
          / | \
         A  B  C          ← 候选 thoughts
        /|  |  |\
       A1 A2 B1 C1 C2     ← 各自展开
       ...
```

### 7.2 四要素

| 要素 | 说明 |
|------|------|
| **Thought generator** | 每节点生成 k 个子 thought |
| **State evaluator** | 给每个 thought 打分 |
| **Search algorithm** | BFS / DFS / Beam Search |
| **Terminal check** | 判定何时停 |

### 7.3 案例：Game of 24

给定 4 个数，用 +-*/ 得到 24。

- ReAct：贪心走一条路，失败率高
- ToT：BFS 展开多个表达式，评估，显著提升

### 7.4 成本

- k × depth 次 LLM 调用
- 可能比 ReAct 贵 10-50 倍
- 有回报场景才值（数学/代码/博弈）

### 7.5 代表

- [官方实现](https://github.com/princeton-nlp/tree-of-thought-llm)
- LangGraph 可实现（状态图 + 分支）

---

## 8. Language Agent Tree Search (LATS)

### 8.1 核心思想

**ToT + 蒙特卡洛树搜索 (MCTS)**：

```
UCB 公式选最有希望的分支:
  argmax(exploit + exploration_bonus)

- 展开
- 评估
- 回传 reward
- 更新树统计

多轮后选择平均 reward 最高的路径
```

### 8.2 特色

- 平衡探索与利用
- 能回溯重走
- 理论上最优（给足预算）

### 8.3 成本

- 单任务可能数百次 LLM 调用
- 适合**高价值一次性任务**（不适合实时对话）

### 8.4 代表实现

LangGraph Agent Search 社区实现、LATS 论文官方代码。

---

## 9. Reasoning via Planning (RAP)

### 9.1 核心思想

**LLM 作为世界模型 + MCTS**：

```
LLM 预测:
  "如果我做 action A，状态变成 S'"
  "S' 的价值是 V"

MCTS 在 LLM 模拟的"想象世界"里搜索，
找到最优动作序列，才真正执行。
```

### 9.2 vs LATS

| 维度 | LATS | RAP |
|------|------|-----|
| 信号 | LLM self-evaluate | LLM world model |
| 搜索空间 | Thought | Action |
| 适用 | 推理 | 规划/控制 |

### 9.3 适用

- 机器人规划
- 游戏 AI
- 长程策略（如医疗 / 物流）

---

## 10. 其他推理算法

### 10.1 Chain-of-Thought (CoT)

基础：让 LLM 先 reason 再 answer。今天几乎所有算法都内含 CoT。

### 10.2 Self-Consistency

同一 Prompt 多次采样，投票出最常见答案。便宜有效。

### 10.3 Graph-of-Thought (GoT)

ToT 的推广，节点可复用（不只是树），表达共享子问题。

### 10.4 Reflexion + ToT = R-ToT

组合：每分支失败可反思，再重启搜索。

### 10.5 Reasoning Tokens（o1 / DeepSeek R1）

2024 新范式：模型内部原生"长思考"：

```
<think>
...长篇自我推理...
</think>
Final answer: ...
```

- LLM 原生支持 ToT 风格推理
- 不需要外部框架
- Token 贵但效果强

### 10.6 Best-of-N + Reward Model

生成 N 个候选，用 reward model 选最好的。思路简单、有效。

---

## 11. 横向对比

| 算法 | 探索 | 反思 | 成本 | 典型场景 |
|------|------|------|------|---------|
| ReAct | ❌ | ❌ | 1x | 通用 |
| Plan-Execute | ⚠️ 一次规划 | ⚠️ | 1-2x | 多步任务 |
| Reflexion | ❌ | ✅ | 2-5x | 可验证任务 |
| Self-Refine | ❌ | ✅ | 2-4x | 写作/摘要 |
| Self-Consistency | ✅ 采样 | ❌ | 5-10x | 短问答 |
| ToT | ✅ | ❌ | 10-50x | 数学/推理 |
| LATS | ✅ MCTS | ✅ | 50-500x | 高价值任务 |
| RAP | ✅ MCTS | ✅ 世界模型 | 50-500x | 规划/控制 |
| CoT内置（o1） | ✅ 内部 | ✅ 内部 | 5-20x | 通用推理 |

---

## 12. 选型决策树

```
任务类型？
│
├─ 简单 Q&A / 工具调用 ──► ReAct
│
├─ 多步明确任务 ──► Plan-Execute
│
├─ 有明确 reward（测试/验证） ──► Reflexion
│
├─ 需要高质量输出（写作/代码） ──► Self-Refine
│
├─ 数学/博弈/组合优化 ──► ToT 或 LATS
│
├─ 规划/控制任务 ──► RAP
│
└─ 预算紧 + 提升空间大 ──► Self-Consistency
```

---

## 13. 组合策略

算法不是互斥：

```
Outer: Plan-Execute
  │
  └─ Step 1: ReAct
     Step 2: ToT（因为是数学子问题）
     Step 3: Reflexion（因为有 unit test）
     Step 4: ReAct
```

**Dawning 的思路**：每个 Step 可以独立选算法。

---

## 14. Dawning 算法可插拔设计

### 14.1 IReasoningStrategy

```csharp
public interface IReasoningStrategy
{
    string Name { get; }
    Task<ReasoningResult> RunAsync(
        ReasoningContext ctx,
        CancellationToken ct);
}

public record ReasoningContext(
    AgentDefinition Agent,
    ConversationHistory History,
    IToolRegistry Tools,
    ILLMProvider LLM,
    ScopeContext Scope);
```

### 14.2 内置策略

```csharp
services.AddAgentOSKernel()
    .AddReasoningStrategies(r =>
    {
        r.AddReAct();
        r.AddPlanExecute();
        r.AddReflexion();
        r.AddSelfRefine();
        r.AddSelfConsistency(n: 5);
        r.AddTreeOfThought(branchFactor: 3, depth: 4);
        r.AddLATS(simulations: 16);
    });

// Agent 声明用哪个
services.AddAgent("math-solver", a =>
{
    a.Strategy = "tree-of-thought";
    a.StrategyOptions = new { BranchFactor = 5 };
});
```

### 14.3 策略路由

根据任务类型动态选算法：

```csharp
public interface IStrategyRouter
{
    Task<string> PickAsync(string taskDescription, ScopeContext scope);
}
// 实现可基于规则、embedding 或小 LLM
```

### 14.4 成本/质量权衡开关

```csharp
public class StrategyOptions
{
    public int MaxSteps { get; set; } = 20;
    public int BranchFactor { get; set; } = 3;
    public int MaxSimulations { get; set; } = 10;
    public decimal MaxCostPerTask { get; set; } = 0.50m;
    public TimeSpan MaxWallClock { get; set; } = TimeSpan.FromMinutes(2);
}
```

---

## 15. 工程实践

### 15.1 监控关键指标

- 平均步数 / 分支数
- 成功率（按 strategy）
- Cost / task（按 strategy）
- Time / task（按 strategy）
- 反思命中率（Reflexion 场景）

### 15.2 A/B 测试

- Canary 5% 流量切换到 ToT
- 对比成功率 + 成本
- 自动回滚或推广

### 15.3 评估驱动

- 每个策略有专用 dataset
- CI 跑完整 eval 套件
- Regression 门禁：新版策略不能降

### 15.4 混合流量

```
Tier A（付费用户，复杂任务）→ LATS
Tier B（免费用户）          → ReAct
未知复杂度                 → Self-Consistency
```

---

## 16. 与 o1/R1 类模型关系

**观察**：2024-2025 的 o1 / Claude 3.7 thinking / DeepSeek R1 把推理搬进了模型内部：

```
过去：框架层做 ToT/Reflexion
现在：模型内部原生 thinking
```

**不意味着算法过时**：
- o1 模型贵 5-10x
- 外部算法可**在 o1 基础上再加**（框架层 Plan-Execute + 模型内 CoT）
- 很多场景小模型 + 外部算法 > 大模型单独

---

## 17. 常见坑

| 坑 | 说明 |
|----|------|
| 盲上 ToT/LATS | 成本爆炸，多数场景 ReAct 够 |
| Reflexion 没验证 | Evaluator 不可信时反思全是噪声 |
| Plan 永不 replan | 环境变化后 Plan 失效 |
| 搜索没上限 | MCTS 跑满预算才停 |
| 算法切换不记录 | 无法追溯为何慢/贵 |
| 策略选择纯 LLM | LLM 会倾向选贵的"显得智能" |
| 不测性价比 | 复杂策略的边际收益 < 成本差 |

---

## 18. 小结

> **没有万能算法，只有合适的算法。**
>
> ReAct 是默认，Plan-Execute 是结构化，Reflexion 是自进化，ToT/LATS 是搜索极致，RAP 是世界模型。
> Dawning 把算法做成可插拔策略，业务只声明"我这个 Agent 用什么"，框架负责编排与可观测。

---

## 19. 延伸阅读

- [[concepts/agent-loop.md]] — 最基础的 Loop 结构
- [[concepts/prompt-engineering-dspy.zh-CN]] — Prompt 层优化
- [[concepts/multi-agent-patterns.zh-CN]] — 多 Agent 也是一种搜索
- ReAct 论文：<https://arxiv.org/abs/2210.03629>
- Plan-and-Solve：<https://arxiv.org/abs/2305.04091>
- Reflexion：<https://arxiv.org/abs/2303.11366>
- Tree of Thoughts：<https://arxiv.org/abs/2305.10601>
- LATS：<https://arxiv.org/abs/2310.04406>
- RAP：<https://arxiv.org/abs/2305.14992>
