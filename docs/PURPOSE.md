# PURPOSE.md — Dawning Agent OS Wiki 方向意图

> 本文件回答“**为什么有这个 wiki**”。
> “**怎么运作**”由 [SCHEMA.md](./SCHEMA.md) 定义；本文件不重复结构规则。
> Agent 在 ingest / query 前必须先读本文件，确认材料在收录范围内。

---

## 1. 目标

为 dawning-agent-os 这个在建个人 AI agent 产品沉淀一份"可被 LLM 随时查询、随构建过程同步演化"的工程知识库，并让这份知识库与产品代码共处同一主仓库。它要同时满足四件事：

1. **决策可追溯（永久）**：从项目第 0 天起，每一个非显然的架构选择、每一次取代旧决策的转折，都留下 ADR；当存在多个值得对照的备选时，再配一份 comparison。无论几个月还是几年后翻出来，都能完整复原"什么时间、基于什么信息、做了什么选择、否决了哪些备选、后来是否被推翻"——这条时间线本身就是项目最重要的资产。
2. **外部知识可消化**：读到的论文 / 框架 / 仓库不停留在收藏夹，而是被压成 entity / concept 页，并显式连接到本项目的设计判断（"支持我的假设" / "被我否决，理由是 X"）。
3. **LLM-friendly 的项目记忆**：未来当我或 coding agent 写代码时，wiki 是第一手上下文来源——比读源码更快回答"这个模块为什么长这样"。
4. **产品实现与决策记忆同仓**：dawning-assistant 即将删除、dawning-agents 已弃用后，本仓库升级为产品 monorepo；docs/ 继续作为内置 LLM-Wiki，应用代码在同仓库内演进。

## 2. 关键问题

> 项目策略：**先做一款 Agent 产品，待产品成熟后再从中提取出 Agent Framework**。
> 这意味着 dawning-agent-os 是产品 monorepo，而 docs/ 是内置 LLM-Wiki；wiki 同时要服务两个阶段：当下的产品决策、以及未来"哪些代码该被抽出来"的判断。

驱动收录的核心问题清单（会持续演化）：

**产品阶段（当下）**

> 以下是 LLM 可机读的产品契约。隐喻与展开见后续段落。
> **关键澄清**：本产品的差异化不在"领域"（生活 vs 编程），而在"形态"（管家 vs 工具）。AI Coding 场景**包含在内**，但本产品不是 Copilot / Cursor 的替代品，而是另一个物种。

