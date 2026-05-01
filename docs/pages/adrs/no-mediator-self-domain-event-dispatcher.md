---
title: ADR-022 去 MediatR：自研领域事件分发器与 AppService 立面
type: adr
subtype: architecture
canonical: true
summary: Application 层不再使用 MediatR；改为 API → IXxxAppService → 实现 的 OO 调用立面，Domain Event 派发由自研 IDomainEventDispatcher 承担，Domain.Core 同步移除 MediatR.Contracts 依赖。
tags: [agent]
sources: []
created: 2026-05-01
updated: 2026-05-01
verified_at: 2026-05-01
freshness: volatile
status: active
archived_reason: ""
supersedes: [pages/adrs/application-folder-layout.md]
related: [pages/adrs/backend-architecture-equinox-reference.md, pages/adrs/engineering-skeleton-v0.md, pages/adrs/architecture-test-assertion-strategy.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-01
adr_revisit_when: "出现真正的事件订阅生态（≥ 3 个 EventHandler 跨模块协作）；或自研 Dispatcher 在性能 / 安全审计中暴露问题；或 .NET 标准库提供等价的轻量事件派发抽象；或 Application 层出现需要 Pipeline Behavior 才能集中处理的横切关注点（事务 / 校验 / 日志）数量 ≥ 3。"
---

# ADR-022 去 MediatR：自研领域事件分发器与 AppService 立面

> Application 层不再使用 MediatR；改为 API → IXxxAppService → 实现 的 OO 调用立面，Domain Event 派发由自研 IDomainEventDispatcher 承担，Domain.Core 同步移除 MediatR.Contracts 依赖。

## 背景

[ADR-018](backend-architecture-equinox-reference.md) 在参考 EquinoxProject 的同时引入了 MediatR v12.x，让 Application 层走 `mediator.Send(query/command)` 的消息派发模式，并配套 `ICommand` / `IQuery` 标记接口与 Pipeline Behavior（Logging / Validation / Transaction / DomainEventDispatch）。[ADR-021](application-folder-layout.md) 在此基础上为 Application 项目设计了 `Abstractions/` + `Messaging/` + `<Feature>/` 的扁平垂直切片目录形态。

S3 切片（Application 层最小通电）按上述两份 ADR 落地后暴露了三个相互关联的问题：

1. **接口与实现的视觉混淆**。垂直切片 `Runtime/` 同时容纳 `GetRuntimeStatusQuery`（消息 / 契约）与 `GetRuntimeStatusQueryHandler`（实现），加上 `RuntimeStatus` DTO，三者并列。审阅时无法第一眼分辨"哪个是 API 调用入口、哪个是实现细节"。强行加 `Contracts/` + `Handlers/` 子文件夹（方案 A）或 UseCase 三件套（方案 C）只在视觉上分离，没有触及根因。

2. **MediatR 同时承担两个不同角色**。它既做 Use Case Mediator（`Send`：Controller 直发请求到 Handler），又做 Domain Event Bus（`Publish`：聚合事件分发给多个订阅者）。这两个角色在 Jimmy Bogard 自己后来的多次公开表态中已被认定为反模式合体：`IMediator` 出现在 Controller 是当初设计未预期的用法。把两个角色解耦后，"Use Case Mediator"在我们的桌面单进程场景里**没有实质收益**——它只是把直接 OO 调用包了一层消息查表派发。

3. **MediatR v13 的商业 license 风险长期悬而未决**。即便当下 v12.x 仍在 Apache 2.0，未来需要新特性时会被迫在"付费 / 迁移到 NetDevPack.SimpleMediator / 自研"三选一中选；这是个永远不会消失的依赖治理负担。

更具体地：

- Equinox 的实际做法是 Controller 调 `IXxxAppService` 接口，AppService 内部再翻译为 Command / 直接执行；Equinox 的 Mediator 只用于 Domain Event 的 Publish 与 DomainNotification 错误流。ADR-018 借鉴 Equinox 时只取了"用 MediatR"这个表层结论，没有对齐 Equinox 在"Controller 不直接 send"这个层面的真实做法。
- Application 项目对外暴露的真正契约应是 `IRuntimeAppService.GetStatusAsync()`——这是一个语义稳定的应用服务接口，而不是 `mediator.Send(GetRuntimeStatusQuery)`——后者把"消息"伪装成了"接口"。
- Domain Event 在 V0 阶段没有任何订阅者，未来加订阅者时 MediatR 的 Publish 与一个 30 行自研 Dispatcher 在功能、安全、性能上几乎等价；区别只在"自研需要一次性写 30 行 + 测试"和"MediatR 永远要为 license 焦虑"。

S3 已实现代码尚处 V0 通电阶段，重做成本可控；不在这个时点收敛，第二刀（聊天 + agent inbox + Memory Ledger）切片落地后再调整将波及多倍代码。

## 备选方案

- **X1 维持现状**：Application 继续用 MediatR，Use Case + Domain Event 都走 MediatR。
- **X2 去 Use Case Mediator，保留 Domain Event Mediator**：Controller 调 `IXxxAppService`；AppService 内部直接调 Domain；Domain Event 仍走 `mediator.Publish`。
- **X3 完全去 MediatR**：Use Case 走 `IXxxAppService` 立面；Domain Event 走自研 `IDomainEventDispatcher`；Domain.Core 不再依赖 MediatR.Contracts；Application 不再依赖 MediatR。
- **X4 保留 MediatR 双角色，AppService 包一层**：Controller 调 `IXxxAppService`；AppService 内部 `_mediator.Send(query)`；Domain Event 仍走 MediatR。

## 被否决方案与理由

**X1 维持现状**：

- 已暴露的接口 / 实现混淆与 MediatR 双角色滥用无缓解；ADR-021 的 `Abstractions/Messaging/<Feature>/` 形态读 review 时仍卡涩。
- MediatR v13 商业 license 风险长期悬而未决，迁移成本只会随业务规模扩大而上升。
- Pipeline Behavior 的"集中横切"红利在 V0 单查询场景下未兑现；事务 / 验证 / 日志全部为 hook 占位代码。

**X2 去 Use Case Mediator，保留 Domain Event Mediator**：

- 解决了 50% 问题（Use Case 立面清晰），但 Domain Event 仍依赖 MediatR；Domain.Core 仍引 `MediatR.Contracts`；架构测试仍需为 MediatR 命名空间留例外。
- License 焦虑、依赖治理负担没有消失；只是延后到"哪天 MediatR 报 CVE 或破坏性升级"。
- 决策不彻底：明明已经怀疑 MediatR 的合理性，却在 Domain Event 上保留它，本质是"50% 拥抱 + 50% 怀疑"，未来仍会回来重新决策。

**X4 保留 MediatR 双角色，AppService 包一层**：

- AppService 在 V0 单查询场景下退化为"forwarder"——直接把参数转发给 `_mediator.Send`，没有真活；属于无收益间接层。
- 真正的横切收益（事务 / 日志）依赖 Pipeline Behavior，但在我们这里等到第二刀才会真用上；为此保留 MediatR 是"为不存在的需求付当前成本"。
- 没有解决 License 焦虑。

## 决策

采用 X3：完全去除 MediatR / MediatR.Contracts 依赖，Application 层改 AppService 立面，Domain Event 派发由自研 `IDomainEventDispatcher` 承担。

### 1. Application 层文件夹形态

```text
src/Dawning.AgentOS.Application/
  Abstractions/                   # 端口（Application 声明，Infra.* 实现）
    IClock.cs
    IRuntimeStartTimeProvider.cs
    IDomainEventDispatcher.cs

  Interfaces/                     # AppService 立面契约（API 层调用入口）
    IRuntimeAppService.cs

  Services/                       # AppService 实现
    RuntimeAppService.cs

  <Feature>/                      # 仅放 DTO（与 feature 同名）
    Runtime/
      RuntimeStatus.cs

  DomainEventHandlers/            # V0 暂空；Application 内部对 Domain Event 的反应
                                  # （ApplicationService 跨聚合编排时的事件订阅者）
```

四类文件夹的判据：

| 文件夹 | 判据 | 反例 |
|---|---|---|
| `Abstractions/` | 接口在此声明，实现在 `Dawning.AgentOS.Infra.*` 项目 | 实现也在 Application → 不放此处 |
| `Interfaces/` | AppService 立面契约，由 `Services/` 中的实现实现，由 API 层调用 | 没有对应 `Services/` 实现 → 不属于 AppService 立面 |
| `Services/` | `Interfaces/` 中契约的实现，与契约一一对应 | 内部 helper / 私有类 → 不放此处 |
| `<Feature>/` | 业务名词，仅放 DTO（`RuntimeStatus` 这类 record） | 包含逻辑 / handler → 应进 `Services/` |
| `DomainEventHandlers/` | 实现 `IDomainEventHandler<T>` 的类，订阅 Domain Event | 与外部系统通信的 EventHandler → 应在 Infra 层 |

### 2. Domain Event 派发机制（自研）

#### 2.1 Domain.Core

- `IDomainEvent` 改为纯 marker，**不再继承 `MediatR.INotification`**。仍保留 `OccurredOn` 属性。
- 新增 `IDomainEventHandler<TEvent>`，声明在 Domain.Core，与 `IDomainEvent` 同栖：

  ```csharp
  public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
  {
      Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
  }
  ```

- Domain.Core 移除 `MediatR.Contracts` PackageReference，恢复"零外部依赖"。

#### 2.2 Application

- `IDomainEventDispatcher` 端口（`Application/Abstractions/`）：

  ```csharp
  public interface IDomainEventDispatcher
  {
      Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken);
      Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken);
  }
  ```

- Application 不再依赖 MediatR；项目移除 `MediatR` PackageReference。

#### 2.3 Infrastructure（V0 不实现）

- `DomainEventDispatcher` 实现是 Infra 层职责，在 S4 落地时实现；本 ADR 仅立端口与契约。
- 实现要点（写在 ADR 中作为 S4 落地指引）：
  - 注入 `IServiceProvider` 与 `ILogger<DomainEventDispatcher>`，构造单次反射后用 `ConcurrentDictionary` 缓存 `MakeGenericType` + `GetMethod` 结果。
  - 串行 `await` 多个 handler；遇异常使用 `ExceptionDispatchInfo.Capture(...).Throw()` 解包 `TargetInvocationException`，避免吞掉真异常的堆栈。
  - 默认在事务 commit **之前** 派发；如需"after commit"语义，由聚合或 AppService 显式控制时机，不由 Dispatcher 自带。
  - V0 不实现并行派发、不实现事件 outbox、不实现跨进程派发；这些落到未来 ADR 复议。

### 3. ADR-018 的局部废止

ADR-018 被本 ADR 在以下范围定向 supersede（ADR-018 整体仍 `accepted`，正文需补一段回链）：

- §Mediator 段全部废止（包括标记接口、`mediator.Send` 心智、Pipeline Behavior 的 Logging/Validation/Transaction/DomainEventDispatch 设计）。
- §Pipeline Behaviors 段废止；事务 / 日志 / 验证由 AppService 内部用 IUnitOfWork + ILogger + 显式调用承担。
- §架构测试 中"实现 ICommand/IQuery 的类型必须放 Application"、"Pipeline Behavior 必须以 Behavior 结尾并放 Infra.CrossCutting.Bus"、"Domain/Domain.Services 不引用 MediatR 主包（只允许 MediatR.Contracts）" 三条断言全部失效；本 ADR 改为更严的"全树不得依赖 MediatR / MediatR.Contracts"。
- §依赖方向 中 `Application` 引用 `MediatR` 抽象包、`Infra.CrossCutting.Bus` 引用 MediatR、`Domain.Core` 通过 `IDomainEvent : INotification` 依赖 `MediatR.Contracts` 三处全部废止。
- §项目划分 中 `Infra.CrossCutting.Bus` 项目的"MediatR 注册 + Pipeline Behaviors"职责变更为"DomainEventDispatcher 实现 + DI 注册"；项目本身保留与否由 S4 决定。

ADR-018 其它部分（DDD 分层、Result 模式、IUnitOfWork、按命名后缀自动注册、NetArchTest 架构测试基线、不接 Identity / 云后端）继续生效。

由于 ADR-018 仅被部分定向 supersede，本 ADR 的 `supersedes` 不列入 ADR-018，仅列入 ADR-021（后者整体被取代）。

### 4. ADR-021 的整体取代

ADR-021 的核心结论是 Application 内部走 `Abstractions/ + Messaging/ + <Feature>/` 的 marker + 垂直切片心智。本 ADR 取消 `Messaging/` 文件夹（marker 接口与消息派发不再存在），改为 `Abstractions/ + Interfaces/ + Services/ + <Feature> DTO/ + DomainEventHandlers/` 的 AppService 立面心智。ADR-021 的"Common/ 是反模式"洞察被本 ADR 继承——本 ADR 同样禁止 `Common/` / `Shared/` / `Helpers/` / `Utils/` 等无判据目录名。

ADR-021 状态转入 `superseded`，`canonical: false`；其历史价值（"Common/ 反模式"论证、"第二次原则"、"机器可校的目录边界"等）作为本 ADR 的思想前提，不重复展开。

### 5. 架构测试调整

废止：

- `DomainCore_DoesNotReferenceMainMediatRPackage`：MediatR 不再存在，断言无意义。
- `Application_AbstractionsFolder_OnlyContainsInterfaces`（NetArchTest，针对 ADR-021 的 marker 文件夹）：marker 文件夹已不存在。
- 已禁包列表中对 `MediatR` / `MediatR.Contracts` 的特例处理。

新增：

- `EntireSolution_DoesNotReferenceMediatR`：全树（Domain.Core / Domain / Domain.Services / Application / Infra.* / Api）都不得引用 `MediatR` 或 `MediatR.Contracts`。
- `Application_AbstractionsFolder_OnlyContainsInterfaces`（断言名复用，语义升级）：`Dawning.AgentOS.Application.Abstractions` 命名空间只能包含 interface（包括 IClock / IRuntimeStartTimeProvider / IDomainEventDispatcher）。
- `Application_InterfacesFolder_OnlyContainsInterfaces`：`Dawning.AgentOS.Application.Interfaces` 命名空间只能包含 interface（AppService 契约）。
- `Application_ServicesNamespace_AllImplementAtLeastOneApplicationInterface`：`Dawning.AgentOS.Application.Services` 命名空间下的 concrete class 必须实现 `Dawning.AgentOS.Application.Interfaces` 命名空间中的至少一个接口。
- `DomainCore_DoesNotReferenceAnyExternalPackages`：Domain.Core 程序集引用列表只允许 BCL，不得包含任何第三方包。

ADR-018 中既有的"层级方向"断言（Application 不依赖 Infra.* / Services.Api / Dapper / SQLite / AspNetCore）继续有效。

### 6. AppService DI 注册

V0 阶段在 `Infra.CrossCutting.IoC` 集中显式注册 `services.AddScoped<IRuntimeAppService, RuntimeAppService>()`。按命名后缀自动注册策略（ADR-018 §DI）继续生效，AppService 自然落入 `*Service` 命名后缀的扫描范围。

### 7. 不在本 ADR 范围

- 自研 `DomainEventDispatcher` 的 Infra 实现：归 S4 实施。
- AppService 事务边界策略（手动 `using var tx = uow.BeginTransaction()` / 装饰器 / 其它）：等首个写聚合的 AppService 出现时再开 ADR。
- AppService 自动注册策略升级：等首次"加业务忘了注册"痛点出现时再决策。
- Pipeline Behavior 的等价替代品（如装饰器或 OpenTelemetry）：本 ADR 不引入。

## 影响

**正向影响**：

- API 层调用契约从"消息伪装的接口"回到真正的 OO 接口（`IRuntimeAppService.GetStatusAsync()`），可读性、可 mock 性、可 review 性同时提升。
- Application 项目内部 Interface（`Interfaces/`）与 Implementation（`Services/`）按文件夹边界清晰分离，符合标准 .NET DDD 习惯。
- 全树移除 MediatR / MediatR.Contracts 依赖，License 焦虑彻底消除；NuGet 依赖减少 2 个。
- Domain.Core 恢复"零外部依赖"，符合 Clean Architecture 原教旨。
- 架构测试从"为 MediatR 命名空间留例外"变为"整树禁用 MediatR"，断言更简单、更严格。
- 对齐 EquinoxProject v1.10 的真实做法（Controller 调 `IXxxAppService`），ADR-018 的 Equinox 借鉴更彻底。

**代价 / 风险**：

- S3 已实现代码（marker 接口、Query 类、QueryHandler、对应 8 个测试）需推翻重做；commit 历史保留 ADR-021 + S3 的探索过程作为决策演进证据。
- 失去 MediatR Pipeline Behavior 的"集中横切"基础设施；当出现需要事务 / 验证 / 日志一处搞定的真实诉求时，需另行决策装饰器或其它方案。
- 自研 `DomainEventDispatcher` 需要正确处理：反射调用的 `TargetInvocationException` 解包、handler 异常的透传、handler 多态派发（如订阅 `IDomainEvent` 基类）。出 bug 概率虽低但非零；通过 ≥ 4 个针对性单元测试覆盖 happy path / 异常透传 / 多 handler 串行 / cancellation。
- 调用从 `_mediator.Send(query)` 改为 `_appService.GetStatusAsync()` 后，"通过 Pipeline 拦截全部请求"的可观测性钩点消失；OpenTelemetry / Logging 需在 AppService 实现内显式埋点，或通过装饰器引入。
- 第二刀（聊天 + agent inbox + Memory Ledger）切片中如果出现"3+ 个用例需要相同事务模板"，会触发"装饰器 vs 重新引入 mediator"的复议。

## 复议触发条件

- 出现真正的 Domain Event 订阅生态（≥ 3 个 EventHandler 跨模块协作），自研 Dispatcher 的人工维护成本接近一个三方库时。
- 自研 Dispatcher 在性能基准（≥ 10k events/sec 持续派发）或安全审计（异常路径、DI 生命周期）中暴露问题时。
- .NET 标准库或一线 BCL 包（如 `Microsoft.Extensions.*`）提供等价的轻量事件派发抽象时。
- Application 层出现 ≥ 3 个需要 Pipeline Behavior 才能集中处理的横切关注点（事务边界、统一校验、统一日志、统一审计），且装饰器方案被论证为不够时。
- ADR-018 因其它原因被整体 supersede 时，本 ADR 同步复议是否仍适用新的项目划分。

## 相关页面

- [ADR-018 后端架构参考 Equinox：DDD + MediatR + Result 模式](backend-architecture-equinox-reference.md)：上游决策；本 ADR 定向 supersede 其 §Mediator / §Pipeline Behaviors / §架构测试 / §项目划分中与 MediatR 相关的部分。
- [ADR-021 Application 项目目录约定：Abstractions / Messaging / 垂直切片](application-folder-layout.md)：被本 ADR 整体 supersede。
- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：V0 通电边界，本 ADR 不影响其项目划分与依赖方向。
- [ADR-020 架构测试断言策略](architecture-test-assertion-strategy.md)：本 ADR 新增的架构断言遵循其类型级断言到具体类型名的纪律。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 是 X3 实施前的方案确认产物。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
