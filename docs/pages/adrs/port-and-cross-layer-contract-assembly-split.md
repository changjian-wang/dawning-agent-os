---
title: ADR-037 端口与跨层契约程序集划分：Domain Event 进 Domain.Core，其余技术契约进 Dawning.AgentOS.Abstractions
type: adr
subtype: architecture
canonical: true
summary: 把 Application 项目内 Abstractions/（端口接口）与 Llm/（LLM 能力契约 DTO）按性质迁出：IDomainEventDispatcher 因引用 IDomainEvent 而下沉到 Domain.Core 与领域事件同栖；其余 6 个技术端口（IClock / IRuntimeStartTimeProvider / IAppDataPathProvider / IDbConnectionFactory / ISchemaInitializer / ILlmProvider）与 7 个 LLM DTO 迁入新建的 Dawning.AgentOS.Abstractions 程序集，命名空间相应改为 Dawning.AgentOS.Abstractions.*；Abstractions 仅 ProjectReference 到 Domain.Core 以取用 shared-kernel 原语 Result<T> / DomainError，禁止依赖 Domain / Domain.Services / Application / Infrastructure / Api；Application 项目仅保留用例编排，Infrastructure 改为引用 Abstractions 而不再依赖 Application 主项目。
tags: [agent]
sources: []
created: 2026-05-07
updated: 2026-05-07
verified_at: 2026-05-07
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/no-mediator-self-domain-event-dispatcher.md, pages/adrs/engineering-skeleton-v0.md, pages/adrs/persistence-repository-style-dawning-orm-dapper.md, pages/adrs/architecture-test-assertion-strategy.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-07
adr_revisit_when: "Abstractions 中出现一个无法用「是否引用 Domain.Core 类型」机器判定归属的新端口（例如 IPasswordHasher / IEncryptor / IFileWatcher 等模糊技术契约）；或 Domain.Core 因新增非 IDomainEvent 相关的端口而扩张；或 Abstractions 项目下的 LLM DTO 因引入领域概念字段而失去中立性；或多入口（WeChat OA / Mobile / Web）落地时发现 Abstractions/Application 的边界需要重划；或本仓库决定将 Abstractions 升级为可独立 NuGet 包对外发布。"
---

# ADR-037 端口与跨层契约程序集划分：Domain Event 进 Domain.Core，其余技术契约进 Dawning.AgentOS.Abstractions

> 把 Application 项目内 Abstractions/（端口接口）与 Llm/（LLM 能力契约 DTO）按性质迁出：IDomainEventDispatcher 因引用 IDomainEvent 而下沉到 Domain.Core 与领域事件同栖；其余 6 个纯技术端口（IClock / IRuntimeStartTimeProvider / IAppDataPathProvider / IDbConnectionFactory / ISchemaInitializer / ILlmProvider）与 7 个 LLM DTO 迁入新建的 Dawning.AgentOS.Abstractions 中立程序集，命名空间相应改为 Dawning.AgentOS.Abstractions.*；Application 项目仅保留用例编排，Infrastructure 改为引用 Abstractions 而不再依赖 Application 主项目。

## 背景

[ADR-022](no-mediator-self-domain-event-dispatcher.md) §决策 1 把 Application 项目内部目录定型为 `Abstractions/ + Interfaces/ + Services/ + <Feature> DTO/ + DomainEventHandlers/`。`Abstractions/` 是 Hexagonal 架构里的端口家：声明在此、由 `Dawning.AgentOS.Infra.*` 项目实现。

ADR-022 只回答了「端口往哪个文件夹放」，没有回答「端口该不该与用例实现共享同一个程序集」。截至 2026-05-07，事实数据是：

- Infrastructure 程序集只**用到** Application 程序集内的两棵子树（`Application/Abstractions/**` 7 个端口 + `Application/Llm/**` 7 个 LLM DTO）。
- 但通过 `<ProjectReference>` 拉进来的是**整个** Application 程序集，包含 `Runtime/Inbox/Chat/Memory/Services/Interfaces/DependencyInjection` 等 13 个用例 / 编排目录。
- 反向断言 `Infrastructure_DoesNotReferenceApplication` 写不出来，因为反向引用是允许的。

进一步审视端口本身：

