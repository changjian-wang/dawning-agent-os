---
title: ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电
type: adr
subtype: architecture
canonical: true
summary: 第一刀工程骨架只验证 Electron 桌面壳、Arco Design Vue UI、DDD 分层 ASP.NET Core 本地后端、startup token 通信与 SQLite/Dapper 最小数据链路。
tags: [agent, security]
sources: []
created: 2026-04-28
updated: 2026-04-28
verified_at: 2026-04-28
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/repository-shape-product-monorepo-with-wiki.md, pages/adrs/mvp-desktop-stack-electron-aspnetcore.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-28
adr_revisit_when: "V0 骨架通电并完成 health endpoint、startup token、SQLite/Dapper 与架构测试后；或进入 inbox item / Memory Ledger / LLM provider 第二阶段方案前。"
---

# ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电

> 第一刀工程骨架只验证 Electron 桌面壳、Arco Design Vue UI、DDD 分层 ASP.NET Core 本地后端、startup token 通信与 SQLite/Dapper 最小数据链路。

## 背景

ADR-014 已确定 MVP 第一版切片是聊天窗口 + agent inbox + 读侧整理。ADR-015 已确定产品代码进入 dawning-agent-os 本仓库。ADR-016 已确定桌面技术栈为 Electron + Arco Design Vue + TypeScript + Vite + ASP.NET Core 本地后端，并默认使用 SQLite。

当前还缺少一个可以真正开始 scaffold 的工程骨架方案。若直接一次性生成完整业务代码，容易把 LLM provider、Memory Ledger、inbox、SQLite、桌面进程管理和打包发布混在一起，导致第一刀过大。若后端骨架过于扁平，后续再补 DDD 分层、仓储接口、应用服务和基础设施边界会带来较高迁移成本。若只写后端或只写前端，又无法验证本产品最关键的双进程桌面形态。

因此，V0 骨架应先回答一个更小的问题：桌面壳能否启动、DDD 分层后端能否启动、二者能否用受控 token 通信、SQLite + Dawning.ORM.Dapper 是否能通过最小 CRUD 验证。只有这些通电后，才进入第一版业务模型。

本页是已接受的工程骨架决策。它确认 V0 scaffold 的架构边界；实际创建产品目录或代码前，仍必须按 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md) 给出执行方案并获得 user 确认。

## 备选方案

- 方案 A：先做全量 scaffold，一次性创建桌面端、后端、Core、数据层、LLM provider、Memory、inbox 和测试。
- 方案 B：先只做扁平后端 API 与 SQLite 验证，前端和 Electron 后置。
- 方案 C：先只做 Electron + Arco Design Vue 界面，后端用 mock。
- 方案 D：先做最小通电骨架：Electron 桌面壳 + Arco Design Vue 占位 UI + DDD 分层 ASP.NET Core 后端 + health endpoint + startup token 通信 + SQLite/Dapper 最小测试。

## 被否决方案与理由

**方案 A 全量 scaffold**：

- 范围过大，会把尚未验证的业务模型、provider 抽象和桌面进程管理一起固化。
- 容易违反“方案先行”的精神：看似省时间，实际会让后续回滚和重构成本变高。
- 第一刀的目标应是降低不确定性，而不是一次性铺满目录。

**方案 B 只做扁平后端**：

- 可以快速验证 SQLite/Dapper，但无法验证 Electron + ASP.NET Core 的本地桌面进程模型。
- 扁平后端会推迟 DDD 边界设计；一旦业务开始进入 Memory、inbox、LLM provider 与权限动作模型，再拆出 Domain / Application / Infrastructure 会更麻烦。
- 本产品不是纯 Web API；如果不尽早验证桌面壳与本地后端通信，后续可能在打包、启动和 token 传递上返工。

**方案 C 只做前端 mock**：

- 可以快速看到界面，但会绕开本地 runtime、localhost token、health check 和数据链路这些关键风险。
- mock 过多会让第一版看起来“有产品”，但真正的后端集成仍未发生。

## 决策

采用方案 D：先创建最小通电骨架。

目标：

