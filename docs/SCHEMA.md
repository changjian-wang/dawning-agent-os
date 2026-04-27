# SCHEMA.md — Dawning Agent OS Wiki 结构契约

> 本文件是 LLM-Wiki 的**结构契约**：定义目录、页面类型、front matter、模板、流程与红线。
> **方向意图（为什么、收录范围）由 [PURPOSE.md](./PURPOSE.md) 定义；本文件不重复。**
> Agent 在执行任何写操作前，必须先读 PURPOSE.md 与本文件。

---

## 1. 设计原则

1. 单一真相源：规则只在本文件，agent 入口文件只做路由，不复述。
2. 类型尽量少；新建页面前先尝试落入既有类型。
3. 同一主题恰好一处权威页（`canonical`）。
4. 所有事实性判断可追溯到 `raw/`。
5. 优先更新已有页面，而不是持续新建。
6. `overview.md` `log.md` 是派生物，不是启动前提；自动化前不存在也合法。

## 2. 目录结构

```text
docs/
├── PURPOSE.md              # 方向意图（为什么、收录范围）
├── SCHEMA.md               # 本文件：结构契约
├── overview.md             # 派生物：当前快照（自动生成；无脚本时缺席）
├── raw/                    # 原始资料（不可变，LLM 只读）
│   ├── papers/             # 论文 / arXiv
│   ├── articles/           # 博文 / 演讲 / Gist
│   ├── repos/              # 仓库分析笔记
│   ├── official/           # 官方文档摘录
│   ├── meetings/           # 会议 / 讨论记录（遗留，新讨论记录优先放 articles/）
│   └── assets/             # raw 引用的图片
├── pages/                  # Wiki 编译产物
│   ├── hubs/               # 导航页（Map of Content）
│   ├── entities/           # 具名对象
│   ├── concepts/           # 概念解释
│   ├── comparisons/        # 横向对比 / 选型分析
│   ├── rules/              # 强制 / 推荐 / 参考规则
│   └── adrs/               # 架构决策记录
└── images/                 # wiki 页面引用的图片
```

### 2.1 增长规则

- `pages/{type}/` 下默认扁平。
- 同一 `type` 目录下页面数 ≥ **12** 时，才可按主题域加 **一层** 子目录；未达到 12 时必须保持扁平。
- 子目录命名必须为 `kebab-case`，仅作物理分组；不得引入新语义，不得改变页面 `type`。
- 子目录名按"主题域"命名（如 `memory-systems/`、`orchestration/`），**不得复用 §3.2 的 subtype 枚举值**（如 `framework/`、`protocol/`、`paper/`），避免物理目录与 front matter `subtype` 字段语义重叠。
- 禁止二级及更深子目录。
- 未经修改本 SCHEMA，不得新增 `docs/` 顶层目录或 `pages/` 下的 type 目录（红线见 §10 #5/#6）。

## 3. 页面类型

共 6 种主类型，与目录一一对应：

| type | 路径 | 适用 | 不适用 |
|---|---|---|---|
| `hub` | `pages/hubs/` | 主题入口、阅读顺序、范围划分 | 长篇论证 |
| `entity` | `pages/entities/` | 框架、协议、工具、论文、仓库等具名对象 | 跨对象比较、概念解释 |
| `concept` | `pages/concepts/` | 概念、模式、方法论的解释 | 横向选型 |
| `comparison` | `pages/comparisons/` | 多对象横向对比 / 选型分析 | 单对象介绍 |
| `rule` | `pages/rules/` | 强制 / 推荐 / 参考的硬规则 | 可被推翻的架构选择 |
| `adr` | `pages/adrs/` | 产品 / 架构决策记录（可被新决策 supersede） | 不可违反的红线 |

### 3.1 路由口诀（按顺序判定，命中即停）

1. 这是对所有页面持续生效、违反即错误的硬约束（命名、强制流程、规约）？→ `rule`
2. 这是当前被接受的决策 / 取舍，未来可被新决策取代？→ `adr`
3. 这是 ≥ 2 个具名对象的横向对照 / 选型？→ `comparison`
4. 主语是单个具名对象（框架 / 协议 / 工具 / 论文 / 仓库）？→ `entity`
5. 主语是概念 / 模式 / 方法论，而不是某个具名对象？→ `concept`
6. 这只是导航汇总，自身不承载论证？→ `hub`
7. 都不像 → 停下来问人类，不要新建页面，禁止发明新类型。

### 3.2 可选 subtype

`subtype` 仅用于轻量语义标注，不参与目录约束。新增 subtype 须先改本 SCHEMA。

使用规则：

