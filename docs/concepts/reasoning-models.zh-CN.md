---
title: "推理模型（Reasoning Models）：o1/o3/R1/QwQ/Gemini Thinking 与 Agent 的融合"
type: concept
tags: [reasoning-models, o1, o3, deepseek-r1, qwq, gemini-thinking, chain-of-thought, inference-compute]
sources: [concepts/reasoning-algorithms.zh-CN.md, concepts/agent-loop.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 推理模型（Reasoning Models）：o1/o3/R1/QwQ/Gemini Thinking 与 Agent 的融合

> 2024.09 OpenAI o1 发布，2025.01 DeepSeek R1 开源，颠覆了"LLM = next token predictor"的朴素认知。
> 推理模型把"思考"移到模型内部——这对传统 ReAct / Plan-Execute 的 Agent 架构是**根本挑战**。
>
> 本文梳理推理模型的原理、代表、与 Agent 编排的冲突与融合，以及 Dawning 的适配策略。

---

## 1. 什么是推理模型

### 1.1 本质区别

| 维度 | 标准 LLM | 推理模型 |
|------|---------|---------|
| 训练 | SFT + RLHF | + 大规模 RL on reasoning |
| 生成 | 直接出答案 | 先"思考"再出答案 |
| Token 结构 | answer | `<think>...</think>` + answer |
| Test-time compute | 固定 | **按需变长** |
| 擅长 | 生成、对话 | 数学、代码、多步推理 |
| 弱项 | 复杂推理 | 创意写作、闲聊偶尔怪异 |

### 1.2 什么是"思考"

模型内部先产生数千到数万 token 的"内部独白"：

```
<think>
让我一步步想这个问题。
先识别关键变量...
假设 x = ...
验证：如果 x = 5，那么...不对，因为...
再试 x = 7...
嗯，这个方法不对，换一种...
</think>

答案：x = 11
```

这段"思考"是**模型学会的行为**，不是外部 prompt 教的。

### 1.3 核心原理

**RL with verifiable rewards (RLVR)**：

```
Task (有明确答案，可验证)
  ↓
Model 生成长思考 + 答案
  ↓
验证器打分 (正确/错误)
  ↓
RL 更新（PPO / GRPO / DPO 系）
  ↓
模型学会"如何思考"（不是模仿，是探索）
```

关键：
- **可验证任务**（数学 / 代码 / 逻辑）
- **长链生成**（允许数千 token 思考）
- **多次 rollout + 奖励**

---

## 2. 主要产品（2026）

### 2.1 闭源

| 模型 | 出品 | 发布 | 定位 |
|------|------|------|------|
| **o1 / o1-preview** | OpenAI | 2024.09 | 推理模型开创 |
| **o1 (final)** | OpenAI | 2024.12 | 可调推理量 |
| **o3 / o3-mini** | OpenAI | 2025.01-04 | 下一代 |
| **o4** | OpenAI | 2025 Q3（传闻） | — |
| **Claude 3.7 Sonnet Thinking** | Anthropic | 2025.02 | extended thinking 模式 |
| **Claude 4 / Opus 4 Thinking** | Anthropic | 2025 | |
| **Gemini 2.0 Flash Thinking** | Google | 2024.12 | |
| **Gemini 2.5 Pro Thinking** | Google | 2025 | |
| **Grok 3 Think** | xAI | 2025 | |

### 2.2 开源（爆发）

| 模型 | 出品 | 发布 | 说明 |
|------|------|------|------|
| **DeepSeek R1** | DeepSeek | 2025.01 | 开源王者，开源界标 |
| **DeepSeek R1-Zero** | DeepSeek | 2025.01 | 纯 RL 无 SFT |
| **QwQ-32B** | 阿里 Qwen | 2024.11 | 首批开源推理 |
| **Qwen3-Reasoning** | 阿里 | 2025 | Qwen3 系列 |
| **s1** | 斯坦福 | 2025.02 | 仅 1K 样本复现 |
| **Sky-T1** | Berkeley | 2025.01 | 低成本复现 |
| **OpenThoughts** | 学术 | 2025 | 开放数据 |
| **Marco-o1** | 阿里 | 2024.11 | 早期复现 |
| **Phi-4-Reasoning** | Microsoft | 2025 | 小模型推理 |

### 2.3 代表性基准表现

| 基准 | GPT-4o | o1 | o3 | DeepSeek R1 | Claude 3.7 Thinking |
|------|--------|-----|-----|-------------|-------------------|
| AIME 2024 | 13% | 74% | 87% | 80% | 70% |
| GPQA | 50% | 76% | 88% | 72% | 80% |
| SWE-Bench Verified | 35% | 42% | 72% | 50% | 62% |
| Codeforces Elo | — | 1900 | 2700+ | 2000 | 2100 |

---

## 3. DeepSeek R1 深度剖析

### 3.1 为什么重要

- **性能对标 o1**
- **完全开源（MIT 权重）**
- **论文公开方法**
- **成本约 OpenAI 1/50**

### 3.2 训练 Pipeline

```
Step 1: DeepSeek-V3-Base
         ↓ 
Step 2: Cold-start SFT (少量长思考样本)
         ↓
Step 3: Reasoning RL (大规模 RLVR)
         ├─ 数学题
         ├─ 代码题
         └─ 逻辑题
         ↓
Step 4: Rejection Sampling + SFT (扩展到通用)
         ↓
Step 5: Full RL (含 human preference)
         ↓
DeepSeek R1
```

### 3.3 R1-Zero 的启示

- **完全跳过 SFT**
- **纯 RL from base**
- 仍然涌现 reasoning
- 证明 **RL 而非 SFT 是关键**

### 3.4 蒸馏

- R1 → 蒸馏到 Qwen / Llama 小模型
- DeepSeek-R1-Distill-Qwen-7B 达到 GPT-4o 某些基准水平

---

## 4. 推理模型的成本 / 延迟特点

### 4.1 "推理税"

```
同一个问题：
GPT-4o:        answer  500 tokens  → $0.005, 2s
o1:            thought 5K + answer → $0.3,   20s
o1-pro:        thought 50K        → $3,     2min
```

### 4.2 Test-time Compute Scaling

**新的 scaling law**（Ilya / OpenAI 提出）：

```
性能 ~ log(test-time compute)
```

- 允许模型"多想"换更好答案
- 与 train-time scaling 独立维度

### 4.3 User-controllable

```
OpenAI: reasoning_effort = "low" | "medium" | "high"
Claude: thinking_budget_tokens (e.g. 16K)
Gemini: thinking_config.thinking_budget
DeepSeek: 通过 prompt 控制（不标准化）
```

### 4.4 成本对比（2026 参考）

| 模型 | Input $/M | Output $/M | 思考计费 |
|------|-----------|-----------|---------|
| GPT-4o-mini | 0.15 | 0.60 | — |
| GPT-4o | 2.50 | 10 | — |
| o1-mini | 3 | 12 | ✅ 全计费 |
| o1 | 15 | 60 | ✅ 全计费 |
| o3 | 数倍 | 数倍 | ✅ |
| DeepSeek R1 | 0.55 | 2.19 | ✅（便宜） |
| Claude 3.7 Thinking | 3 | 15 | ✅ |

---

## 5. 推理模型 vs 传统 Agent Loop 的冲突

### 5.1 矛盾

传统 ReAct Agent：
```
Thought: ...
Action: tool_call
Observation: ...
Thought: ...
Action: ...
```

推理模型：
```
<think>
Thought 1...
Thought 2...
... (5000 tokens)
</think>
Answer
```

**矛盾点**：
- 推理模型把 "Thought" 内化了
- 传统 ReAct 把 Thought 外化以方便调工具
- **两者不能简单叠加**

### 5.2 三种融合范式

#### 范式 A：推理模型作为"终局答题者"

```
Agent Loop (便宜模型)
  ├─ 检索
  ├─ 工具调用
  └─ 收集足够 context
         ↓
推理模型（只在关键决策时调用）
  → 最终答案
```

**优劣**：
- ✅ 成本合理
- ✅ 工具链清晰
- ❌ 没完全发挥推理模型

#### 范式 B：推理模型原生 tool use

- o1 / o3 / Claude Thinking 2025+ 都支持
- 模型内部思考 + 工具调用交织
- 不需要外部 ReAct

```
Input
  ↓
Reasoning Model
  <think>
  我需要查一下...
  tool_call(search, "...")
  </think>
  
  [tool_result]
  
  <think>
  好，结果表明...
  </think>
  
  Answer
```

**挑战**：
- Thinking token 是否返回？（OpenAI 不返回，Claude 部分返回）
- Trace 如何观测思考？
- 思考可能调用工具多轮

#### 范式 C：推理模型 + Agentic 管弦

```
Orchestrator Agent (普通模型)
  ├── 路由
  ├── Sub-agent: Planner (推理模型)
  ├── Sub-agent: Executor (便宜模型)
  └── Sub-agent: Critic (推理模型)
```

**关键**：推理模型只在**难决策**用，不在全链用。

### 5.3 Dawning 选择

Dawning Layer 1 的 Agent Loop 支持三种范式：

```csharp
public enum ReasoningMode
{
    Legacy,       // 普通模型 + ReAct
    Integrated,   // 推理模型原生 tool use
    Hybrid        // 推理模型作为 critic/planner，普通模型执行
}
```

---

## 6. 工程落地要点

### 6.1 选型决策树

```
任务需要多步推理 / 验证？
  No  → 普通模型
  Yes →
      是否 latency-critical (<2s)？
        Yes → 普通模型 + 外部 ReAct
        No  →
            是否 cost-critical？
              Yes → 开源 R1 / QwQ / 蒸馏小模型
              No  → o1 / o3 / Claude Thinking
```

### 6.2 Prompt 设计差异

**普通模型**：
- Few-shot 有效
- Chain-of-thought 显式加
- 结构化输出模板

**推理模型**：
- **少 few-shot**（会干扰内部思考）
- **不加 CoT 指令**（已内化）
- 只给任务描述 + 约束
- 结构化输出仍有效但放尾部

```
# 推理模型的 prompt
你是一位数学老师。请解答以下问题。

问题：...

要求：
- 答案用 \boxed{} 包裹
- 如果需要多步，请分步
```

### 6.3 流式输出挑战

- 思考可能数万 token，用户等待痛苦
- 策略：
  - 显示"正在深度思考..."
  - 显示已 elapsed 时间
  - 允许取消
  - 展示中间摘要（Claude 的 Extended Thinking）

### 6.4 成本控制

- `reasoning_effort=low` 先试
- 失败后 fallback 到 `high`
- 缓存（相同问题）
- 预算硬上限（ICostBudget）

---

## 7. 思考链（Thinking Trace）的可见性

### 7.1 各家策略

| 厂商 | Thinking 是否可见 |
|------|-----------------|
| OpenAI o1/o3 | ❌ 不返回完整 thinking（摘要） |
| Anthropic Claude Thinking | ✅ 返回（extended_thinking block） |
| Google Gemini Thinking | ⚠️ 可选开启 |
| DeepSeek R1 | ✅ 返回 |
| QwQ / Qwen | ✅ 返回 |

### 7.2 为什么 OpenAI 不返回

- 商业保护（竞争对手蒸馏）
- 思考链可能有"内部人格"不适合展示
- 仍然收费（不返回也按输出 token 算）

### 7.3 对观测的影响

- 无法 trace 推理路径（OpenAI）
- 调试困难
- 合规审计需求冲突（监管要解释）

### 7.4 Dawning 做法

```
GenAI SemConv 扩展：
  gen_ai.reasoning.input_tokens
  gen_ai.reasoning.output_tokens
  gen_ai.reasoning.trace (可选，按支持)
```

不能返回的厂商，记录 token 数与 latency。

---

## 8. 推理模型不擅长的地方

### 8.1 开放式生成

- 创意写作
- 闲聊
- 角色扮演

倾向"过度分析"。

### 8.2 低延迟需求

- 语音 Agent（需 <500ms 首响）
- UI 实时补全

### 8.3 成本敏感场景

- 大规模 batch
- 日常 chatbot

### 8.4 简单指令

- "总结这段"
- "翻译成英文"

用推理模型是杀鸡用牛刀。

---

## 9. 推理模型与工具调用

### 9.1 o1 Tool Use（2025 正式）

- 支持 function calling
- 思考过程可交织工具调用
- 并行工具调用

### 9.2 Claude Extended Thinking + Tools

- 思考段中可规划工具调用
- 一次 request 多轮工具
- 返回 thinking + tool_uses + response

### 9.3 DeepSeek R1 + Tools

- R1 原版工具调用较弱
- 需要 prompt 引导
- 社区有 fine-tune 版（R1-Tool）

### 9.4 路由策略

```
简单工具任务 → GPT-4o-mini + ReAct
复杂推理工具 → o3 / Claude Thinking + native tools
规划/决策 → o1 作为 Planner，4o-mini 作为 Executor
```

---

## 10. 推理模型 + RAG

### 10.1 挑战

- 推理模型长思考 + 长检索 context = token 爆炸
- 可能在思考中重复检索

### 10.2 策略

```
Agent (便宜模型) 先检索过滤
  ↓
推理模型接收浓缩 context
  ↓
做深度推理
```

而不是：

```
推理模型 ← 直接给 50K context（烧钱）
```

### 10.3 Hybrid RAG

- 先跑 basic RAG + rerank（便宜）
- 召回 top-5 给推理模型
- 利用推理模型的"验证"能力筛 hallucination

---

## 11. 多 Agent 中的推理模型

### 11.1 角色分工

| Agent | 推荐模型 |
|-------|---------|
| Orchestrator | 普通（4o / Sonnet） |
| Planner | 推理（o1 / R1） |
| Executor | 普通（便宜） |
| Critic | 推理（验证能力强） |
| Summarizer | 普通 |

### 11.2 例：代码修 bug

```
Orchestrator: "修 bug X"
  ↓
Planner (o3): 分析 → plan
  ↓
Executor (4o-mini): 按 plan 改代码
  ↓
Critic (o3): review 代码，指出问题
  ↓
Orchestrator: 综合决定是否通过
```

---

## 12. 微调推理模型

### 12.1 能否 fine-tune

- OpenAI o1 / o3：**不开放 fine-tune**（2026 初）
- Claude Thinking：**未开放**
- **DeepSeek R1 / QwQ**：✅ 开源，可 fine-tune

### 12.2 方法

- **SFT**：用长思考数据微调（慎重，可能损伤推理）
- **RL from R1**：继续 RLVR
- **蒸馏**：R1 → 小模型

### 12.3 挑战

- 长思考样本稀缺
- RL 代价高（GPU 集群）
- 防止 mode collapse

### 12.4 替代

如果不能 fine-tune：
- Prompt engineering
- RAG 外挂知识
- Tool + 推理模型组合

---

## 13. 开源生态工具链

### 13.1 训练

- **OpenRLHF / TRL**：RL 训练框架
- **verl**（字节）：专门 RLVR
- **OpenR** / **OpenRLHF-Reasoning**：推理 RL
- **simpleRL**：极简复现

### 13.2 数据

- **OpenThoughts** (Berkeley)
- **Bespoke-Stratos**
- **Hermes 3** SFT
- **Open Reasoning Dataset** (various)

### 13.3 推理部署

- **vLLM** / **SGLang**：支持 thinking tokens
- **llama.cpp**：本地推理 R1 蒸馏版
- **LMDeploy**：性能优化

---

## 14. Dawning 适配策略

### 14.1 Layer 0 ILLMProvider 扩展

```csharp
public interface IReasoningProvider : ILLMProvider
{
    Task<ReasoningResponse> ChatWithReasoningAsync(
        ChatRequest request,
        ReasoningOptions options,  // effort, budget_tokens
        CancellationToken ct);
}

public record ReasoningResponse(
    string Content,
    string? ThinkingTrace,      // 可能为 null
    int ReasoningTokens,
    int OutputTokens);
```

### 14.2 Layer 1 Agent Loop 支持

```csharp
public class AgentOptions
{
    public ReasoningMode ReasoningMode { get; set; }
    public int? ReasoningBudgetTokens { get; set; }
    public string? ReasoningEffort { get; set; }
}
```

### 14.3 Layer 6 Observability

- 记录 reasoning token 成本
- trace thinking（若可获得）
- 延迟分解（思考 vs 输出）

### 14.4 Layer 7 成本治理

```
IReasoningBudget:
  - max_reasoning_tokens_per_request
  - max_reasoning_tokens_per_user_per_day
  - fallback to non-reasoning when exceeded
```

### 14.5 推荐模式库

```
Dawning.Agents.Patterns.ReasoningPlanExecute
Dawning.Agents.Patterns.ReasoningCritic
Dawning.Agents.Patterns.ReasoningOnDemand  (router)
```

---

## 15. 未来趋势

### 15.1 2026-2027 可预期

- **推理模型成为默认**（像今天 chat 模型一样）
- **推理 + 多模态融合**（Gemini 3 Thinking with vision）
- **Inference-time search**：MCTS-like 外化搜索
- **自主 reasoning depth**：模型学会判断何时深思
- **Smaller + Faster**：Phi-5-Reasoning / Gemma-3-Thinking
- **专用硬件**：针对 long context reasoning 优化

### 15.2 对 Agent 框架的影响

- ReAct 依然有价值（不是所有任务需要推理模型）
- Plan/Critic 角色更适合推理模型
- 长链 Agent 可能被"推理模型原生"替代
- 框架重心从"how to chain"转向"how to route + observe"

---

## 16. 小结

> 推理模型不是"更强的 LLM"，是**新一类计算资源**。
> 像 GPU vs CPU 一样：不是替代，而是**选择性使用**。
>
> Agent 框架的任务不再是"帮模型思考"（它自己会），
> 而是**让模型用对场景、控成本、可观测、可治理**。
>
> Dawning 的选择：Layer 0 抽象三种模式（Legacy / Integrated / Hybrid），
> Layer 1 Agent Loop 不强耦合 ReAct，
> Layer 6/7 让推理模型的成本与 trace 可管理。

---

## 17. 延伸阅读

- [[concepts/reasoning-algorithms.zh-CN]] — 外部推理算法（ReAct 等）
- [[concepts/agent-loop.md]] — Agent Loop 设计
- [[concepts/cost-optimization.zh-CN]] — 推理模型成本
- [[concepts/observability-deep.zh-CN]] — thinking trace
- DeepSeek R1 论文: <https://arxiv.org/abs/2501.12948>
- OpenAI o1 System Card: <https://openai.com/index/openai-o1-system-card/>
- s1: Simple test-time scaling: <https://arxiv.org/abs/2501.19393>
- Anthropic Extended Thinking: <https://www.anthropic.com/news/visible-extended-thinking>
- Gemini Thinking: <https://ai.google.dev/gemini-api/docs/thinking>
- verl: <https://github.com/volcengine/verl>
- OpenThoughts: <https://github.com/open-thoughts/open-thoughts>
