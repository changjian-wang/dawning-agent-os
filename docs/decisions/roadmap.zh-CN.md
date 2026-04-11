---
title: "路线图：分层学习路径"
type: decision
tags: [roadmap, architecture, learning-path]
sources: [decisions/success-criteria.md, decisions/success-criteria.zh-CN.md]
created: 2026-04-08
updated: 2026-04-08
status: active
---

# 路线图：分层学习路径

> 以知识依赖为序，逐层构建企业级分布式多 Agent .NET 框架（含技能自演化能力）。

## 指导原则

- **深度优先于速度** —— 每一层先彻底理解，再动手实现。
- **依赖驱动排序** —— 每一层依赖前一层，不跳层。
- **学 → 设计 → 建 → 验** —— 每一层产出：框架研究笔记、设计决策文档（`decisions/`）、实现 + 测试、已验证 SC 项。
- **dawning-assistant 作为第一消费者** —— 每个特性必须被真实使用。

## 依赖图

![Layer 依赖图](../images/decisions/06-layer-dependency.png)

---

## Layer 0：LLM Provider 抽象

**前置依赖**：无。

**需要掌握的知识**：
- 流式协议（SSE、chunked transfer）
- Token 计数与上下文窗口管理
- Function calling / tool-use schema（OpenAI、Ollama、Azure OpenAI）
- Provider 故障转移与重试模式

**交付物**：
- `ILLMProvider` 接口（ChatAsync、ChatStreamAsync、ChatStreamEventsAsync）
- 统一流式事件模型（TextDelta、ToolCallRequested、ToolCallCompleted、RunCompleted、Error）
- 至少 2 个 Provider：Ollama（本地）+ OpenAI（远程）
- 每次调用的 token 用量 / 延迟 / 成本追踪
- Provider 契约测试套件

**验证**：SC-7（LLM Provider 层）

**设计文档**：`decisions/llm-provider-design.md`

---

## Layer 1：Agent 循环与 Tool 协议

**前置依赖**：Layer 0。

**需要掌握的知识**：
- 原生 Function Calling / Tool Use 协议（OpenAI `tool_calls`、Anthropic `tool_use`、Ollama）
- Agent 执行循环：prompt 组装 → LLM 调用 → tool call 决策（模型原生）→ tool 执行 → 结果注入 → 循环 / 终止
- 并行 tool calling（现代模型单次响应返回多个 tool call）
- Tool 协议标准化：MCP（Model Context Protocol）作为新兴标准
- Structured Output（JSON mode、`response_format`）实现可靠解析
- Tool 定义 schema（JSON Schema）与结果编组
- Stateful Prompt 组装（系统指令 + 技能 + 记忆上下文 + 工具定义）
- 循环终止策略：模型自主停止 vs 最大步骤数 / 最大 token 预算
- 执行期间 Prompt 不可变性

**交付物**：
- 核心 Agent 循环：prompt 组装 → LLM 调用 → 原生 tool call 解析 → tool 分发 → 结果注入 → 重复 / 终止
- 并行 tool call 支持（模型请求时并发分发多个 tool）
- 通过 DI 注册 Tool（含 JSON Schema 定义）
- MCP 兼容的 Tool 协议抽象
- 非 tool 响应的 Structured Output 支持
- StatefulPrompt record（带版本号）
- 连续运行间的 StatefulPrompt diff
- 最大步骤数和最大 token 预算执行

**验证**：SC-3（Stateful Prompt 协议）

**设计文档**：`decisions/agent-loop-design.md`

---

## Layer 2：记忆系统

**前置依赖**：Layer 1。

> **注意**：长期记忆（向量检索）方案仍在评估中，存在已知局限（详见 [concepts/context-management.md](../concepts/context-management.md)）。本层当前仅确认短期记忆部分，长期记忆交付物待定。

**需要掌握的知识**：
- 上下文窗口管理策略（buffer、滑动窗口、摘要压缩）
- 无知识丢失的记忆压缩
- Embedding 模型与向量相似度搜索（长期记忆预研）
- 向量数据库的已知局限与替代方案

**交付物**：
- 可配置策略的短期记忆（buffer / 滑动窗口 / 摘要）
- 超出上下文窗口阈值时自动压缩
- `ILongTermMemory` 接口定义（预留扩展点，不实现具体后端）
- 长期记忆方案评估报告

**验证**：SC-8（记忆系统）

**设计文档**：`decisions/memory-system-design.md`

---

## Layer 3：多 Agent 编排

**前置依赖**：Layer 1 + Layer 2。

**需要掌握的知识**：
- 工作流原语：顺序、并行、条件分支、重试/补偿
- Handoff 协议：所有权转移、上下文快照、预算传递
- 委托深度控制
- 共享记忆命名空间模型（global / team / session / private）

**交付物**：
- 支持 4 种工作流原语的编排器
- 原子 handoff 契约（HandoffEnvelope）
- 可配置委托深度（超限策略违反停止）
- 四级记忆 scope 隔离
- 5-Agent 确定性集成测试（含完整审计轨迹）

