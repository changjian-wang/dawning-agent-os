---
title: ADR-019 测试栈：NUnit + Moq + NetArchTest
type: adr
subtype: tooling
canonical: true
summary: V0 后端测试栈锁定 NUnit 4.4.x + NUnit3TestAdapter + Microsoft.NET.Test.Sdk + Moq 4.20.x + coverlet 6.x，断言使用 NUnit 原生 Assert.That，不引入第三方 fluent 断言库；架构测试沿用 ADR-018 的 NetArchTest.Rules。
tags: [agent]
sources: []
created: 2026-04-30
updated: 2026-05-01
verified_at: 2026-05-01
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/engineering-skeleton-v0.md, pages/adrs/backend-architecture-equinox-reference.md, pages/adrs/architecture-test-assertion-strategy.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-30
adr_revisit_when: "NUnit 4.x 与目标 .NET 主版本破坏二进制兼容；Moq 出现 SponsorLink 类商业化变动且无平滑替代；测试套件超过 10000 用例需复议性能差异；本项目长期 contributor 规模显著扩大且新成员普遍来自 xUnit 生态；或社区出现成熟非商业 fluent 断言替代 FluentAssertions 时。"
---

# ADR-019 测试栈：NUnit + Moq + NetArchTest

> V0 后端测试栈锁定 NUnit 4.4.x + NUnit3TestAdapter + Microsoft.NET.Test.Sdk + Moq 4.20.x + coverlet 6.x，断言使用 NUnit 原生 Assert.That，不引入第三方 fluent 断言库；架构测试沿用 ADR-018 的 NetArchTest.Rules。

## 背景

[ADR-017](engineering-skeleton-v0.md) 已规划 V0 测试项目布局（Architecture / Domain / Domain.Services / Application / Infrastructure / Api 六个测试项目），[ADR-018](backend-architecture-equinox-reference.md) 已锁定架构测试库 NetArchTest.Rules v2.x，但**这两份决策都没有锁定单元测试框架、断言库与 Mock 库**。如果在 S1 开始 scaffold 测试项目时再口头决定，几条风险会一起浮出：

- xUnit / NUnit / MSTest 三选一在团队内反复争论，每次新建测试项目都要重新解释一遍。
- 断言风格在 `Assert.Equal(...)` / `Assert.That(...)` / FluentAssertions `.Should()` 之间分裂。
- FluentAssertions v8 起转为商业 license，引入后随时面临"要么付费要么迁移"。Shouldly 等替代品社区维护强度不一。
- Moq 历史上的 SponsorLink 风波（4.20.0）已经让生态对 mock 库可持续性敏感；选型理由要写下来。
- dawning / dawning-agents / dawning-assistant 三仓库历史上选用 xUnit；但 dawning-agents 已被本仓库 owner 标记弃用，dawning / dawning-assistant 后续是否跟随本项目改 NUnit 不在本 ADR 范围内。

需要一份独立 ADR 把测试栈锁死，作为 S1 测试项目 scaffold、未来 CI 配置、覆盖率门禁的统一来源。

## 备选方案

- **方案 A**：xUnit 2.9.x + 原生 `Assert` + Moq 4.20.x + coverlet。与历史 dawning / dawning-agents / dawning-assistant 一致。
- **方案 B**：xUnit 2.9.x + FluentAssertions 6.12.x + Moq 4.20.x + coverlet。S1 早期推荐方案。
- **方案 C**：MSTest v3.x + Moq + coverlet。微软 VS 团队官方测试框架。
- **方案 D**：NUnit 4.4.x + 原生 `Assert.That` + Moq 4.20.x + coverlet。Azure SDK for .NET 同款测试栈。

配套维度：

- **测试框架**：xUnit / NUnit / MSTest。
- **断言库**：原生 / FluentAssertions / Shouldly / NFluent。
- **Mock 库**：Moq / NSubstitute / FakeItEasy。
- **架构测试**：沿用 ADR-018 的 NetArchTest.Rules，本 ADR 不再讨论。
- **覆盖率**：coverlet.collector，行业默认，本 ADR 不再讨论。

## 被否决方案与理由

**方案 A xUnit + 原生 Assert**：

- 跨仓库一致性是它最大的优点，但 dawning-agents 已被弃用，dawning 与 dawning-assistant 的 xUnit 选择并未通过 ADR 锁定，未来可能跟随本项目调整；一致性收益低于预期。
- xUnit 原生 `Assert.Equal(expected, actual)` 顺序敏感、读起来不直观，一旦多字段断言或集合断言就要堆叠多行 `Assert`，可读性弱于 NUnit `Assert.That(actual, Is.EqualTo(expected))`。
- xUnit 强制"每个测试一个新类实例"对单元测试有利，但对集成测试场景不友好（数据库连接、SQLite bootstrap、testcontainer 等共享初始化要走 `IClassFixture<T>` / `ICollectionFixture<T>`，写法相对啰嗦）。

