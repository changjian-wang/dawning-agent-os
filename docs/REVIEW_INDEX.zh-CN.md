---
title: "知识库 Review 索引（2026-04-18 快照）"
type: meta
tags: [review, index, meta, tldr, cross-reference]
created: 2026-04-18
updated: 2026-04-18
status: active
---

# 知识库 Review 索引

本文件是 **审阅辅助清单**，汇总近期 7 个 Batch（A-NN）新增 / 修改的所有文档，提供：

- 每份文档 **一行 TL;DR**
- 主题聚类
- 交叉引用关系图
- 建议审阅顺序
- 一致性自检清单

---

## 1. 总览

- **新增**：40 份
- **修改**：4 份（含 1 张架构图 + 3 个 landscape 文档）
- **总覆盖**：基础概念 → 模块对比 → 产品剖析 → 经济生态

---

## 2. 分主题索引（附 TL;DR）

### 2.1 基础与协议（6）

| 文件 | TL;DR |
|------|------|
| [concepts/agent-loop.md](concepts/agent-loop.md) | Agent 循环抽象（§2.4 新增） |
| [concepts/protocols-a2a-mcp.zh-CN.md](concepts/protocols-a2a-mcp.zh-CN.md) | MCP（工具协议）/ A2A（Agent 互通）规范 |
| [concepts/dawning-capability-matrix.zh-CN.md](concepts/dawning-capability-matrix.zh-CN.md) | Dawning 8 层 × 16 接口能力矩阵 |
| [comparisons/framework-modules-mapping.zh-CN.md](comparisons/framework-modules-mapping.zh-CN.md) | 主流框架模块 ↔ Dawning 分层映射 |
| [comparisons/agent-framework-landscape.zh-CN.md](comparisons/agent-framework-landscape.zh-CN.md) | 框架全景（已有，本批嵌图） |
| [comparisons/agent-os-vs-frameworks.md](comparisons/agent-os-vs-frameworks.md) | Agent OS vs Framework 定位（本批嵌图） |

### 2.2 记忆与知识（5）

| 文件 | TL;DR |
|------|------|
| [concepts/memory-architecture.zh-CN.md](concepts/memory-architecture.zh-CN.md) | Working/Episodic/Semantic/Procedural 记忆分层 |
| [concepts/embedding-models.zh-CN.md](concepts/embedding-models.zh-CN.md) | Embedding 模型选型（BGE/Qwen3/OpenAI） |
| [concepts/next-gen-rag.zh-CN.md](concepts/next-gen-rag.zh-CN.md) | GraphRAG / HippoRAG / Self-RAG / ColPali / Agentic RAG |
| [comparisons/rag-pipeline-comparison.zh-CN.md](comparisons/rag-pipeline-comparison.zh-CN.md) | RAG 主流 pipeline 结构对比 |
| [comparisons/vector-database-comparison.zh-CN.md](comparisons/vector-database-comparison.zh-CN.md) | Qdrant/Milvus/Weaviate/pgvector/LanceDB 等对比 |

### 2.3 推理与训练（5）

| 文件 | TL;DR |
|------|------|
| [concepts/reasoning-algorithms.zh-CN.md](concepts/reasoning-algorithms.zh-CN.md) | ReAct / CoT / ToT / Plan-Execute 等算法 |
| [concepts/reasoning-models.zh-CN.md](concepts/reasoning-models.zh-CN.md) | o1 / R1 / QwQ / Gemini Thinking 推理模型 |
| [concepts/inference-time-search.zh-CN.md](concepts/inference-time-search.zh-CN.md) | Best-of-N / MCTS / PRM / Self-Consistency |
| [concepts/prompt-engineering-dspy.zh-CN.md](concepts/prompt-engineering-dspy.zh-CN.md) | Prompt 工程 + DSPy 声明式编程 |
| [concepts/post-training.zh-CN.md](concepts/post-training.zh-CN.md) | SFT/DPO/KTO/ORPO/RLVR/GRPO + PEFT |

