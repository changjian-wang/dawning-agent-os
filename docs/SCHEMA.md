# SCHEMA.md — Dawning Agent OS Wiki 结构契约

> 本文件是 LLM-Wiki 的**结构契约**：定义目录、页面类型、frontmatter、模板、流程与红线。
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
│   ├── meetings/           # 会议 / 讨论记录（遗留，新资料尽量不进此目录）
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

- `pages/` 下默认扁平。
- 某一目录活跃页面 > 20 或同类稳定页面 ≥ 5 时，才允许加一层子目录。
- 不允许新增顶层目录，须先改本 SCHEMA。
- 不允许恢复旧版 `synthesis/` `readings/` `frameworks/{x}/tier-N/` 等结构。

## 3. 页面类型

共 6 种主类型，与目录一一对应：

| type | 路径 | 适用 | 不适用 |
|---|---|---|---|
| `hub` | `pages/hubs/` | 主题入口、阅读顺序、范围划分 | 长篇论证 |
| `entity` | `pages/entities/` | 框架、协议、工具、论文、仓库等具名对象 | 跨对象比较、概念解释 |
| `concept` | `pages/concepts/` | 概念、模式、方法论的解释 | 横向选型 |
| `comparison` | `pages/comparisons/` | 多对象横向对比 / 选型分析 | 单对象介绍 |
| `rule` | `pages/rules/` | 强制 / 推荐 / 参考的硬规则 | 可被推翻的架构选择 |
| `adr` | `pages/adrs/` | 架构决策记录（可被新决策 supersede） | 不可违反的红线 |

### 3.1 路由口诀

- 具名对象 → `entity`
- 解释为什么 / 怎么理解 → `concept`
- 多对象横向选型 → `comparison`
- 不可违反的硬规则 → `rule`
- 可被推翻的架构选择 → `adr`
- 只是导航 → `hub`
- 拿不准 → 不要新建页面，先和人类确认；禁止发明新类型

### 3.2 可选 subtype

`subtype` 仅用于轻量语义标注，不参与目录约束。新增 subtype 须先改本 SCHEMA。

| type | 允许的 subtype |
|---|---|
| `hub` | `map` |
| `entity` | `framework`, `protocol`, `tool`, `paper`, `repo` |
| `concept` | `pattern`, `methodology`, `theory`, `workflow` |
| `comparison` | `landscape`, `selection`, `tradeoff` |
| `rule` | `convention`, `style`, `process` |
| `adr` | `architecture`, `tooling`, `scope` |

## 4. Frontmatter 规范

每个 wiki 页面必须包含完整 frontmatter：

```yaml
---
title: 页面标题
type: hub | entity | concept | comparison | rule | adr
subtype: framework              # 可选；按 type 选择，不适用可省略
canonical: true                  # 同主题最多一页 true
summary: 一句话摘要
tags: [agent, memory]            # 受控词表（见 §5）
sources: [raw/papers/example.md] # 来源
created: 2026-04-27
updated: 2026-04-27
verified_at: 2026-04-27          # 最近一次确认事实仍准确
freshness: evergreen             # evergreen | volatile
status: draft                    # draft | active | stale | archived
archived_reason: ""              # status=archived 时必填
supersedes: []                   # 可选，本页取代的旧页（路径）
related: []                      # 可选，强相关页面（路径）
part_of: []                      # 可选，所属 hub 或父概念（路径）
---
```

### 4.1 字段表

| 字段 | 必填 | 说明 |
|---|---|---|
| `title` | 是 | 页面标题 |
| `type` | 是 | 6 种主类型之一 |
| `subtype` | 否 | 受控枚举 |
| `canonical` | 是 | `true` / `false`；同主题最多一页 `true` |
| `summary` | 是 | 一句话页面意图 |
| `tags` | 是 | 受控词表，至少 1 个 |
| `sources` | 是 | 来源路径数组；`entity/concept/comparison` 必须非空 |
| `created` | 是 | 创建日期 |
| `updated` | 是 | 最近更新日期 |
| `verified_at` | 是 | 最近一次确认仍准确的日期 |
| `freshness` | 是 | `evergreen`（长期稳定）或 `volatile`（需定期复查） |
| `status` | 是 | `draft / active / stale / archived` |
| `archived_reason` | 条件 | `status: archived` 时必填 |
| `supersedes` | 否 | 取代关系列表（路径） |
| `related` | 否 | 相关页面列表（路径） |
| `part_of` | 否 | 所属父页（路径） |

**链接字段格式约定**：`supersedes / related / part_of` 中的元素一律写**相对 `docs/` 的路径**，例如 `pages/adrs/options-over-elaboration.md`。不写 wikilink、不写裸标题、不写绝对路径。

### 4.2 类型专属字段

`type: rule` 必须额外包含：

```yaml
level: 强制 | 推荐 | 参考
```

`type: adr` 必须额外包含：

```yaml
adr_status: proposed | accepted | superseded
adr_date: 2026-04-27          # 决策日期（首次 accept 的日期；superseded 后保留原始日期）
adr_revisit_when: ""          # 触发复议的条件，可为空字符串；非空时 lint 会校验是否为可观察的事件而非空话
```

### 4.3 sources 规则

- `entity / concept / comparison`：`sources` 必须非空。
- `hub / rule / adr`：`sources` 可为空数组 `[]`。
- 路径相对 `docs/` 写：`raw/papers/example.md`。
- 页面中无法回溯到 `sources` 的事实性判断必须删除或补来源。

