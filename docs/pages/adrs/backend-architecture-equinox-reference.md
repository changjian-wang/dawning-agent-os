---
title: ADR-018 后端架构参考 Equinox：DDD + MediatR + Result 模式
type: adr
subtype: architecture
canonical: true
summary: 后端骨架在 ADR-017 工程边界之上参考 EquinoxProject v1.10 的 DDD 分层与 CQRS 心智，但保留 Dapper、引入 MediatR v12 与 Result 模式，并定义 IUnitOfWork、自动注册与架构测试约束。
tags: [agent]
sources: []
created: 2026-04-29
updated: 2026-05-01
verified_at: 2026-04-29
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/repository-shape-product-monorepo-with-wiki.md, pages/adrs/mvp-desktop-stack-electron-aspnetcore.md, pages/adrs/engineering-skeleton-v0.md, pages/adrs/testing-stack-nunit-v0.md, pages/adrs/architecture-test-assertion-strategy.md, pages/adrs/no-mediator-self-domain-event-dispatcher.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-29
adr_revisit_when: "出现需要分布式事务、第一个独立 read model、IUnitOfWork 接口属性超过 50 项、MediatR v12.x 与目标 .NET 主版本不再兼容、Equinox 蓝本发生重大版本调整、自动注册按命名后缀冲突阻碍业务扩展、Result 模式无法承载新的错误维度，或 Identity 网关 / 云后端 / 多设备同步要进入项目时。"
---

# ADR-018 后端架构参考 Equinox：DDD + MediatR + Result 模式

> 后端骨架在 ADR-017 工程边界之上参考 EquinoxProject v1.10 的 DDD 分层与 CQRS 心智，但保留 Dapper、引入 MediatR v12 与 Result 模式，并定义 IUnitOfWork、自动注册与架构测试约束。

## 背景

[ADR-016](mvp-desktop-stack-electron-aspnetcore.md) 已确定 MVP 桌面技术栈为 Electron + ASP.NET Core 本地后端，并要求后端遵循 DDD 分层骨架；[ADR-017](engineering-skeleton-v0.md) 已确定 V0 工程通电的目录边界与最小验证范围。但是这两份决策没有回答几个具体的工程问题：层之间用什么样的消息路径、命令 / 查询如何区分、领域事件怎么 raise / dispatch、业务错误如何在不抛异常的前提下累积返回、IUnitOfWork 应该胖还是瘦、DI 是显式注册还是按约定扫描、架构边界怎么自动验证。

如果让每个新业务切片各自决定上述细节，第一刀骨架会快速被多种风格污染：有的 handler 抛异常做错误流、有的返回 nullable、有的直接 throw FluentValidation；有的 service 自己 begin / commit 事务、有的依赖 mediator pipeline；有的仓储手动注册到 DI、有的按后缀自动扫描；架构边界不约束就会被业务方便压垮。这些风格分裂会在第二刀（聊天 + agent inbox + Memory Ledger）开始迅速恶化。