- `IClock / IRuntimeStartTimeProvider / IAppDataPathProvider / IDbConnectionFactory / ISchemaInitializer / ILlmProvider` 与 7 个 LLM DTO 都不引用任何 Domain 类型；它们是中立的技术契约，命名空间挂在 `Dawning.AgentOS.Application.*` 之下只是历史遗留。
- 唯一有领域语义的端口是 `IDomainEventDispatcher`：它的 `DispatchAsync` 签名直接引用 `Dawning.AgentOS.Domain.Core.IDomainEvent`。这条端口与 `IDomainEvent` 同栖于 Domain.Core 比挂在 Application 更顺（与 Vernon《IDDD》§8 的 DomainEventPublisher 归属一致）。

[ADR-036](persistence-repository-style-dawning-orm-dapper.md) 刚通过 `Persistence/Entities/` + `Persistence/Repositories/` 双子目录契约把"目录就是契约"贯彻到 Infrastructure 内部。本 ADR 把同一种洁癖延伸到程序集边界。

多入口前瞻（WeChat OA / Mobile / Web）落地后将以新增 `Api.{Channel}` 平级 primary adapter 的方式接入；Application 与 Domain 边界保持不变。本 ADR 为该形态预留 LLM 能力契约的中立位置（在 Abstractions 内），不预设具体 channel 模型——后者由独立 ADR 承担。

## 备选方案

- M0. 维持现状：Infrastructure 引用整个 Application 主项目。
- M1. 端口物理拆到 `Application.Contracts`，命名空间保留 `Dawning.AgentOS.Application.*` 不变。
- M3. 反向：把 Application 用例代码（13 个目录）抽出到 `Application.UseCases`，原 Application 程序集瘦身为 Contracts。
- M4. 端口与 LLM DTO 全部迁入单一中立程序集 `Dawning.AgentOS.Abstractions`，含 `IDomainEventDispatcher`。
- M5. 端口按性质双桶：`IDomainEventDispatcher` → Domain.Core 与 IDomainEvent 同栖；其余 6 个端口 + 7 个 LLM DTO → 新建中立程序集 `Dawning.AgentOS.Abstractions`。

## 被否决方案与理由

### M0 维持现状：否决

- `Infrastructure_DoesNotReferenceApplication` 写不出来；反向断言永远缺一条。
- Application 用例代码膨胀时 Infrastructure rebuild 耦合越来越宽。
- 与 ADR-036「目录就是契约」洁癖形成内部一致性问题：内部用契约管，跨程序集边界反倒不管。

### M1 同命名空间分多程序集：否决

- 物理拆分了但命名空间仍叫 `Dawning.AgentOS.Application.*`，没有正面回答「为什么这些端口要挂在 Application 名下」的怀疑。
- "同命名空间跨多程序集"在团队代码库中是首次出现，需要一份 ADR 反复解释；正名重命名机械成本相当但心智更直白。

### M3 反向抽 UseCases：否决

- 要动的物理文件数量是 M5 的约 3 倍。
- "Application = 端口 + 用例"是 DDD/Hexagonal 主流共识；把用例从同名项目中拆走会让新人困惑「Application 项目剩什么」。
- 结果与 M5 等价（都让 Infrastructure 不依赖用例代码），代价高。

### M4 单桶 Abstractions：否决

- M4 把 `IDomainEventDispatcher` 也塞进 `Abstractions`。该端口本身是领域事件机制的一部分，与 `IDomainEvent` / `IDomainEventHandler<T>` 同栖于 Domain.Core 更准（与 Vernon《IDDD》§8 DomainEventPublisher 归属一致）。在 M5 下 Abstractions 已经允许引用 Domain.Core 当 shared kernel 用，但「分发器」属于领域事件机制本身、应该和 IDomainEvent 同址而不是「跨包消费」。
- M5 的双桶判据是机器可判的（「是否引用 Domain.Core 类型」），不会复制 [ADR-021](application-folder-layout.md)「Common/ 反模式」的语义空洞问题。

## 决策

采用 M5。

### D1. 新增程序集 `Dawning.AgentOS.Abstractions`

物理位置：`src/Dawning.AgentOS.Abstractions/`

内容（从 Application 主项目按文件物理迁出，命名空间统一改为 `Dawning.AgentOS.Abstractions.*`）：

