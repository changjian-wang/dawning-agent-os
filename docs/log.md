# 操作日志

> 追加式记录。每次 Ingest / Query / Lint 操作追加一条。
> 格式：`## [日期] 操作类型 | 标题`

---

## [2026-04-07] init | Wiki 结构初始化

- 操作：从平面文档结构迁移到 LLM Wiki 三层架构
- 模式参考：[Karpathy LLM Wiki](https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f)
- 创建 SCHEMA.md：定义 wiki 结构约定、操作工作流、命名规范
- 创建目录结构：raw/（6 子目录）、entities/、concepts/、comparisons/、decisions/、synthesis/
- 迁移现有文档到对应分类

## [2026-04-07] ingest | arXiv 2603.18743 Memento-Skills

- 来源：`raw/papers/memento-skills-2603.18743.md`（待补充原文）
- 贡献页面：`decisions/roadmap-90-days.zh-CN.md`（路线图基于该论文设计）
- 关键要点：Agent-designing-agent、Stateful Prompts、Read-Write Reflective Learning

## [2026-04-07] ingest | 6 大 Tier-1 Agent 框架

- 来源：GitHub 源码分析（MAF、SK、LangGraph、CrewAI、OpenAI SDK、Google ADK）
- 新建页面：`entities/frameworks/` 下 6 个框架实体页
- 更新页面：`comparisons/agent-framework-landscape.zh-CN.md`
- 关键要点：MAF 25+ NuGet 包、SK Abstractions/Core 分离、LangGraph Pregel 引擎 + checkpoint 三件套、CrewAI Flow 装饰器、OpenAI SDK 扁平极简、Google ADK ~25 模块最丰富

## [2026-04-07] ingest | Karpathy LLM Wiki 模式

- 来源：`raw/articles/karpathy-llm-wiki.md`（待补充原文）
- 新建页面：`concepts/llm-wiki-pattern.zh-CN.md`
- 关键要点：编译优于检索、三层架构（Raw/Wiki/Schema）、Ingest/Query/Lint 三种操作、维护成本归零的洞察

## [2026-04-08] restructure | 路线图重构为分层学习路径 + 成功标准

- 背景：原 `roadmap-90-days` 实际是详细验收标准文档，缺少标准路线图要素（阶段、优先级、依赖关系）
- 研究：分析 4 种标准路线图模式（Theme-Based/Milestone-Based/Now-Next-Later/RFC-KEP）
- 操作：
  - 新建 `decisions/roadmap.md`：英文版分层学习路径路线图（Layer 0–7，按知识依赖排序）
  - 新建 `decisions/roadmap.zh-CN.md`：中文版分层学习路线图
  - 重命名 `decisions/roadmap-90-days.md` → `decisions/success-criteria.md`：添加 YAML frontmatter，更新标题
  - 重命名 `decisions/roadmap-90-days.zh-CN.md` → `decisions/success-criteria.zh-CN.md`：添加 YAML frontmatter，更新标题
  - 更新 `index.md`：新增路线图条目，更新成功标准条目，页面计数 11 → 13
- 设计决策：Layer 依赖链 L0(LLM Provider) → L1(Agent Loop) → L2(Memory) → L3(Multi-Agent) → L4(Skill Router) → L5(Skill Evolution) → L6(Distributed) → L7(Governance)

## [2026-04-08] spec | Layer 0 开发前技术规格

- 背景：在开发前确定所有技术点，建立需求 → 功能 → 技术规格的文档链路
- 参考：dawning-agents 已有实现作为设计参考（不作为代码基线）
- 新建页面：
  - `decisions/layer-0-requirements.zh-CN.md`：需求说明（8 用户场景、9 约束条件、SC-7 映射、参考实现分析、8 个开放问题）
  - `decisions/layer-0-features.zh-CN.md`：功能清单（7 大功能域、63 项功能，P0×27 / P1×26 / P2×10）
  - `decisions/layer-0-tech-spec.zh-CN.md`：技术规格（ILLMProvider 接口、数据模型、流式事件设计决策、Decorator 降级模式、配置驱动价格表、异常层次、NuGet 包结构）
- 关键设计决策：
  - Q1: 流式事件 → 单 record + 判别字段（非 5 独立 record）
  - Q2: Provider 降级 → Decorator 模式（FallbackLLMProvider）
  - Q3: 成本估算 → 配置驱动价格表（IOptions<ModelPricingOptions>）
  - Q5: 模型能力 → 配置声明（非运行时探测）

## [2026-04-08] decision | dawning-agent-framework 定位为全新项目

- 决策：dawning-agent-framework 是**全新独立项目**，不依赖 dawning-agents，代码从零实现
- dawning-agents 仅作为学习参考（了解哪些设计已验证可行、哪些是已知不足）
- 命名空间：`Dawning.AgentFramework.*`（非 `Dawning.Agents.*`）
- 影响：
  - Layer 0 文档中 "V1/V2" 语言全部替换为 "参考/新设计"
  - 功能清单中 63 项功能全部从零实现（参考列标注 dawning-agents 中是否有可借鉴设计）
  - Tech Spec 中 NuGet 包命名统一为 `Dawning.AgentFramework.*`

## [2026-04-09] refactor | 文档精简合并