1. 每页最多 1 个 subtype；拿不准时省略，不得自造。
2. subtype 描述页面的内容形态；tag 描述主题维度。二者不得互相替代（例：`framework` 是 subtype 不是 tag；`memory` 是 tag 不是 subtype）。
3. subtype 不参与路径推导，不得用目录名反推 subtype。

| type | 允许的 subtype |
|---|---|
| `hub` | `map` |
| `entity` | `framework`, `protocol`, `tool`, `paper`, `repo` |
| `concept` | `pattern`, `methodology`, `theory`, `workflow` |
| `comparison` | `landscape`, `selection`, `tradeoff` |
| `rule` | `convention`, `style`, `process` |
| `adr` | `product`, `architecture`, `tooling`, `scope` |

## 4. Front Matter 规范

每个 wiki 页面必须包含 front matter。front matter 是机器可校的页面契约，不是展示文案。

### 4.1 基础规则

- `subtype` 是唯一可整体省略的通用字段：不适用时省略本行；适用时必须取 §3.2 允许值。
- 除 `subtype` 外，§4.2 的通用字段必须全部出现；不适用时使用空值。
- 空数组写 `[]`；空字符串写 `""`；布尔值只写 `true / false`，不写字符串。
- 日期统一写 `YYYY-MM-DD`；不写时间、时区或自然语言日期。
- front matter 字段只表达结构元数据；正文论证、解释和引用展开写在页面正文。

通用字段骨架如下（示例以 `concept` 为例；其它 type 须按 §3.2 / §4.3 调整）：

```yaml
---
title: 页面标题
type: concept                   # hub | entity | concept | comparison | rule | adr
subtype: pattern                # 可选；按 type 选择，不适用则省略本行
canonical: true                  # 同主题最多一页 true
summary: 一句话摘要
tags: [agent, memory]            # 受控词表（见 §5）
sources: [raw/papers/example.md] # 来源；见 §4.4
created: 2026-04-27
updated: 2026-04-27
verified_at: 2026-04-27          # 最近一次确认事实仍准确
freshness: evergreen             # evergreen | volatile
status: draft                    # draft | active | stale | archived
archived_reason: ""              # status=archived 时必填
supersedes: []                   # 本页取代的旧页；无则 []
related: []                      # 强相关页面；无则 []
part_of: []                      # 所属 hub 或明确父 concept / rule；无则 []
---
```

### 4.2 通用字段表

| 字段 | 出现规则 | 值域 / 格式 | 说明 |
|---|---|---|---|
| `title` | 必填 | 非空字符串 | 页面标题，应与 H1 保持一致 |
| `type` | 必填 | `hub / entity / concept / comparison / rule / adr` | 6 种主类型之一 |
| `subtype` | 可选 | §3.2 允许值 | 轻量语义标注；不适用时省略本行 |
| `canonical` | 必填 | `true / false` | 同主题最多一页 `true`；见 §4.6 |
| `summary` | 必填 | 非空字符串 | 一句话页面意图，不写多段解释 |
| `tags` | 必填 | 受控 tag 数组 | 至少 1 个，全部来自 §5 |
| `sources` | 必填 | `raw/` 路径数组 | 来源路径；非空规则见 §4.4 |
| `created` | 必填 | `YYYY-MM-DD` | 创建日期，创建后不改 |
| `updated` | 必填 | `YYYY-MM-DD` | 最近一次内容更新日期 |
| `verified_at` | 必填 | `YYYY-MM-DD` | 最近一次确认事实仍准确的日期 |
| `freshness` | 必填 | `evergreen / volatile` | 长期稳定或需定期复查 |
| `status` | 必填 | `draft / active / stale / archived` | 页面生命周期状态 |
| `archived_reason` | 必填 | 字符串 | 未归档时写 `""`；`status: archived` 时必须非空 |
| `supersedes` | 必填 | 路径数组 | 本页取代的旧页；无则 `[]` |
| `related` | 必填 | 路径数组 | 强相关页面；无则 `[]` |
| `part_of` | 必填 | 路径数组 | 所属 hub 或明确父 concept / rule；无则 `[]` |

### 4.3 类型专属字段

类型专属字段只能出现在对应 type 上；其它 type 必须不出现这些字段。`hub / entity / concept / comparison` 没有专属字段。

#### 4.3.1 rule

| 字段 | 出现规则 | 值域 / 格式 | 说明 |
|---|---|---|---|
| `level` | 必填 | `强制 / 推荐 / 参考` | 强制 = 违反即错（lint error）；推荐 = 默认遵守，例外必须在 §6.5 的「例外」章节说明；参考 = 建议性，不阻断 |

#### 4.3.2 adr

