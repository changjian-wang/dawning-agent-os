---
title: "Inference-Time Search 与测试时计算：Best-of-N、MCTS、Self-Consistency、Reasoning Search"
type: concept
tags: [inference-time-compute, search, mcts, best-of-n, self-consistency, reward-model, reasoning, test-time-scaling]
sources: [concepts/reasoning-models.zh-CN.md, concepts/reasoning-algorithms.zh-CN.md]
created: 2026-04-18
updated: 2026-04-18
status: active
---

# Inference-Time Search 与测试时计算：Best-of-N、MCTS、Self-Consistency、Reasoning Search

> 2024-2025 最重要的 LLM 进展不是更大的模型，是**用更多推理时计算换质量**：
> - OpenAI o1/o3：内嵌长链 reasoning
> - DeepSeek R1：GRPO 训练 + 推理搜索
> - Gemini Thinking：思考链
> - AlphaGeometry / AlphaProof：MCTS + LLM
> - Self-Consistency、Best-of-N、Verifier、Process Reward Model
>
> 核心思想：**训练到瓶颈，就在推理时多花计算**。
> 本文系统梳理 Inference-Time Search 方法族、Agent 中的应用、Dawning 抽象。

---

## 1. Test-Time Compute Scaling 概念

### 1.1 三类 scaling

```
1. Pre-training scaling (2020-2023):
   更大模型 + 更多数据 → 更强能力

2. Post-training scaling (2023-2024):
   RLHF / DPO / RLVR → 对齐 + 行为

3. Inference-time scaling (2024-):
   推理时花更多计算 → 更好答案
```

### 1.2 OpenAI o1 曲线

- 推理时花更多 token（thinking）→ 分数单调上升
- 在 AIME / Codeforces / GPQA 上远超 GPT-4o
- 代价：token 成本 10-100x

### 1.3 意义

- 小模型 + 大搜索 可能 > 大模型 + 无搜索
- 新的成本-质量权衡维度
- Agent 可动态决定计算量

---

## 2. 核心方法族

### 2.1 方法全景

```
Inference-Time Compute
├── Sequential (链式加深)
│   ├── Chain-of-Thought
│   ├── Reasoning Model (o1/R1 internal CoT)
│   ├── Self-Reflection / Self-Refine
│   └── ReAct loops
├── Parallel (并行采样)
│   ├── Self-Consistency (多 sample + majority vote)
│   ├── Best-of-N (多 sample + verifier 选)
│   └── Beam Search (受控扩展)
└── Tree / Search
    ├── Tree-of-Thought
    ├── MCTS + LLM (AlphaGeometry / ToT)
    ├── Graph-of-Thought
    └── Forest-of-Thought
```

---

## 3. Chain-of-Thought (CoT)

### 3.1 基础

- prompt "think step by step"
- 模型输出中间推理

### 3.2 变体

- Zero-shot CoT
- Few-shot CoT（示例）
- Auto-CoT（自动生成示例）
- Structured CoT（JSON / XML）

### 3.3 局限

- 容易 wander
- 无验证
- 长 CoT 易幻觉

---

## 4. Self-Consistency

### 4.1 流程

```
问题 → 温度采样 N 次 → 得 N 个答案 → 投票 / 聚合
```

### 4.2 优点

- 极简单
- 质量显著提升（+5-15%）

### 4.3 缺点

- 成本 Nx
- 只对"有正确答案"类问题有效（数学、分类）
- 对开放生成无效

### 4.4 改进

- **加权 voting**（confidence 加权）
- **选择性**（分歧大时才多采）

---

## 5. Best-of-N + Verifier

### 5.1 流程

```
问题 → 采样 N 个候选 → Verifier 打分 → 选最优
```

### 5.2 Verifier 类型

| 类型 | 描述 |
|------|------|
| **ORM** (Outcome Reward Model) | 打最终答案 |
| **PRM** (Process Reward Model) | 打每一步 |
| **Code Runner** | 代码跑测试 |
| **Symbolic Checker** | 数学证明器 |
| **LLM-as-Judge** | 另一 LLM 评分 |

### 5.3 PRM 的价值

逐步打分 → 早期截断错误分支 → 计算省

DeepMind Let's Verify Step by Step（2023）：PRM > ORM。

### 5.4 成本

- N 通常 4-32
- 有 early stopping

---

## 6. Beam Search

### 6.1 传统 beam search

- 每步保留 top-K beams
- 受控扩展

### 6.2 应用于推理

- 每步评分（logits / PRM）
- 保留 top beams
- 扩展直到终止

### 6.3 对比

- vs Best-of-N：更结构化但多样性弱
- vs MCTS：简单但不探索

---

## 7. Tree-of-Thought (ToT)

### 7.1 核心

```
         问题
        /  |  \
     状态1  状态2  状态3      ← 探索多路径
    /  \       /  \
   ...  ...  ...  ...
```

### 7.2 流程