**验证**：SC-2（多 Agent 协作）

**设计文档**：`decisions/multi-agent-orchestration-design.md`

---

## Layer 4：技能路由器（Read Phase）

**前置依赖**：Layer 2 + Layer 3。

**需要掌握的知识**：
- 多信号评分模型（语义相似度、成功率、时间衰减、失败模式）
- 在线反馈环（无需权重更新的效用信号）
- 置信度阈值与回退策略
- 路由基准测试设计

**交付物**：
- 路由器接受 prompt 状态 + 任务描述 + 用户提示 → top-k 评分技能
- 5 个评分维度：语义相似度、历史成功率、时间衰减、失败模式匹配、when-to-use 元数据
- 可配置置信度阈值（低于阈值回退到完整注入或用户澄清）
- 在线反馈：每次运行后按所选技能发送效用信号
- Top-5 命中率 >= 85%（基准集 >= 30 场景）

**验证**：SC-4（技能路由器）

**设计文档**：`decisions/skill-router-design.md`

---

## Layer 5：技能演化（Write Phase）

**前置依赖**：Layer 4。

**需要掌握的知识**：
- 反思流水线（轨迹摘要、根因分析）
- 候选补丁生成（结构化 markdown diff）
- 质量门禁：lint、策略合规、回归评测、人工审批
- 技能工件 schema：intent、when-to-use、limitations、failure-patterns、examples、revision-metadata
- 灰度发布与自动回滚

**交付物**：
- 运行后反思流水线 → 结构化轨迹摘要
- 发现改进机会时生成候选技能补丁
- 4 阶段质量门禁（lint → policy → regression → 可选人工审批）
- 版本化技能注册表（仅追加，任意历史版本可检索）
- 可配置流量百分比的灰度发布
- 指标下降时自动回滚
- 废弃技能治理流程（废弃 → 宽限期 → 归档）

**验证**：SC-5（反思技能演化）+ SC-6（技能生命周期管理）

**设计文档**：`decisions/skill-evolution-design.md`

---

## Layer 6：分布式架构（三面体）

**前置依赖**：Layer 3 + Layer 5。

**需要掌握的知识**：
- 控制面设计：Agent 注册表、策略存储、技能生命周期管理器、评测调度器
- 运行面设计：任务队列消费者、持久检查点、租约/心跳、优雅排空
- 记忆面作为独立可扩展服务
- 异步消息契约：AgentTaskEnvelope、HandoffEnvelope、SkillArtifact、PolicyDecision
- 幂等协议与混沌测试
- Schema 演进（仅追加、版本化注册）

**交付物**：
- 控制面 4 组件（注册表、策略存储、技能生命周期、评测调度器）
- 运行面（持久检查点/恢复 + 滚动部署）
- 记忆面拆分为独立的短期和长期服务
- 版本化异步契约（含 correlation/causation ID）
- 传输抽象（`IMessageBus`）：内存 + 持久后端
- 混沌测试验证的幂等协议

**验证**：SC-1（分布式架构）

**设计文档**：`decisions/distributed-architecture-design.md`

---

## Layer 7：治理、合规与可观测性

**前置依赖**：Layer 6。

**需要掌握的知识**：
- 工具执行、技能发布、Agent 部署、记忆访问的 RBAC 模型
- 不可变审计日志设计（仅追加、防篡改）
- 日志和追踪中的 PII/密钥脱敏
- 工具执行策略引擎（allowlist、denylist、风险等级门禁）
- OpenTelemetry span 层级
- CI/CD 中的 SLO 定义与执行
- 评测报告设计（按技能、按 Agent、按工作流）

**交付物**：
- 5 个域的 RBAC 执行
- 不可变仅追加审计日志
- 可配置 PII/密钥脱敏流水线
- 工具执行策略：allowlist + denylist + 风险等级确认门禁
- 技能演化策略防火墙（阻止不安全模式）
- 合规证据导出（JSON + PDF）
- OpenTelemetry 追踪：run → step → call 层级
- 指标：成功率、步骤数、token、成本、延迟百分位、路由命中率、演化增益
- SLO 门禁（回归时阻止发布晋级）
- 夜间评测报告（版本化、可 diff）

**验证**：SC-9（安全、合规与治理）+ SC-10（可观测性、SLO 与发布门禁）

**设计文档**：`decisions/governance-observability-design.md`

---

## 交叉引用

- [[decisions/success-criteria]] —— 完整 49 项验收清单（SC-1 到 SC-10）
- [[decisions/phase-0-overview]] —— 技术栈与架构原则
- [[comparisons/agent-framework-landscape.zh-CN]] —— 18 框架竞品分析
- [[concepts/llm-fundamentals]] —— LLM 基础知识参考
- [[raw/papers/memento-skills-2603.18743]] —— 技能自演化研究基础