| 字段 | 出现规则 | 值域 / 格式 | 说明 |
|---|---|---|---|
| `adr_status` | 必填 | `proposed / accepted / superseded` | 只能按 `proposed → accepted → superseded` 单向推进，不得跳级或回退 |
| `adr_date` | 必填 | `YYYY-MM-DD` | 进入当前 `adr_status` 的日期；`accepted` 后不再变更，被 `superseded` 时保留原始 accept 日期 |
| `adr_revisit_when` | 条件 | 字符串 | `accepted` 时必须非空且为可观察事件；`proposed / superseded` 可为 `""` |

补充约束：

- `adr_status: superseded` 必须由某个新 ADR 通过 `supersedes` 指向（与 §4.5 / §4.6 联动）。
- `adr_status: proposed` 仅供草拟阶段使用；不得长期停留在 proposed 而被其它页作为当前结论引用。

### 4.4 sources 规则

`sources` 表达页面与 `raw/` 原始资料的追溯关系；wiki 页面之间的关系写入 `related / part_of / supersedes`，不写入 `sources`。

出现规则：

| type | `sources` 出现规则 |
|---|---|
| `entity / concept / comparison` | 必填且非空 |
| `hub / rule / adr` | 必填，可为 `[]` |

路径格式：

- 相对 `docs/` 写，必须以 `raw/` 开头，例如 `raw/papers/example.md`。
- 全小写、`kebab-case`，文档类必须以 `.md` 结尾；`raw/assets/` 下的资产可使用对应扩展名。
- 不写 URL、不写绝对路径、不写 wikilink；外部资料须先归档到 `raw/` 后再引用。

可追溯性：

- 页面正文每条事实性判断必须可追溯到 `sources` 中的至少一项；无法追溯的判断必须删除或补来源。
- `sources` 中的路径必须真实存在；删除 `raw/` 文件之前必须先解除所有引用。

### 4.5 链接字段规则

`supersedes / related / part_of` 表达 wiki 页面之间的关系；它们的元素是页面路径，不是来源（来源写入 §4.4 的 `sources`）。

通用路径规则（对三个字段都强制）：

- 相对 `docs/` 写，必须以 `pages/` 开头，例如 `pages/adrs/options-over-elaboration.md`。
- 全小写、`kebab-case`、必须以 `.md` 结尾。
- 不写 wikilink、不写裸标题、不写绝对路径、不写 URL。
- 不得指向 `raw/`；raw 来源只写入 `sources`。
- 不得指向自身。
- 路径必须真实存在；指向尚未创建页面时，先创建目标页或暂不填写。
- 数组元素必须去重。

字段专属规则：

| 字段 | 指向范围 | 额外规则 |
|---|---|---|
| `supersedes` | 同 type 旧页（ADR→ADR、rule→rule） | 取代关系图必须为 DAG（无环）；被指向方应在 §4.6 下转入 archived/superseded |
| `part_of` | hub 或明确父 concept / rule | 数组首元素必须是 hub；其余可为父 concept / rule |
| `related` | 任意 wiki 页面 | 不要求双向；不写自指或同义重复 |

风格建议（非强制）：

- `related` 中互为理解前提的两页，建议双向维护以提升可达性。
- `part_of` 一般 1 条即可；多 hub 归属说明主题边界不清，应优先调整 hub 而非堆叠。

### 4.6 状态与 canonical 规则

`status` 描述页面生命周期；`canonical` 描述权威性。两者互相独立但有约束。

#### 4.6.1 status 生命周期

| 当前状态 | 允许转入 | 说明 |
|---|---|---|
| `draft` | `active`, `archived` | 起草中；不得作为当前结论被引用 |
| `active` | `stale`, `archived` | 当前生效；evergreen / volatile 决定复查节奏 |
| `stale` | `active`, `archived` | 需复查；过期超过 30 天必须降级（见 §4.6.2） |
| `archived` | —（终态） | 已退役；保留历史记录，不可回退 |

转换规则：

- 状态只能按上表推进，不得跳转或回退。
- `archived` 是终态；要重启同主题须新建页面，并通过 `supersedes` 指向旧 archived 页。
- `archived` 页面不得作为任何 hub / 其它页的主入口被链接（历史引用与 `supersedes` 反向追溯除外）。

#### 4.6.2 canonical 与 status 的耦合

- `canonical: true` 表示同主题当前权威页；同主题最多一页为 `true`（与 §1 设计原则 #3 一致）。
- `draft` 必须 `canonical: false`。
- `archived` 必须 `canonical: false`，并填写非空 `archived_reason`。
- `stale` 暂时允许 `canonical: true`；进入 `stale` 超过 30 天仍未复查必须降级为 `canonical: false` 或转入 `archived`。
- ADR 的 `superseded` 状态与 canonical 处理由 §4.3.2 / §4.5 统一管控，本节不再重述。

