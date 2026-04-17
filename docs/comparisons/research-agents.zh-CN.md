---
title: "自主研究 Agent：Deep Research、AI Scientist、AlphaEvolve、STORM"
type: comparison
tags: [research-agent, deep-research, ai-scientist, alphaevolve, storm, autonomous, long-horizon]
sources: [comparisons/agentic-coding-deep-dive.zh-CN.md, concepts/reasoning-models.zh-CN.md]
created: 2026-04-18
updated: 2026-04-18
status: active
---

# 自主研究 Agent：Deep Research、AI Scientist、AlphaEvolve、STORM

> 2025 出现一种新型 Agent：**不是帮你答一个问题，而是替你做完一项研究**。
> - OpenAI Deep Research：30 分钟写出带引用的行业报告
> - Google Gemini Deep Research：同类产品
> - Perplexity Deep Research：搜索 + 推理
> - Sakana AI Scientist：端到端生成 ML 论文
> - DeepMind AlphaEvolve：进化式算法发现
> - Stanford STORM：Wiki 风格研究
>
> 这类 Agent 的共同点：**长时自主 + 工具密集 + 知识综合 + 可引用 + 可复核**。
> 本文拆解 Research Agent 的技术范式、对比主流产品、讨论落地路径。

---

## 1. 定义与范畴

### 1.1 什么是 Research Agent

输入：一个研究问题 / 任务
输出：经多步搜索 + 推理 + 综合后的长文档 / 代码 / 发现
特征：
- 运行数十分钟到数小时
- 执行数十到数千次工具调用
- 产出带引用、可验证
- 自主规划 + 自我修正

### 1.2 与普通 QA Agent 区别

| 维度 | QA Agent | Research Agent |
|------|---------|---------------|
| 时长 | 秒级 | 分钟-小时 |
| 步骤 | 1-5 | 数十-数百 |
| 工具调用 | 少 | 数十-数千 |
| 输出 | 答案 | 报告 / 论文 / 代码 |
| 引用 | 可选 | 必需 |
| 可复核 | 低 | 高（reasoning + sources） |

---

## 2. 技术范式三大流派

### 2.1 Search-Compile（搜索-编译派）

代表：Deep Research 类

```
问题 → 生成检索计划 → 多轮 Web 搜索 + 页面阅读
     → 知识提取 + 整合 → 大纲生成 → 分节写作 → 引用校对
```

特征：
- 重浏览器 / Web search 工具
- 长上下文或 RAG 管理巨量资料
- 结构化产出

### 2.2 Experiment-Iterate（实验-迭代派）

代表：AI Scientist / ML Agent Bench

```
课题 → 假设 → 设计实验 → 执行 → 分析 → 修正假设 → ...
```

特征：
- 重代码执行
- 实验记录与数据可视化
- 科研自动化

### 2.3 Evolve-Discover（进化-发现派）

代表：AlphaEvolve / FunSearch

```
初始候选 → LLM 变体生成 → 自动评估（可执行测试）
         → 选择最优 → 再进化 → ...
```

特征：
- 进化算法 + LLM 变体器
- 评分函数是核心
- 用于数学 / 算法发现

---

## 3. Deep Research 产品对比

### 3.1 主流产品矩阵

| 产品 | 出品 | 基础模型 | 工具 | 定位 |
|------|------|---------|------|------|
| **OpenAI Deep Research** | OpenAI | o3 / o4 | Browser, Code | 行业通用 |
| **Gemini Deep Research** | Google | Gemini 2.5 Pro Thinking | Browser | 通用 |
| **Perplexity Deep Research** | Perplexity | 自研 + 多模型 | Search, Browser | 搜索强 |
| **You.com Research** | You | 多模型 | Search | 通用 |
| **Claude Research** | Anthropic | Claude 4 Opus | Browser, Code | 推理强 |
| **xAI Grok DeepSearch** | xAI | Grok 3 | Search, X | 含社交 |
| **Kimi Researcher** | Moonshot | Kimi k2 | Browser | 中文强 |
| **Doubao Research** | ByteDance | Doubao | Search, Browser | 中国 |
| **DeerFlow** | ByteDance | 开源 | 自定义 | 开源 |
| **GPT-Researcher** | 开源 | 任意 LLM | Search | 开源经典 |

### 3.2 使用体验对比

| 产品 | 时长 | 报告长度 | 引用质量 | 可导出 |
|------|------|---------|---------|--------|
| OpenAI DR | 5-30min | 10-50 页 | 高 | PDF/MD |
| Gemini DR | 5-15min | 5-30 页 | 高 | Docs |
| Perplexity | 2-10min | 中 | 高 | MD/Web |
| Claude Research | 10-30min | 中-长 | 高 | MD |
| GPT-Researcher | 自控 | 自控 | 中 | MD/PDF |

---

## 4. OpenAI Deep Research 深度剖析

### 4.1 架构推测

