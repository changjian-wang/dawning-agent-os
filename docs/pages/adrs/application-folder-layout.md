---
title: ADR-021 Application 项目目录约定：Abstractions / Messaging / 垂直切片
type: adr
subtype: architecture
canonical: true
summary: Dawning.AgentOS.Application 内部不使用 Common/ 总目录，改为按职责扁平：Abstractions/ 放端口、Messaging/ 放 CQRS marker、<Feature>/ 放垂直切片，跨切片 DTO 在第二次出现时按主题单独建文件夹。
tags: [agent]
sources: []
created: 2026-04-30
updated: 2026-04-30
verified_at: 2026-04-30
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/engineering-skeleton-v0.md, pages/adrs/backend-architecture-equinox-reference.md, pages/adrs/architecture-test-assertion-strategy.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-30
adr_revisit_when: "Application 项目根的横切文件夹数量 ≥ 5；或出现一类既非端口、亦非 CQRS marker、亦非垂直切片、亦非跨切片 DTO 的新类型；或编排扁平布局所需的架构测试断言成本超过维护一个 Common/ 总目录的成本时；或上游 ADR-017 / ADR-018 因其它原因被整体 supersede 时。"
---

# ADR-021 Application 项目目录约定：Abstractions / Messaging / 垂直切片

> Dawning.AgentOS.Application 内部不使用 Common/ 总目录，改为按职责扁平：Abstractions/ 放端口、Messaging/ 放 CQRS marker、<Feature>/ 放垂直切片，跨切片 DTO 在第二次出现时按主题单独建文件夹。

## 背景

[ADR-017](engineering-skeleton-v0.md) 工程骨架 V0 列出了 Application 项目内部的两个文件夹：`Abstractions/`（端口）与 `Common/`（结果、分页、错误模型）。[ADR-018](backend-architecture-equinox-reference.md) 后端架构参考又把 CQRS marker 接口的位置定到了 `Application/Common/Messaging/`。两份决策都没有解释「为什么端口去 `Abstractions/`、消息接口去 `Common/`、其它共享类型也去 `Common/`」，于是出现一个隐性问题：`Common/` 的语义判据缺失。

`Common/` 这个名字不能回答「放在这里是什么职责」。它只能回答「放在这里不是别的某种东西」。一个文件夹的命名如果只能用排除法定义，本质上是反模式：

- 一年后 `Common/Helpers/`、`Common/Utils/`、`Common/Misc/` 会自然繁殖。
- 新加一类共享类型（pagination、validation envelope、error mapping 等）时无法机器判定它该不该进 `Common/`。
- 架构测试无法对 `Common/` 写出有意义的禁向断言：语义为空就没有可禁的方向。

S3 切片即将开始 scaffold Application 项目，这是定型的最后机会；如果带着 `Common/` 进入第二刀业务切片，后续每一个跨切片 DTO 都要在 PR 中重新讨论一次「这是 Common 还是别的」。

## 备选方案

- **方案 A**：维持 ADR-017 / ADR-018 现状，用 README 写下 `Common/` 与 `Abstractions/` 的判据。
- **方案 B**：把 `Messaging/` 并入 `Abstractions/`，全部「Application 对外暴露 / 依赖反转的接口」同住。
- **方案 C**：取消 `Common/`，全部扁平到 Application 项目根（含 marker、跨切片 DTO、垂直切片）。
- **方案 D**：按职责分类的扁平化——`Abstractions/`（端口）、`Messaging/`（CQRS marker）、`<Feature>/`（垂直切片）、跨切片 DTO 第二次出现时按主题单独建文件夹（如 `Pagination/`、`Errors/`），永远不建 `Common/` / `Shared/` 这类无判据的总目录。

## 被否决方案与理由

**方案 A 维持 Common/ + README**：

- README 是文档，不是约束；半年后新人不会记得多层判据，`Common/` 仍会繁殖。
- 文档保护不了反模式的目录名；任何「别的不知道放哪就放这里」的判据都没法机器化。
- 架构测试无法对 `Common/` 写出禁向断言，违反 [ADR-020](architecture-test-assertion-strategy.md) 强调的「机器可校的边界」原则。