#### 4.6.3 非 canonical 页的可达性

- `canonical: false` 的页面必须能通过 `supersedes`（被新页取代）或 `part_of`（归属父页 / hub）找到对应的当前权威页；不允许出现游离的非 canonical 页。

## 5. 受控 tag 词表

### 5.1 词表

| 维度 | 标签 | 适用 type |
|---|---|---|
| 领域 | `agent`, `memory`, `skill`, `orchestration`, `llm-provider`, `rag`, `eval`, `observability`, `security`, `protocol` | 任意 |
| 来源风格 | `research`, `engineering`, `vendor-doc` | `entity / concept / comparison` |
| 产品哲学 | `product-philosophy`, `butler-positioning`, `interaction-design`, `subject-object-boundary`, `memory-design`, `proactivity`, `privacy` | `hub / adr / rule / concept` 中承载「本产品为何是这样」判断的页 |
| wiki 自身 | `meta`, `convention`, `process` | `hub`，或 `rule` 且 subtype ∈ `convention / style / process` |

注：原「形态」维度（`framework / pattern / paper / repo / tool / methodology`）已并入 §3.2 `subtype`，不再作为 tag。

### 5.2 使用规则

1. 每页至少 1 个 tag，至多 6 个；超过 6 个说明主题不聚焦，应拆分或转 hub。
2. 每页必须至少 1 个「领域」维度 tag，作为主分类锚点。
3. tag 必须取本节词表中的值；新增 tag 必须先改本节。
4. tag 必须 `kebab-case`、全小写。
5. tag 数组必须去重。
6. 同一页 tag 之间应语义正交；语义重叠时只保留更精确的一个。
7. 各维度的适用 type 见 §5.1 「适用 type」列；不得越界使用（例：entity 不贴产品哲学，hub 不贴 research）。

## 6. 正文模板

### 6.0 通用约定

- 每页 H1 必须等于 front matter `title`，紧跟 `> {summary}`，再进入第一个 H2；H1 与第一个 H2 之间不允许其它正文。
- 必备 H2 必须出现且按模板顺序排列；可选 H2（标 `<!-- 可选 -->`）可省略，若出现必须保留模板位置。
- 模板不复述 front matter 字段（如 `adr_status / adr_date / level`）；这些在 §4 已有契约，正文不重复声明。
- `相关页面` 章节列出本页 `related / part_of / supersedes` 中的链接；`来源` 章节列出本页 `sources` 中的链接。

### 6.1 Hub

> 必备 H2：`范围`、`从这里开始`、`相关页面`。可选 H2：`不在范围内`、`阅读地图`。

```markdown
# {标题}

> {summary}

## 范围

## 不在范围内       <!-- 可选 -->

## 从这里开始

## 阅读地图         <!-- 可选 -->

## 相关页面
```

### 6.2 Entity

> 必备 H2：`它是什么`、`为什么重要`、`关键机制`、`局限与边界`、`相关页面`、`来源`。可选 H2：`同义名`、`与本产品的关系`。

```markdown
# {标题}

> {summary}

## 它是什么

## 为什么重要

## 同义名               <!-- 可选 -->

## 关键机制

## 局限与边界

## 与本产品的关系       <!-- 可选 -->

## 相关页面

## 来源
```

### 6.3 Concept

> 必备 H2：`问题`、`简短结论`、`分析`、`局限`、`相关页面`、`来源`。可选 H2：`与本产品的关系`。

```markdown
# {标题}

> {summary}

## 问题

## 简短结论

## 分析

## 局限

## 与本产品的关系       <!-- 可选 -->

## 相关页面

## 来源
```

### 6.4 Comparison

> 必备 H2：`背景`、`候选方案`、`评估维度（矩阵）`、`选型与理由`、`相关页面`、`来源`。可选 H2：`落地数据 / 踩坑`。
> 评估矩阵列数与候选方案数一致；维度行建议至少覆盖能力、成本、风险、可逆性。

```markdown
# {标题}

> {summary}

## 背景

## 候选方案

## 评估维度（矩阵）

| 维度 | 方案 A | 方案 B | … |
|---|---|---|---|

## 选型与理由

## 落地数据 / 踩坑       <!-- 可选 -->

## 相关页面

## 来源
```

### 6.5 Rule（阿里 P3C 风格）

> 必备 H2：`规则`、`正例`、`反例`、`相关页面`。可选 H2：`例外`。
> `level` 由 front matter 承载，正文不重复。

```markdown
# {标题}

> {summary}

## 规则

## 正例

## 反例

## 例外                 <!-- 可选 -->

## 相关页面
```