```
Planner (o3 Thinking)
  ↓ 生成检索大纲 (10-30 子问题)
Research Loop:
  for each subtopic:
    Browser Agent 多轮搜索 + 阅读
    提取关键信息 + 引用
  Memory: scratchpad 整合
Writer (o3 / o4)
  ↓ 结构化长报告
Citation Verifier
```

### 4.2 关键技术

- **o3 reasoning**：深度反思 + 修正
- **Browser tool**：完整浏览能力
- **Citation enforcement**：每句带来源
- **Iterative refinement**：发现不足自补

### 4.3 强项

- 引用质量高（学术级）
- 结构化清晰
- 覆盖广度

### 4.4 局限

- 慢（30min 用户体验挑战）
- 成本高（$5-50 / 次）
- 仍会幻觉（需人复核）
- 上下文有限（超长话题分不清）

---

## 5. AI Scientist（Sakana）

### 5.1 目标

端到端自动化 ML 研究：
从课题 → 实验 → 论文 → 同行评审

### 5.2 流程

```
1. Idea Generation (LLM brainstorm)
2. Novelty Check (搜现有论文)
3. Experiment Design
4. Code Generation (基于 template)
5. Execution (GPU)
6. Data Analysis + 图表
7. Paper Writing (LaTeX)
8. Review (LLM 反驳)
9. Revise (循环)
```

### 5.3 v2（2025）

- 更强 Planner
- 长 context + tool use
- 实际发表多篇可读论文

### 5.4 争议

- 能否做出真正新发现？
- vs. 人类研究者：**深度浅**、**新颖性一般**、**但广度高**
- 未来：辅助 > 替代

### 5.5 启示

- 研究也能流水线化
- Agent + 实验环境 = 科研副驾

---

## 6. AlphaEvolve（DeepMind）

### 6.1 目标

LLM 驱动的进化式算法发现。

### 6.2 核心循环

```
Seed Program (初始代码)
  → LLM Mutator: 生成多个变体 (prompt + examples)
  → Evaluator: 自动测试 (分数函数)
  → 选择 Top-K
  → 回到 Mutator
  → 数百-数千代
```

### 6.3 成果

- 改进 4x4 矩阵乘法（数学）
- 优化 Google 数据中心调度算法
- 发现新 sorting / hashing 片段

### 6.4 关键条件

- **可自动评分**（有 ground truth / benchmark）
- **搜索空间大但可枚举局部**
- **LLM 有足够专业先验**

### 6.5 适用领域

- 数学证明片段
- 算法优化
- 编译器优化
- 电路 / 芯片设计

### 6.6 不适用

- 难以自动评估的创作
- 需人类主观判断的产出

---

## 7. STORM (Stanford)

### 7.1 Wiki 风格研究

```
问题 → 视角生成 (多专家视角)
     → 每视角 Web 搜索 + 笔记
     → 大纲合成 (Wiki 风格)
     → 分节写作
     → 编辑整合
```

### 7.2 Co-STORM（协作版）

- 多 Agent 讨论 + 人类插入问题
- 适合探索性主题

### 7.3 开源

- <https://github.com/stanford-oval/storm>
- .NET / Python 可学习

### 7.4 启示

- "视角"比"子问题"更能组织知识
- 仿 Wiki 结构帮人快速消化

---

## 8. GPT-Researcher 家族（开源）

### 8.1 特点

- 简洁架构
- 可自托管
- 任意 LLM

### 8.2 流程

```
主问题 → 子问题列表 → 各子问题独立搜索
                  → 各写小报告 → 汇总
```

### 8.3 局限

- 简单 → 深度不够
- 无推理模型加持时质量一般

### 8.4 变体

- **TogetherAI Open Deep Research**
- **DeerFlow** (ByteDance 开源)
- **Dify Deep Research 模板**

---

## 9. 常见工具栈

### 9.1 搜索

- Tavily / Serper / Exa / Brave Search / Google CSE
- Perplexity API
- 自建 SearXNG

### 9.2 浏览

- Browser-Use
- Playwright + 封装
- Apify / Browserbase
- Firecrawl（专业抓取）

### 9.3 代码执行

- E2B / Modal Sandbox
- Riza / Judge0
- 自建 Firecracker

### 9.4 向量 / 知识

- Qdrant / LanceDB / PostgreSQL+pgvector
- Unstructured / LlamaParse for PDF
- ArXiv / PubMed API

### 9.5 引用校验

- 自定义 checker：逐句映射来源
- PDF 段落提取

---

## 10. 工程挑战

### 10.1 上下文爆炸

- 数十个 PDF + 数百网页
- 方案：分层总结、RAG、滑窗

### 10.2 成本爆炸

- 长 reasoning + 大量 token
- 方案：混合 router（小模型筛，大模型综合）

### 10.3 时长 UX

- 用户等不了 30 分钟
- 方案：Streaming 中间产物、任务化后台

### 10.4 可复核

- 用户难验证 30 页报告
- 方案：每条 claim 带 inline source、Source Map UI

### 10.5 幻觉

- 即使带引用，"link 对但内容编"
- 方案：Verifier Agent 二审

### 10.6 终止判断