**方案 B xUnit + FluentAssertions 6.12.x**：

- FluentAssertions v8（2024）转为商业 license，6.12.x 仍是 Apache 2.0，可短期使用；但项目方向是商业化，未来 6.x 安全更新断供风险高。
- 引入第三方断言库等于多一个长期依赖，与本项目"依赖最小化"取向冲突。
- 断言风格 `.Should().Be(...)` 流畅，但 NUnit 原生 `Assert.That(...)` 已能达到相近可读性，没有必要为这点甜度付商业风险。

**方案 C MSTest v3.x**：

- 微软官方测试框架，VS 内置体验最好，但 .NET 平台主流 OSS（runtime / aspnetcore / efcore）几乎不用 MSTest；社区文档、教程、示例覆盖度远低于 xUnit / NUnit。
- 数据驱动 / 参数化测试特性偏弱，与 NUnit 4 的 `[TestCase]` / `[TestCaseSource]` / `[Values]` 体系差距明显。

**Shouldly / NFluent**：

- 社区维护强度低于 FluentAssertions；选第三方 fluent 断言库本身就违反"依赖最小化"原则，没有理由用次选的来替代主选的。

**NSubstitute / FakeItEasy 替代 Moq**：

- API 风格偏好差异，没有客观优劣；Moq SponsorLink 风波后已回退该机制，4.20.x 之后版本无侵入性。
- Azure SDK 同时使用 Moq 与 NSubstitute，证明二者在企业级项目里都可用；本项目选 Moq 与 dawning 网关项目历史经验一致，无须额外学习成本。

## 决策

采用方案 D：NUnit 4.4.x + 原生 `Assert.That` + Moq 4.20.x + coverlet 6.x。

### 包版本（中央包管理）

`Directory.Packages.props` 中加入以下版本声明：

| 包 | 版本 | 说明 |
|---|---|---|
| `NUnit` | 4.4.0 | 测试框架；与 Azure SDK for .NET 同步主线 |
| `NUnit3TestAdapter` | 4.6.0 | 测试运行器，命名沿用 NUnit3 但兼容 NUnit 4 |
| `NUnit.Analyzers` | 4.4.0 | 测试代码静态分析（断言参数顺序、Async 误用等） |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | 测试宿主 |
| `Moq` | 4.20.72 | Mock 库；4.20.x 起已移除 SponsorLink |
| `coverlet.collector` | 6.0.4 | 覆盖率采集；与 Azure SDK 同款 |

### 测试项目命名与结构

继承 [ADR-017](engineering-skeleton-v0.md) 与 [ADR-018](backend-architecture-equinox-reference.md) 的项目划分：

```text
tests/
  Dawning.AgentOS.Domain.Core.Tests/         # NUnit
  Dawning.AgentOS.Domain.Tests/              # NUnit
  Dawning.AgentOS.Domain.Services.Tests/     # NUnit
  Dawning.AgentOS.Application.Tests/         # NUnit + Moq
  Dawning.AgentOS.Infra.Data.Tests/          # NUnit + Microsoft.Data.Sqlite (内存模式)
  Dawning.AgentOS.Services.Api.Tests/        # NUnit + Microsoft.AspNetCore.Mvc.Testing
  Dawning.AgentOS.Architecture.Tests/        # NUnit + NetArchTest.Rules (ADR-018)
```

### 命名约定

- 测试类：`{被测类型}Tests`，例如 `RuntimeCheckpointTests`、`HealthQueryHandlerTests`。
- 测试方法：`{方法名}_{场景}_{期望结果}`，例如 `Add_NewCheckpoint_RaisesDomainEvent`、`Handle_InvalidUserId_ReturnsValidationFailure`。
- 测试夹具：单个 `[TestFixture]` 默认省略（NUnit 4 起 `[TestFixture]` 在大多数场景可省略）。
- 数据驱动：优先 `[TestCase(...)]` 内联；多个数据源或共享数据集用 `[TestCaseSource(...)]`。
- 异步测试：方法签名 `public async Task ...`，断言用 `Assert.That(async () => await ...)` 或直接 `await` 后再断言。

### 断言风格

- 全部使用 NUnit 原生 `Assert.That(actual, Is.EqualTo(expected))`，禁止引入 FluentAssertions / Shouldly / NFluent / 自研 fluent 包装。
- 多字段断言使用 `Assert.Multiple(() => { ... })`，避免第一个失败掩盖后续失败。
- 异常断言使用 `Assert.That(() => action(), Throws.TypeOf<XxxException>())` 或 `Assert.ThrowsAsync<>`。
- Domain.Core 的 `Result<T>` 断言模式：先 `Assert.That(result.IsSuccess, Is.True)`，再 `Assert.That(result.Value, Is.EqualTo(...))`，失败分支断言 `result.Errors` 集合内容。