### 6.6 ADR（Nygard 风格）

> 必备 H2：`背景`、`备选方案`、`被否决方案与理由`、`决策`、`影响`、`复议触发条件`、`相关页面`。
> `adr_status / adr_date / adr_revisit_when` 由 front matter 承载，正文不重复。

```markdown
# {标题}

> {summary}

## 背景

## 备选方案

## 被否决方案与理由

## 决策

## 影响

## 复议触发条件

## 相关页面
```

## 7. 规模与拓扑约束

### 7.1 写作风格

单页规模不设硬性字数上限，由内容深度决定。`entity / concept / comparison / adr` 类型遵循「问题 → 结论 → 机制 → 边界 → 落地 → 引用」的 6 步叙述骨架；具体 H2 已在 §6 各模板中给出，本节不重复列出。`hub / rule` 不需要叙述骨架（hub 是导航、rule 是规约）。

写作约束：

- 写作时只增加章节、不省略 §6 标记的必备 H2。
- 一页同时承载多个不相关主题时应拆分或转 hub。
- 单页可选 H2 数量超过模板规定 + 4 个时，lint 提示是否应拆分（提示性，非阻断）。

### 7.2 拓扑约束（机器可校）

Hub 拓扑：

- Hub 嵌套链路 ≤ **2 层**；嵌套指 `part_of: hub → hub` 的链路，不是物理目录。
- Hub 之间的 `part_of` 关系图必须为 DAG（无环）。
- **恰好一个 root hub**：`pages/hubs/agent-os.md`；该页须 `canonical: true`、`status: active`、`part_of: []`。
- 每个 hub 自身必须 `canonical: true`（hub 是某主题的入口权威）。
- 单 hub 的「从这里开始」清单建议 ≤ 12 项；超出时应拆分子 hub 或调整范围（提示性，非阻断）。

页面入站：

- 每个非 hub 页必须从至少一个 hub 或同类型页被链接到（无入站链接 = 孤岛）。
- canonical 唯一性见 §4.6.2，本节不再重述。

## 8. 工作流

### 8.1 Ingest（摄入新资料）

1. 读 `PURPOSE.md`，确认资料在收录范围内；不在范围则拒收。
2. 确认资料已由人类或外部流程放入 `raw/{category}/`；agent 只读 `raw/`，不得修改。
3. 检查同主题是否已有 `canonical: true` 页；若有，优先更新该页而不是新建（§1 #3 / §4.6.2）。
4. 仅当现有页面无法承接时新建；新建前按 §3.1 路由口诀确定 `type`，禁止发明新类型。
5. 准备 front matter（按 §4.1 / §4.2 / §4.3 规范），特别是 `canonical / verified_at / freshness`。
6. 从 §6 对应模板复制骨架起草；H1 必须等于 front matter `title`，紧跟 `> {summary}`；不得从空白页起写；可增加 H2，但不得删除必备 H2。
7. 至少从一个 hub 链接到新页（避免孤岛）；hub 嵌套与 root hub 见 §7.2。
8. 按 §8.3 阻断项自检；不通过不得合入。

### 8.2 Query（查询）

1. 先读 `PURPOSE.md` 与本 SCHEMA。
2. 从 root hub `pages/hubs/agent-os.md` 进入，逐级跳转到子 hub → canonical 页；不绕过 hub 直接全文搜索。
3. 回答中所有事实性判断必须配引用（wiki 页面相对路径或 `sources`）；引用粒度到页或更细。
4. 查不到答案时返回「未收录」并提示是否需要走 §8.1 ingest；不得基于通用知识硬编当前结论。
5. 仅当答案有持续复用价值时回写为页面或更新已有页。

### 8.3 Lint（健康检查）

> 在自动化 lint 上线前，每次 ingest / 编辑后须由 agent 按本节自检；违反阻断项不得合入。

#### 8.3.1 阻断项（lint error，必须修）