- 验证 Electron 能启动桌面窗口并承载 Arco Design Vue + TypeScript + Vite UI。
- 验证 DDD 分层 ASP.NET Core 本地后端能启动并提供 health endpoint。
- 验证 Electron 启动时能获得后端随机端口与 startup token，并用 token 调用 health endpoint。
- 验证 Microsoft.Data.Sqlite + Dawning.ORM.Dapper 能完成最小 CRUD 与基础分页行为。

目录 / 模块边界采用“项目边界先固定，业务目录按垂直切片增长”的粒度。V0 方案应明确第一层和关键第二层目录，避免后续拆层；但不为尚未实现的业务创建大量空目录。

```text
apps/
	desktop/                          # Electron + Arco Design Vue + TypeScript + Vite
		electron/
			main/                         # Electron main process：窗口、后端进程、端口 / token 注入
			preload/                      # 受控 IPC 暴露；不直接暴露 Node API 给 renderer
		src/
			api/                          # renderer 调用本地后端的 typed client
			app/                          # Vue app bootstrap、providers、全局错误处理
			components/                   # 通用 UI 组件
			layouts/                      # 桌面主布局
			pages/
				chat/                       # V0 占位页；后续承载聊天切片
				inbox/                      # V0 占位页；后续承载 agent inbox
				memory/                     # V0 占位页；后续承载 Memory Ledger
			stores/                       # Pinia store；V0 只放 runtime / connection 状态
			styles/                       # 全局样式与 Arco 主题入口
			types/                        # 前端共享类型

src/
	Dawning.AgentOS.Api/              # ASP.NET Core 本地 API 宿主
		Endpoints/                      # Minimal API endpoint groups；V0 只放 Health / Runtime
		Middleware/                     # startup token、本地请求边界、错误处理
		Options/                        # Api host / security / desktop integration options
		Program.cs
	Dawning.AgentOS.Application/      # 用例编排、DTO、应用服务接口
		Abstractions/                   # 应用层端口：runtime、storage、clock、user data path 等
		Common/                         # 应用层结果、分页、错误模型
		Contracts/                      # API / application DTO；不向前端暴露 Domain entity
		Runtime/                        # V0 health / runtime status 用例
	Dawning.AgentOS.Domain/           # 实体、值对象、领域事件、仓储接口、领域概念
		Common/                         # Entity、ValueObject、DomainEvent、Timestamp 等基础模型
		Repositories/                   # 仓储接口；具体实现只在 Infrastructure
		Runtime/                        # V0 运行状态 / 本地实例领域概念
		Permissions/                    # ActionLevel、PermissionDecision、确认边界等产品权限模型
	Dawning.AgentOS.Domain.Services/  # 领域服务 / 领域策略；只依赖 Domain
		Permissions/                    # 动作分级、确认策略等跨实体 / 值对象的纯领域规则
	Dawning.AgentOS.Infrastructure/   # SQLite/Dapper、provider、系统路径、DI
		Diagnostics/                    # 本地日志、trace、诊断文件路径
		DependencyInjection/            # AddInfrastructure 等 DI 扩展
		Persistence/
			Sqlite/                       # SQLite connection factory、schema bootstrap、migrations
			Dapper/                       # Dawning.ORM.Dapper adapter 验证与仓储基类
			Repositories/                 # Domain 仓储接口的 Dapper / SQLite 实现
		System/                         # 用户数据目录、平台路径、进程 / 环境适配
		Security/                       # startup token 校验实现

tests/
	Dawning.AgentOS.Architecture.Tests/ # 项目引用与层依赖方向测试
	Dawning.AgentOS.Domain.Tests/     # 纯领域测试；不引用 Infrastructure
	Dawning.AgentOS.Domain.Services.Tests/ # 纯领域服务测试；不引用 Application / Infrastructure
	Dawning.AgentOS.Application.Tests/# 用例与端口测试；mock Infrastructure
	Dawning.AgentOS.Infrastructure.Tests/ # SQLite / Dapper 集成测试
	Dawning.AgentOS.Api.Tests/        # endpoint 与 startup token 测试
```

业务模块增长规则：