### 2.4 Agent 形态（6）

| 文件 | TL;DR |
|------|------|
| [concepts/multi-agent-patterns.zh-CN.md](concepts/multi-agent-patterns.zh-CN.md) | Supervisor / Swarm / Debate / Handoff 多 Agent 模式 |
| [concepts/multimodal-agents.zh-CN.md](concepts/multimodal-agents.zh-CN.md) | 多模态 Agent（VLM/ASR/TTS/视频/机器人）|
| [comparisons/agentic-coding-deep-dive.zh-CN.md](comparisons/agentic-coding-deep-dive.zh-CN.md) | Cursor/Cline/Aider/Claude Code/Devin/OpenHands |
| [comparisons/computer-use-agents.zh-CN.md](comparisons/computer-use-agents.zh-CN.md) | Anthropic CU / Operator / Browser-Use / UFO |
| [comparisons/research-agents.zh-CN.md](comparisons/research-agents.zh-CN.md) | Deep Research / AI Scientist / AlphaEvolve / STORM |
| [comparisons/workflow-vs-agent.zh-CN.md](comparisons/workflow-vs-agent.zh-CN.md) | 工作流引擎 vs Agent：Temporal/Restate/Dapr/Airflow |

### 2.5 工具与结构化（3）

| 文件 | TL;DR |
|------|------|
| [comparisons/function-calling-comparison.zh-CN.md](comparisons/function-calling-comparison.zh-CN.md) | OpenAI/Anthropic/Gemini function calling 方言对比 |
| [concepts/structured-output.zh-CN.md](concepts/structured-output.zh-CN.md) | JSON Mode / Structured Output / Outlines |
| [concepts/skill-evolution.zh-CN.md](concepts/skill-evolution.zh-CN.md) | Tool → Skill → Package 演进 |

### 2.6 基础设施与部署（6）

| 文件 | TL;DR |
|------|------|
| [comparisons/llm-gateway-comparison.zh-CN.md](comparisons/llm-gateway-comparison.zh-CN.md) | LiteLLM/Portkey/Helicone/OpenRouter 网关 |
| [comparisons/local-llm-comparison.zh-CN.md](comparisons/local-llm-comparison.zh-CN.md) | Ollama/vLLM/TGI/llama.cpp 本地推理 |
| [comparisons/polyglot-agent-ecosystem.zh-CN.md](comparisons/polyglot-agent-ecosystem.zh-CN.md) | Python/TS/.NET/Java/Go 生态对比 |
| [concepts/deployment-architectures.zh-CN.md](concepts/deployment-architectures.zh-CN.md) | 部署架构（单体/微服务/Serverless） |
| [concepts/state-persistence.zh-CN.md](concepts/state-persistence.zh-CN.md) | LangGraph Checkpoint / Temporal / Letta / Event Sourcing |
| [concepts/edge-small-models.zh-CN.md](concepts/edge-small-models.zh-CN.md) | Phi-4/Gemini Nano/Apple Foundation + Core ML/MLX |

### 2.7 观测与治理（5）

| 文件 | TL;DR |
|------|------|
| [concepts/observability-deep.zh-CN.md](concepts/observability-deep.zh-CN.md) | OTel / Langfuse / Phoenix / Braintrust 深度 |
| [concepts/cost-optimization.zh-CN.md](concepts/cost-optimization.zh-CN.md) | Token/Cache/Router/Batch 成本治理 |
| [concepts/agent-evaluation.zh-CN.md](concepts/agent-evaluation.zh-CN.md) | Eval 框架（RAGAS/DeepEval/Phoenix/Braintrust） |
| [concepts/agent-ux-patterns.zh-CN.md](concepts/agent-ux-patterns.zh-CN.md) | Streaming / HITL / Generative UI 模式 |
| [concepts/ai-compliance.zh-CN.md](concepts/ai-compliance.zh-CN.md) | EU AI Act / NIST / ISO 42001 合规 |

### 2.8 安全与身份（2）

