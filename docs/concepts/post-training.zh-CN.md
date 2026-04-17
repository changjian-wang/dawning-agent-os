---
title: "LLM 后训练（Post-Training）：SFT、DPO、KTO、GRPO、LoRA、QLoRA、ReFT 与 Agent 特化"
type: concept
tags: [post-training, sft, dpo, kto, orpo, grpo, lora, qlora, reft, distillation, agent-finetune]
sources: [concepts/reasoning-models.zh-CN.md, concepts/dataset-building.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# LLM 后训练（Post-Training）：SFT、DPO、KTO、GRPO、LoRA、QLoRA、ReFT 与 Agent 特化

> 2024-2026 后训练（Post-Training）从论文走向工程，从 RLHF 一统天下走向 DPO/KTO/GRPO 多元化。
> 对 Agent 项目，"是否要微调"从奢侈品变成日常决策——R1 蒸馏小模型、Agent 工具特化、对齐企业风格——都需要后训练能力。
>
> 本文系统梳理后训练算法谱系、Agent 特化策略、Dawning 在哪里需要 / 不需要做。

---

## 1. 训练阶段全景

```
预训练 (Pretraining)
  ── Trillion tokens, 通用语言能力
  ── 极少团队做（OpenAI/Anthropic/Google/Meta/DeepSeek/Qwen/Mistral...）
  ↓
后训练 (Post-Training)
  ├── SFT (指令跟随)
  ├── 偏好对齐 (RLHF / DPO / KTO / IPO / ORPO)
  ├── 推理 RL (RLVR / GRPO / PPO)
  └── 工具 / Agent 特化
  ──> 大企业 / 团队都可做
  ↓
微调 (Domain Fine-tune)
  └── 业务特化 (LoRA / QLoRA 居多)
  ──> 几乎人人可做
```

---

## 2. SFT（监督微调）

### 2.1 原理

```
(Instruction, Response) pair → 最大似然 → 模型学复刻
```

### 2.2 用途

- 指令跟随
- 风格对齐
- 域知识灌输
- 蒸馏（教师 → 学生）
- 工具调用格式

### 2.3 数据规模

- 原版指令对齐：50K-1M
- 域微调：1K-50K
- 风格微调：100-1K
- 蒸馏：50K+

### 2.4 优势

- 简单、稳定、可控
- 数据质量好就好

### 2.5 局限

- 复制能力 ≠ 推理能力
- 容易 overfit
- 偏离预训练分布太远会失能

### 2.6 工具

- TRL (Hugging Face)
- Axolotl（社区最流行）
- LLaMA-Factory
- Unsloth（极速）
- torchtune (PyTorch 官方)

---

## 3. RLHF（人类反馈强化学习）

### 3.1 经典三步

```
Step 1: SFT (基础指令模型)
Step 2: Reward Model
        - 人标注 (chosen, rejected)
        - 训练 RM
Step 3: PPO
        - SFT 模型 vs RM
        - PPO 优化
```

### 3.2 现状

- **历史地位重要**（GPT-3.5 / GPT-4 都用）
- **2024-2025 被 DPO/KTO 替代**（更简单）
- 只在最大模型仍在用

### 3.3 复杂性

- 训 RM
- 维护 reference model
- KL penalty 调参
- GPU 内存 (4 个模型副本)
- 不稳定

---

## 4. DPO（Direct Preference Optimization）

### 4.1 原理

**绕过 RM**：直接从偏好对优化策略。

```
Loss = -log σ(β · log(π(chosen)/π_ref(chosen)) 
              - β · log(π(rejected)/π_ref(rejected)))
```

### 4.2 优势

- 不训 RM
- 更稳定
- 显存友好
- 实现简单

### 4.3 局限

- 仍需 reference model
- 偏好对偏离 SFT 分布过远效果差
- 易"reward hacking"

### 4.4 是 RLHF 的替代

2024 后开源界几乎默认 DPO 替 RLHF。

---

## 5. KTO（Kahneman-Tversky Optimization）

### 5.1 原理

不需要成对偏好，单标签即可：

```
(Response, Good/Bad)
```

基于 Kahneman-Tversky 损失函数。

### 5.2 优势

- 数据采集更便宜（不需要对比）
- 部分场景比 DPO 好
- 可处理 imbalanced 数据

### 5.3 适用

- 业务反馈是 thumbs up/down 而非配对
- 大量"好"样本 + 少量"差"样本

---

## 6. ORPO（Odds-Ratio Preference Optimization）

### 6.1 原理

**SFT + 偏好一步走**：把 SFT 损失和偏好损失合在一起，去掉 reference model。

### 6.2 优势

- 单阶段训练
- 不要 ref model（省显存一半）
- 简化 pipeline

### 6.3 是 DPO 的进化

2024 中后流行起来。Hugging Face TRL 默认支持。

---

## 7. IPO / SimPO / GPO 等变体

| 算法 | 改进 |
|------|------|
| IPO | 修复 DPO 的 length bias |
| SimPO | 不需要 reference model 的简化 |
| GPO | 通用偏好优化框架 |
| DPO+ | 各种 DPO 改进 |
| SLiC-HF | Sequence-Level 对比 |

**实战**：DPO 与 ORPO 仍是主流，其他作为研究备选。

---

## 8. RLVR（RL with Verifiable Rewards）

### 8.1 原理

任务有客观正确答案（数学 / 代码）：

```
Sample 多个 response → 验证 → 正确给 +1，错误给 -1 → RL
```

### 8.2 与 RLHF 区别

- 奖励来源：**程序验证**而非人 / RM
- 干净、可大规模

### 8.3 用途

- 推理模型核心训练（o1 / R1）
- 代码模型
- 数学模型

---

## 9. PPO vs GRPO

### 9.1 PPO

经典 RL 算法，需要 critic / value network。

```
Policy + Value (两模型) + KL penalty + Clip
```

### 9.2 GRPO（Group Relative Policy Optimization）

DeepSeek 提出，**去掉 critic**：

```
对同一问题生成 G 个 response
组内做 advantage normalize：
  advantage_i = (reward_i - mean(rewards)) / std(rewards)
PPO 风格更新（带 KL）
```

### 9.3 优势

- 显存省一半（无 critic）
- 简单
- 推理任务效果好

### 9.4 GRPO 的派生

- DAPO（DeepSeek-Math 后续）
- Dr. GRPO（修复 length bias）
- ReMax / RLOO（同类无 critic）

### 9.5 工程

- verl（字节）：开源 GRPO 训练框架
- OpenRLHF：综合 RL 训练
- TRL：现已支持 GRPO

---

## 10. 参数高效微调（PEFT）

### 10.1 LoRA

**Low-Rank Adaptation**：

```
冻结原权重 W
  W_eff = W + B·A   (A: r×k, B: d×r,  r << min(d,k))
仅训练 A, B
```

参数量降 100-1000x。

### 10.2 QLoRA

LoRA + 4-bit 量化：

```
原模型 4-bit 加载（7B 只占 ~4GB）
LoRA adapter 16-bit 训练
```

**意义**：消费级 GPU (24GB) 可微调 70B。

### 10.3 DoRA / VeRA / Galore

LoRA 的改进变体：
- DoRA：分解 magnitude + direction，效果更好
- VeRA：随机 frozen + 训 vector
- GaLore：梯度低秩投影，全参数 + 低显存

### 10.4 ReFT（Representation Fine-Tuning）

- 不动权重，**训"激活干预器"**
- 参数 100x 比 LoRA 少
- 部分场景持平
- 推理需 hook（部署稍复杂）

### 10.5 Prefix / Prompt Tuning

- 训 soft prompt
- 极少参数
- 效果通常不如 LoRA

### 10.6 选型

| 显存 | 模型大小 | 推荐 |
|------|---------|------|
| 24GB | < 13B | LoRA |
| 24GB | 13-70B | QLoRA |
| 80GB+ | 70B | QLoRA / LoRA |
| 80GB+ | 70B+ | Full FT 或 LoRA |
| 多机 | 任意 | DeepSpeed / FSDP + LoRA / Full |

---

## 11. Agent 特化训练

### 11.1 为什么需要

通用模型问题：
- Tool calling 格式偶尔出错
- 长 plan 漂移
- 工具选择次优
- 风格不统一

### 11.2 训练数据来源

```
1. 真实生产 trace
   - 成功的完整 trajectory
   - 失败 + 修正 trajectory
   - HITL approved 样本

2. 合成 trajectory
   - 大模型生成
   - 工具调用模拟
   - 用 GRPO 优化

3. 公开数据集
   - ToolBench (16K APIs)
   - ToolLLM
   - APIBench
   - BFCL (Berkeley Function Calling Leaderboard)
   - Agent Instruct
   - Glaive Function Calling
```

### 11.3 Agent SFT

```
Format:
[
  {role: "system", content: "你是一个工具助手, 工具列表: ..."},
  {role: "user", content: "..."},
  {role: "assistant", content: null, tool_calls: [...]},
  {role: "tool", content: "..."},
  {role: "assistant", content: "..."}
]
```

直接 SFT 这种 multi-turn tool use 数据。

### 11.4 Agent RL

奖励：
- 任务完成度（验证器）
- 工具调用正确率
- 步数效率
- 成本

**典型 pipeline**：
- ReAct trajectory rollout
- Reward model / verifier
- GRPO 更新

例：
- xLAM (Salesforce)：Agent 专用模型 + RL
- ToolACE：工具调用对齐
- AgentInstruct + RL

### 11.5 Function Calling 训练

最实用的 Agent 训练方向：

- BFCL 基准（Berkeley）
- Hermes 3 系列：开源 FC 强
- xLAM：Agent + FC 一体
- Watt-Tool：小模型 FC SOTA

---

## 12. 蒸馏（Distillation）

### 12.1 模式

```
教师 (大模型 / 推理模型)
  → 生成 (Q, A) 或 (Q, thought, A)
  → SFT 学生 (小模型)
```

### 12.2 R1 蒸馏现象

DeepSeek 把 R1 输出蒸馏到 Qwen / Llama：
- DeepSeek-R1-Distill-Qwen-7B
- 仅 SFT，效果接近原版
- 证明**推理能力可蒸馏**

### 12.3 用途

- 缩小模型（成本 / 延迟）
- 私有化部署
- 边缘 / 移动端

### 12.4 局限

- 学生上限 ~ 教师
- 长思考蒸馏需大数据
- 易丢通用能力

---

## 13. 训练硬件 / 时间预算

### 13.1 LoRA SFT 7B

- 1× A100 80GB / RTX 4090（QLoRA）
- 1-10K 样本：数小时
- 50K 样本：1-2 天

### 13.2 全参数 SFT 7B

- 8× A100：1-3 天
- DeepSpeed Zero-3 / FSDP

### 13.3 DPO 7B

- 类似 SFT 但需 ref model
- 显存约 1.5x

### 13.4 GRPO 7B

- 8× A100：3-7 天
- 多 rollout 显存大
- 需要 vLLM serving 加速

### 13.5 70B 训练

- 全参数：32-64× H100，数天
- LoRA / QLoRA：单/多卡可做

### 13.6 云成本参考

- A100 80GB：$1.5-2.5/h
- H100 80GB：$3-5/h
- 1B token 训练（小模型）：$1K-10K
- 全参数 70B SFT：$10K-100K
- 复现 R1 类：$300K+（DeepSeek 自爆）

---

## 14. 评估

### 14.1 通用

- MMLU / HellaSwag / ARC（旧）
- IFEval（指令跟随）
- AlpacaEval / Arena-Hard / MT-Bench
- LMSys Arena Elo

### 14.2 推理

- MATH / AIME
- GSM8K
- GPQA
- AGIEval

### 14.3 代码

- HumanEval / MBPP
- LiveCodeBench
- SWE-Bench

### 14.4 工具 / Agent

- BFCL（Berkeley Function Calling）
- ToolBench
- AgentBench
- WebArena

### 14.5 业务定制

最重要：**自家 Golden Set**！见 [[concepts/dataset-building.zh-CN]]。

---

## 15. 何时该微调

### 15.1 不该微调的情况（80%）

- 提示工程没穷尽
- RAG 没尝试
- Few-shot 没用
- 数据 < 100 条
- 期望"模型变聪明"
- 没有 eval 框架

### 15.2 该微调的情况

- 风格 / 输出格式严格要求（Prompt 难达成）
- 大量私有专业术语
- 工具调用稳定性需求
- 成本 / 延迟硬约束（需要小模型）
- 私有化部署，离线场景
- 蒸馏推理模型到小模型
- 已有 Golden Set + 持续 eval

### 15.3 决策树

```
有 100+ 高质量样本？
  No  → 先做 Prompt + RAG + Few-shot
  Yes →
    Prompt 调到上限了？
      No  → 继续调 Prompt
      Yes →
        RAG 能解决？
          Yes → RAG
          No  →
            目的：风格 / 格式 / 工具？
              → SFT (LoRA)
            目的：偏好对齐？
              → DPO / KTO
            目的：复杂推理？
              → GRPO（成本高）
            目的：缩小模型？
              → 蒸馏
```

---

## 16. 训练栈生态

### 16.1 数据工具

- Argilla / Label Studio：标注
- Distilabel：合成
- Lilac / Nomic Atlas：质量管理
- Hugging Face Datasets：托管

### 16.2 训练框架

| 框架 | 特点 |
|------|------|
| **TRL (HF)** | 通用 SFT/DPO/PPO/GRPO |
| **Axolotl** | 易用配置，社区主力 |
| **LLaMA-Factory** | UI 友好，国内流行 |
| **Unsloth** | 极速，省显存 |
| **torchtune** | PyTorch 官方 |
| **DeepSpeed-Chat** | 大规模 |
| **OpenRLHF** | RLHF 专精 |
| **verl** | GRPO 推理 RL |
| **NeMo (NVIDIA)** | 企业级 |
| **Megatron-LM** | 超大规模 |

### 16.3 推理 / 部署

- vLLM
- SGLang
- TensorRT-LLM
- TGI
- llama.cpp（量化部署）

### 16.4 实验跟踪

- Weights & Biases
- MLflow
- Trackio

---

## 17. Dawning 与微调的关系

### 17.1 Dawning 不做的事

- 不是训练框架
- 不内置 SFT/DPO/GRPO 实现
- 不管 GPU 集群

### 17.2 Dawning 做的事

#### 17.2.1 数据回流

通过 IAgentEventStream + Trace → Dataset 流水线，
把生产数据转成可训练格式（见 [[concepts/dataset-building.zh-CN]]）。

#### 17.2.2 模型适配

```csharp
public interface ILLMProvider
{
    // 内置支持加载微调模型（GGUF / safetensors / API ID）
}
```

实现：
- Ollama Provider（GGUF / 自定义模型）
- vLLM Provider（HF 模型 / LoRA）
- OpenAI/Azure（fine-tune API）
- 自托管 endpoint

#### 17.2.3 模型路由

```
PolicyEngine：
  - 复杂任务 → 旗舰模型
  - 工具任务 → 自家微调小模型
  - 简单任务 → 蒸馏 7B 本地
```

#### 17.2.4 持续评估

Dawning Layer 6 的 evaluation 钩子可对接 BFCL / 自家 Golden Set，
持续测**生产模型 vs fine-tune 候选**。

#### 17.2.5 Skill 与微调结合

Skill 演化（[[concepts/skill-evolution.zh-CN]]）的"Patch"阶段：
- 多次失败 trace 触发 fine-tune 任务
- Fine-tune 模型替代或备选
- Canary 上线

### 17.3 边界

Dawning 与 **Axolotl / TRL / verl** 是**协作**而非竞争。
Dawning 提供：数据 + 评估 + 路由 + 部署。
训练交给生态。

---

## 18. 实战流程：把 R1 蒸馏成业务专用 7B

```
1. 准备数据
   - 生产 1000 条复杂业务问题
   - 用 R1 生成长思考 + 答案
   - 人工抽检质量
   - 清洗成 SFT 格式
   
2. 选基座
   - Qwen2.5-7B-Instruct
   - 或 Llama-3.1-8B
   
3. SFT
   - Axolotl + QLoRA
   - 1× A100 80GB
   - 3 epochs, ~6 小时
   
4. Eval
   - Golden Set (200 条)
   - 对比基座 + R1
   - 关注：成功率、思考长度、成本
   
5. 上线
   - 转 GGUF（llama.cpp）
   - Ollama 部署
   - Dawning Provider 路由
   
6. 监控 + 迭代
   - Trace 回收新数据
   - 季度重训
```

---

## 19. 常见陷阱

| 陷阱 | 教训 |
|------|------|
| 数据少就硬 SFT | 过拟合 + 通用能力丢 |
| 不做 eval 上线 | 看不出退化 |
| 一次训太多 epoch | overfit |
| 无 reference 偏好 | DPO 走偏 |
| 蒸馏不抽检数据 | 教师错也学 |
| 训完不冻 base | 升级时找不到对应版本 |
| 单一基准评估 | 通用能力悄悄掉 |
| Prompt 没调上限就训 | 浪费钱 |

---

## 20. 趋势

### 20.1 2026-2027 可期

- **GRPO 成主流**：所有推理 RL 默认
- **R1 蒸馏遍地**：每家有 7B/14B 推理小模型
- **Agent 模型专门化**：xLAM 路线
- **PEFT 取代 Full FT**（除了基础研究）
- **Online RL**（生产 trace 实时学）逐步可用
- **"Constitutional AI" 风格自训**进入企业
- **Multi-modal 微调成熟**（视觉 / 语音）

### 20.2 不会发生的（短期）

- 微调"一劳永逸" → 持续 ops 是常态
- 小公司复现 GPT-4 → 成本仍 prohibitive
- 完全替代 Prompt → Prompt 永远是第一手段

---

## 21. 小结

> 后训练从"必须 RLHF"走向"DPO/KTO/GRPO 多路线"，
> PEFT 让微调不再是大公司专利，
> R1 蒸馏让小模型也能"思考"——
> 这是 Agent 工程师 2026 必须懂的能力。
>
> Dawning 不做训练框架，做**数据回流 + 多模型适配 + 路由 + Skill 演化的接入点**——
> 让微调结果在生产可用、可观测、可治理。

---

## 22. 延伸阅读

- [[concepts/reasoning-models.zh-CN]] — RLVR 推理模型
- [[concepts/dataset-building.zh-CN]] — 数据构建
- [[concepts/embedding-models.zh-CN]] — Embedding 微调
- [[concepts/skill-evolution.zh-CN]] — 微调与 Skill 演化
- TRL: <https://huggingface.co/docs/trl>
- Axolotl: <https://github.com/axolotl-ai-cloud/axolotl>
- Unsloth: <https://github.com/unslothai/unsloth>
- LLaMA-Factory: <https://github.com/hiyouga/LLaMA-Factory>
- verl: <https://github.com/volcengine/verl>
- DPO 论文: <https://arxiv.org/abs/2305.18290>
- GRPO（DeepSeekMath）: <https://arxiv.org/abs/2402.03300>
- KTO: <https://arxiv.org/abs/2402.01306>
- ORPO: <https://arxiv.org/abs/2403.07691>
- BFCL: <https://gorilla.cs.berkeley.edu/leaderboard.html>
- xLAM: <https://github.com/SalesforceAIResearch/xLAM>