## 5. 受控 tag 词表

新增 tag 必须先修改本节。当前允许的 tag：

| 维度 | 标签 |
|---|---|
| 领域 | `agent`, `memory`, `skill`, `orchestration`, `llm-provider`, `rag`, `eval`, `observability`, `security`, `protocol` |
| 形态 | `framework`, `pattern`, `paper`, `repo`, `tool`, `methodology` |
| 来源风格 | `research`, `engineering`, `vendor-doc` |
| 产品哲学 | `product-philosophy`, `butler-positioning`, `interaction-design`, `subject-object-boundary`, `memory-design`, `proactivity`, `privacy` |
| wiki 自身 | `meta`, `convention`, `process` |

**产品哲学维度何时使用**：当页面承载的是"本产品为何是这样"的判断（管家定位、选择题优先、主客体边界、长期记忆作为核心等）时贴此维度的 tag。研究/通用领域知识不贴此维度。

## 6. 正文模板

### 6.1 Hub

```markdown
# {标题}

> {summary}

## 范围

## 从这里开始

## 相关页面
```

### 6.2 Entity

```markdown
# {标题}

> {summary}

## 它是什么

## 为什么重要

## 同义名

## 关键机制

## 局限与边界

## 相关页面

## 来源
```

### 6.3 Concept

```markdown
# {标题}

> {summary}

## 问题

## 简短结论

## 分析

## 局限

## 相关页面

## 来源
```

### 6.4 Comparison（美团技术博客风格）

```markdown
# {标题}

> {summary}

## 背景

## 候选方案

## 评估维度（矩阵）

| 维度 | 方案 A | 方案 B | 方案 C |
|---|---|---|---|

## 选型与理由

## 落地数据 / 踩坑

## 相关页面

## 来源
```

### 6.5 Rule（阿里 P3C 风格）

```markdown
# {标题}

> level: 强制 | 推荐 | 参考
> {summary}

## 规则

## 正例

## 反例

## 例外

## 相关页面
```

### 6.6 ADR（Nygard 风格）

```markdown
# {标题}

> adr_status: proposed | accepted | superseded
> adr_date: YYYY-MM-DD
> adr_revisit_when: {可观察的复议触发条件，可为空}
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

- 单页规模由**内容**决定，不设硬字数上限；但当一级章节 > 8 个或单页同时讲多个主题时，必须拆分或转 hub。判断依据：一个反应式问题——"读者能否在 5 分钟内抓到这页的主张？"答案是"否"就要拆。
- Hub 嵌套 ≤ **2 层**。
- **恰好一个 root hub**：`pages/hubs/agent-os.md`。
- 每个非 hub 页必须从至少一个 hub 或同类型页被链接到（无入站链接 = 孤岛）。
- 同主题最多一页 `canonical: true`；其它页只能引用，不复述事实。

## 8. 工作流

### 8.1 Ingest（摄入新资料）

1. 读 `PURPOSE.md`，确认资料在收录范围内；不在范围则拒收。
2. 确认资料已由人类或外部流程放入 `raw/{category}/`；agent 只读 `raw/`，不得修改。
3. 先尝试更新现有页面；现有页面无法承接才新建。
4. 新建页面前先确定 `type`，禁止发明新类型。
5. 完整填写 frontmatter，特别是 `canonical / verified_at / freshness`。
6. 至少从一个 hub 链接到新页（避免孤岛）。

### 8.2 Query（查询）

1. 先读 `PURPOSE.md` 与本 SCHEMA。
2. 从 `pages/hubs/` 进入。
3. 回答时引用具体页面与 `sources`。
4. 仅当答案有持续复用价值时回写为页面或更新已有页。

### 8.3 Lint（健康检查）

周期性检查：

- frontmatter 必填字段是否完整、字段值是否合法。
- `tags` 是否全部在受控词表内。
- `canonical` 是否唯一。
- 孤岛页面（无入站链接）。
- `freshness: volatile` 且 `verified_at` 超过 90 天的页面。
- `status: archived` 页面是否仍被当作主入口引用。
- `adr_status: superseded` 的页面是否被某个新 ADR 通过 `supersedes` 正确指向。
- `supersedes / related / part_of` 中的路径是否真实存在。
- 单页一级章节数是否超过 §7 的拓扑约束。

## 9. 命名与语言

- 文件名：`kebab-case.md`。
- 页面标题：可中文。
- 图片命名：`{page-slug}-{purpose}.{ext}`。
- 默认中文优先；不再默认维护 `.md` / `.zh-CN.md` 双语 twin。
- `raw/` 保留原始语言。

## 10. 红线

1. 不修改 `raw/` 下的任何文件。
2. 不写无法追溯到 `sources` 的事实性判断。
3. 不创建无 frontmatter 的页面。
4. 不删除页面；用 `status: archived` + `archived_reason`。
5. 不发明新的 `type`、`subtype` 或 `tag`，须先改本 SCHEMA。
6. 不新增顶层目录，须先改本 SCHEMA。
7. 不收录 [PURPOSE.md](./PURPOSE.md) §3 范围之外的资料。
8. 不手维 `overview.md` 与 `log.md`，它们是派生物。

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
| 3.2 | 2026-04-27 | 修正红线重复；明确 ingest 时 agent 只读 `raw/`；同步 ADR 模板字段；Entity 模板增同义名；frontmatter 示例标注 `subtype` 为可选 |

---

*Schema 版本：3.2 | 最后更新：2026-04-27 | 协作演化：人类 + LLM*