```yaml
product_name: dawning-agent-os
product_type: 通用个人 AI agent（"AI 管家"形态）
repository_shape: 产品 monorepo + 内置 LLM-Wiki；docs/ 记录决策和知识，应用代码也进入本仓库
mvp_technology_shape: 桌面 App + ASP.NET Core 本地后端；桌面壳采用 Electron，后端作为本地 agent runtime；第一版支持 Windows / macOS
core_value: 让用户用最自然的语气说话，agent 负责听懂、推断、执行；用户不需要会写 prompt
core_interaction_principle:
  name: options-over-elaboration（选择题优先于问答题）
  rationale: |
    写清楚需求本身就是一件耗时耗力的活，连产品作者自己也常常做不到。
    传统 AI 工具要求用户先把需求描述清楚才能用好。
    本产品反过来：当用户表达模糊时，agent 主动给候选方案让用户挑选，
    把用户的角色从"需求作者"降级为"候选评委"。挑选/否决/微调比从零写需求轻松 10 倍。
  default_strategy_when_ambiguous:
    - "1. 先关联当前对话、当前任务、已授权材料与长期记忆，推断用户最可能的意图"
    - "2. 能推断时，主动给 2–4 个候选解，而不是先把问题还给用户"
    - "3. 用一句话说明每个候选解的核心差异"
    - "4. 推断不出来或风险超过动作级别边界时，才询问；询问也优先给选择题"
    - "5. 让用户用最低成本（点选 / 一句话否决 / 改一个词）来收敛"
target_user:
  - 不想写 prompt / 不想管理上下文的人（无论是否程序员）
  - 痛点：问题问得抽象、不知道怎么把需求翻译给 LLM、跨会话上下文反复丢失
  - 包含：写不清需求的程序员、不擅长 prompt 工程的高级用户、能用 LLM 但懒得反复铺上下文的人
not_target_user:
  - 追求每一步都手工控制，且愿意自己维护 prompt / memory / context 的高级用户
in_scope_examples:
  - 生活：日程安排、信息整理、起草、归类、提醒、生活决策辅助
  - 工作：邮件 / 文档处理、会议纪要整理、抽象需求拆解
  - 编程：把"我想做 X"翻译成代码任务、长期记住项目上下文与个人风格、在程序员表达不清时主动澄清
out_of_scope:
  - 专业领域终局判断（法律、医疗、心理诊断）→ 须声明不专业并建议咨询人类专家
  - 企业级 RPA / 工作流编排平台
  - 替代 IDE 内的低延迟代码补全（Copilot 已经做得很好，无需重做）
key_differentiator:
  vs_chatgpt: 有长期记忆、主动观察、不只是被动问答
  vs_copilot_cursor: 不依赖用户会写 prompt；理解抽象意图；跨会话记住主人；管家形态而非补全器
  vs_general_agent_framework: 我们是产品而非框架；framework 是产品成熟后的副产物
key_constraint:
  repository_boundary: dawning-agent-os 不再是 wiki-only；docs/ 仍受 SCHEMA 约束，代码实现可在 apps/、src/、tests/ 等目录演进
  subject_object_boundary: agent 是客体，user 是主体；agent 永远不替 user 形成最终判断、塑造偏好、定义身份
  failure_default: 不确定时优先给候选让用户挑，不赌运气、不强制用户重新描述
  mvp_input_boundary: MVP 信息整理不默认读取用户文件夹；第一版从 user 显式提供 / 选择的材料或 agent 管理的 inbox 开始
  memory_purpose: 记忆服务于侍奉（理解主人），不用于行为操控（推送 / 引导消费 / 塑造注意力）
  memory_source_boundary: user 与 agent 沟通的所有话题都可作为记忆来源；agent 不主动翻 user 的电脑，除非 user 明确要求扫描指定范围或全盘文件
  memory_mvp_strategy: MVP 先做显式 Memory Ledger，所有关键记忆可解释、可查看、可编辑、可删除；向量 / embedding 检索后置
  interest_profile_strategy: 兴趣 / 标签不是永久静态偏好；MVP 以显式 tags 冷启动，并在 Memory Ledger 中用权重、置信度与时间衰减维护关注信号
  mvp_first_slice: 第一版采用聊天窗口 + agent inbox；输入限于显式材料与会话沉淀；动作先做总结 / 分类 / 打标签 / 候选整理方案；Memory Ledger 可查看 / 编辑 / 删除；兴趣权重先用简单可解释规则
  mvp_desktop_stack: MVP 采用 Electron 桌面壳 + ASP.NET Core 本地后端；支持 Windows / macOS；第一版通信 localhost + 随机端口 + 启动 token；存储优先 SQLite，落在系统用户应用数据目录
  mvp_default_llm_providers: 第一版默认接入 GPT 与 DeepSeek；保留 provider 抽象，不绑定单一供应商
  implementation_process: 永远方案先行；产品代码实现、目录生成、依赖引入和架构性修改前先给方案并获得确认
  proactivity_default: 默认不实时打断；普通主动性汇总成候选摘要，只有安全 / 截止时间 / 数据丢失 / 不可逆风险才允许立即打断
  draft_style: 代笔默认冷静、客观、可靠，不做不必要的情绪表达，不深度拟人模仿 user
```

**判定一个具体动作是否合规：**

- L0 信息型 / 只读型（查询、搜集、读取、总结、候选方案生成）→ agent 可自决。
- L1 可逆整理型（分类、打标、整理、本地归档、可恢复移动）→ agent 可自决，事后告知并保留回滚路径。
- L2 内容修改型（修改文件内容、更新记录、批量重命名 / 移动）→ 需要 user 明确确认；若 user 在当前任务中已经明确指定对象与动作，该指令可视为确认，但 agent 仍须提供 diff / 操作记录 / 回滚可能。
- L3 高风险型（删除、永久删除、发邮件、付款、对外发言、git push、日历写入、权限 / 密钥变更）→ 必须 user 一键确认，且不得从模糊指令中推断授权。
- 删除默认走软删除 / 回收站 / 隔离区，保留 **30 天**恢复窗口；到期后才允许清理。无法提供恢复路径的删除，一律按不可逆高风险动作处理。
- 模糊指令（"处理一下" / "整理一下" / "优化一下"）默认先关联上下文推断；能推断则给候选或按动作级别处理，推断不出来才询问。L0 可直接做，L1 先预览或小范围执行，L2/L3 不执行。
- 主动性默认不实时打断；普通建议汇总为候选摘要。只有安全、截止时间、数据丢失、误删 / 误改风险等高优先级事件才允许立即打断。
- 代笔默认冷静、客观、可靠，不加入不必要的情绪，不深度拟人模仿 user；对外发送仍须确认。
- 涉及 user 的偏好 / 价值观 / 身份认同 → agent 只能起草建议或列 tradeoff，不下结论
- 涉及专业终局判断（法律 / 医疗 / 心理 / 重大架构选型） → agent 须声明不专业并建议找人类专家或留 ADR 由 user 定夺