- Thought 分解
- 各 thought 评估（LLM 判断可行性）
- 扩展高潜力分支
- BFS / DFS / beam

### 7.3 适用

- 24-点、creative writing、puzzle

### 7.4 代价

- 计算膨胀
- 需要好的 state evaluator

---

## 8. MCTS + LLM

### 8.1 MCTS 经典（AlphaGo）

```
Selection (UCT) → Expansion → Simulation → Backpropagation
```

### 8.2 LLM 版本

- Selection 由 LLM + 分数选择
- Expansion LLM 生成动作
- Simulation LLM 快速 rollout 或 value model
- Backprop 更新 Q

### 8.3 代表

- **AlphaCode 2**：代码生成 + MCTS
- **AlphaGeometry**：几何证明
- **AlphaProof**：数学证明
- **ToT with MCTS**
- **Llemma / Lean 相关**

### 8.4 优势

- 理论保证
- 平衡探索-利用

### 8.5 挑战

- 实现复杂
- LLM call 多 → 贵
- Value function 难设计（开放任务）

---

## 9. Self-Refine / Self-Reflection

### 9.1 基础

```
初次答 → 自评 → 找问题 → 修正 → 再自评 → ...
```

### 9.2 变体

- **Reflexion**：基于失败的反思累积
- **Self-Refine**：迭代改进
- **CRITIC**：调用工具验证
- **Debate**（多 Agent 互驳）

### 9.3 问题

- LLM 自评不可靠（可能不发现错误）
- 循环可能更差（过校正）
- 需要 external signal（工具 / 测试）

---

## 10. Reasoning Model (o1/R1 风)

### 10.1 训练方式

- 大规模 reasoning SFT
- RLVR（可验证奖励）
- GRPO 优化 policy

### 10.2 推理特征

- 长 hidden CoT（o1）或 visible CoT（R1）
- 自修正
- 回溯

### 10.3 与其他方法关系

- 相当于把"搜索"蒸馏进模型权重
- 用户不必再外层做 Best-of-N
- 但叠加 Best-of-N + Self-Consistency 仍能提升

### 10.4 两种 reasoning 成本

| 方式 | 成本 | 可控性 |
|------|------|-------|
| Internal (o1) | token 多 | 不可控步数 |
| External search | N 次 inference | 可控预算 |

---

## 11. Process Reward Model (PRM)

### 11.1 训练

- 收集多步推理过程
- 标注每步 correct / wrong / neutral
- 训练分类器 / regressor

### 11.2 用法

- Best-of-N 中逐步打分
- 剪枝明显错误分支
- 指导 MCTS selection

### 11.3 数据难点

- 标注昂贵
- 自动化：让 solver 生成，成败回传步级标注

### 11.4 代表

- OpenAI PRM800K
- Let's Verify Step-by-Step
- DeepSeek Math shepherd

---

## 12. 搜索的成本控制

### 12.1 预算概念

每个请求的推理预算：
- Token budget
- Time budget
- Dollar budget
- Step budget

### 12.2 Budget-Forcing

- 超预算直接 cut
- o1 允许用户设 reasoning effort (low / medium / high)

### 12.3 Adaptive Compute

- 简单问题少搜
- 难题多搜
- Difficulty Classifier 决定

### 12.4 Router

- 简单 → 小模型
- 中等 → 中模型
- 难 → 大模型 + 搜索

---

## 13. Agent 中的 Inference-Time Search

### 13.1 ReAct + 搜索

- ReAct 本身是顺序搜索
- 可以加并行探索

### 13.2 Tool Call Ranking

多个候选 tool 调用：
- Best-of-N 生成多候选
- Verifier 评估（policy / cost / risk）
- 选最佳

### 13.3 Plan 验证

- 生成多个 plan
- PRM 打分
- 最优执行

### 13.4 Multi-Agent Debate

- Agent A 方案
- Agent B 反驳
- Agent C 裁决

### 13.5 Verifier Agent

- 产出 Agent 执行后
- Verifier Agent 检查
- 不通过则重做

---

## 14. 工具执行回路中的验证

### 14.1 Code Execution

- 生成代码 → 执行 → 失败 → 修正 → 再执行
- AlphaCode 风
- 最硬的 verifier

### 14.2 Unit Test Generation

- 同时生成代码 + 测试
- 互相验证

### 14.3 Formal Verification

- 数学：Lean / Coq
- 代码：SMT solver
- 强保证但局限领域

---

## 15. 开源实现

| 项目 | 方法 | 语言 |
|------|------|------|
| **DSPy** | 优化 prompt + search | Python |
| **LiteLLM + Best-of-N wrapper** | Best-of-N | Python |
| **OpenR** | PRM + Search | Python |
| **LLM-MCTS** | MCTS + LLM | Python |
| **Graph-of-Thought** | GoT | Python |
| **TorchGen / DeepSearch** | 各种 | Python |
| **Qwen-QwQ / DeepSeek-R1** | 推理模型权重 | 开源 |