[EquinoxProject](https://github.com/EduardoPires/EquinoxProject) 是一个长期维护、社区认可度较高的 .NET DDD 工程蓝本，最新 v1.10（2025-04-08）已迁移到 .NET 9，并把 MediatR 替换为 NetDevPack.SimpleMediator、把 AutoMapper 替换为自定义 mapping、引入 NetArchTest.Rules 做架构测试、加入 SQLite 自动迁移。它的分层（Domain.Core / Domain / Application / Infra.Data / Infra.CrossCutting.Bus / Infra.CrossCutting.IoC / Services.Api）、命令与查询心智、领域事件机制、UnitOfWork + Repository、DomainNotification 错误流和 NetArchTest 架构测试形成了一组成熟的工程约束，可作为本项目的参照系。

但 Equinox v1.10 的部分选择与本项目其他 ADR 冲突或超前：

- 它使用 EF Core 9 + Migrations，与 [ADR-016](mvp-desktop-stack-electron-aspnetcore.md) 中已确定的 Dapper + Dawning.ORM.Dapper 数据访问选择直接冲突。
- 它默认接入 ASP.NET Identity + JWT + OpenIddict，本项目 V0 是桌面 App + startup token，远期才考虑接入网关。
- 它把 Mediator 包装为 NetDevPack.SimpleMediator，是为了规避 MediatR v13 的商业 license；本项目当前选择直接使用 MediatR v12.x（Apache 2.0），未来需要时再讨论 v13 商业付费或迁回 SimpleMediator。
- 它使用 DomainNotification 通过 mediator.Publish 收集业务校验错误，依赖 NotificationHandler 在请求末端汇总；本项目选择更直接的 `Result` / `Result<T>` 模式，避免 mediator notification scope 与请求生命周期耦合。

因此，需要一份独立 ADR 把"参考 Equinox 但定向偏离"这件事记录清楚，作为后续后端代码风格、目录命名、DI 策略、架构测试断言的统一来源，并接管 [ADR-017](engineering-skeleton-v0.md) 中部分由 ADR-018 决定的工程细节。

## 备选方案

- **方案 A**：自己从零设计 DDD 工程骨架，不参考成熟蓝本。
- **方案 B**：完整照搬 EquinoxProject v1.10，包括 EF Core、ASP.NET Identity、DomainNotification、NetDevPack.SimpleMediator。
- **方案 C**：参考 EquinoxProject 的分层 / 命名 / 心智，定向偏离 ORM、Mediator 包名、错误流模型与 Identity 接入。

配套维度：

- **Mediator 选型**：MediatR v12.x（Apache 2.0）/ MediatR v13+（商业 license）/ NetDevPack.SimpleMediator（OSS 替代）。
- **业务错误模型**：抛异常 + 全局过滤 / DomainNotification + mediator.Publish / `Result` 与 `Result<T>` 返回值。
- **IUnitOfWork 形态**：胖入口（每个聚合一属性） / 泛型方法 `Repository<T>()` / 索引器与扩展方法糖 / 接口分组。
- **DI 策略**：每个层提供显式 `Add*` 扩展 / Infra.CrossCutting.IoC 集中注册 + 按命名后缀自动扫描业务 Service / Repository。
- **架构测试**：手写反射 / NetArchTest.Rules / 不做架构测试。

## 被否决方案与理由

**方案 A 自己从零设计**：

- 第二刀业务切片陡峭增加，自研 DDD 骨架会拉长决策时间，且容易反复返工。
- Equinox 已经踩过 .NET DDD 工程蓝本里的常见坑（Mediator 商业化、AutoMapper 复杂度、架构测试库选择），从零开始等于重新踩。
- 不利于将来接入 dawning 网关项目（已经使用 dawning 风格 IUnitOfWork + 按后缀自动注册），心智不一致迁移成本高。

**方案 B 完整照搬 Equinox**：

- EF Core 与 [ADR-016](mvp-desktop-stack-electron-aspnetcore.md) 中已确定的 Dapper + Dawning.ORM.Dapper 数据访问选择冲突，会推翻已落地决策。
- ASP.NET Identity / OpenIddict 在 V0 桌面 App 阶段过度，与 [ADR-017](engineering-skeleton-v0.md) 中"V0 不接 Identity / 云后端 / 账号系统"约束直接冲突。
- DomainNotification 模式依赖 mediator notification handler 在请求末端汇总错误，需要额外 scope 级 collector 与请求生命周期协调；与本项目偏好的"调用流直观、返回值即结果"风格不一致。
- NetDevPack.SimpleMediator 是为规避 MediatR v13 商业化而生的等价 OSS 替代品；本项目当前选择直接锁定 MediatR v12.x（Apache 2.0），未来再评估迁移。

**MediatR v13+（商业 license）**：

- 跟最新主线，但商业 license 与本项目"远期可能商业化、需控制依赖成本"路线冲突。
- v12.x 的 API 与 v13+ 几乎一致，未来真要切回原版主线时迁移成本极低。

**NetDevPack.SimpleMediator**：

- 它是为规避 MediatR v13 而生，本身没有问题；但本项目当前 v12.x 仍在 Apache 2.0 下，不需要替代品。
- 引入会让代码 mediator API 与 .NET DDD 主流社区文档不一致，新人 onboarding 成本上升。

**业务错误：抛异常 + 全局过滤**：

- 调用流不直观，校验失败要走 throw → middleware 捕获 → 转 ProblemDetails，跨 3 层。
- 多字段错误一次性返回不自然，异常承载多字段错误结构会走样。
- 与 Result 模式相比丢失"业务失败可枚举"的语义。

**业务错误：DomainNotification + mediator.Publish**：

- 是 Equinox 原版做法，依赖 mediator notification handler 在 scope 末端汇总错误。
- 引入额外的 NotificationHandler、scope 级 collector、Controller 末端检查约定，比 Result 模式重。
- 一旦 Pipeline Behavior 顺序错配，notification 与最终响应会错位。

**IUnitOfWork：泛型 `Repository<T>()` 入口**：

- 接口永不膨胀，但调用从 `_uow.RuntimeCheckpoints.AddAsync(...)` 变成 `_uow.Repository<IRuntimeCheckpointRepository>().AddAsync(...)`，调用语法显著变重。
- 与 dawning 网关项目 `_uow.User`、`_uow.Role` 心智不一致，跨项目阅读体验下降。

**IUnitOfWork：接口分组 + 嵌套**：

- 调用从 `_uow.X.Add(...)` 变成 `_uow.GroupA.X.Add(...)`，多一层导航，前期看不出收益。
- 子接口仍会随聚合膨胀，没有解决根本问题。

**DI 策略：每个层提供显式 `Add*` 扩展**：

- 显式可控，但每个新 Service / Repository 都要在对应 `Add*` 扩展里加一行注册，机械工作量随业务模块线性增长。
- dawning 网关项目已经验证按命名后缀 + 命名空间过滤的自动注册可以稳定使用，没必要让本项目走更繁琐的路径。

**架构测试：手写反射**：

- V0 探索阶段尝试过手写 csproj XML 解析做依赖方向断言，能用但表达力弱、加新断言代码量大。
- NetArchTest.Rules 是 Equinox v1.10 已采用的标准库，断言 DSL 表达力高，覆盖范围更广。

## 决策

> **部分被 [ADR-022 去 MediatR：自研领域事件分发器与 AppService 立面](no-mediator-self-domain-event-dispatcher.md) 定向 supersede（2026-05-01）。** ADR-022 废止了本 ADR 中与 MediatR 相关的所有内容：§Mediator、§Pipeline Behaviors、架构测试中针对 MediatR 的条款、以及§项目划分 / §依赖方向中与 MediatR / MediatR.Contracts 相关的条目。本 ADR 的其它部分（DDD 分层、Result 模式、IUnitOfWork 胖入口、按命名后缀自动注册、NetArchTest 架构测试基线、不接 Identity / 云后端）继续生效。
>
> **Api 部分被 [ADR-023 Api 入口立面：AppService 接入与 V0 端点形态](api-entry-facade-and-v0-endpoints.md) 定向 supersede（2026-05-01）。** ADR-023 废止了本 ADR 中与 Api 项目相关的命名漂移与形态空白：① 项目命名以 `Dawning.AgentOS.Api` 为准（不是本 ADR 文中沿用 Equinox 的 `Services.Api`），② Api 端点风格固定为 Minimal API endpoint groups（非 Controller），③ 响应壳明确为"成功 → DTO，失败 → ProblemDetails"，不引入 `ApiResult<T>` 包装，④ `Result<T>.ToHttpResult()` 扩展归属 Api 项目的 `Results/` 目录而不是 Application 层，⑤ V0 不引入 Swagger / OpenAPI、Asp.Versioning、`MapHealthChecks` / `IHealthCheck`，⑥ AppService 自动注册由 Application 层 `AddApplication()` 扩展承担。本 ADR 中的 Result 模式、ProblemDetails 状态码语义（200 / 400 / 422 / 404）继续作为依据，由 ADR-023 §4 的映射规则具体化。

采用方案 C：参考 Equinox v1.10 的分层 / 命名 / 心智，定向偏离 ORM、Mediator 包名、错误流模型与 Identity 接入。

### 项目划分

```text
src/
  Dawning.AgentOS.Domain.Core/                 # 纯基类：Entity、AggregateRoot、IDomainEvent、Result、DomainError
  Dawning.AgentOS.Domain/                      # 业务聚合 + 值对象 + 领域事件 + 仓储接口 + IUnitOfWork
  Dawning.AgentOS.Domain.Services/             # 跨聚合 / 跨值对象的纯领域服务
  Dawning.AgentOS.Application/                 # Commands / Queries / Handlers / Behaviors / DTO / Application port
  Dawning.AgentOS.Infra.Data/                  # DbContext + Dapper Repository + UnitOfWork + Schema bootstrap
  Dawning.AgentOS.Infra.CrossCutting.Bus/      # MediatR 注册 + Pipeline Behaviors + DomainEventDispatcher
  Dawning.AgentOS.Infra.CrossCutting.Security/ # Startup token / 本地通信安全凭据
  Dawning.AgentOS.Infra.CrossCutting.IoC/      # NativeInjectorBootStrapper：集中注册 + 按后缀自动扫描
  Dawning.AgentOS.Services.Api/                # Endpoints + Middleware + ProblemDetails 映射

tests/
  Dawning.AgentOS.Domain.Core.Tests/
  Dawning.AgentOS.Domain.Tests/
  Dawning.AgentOS.Domain.Services.Tests/
  Dawning.AgentOS.Application.Tests/
  Dawning.AgentOS.Infra.Data.Tests/
  Dawning.AgentOS.Services.Api.Tests/
  Dawning.AgentOS.Architecture.Tests/          # NetArchTest.Rules
```

### 依赖方向

- `Domain.Core` 零依赖，不引用任何项目或基础设施包。
- `Domain` 引用 `Domain.Core`。
- `Domain.Services` 引用 `Domain` 与 `Domain.Core`。
- `Application` 引用 `Domain.Services`、`Domain` 与 `Domain.Core`，并引用 `MediatR` 抽象包。
- `Infra.Data` 引用 `Application`、`Domain` 与 `Domain.Core`，并引用 `Dapper`、`Microsoft.Data.Sqlite`、`Dawning.ORM.Dapper`。
- `Infra.CrossCutting.Bus` 引用 `Application` 与 `Domain.Core`，并引用 `MediatR`。
- `Infra.CrossCutting.Security` 引用 `Application`，提供 startup token / 本地通信安全凭据实现；不依赖 `Infra.Data` / `Infra.CrossCutting.Bus`。
- `Infra.CrossCutting.IoC` 引用其余所有 src 项目，作为唯一集中注册位置。
- `Services.Api` 只引用 `Infra.CrossCutting.IoC`，不直接依赖其他层。
- `Domain` / `Domain.Services` 不依赖 MediatR 主包；只有 `IDomainEvent : INotification` 这一处依赖 `MediatR.Contracts` 抽象包。
- `Application` / `Domain` / `Domain.Services` 不引用 `Dapper`、`Microsoft.Data.Sqlite`、`Microsoft.AspNetCore.*` 等基础设施 / 框架包。

### ORM 与数据访问

- 数据访问采用 `Dapper` + `Dawning.ORM.Dapper` + `Microsoft.Data.Sqlite`，与 [ADR-016](mvp-desktop-stack-electron-aspnetcore.md) 一致；不引入 EF Core，偏离 Equinox v1.10。
- `DbContext` 在 `Infra.Data` 内部持有 `IDbConnection` 与 `IDbTransaction`，提供 `BeginTransaction / Commit / Rollback / Dispose`；该类型不暴露给 `Application` 层。
- 仓储构造函数注入 `DbContext`，所有仓储共享同一连接与事务实例。
- `UnitOfWork` 是 `DbContext` 的转发器，Application 只看见 `IUnitOfWork`，不看见 `IDbConnection`，守 [ADR-017](engineering-skeleton-v0.md) 中"UnitOfWork 不向 Application 暴露裸 IDbConnection"红线。

### Mediator

- 采用 `MediatR` v12.x（Apache 2.0），不使用 NetDevPack.SimpleMediator 或 MediatR v13+。
- 在 `Application` 项目 `Common/Messaging/` 下定义标记接口 `ICommand`、`ICommand<TResponse>`、`IQuery<TResponse>`，均继承 `IRequest<TResponse>`，Pipeline Behavior 通过类型约束区分 Command / Query。
- 即便 V0 只有 Query（health / runtime status），仍走 `mediator.Send(...)`，避免后续从 service 调用切到 mediator 的二次重构。
- handler 注册采用 MediatR 自带的程序集扫描 `RegisterServicesFromAssembly`，目标是 `Dawning.AgentOS.Application` 程序集。

### Pipeline Behaviors

注册顺序即执行顺序，外层在前：

1. `LoggingBehavior` — 请求开始 / 结束 / 耗时 / 失败日志。
2. `ValidationBehavior` — V0 留空 hook，引入 FluentValidation 时启用。
3. `TransactionBehavior` — 仅对实现 `ICommand` 标记接口的请求 `BeginTransaction` / `Commit` / `Rollback`；Query 跳过事务。
4. `DomainEventDispatchBehavior` — V0 留 hook：在 next() 后扫聚合 `DomainEvents` 并 `mediator.Publish`，但不订阅任何事件；待第二刀业务切片需要副作用时再加订阅者。

Pipeline 顺序由 `Infra.CrossCutting.IoC` 集中注册保证；架构测试断言 Behavior 命名后缀 `*Behavior` 与所在项目，避免静默重排。

### 业务错误：Result 模式

- `Domain.Core` 提供 `Result` / `Result<T>` 与 `DomainError(string Code, string Message, string? Field)`。
- handler 返回 `Result<TResponse>`；不抛业务异常，只抛聚合内不变量违反这种"bug 级"异常（构造非法、id 为 Guid.Empty 等）。
- 多字段错误使用 `Result.Failure(err1, err2, ...)` 累积返回。
- `Services.Api` 末端通过 `Result<T>.ToHttpResult()` 扩展统一映射：成功 → 200；含 Field 的错误 → 400 ProblemDetails；业务规则错误 → 422；不存在错误 → 404。
- 不采用 Equinox 原版 DomainNotification + mediator.Publish 错误流。

### Domain Events

- `Domain.Core/IDomainEvent : MediatR.INotification`，仅依赖 `MediatR.Contracts` 抽象包。
- `AggregateRoot` 提供 `Raise(IDomainEvent e)` / `IReadOnlyList<IDomainEvent> DomainEvents` / `ClearDomainEvents()`。
- V0 在 `DomainEventDispatchBehavior` 中保留 dispatch hook，但不订阅任何 EventHandler；当业务真正需要副作用（写 audit log / 推 notification / 更新 read model）时再加订阅。
- V0 不实现 EventStore / Event Sourcing，偏离 Equinox v1.10 的 EventStoreSQLContext。

### IUnitOfWork：dawning 风格胖入口

接口在 `Domain` 项目，按 dawning 网关项目同款心智设计：

```csharp
public interface IUnitOfWork
{
    // 业务仓储入口；每加一个聚合追加一行属性
    IRuntimeCheckpointRepository RuntimeCheckpoints { get; }

    // 事务方法；不暴露 IDbConnection / IDbTransaction
    void BeginTransaction();
    void Commit();
    void Rollback();
    Task CommitAsync(CancellationToken ct = default);
}
```

- 调用语法：`_uow.RuntimeCheckpoints.AddAsync(c, ct)`，与 dawning 网关项目一致。
- 接口随业务线性膨胀是已知代价，且 [ADR-017](engineering-skeleton-v0.md) 与本 ADR 都不视其为风险：Application 层只关心调用语法，没有人会 review 接口本身。
- 接口超过 50 项时复议是否切换到泛型 `Repository<T>()` 或接口分组（写入复议触发条件）。

### DI：自动注册按命名后缀

`Infra.CrossCutting.IoC/NativeInjectorBootStrapper.cs` 提供唯一 `AddDawningAgentOS(IConfiguration)` 扩展，集中注册：

- `DbContext` / `IUnitOfWork` 显式注册（事务边界关键）。
- MediatR via `services.AddMediatR(c => c.RegisterServicesFromAssembly(...))`。
- Pipeline Behaviors 显式注册（顺序敏感）。
- 业务 `*Service` / `*Repository` 类型按命名后缀扫描程序集，约束命名空间为 `Dawning.AgentOS.*`，注册为 Scoped。
- Application port（`IClock`、`IUserDataPathProvider` 等）按命名后缀不能命中，单独显式注册。

该决策**取代** [ADR-017](engineering-skeleton-v0.md) 中"V0 不使用按命名后缀自动扫描注册所有 Service / Repository 作为默认机制"约束。

### 架构测试

- 单独 `Dawning.AgentOS.Architecture.Tests` 项目，使用 `NetArchTest.Rules` v2.x。
- 必备断言：
  - `Domain.Core` 不依赖任何 src 项目。
  - `Domain` 仅依赖 `Domain.Core`。
  - `Domain.Services` 仅依赖 `Domain` / `Domain.Core`。
  - `Application` 不依赖 `Infra.*` / `Services.Api` / `Dapper` / `Microsoft.Data.Sqlite` / `Microsoft.AspNetCore.*`。
  - `Infra.Data` / `Infra.CrossCutting.*` 不依赖 `Services.Api`。
  - `Infra.CrossCutting.Security` 不依赖 `Infra.Data` 与 `Infra.CrossCutting.Bus`。
  - `Services.Api` 仅依赖 `Infra.CrossCutting.IoC`。
  - `Domain` / `Domain.Services` 不引用 `MediatR` 主包（只允许 `MediatR.Contracts`）。
  - 实现 `ICommand` / `IQuery` 的类型必须放在 `Application` 项目。
  - Pipeline Behavior 实现类必须以 `Behavior` 结尾，并放在 `Infra.CrossCutting.Bus`。
  - Startup token 相关类型（`IStartupTokenProvider` / `IStartupTokenValidator` / 实现 / Defaults）必须放在 `Infra.CrossCutting.Security`。

### Identity / 网关 / 云接入

- V0 不接入 ASP.NET Identity / OpenIddict / JWT。
- Startup token 是 V0 桌面进程间本地通信的临时凭据，归 `Infra.CrossCutting.Security` 项目管理；它**不是**领域权限模型，也不是面向远端用户的身份认证。Domain / Domain.Services 中的 `Permissions/ActionLevel` 与 startup token 互不相通。
- 网关与云后端接入作为复议触发条件之一，届时另起 ADR 决定如何在保持 startup token 的本地通信前提下增量接入身份层。

## 影响

**正向影响**：

- 新业务切片有统一的写法：handler 返回 `Result<T>`、聚合 `Raise` 领域事件、`uow.X.AddAsync(...)` 操作仓储、TransactionBehavior 自动包事务。新人 / 未来 coding agent onboarding 成本下降。
- Mediator + Pipeline 提前定型，第二刀（聊天 + agent inbox + Memory Ledger）开始无需重构 endpoint / service 调用风格。
- IUnitOfWork 与自动注册风格与 dawning 网关项目一致；远期接入网关时跨项目阅读成本低。
- 架构测试使用 NetArchTest.Rules，断言能力强；依赖方向与命名约束机器可校。
- 与 Equinox 蓝本对齐使本项目可在社区文档 / 教程 / 示例中找到大量参考；偏离点（Dapper / 错误流 / Identity）在本 ADR 中显式说明，未来回看不会迷失。

**代价 / 风险**：

- IUnitOfWork 接口随业务线性膨胀，每加一个聚合改 Domain 接口 + Infra 实现。已知代价，超过 50 项复议。
- 自动注册按命名后缀对**严格遵守 `*Service` / `*Repository` 命名约定**有强依赖，违反约定会静默漏注册；架构测试需配合断言新增 `*Service` / `*Repository` 类型必须实现至少一个 `Dawning.AgentOS.*` 接口。
- MediatR v12.x 不再获得新版本特性；如未来 .NET 主版本破坏二进制兼容，需要切换到 v13+（商业 license）或 NetDevPack.SimpleMediator。
- Pipeline Behavior 顺序敏感，注册顺序错误会导致事务边界、错误处理、日志位置错乱；通过架构测试 + 集中注册位置降低风险。
- Result 模式要求 handler 签名统一返回 `Result<T>`；Application port 中老代码若已用异常风格需要适配。

## 复议触发条件

- 出现需要分布式事务（多 SQLite 文件 / 跨进程协调）时。
- 出现第一个独立 read model（CQRS 真正分离）时。
- IUnitOfWork 接口属性超过 50 项，调用语法仍想保留但接口体积成为代码维护负担时。
- MediatR v12.x 与目标 .NET 主版本不再兼容，或社区生态停止维护时。
- Equinox 蓝本发生重大版本调整（v2.x），影响参考有效性时。
- 自动注册按命名后缀冲突阻碍业务扩展（例如出现非 `*Service` / `*Repository` 后缀但仍需自动注册的场景）时。
- Result 模式无法承载新的错误维度（OAuth challenge / 多步交互 flow）时。
- Identity 网关 / 云后端 / 多设备同步要进入项目时。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：产品契约与 MVP 技术形态。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-015 仓库形态：产品 monorepo + 内置 LLM-Wiki](repository-shape-product-monorepo-with-wiki.md)：产品代码承载仓库。
- [ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端](mvp-desktop-stack-electron-aspnetcore.md)：桌面与后端选型，含 Dapper + Dawning.ORM.Dapper。
- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：V0 通电边界与最小验证范围。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：实现前先方案、后确认、再执行。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