**决策示例：**

- "帮我把这周的邮件分类" → 自决执行，✅
- "帮我回复张三那封邮件" → 起草并请 user 确认后再发，⚠️
- "我该不该接这个 offer？" → 列 tradeoff，不给结论，⚠️
- "帮我把这个 Python 脚本改成异步" → 当前指令已明确对象与动作，可修改并提供 diff / 回滚路径；commit / push 须再次确认，✅⚠️
- "我这段需求到底想干什么，帮我捋一下" → 主动澄清模糊点、给 2–4 个候选拆解让用户挑，✅（这正是产品差异化场景）
- "学张三的 PR review 风格帮我 review 一下" → 模仿过头会替 user 形成观点，⚠️须明示"这是模仿稿，最终判断在你"
- "帮我清理一下这些旧文件" → 先列候选清理项；执行时只软删除并保留 30 天恢复窗口，永久删除须再次确认，⚠️

**反例（产品不该这么做）：**

- 用户说"帮我搞个登录功能"，agent 反问"你要哪种登录方式？OAuth 还是密码？要支持 SSO 吗？要记住密码吗？……" → ❌ 这是把需求重新写一遍的活推回给用户。正确做法：直接给 2–3 个候选实现，每个一句话说明区别，让用户点选或一句话否决。
- 用户说"我今晚想吃点轻松的"，agent 反问"什么叫轻松？" → ❌ 应直接给 3 个候选（清淡 / 速食 / 不用出门）让用户挑。
- 用户表达模糊时，agent 输出一段长 prompt 让用户"看看这样描述对不对" → ❌ 这是把 prompt 工程外包回用户，违背 core_interaction_principle。

---

下面是面向人类读者的展开。
- **核心原则——分身但有边界**：优秀管家像主人的分身，但永远分清主体（主人）与客体（管家）。三层划分：

  | 层 | 管家可以做 | 管家不能做 |
  |---|---|---|
  | 执行层（手） | 替主人做事：订餐、整理、起草、归类 | — |
  | 判断层（嘴） | 替主人**起草**判断、提供选项、说明 tradeoff | 替主人**做出**判断 |
  | 意志层（心） | 学习偏好、识别状态 | 替主人**形成**偏好、定义"我是谁" |

- **最小可用形态（MVP）**：**主场景 = 信息整理**，但第一版不默认读取用户文件夹。原因是大部分用户没有稳定整理文件夹的习惯，默认扫文件夹会把产品建立在错误前提上，还会放大隐私和信任风险。第一版形态采用聊天窗口 + agent inbox：聊天用于表达意图、澄清和反馈，inbox 用作 user 主动投喂的待整理材料容器。输入先限于显式材料与会话沉淀；动作先做总结、分类、打标签和候选整理方案，不默认写入外部系统、不默认修改 / 移动 / 删除文件。若使用兴趣 tags 做冷启动，tags 只作为初始种子；关注信号进入 Memory Ledger，并按权重、置信度与时间衰减维护，长期不关注默认降权。选型理由：最能验证「长期记忆 + 选择题优先」差异化（零记忆不可用）、失败可逆、闭环最短、累积记忆数据快；主场景详见 [ADR-005](pages/adrs/mvp-main-scenario-information-curation.md)，第一版切片详见 [ADR-014](pages/adrs/mvp-first-slice-chat-inbox-read-side.md)，输入边界详见 [ADR-012](pages/adrs/mvp-input-boundary-no-default-folder-reading.md)，兴趣画像详见 [ADR-013](pages/adrs/interest-profile-weighting-and-decay.md)。
- **副场景（侦察兵）**：
  - 日程：只做「读 + 候选生成」（识别冲突、给出推谁 / 合并 / 延期的 2–3 个候选），不调用日历写 API。
  - 生活决策：只列 tradeoff / 给 2–4 个候选，不下结论。
  副场景与主场景共享 Memory 模块；是否能被复用是验证「通用层抽取」是否成立的依据（对应下方「通用层抽取（未来）」中的 framework 抽取问题）。