**方案 B 把 Messaging 并入 Abstractions**：

- 语义错位。`ICommand` / `IQuery` 是 Application 自我消费的 marker，没有「被 Infra 实现」这种依赖反转关系。把它放进 `Abstractions/` 会让该文件夹失去单一判据（端口 = Infra 实现）。
- 一旦 `Abstractions/` 的判据松动，未来其它「看起来像接口」的类型也会自由进入，最终回到 A 的局面。

**方案 C 完全扁平到根**：

- 方向对，但漏一类。跨切片 DTO（pagination / errors）确实需要一个家：直接铺在根目录会与垂直 feature 文件夹混排。
- 预先建一个根级 `Common/` / `Shared/` 兜底，又退回 A。
- C 的真正问题是「漏说了跨切片 DTO 怎么办」；只要补上这一条规则就能升级为 D。

## 决策

采用方案 D。Application 项目内部目录采用以下扁平职责分类：

```text
src/Dawning.AgentOS.Application/
  Abstractions/              # Ports：Application 声明，Infra.* 项目实现
    IClock.cs
    IRuntimeStartTimeProvider.cs

  Messaging/                 # CQRS marker / framework contract，Application 自我消费
    ICommand.cs
    IQuery.cs

  <Feature>/                 # 垂直切片（业务名词），含 Command / Query / Handler / DTO / Validator
    Runtime/
      GetRuntimeStatusQuery.cs
      GetRuntimeStatusQueryHandler.cs
      RuntimeStatus.cs

  <CrossCutting>/            # 跨切片 DTO，第二次出现时按主题单独建（不预先建空目录）
    Pagination/              # 例：当 ≥ 2 个 feature 都需要分页时才出现
    Errors/                  # 例：当出现共享 ApplicationError 扩展时才出现
```

### 四类文件夹的唯一判据

| 文件夹类型 | 判据 | 反例 |
|------------|------|------|
| `Abstractions/` | 接口在此声明，**所有具体实现都在 `Dawning.AgentOS.Infra.*` 项目** | 实现也在 Application 项目 → 不放此处 |
| `Messaging/` | CQRS marker / pipeline contract，**没有外部实现者**，Application 自我消费 | 接口被 Infra 实现 → 它是端口，应进 `Abstractions/` |
| `<Feature>/` | 业务名词；Command / Query / Handler / DTO 协同表达一个用例 | 跨多个 feature 共享 → 不放某个 feature 文件夹 |
| `<CrossCutting>/`（如 `Pagination/`） | **≥ 2 个 feature** 都需要的纯数据类型，按主题独立成文件夹 | 单 feature 使用 → 放 feature 内部，不晋升 |

### 「第二次原则」

跨切片共享类型只在**第二次**出现时才晋升到根级横切文件夹。第一次出现的类型留在它所在的 feature 内部。这是 Rule of Three 的弱化版（Two），用于阻止"以后可能用到"驱动的预先抽象。

### 严禁的目录名

- ❌ `Common/`
- ❌ `Shared/`
- ❌ `Helpers/` / `Utils/` / `Misc/`
- ❌ `Infrastructure/`（在 Application 内）
- ❌ 任何只能用排除法定义的名字

### 与 ADR-017 / ADR-018 的关系

本 ADR 仅替换两个上游 ADR 中关于 Application 项目内部目录的部分：

- 取代 [ADR-017](engineering-skeleton-v0.md) 中 `Application/Common/` 的存在前提。ADR-017 其它内容（项目划分、依赖方向、V0 通电范围）继续生效。
- 取代 [ADR-018](backend-architecture-equinox-reference.md) §Mediator 中 `Common/Messaging/` 的具体路径，改为根级 `Messaging/`。ADR-018 其它内容（MediatR v12.x、Result 模式、IUnitOfWork、自动注册策略）继续生效。