```text
src/Dawning.AgentOS.Abstractions/
  IClock.cs                              namespace Dawning.AgentOS.Abstractions
  IRuntimeStartTimeProvider.cs           namespace Dawning.AgentOS.Abstractions
  Hosting/IAppDataPathProvider.cs        namespace Dawning.AgentOS.Abstractions.Hosting
  Persistence/IDbConnectionFactory.cs    namespace Dawning.AgentOS.Abstractions.Persistence
  Persistence/ISchemaInitializer.cs      namespace Dawning.AgentOS.Abstractions.Persistence
  Llm/ILlmProvider.cs                    namespace Dawning.AgentOS.Abstractions.Llm
  Llm/LlmCompletion.cs                   namespace Dawning.AgentOS.Abstractions.Llm
  Llm/LlmErrors.cs                       namespace Dawning.AgentOS.Abstractions.Llm
  Llm/LlmMessage.cs                      namespace Dawning.AgentOS.Abstractions.Llm
  Llm/LlmRequest.cs                      namespace Dawning.AgentOS.Abstractions.Llm
  Llm/LlmRole.cs                         namespace Dawning.AgentOS.Abstractions.Llm
  Llm/LlmStreamChunk.cs                  namespace Dawning.AgentOS.Abstractions.Llm
  Llm/LlmStreamChunkKind.cs              namespace Dawning.AgentOS.Abstractions.Llm
```

csproj 形态：

- `<TargetFramework>net10.0</TargetFramework>`
- `<RootNamespace>Dawning.AgentOS.Abstractions</RootNamespace>`
- `<AssemblyName>Dawning.AgentOS.Abstractions</AssemblyName>`
- `<IsPackable>false</IsPackable>`
- 零 `<PackageReference>`（仅依赖 BCL）。
- 唯一的 `<ProjectReference>`：→ `Dawning.AgentOS.Domain.Core`，用于取用 shared-kernel 原语 `Result<T>` / `DomainError`（见 D3）。Domain.Core 本身零外部依赖，传递不进任何 `<PackageReference>`。

### D2. `IDomainEventDispatcher` 下沉到 Domain.Core

将 `Application/Abstractions/IDomainEventDispatcher.cs` 物理迁入 `src/Dawning.AgentOS.Domain.Core/`，命名空间改为 `Dawning.AgentOS.Domain.Core`。理由：

- 该端口签名直接引用 `Dawning.AgentOS.Domain.Core.IDomainEvent`，与领域事件同栖契合 Vernon《IDDD》§8 的 DomainEventPublisher 归属。
- Domain.Core 上不引入任何第三方包；ADR-022 §5 的 `DomainCore_DoesNotReferenceAnyExternalPackages` 断言不破。
- AppService 持有 `IDomainEventDispatcher` 引用、聚合（Domain）只 raise 不 dispatch，依赖方向不变。

### D3. 桶判据（机器可判）

Domain.Core 在本仓里实际承担两重职责：(a) 领域事件机制本身（`IDomainEvent` / `IDomainEventHandler<T>` / `IAggregateRoot`），(b) 纯深 shared kernel 原语 `Result` / `Result<T>` / `DomainError`。后者被全工程当作「BCL 扩展」使用（`Result<T>` 是全部 AppService 的返回型，`DomainError` 是错误传达原语），跨层引用从开始就应该被允许。

基于此，桶判据如下：

- **进 Domain.Core**：端口 / 接口本身是领域事件机制的一部分（如 `IDomainEventDispatcher`，签名引用 `IDomainEvent`）。理由：与 Vernon《IDDD》§8 的 DomainEventPublisher 同栗，不仅是「签名抵不过去」。
- **进 Abstractions**：其他技术端口与 DTO。允许引用 Domain.Core 提供的 shared-kernel 原语（`Result<T>` / `DomainError` / `IAggregateRoot` 等§b 部分），**禁止**引用 `Dawning.AgentOS.Domain` / `Dawning.AgentOS.Domain.Services` / `Dawning.AgentOS.Application` / `Dawning.AgentOS.Infrastructure` / `Dawning.AgentOS.Api` 中的任何类型。
- **进 Application**：用例编排接口（`IXxxAppService`）与跨切片用例 DTO，签名引用 Domain 聚合或 Application Contracts。