- **最终形态（北极星，非承诺）**：长期收敛于「个人计算的中间层 / 个人 OS」——长期记忆与意图理解作为系统级能力，被沉淀为 memory / intent / permission 层。短期产品仍会主动接入文件、笔记、邮件、日历等外部数据源；长期目标是让其它 app 也能反向接入这层能力，而不是让 agent 永远停留在逐个 app 适配器集合。本条仅作为路径过滤器（每个架构决策反问「是否朝此方向收敛」），不进入 §4.1 thesis、不立刻驱动功能清单；待 MVP 跑通且 Memory 模块出现外部依赖后，再考虑升级为 §4.1 + 起草 ADR。
- **仓库形态**：dawning-agent-os 不再只是 wiki-only 仓库，而是产品 monorepo + 内置 LLM-Wiki。docs/ 继续记录决策、知识和边界；产品代码也进入本仓库。详见 [ADR-015](pages/adrs/repository-shape-product-monorepo-with-wiki.md)。
- **MVP 技术形态**：第一版采用桌面 App + ASP.NET Core 本地后端，桌面壳选 Electron，支持 Windows / macOS。Electron 负责窗口、托盘、快捷入口、聊天 / inbox / Memory Ledger UI；ASP.NET Core 负责本地 agent runtime、Memory、inbox、权限与动作分级。默认接入 GPT / DeepSeek，数据存储使用系统用户应用数据目录下的 SQLite。详见 [ADR-016](pages/adrs/mvp-desktop-stack-electron-aspnetcore.md)。
- **个人 OS 不是什么**：不是替代操作系统内核，不是 app launcher，不是企业工作流平台，不是基于情绪 / 注意力做推荐分发的系统。它首先是个人记忆、意图理解、授权与执行边界的中间层。
- **每次决策必答的两个问题**：
  1. 这个设计是"为本产品定制"，还是"任何 Agent 都需要"？
  2. 如果是后者，能否暂时以产品代码形态存在，等被复用 ≥ 2 次再考虑抽进 framework？
- 已有 Agent 框架（OpenAI Agents SDK、LangGraph、AutoGen、CrewAI、Microsoft Agent Framework 等）哪些机制值得借鉴、哪些被本产品否决？为什么？
- 哪些论文 / 模式（ReAct、Reflexion、Voyager、MemGPT、Toolformer 等）真正影响了产品设计？哪些只是看起来相关？

**通用层抽取（未来）**

- Memory / Skill / Tool / Orchestration 这几层在产品里如何切分？哪些边界是被产品需求自然推出来的、哪些是我提前过度设计的？
- 哪些代码已经被复用 ≥ 2 次或被外部依赖？这些是 framework 抽取的天然候选。
- 协议层（MCP / A2A）的大方向已定为外挂适配器；开放问题是何时接入、先接哪个协议、接到多深。
- Agent 的 self-improvement / skill 演化机制不作为默认能力；开放问题是未来是否作为可选模块、由什么产品信号触发。

**架构持续追问**

- dawning-agent-os 的核心抽象到底是什么？产品成熟后回头看，初版 thesis 哪些被验证、哪些被推翻？
- 什么时候才算"产品足够成熟、可以开始抽 framework"？需要哪些信号（复用次数、API 稳定度、用户场景覆盖度）？

## 3. 收录范围

### 3.1 包含

- **dawning-agent-os 自身设计**：架构决策（ADR）、模块边界、命名约定、范围边界
- **影响 agent 行为边界的产品形态决策**：MVP 主场景、管家定位、交互哲学、记忆边界、个人 OS 北极星等会反向影响架构的判断
- **实现过程的关键节点**：里程碑、推翻重做的来龙去脉、重大踩坑与修复思路、被废弃的方案及废弃理由——只记"读源码看不出来的那部分"
- **直接相关的外部对象**：被借鉴或被对比的 Agent 框架、协议、工具、论文、仓库
- **核心概念解释**：memory / skill / tool-use / orchestration / planning / reflection 等本项目用到的概念
- **横向对比**：选型分析、tradeoff 矩阵、否决理由
- **工程实践**：与 Agent 系统直接相关的工程模式（错误处理、并发、可观测性、安全边界）