由于本 ADR 仅 refine 两份上游 ADR 的局部细节而非全面取代，`supersedes` 字段保持为空。两份上游 ADR 仍为 `accepted`，下次更新时正文加一行回链到本 ADR 即可。

### 架构测试新增断言

在 `LayeringTests` 中追加：

- `Application_AbstractionsFolder_OnlyContainsInterfaces`：使用 NetArchTest，断言 `Dawning.AgentOS.Application.Abstractions` 命名空间下的类型都是 `interface`，禁止出现 concrete class。这是「Abstractions = 端口」判据的机器化。

后续 Infra.* 项目落地后再补：

- `Infra_StarProvidesImplementationsForApplicationAbstractions`：断言 `Application.Abstractions` 中的每个接口都至少有一个实现来自 `Dawning.AgentOS.Infra.*` 程序集。

`Messaging/` 与 `<Feature>/` 不需要专属断言；它们的形态由 ADR-018 既有的「Pipeline Behavior 必须放 Infra.CrossCutting.Bus」、「实现 ICommand / IQuery 的类型必须在 Application」两条断言间接覆盖。

## 影响

**正向影响**：

- 每个文件夹都有单一可机器判定的判据；新增类型时无歧义。
- 架构测试可以钉死 `Abstractions/` 只能装接口；`Common/` 这个语义空洞的目录消失。
- 跨切片 DTO 在第二次出现时才晋升，避免 YAGNI 抽象与"以后可能用到"的预建空目录。
- 与 [ADR-018](backend-architecture-equinox-reference.md) 的 `Infra.CrossCutting.*` 拆分逻辑一致：每个 cross-cutting 文件夹都有独立职责，不堆在一个 `CrossCutting/` 总目录下。
- 与 [ADR-020](architecture-test-assertion-strategy.md) 强调的"机器可校边界"一致。

**代价 / 风险**：

- ADR-017 / ADR-018 的旧目录提法在历史阅读时与本 ADR 不一致；需要在两份上游 ADR 后续更新时各加一行回链到本 ADR。
- 跨切片 DTO 出现频率较高时，根级横切文件夹数量会增长（`Pagination/`、`Errors/`、`Mapping/`...）；本 ADR 接受这个代价，并把"≥ 5 个根级横切文件夹"作为复议触发条件。
- 「第二次原则」要求 reviewer 在 PR 中识别"这是第几次出现"；可能会出现误判（提前晋升或滞后晋升）。误判可后续修正，不会造成结构性损害。
- 团队规模扩大后，新成员需要先读本 ADR 才能理解为什么没有 `Common/`；通过 Application 项目根 README 索引到本 ADR 缓解。

## 复议触发条件

- Application 项目根的横切文件夹数量 ≥ 5（如 `Pagination/`、`Errors/`、`Validation/`、`Mapping/`、`Diagnostics/`...），导致根目录可读性下降。
- 出现一类既非端口、亦非 CQRS marker、亦非垂直切片、亦非跨切片 DTO 的新类型，无法纳入现有四类。
- 编排扁平布局所需的架构测试断言成本（如「Abstractions 只装接口」断言频繁失效或误报）超过维护一个 `Common/` 总目录的成本。
- 上游 ADR-017 / ADR-018 因其它原因被整体 supersede 时，本 ADR 同步复议是否仍适用新的项目划分。

## 相关页面

- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：上游决策，Application 项目存在的总前提；本 ADR refine 其内部目录形态。
- [ADR-018 后端架构参考 Equinox：DDD + MediatR + Result 模式](backend-architecture-equinox-reference.md)：上游决策，CQRS / Result / IUnitOfWork 等 Application 层心智来源；本 ADR refine 其 §Mediator 中 `Common/Messaging/` 的路径。
- [ADR-020 架构测试断言策略](architecture-test-assertion-strategy.md)：本 ADR 的「Abstractions 只装接口」断言遵循其类型级断言到具体类型名的纪律。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 是 S3 实现前的方案确认产物。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