- front matter 必填字段是否完整、字段值是否合法。
- `tags` 是否全部在 §5 词表内、数量在 1–6、去重、`kebab-case` 全小写；至少 1 个领域 tag；维度未越界。
- `canonical` 同主题是否唯一；`status: archived` / `adr_status: superseded` 必须 `canonical: false`。
- `draft` 页面是否 `canonical: false`。
- `status: archived` 是否有非空 `archived_reason`、是否仍被当作主入口引用。
- `status` 转换是否符合 §4.6.1 矩阵；`archived` 不得回到其它状态。
- `stale` 且 `canonical: true` 进入 stale 超过 30 天（须降级或归档）。
- `adr_status` 是否单向推进（不得从 accepted 回 proposed，不得跳过 accepted 直接 superseded）。
- `adr_status: accepted` 页面是否有非空且可观察的 `adr_revisit_when`。
- `adr_status: superseded` 页面是否被某个新 ADR 通过 `supersedes` 正确指向。
- 类型专属字段是否仅出现在对应 type 上。
- 日期字段是否统一为 `YYYY-MM-DD`。
- 必备 H2 是否齐全且按 §6 顺序；H1 是否等于 front matter `title`；正文是否复述 front matter 字段（§6.0 禁止）。
- `sources` 路径是否真实存在、以 `raw/` 开头、符合 §4.4 路径格式。
- `supersedes / related / part_of` 路径是否真实存在、以 `pages/` 开头、符合 §4.5 格式、无自指、无指向 `raw/`、数组已去重。
- wiki 页面文件名、`pages/` 子目录名、`images/` 图片命名是否符合 §9；重命名时是否已同步更新 front matter 链接字段与正文链接。
- `supersedes` 关系图是否为 DAG（无环）。
- `part_of` 首元素是否为 hub。
- `canonical: false` 页面是否可通过 `supersedes` 或 `part_of` 追溯到当前权威页。
- root hub `pages/hubs/agent-os.md` 是否存在，且 `canonical: true / status: active / part_of: []`。
- 所有 hub 是否 `canonical: true`。
- hub→hub 的 `part_of` 链路是否为 DAG 且嵌套深度 ≤ 2。

#### 8.3.2 提示项（lint warning，建议修）

- 单页可选 H2 数量超过模板 + 4 个。
- 单 hub「从这里开始」清单 > 12 项。
- `freshness: volatile` 且 `verified_at` 超过 90 天。
- `status: archived` 页面仍被当作主入口引用。

#### 8.3.3 复查项（lint info，周期提醒）

- `freshness: evergreen` 且 `verified_at` 超过 365 天。
- `adr_status: proposed` 长期未推进到 accepted。
- 孤岛页面（无入站链接）。

### 8.4 Supersede（取代旧决策）

1. 新建一份 ADR 或 rule，按 §6.6 / §6.5 模板起草。
2. 在新页 front matter `supersedes` 中加入旧页路径（§4.5 字段专属规则）。
3. 旧页 `adr_status` 改为 `superseded` 或 `status` 改为 `archived`，并设 `canonical: false`。
4. 检查旧页是否仍被 hub / 其它页作为主入口引用；如有，改指新页。
5. 按 §8.3 阻断项自检。

### 8.5 Archive（归档）

1. 确认页面无后续维护价值；ADR 应优先走 §8.4 supersede 而非直接 archive。
2. 设 `status: archived`、`canonical: false`、填写非空 `archived_reason`。
3. 移除该页作为主入口的所有 hub 引用；保留历史 `related / supersedes` 反向追溯。
4. 按 §8.3 阻断项自检。

## 9. 命名与语言

### 9.1 Wiki 页面路径

- `pages/**/*.md` 文件名必须为全小写 `kebab-case.md`，只允许英文字母、数字与连字符。
- slug 应表达主题，不表达 `type / subtype / status / 日期`；这些信息由目录与 front matter 承载。
- 页面创建后应尽量保持 slug 稳定；确需重命名时，必须同步更新所有 `supersedes / related / part_of` 与正文链接。
- `pages/{type}/` 下子目录命名规则同文件名，且仍须遵守 §2.1 增长规则。

### 9.2 页面标题

- `title` 与 H1 可中文，且二者必须一致（见 §6.0）。
- 专有名词保留官方写法；中文标题中可混用英文专名，例如 `OpenAI Agents SDK 的工具调用模型`。
- 标题可随内容演化调整；slug 不因标题措辞微调而频繁变更。

### 9.3 图片与资产

- wiki 正文引用的图片放入 `images/`；`raw/` 原始资料引用的图片放入 `raw/assets/`。
- wiki 图片命名为 `{page-slug}-{purpose}.{ext}`，其中 `purpose` 也必须为全小写 `kebab-case`。
- 同一页面同一用途多图时追加序号：`{page-slug}-{purpose}-01.{ext}`。
- 图片扩展名限于常见静态格式：`.png / .jpg / .jpeg / .webp / .svg`。

### 9.4 语言策略

- wiki 页面默认中文优先，不再默认维护 `.md` / `.zh-CN.md` 双语 twin。
- 只有当英文版本本身是交付物或对外发布物时，才允许新增英文 twin；新增前须在对应页面说明维护责任。
- `raw/` 文件内容保留原始语言；agent 不为了统一语言而改写、翻译或覆盖 `raw/` 文件。

## 10. 红线