- `Memory`、`Inbox`、`LLMProviders`、`InterestProfile` 等目录不在 V0 为了“看起来完整”而空建。
- 当进入某个垂直切片时，再同时在 Domain / Domain.Services / Application / Infrastructure / Api 中增加对应目录与实现。
- 每个新增业务目录必须能回答“它属于领域规则、用例编排、基础设施适配，还是 HTTP 边界”，不能用 `Services/` 兜底堆放。
- Domain Services 单独放在 `Dawning.AgentOS.Domain.Services` class library；它仍属于 Domain layer，不是 Application service layer。
- Domain Services 只用于不自然归属于某个实体 / 值对象、但仍属于领域规则的无状态行为；应用流程编排放 Application，外部系统访问放 Infrastructure。
- `Dawning.AgentOS.Domain.Services` 只能引用 `Dawning.AgentOS.Domain` 与 BCL；Domain 不反向引用 Domain.Services，避免实体 / 值对象依赖服务编排。
- 领域中的 `Permissions` 只表达产品动作权限、动作分级、确认边界等业务概念；startup token 属于本地通信安全，不进入 Domain / Domain.Services。
- 前端页面目录可以先有 chat / inbox / memory 占位，因为这是 ADR-014 已确认的界面结构；占位页只展示状态，不提前实现业务流。

架构补充约束：

- 后端是本地 modular monolith，不拆微服务；层拆分用于边界和可测试性，不引入内部 HTTP / RPC。
- API 与前端只交换 Contracts / DTO，不直接暴露 Domain entity 或 Infrastructure persistence object。
- Repository interface 归属 Domain；UnitOfWork / transaction boundary 作为 Application 端口表达，由 Infrastructure 提供实现。
- UnitOfWork 不向 Application 暴露裸 `IDbConnection`；需要备份、导出、迁移、事务等能力时，使用明确的 Application port，由 Infrastructure 提供实现。
- SQLite schema、migration、bootstrap 和 schema version 归属 Infrastructure/Persistence，不散落到 API 或 Application。
- DI 采用显式 `AddApplication`、`AddDomainServices`、`AddInfrastructure` 扩展；V0 不使用按命名后缀自动扫描注册所有 Service / Repository 作为默认机制。
- Api 入口只负责本地宿主、middleware 顺序和调用各层 DI 扩展，不在 `Program.cs` 中堆叠大量注册细节。
- Mapping 以 Application Contracts 的显式转换为主；V0 不引入 AutoMapper，避免映射规则隐式化。
- Electron main process 负责后端进程生命周期：启动、端口 / token 注入、日志路径、异常退出提示与正常关闭。
- 本地日志、trace 与诊断文件默认落在系统用户应用数据目录，不写入仓库目录。
- 架构测试必须验证层依赖方向，避免 Domain / Application 反向引用 Infrastructure 或 API。

第一刀只放入这些能力：

- Desktop shell：窗口启动、基础布局、后端连接状态展示。
- Frontend UI：Chat、Inbox、Memory Ledger 三个占位区域；不实现真实业务流。
- Local API：health endpoint 与启动 token 校验。
- Domain：只放 V0 所需的最小领域边界，例如基础实体约定、动作级别 / 权限模型、runtime 状态等领域概念；不提前建完整 agent framework。
- Domain.Services：只放确有 V0 领域规则需要的纯领域服务，例如动作分级 / 确认策略；不把应用编排或技术安全逻辑放入此项目。
- Application：只放 health / runtime 状态这类最小用例边界，以及必要端口接口；不提前实现聊天、inbox 或 Memory 应用服务。
- Infrastructure：只放 SQLite 连接、Dapper 验证、系统数据目录解析、startup token 校验实现与基础 DI 注册。
- Storage validation：最小 SQLite 数据库位置解析、连接创建、Dapper CRUD 验证测试。

关键技术选择：

- 前端使用 Arco Design Vue + TypeScript + Vite，并采用桌面产品所需的轻量工程组织，不复制后台管理模板。
- 后端使用 ASP.NET Core + DDD 分层，遵循 DI、Options、ILogger、CancellationToken 等基础设施约定。
- 依赖方向为 Api -> Application -> Domain.Services -> Domain，Application 也可直接引用 Domain；Infrastructure 依赖 Application / Domain 并通过 DI 提供实现；Domain 不依赖 Domain.Services、Application、Infrastructure、Dapper、ASP.NET Core、SQLite 或外部 LLM SDK。
- Domain 与 Domain.Services 默认不引用 MediatR、ASP.NET Core、Microsoft.Extensions.Configuration、Microsoft.Extensions.Logging 或其它框架包；需要日志、配置、事件发布时，由 Application port 或 Infrastructure adapter 处理。
- 数据访问使用 Microsoft.Data.Sqlite + Dawning.ORM.Dapper；第一刀不引入 MySQL provider。
- 通信采用 localhost + 随机端口 + startup token；第一刀不引入 named pipe / Unix domain socket。

