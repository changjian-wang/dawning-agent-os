---
title: "Agent 评估（Eval）专题：指标、工具与方法论"
type: concept
tags: [evaluation, eval, langsmith, langfuse, arize, metrics, llm-as-judge]
sources: [concepts/skill-evolution.zh-CN.md, comparisons/rag-pipeline-comparison.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agent 评估（Eval）专题：指标、工具与方法论

> "能不能改善"是 Layer 5 的目标，"怎么知道改善了"是 Eval 的目标。
> 没有系统的评估，Agent 的改进只是盲人摸象。
>
> 本文梳理 Agent Eval 的完整知识：四层指标体系、五类评估方法、三大商业工具对比，以及 Dawning 在这一层的设计方向。

---

## 1. 为什么 Agent 比传统软件更难评估

### 1.1 输入输出都是自然语言

- 没有精确匹配的"正确答案"
- 语义等价的答案可能有无数种
- 评估本身可能需要 LLM 参与

### 1.2 执行路径多变

- 同一个任务，Agent 可能走出完全不同的工具调用序列
- 结果可能相同，但效率和成本大不相同
- 单次执行不具代表性

### 1.3 多维目标的权衡

```
       正确性
         │
         │
  成本 ──┼── 延迟
         │
         │
       安全性
```

优化一个维度往往劣化其他维度。**Eval 必须覆盖所有维度**。

---

## 2. 四层指标体系

### 2.1 Layer 1：输出质量

| 指标 | 含义 | 测量方法 |
|------|------|---------|
| **Correctness** | 答案正确吗 | 参考答案对比 / LLM 判定 |
| **Faithfulness** | 是否基于提供的上下文（防幻觉） | LLM 判定 / 引用验证 |
| **Relevance** | 是否回答了问题 | LLM 判定 |
| **Coherence** | 语言流畅性 | LLM 判定 / Perplexity |
| **Groundedness** | 每句话是否有来源支撑（RAG） | 引用覆盖率 |

### 2.2 Layer 2：行为质量

| 指标 | 含义 |
|------|------|
| **Tool Selection Accuracy** | 工具选对了吗 |
| **Tool Argument Accuracy** | 参数填对了吗 |
| **Plan Quality** | 规划是否最优（步骤数、顺序） |
| **Path Efficiency** | 是否走了无用路径 |
| **Handoff Quality** | 多 Agent 移交是否合理 |

### 2.3 Layer 3：运行质量

| 指标 | 含义 |
|------|------|
| **Latency p50/p95/p99** | 响应延迟分布 |
| **Cost per task** | 完成任务的平均成本 |
| **Token Usage** | input/output token |
| **Retry Rate** | 重试频率 |
| **Failure Rate** | 完全失败的比例 |

### 2.4 Layer 4：业务质量

| 指标 | 含义 |
|------|------|
| **Task Success Rate** | 业务定义的成功率 |
| **User Satisfaction** | 👍/👎 比例 / NPS |
| **Escalation Rate** | 升级到人工的比例 |
| **Containment Rate** | Agent 独立完成的比例 |

---

## 3. 五类评估方法

### 3.1 参考答案对比（Reference-Based）

```
Input: "1+1=?"
Expected: "2"
Actual: "等于2"

Metric: ROUGE / BLEU / Exact Match / Fuzzy Match / Embedding Similarity
```

**优点**：确定性高
**缺点**：大量任务没有唯一答案

### 3.2 LLM-as-Judge

```
Prompt:
  给定问题 Q，参考答案 A，模型答案 B。
  评估 B 是否语义等价于 A，给出 1-5 分。

评判 LLM: GPT-4o / Claude Opus / Gemini Pro
```

**优点**：处理开放式答案
**缺点**：
- Judge 本身有偏差（偏向 verbose、偏向自家模型）
- 成本可观
- 需要校准（比对人工标注）

**2025 年的最佳实践**：
- Judge 用**不同于被评模型的 LLM**（避免自评偏差）
- 用 **pairwise comparison** 优于 pointwise scoring
- 结合 **Chain-of-Thought** 要求 Judge 解释理由

### 3.3 Rubric-Based Evaluation

把"好答案"分解为可测量的 rubric：

```yaml
rubric:
  - name: "Accuracy"
    weight: 0.4
    scale: [0, 1]
    definition: "事实信息是否正确"
  - name: "Completeness"
    weight: 0.3
    scale: [0, 1]
    definition: "覆盖了用户问题的所有方面"
  - name: "Actionability"
    weight: 0.3
    scale: [0, 1]
    definition: "提供了可执行的下一步"
```

LLM 按 rubric 逐项打分，加权汇总。

### 3.4 基于约束的评估（Constraint-Based）

不需要参考答案，检查输出是否满足约束：

- **格式约束**：JSON 合法？包含特定字段？
- **长度约束**：不超过 200 字
- **安全约束**：不包含 PII / 不包含辱骂词
- **工具调用约束**：是否调用了必需工具
- **引用约束**：RAG 场景下每个事实必须有引用

### 3.5 基于人工标注的评估（Human Eval）

黄金标准，但成本最高：

```
方案 A: 全量人工（不可扩展）
方案 B: 抽样人工 + LLM-as-Judge 校准
方案 C: 用户标注驱动（👍/👎 隐式反馈）
```

**2025-2026 行业共识**：
- 用 Human Eval **校准 LLM Judge**
- 日常运行靠 LLM Judge
- Release 前抽样 Human Eval

---

## 4. RAG 专项评估（RAGAS 体系）

RAG 系统的标准评估指标：

| 指标 | 含义 | 测量 |
|------|------|------|
| **Context Precision** | 检索结果有多少是真的相关 | LLM 判定每个 chunk 是否相关 |
| **Context Recall** | 需要的信息被检索到多少 | 对比参考答案所需信息 |
| **Faithfulness** | 回答是否基于 context | LLM 拆解事实 → 检查是否在 context 中 |
| **Answer Relevance** | 回答是否针对问题 | 反向生成问题 + 相似度 |
| **Answer Correctness** | 回答是否正确 | 语义匹配参考答案 |

**RAGAS** 框架（<https://docs.ragas.io/>）把这些指标实现成 Python 库。

---

## 5. Agent 专项评估（Agent Traces）

### 5.1 Trajectory Evaluation

评估 Agent 整条执行轨迹：

```
Trace:
  Step 1: LLM → tool_call(search, "Python sort")
  Step 2: tool_result: "..."
  Step 3: LLM → tool_call(search, "Python timsort")  ← 第二次搜索
  Step 4: tool_result: "..."
  Step 5: LLM → final answer

Trajectory Quality:
  - Efficiency: 能否一次搜索完成？（理想：2 步；实际：5 步）
  - Tool Selection: 每步选的工具对吗？
  - Progress: 每步是否朝目标前进？
```

### 5.2 Goal Completion

```
Goal: "预订从北京到上海明天下午 2 点的高铁"

Checks:
  ✅ 调用了 search_trains(from="北京", to="上海")
  ✅ 选出了明天下午 2 点的班次
  ✅ 调用了 book_ticket
  ✅ 返回了订单号
```

---

## 6. 商业工具对比

### 6.1 LangSmith（LangChain）

| 能力 | 说明 |
|------|------|
| 追踪 | 全链路自动采集 LLM / Tool / Chain 调用 |
| Dataset | 在线收集 + 版本化 |
| Eval | 内建 LLM Judge + 自定义 Evaluator |
| Playground | 对比多版本 Prompt |
| A/B Testing | 灰度 + 比较 |
| 价格 | Cloud 按 trace 计费 / Self-hosted 企业版 |

**优势**：与 LangChain/LangGraph 零集成成本
**劣势**：对非 LangChain 应用友好度一般

### 6.2 Langfuse

| 能力 | 说明 |
|------|------|
| 追踪 | OpenTelemetry 兼容，供应商中立 |
| Dataset | ✅ |
| Eval | LLM-as-Judge + User Feedback + 自定义 |
| Prompt Management | 版本化 + 灰度 |
| 价格 | **开源自托管免费** + Cloud 订阅 |

**优势**：开源 + 供应商中立
**劣势**：生态不如 LangSmith 丰富

### 6.3 Arize AI / Phoenix

| 能力 | 说明 |
|------|------|
| 追踪 | OpenTelemetry + OpenInference 规范 |
| Monitoring | 生产环境监控 + 漂移检测 |
| Eval | 模型评估 + LLM Eval |
| **Phoenix** | Arize 开源版，本地运行 |

**优势**：偏向 ML / LLMOps，生产级监控成熟
**劣势**：Agent-specific 能力少于 LangSmith

### 6.4 Braintrust

| 能力 | 说明 |
|------|------|
| Eval-first 设计 | 专注 Eval，不做追踪 |
| Playground | 交互式 Prompt 迭代 |
| CI Integration | pytest 风格集成 |

### 6.5 工具选型建议

| 场景 | 推荐 |
|------|------|
| LangChain/LangGraph 生态 | LangSmith |
| 供应商中立 / 开源偏好 | Langfuse |
| 生产级 LLMOps | Arize / Phoenix |
| CI 密集的 Eval 驱动开发 | Braintrust |
| .NET 企业（Dawning） | **OpenTelemetry + Langfuse + 自研 Eval Gate** |

---

## 7. Dataset 管理

### 7.1 Dataset 的三个来源

| 来源 | 特点 |
|------|------|
| **合成（Synthetic）** | LLM 生成，大量但质量参差 |
| **生产抓取** | 真实用户输入，质量高但隐私敏感 |
| **人工构造** | 覆盖边界情况，成本高但最有价值 |

### 7.2 Dataset 的版本化

```yaml
dataset: customer-support-v3
items:
  - id: 001
    input: "我想退款"
    expected_tool: refund_api
    rubric_scores:
      empathy: 0.9
      correctness: 1.0
    source: production-2026-03
    reviewer: alice
    version_created: 2026-04-01
```

### 7.3 Golden Dataset vs Regression Dataset

| 类型 | 作用 |
|------|------|
| **Golden** | 少量、高质量、作为基准 |
| **Regression** | 历史 bug 案例，确保不复发 |
| **Adversarial** | 对抗样本，测试鲁棒性 |
| **Production Shadow** | 生产样本匿名化，持续增长 |

---

## 8. 评估流水线（Eval Pipeline）

### 8.1 三阶段

```
Offline Eval      ──►   CI Eval        ──►   Online Eval
（开发时）              （PR 时）              （生产时）

- 全量 dataset           - 子集 golden         - 全量用户请求（采样）
- 慢、彻底               - 快、阻塞合并         - 实时监控
- 驱动算法改进           - 防止回归             - 检测漂移
```

### 8.2 CI 门禁示例

```yaml
# .github/workflows/eval.yml
- name: Run Eval
  run: dotnet test --filter Category=Eval

- name: Check Success Rate
  run: |
    if [[ $SUCCESS_RATE < 0.85 ]]; then
      echo "::error::Success rate dropped below 85%"
      exit 1
    fi
```

### 8.3 在线监控（Online Eval）

```
用户请求 → Agent 执行 → 生成追踪
                          │
                          ├─► OpenTelemetry ──► 监控面板
                          │
                          └─► 采样（1%）──► LLM Judge ──► 告警
```

监控的关键指标（对应 Layer 3 + Layer 4）：
- 实时成功率
- 平均延迟 / 成本
- 异常模式检测（错误突增、LLM Judge 分数下降）

---

## 9. LLM-as-Judge 的常见陷阱与修正

### 9.1 偏差问题

| 偏差 | 表现 | 修正 |
|------|------|------|
| Verbosity Bias | 偏好长答案 | 提示里明确"简洁优先" |
| Position Bias | pairwise 时偏好第一个 | 随机化顺序 + 双向评估 |
| Self-Enhancement | 偏好自家模型输出 | Judge 用不同家族 LLM |
| Beauty Bias | 偏好表面优美的答案 | 加强 rubric 约束 |

### 9.2 Judge 可信度验证

```
人工标注 1000 条 → LLM Judge 判定 1000 条
                       │
                       ▼
              计算 Agreement（Cohen's Kappa）
                       │
                       ▼
             κ > 0.7 → Judge 可信
             κ < 0.7 → 调整 Prompt 或换模型
```

---

## 10. Dawning 的 Eval 设计

### 10.1 定位：贯穿多层

```
Layer 1: Tool Invocation Pipeline       ──► Tool Accuracy 指标
Layer 2: Memory (Observation)           ──► 行为 Trace 存储
Layer 3: Orchestrator Runtime           ──► Trajectory 采集
Layer 5: Skill Evolution (Gate)         ──► Regression + A/B 门禁
Layer 7: Audit + Telemetry              ──► 出口对接 Langfuse / OTEL
```

### 10.2 评估能力 DI 注册

```csharp
services.AddAgentEvaluation(eval =>
{
    eval.UseOpenTelemetry();
    eval.UseLangfuse(opt => opt.Endpoint = "...");

    eval.AddEvaluator<CorrectnessEvaluator>(options =>
    {
        options.JudgeModel = "claude-3-5-sonnet";
        options.PromptTemplate = ...;
    });
    eval.AddEvaluator<FaithfulnessEvaluator>();
    eval.AddEvaluator<ToolSelectionEvaluator>();
    eval.AddEvaluator<LatencyEvaluator>(opt => opt.P99Threshold = 5000);

    eval.WithDataset("support-golden-v3");
});
```

### 10.3 Gate 集成（Layer 5）

```csharp
// Skill 补丁必须通过 Eval 才能部署
skillEvolution.AddGate(new EvalGate
{
    Dataset = "support-golden-v3",
    MinSuccessRate = 0.85,
    MinABWinRate = 0.55
});
```

### 10.4 Dawning 独有价值

| 能力 | 其他工具 | Dawning |
|------|---------|---------|
| Scope 感知 Eval | ❌ | ✅ 不同 Scope 不同 dataset |
| Eval → Skill 演化闭环 | ⚠️ 手动 | ✅ 内建 Layer 5 流水线 |
| 治理强绑定 | ❌ | ✅ Eval 事件 → 审计 |
| .NET 原生 | ❌ | ✅ |
| 厂商无关（OTEL） | ⚠️ | ✅ |

---

## 11. 实施路线图

| 阶段 | 能力 |
|------|------|
| **M0** | OpenTelemetry trace 采集 |
| **M1** | Offline Eval 框架（参考答案对比 + LLM Judge） |
| **M2** | Dataset 管理（版本 + 金集 + 回归集） |
| **M3** | Eval Gate 集成到 Layer 5 |
| **M4** | Online Eval（采样 + 告警） |
| **M5** | 对接 Langfuse / Phoenix |

---

## 12. 小结

> **Eval 不是"测试"，是 Agent 的神经系统——持续的反馈让系统能学会。**
>
> 四层指标（输出/行为/运行/业务）+ 五类方法（参考/Judge/Rubric/Constraint/Human）+ 三段流水线（Offline/CI/Online）+ 治理绑定，是一套完整方案。
>
> Dawning 不做 Eval 产品，但把**指标口径 + OpenTelemetry 输出 + Layer 5 联动**做成一等公民，让用户自由接入 Langfuse / Phoenix / LangSmith。

---

## 13. 延伸阅读

- [[concepts/skill-evolution.zh-CN]] — Layer 5 的 Eval Gate
- [[comparisons/rag-pipeline-comparison.zh-CN]] — RAG 专项指标（§4 RAGAS）
- RAGAS：<https://docs.ragas.io/>
- LangSmith：<https://docs.smith.langchain.com/>
- Langfuse：<https://langfuse.com/>
- Arize Phoenix：<https://docs.arize.com/phoenix>
- G-Eval 论文：<https://arxiv.org/abs/2303.16634>