### 3.2 不包含

- **代码实现细节**：函数签名、字段列表、类继承关系——那是源码 + XML doc 的事。
- **通用 LLM / ML 训练知识**：除非直接服务于 Agent 设计判断。
- **纯商业 / 增长策略**：roadmap、商业模式、定价、渠道、市场营销、竞品市场分析。会影响 agent 架构和行为边界的产品形态决策仍按 §3.1 收录。
- **个人随手摘录**：未经压缩、未连接到任何设计判断的二手转述。
- **教程内容**：how-to 类入门文档不进 wiki，进 README / 官方 docs。反例锚点：本 wiki 只收「为什么这么做」（决策与机制），不收「怎么操作」（步骤与命令行）；踩坑与修复思路归§3.1，不算教程。
- **AI Coding 工具的低延迟补全实现细节**：与本产品形态不同（工具 vs 管家），不收录。但同一类工具的**长期记忆 / 意图理解 / 跨会话上下文**机制与本产品同主题，按 §3.1 收录。

### 3.3 边界案例

- **涉及 LLM 但不直接服务 Agent 设计的材料** → 默认拒收，除非能写出明确的 Agent 视角连接。
- **某个框架的某个机制看起来很有意思，但本项目暂时用不到** → 收为 entity，但页面必须诚实写"暂未采纳，原因 X"，避免变成 wishlist。
- **同一概念在不同来源中名字不同**（如 skill vs tool vs capability）→ 选一个 canonical 页，其它名字在 entity 页的「同义名」H2（SCHEMA §6.2）或「相关页面」注明。
- **非中英文一手资料** → 仍可收录；wiki 页语言要求见 SCHEMA §9.4。

## 4. 当前 Thesis

> Thesis 允许演化。它会随着产品迭代与 ingest 外部资料自然浮现，而不是一开始就强行编完。每次重大判断变化时更新本节，并在 `pages/adrs/` 下补一份 ADR 解释取代关系。
> **当前阶段是"产品先行"**，因此本节刻意保持克制：只写当下站得住的边界判断，不预判 framework 该长什么样。

### 4.1 站得住的边界判断

**产品红线（来自管家定位 + 主客体边界；本节细化 §2 yaml 中 `key_constraint` 的三条约束）**