判据仍是机器可判的：「依赖领域事件机制」、「依赖 Domain 聚合」、「只依赖 BCL + shared kernel」三者互斥，均可由架构断言校验（见 D6）。不会产生「这是技术还是领域」的反复讨论。

### D4. 命名空间正名（机械替换）

跨工作区一次性替换：

- `Dawning.AgentOS.Application.Abstractions` → `Dawning.AgentOS.Abstractions`
- `Dawning.AgentOS.Application.Abstractions.Hosting` → `Dawning.AgentOS.Abstractions.Hosting`
- `Dawning.AgentOS.Application.Abstractions.Llm` → `Dawning.AgentOS.Abstractions.Llm`
- `Dawning.AgentOS.Application.Abstractions.Persistence` → `Dawning.AgentOS.Abstractions.Persistence`
- `Dawning.AgentOS.Application.Llm` → `Dawning.AgentOS.Abstractions.Llm`

`IDomainEventDispatcher` 的 `using` 从 `Dawning.AgentOS.Application.Abstractions` 改为 `Dawning.AgentOS.Domain.Core`（多数情况下原本就 `using Dawning.AgentOS.Domain.Core;` 用 `IDomainEvent`，新增 `using` 项即可；若全工作区前缀替换被命中，需手动恢复并改为 Domain.Core）。

涉及文件约 30 个 `using` 改动 + 14 个文件物理 `git mv`，无业务逻辑改动。

### D5. ProjectReference 调整

- `Dawning.AgentOS.Abstractions`：唯一 → `Domain.Core`（取 shared kernel）。
- `Dawning.AgentOS.Domain.Core`：无依赖（`IDomainEventDispatcher` 仅引 `IDomainEvent`，已在同程序集）。
- `Dawning.AgentOS.Application` 新增 → `Abstractions`（保留 → `Domain.Core / Domain / Domain.Services`）；删除内部 `Abstractions/Llm` 目录。
- `Dawning.AgentOS.Infrastructure`：删除 → `Application`，新增 → `Abstractions`（保留 → `Domain.Core / Domain`）。
- `Dawning.AgentOS.Api`：保留 → `Application`，新增 → `Abstractions`（`Api.Endpoints.Llm` / `Api.Endpoints.Chat` 现已 `using` `Application.Llm`，正名后改 `using`）。

### D6. 架构测试调整

ADR-022 §5 既有断言行为：

- `Application_AbstractionsFolder_OnlyContainsInterfaces` —— 该命名空间已不在 Application 程序集；语义等价的检查改在 Abstractions 程序集上，重命名为 `AbstractionsAssembly_OnlyContainsInterfacesAndDtos`。
- `Application_DoesNotReferenceInfraOrApiLayers` —— 不变。
- `DomainCore_DoesNotReferenceAnyExternalPackages` —— 不变。

新增断言：

- `Infrastructure_DoesNotReferenceApplication`：`Infrastructure.GetReferencedAssemblies()` 不得包含 `"Dawning.AgentOS.Application"`，但允许 `"Dawning.AgentOS.Abstractions"` 与 `"Dawning.AgentOS.Domain.Core"`。
- `Abstractions_OnlyReferencesDomainCoreSharedKernel`：Abstractions 只允许引用 `Dawning.AgentOS.Domain.Core`（作为 shared kernel），不得引用 `Dawning.AgentOS.Domain` / `Dawning.AgentOS.Domain.Services` / `Dawning.AgentOS.Application` / `Dawning.AgentOS.Infrastructure` / `Dawning.AgentOS.Api`。
- `Abstractions_DoesNotReferenceFrameworkAdapterPackages`：Abstractions 不得引用 `Microsoft.Data.Sqlite / Dapper / Dawning.ORM.Dapper / Microsoft.AspNetCore.* / MediatR` 等基础设施包。
- `DomainCore_DispatcherSignaturesOnlyReferenceBclAndDomainCore`：Domain.Core 内的端口接口（`IDomainEventDispatcher`）签名只引用 BCL 与 Domain.Core 自有类型。

### D7. ADR-022 的局部 refine