### Mock 风格

- 使用 Moq 4.20.x 的 strict mock 默认；setup 必须明确，避免隐式默认值掩盖未配置调用。
- Mock 实例命名 `_xxxMock` 或 `xxxMock`，作为字段在 `[SetUp]` 中重建，避免测试间状态泄露。
- 不为简单 stub 引入 NSubstitute；不为单一项目混用两种 mock 库。

### 测试生命周期

- 默认每个 `[Test]` 前重建被测对象与 mock：`[SetUp]` 中 `_sut = new Xxx(_dep1Mock.Object, ...)`。
- 集成测试（Infra.Data.Tests / Services.Api.Tests）的昂贵资源（SQLite 文件、WebApplicationFactory）使用 `[OneTimeSetUp]` 共享，但每个 `[Test]` 仍清理可变状态（清表、回滚事务）。
- 不依赖 `[Order]` 让测试按特定顺序执行；任意顺序均能通过。

### 并行执行

- 项目级别启用并行：`[assembly: Parallelizable(ParallelScope.Fixtures)]`，fixture 内串行避免共享 mock 字段冲突。
- Infra.Data.Tests 必须显式 `[NonParallelizable]` 或使用每用例独立的 SQLite 实例，避免文件锁冲突。

### 覆盖率门槛

- V0 暂不设最低覆盖率门槛，先建立测试编写习惯。
- Domain.Core / Domain / Domain.Services 项目预期高覆盖率（>90%），Application 中等（>70%），Infra.Data / Services.Api 由集成测试覆盖关键路径。
- 复议时机：测试套件达 1000 用例后评估是否引入硬门槛。

### 与 NetArchTest 的关系

- 架构测试由 [ADR-018](backend-architecture-equinox-reference.md) 锁定的 NetArchTest.Rules 提供；本 ADR 不重复定义架构测试断言。
- 架构测试项目自身使用 NUnit 编写测试方法，调用 NetArchTest API 做断言。

## 影响

**正向影响**：

- 测试栈一次性锁死，S1 起的所有测试项目按统一栈 scaffold，避免跨项目风格分裂。
- 不引入第三方断言库，规避 FluentAssertions v8 商业化、Shouldly 维护波动等长期风险，依赖最小化。
- NUnit 4 + `Assert.That` 流畅断言对 DDD / Result 模式的多字段失败、领域事件累积等场景表达力优于 xUnit `Assert.Equal`。
- 与 Azure SDK for .NET 同款栈（NUnit + Moq + coverlet），将来需要参考企业级 .NET 测试模式时社区资料对齐。
- `[TestCase]` / `[TestCaseSource]` / `[Values]` 数据驱动写法在 Domain 单测中比 `[Theory] + [InlineData]` 更紧凑。

**代价 / 风险**：

- 与 dawning / dawning-assistant 历史 xUnit 选择不一致；跨仓库阅读测试代码需要心智切换。两者后续是否对齐 NUnit 不在本 ADR 范围内。
- NUnit 默认实例共享语义弱于 xUnit "每用例新实例"；必须靠 `[SetUp]` 重建状态，违反约定会出现"用例顺序依赖"的隐藏 bug。命名约定 + Code Review + 可选的 NUnit.Analyzers 规则集是缓解手段。
- NUnit 生态中文文档少于 xUnit；新人 onboarding 主要靠官方 docs.nunit.org。
- 测试运行器名 `NUnit3TestAdapter` 与 NUnit 4 主版本号不一致，容易让人误以为版本错配；需在 README 或 CONTRIBUTING 中说明。

## 复议触发条件

- NUnit 4.x 与目标 .NET 主版本破坏二进制兼容，且社区维护停滞时。
- Moq 出现 SponsorLink 类侵入式商业化变动且无平滑替代时；切换 NSubstitute / FakeItEasy。
- 测试套件超过 10000 用例，NUnit / xUnit 性能差异成为构建瓶颈时。
- 本项目长期 contributor 规模显著扩大，新成员普遍来自 xUnit 生态且学习曲线明显拖慢交付时。
- 社区出现成熟非商业 fluent 断言库（API 表达力高于 NUnit 原生且许可清晰）时，复议是否引入。
- dawning / dawning-assistant 跨仓库统一选择又被改回 xUnit 时；本 ADR 标记 superseded 并给出迁移方案。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：产品契约与 MVP 技术形态。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：V0 测试项目划分。
- [ADR-018 后端架构参考 Equinox：DDD + MediatR + Result 模式](backend-architecture-equinox-reference.md)：测试项目结构与架构测试库（NetArchTest.Rules）。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：实现前先方案、后确认、再执行。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