- **主体客体不可僭越**：管家不替主人形成判断、不替主人持有观点、不替主人定义身份。判断层只起草不下结论；意志层只学习偏好不塑造偏好。详见 [ADR-001](pages/adrs/butler-positioning-and-subject-object-boundary.md)。
- **重要动作必须显式确认；代笔必须明示**。查询 / 搜集 / 分类 / 可逆整理可自决；修改文件内容、写入外部系统、删除、发邮件、付款、对外发言等动作须按重要性级别确认。删除不得直接硬删，默认软删除并保留 30 天恢复窗口；无法恢复的删除视为不可逆高风险动作。管家替主人起草的内容，主人自己必须知道这是起草而非代发。详见 [ADR-004](pages/adrs/important-action-levels-and-confirmation.md)。
- **失败时优先保守**。管家做错一次比做对十次更伤信任。宁可给候选让用户挑、问"是这样吗"再做，不要赌运气、不要把活推回给用户重新描述。低成本的纠错路径与可逆性是产品 Day-1 要求，不是后期优化。
- **选择题优先于问答题**。当用户表达模糊时，默认动作是先关联当前对话、任务、已授权材料与长期记忆来推断；能推断时给 2–4 个候选让用户挑，而不是反问"你具体想要什么"。推断不出来或风险超过动作级别边界时才询问，且询问也优先给选择题。挑选 / 否决 / 微调比从零写需求轻松 10 倍——这是产品的核心交互哲学，违反则丢失差异化。详见 [ADR-002](pages/adrs/options-over-elaboration.md)。
- **长期记忆是核心而非可选模块**。管家的价值主要来自"他了解你"，没有持续记忆的管家叫前台。自研重点是记忆模型、用户画像语义、可解释 / 可控策略；底层存储、检索、embedding 基础设施可以复用成熟组件。MVP 先采用显式 Memory Ledger，确保关键记忆可解释、可查看、可编辑、可删除；向量 / embedding 检索后置。详见 [ADR-003](pages/adrs/long-term-memory-as-core-capability.md) 与 [ADR-011](pages/adrs/explicit-memory-ledger-mvp.md)。
- **记忆服务于侍奉，不用于行为操控**。user 与 agent 沟通的所有话题都可作为记忆来源；关键记忆必须可查看、可编辑、可删除；推断性记忆必须标注为推断。agent 不主动翻 user 的电脑，不主动扫描本地文件；只有 user 明确要求扫描指定范围或全盘文件时才可读取对应文件系统内容。记住主人讨厌什么是为了帮他过滤，不是为了基于他的情绪推送内容、引导消费、或塑造他的注意力。详见 [ADR-007](pages/adrs/memory-privacy-and-user-control.md)。
- **兴趣画像必须有权重与衰减**。user 选择的 tags 只是冷启动种子，不是永久偏好或身份标签。agent 应在 Memory Ledger 中把关注主题表达为带权重、置信度、最近触达时间和衰减策略的信号；长期不关注默认降权，反复主动投喂 / 确认可升权，user 可 pin、降权、归档或删除。详见 [ADR-013](pages/adrs/interest-profile-weighting-and-decay.md)。
- **MVP 输入不假设用户已有整洁文件夹**。信息整理要解决的正是“东西散、上下文乱、用户懒得整理”的问题，因此第一版不默认读取用户文件夹，不把“用户已经有清晰目录结构”作为前提。默认入口应是 user 显式提供 / 选择的材料、agent 管理的 inbox，或会话中自然沉淀的待整理内容；读取文件夹只作为显式授权能力。详见 [ADR-012](pages/adrs/mvp-input-boundary-no-default-folder-reading.md)。
- **MVP 第一版切片保持窄而可逆**。第一版采用聊天窗口 + agent inbox，只接显式材料与会话沉淀，先做总结 / 分类 / 打标签 / 候选整理方案；Memory Ledger 可查看、可编辑、可删除；兴趣权重先用简单可解释规则。小批量文件、外部数据源、写索引、移动 / 重命名 / 删除等能力后置。详见 [ADR-014](pages/adrs/mvp-first-slice-chat-inbox-read-side.md)。
- **仓库是产品 monorepo，docs/ 是内置 LLM-Wiki**。dawning-assistant 即将删除、dawning-agents 已弃用后，本仓库承载产品实现与决策记忆；docs/ 仍按 SCHEMA 维护，应用代码可在 docs/ 外新增 apps/、src/、tests/ 等目录。详见 [ADR-015](pages/adrs/repository-shape-product-monorepo-with-wiki.md)。
- **MVP 桌面技术栈采用 Electron + ASP.NET Core 本地后端**。成熟框架优先；第一版支持 Windows / macOS；Electron 负责桌面壳和交互界面，ASP.NET Core 作为本地 agent runtime；默认接入 GPT / DeepSeek；SQLite 数据放在系统用户应用数据目录；第一版不默认做云后端、账号系统或同步服务。详见 [ADR-016](pages/adrs/mvp-desktop-stack-electron-aspnetcore.md)。
- **实现永远方案先行**。产品代码实现、目录生成、依赖引入和架构性修改前，必须先给方案并获得确认；不得未经确认一次性生成 apps/、src/、tests/ 等产品目录或大块 scaffold。详见 [Rule 实现前必须方案先行](pages/rules/plan-first-implementation.md)。
- **主动性默认克制**。agent 默认不实时打断 user；普通主动性汇总成候选摘要。只有安全、截止时间、数据丢失、误删 / 误改风险等高优先级事件才允许立即打断。详见 [ADR-008](pages/adrs/proactivity-and-interruption-boundary.md)。
- **抽象指令默认上下文优先**。当 user 说"处理一下"、"整理一下"、"优化一下"等模糊指令时，agent 先关联上下文和长期记忆推断；能推断则给 2–4 个候选方案或按动作级别处理，推断不出来才询问。L0 可直接做，L1 先预览或小范围执行，L2/L3 不执行。详见 [ADR-009](pages/adrs/abstract-instruction-fallback.md)。
- **代笔默认客观可靠**。agent 代笔时默认冷静、客观、可靠，不加入不必要的情绪，不深度拟人模仿 user；对外内容始终只是草稿，发送必须确认。详见 [ADR-010](pages/adrs/objective-drafting-style.md)。
- **领域无关，但场景有限**。管家不挑话题，但每个领域都浅；当用户进入专业深度（法律、医疗、心理）时，管家须承认"这不是我的活，需要找 X"，不假装专业。