ADR-022 §决策 1 中的目录树示意图给出的是 Application **主项目**内的逻辑结构。本 ADR 不改变其文件夹判据，但把 `Abstractions/` 子树物理位置改为独立程序集 + Domain.Core 同栖。ADR-022 状态保持 `accepted`，无需 supersede；后续更新时正文加一行回链到本 ADR 即可。

ADR-022 §5 中断言 `Application_AbstractionsFolder_OnlyContainsInterfaces` 的语义由本 ADR D6 接管并改名。

### D8. Solution 与共享配置

- 新项目加入 `Dawning.AgentOS.slnx` 的 `/src/` 目录组。
- 新项目继承 `Directory.Build.props` 与 `Directory.Packages.props`，`<IsPackable>false</IsPackable>`。
- 新项目无需新增 PackageReference。

### D9. 不在本 ADR 范围

- 跨切片 DTO（`Pagination/`、`Errors/`）是否进 Abstractions：等到出现「第二次」时由 ADR-022「第二次原则」再判（提示：DTO 若被 Infrastructure / Api 适配器消费才进 Abstractions，否则留 Application 主项目）。
- Abstractions 是否最终升级为可独立发布的 NuGet 包：远期可能性，本 ADR 不预设。
- 多入口（WeChat OA / Mobile / Web）的 channel 模型 / 身份模型 / 部署拓扑：均由独立 ADR 承担。本 ADR 仅保证 LLM 能力契约位于中立程序集，未来 `Api.{Channel}` 可平级新增 primary adapter 而不动 Application / Domain 边界。

## 影响

### 正向影响

- 反向架构断言可机器化：`Infrastructure_DoesNotReferenceApplication` 这条本应可写出的反向断言写出来了。
- Application 用例代码改动不再触发 Infrastructure rebuild（除非端口签名变化，那是合理的级联）。
- `IDomainEventDispatcher` 与 `IDomainEvent` 同栖，符合 Vernon《IDDD》§8 的 DomainEventPublisher 归属传统。
- 新人读 csproj 即可一眼看出 Hexagonal 形状：Adapter（Infrastructure）依赖 Ports（Abstractions + Domain.Core），不依赖 UseCases（Application）。
- LLM 能力契约从 `Application.Llm` 正名为 `Abstractions.Llm`，未来多入口（WeChat / Mobile / Web）adapter 可直接消费而不必绕道 Application。
- 与 ADR-036「目录就是契约」一脉相承，把契约从「目录」延伸到「程序集」。

### 代价 / 风险

- 30 处 `using` 一次性机械替换 + 14 个文件 `git mv`；无业务逻辑改动，但需要一次完整 build + test 验证全绿。
- 项目数从 5 增至 6；solution 拓扑略复杂，但仍属合理范围。
- Abstractions 项目未来增长时需对每个新端口判定「是否引用 Domain.Core」；判据虽然机器可判，但仍需 reviewer 注意。模糊端口（如 `IPasswordHasher`：是否引用 `User` 聚合？）出现时按 D9 触发复议。
- `IDomainEventDispatcher` 落 Domain.Core 是 Vernon 派系实践；Uncle Bob CA 主流派会认为 dispatcher 是 application/infrastructure concern。本仓库已在 ADR-022 §5 立 `DomainCore_DoesNotReferenceAnyExternalPackages` 断言守住 Domain.Core 的纯净性，可控。

## 复议触发条件

参见 front matter `adr_revisit_when` 字段，正文不重复。

## 相关页面

- [ADR-022 去 MediatR：自研领域事件分发器与 AppService 立面](no-mediator-self-domain-event-dispatcher.md)：本 ADR 局部 refine 其 §决策 1 目录树的物理实现与 §5 架构断言的程序集归属；ADR-022 状态不变。
- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：工程骨架与依赖方向的总前提。
- [ADR-036 持久化仓储风格统一：Infrastructure Repository 采用 Dawning.ORM.Dapper](persistence-repository-style-dawning-orm-dapper.md)：刚把「目录即契约」落到 Persistence 内部；本 ADR 是同一原则向程序集边界的延伸。
- [ADR-020 架构测试断言策略](architecture-test-assertion-strategy.md)：本 ADR 新增的架构断言遵循其类型级到具体类型名的纪律。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 是 M5 实施前的方案确认产物。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
