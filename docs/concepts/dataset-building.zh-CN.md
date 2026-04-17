---
title: "Agent 测试数据集建设：合成数据、人工标注、持续反馈"
type: concept
tags: [dataset, synthetic-data, annotation, golden-set, continuous-feedback, eval-data]
sources: [concepts/agent-evaluation.zh-CN.md, concepts/observability-deep.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agent 测试数据集建设：合成数据、人工标注、持续反馈

> 评估方法再好，没有数据集也是空谈。
> **Dataset 是 Agent 质量工程的根基**——从 0 到 1 构建一个好的评测集，是比选框架更重要的决策。
>
> 本文梳理 Dataset 构建的四条路径（人工 / 合成 / 生产采样 / 公开基准），标注规范，数据治理，以及 Dawning 的 Dataset 管理设计。

---

## 1. 为什么 Dataset 是根基

### 1.1 没有 Dataset 的后果

- Prompt 改动没人知道更好还是更差
- 模型升级靠"感觉"
- 上线前不知道回归风险
- 生产问题无法定位到规格

### 1.2 Dataset 的四大用途

| 用途 | 数据特征 |
|------|---------|
| **Regression** | 已知期望输出的代表性样本 |
| **Quality** | 复杂 / 边缘场景 |
| **Adversarial** | 安全 / 红队样本 |
| **Capability** | 能力测试（数学 / 代码 / 多步...） |

---

## 2. 数据集的生命周期

```
采集 ──► 标注 ──► 审核 ──► 发布 ──► 使用 ──► 回流 ──► 更新
  ▲                                                      │
  └──────────────────────────────────────────────────────┘
           持续循环
```

---

## 3. 四条采集路径

### 3.1 路径 1：人工构造（Seed）

**适合**：冷启动，业务专家参与。

```
业务专家 → 写 20-50 个代表性用例
→ 覆盖主要意图
→ 覆盖边缘情况
→ 覆盖典型失败
```

**优势**：质量高、覆盖意图明确。
**劣势**：量少、贵、易遗漏分布。

### 3.2 路径 2：合成数据（Synthetic）

**LLM 生成数据训练/评估 LLM**。

#### 3.2.1 Self-Instruct

```
Seed 样本 50 个
   ↓ LLM 扩展
生成 500 个相似但不同的样本
   ↓ 质量过滤
保留 300 个
```

#### 3.2.2 Evol-Instruct

**让 LLM 把简单样本"进化"成难样本**：
- 深化（加约束）
- 复杂化（多步）
- 具体化（加细节）
- 推理链（要求 CoT）

#### 3.2.3 Back-Translation for QA

```
文档 → LLM 生成 5 个可能问题 → QA 对
```

RAGAS / Ragas / Synthesize 都用此法。

#### 3.2.4 优缺点

| 优 | 劣 |
|----|----|
| 量大便宜 | 质量参差 |
| 可定向 | 偏向训练该 LLM 的分布 |
| 快速扩展 | 需要过滤 |
| 覆盖长尾 | Adversarial 弱 |

### 3.3 路径 3：生产采样（Production Mining）

**从真实流量中挖掘**，最珍贵。

#### 3.3.1 采样策略

| 策略 | 说明 |
|------|------|
| **随机** | 基线代表性 |
| **高成本** | $/request p99 |
| **慢请求** | latency p99 |
| **低置信** | logprobs 低 |
| **负反馈** | 👎 / 差评 |
| **异常路径** | 工具调用失败 / 多轮循环 |
| **跨 Scope 尝试** | 安全相关 |
| **新意图** | Clustering 出现的新簇 |

#### 3.3.2 Trace → Dataset

```
Observability Trace
    │
    ▼
自动分类（embedding clustering）
    │
    ▼
人工抽样审核
    │
    ▼
去 PII（必须）
    │
    ▼
加入 Dataset
```

### 3.4 路径 4：公开 Benchmark

拿来做**基线对标**，不是替代自己的 dataset。

| Benchmark | 覆盖 |
|-----------|------|
| MMLU | 多学科知识 |
| HumanEval / MBPP | 代码 |
| GSM8K / MATH | 数学 |
| GPQA | 科学博士级 |
| SWE-Bench | 软件工程 |
| AgentBench | 多场景 Agent |
| WebArena / VisualWebArena | Web Agent |
| AssistantBench | 网页助手 |
| τ-Bench（Tau-Bench） | 工具调用 |
| ToolBench | 工具链 |
| OSWorld | 桌面 Agent |
| MINT | 多轮工具 |
| LongBench | 长上下文 |
| MMLU-Pro | 升级版 MMLU |

---

## 4. 标注规范

### 4.1 标注任务分类

| 任务 | 输入 | 输出 |
|------|------|------|
| **Binary** | 样本 | Pass / Fail |
| **Likert** | 样本 | 1-5 分 |
| **Pairwise** | A, B | 哪个更好 |
| **Structured** | 样本 | 多维度打分 |
| **Span** | 文本 | 标出错误片段 |
| **Rewrite** | 差答 | 改为好答 |

### 4.2 标注准则文档

每个 Dataset 必备 **Annotation Guidelines**：

```
# Annotation Guidelines v1.2

## 任务
判定 Agent 回答的"完整性"分数（1-5）。

## 标准
5 = 完全回答了所有问题，且无多余
4 = 回答了主要问题，小细节遗漏
3 = 回答了一半左右
2 = 触及但未真正回答
1 = 完全偏题

## 边缘情况
- 用户问题模糊 → 按最合理解释判
- Agent 主动澄清 → 视为积极
- 工具调用失败但尝试了 → 不扣完整性分

## 示例
[给 5 个 borderline 例子]
```

### 4.3 一致性度量

多人标同一批，计算：

- **Cohen's Kappa**（2 人）
- **Fleiss' Kappa**（>2 人）
- **Krippendorff's Alpha**（任意）

κ < 0.6 = 标准不清 → 回炉准则。

### 4.4 分工协作

```
业务专家  ←→  准则制定
           ↓
标注员（3 人）  → 独立标注
           ↓
冲突仲裁员  → 解决分歧
           ↓
Dataset Curator → 审核入库
```

---

## 5. 数据治理

### 5.1 Version Control

- **Dataset 必须有版本号**
- Semver：`1.2.3`（major = 语义变；minor = 扩展；patch = 修正）
- 对比报告必须同版本

### 5.2 Split 管理

```
dataset-v1.2.0/
├── train/     (若用于微调，通常 Agent eval 不需要)
├── dev/       40%
├── test/      40%   ← 用于 CI 回归
└── holdout/   20%   ← 从不暴露，季度评估用
```

**绝不**把 holdout 用于开发（数据泄漏）。

### 5.3 Metadata

每条样本附带：

```json
{
  "id": "case-0001",
  "input": {...},
  "expected": {...},
  "tags": ["intent:billing", "difficulty:hard"],
  "source": "production-2026-03",
  "created_by": "alice",
  "created_at": "2026-04-01",
  "annotation_version": 3,
  "pii_redacted": true,
  "scope": "public"
}
```

### 5.4 PII / 合规

- 生产采样样本**必须**脱敏
- 公开 Dataset 要额外法务审核
- 保留完整审计链（谁标的、何时、从何来）

### 5.5 Dataset Cards

类似 Model Card，每个 Dataset 附带：

```
# Dataset Card: customer-service-v2.1

## 动机
用于评估客服 Agent 回答质量。

## 数据源
- 60% 合成（Evol-Instruct）
- 30% 生产采样（2025Q4-2026Q1）
- 10% 人工编写

## 分布
- 意图: billing(35%), technical(30%), policy(25%), other(10%)
- 难度: easy(40%), medium(40%), hard(20%)

## 已知偏差
- 中文样本 > 英文
- 2025 年前产品文档不覆盖

## 变更历史
- v2.1 (2026-04): 增加 50 个 adversarial
- v2.0 (2026-02): 迁移到新 schema
```

---

## 6. 质量保证

### 6.1 自动过滤

- 去重（embedding 相似度 > 0.95）
- 长度异常
- 格式非法
- PII 残留（二次扫描）
- 语言不符

### 6.2 人工抽检

- 每批 10% 抽样
- 发现问题 → 回炉
- 季度性全量审核

### 6.3 Dataset Eval（元评估）

定期测：
- 难度分布
- 覆盖意图
- 区分度（强/弱模型分差）

**区分度**：一个好 dataset 上 GPT-4o 和 GPT-4o-mini 分差明显；若无差异 → dataset 太简单。

---

## 7. 持续反馈回流

### 7.1 三条反馈信号

| 信号 | 质量 | 量 |
|------|------|----|
| **显式**（👍/👎） | 准但少 | 1-5% 用户给 |
| **隐式**（重问 / 切换 Agent） | 模糊 | 多 |
| **成功信号**（完成交易 / 结束会话） | 间接 | 多 |

### 7.2 反馈 → Dataset 流水线

```
用户 👎 某回答
    │
    ▼
Trace 打标签 "negative_feedback"
    │
    ▼
每日 Batch：
  - 抽样 1000 条负反馈
  - Clustering 归类
  - 每类取 5-10 条候选
    │
    ▼
标注员审核：
  - 是真问题 → 入 Dataset
  - 是用户乱点 → 丢弃
  - 是已知 bug → 触发事件
    │
    ▼
Dataset v2.x 发布
    │
    ▼
CI 回归
```

### 7.3 主动采样（Active Sampling）

不随机采，按"学习价值"采：

- 模型低置信样本
- 多模型分歧大样本
- 覆盖不足的簇

---

## 8. 多维度 Dataset

### 8.1 纵向分类

| 类型 | 规模 | 频率 |
|------|------|------|
| **Smoke** | 20-50 | 每次 CI |
| **Regression** | 200-1000 | 每次 merge |
| **Nightly** | 2000-10000 | 每晚 |
| **Weekly Full** | 10000+ | 每周 |
| **Release Holdout** | 1000 | 版本发布前 |

### 8.2 横向分类

按能力、场景、用户群分：

```
customer-service/
  ├── billing-faq/
  ├── technical-support/
  ├── refund-handling/
  ├── adversarial/
  └── multilingual/
```

### 8.3 多语

- 关键意图每语言 ≥ 50 条
- 翻译 + 本地审核（避免直译怪）
- 文化特化样本

---

## 9. 工具与平台

### 9.1 标注平台

| 工具 | 特色 |
|------|------|
| **Label Studio** | 开源，万金油 |
| **Argilla** | LLM 标注原生 |
| **Scale Data Engine** | 商业，质量高 |
| **Prodigy** | 小团队快标 |
| **SuperAnnotate** | 多模态 |

### 9.2 Dataset 管理

| 工具 | 特色 |
|------|------|
| **HuggingFace Datasets** | 通用开源 |
| **LangSmith Datasets** | 与 Trace 联动 |
| **Langfuse Datasets** | 开源，与 OTel 联动 |
| **Arize Phoenix** | OpenInference 联动 |
| **Braintrust** | Eval + Dataset 一体 |
| **DVC** | Git 风格版本控制 |
| **W&B Tables** | ML 实验联动 |

### 9.3 合成生成

| 工具 | 特色 |
|------|------|
| **DataGen（Nemo）** | NVIDIA |
| **Synthesize（Cohere）** | 企业 |
| **Distilabel** | 开源 |
| **RAGAS** | RAG 测试集生成 |
| **Giskard** | 红队 + 合成 |

---

## 10. Agent 特有的数据需求

### 10.1 Trajectory Dataset

不只是 (input, output)，而是 (input, trajectory, final)：

```json
{
  "input": "帮我订周五的会议",
  "trajectory": [
    {"step": 1, "action": "check_calendar", "obs": "..."},
    {"step": 2, "action": "find_slot", "obs": "..."},
    {"step": 3, "action": "send_invite", "obs": "..."}
  ],
  "final": "已安排周五 2pm"
}
```

Trajectory eval 需要这种数据。

### 10.2 Tool Call Dataset

```json
{
  "input": "北京今天天气",
  "expected_tool_calls": [
    {"name": "get_weather", "args": {"city": "Beijing"}}
  ]
}
```

验证工具选择 + 参数正确性。

### 10.3 Multi-turn Dataset

```json
{
  "turns": [
    {"role": "user", "content": "..."},
    {"role": "assistant", "content": "..."},
    {"role": "user", "content": "..."},
    {"role": "assistant", "content": "..."}  ← 评估这里
  ]
}
```

### 10.4 Adversarial / Red-team Dataset

- Prompt 注入样本
- Jailbreak 样本
- 权限越界尝试
- PII 诱导

必须与常规 Dataset 分离存储。

---

## 11. Dawning 的 Dataset 设计

### 11.1 IDataset

```csharp
public interface IDataset
{
    string Name { get; }
    string Version { get; }
    Task<IAsyncEnumerable<DatasetItem>> ReadAsync(
        string split = "test",
        CancellationToken ct = default);
}

public record DatasetItem(
    string Id,
    JsonElement Input,
    JsonElement? Expected,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<string, object>? Metadata);
```

### 11.2 存储后端

- `Dawning.Agents.Datasets.Local`（JSONL / Parquet）
- `Dawning.Agents.Datasets.HuggingFace`
- `Dawning.Agents.Datasets.Langfuse`
- `Dawning.Agents.Datasets.LangSmith`

### 11.3 Trace → Dataset 流水线

```csharp
services.AddAgentOSKernel()
    .AddDatasetPipeline(p =>
    {
        p.SampleFromTraces(cfg =>
        {
            cfg.Filter = t => t.UserFeedback == Negative;
            cfg.SampleRate = 0.1;
            cfg.RedactPII = true;
            cfg.TargetDataset = "customer-service-negative-feedback";
        });
    });
```

### 11.4 Eval 与 Dataset 联动

```csharp
var dataset = await datasetStore.LoadAsync("customer-service", "v2.1");
var result = await evalRunner.RunAsync(dataset, evaluators, agent);
```

---

## 12. 常见错误

| 错误 | 后果 |
|------|------|
| 无 Golden Set | 升级全靠感觉 |
| Dataset 不版本化 | 无法 reproducibility |
| Holdout 暴露给开发 | 数据泄漏，假高分 |
| 只有 happy path | 生产一遇边缘就翻 |
| 合成数据不过滤 | LLM 自产自消自 overfit |
| 标注准则不清 | κ 低，数据噪声 |
| PII 不脱敏 | 合规灾难 |
| 反馈不回流 | 失败不断重复 |
| Trace 和 Dataset 脱节 | 生产问题无法复现 |
| 只测通用基准 | 在你的业务上不代表质量 |

---

## 13. 落地节奏

**Week 1-2**：业务专家手写 50 个样本（Seed）
**Week 3-4**：合成扩展到 500，人工抽检
**Month 2**：建立 Trace→Dataset 流水线
**Month 3**：接入负反馈回流
**Quarter 2**：季度性 holdout 全面评估
**持续**：每月发布 dataset 增量版本

---

## 14. 小结

> **Agent 的质量 = Dataset 的质量。**
>
> Seed → 合成 → 生产采样 → 反馈回流，是必经的四级火箭。
> 标注准则、版本管理、PII 合规，是工程化的三个支柱。
>
> Dawning 的策略：把 Dataset 当**产品**对待——版本化、Card 化、流水线化，让 Eval 有源头活水。

---

## 15. 延伸阅读

- [[concepts/agent-evaluation.zh-CN]] — Eval 方法论
- [[concepts/observability-deep.zh-CN]] — Trace 采样
- [[concepts/skill-evolution.zh-CN]] — Dataset 驱动的演化
- Langfuse Datasets：<https://langfuse.com/docs/datasets>
- Argilla：<https://argilla.io/>
- Distilabel：<https://distilabel.argilla.io/>
- AgentBench：<https://github.com/THUDM/AgentBench>
- τ-Bench：<https://github.com/sierra-research/tau-bench>