- Agent 不知道"够了"
- 方案：budget（时长 / token / 步数）+ 自评完成度

---

## 11. 评估基准

| 基准 | 范围 | 说明 |
|------|------|------|
| **HLE (Humanity's Last Exam)** | 通用 | 博士级难题 |
| **GAIA** | 助理 | 工具 + 多步 |
| **BrowseComp** | 浏览 | 难查找问题 |
| **DeepResearchBench** | 报告 | 综合能力 |
| **AgentClinic / MedQA-Deep** | 医疗研究 | 专业 |
| **SciResearchBench** | 科研 | 实验设计 |

---

## 12. 企业落地场景

### 12.1 市场研究

- 竞品分析、行业报告、尽调

### 12.2 投研

- 公司分析、财报解读、产业链研究

### 12.3 法律

- 判例研究、合规分析、尽调

### 12.4 医疗

- 文献综述、药物研究、临床指南更新

### 12.5 咨询

- 客户方案底稿

### 12.6 学术

- 文献综述（需人复核）

### 12.7 工程

- Architecture decision 研究
- 选型评估
- 兼容性调研

---

## 13. 安全与合规

### 13.1 版权

- Web 抓取合规
- 引用必须准确
- Fair use 界限

### 13.2 机密

- 企业内 Deep Research 需接私域
- 禁出域策略

### 13.3 误导

- 幻觉报告可能误导决策
- 必须标"AI 生成，需核"
- 禁止用于医/法/金直接决策

### 13.4 偏见

- 来源偏见传递
- 多样化来源 + 标注立场

---

## 14. 自建 Research Agent 路径

### 14.1 MVP（1 周）

- GPT-Researcher 开源起步
- 接 1-2 搜索 API
- 1 次性报告

### 14.2 Level 2

- 加 Reasoning 模型
- 多轮 refine
- 引用校验

### 14.3 Level 3

- 专域知识源（ArXiv / 内部 Wiki）
- 领域 LoRA / prompt
- 自评机制

### 14.4 Level 4（产品级）

- 多 Agent 协作（Planner + Searchers + Writer + Reviewer）
- Memory（跨任务知识累积）
- 成本治理
- UI：streaming + source map + 交互插入

---

## 15. Dawning 与 Research Agent

### 15.1 模式适配

Dawning 用 **Skill Set** 承载："deep-research" skill：
- Plan skill
- Browse skill
- Summarize skill
- Write skill
- Review skill

### 15.2 Layer 组合

- Layer 2 Working Memory：scratchpad
- Layer 3 Vector Store：抓取资料 RAG
- Layer 4 ReAct / Plan-Execute
- Layer 6 Skill：研究流程编排
- Layer 7 Citation Policy：强制引用

### 15.3 长时任务

- `IWorkflow` 接口 → 适配 Temporal / Durable
- Checkpoint 可恢复
- Streaming 产物

### 15.4 企业版

- 私域搜索（Elasticsearch / Azure AI Search）
- 接 SharePoint / Confluence
- 审计 & 引用本地化

### 15.5 不做什么

- Dawning 不自建搜索引擎
- 不自建浏览器自动化（接 Browser-Use / Playwright MCP）
- 不自建 PDF 解析（接 LlamaParse / Unstructured）

---

## 16. 展望

- 2026：Research Agent 进入企业标配
- 2027：领域专用 Research Agent（法律 / 医学 / 金融）超越通用
- 2028：Research + Experiment 融合（设计 → 执行 → 验证闭环）
- 长线：AI Scientist 产出可独立发表 research

---

## 17. 小结

> Research Agent 是 **Agent 长时自主 + 工具密集 + 知识综合** 的首个杀手级形态。
> - 搜索-编译派（Deep Research）已经可用
> - 实验-迭代派（AI Scientist）在突破
> - 进化-发现派（AlphaEvolve）展示了专业领域价值
>
> 共性问题：**上下文、成本、时长、可信、终止**。
> Dawning 的角色：提供 Skill 编排、长时任务可靠性、私域检索、引用治理，让企业 Research Agent 落地。

---

## 18. 延伸阅读

- [[comparisons/agentic-coding-deep-dive.zh-CN]] — 代码 Agent
- [[concepts/reasoning-models.zh-CN]] — 推理模型
- [[concepts/next-gen-rag.zh-CN]] — 知识检索
- [[concepts/multi-agent-patterns.zh-CN]] — 多 Agent 协作
- [[concepts/state-persistence.zh-CN]] — 长任务可靠
- OpenAI Deep Research: <https://openai.com/index/introducing-deep-research/>
- Gemini Deep Research: <https://gemini.google/overview/deep-research/>
- Sakana AI Scientist: <https://sakana.ai/ai-scientist/>
- AlphaEvolve: <https://deepmind.google/discover/blog/alphaevolve/>
- Stanford STORM: <https://storm.genie.stanford.edu/>
- GPT-Researcher: <https://github.com/assafelovic/gpt-researcher>
- DeerFlow: <https://github.com/bytedance/deer-flow>
