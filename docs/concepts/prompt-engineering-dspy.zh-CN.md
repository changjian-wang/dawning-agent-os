---
title: "Prompt Engineering 与 DSPy 自动优化深度"
type: concept
tags: [prompt, prompt-engineering, dspy, optimization, few-shot, cot]
sources: [concepts/skill-evolution.zh-CN.md, concepts/agent-evaluation.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Prompt Engineering 与 DSPy 自动优化深度

> Prompt 是 LLM 的"源代码"。
> 从 2022 年的"手写 Prompt"到 2024-2025 年的"DSPy 编译式优化"，再到 2026 年 Dawning Layer 5 的"运行时演化"，这条路线图代表了 Agent 研发工程化的主线。
>
> 本文梳理 Prompt Engineering 的完整技法体系、DSPy 自动优化原理，以及 Dawning 如何把两者整合进技能演化循环。

---

## 1. Prompt Engineering 的演进三阶段

```
Stage 1 (2022-2023)     Stage 2 (2024-2025)       Stage 3 (2026+)
────────────────        ─────────────────         ───────────────
手写 Prompt               DSPy 编译优化             运行时演化
("Prompt Engineer")     ("Prompt Compiler")       ("Self-Evolving Skills")

特征：
- 人工反复试错            - 目标：Metric 最大化     - 目标：生产指标最大化
- Notion/Excel 管理      - 编译时优化 few-shot     - 运行时反思 + 门禁
- 版本难追                - 版本化 + 可重复         - Scope + 治理 + 灰度
```

---

## 2. 核心 Prompt 技法体系

### 2.1 Zero-Shot vs Few-Shot

```
Zero-Shot:
  "把以下评论分类为正面/负面/中性："

Few-Shot:
  "示例：
   评论：'产品质量很好' → 正面
   评论：'客服态度不好' → 负面
   评论：'发货还算快' → 中性
   现在分类：'价格有点贵但值得'"
```

**2025 年观察**：前沿模型（GPT-4o / Claude 3.7 / Gemini 2.5）zero-shot 能力已经很强，Few-Shot 主要用在：
- 输出格式固化
- 领域术语映射
- 特定推理路径引导

### 2.2 Chain-of-Thought（CoT）

让 LLM 显式写出推理步骤：

```
标准 Prompt:
  "17 × 23 = ?"
  → 可能出错

CoT Prompt:
  "17 × 23 = ?
   让我们一步步算：
   17 × 23 = 17 × (20 + 3)
          = 17 × 20 + 17 × 3
          = 340 + 51
          = 391"
```

**演进**：
- **Zero-Shot CoT**："Let's think step by step"（2022）
- **Few-Shot CoT**：给出带推理的示例
- **Self-Consistency**（2022）：多次采样 + 投票
- **Tree-of-Thoughts（ToT）**（2023）：分支搜索 + 剪枝
- **Extended Thinking**（2024-2025）：Claude/OpenAI 原生支持，模型内部深度推理

### 2.3 Structured Output

强制模型返回结构化数据：

```json
{
  "type": "json_schema",
  "json_schema": {
    "name": "classification",
    "schema": {
      "type": "object",
      "properties": {
        "sentiment": { "enum": ["positive", "negative", "neutral"] },
        "confidence": { "type": "number" }
      },
      "required": ["sentiment", "confidence"]
    },
    "strict": true
  }
}
```

**2024-2025 的重大进展**：OpenAI Strict Mode、Anthropic Tool Use、Gemini responseSchema 都能保证 100% JSON 合规。

### 2.4 System Prompt 设计

```
[角色]
你是一个专业的金融分析师。

[目标]
帮助用户理解财报并给出投资建议。

[约束]
- 不提供具体买卖建议
- 所有数据必须注明来源
- 超出范围时建议咨询持牌顾问

[输出格式]
使用 Markdown，分节："摘要"/"详细分析"/"风险提示"

[示例]
（可选的 Few-Shot）
```

### 2.5 Prompt 压缩与缓存

| 技法 | 说明 |
|------|------|
| **LLMLingua** | 用小模型压缩 Prompt（去冗余） |
| **Prompt Caching** | Anthropic / OpenAI 内建，重复 prefix 降价 |
| **Context Compression** | 用 LLM 摘要长上下文 |

### 2.6 角色扮演与元指令

| 技法 | 示例 |
|------|------|
| **角色植入** | "你是一个 10 年经验的 SRE 工程师" |
| **元指令** | "回答前先反思你的假设" |
| **否定约束** | "不要使用专业术语"（语义否定通常不如正向约束有效） |
| **链接思考** | "Let's solve this step by step" |

---

## 3. Prompt Engineering 的工程化痛点

| 痛点 | 表现 |
|------|------|
| **可重现差** | 同一 Prompt 在不同 LLM 上效果天差地别 |
| **脆弱性** | 改个词就可能崩坏 |
| **调参靠直觉** | 改哪个部分有效？无法系统分析 |
| **难以组合** | 多个 Prompt 串成 pipeline，改一处影响全局 |
| **评估成本高** | 每次改动需要跑 dataset 验证 |
| **跨模型迁移** | 换模型几乎要重写 |

→ **这些痛点催生了 DSPy**。

---

## 4. DSPy：Prompt 编译器

### 4.1 核心思想

> **把 Prompt 从"写"变成"编译"。**
>
> 开发者只声明**程序结构**（Signature 输入输出、Module 组合），
> 具体 Prompt 文本由 DSPy **编译器根据 metric 优化生成**。

### 4.2 三大原语

#### 4.2.1 Signature

```python
class ClassifySentiment(dspy.Signature):
    """Classify sentiment of customer review."""
    review: str = dspy.InputField()
    sentiment: Literal["positive", "negative", "neutral"] = dspy.OutputField()
    confidence: float = dspy.OutputField()
```

描述"做什么"，不关心"怎么让 LLM 做到"。

#### 4.2.2 Module

```python
class SentimentClassifier(dspy.Module):
    def __init__(self):
        self.classify = dspy.ChainOfThought(ClassifySentiment)

    def forward(self, review):
        return self.classify(review=review)
```

Module 组合其他 Module（`Predict`, `ChainOfThought`, `ReAct`, `ProgramOfThought`）。

#### 4.2.3 Optimizer (Teleprompter)

```python
from dspy.teleprompt import MIPRO

optimizer = MIPRO(
    metric=accuracy_metric,
    num_candidates=20
)

optimized_classifier = optimizer.compile(
    SentimentClassifier(),
    trainset=train_examples,
    valset=val_examples
)
```

Optimizer 自动做这几件事：
1. **Bootstrap Few-Shot**：从训练集选最有代表性的示例
2. **Instruction Optimization**：用 LLM 生成多个候选 system prompt
3. **Search**：用 Metric 评估每个组合，选最优

### 4.3 DSPy 支持的优化器

| 优化器 | 原理 | 适合场景 |
|--------|------|---------|
| **BootstrapFewShot** | 自动挑选 few-shot 示例 | 简单任务 |
| **BootstrapFewShotWithRandomSearch** | 随机采样 + 评估 | 中等规模 |
| **COPRO** | 纯 instruction 优化 | Zero-shot 场景 |
| **MIPRO / MIPROv2** | 联合优化 instruction + demos | 复杂任务（当前最强） |
| **BootstrapFinetune** | 用优化结果做 SFT | 有 finetune 预算 |
| **BetterTogether** | LLM-driven + Gradient | 混合模式 |

### 4.4 一个完整例子

```python
import dspy

# 1. 配置 LLM
dspy.settings.configure(lm=dspy.OpenAI(model="gpt-4o-mini"))

# 2. 定义 Signature
class GenerateAnswer(dspy.Signature):
    """Answer questions using provided context."""
    context = dspy.InputField()
    question = dspy.InputField()
    answer = dspy.OutputField()

# 3. 组合 Module
class RAG(dspy.Module):
    def __init__(self, retriever):
        self.retriever = retriever
        self.generate = dspy.ChainOfThought(GenerateAnswer)

    def forward(self, question):
        context = self.retriever(question).passages
        return self.generate(context=context, question=question)

# 4. 定义 Metric
def exact_match(example, pred, trace=None):
    return pred.answer.strip().lower() == example.answer.strip().lower()

# 5. 编译
optimizer = dspy.MIPROv2(metric=exact_match, auto="medium")
optimized_rag = optimizer.compile(
    RAG(retriever=my_retriever),
    trainset=train_set,
    max_bootstrapped_demos=4,
    max_labeled_demos=4
)

# 6. 使用
answer = optimized_rag(question="...")
```

### 4.5 DSPy 的局限

| 局限 | 说明 |
|------|------|
| 需要训练集 | 至少 20-50 条标注 |
| 编译成本 | 优化期间大量 LLM 调用 |
| 静态优化 | 编译后 Prompt 固定，生产中不再演化 |
| 治理缺失 | 没有灰度、回滚、审计 |
| Python 生态 | 对 .NET 支持弱 |
| Agent 能力有限 | 主要针对单次推理，不是完整 Agent 系统 |

---

## 5. DSPy vs 手写 vs Layer 5

```
     手写 Prompt           DSPy 编译             Dawning Layer 5
──────────────────      ──────────────────      ────────────────────
优点：                     优点：                   优点：
- 上手快                  - 自动优化               - 运行时演化
- 灵活                    - 可重现                 - 治理 + 灰度
                         - Metric 驱动             - Scope 感知

缺点：                     缺点：                   缺点：
- 不可重复                 - 需训练集               - 工程复杂度高
- 不可扩展                 - 编译时固定             - 需要稳定的 Metric
- 缺乏 metric              - 无治理                 - Layer 5 还在建设
- 难评估
```

→ **三者不是替代，而是阶梯**：
1. 先手写 Prompt 原型
2. 用 DSPy 编译一次做离线优化
3. 进入 Layer 5 持续演化

---

## 6. 跨 LLM 的 Prompt 移植

### 6.1 三家的 Prompt 偏好差异

| LLM | 偏好特征 |
|------|---------|
| **GPT-4/4o** | 结构化、分节、明确的指令；擅长跟随系统消息 |
| **Claude 3.x** | 自然语言为主；喜欢 XML 结构（`<instructions>...</instructions>`）；推理更线性 |
| **Gemini 2.x** | 中间偏 GPT；长上下文优势明显 |

### 6.2 通用模板的陷阱

```
❌ 假设：一套 Prompt 走天下
✅ 现实：每家的 prompt 需要适配

常见坑：
- Claude 不喜欢"You MUST"这种命令式强调
- GPT 在 system 里强调"Never"通常比在 user message 里管用
- Gemini 对冗长 system prompt 执行力较差
```

### 6.3 Dawning 的抽象策略

```csharp
public interface IPromptAdapter
{
    LLMRequest Adapt(PromptTemplate template, Dictionary<string, object> vars);
}

// 每家适配器做 Prompt 结构翻译
public class OpenAIPromptAdapter : IPromptAdapter { ... }
public class AnthropicPromptAdapter : IPromptAdapter
{
    // 自动把结构化 section 转成 <instructions> / <context> XML
}
```

---

## 7. Prompt 版本管理最佳实践

### 7.1 存储在哪里

| 方案 | 优缺点 |
|------|-------|
| 代码内字符串 | 简单，但难以非研发人员协作 |
| Prompt 文件（.prompt/.md） | 可版本化，CI 友好 |
| 数据库 + Admin UI | 可灰度、运行时切换 |
| **推荐**：代码 + DB 双存在 | 默认代码，需要实验时用 DB 覆盖 |

### 7.2 Prompt 与 Skill 的关系

在 Dawning 中：

```
Skill
├── prompt_template     ← Prompt 文本 / 结构
├── version             ← 语义化版本
├── tools[]             ← 依赖的 ITool
├── gates[]             ← 门禁证据
└── metadata            ← Scope / Owner / 演化历史
```

Prompt 不是独立实体，而是 Skill 的一个字段，**演化时整体变更**。

---

## 8. Dawning 整合设计

### 8.1 三个层次

```
Layer 0 (driver):
  ILLMProvider + IPromptAdapter  ← 跨家翻译

Layer 1 (system call):
  PromptTemplate + SkillPrompt   ← 文本模板 + 变量绑定

Layer 5 (evolution):
  Reflect ─► Patch ─► Gate ─► Deploy
                          ↑
              可选：DSPy 集成（离线优化）
```

### 8.2 DSPy 集成方向

Dawning 不重造 DSPy，而是作为 Layer 5 的**可选离线优化器**：

```csharp
services.AddSkillEvolution(evo =>
{
    evo.UseOfflineOptimizer(new DSPyOptimizer
    {
        PythonEndpoint = "http://localhost:8000",  // DSPy 服务
        Optimizer = DSPyOptimizerKind.MIPROv2,
        Trainset = "skills/weather/train.jsonl"
    });
});
```

工作流：
1. 正常运行 → Observe Memory 积累轨迹
2. 定期将轨迹导出为 DSPy 训练集
3. DSPy 编译新 Prompt
4. 新 Prompt 作为 Skill Patch 进入 Gate
5. 通过 Gate 后灰度部署

### 8.3 Dawning 独有的 Prompt 工程能力

| 能力 | 其他工具 | Dawning |
|------|---------|---------|
| Prompt 版本化 | ⚠️ 外部 | ✅ 与 Skill 绑定 |
| Scope 感知 | ❌ | ✅ |
| Prompt A/B 灰度 | ⚠️ 商业工具 | ✅ 内建 |
| 跨 LLM 适配 | ⚠️ 手动 | ✅ IPromptAdapter |
| 结合 DSPy 编译 | ⚠️ | ✅ 可选集成 |
| 治理 + 审计 | ❌ | ✅ Layer 7 |

---

## 9. Prompt Engineering 实用技法手册

### 9.1 快速上手检查清单

- [ ] 角色是否明确（"你是 X"）
- [ ] 目标是否具体（"完成 Y"）
- [ ] 约束是否正向（"必须" > "不要"）
- [ ] 输出格式是否结构化（JSON / Markdown 分节）
- [ ] 是否有 2-3 个 Few-Shot 示例
- [ ] 是否要求 Chain-of-Thought
- [ ] 边界情况是否说明（"如果不知道，回答 '未知'"）
- [ ] 测试数据是否覆盖边界

### 9.2 调试技巧

| 症状 | 可能原因 | 修法 |
|------|---------|------|
| 输出太长 | 缺少长度约束 | 显式指定字数 / 使用 `max_tokens` |
| 不遵循格式 | 未用结构化输出 | 启用 Strict Mode / `response_format` |
| 不调用工具 | 工具描述不清 | 丰富 `description` + `tool_choice: "required"` |
| 幻觉 | 缺上下文 / 缺约束 | 补 RAG / 加"如果不知道就说不知道" |
| 跨轮不一致 | 短期记忆丢失 | 摘要关键事实 + 放到 system |
| 代码生成错误 | 没给示例 | Few-Shot + 指定语言版本 |

---

## 10. 延伸：高级范式

### 10.1 Meta-Prompting

让 LLM **生成** Prompt：

```
Meta-Prompt:
  "我想让一个 LLM 做 X 任务。
   请生成一个高质量的 system prompt，包含角色、目标、约束、示例、输出格式。"

→ Output: 可直接使用的 Prompt
```

### 10.2 Self-Refine

```
Step 1: LLM 生成答案 A
Step 2: LLM 批评答案 A
Step 3: LLM 基于批评改进为答案 B
Step 4: 迭代 N 轮
```

### 10.3 Prompt Chaining

把复杂任务拆成多步 Prompt：

```
Step 1: 提取实体
Step 2: 基于实体检索
Step 3: 基于检索结果总结
Step 4: 基于总结提建议
```

**Dawning 中对应**：多 Skill 组合（Layer 3 Orchestrator）。

---

## 11. 小结

| 阶段 | 抽象 | 典型工具 | Dawning 位置 |
|------|------|---------|-------------|
| 写 Prompt | 文本 | 文本编辑器 | PromptTemplate |
| 评估 Prompt | Metric | LangSmith / Langfuse | Layer 7 Eval |
| 编译 Prompt | Module + Optimizer | DSPy | Layer 5 可选集成 |
| 运行时演化 | Skill + Gate + Rollout | （无现成工具） | **Layer 5 本体** |
| 跨 LLM 适配 | Adapter | MAF / Spring AI | Layer 0 IPromptAdapter |

> **Prompt 不应该是"经验性手工制品"，而应该是"工程可追溯的组件"**。
> Dawning 把 Prompt 放在 Skill 的字段位置，和其他工程资产一样享受版本化、门禁、灰度、审计。

---

## 12. 延伸阅读

- [[concepts/skill-evolution.zh-CN]] — Layer 5 如何让 Prompt 持续演化
- [[concepts/agent-evaluation.zh-CN]] — 如何用 Metric 驱动 Prompt 优化
- [[comparisons/function-calling-comparison.zh-CN]] — 跨 LLM 的 Tool Prompt 差异
- DSPy 官方：<https://dspy.ai/>
- Prompting Guide：<https://www.promptingguide.ai/>
- Anthropic Prompt Library：<https://docs.anthropic.com/en/prompt-library/library>
- OpenAI Prompting Guide：<https://platform.openai.com/docs/guides/prompt-engineering>