- 问题：8 个文件存在严重冗余（success-criteria §1–§8 与 roadmap §1–§8 逐字相同，EN/ZH 翻译导致 4 份拷贝）
- 操作：
  - 删除 `decisions/roadmap.md`（EN）— 保留 .zh-CN 版本为规范
  - 删除 `decisions/success-criteria.md`（EN）— 保留 .zh-CN 版本为规范
  - `success-criteria.zh-CN.md`：删除重复的 §1–§8（~350 行），改为交叉引用 roadmap.zh-CN
  - `phase-0-overview.md`：命名空间 `Dawning.Agents.*` → `Dawning.AgentFramework.*`，标注为历史文档
  - `index.md`：移除 EN 文件条目，页面计数 16 → 14
- 效果：8 → 6 个文件，减少 ~2000 行冗余

## [2026-04-09] reading | MAF 源码解析 00-overview

- 来源：GitHub `microsoft/agent-framework` 仓库源码分析
- 新建页面：`readings/frameworks/maf/00-overview.zh-CN.md`
- 分析范围：仓库结构、25+ NuGet 包分层、核心类型总览、设计模式
- 关键发现：
  - MAF 建立在 `Microsoft.Extensions.AI`（`IChatClient`）之上，不自己定义 LLM 抽象
  - `AIAgent` 是 abstract class（非接口），使用 Template Method 模式
  - 深度使用 Decorator 模式（`DelegatingAIAgent` → OTel / Logging / FunctionInterception）
  - `ChatClientAgent` 是核心实现，内部封装 Agent Loop（LLM → tool calls → LLM 循环）
  - 会话支持 JSON 序列化/反序列化，ContinuationToken 支持后台异步 Agent
- 对 dawning-agent-framework 启示：
  - 借鉴：M.E.AI 集成策略、Decorator 链、Template Method、AsyncLocal 上下文流动
  - 避免：25+ 包过度拆分、抽象类限制继承、Agent Loop 深度封装不够灵活

## [2026-04-09] reading | MAF 源码解析 01-abstractions

- 来源：GitHub `Microsoft.Agents.AI.Abstractions` 包源码逐文件分析
- 新建页面：`readings/frameworks/maf/01-abstractions.zh-CN.md`
- 分析范围：AIAgent 抽象基类、DelegatingAIAgent Decorator、AgentSession/StateBag、AgentResponse 双模态、AIContext/AIContextProvider 两阶段生命周期、ChatHistoryProvider、AgentRunContext AsyncLocal
- 关键发现：
  - Template Method 模式贯穿所有抽象类型（Run/RunCore、Invoking/InvokingCore）
  - 三层 override 策略：简单（ProvideAIContextAsync）/ 中等（InvokingCoreAsync）/ 完全控制
  - 消息来源追踪（External/ChatHistory/AIContextProvider）+ 过滤器防循环注入
  - GetService 类 COM QueryInterface 模式用于 Decorator 链服务发现
  - AgentSessionStateBag 用 ConcurrentDictionary + JSON 延迟反序列化
  - 结构化输出 RunAsync<T>() 在基类 partial class 中实现，子类无感知
- 对 dawning-agent-framework 启示：
  - 借鉴：Template Method、两阶段上下文注入、StateBag、消息来源追踪、三层 override 策略
  - 差异化：interface IAgent + AgentBase（非纯抽象类）、统一 IAgentMiddleware（非分离 Provider）、纯 DI（非 GetService）

## [2026-04-09] reading | MAF 源码解析 02-agent-lifecycle

- 来源：GitHub `ChatClientAgent.cs`、`ChatClientAgentOptions.cs`、`ChatClientAgentSession.cs`、`AIAgentBuilder.cs`、`ChatClientExtensions.cs` 源码分析
- 新建页面：`readings/frameworks/maf/02-agent-lifecycle.zh-CN.md`
- 分析范围：Agent 三种创建方式、构造内部流程、IChatClient 中间件管道、RunCoreAsync/RunCoreStreamingAsync 完整流程、ChatOptions 三层合并、会话两种模式、AIAgentBuilder Decorator 管道、Provider 通知机制
- 关键发现：
  - **Agent Loop 不在 ChatClientAgent 中** — 被委托给 `FunctionInvokingChatClient`（M.E.AI 层），ChatClientAgent 只做一次 GetResponseAsync
  - IChatClient 中间件管道：FunctionInvokingChatClient → PerServiceCallChatHistoryPersistingChatClient → leaf
  - ChatOptions 三层优先级合并：AgentRunOptions > 请求级 > Agent 级；Instructions 是拼接而非覆盖
  - 构造时 Options 深拷贝、StateKeys 唯一性校验（fail-fast）
  - 两种会话模式：框架管理（本地 ChatHistoryProvider）和服务管理（LLM 服务端），自动切换
  - ContinuationToken 支持流式断点续传（保存 InputMessages + ResponseUpdates）
  - AIAgentBuilder 反序应用 Decorator 工厂（先注册 = 最外层 = 最先执行）
- 对 dawning-agent-framework 启示：
  - 借鉴：Options 克隆、三层配置合并、StateKeys 校验、Instructions 拼接策略、两种会话模式
  - 差异化：Tool loop 放在 Agent 层（非 IChatClient 层）以支持拦截、策略模式替代 sealed class、单枚举替代三个冲突布尔值、v0.1 仅 end-of-run 持久化