| 文件 | TL;DR |
|------|------|
| [concepts/agent-security.zh-CN.md](concepts/agent-security.zh-CN.md) | Prompt injection / Tool 滥用 / Sandbox |
| [concepts/agent-identity-auth.zh-CN.md](concepts/agent-identity-auth.zh-CN.md) | OAuth 2.1 / Token Exchange / MCP Auth / Zero Trust |

### 2.9 数据与生态（3）

| 文件 | TL;DR |
|------|------|
| [concepts/dataset-building.zh-CN.md](concepts/dataset-building.zh-CN.md) | 数据集构建（SFT/DPO/Eval） |
| [concepts/enterprise-roadmap.zh-CN.md](concepts/enterprise-roadmap.zh-CN.md) | 企业 Agent 落地 90 天路线 |
| [comparisons/agent-marketplace.zh-CN.md](comparisons/agent-marketplace.zh-CN.md) | GPTs Store / Agent Space / x402 / 定价模型 |

### 2.10 对比（已存在，已扩展交叉引用）

| 文件 | TL;DR |
|------|------|
| [comparisons/maf-vs-langgraph.zh-CN.md](comparisons/maf-vs-langgraph.zh-CN.md) | Microsoft Agent Framework vs LangGraph 深度对比 |

---

## 3. 阅读路径推荐

### 3.1 新人入门路径

```
1. agent-loop (§2.4)
2. dawning-capability-matrix
3. framework-modules-mapping
4. agent-framework-landscape
5. maf-vs-langgraph
```

### 3.2 架构师路径

```
1. dawning-capability-matrix
2. framework-modules-mapping
3. workflow-vs-agent
4. state-persistence
5. deployment-architectures
6. enterprise-roadmap
```

### 3.3 研发路径（写 Agent）

```
1. reasoning-algorithms
2. multi-agent-patterns
3. structured-output
4. function-calling-comparison
5. memory-architecture
6. next-gen-rag
7. prompt-engineering-dspy
```

### 3.4 SRE / 运维路径

```
1. observability-deep
2. cost-optimization
3. agent-evaluation
4. state-persistence
5. deployment-architectures
6. llm-gateway-comparison
```

### 3.5 安全 / 合规路径

```
1. agent-security
2. agent-identity-auth
3. ai-compliance
4. computer-use-agents (§安全章节)
```

### 3.6 前沿 / 产品路径

```
1. reasoning-models
2. inference-time-search
3. agentic-coding-deep-dive
4. computer-use-agents
5. research-agents
6. agent-marketplace
```

### 3.7 模型 / 训练路径

```
1. post-training
2. embedding-models
3. edge-small-models
4. local-llm-comparison
5. dataset-building
```

---

## 4. 核心交叉引用（链接健康度自检）

以下是应审阅的链接对（文档 A ↔ 文档 B 是否双向互引）：

| 关键配对 | 期望双向 |
|---------|---------|
| reasoning-models ↔ inference-time-search | ✅ |
| reasoning-models ↔ post-training | ✅ |
| memory-architecture ↔ next-gen-rag | ✅ |
| agent-security ↔ agent-identity-auth | ✅ |
| agent-identity-auth ↔ protocols-a2a-mcp | ✅ |
| workflow-vs-agent ↔ state-persistence | ✅ |
| computer-use-agents ↔ multimodal-agents | 待检 |
| agentic-coding-deep-dive ↔ research-agents | 待检 |
| edge-small-models ↔ local-llm-comparison | ✅ |
| cost-optimization ↔ llm-gateway-comparison | 待检 |
| agent-marketplace ↔ skill-evolution | ✅ |
| agent-marketplace ↔ agent-identity-auth | ✅ |

---

## 5. 一致性自检清单

### 5.1 YAML Front-matter

- [ ] 每份文档都含 `title / type / tags / sources / created / updated / status`
- [ ] `type` 值为 `concept` | `comparison` | `meta`
- [ ] `created` 在 2026-04-17 或 2026-04-18
- [ ] `status: active`

### 5.2 Dawning 策略段