**架构边界（产品 + 未来 framework 都适用）**

- **协议层（MCP / A2A）作为外挂适配器**，不绑定进核心代码。这条与"产品 vs framework"无关，是为了避免被生态变化拖死。
- **Agent 的 self-improvement / skill 自演化不作为默认能力**。即便要做，也作为可选模块；当前产品阶段属过早优化。
- **外部知识必须先验证再吸收**。看到一个论文 / 框架的机制，先问"这能解决我产品里的哪个具体问题"，回答不出来就不进设计，只进 entity 页备查。

### 4.2 待产品验证的开放问题（暂不下结论）

**架构层**

- 产品的核心抽象到底是什么？→ 暂无答案，留待产品 MVP 跑通后回填。
- 哪些产品代码该被抽进未来 framework？→ 暂无答案，需要复用次数 ≥ 2 或外部依赖出现时再评估。
- 微内核 + 可插拔技能 vs 大一统 framework / Memory 与 Skill 是否该切分 → 产品阶段不预设，让需求自然推出模块边界。

**MVP 成功信号（信息整理主场景）**

- 每天能被作者自己 dogfood，且不是为了测试而刻意使用。
- 候选生成能减少需求描述成本：用户更多是在选择 / 否决 / 微调，而不是重新解释任务。
- Memory 被真实复用：新任务至少部分依赖历史分类、命名、偏好或纠错记录。
- 纠错成本低：错分 / 错标 / 错移可以快速回滚，且不会伤害用户信任。
- 副场景开始复用同一套 Memory 模块，而不是各自写一套孤立逻辑。

### 4.3 元规则