验证方式：

- 后端 health endpoint 能在本地启动后返回健康状态。
- 未携带 startup token 的本地请求被拒绝，携带 token 的请求成功。
- Electron 桌面窗口能显示后端连接状态。
- SQLite/Dapper 测试覆盖插入、查询、更新、删除和基础分页。
- 测试或项目引用能验证依赖方向：Domain 不引用 Domain.Services / Application / Infrastructure / Api，Domain.Services 不引用 Application / Infrastructure / Api，Application 不引用 Infrastructure，Api 不直接写 SQLite/Dapper。
- 架构测试能验证 API 不直接返回 Domain entity，Infrastructure persistence object 不穿透到 Application / API contracts。
- 架构测试能验证 Domain / Domain.Services 不引用 MediatR、ASP.NET Core、Dapper、SQLite、Microsoft.Extensions.Configuration、Microsoft.Extensions.Logging 等框架或基础设施包；Application 不引用 Dapper、SQLite、YARP、MailKit、HttpContext 等具体 adapter 包。
- SQLite bootstrap 能创建 schema version 记录，后续 migration 有明确入口。
- 骨架构建与测试通过后，再进入 inbox item、Memory Ledger 和 LLM provider 的第二阶段方案。

明确不做：

- 不接 GPT / DeepSeek。
- 不实现真实聊天编排。
- 不实现 inbox item 数据模型。
- 不实现 Memory Ledger 业务模型。
- 不做外部文件扫描、移动、重命名或删除。
- 不做账号系统、云同步、MySQL provider、installer、自动更新或发布流水线。

## 影响

**正向影响**：

- 第一刀能验证最不确定的跨进程桌面形态，而不把业务模型提前做重。
- 后端、前端、桌面壳和 SQLite 数据链路会同时得到最小验证。
- DDD 边界会在第一天固定，后续扩展 Memory、inbox、LLM provider、权限动作模型时不需要先拆层。
- 后续业务开发可以沿着已通电的骨架做垂直切片，降低每步变更的风险。
- SQLite/Dapper 兼容性会在第一时间被测试验证，避免业务层写完后才发现 adapter 细节问题。

**代价 / 风险**：

- 第一刀用户可见功能很少，主要是工程通电，不会马上形成完整产品体验。
- Electron 启动 ASP.NET Core 后端的进程管理细节可能比单一 Web App 更复杂。
- DDD 分层会比扁平后端多几个项目和引用关系；V0 必须只建边界和最小验证，不把企业级模块一次性搬进来。
- startup token 只是 MVP 调试友好的边界，不应被误认为长期安全模型。
- 如果过早在 Core 中抽象 agent framework，会偏离产品先行原则；V0 必须克制。

## 复议触发条件

本方案在 V0 骨架通电并完成 health endpoint、startup token、SQLite/Dapper 与架构测试后复议；或在进入 inbox item、Memory Ledger、LLM provider 第二阶段方案前复议。若执行方案中发现目录边界、进程模型、SQLite/Dapper 验证顺序或第一刀非目标需要调整，应先更新 ADR 或补充新 ADR，而不是直接开始 scaffold。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：本方案对应的产品契约、MVP 技术形态与方案先行要求。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-014 MVP 第一版切片：聊天 + inbox + 读侧整理](mvp-first-slice-chat-inbox-read-side.md)：定义第一版界面与动作范围。
- [ADR-015 仓库形态：产品 monorepo + 内置 LLM-Wiki](repository-shape-product-monorepo-with-wiki.md)：定义产品代码承载仓库。
- [ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端](mvp-desktop-stack-electron-aspnetcore.md)：定义第一版产品技术栈。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：定义实现前先方案、后确认、再执行。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