---

## 16. 成本-质量权衡

### 16.1 经验数据（数学题）

| 方法 | 成本倍数 | 质量提升 |
|------|---------|---------|
| 基线 | 1x | - |
| CoT | 1.2x | +10% |
| Self-Consistency (N=5) | 5x | +15% |
| Best-of-N + PRM (N=8) | 10x | +25% |
| MCTS (100 sims) | 100x | +35% |
| o1-mini 水平 reasoning | 20-50x | +40% |
| o1 / o3 | 50-200x | +50-60% |

### 16.2 业务考量

- 客服：不值（成本敏感）
- 法律/金融：值（准确度第一）
- 代码生成：值（测试可验证）
- 创作：Self-Consistency 无效（主观）

---

## 17. 局限与反模式

| 现象 | 原因 |
|------|------|
| Verifier 不准 → 选错 | Verifier 本身也是 LLM 或弱 |
| Search 收益递减 | N 越大边际越小 |
| 长 CoT 自毁 | 模型 lost in reasoning |
| MCTS 爆炸 | 无好 value function |
| 过拟合 benchmark | 真实分布表现差 |
| 延迟不可接受 | 用户等不起 |
| 成本不可控 | 没预算限制 |
| 幻觉持久 | 反思不发现 |

---

## 18. Dawning 中的抽象

### 18.1 IReasoningStrategy

```csharp
public interface IReasoningStrategy
{
    Task<ReasoningResult> RunAsync(
        ReasoningTask task,
        ReasoningBudget budget,
        CancellationToken ct);
}
```

实现：
- `ReActStrategy`
- `CoTStrategy`
- `SelfConsistencyStrategy`
- `BestOfNStrategy<TVerifier>`
- `TreeOfThoughtStrategy`
- `MctsStrategy`

### 18.2 IVerifier

```csharp
public interface IVerifier
{
    Task<VerifierScore> ScoreAsync(
        ReasoningStep step,
        CancellationToken ct);
}
```

实现：
- Code runner verifier
- LLM-as-Judge verifier
- Symbolic verifier
- Custom PRM verifier

### 18.3 ReasoningBudget

```csharp
public record ReasoningBudget(
    int? MaxTokens,
    TimeSpan? MaxDuration,
    decimal? MaxCostUsd,
    int? MaxSteps,
    ReasoningEffort Effort);
```

### 18.4 Adaptive Router

- 难度分类器 → 选 strategy
- budget 上限全局控制
- 监控回传真实成本 → 调整

### 18.5 可观测

- 每个 candidate + score 落 trace
- 搜索树可视化
- Cost attribution

### 18.6 不做什么

- 不自造 PRM（接 OpenAI / 自训）
- 不重写 reasoning 模型（接 o1/R1/QwQ）
- 不实现 MCTS 数值核心（接 OpenR 等）

---

## 19. 趋势（2026-2028）

- **Reasoning 模型主流化**：内嵌搜索成为标准
- **PRM 普及**：开源 PRM 家族爆发
- **Adaptive compute 产品化**：用户设"难度" / 自动判定
- **Formal + LLM 融合**：数学/代码领域大进展
- **搜索 + Agent 深度融合**：每个工具调用都经 verifier
- **成本治理成熟**：Reasoning budget 成标准 SLO

---

## 20. 小结

> Inference-Time Search 是推理模型之外**另一条正交的性能维度**。
> - 简单：Self-Consistency 立即可用
> - 进阶：Best-of-N + PRM 显著提质
> - 高阶：MCTS 在可验证领域接近最优
> - 终极：reasoning 模型 + external search 叠加
>
> Dawning 的位置：**IReasoningStrategy + IVerifier + ReasoningBudget 抽象**——
> 应用只关心"要不要更准 / 预算多少"，底层策略可替换、可观测、可治理。

---

## 21. 延伸阅读

- [[concepts/reasoning-models.zh-CN]] — o1/R1 推理模型
- [[concepts/reasoning-algorithms.zh-CN]] — 推理算法
- [[concepts/post-training.zh-CN]] — RLVR / GRPO
- [[concepts/cost-optimization.zh-CN]] — 成本治理
- [[concepts/agent-evaluation.zh-CN]] — 评估
- Let's Verify Step by Step: <https://arxiv.org/abs/2305.20050>
- Tree of Thoughts: <https://arxiv.org/abs/2305.10601>
- Self-Consistency: <https://arxiv.org/abs/2203.11171>
- Self-Refine: <https://arxiv.org/abs/2303.17651>
- Reflexion: <https://arxiv.org/abs/2303.11366>
- AlphaGeometry: <https://www.nature.com/articles/s41586-023-06747-5>
- OpenR: <https://github.com/openreasoner/openr>
- DeepSeek-Math Shepherd: <https://arxiv.org/abs/2312.08935>
- Scaling Test-Time Compute (Google): <https://arxiv.org/abs/2408.03314>