- 任何写进 §4.1 的 thesis 都必须最终对应一份 ADR；没有 ADR 的视为待验证。
- 任何 §4.2 的开放问题，一旦被某次产品决策回答了，应同时在本节升级为 §4.1 的判断 + 写 ADR。
- 产品阶段宁可 thesis 少而真，不要多而虚。空白比错误的判断更有价值。
- **ADR 优先级**：§4.1 当前列出的全部边界判断无须一次性补齐 ADR。下面事项是**产品哲学根基或已落地前置 ADR**；其余等真正被产品决策触发时再补：
  1. **管家定位与主客体边界**（[ADR-001](pages/adrs/butler-positioning-and-subject-object-boundary.md)）——对应 §4.1 产品红线 #1「主客体不可僭越」；定义 agent 是客体、user 是主体，判断层只起草不下结论，意志层只学习不塑造。
  2. **选择题优先于问答题**（[ADR-002](pages/adrs/options-over-elaboration.md)）——对应 §4.1 产品红线 #4；定义模糊表达先结合上下文推断，优先给 2–4 个候选，而不是开放式追问。
  3. **长期记忆是核心能力**（[ADR-003](pages/adrs/long-term-memory-as-core-capability.md)）——对应 §4.1 产品红线 #5；定义长期记忆是管家形态的核心差异化，且自研重点在记忆模型、用户画像语义、可解释 / 可控策略。
  4. **重要性级别与确认机制**（[ADR-004](pages/adrs/important-action-levels-and-confirmation.md)）——对应 §4.1 产品红线 #2；定义 L0/L1 可自决、L2 需明确确认、L3 必须一键确认，以及删除默认 30 天软删除恢复窗口。
  5. **MVP 主场景选型 = 信息整理**（[ADR-005](pages/adrs/mvp-main-scenario-information-curation.md)）——对应 §2 「最小可用形态」；被否决方案：日程（失败不可逆、外部依赖重）、生活决策（执行闭环薄、记忆积累慢）。
  6. **产品策略收录边界与个人 OS 北极星澄清**（[ADR-006](pages/adrs/purpose-scope-and-personal-os-north-star.md)）——对应 §3 收录范围与 §2「最终形态」；记录本次 v1.7 对“产品形态决策可收录、商业策略不收录”和“个人 OS 是非承诺北极星”的澄清。
  7. **记忆隐私与用户控制**（[ADR-007](pages/adrs/memory-privacy-and-user-control.md)）——对应 §4.1「记忆服务于侍奉」；定义 conversation memory、文件扫描授权、可查看 / 可编辑 / 可删除、推断性记忆边界。
  8. **主动性与打断边界**（[ADR-008](pages/adrs/proactivity-and-interruption-boundary.md)）——对应 §4.1「主动性默认克制」；定义普通主动性走摘要、高优先级事件才打断。
  9. **抽象指令兜底机制**（[ADR-009](pages/adrs/abstract-instruction-fallback.md)）——对应 §4.1「抽象指令默认上下文优先」；定义先关联上下文推断、推断不足再询问，以及 L0/L1/L2/L3 在模糊指令下的默认动作。
  10. **客观代笔语气**（[ADR-010](pages/adrs/objective-drafting-style.md)）——对应 §4.1「代笔默认客观可靠」；定义冷静客观、不深度拟人模仿、发送前确认。
  11. **Memory MVP 采用显式记忆账本**（[ADR-011](pages/adrs/explicit-memory-ledger-mvp.md)）——对应 §4.1「长期记忆是核心而非可选模块」与 §4.1「记忆服务于侍奉」；定义第一版 Memory 先做可解释账本，向量 / embedding 检索后置。
  12. **MVP 输入边界：不默认读取用户文件夹**（[ADR-012](pages/adrs/mvp-input-boundary-no-default-folder-reading.md)）——对应 §2「最小可用形态」与 §4.1「MVP 输入不假设用户已有整洁文件夹」；定义第一版从 user 显式提供 / 选择的材料或 agent 管理的 inbox 开始，文件夹读取仅作为显式授权能力。
  13. **兴趣画像采用权重与时间衰减**（[ADR-013](pages/adrs/interest-profile-weighting-and-decay.md)）——对应 §2「最小可用形态」与 §4.1「兴趣画像必须有权重与衰减」；定义 tags 只是冷启动种子，关注信号进入 Memory Ledger，并随行为、确认、纠错和时间变化。
  14. **MVP 第一版切片：聊天 + inbox + 读侧整理**（[ADR-014](pages/adrs/mvp-first-slice-chat-inbox-read-side.md)）——对应 §2「最小可用形态」与 §4.1「MVP 第一版切片保持窄而可逆」；定义第一版界面、输入、动作范围、Memory 可见性和兴趣权重规则。
  15. **仓库形态：产品 monorepo + 内置 LLM-Wiki**（[ADR-015](pages/adrs/repository-shape-product-monorepo-with-wiki.md)）——对应 §1「产品实现与决策记忆同仓」与 §4.1「仓库是产品 monorepo，docs/ 是内置 LLM-Wiki」；定义 dawning-agent-os 从 wiki-only 升级为产品主仓库。
  16. **MVP 桌面技术栈：Electron + ASP.NET Core 本地后端**（[ADR-016](pages/adrs/mvp-desktop-stack-electron-aspnetcore.md)）——对应 §2「MVP 技术形态」与 §4.1「MVP 桌面技术栈采用 Electron + ASP.NET Core 本地后端」；定义桌面壳、local backend、Windows / macOS、GPT / DeepSeek、通信、存储与目录建议。

## 5. 读者画像

本节画像决定 wiki 写作的视角与详略；非目标读者无需服务。

- **Builder（主要读者，就是我）**：在写 dawning-agent-os 代码或做架构决策。需要 entity / comparison / adr / rule 提供决策支持与硬约束，能在 5 分钟内回答"我们当时为什么选 X 不选 Y"。
- **Future Coding Agent**：未来接管开发的 LLM agent。需要 wiki 作为长期记忆，比读源码更快理解模块意图。要求 front matter 完整、链接可达、断言可追溯。
- **Maintainer**：维护本 wiki 自身（也是我）。需要 SCHEMA / PURPOSE / rule 保持一致，需要 lint 工作流防止知识库腐烂。

非目标读者：

- 初次接触 Agent 概念的新人 → 读公开教程或论文综述，本 wiki 不承担入门职责。
- dawning-agent-os 的最终用户 → 读 README / 官方文档，本 wiki 不承担产品介绍职责。

---

**本文件元约束**

- 一级章节锁定为当前 5 个（目标 / 关键问题 / 收录范围 / 当前 Thesis / 读者画像）。新增内容只能进入既有章节的子节，不允许加新一级章节。
- 本文件不受 SCHEMA §7.2 拓扑约束（它是契约文件，规模由内容驱动）。
- 任何对 §2 yaml 锚定块、§3 收录范围、§4.1 红线 的修改都必须以 ADR 形式记录变更理由，不允许静默改动。

---

*Purpose 版本：1.17 | 最后更新：2026-04-28 | 与 SCHEMA.md 协同演化*