> 本节只陈述 agent 不得跨越的行为边界；字段值、模板、路径、状态机等细则由对应主体章节定义。若本节与主体章节冲突，以更严格者为准。

### 10.1 资料与派生物

1. 写操作前不跳过 `PURPOSE.md` 与本 SCHEMA。
2. 不修改 `raw/` 下的任何文件；`raw/` 的新增、替换、删除只能由人类或外部流程完成。
3. 不手维 `overview.md` 与 `log.md`；它们是派生物，只能由脚本或约定流程生成。
4. 不收录 `PURPOSE.md` 范围之外的资料。

### 10.2 事实与范围

5. 不写无法追溯到 `sources` 的事实性判断。
6. 不基于通用知识硬编 wiki 的当前结论；查不到时返回「未收录」，并提示是否走 §8.1 ingest。
7. 不把 `draft / archived / superseded` 页面作为当前结论或主入口引用。

### 10.3 结构契约

8. 不创建无完整、合法 front matter 的 wiki 页。
9. 不发明任何受控枚举，包括 `type / subtype / tag / status / freshness / level / adr_status`；确需新增须先改本 SCHEMA。
10. 不新增 `docs/` 顶层目录或 `pages/` 下的 type 目录；确需新增须先改本 SCHEMA。
11. 不删除页面模板规定的必备 H2，不从空白页绕开模板起草。
12. 不创建无法按 §3.1 路由的页面；无法路由时停下来问人类。

### 10.4 生命周期与拓扑

13. 不物理删除 wiki 页面；退役页面必须使用 `status: archived` + `archived_reason`，取代关系走 `supersedes`。
14. 不让同一主题出现两页 `canonical: true`。
15. 不创建孤岛页面；新页必须至少能从一个 hub 或同类型页到达。
16. 不绕过 root hub 建立平行入口；root hub 固定为 `pages/hubs/agent-os.md`。

## 11. 派生物

以下文件不是启动前提，由脚本在自动化就绪后生成：

- `overview.md`：全局快照（取代旧版手维 `index.md`）。
- `log.md`：ingest / query / decision 的追加式日志。

## 12. 版本历史