每份文档末尾（延伸阅读前）是否包含：
- [ ] "Dawning 策略" 或等价章节
- [ ] 明确声明"不做什么"（边界）
- [ ] 明确给出接口 / Layer 映射
- [ ] 复用主流生态，不重造轮子

### 5.3 篇幅

- [ ] 每文 300-600 行
- [ ] 不超过 700 行（避免上下文过大）
- [ ] 有章节编号（20 左右）

### 5.4 语言与术语

- [ ] 中文为主，保留英文专有名词
- [ ] "工具调用"统一（不与"函数调用"混用，除非讨论 API 方言时）
- [ ] "Agent"保留英文，不译为"智能体"（除非特殊语境）
- [ ] "Skill"保留英文
- [ ] 层级用 "Layer 0/1/2..." 格式

### 5.5 交叉链接

- [ ] 使用 Obsidian 格式 `[[path/file]]`
- [ ] 延伸阅读段至少 5 条内部链接 + 5 条外部 URL
- [ ] 外部 URL 应是官方 / 论文（非博客碎片）

### 5.6 结构模式

每份文档建议包含：
- [ ] 1. 引言 / 定义
- [ ] 2-N. 主体对比 / 分析
- [ ] N+1. 反模式 / 局限
- [ ] N+2. 趋势 / 展望
- [ ] 倒数第二章：Dawning 策略
- [ ] 最后：小结
- [ ] 延伸阅读

---

## 6. 已知待优化项（Review 时可关注）

1. **部分文档长度超 550 行**：computer-use-agents、post-training、inference-time-search 可考虑拆分
2. **reasoning-algorithms vs reasoning-models vs inference-time-search** 三者易混淆，可在各自开头加"与 XXX 区别"注释
3. **agent-security vs agent-identity-auth** 边界：security 覆盖全面威胁，auth 专精身份/授权，已区分但可加顶部提示
4. **workflow-vs-agent vs state-persistence** 关联紧密，可互相补充"See also"
5. **部分外部 URL 2026 可能失效**：发布前可运行 link check
6. **中英文名词** 个别处"微调"与"fine-tune"混用，可统一
7. **成本数字** （如 o1 $50-200）会过时，可加"数据截至 2026-Q1"
8. **图片资源** 仅少数文档引用架构图，可补充（可选）

---

## 7. 建议的后续 Batch 主题（若继续写）

按优先级：

| 代号 | 主题 | 价值 |
|-----|------|------|
| OO | Agent CI/CD & Regression Testing | 工程成熟度 |
| PP | Prompt 版本治理 / Promptfoo / LangSmith | DevOps |
| QQ | 世界模型 / 具身 Agent / 机器人 | 前沿 |
| RR | Embedding 微调 + Hard Negative Mining | 深度专题 |
| SS | Agent + DDD / Clean Architecture | 软件工程融合 |
| TT | Agent 可解释性（Interpretability） | 研究前沿 |
| UU | GenAI + 传统 ML 管道 | 企业现状 |
| VV | Streaming 架构（SSE/WebSocket/gRPC） | 工程细节 |

---

## 8. Review 完成后 Action Items（模板）

Review 过程记录：

- [ ] 所有 YAML 合规
- [ ] 所有交叉链接可访问
- [ ] 所有外部 URL 存活（或有 fallback）
- [ ] 术语统一
- [ ] 无重复内容（<15% 重叠）
- [ ] 结构一致
- [ ] 已签收：_________
- [ ] 日期：_________

---

## 9. 文件数量清单

```
docs/concepts/        27 份（新 + 修）
docs/comparisons/     13 份（新 + 修）
docs/entities/frameworks/ 1 份（修改）
docs/ (meta)           1 份（本文件）
———————
合计                  42 份变动
```

---

## 10. 延伸阅读

- [[index]] — 知识库主索引
- [[comparisons/agent-framework-landscape.zh-CN]] — 框架全景
- [[concepts/dawning-capability-matrix.zh-CN]] — 能力矩阵