| 版本 | 日期 | 关键变更 |
|---|---|---|
| 2.0 | 2026-04-27 | 重建版：最小可控模型，4 类页面 |
| 3.0 | 2026-04-27 | 拆分 `topic` → `concept + comparison`、`decision` → `rule + adr`；新增 PURPOSE.md / canonical / verified_at / freshness / 受控 tag 词表 / 规模约束；明确 SCHEMA 为单一真相源 |
| 3.1 | 2026-04-27 | ADR 增 `adr_date` / `adr_revisit_when`；tag 增"产品哲学"维度；`supersedes/related/part_of` 链接格式锚定为相对路径；§7 移除 2500 字硬上限改为内容驱动；§8.3 lint 增 superseded ADR 与链接存在性检查；§10 红线增"不收录 PURPOSE 范围外资料" |
| 3.2 | 2026-04-27 | 修正红线重复；明确 ingest 时 agent 只读 `raw/`；同步 ADR 模板字段；Entity 模板增同义名；front matter 示例标注 `subtype` 为可选 |
| 3.3 | 2026-04-27 | §7 取消 H1 > 8 / 5 分钟主张等硬性规模约束，改为参考国内一线互联网公司工程博客的写作风格；§8.3 lint 同步移除 H1 计数检查 |
| 3.4 | 2026-04-27 | §7 锁定写作风格为美团技术博客式，给出 6 步叙述骨架；拆为 §7.1 写作风格 + §7.2 拓扑约束 |
| 3.5 | 2026-04-27 | §6 各模板加必备 H2 清单；§8.1 ingest 增从模板复制骨架步；§8.3 lint 增必备 H2 检查 + evergreen 365 天提示；§2 raw/meetings 注释更新 |
| 3.6 | 2026-04-27 | §2.1 增长规则改为单一阈值（≥12 加一层子目录）；删除模糊的活跃/稳定双阈值与已废弃旧目录名；明确子目录仅作物理分组 |
| 3.7 | 2026-04-27 | §2.1 增长规则拆成可 lint 的原子规则；明确新增 type 目录必须先修改 SCHEMA |
| 3.8 | 2026-04-27 | §2.1 收紧子目录条件（"才可"+"未达 12 必须扁平"）；禁止子目录名复用 subtype 枚举，要求按主题域命名 |
| 3.9 | 2026-04-27 | §10 红线补全：#6 扩展覆盖 type 目录；新增 #9 canonical 唯一、#10 不得删除必备 H2 |
| 3.10 | 2026-04-27 | §10 红线纯行为化：去除跨节引用（PURPOSE §3 / §6），在节首加入指针说明 |
| 3.11 | 2026-04-27 | §3.1 路由口诀改为按排他性排序的判定链（rule→adr→comparison→entity→concept→hub），明确分辨条件 |
| 3.12 | 2026-04-27 | §3.1 细化 rule/adr/entity/concept 边界；ADR 适用范围扩展为产品 / 架构决策 |
| 3.13 | 2026-04-27 | §3.2 增 subtype 使用规则；ADR subtype 新增 `product`，用于产品决策 / 产品哲学 ADR |
| 3.14 | 2026-04-27 | §3.2 规则 2 加反例锚点（`framework` 是 subtype 不是 tag；`memory` 是 tag 不是 subtype），便于 lint 提示 |
| 3.15 | 2026-04-27 | §4 Front Matter 重构为机器可校契约：明确空值、日期、通用字段、类型专属字段、sources、链接字段、status/canonical 关系；§8.3 lint 同步 |
| 3.16 | 2026-04-27 | 统一采用 `front matter` 术语，避免拼写检查误报 |
| 3.17 | 2026-04-27 | §4.1 / §4.2 同步链接字段注释与字段表：明确 `sources` 仅为 `raw/` 路径、`part_of` 指向范围 |
| 3.18 | 2026-04-27 | §4.3 重构为字段表 + 状态机：rule `level` 给定义；ADR 状态单向推进、`adr_date` 语义重定为「进入当前状态的日期」、`accepted` 强制非空 `adr_revisit_when`；§8.3 lint 同步 |
| 3.19 | 2026-04-27 | §4.4 重构：sources 出现规则改为映射表；明确路径格式（`raw/` 开头、kebab-case、文档 `.md`、禁 URL/绝对路径）；可追溯性与文件存在性显式化；§8.3 lint 增 sources 路径与格式检查 |
| 3.20 | 2026-04-27 | §4.5 重构：拆为通用路径规则 + 字段专属规则 + 风格建议；明确 `pages/` 前缀与 kebab-case `.md`、数组去重、`supersedes` DAG、`part_of` 首元素必须为 hub；§8.3 lint 同步 |
| 3.21 | 2026-04-27 | §4.6 重构：拆 `status` 生命周期矩阵 / canonical 耦合 / 非 canonical 可达性；明确 `draft` 不得 canonical、`stale` 超 30 天必须降级、`archived` 终态且不得作主入口；§8.3 lint 同步 |
| 3.22 | 2026-04-27 | §5 重构：移除「形态」维度（已并入 §3.2 subtype）；新增「适用 type」列；新增 §5.2 使用规则（数量上限 6、必须有领域 tag、kebab-case、维度不得越界）；§8.3 lint 同步 |
| 3.23 | 2026-04-27 | §6 重构：新增 §6.0 通用约定（H1=title、H2 顺序、不复述 front matter）；模板内用 `<!-- 可选 -->` 标注可选 H2；删 Rule/ADR 模板的 front matter 重复 blockquote；entity/concept 增可选「与本产品的关系」；hub 增可选「不在范围内 / 阅读地图」；comparison 矩阵改注释驱动；§8.3 lint 同步 |
| 3.24 | 2026-04-27 | §7 重构：去掉「美团」品牌锚点，§7.1 收敛为指向 §6 的写作风格；§7.2 收紧 hub 拓扑（嵌套指 `part_of` 链路、hub→hub DAG、root hub 三件套、hub 自身必须 canonical、「从这里开始」≤ 12 项提示）；§6.4 标题去品牌；§8.3 lint 同步 |
| 3.25 | 2026-04-27 | §8 重构：§8.1 ingest 步骤序与 §4/§6 对齐（先 front matter 再模板）、增 canonical 冲突检查与自检关卡；§8.2 query 收紧（从 root hub 进入、必须配引用、查不到返回「未收录」）；§8.3 lint 分三档（阻断/提示/复查）并加自检声明；新增 §8.4 supersede 与 §8.5 archive 工作流 |
| 3.26 | 2026-04-27 | §9 重构：拆为 wiki 页面路径 / 页面标题 / 图片与资产 / 语言策略；明确 slug 稳定性、重命名同步、图片命名与扩展名、英文 twin 例外条件；§8.3 lint 增命名检查 |
| 3.27 | 2026-04-27 | §10 重构：红线按资料与派生物 / 事实与范围 / 结构契约 / 生命周期与拓扑分组；补充写前读取、未收录处理、禁止引用非当前页、禁止绕过模板、禁止孤岛与 root hub 平行入口；同步 AGENTS.md 薄摘要 |

---

*Schema 版本：3.27 | 最后更新：2026-04-27 | 协作演化：人类 + LLM*
