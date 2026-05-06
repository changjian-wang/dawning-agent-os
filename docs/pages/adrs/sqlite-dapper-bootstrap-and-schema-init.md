---
title: ADR-024 SQLite/Dapper 通电：连接工厂、Schema 引导与 V0 持久化骨架
type: adr
subtype: architecture
canonical: true
summary: V0 持久化骨架在 Application 层暴露 IDbConnectionFactory / IAppDataPathProvider / ISchemaInitializer 端口；Infrastructure 层用 Microsoft.Data.Sqlite + 嵌入资源 SQL + __schema_version 表实现幂等迁移；启动时由 IHostedService 同步执行；不前置 IUnitOfWork / Repository；烟雾验证扩展 RuntimeStatus.Database 而非新增 endpoint。
tags: [agent, engineering]
sources: []
created: 2026-05-01
updated: 2026-05-01
verified_at: 2026-05-01
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-desktop-stack-electron-aspnetcore.md, pages/adrs/engineering-skeleton-v0.md, pages/adrs/backend-architecture-equinox-reference.md, pages/adrs/no-mediator-self-domain-event-dispatcher.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/architecture-test-assertion-strategy.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-01
adr_revisit_when: "V0 进入第一个 Aggregate Repository 实现时（需要落地 IUnitOfWork 胖入口）；或迁移 SQL 数 ≥ 10、出现需要回滚的破坏性 schema 变更；或多设备同步 / 云后端进入项目；或 Microsoft.Data.Sqlite / Dawning.ORM.Dapper 与目标 .NET 主版本不再兼容；或 SQLite 单文件无法承载（>1GB / 高并发写入冲突）；或出现需要静态加密的隐私要求（如 SQLCipher）。"
---

# ADR-024 SQLite/Dapper 通电：连接工厂、Schema 引导与 V0 持久化骨架

> V0 持久化骨架在 Application 层暴露 IDbConnectionFactory / IAppDataPathProvider / ISchemaInitializer 端口；Infrastructure 层用 Microsoft.Data.Sqlite + 嵌入资源 SQL + __schema_version 表实现幂等迁移；启动时由 IHostedService 同步执行；不前置 IUnitOfWork / Repository；烟雾验证扩展 RuntimeStatus.Database 而非新增 endpoint。

## 背景

[ADR-016](mvp-desktop-stack-electron-aspnetcore.md) 已确定 V0 持久化技术栈是 SQLite + Dapper + `Dawning.ORM.Dapper` SDK，并要求"应用项目自行引用 `Microsoft.Data.Sqlite` 并创建 `SqliteConnection`"。[ADR-017](engineering-skeleton-v0.md) 把"完成 SQLite/Dapper 验证"列入 V0 通电的最后一个 gate，并把 `adr_revisit_when` 写明须在该 gate 后才能进入 inbox / Memory Ledger / LLM provider 第二阶段。[ADR-018](backend-architecture-equinox-reference.md) 提到 `IUnitOfWork` 胖入口与按命名后缀自动注册，但没钉死连接抽象、SQLite 文件路径、迁移机制与连接生命周期。[ADR-022](no-mediator-self-domain-event-dispatcher.md) 把 Application 层定型为 OO AppService 立面，要求事务边界由 AppService 内部用 `IUnitOfWork` 显式承担。[ADR-023](api-entry-facade-and-v0-endpoints.md) 把 Api 层立面定型，但持久化空转：`/api/runtime/status` 只返回 uptime，不读不写任何数据库。

S5 进入实施前缺以下工程问题需要锁死：

- 连接抽象端口的接口形态与归属层。
- SQLite 文件路径的获取策略（跨平台 + 测试可替换）。
- 迁移机制（幂等 SQL / 第三方迁移工具 / 启动时建表）。
- 迁移触发时机（IHostedService / lazy / Program.cs 同步）。
- 连接生命周期（per-call / scoped / singleton）。
- 是否在 S5 一并落地 `IUnitOfWork` 与 Repository。
- 烟雾验证的对外形态（扩展 status / 独立 db-ping endpoint / 仅测试覆盖）。
- Schema 版本表形态与命名。
- 测试中 SQLite 形态（in-memory shared / temp file / 真实 path）。
- 架构测试新增哪些断言。

如果让 S5 落地代码"边写边定"，会重蹈 S3 → S4 期间 ADR-021 → ADR-022 的覆辙。本 ADR 在 ADR-016 / ADR-017 / ADR-018 / ADR-022 / ADR-023 既有契约之上，把持久化骨架的形态钉死，作为 S5 实施的依据；按 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)，代码落地仍须在本 ADR 接受后才进行。

## 备选方案

连接抽象端口（A 轴）：

- **A1** `IDbConnectionFactory.OpenAsync(CancellationToken)` 返回 `DbConnection`，端口归属 `Application/Abstractions/Persistence/`。
- **A2** Application 直接注入 `Func<IDbConnection>` delegate。
- **A3** 不抽象端口，Application 直接依赖 `Microsoft.Data.Sqlite`。

SQLite 文件路径策略（B 轴）：

- **B1** `IAppDataPathProvider.GetDatabasePath()` 端口（Application）+ Infrastructure 实现走平台特定 app-data 目录（macOS `~/Library/Application Support/dawning-agent-os/`、Linux `XDG_DATA_HOME` 或 `~/.local/share/dawning-agent-os/`、Windows `%LOCALAPPDATA%\dawning-agent-os\`）；Options 支持 override 用于测试。
- **B2** 硬编码到当前进程工作目录。
- **B3** 完全由 `appsettings.json` 配置，无端口抽象。

迁移机制（C 轴）：

- **C1** 嵌入资源 `.sql` 文件（`Persistence/Migrations/NNNN_*.sql`）+ 启动时幂等执行 + `__schema_version` 表跟踪。
- **C2** 引入 DbUp / FluentMigrator / Roundhouse 等第三方迁移库。
- **C3** EquinoxProject 风格的"启动时 `EnsureCreated()`"代码内建表。

迁移触发时机（D 轴）：

- **D1** `IHostedService.StartAsync` 在 `app.Run()` 之前同步执行。
- **D2** 第一次拿连接时 lazy 触发（双检锁）。
- **D3** `Program.cs` 中 `app.Services.GetRequiredService<ISchemaInitializer>().InitializeAsync(...).GetAwaiter().GetResult()` 同步阻塞调用。

连接生命周期（E 轴）：

- **E1** per-call open + dispose（factory 每次新建 `SqliteConnection`，调用方 `using`）。
- **E2** long-lived singleton 连接。
- **E3** scoped 连接（per-request）。

UnitOfWork 是否前置（F 轴）：

- **F1** S5 不实现 `IUnitOfWork`（没有 Aggregate 不需要事务边界）。
- **F2** S5 落地 `IUnitOfWork` 空壳但只暴露 `BeginTransaction`。
- **F3** 完整 `IUnitOfWork` + 胖入口（每个 Aggregate 一个属性）。

Repository 是否前置（G 轴）：

- **G1** S5 不实现任何 Repository。
- **G2** S5 实现一个 demo Repository（如 `IRuntimePingRepository`）以验证 `Dawning.ORM.Dapper` 的 CRUD 路径。
- **G3** S5 直接为 ADR-014 第一切片（inbox / Memory Ledger）做 Aggregate Repository。

烟雾验证形态（H 轴）：

- **H1** 扩展现有 `RuntimeStatus` DTO，加 `DatabaseStatus { bool Ready; long? SchemaVersion; string? FilePath }` 子记录。
- **H2** 新增独立 endpoint `/api/runtime/db-ping`。
- **H3** 不加运行时 endpoint，只在 `Dawning.AgentOS.Api.Tests` / `Dawning.AgentOS.Infrastructure.Tests` 里直接调 factory 验证。

Schema 版本表形态（I 轴）：

- **I1** `__schema_version (version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL)`，每条迁移写一行。
- **I2** 单行表 `(current_version INTEGER NOT NULL)`，每次迁移更新该行。
- **I3** 不要版本表；靠迁移 SQL 自身的 `CREATE TABLE IF NOT EXISTS` / `ALTER TABLE` 兜底幂等。

测试 SQLite 形态（J 轴）：

- **J1** `Mode=Memory;Cache=Shared` + 每个测试新 connection string（如 `Data Source=test_<guid>;Mode=Memory;Cache=Shared`）。
- **J2** 临时文件 `Path.GetTempFileName()`，测试结束删除。
- **J3** 真实 path（覆盖 `IAppDataPathProvider`）。

架构测试新增（K 轴）：

- **K1** 新增三条断言：① Application / Domain / Domain.Services 都不依赖 `Microsoft.Data.Sqlite` / `Dapper` / `Dawning.ORM.Dapper`；② `IDbConnectionFactory` 必须在 `Dawning.AgentOS.Application.Abstractions.Persistence` 命名空间；③ `Persistence/` 下的具体实现必须在 `Dawning.AgentOS.Infrastructure.Persistence` 命名空间。
- **K2** 不新增。

## 被否决方案与理由

**A2 `Func<IDbConnection>`**：

- delegate 难承载多方法（如未来需要 `OpenReadOnlyAsync` / `OpenWriteAsync` 区分），扩展性差。
- 接口对 Roslyn / mock 工具链友好，`Mock<IDbConnectionFactory>` 比 `Mock<Func<IDbConnection>>` 直观。
- delegate 没有 `CancellationToken` 自然位置；接口方法可以正常签名 `Task<DbConnection> OpenAsync(CancellationToken)`。

**A3 不抽象端口**：

- 让 Application 识别 `Microsoft.Data.Sqlite`，违反 [ADR-022](no-mediator-self-domain-event-dispatcher.md) / [ADR-023](api-entry-facade-and-v0-endpoints.md) 中的 Application 层依赖边界（Application 只依赖 `Microsoft.Extensions.DependencyInjection.Abstractions` 这一个窄包）。
- 与 [ADR-016](mvp-desktop-stack-electron-aspnetcore.md) "严格依赖边界：Dapper、SQLite 不得穿透进 Domain / Domain.Services；Application 只依赖端口和必要的 .NET abstractions" 直接冲突。

**B2 硬编码到工作目录**：

- 桌面 App 的"工作目录"在 Electron 启动 `dotnet` 子进程时不可控（取决于打包形态）；同一台机器多用户登录会写到错误位置。
- macOS App Sandbox / Windows AppContainer 限制下，工作目录可能根本不可写。

**B3 完全配置化无端口**：

- 测试时仍需在 `appsettings.json` 注入路径，仍需要某种抽象点；不如直接抽 `IAppDataPathProvider` 让 Options 在适配器内部消费。
- 端口归属 Application 让"路径策略"成为一等公民，未来要加云同步备份目录、加密目录等扩展点时有自然挂载位置。

**C2 DbUp / FluentMigrator / Roundhouse**：

- V0 表数 < 5（仅 `__schema_version` + 后续 ADR-014 第一切片的 inbox / Memory）；引入第三方迁移库是 over-engineering。
- 第三方库带 NuGet 依赖、配置约定、自有日志通道，与 [ADR-017](engineering-skeleton-v0.md) "尽量少依赖" 精神冲突。
- 自研嵌入资源 `.sql` + `__schema_version` 表的总代码量约 50 行，已在 EquinoxProject v1.10 等项目验证可行。
- 复议触发条件已写入 front matter（迁移 SQL 数 ≥ 10 或出现需要回滚的破坏性变更），届时再引入。

**C3 EnsureCreated 风格**：

- 建表 SQL 散落在各 Repository / 启动代码里，无法 audit 当前 schema 完整状态。
- 没有版本概念，未来无法支持渐进迁移；一旦有第二条 `ALTER TABLE`，会立刻失效。
- EquinoxProject 自身在 v1.10 已经从 `EnsureCreated` 转向 EF Migrations，是反向案例。

**D2 lazy 迁移**：

- 第一次请求承担迁移延迟 + 异常路径，让本来快速失败（fail-fast）的启动错误延后到运行时。
- 测试需要先打一次 endpoint 才能确认 schema 就绪，违反 J1 in-memory shared 的快速反馈。
- 双检锁会引入并发原语，复杂度比 D1 高。

**D3 `Program.cs` 中 `GetAwaiter().GetResult()`**：

- 阻塞调用在 `Microsoft.Extensions.Hosting` 推荐路径之外，会让 `IConfiguration` / `ILogger` 注册顺序变得脆弱。
- `IHostedService` 是 .NET 标准的"启动同步初始化"位置，工具链（健康检查、生命周期日志）都识别这条路径。

**E2 long-lived singleton 连接**：

- SQLite 在多线程下需要每个写者独占连接；singleton 连接在 ASP.NET Core 多请求并发下会被多个请求争用，要么需要外部锁、要么用文件锁，性能与正确性都差。
- 连接寿命与进程寿命挂钩，进程崩溃时事务边界含混。

**E3 scoped 连接**：

- ASP.NET Core scope 模型是 per-request；每个请求一个连接看似优雅，但 SQLite 单文件 + WAL 模式下短连接更稳，scoped 反而会让"读多写少"的请求长持连接。
- scoped 连接在非 HTTP 路径（IHostedService / 启动迁移）需要手动 `IServiceScopeFactory.CreateScope()`，反而比 per-call 的 `using` 复杂。

**F2 / F3 前置 UnitOfWork**：

- F1 之外的方案要求现在就决定 `IUnitOfWork` 形态，但没有真实 Aggregate 时无法验证形态正确性；写出来的 UoW 是空壳，会被第一个真实 Aggregate Repository 直接重写。
- [ADR-018](backend-architecture-equinox-reference.md) 中 `IUnitOfWork` 胖入口的论证依赖于"每个 Aggregate 一个属性"；零 Aggregate 时这个论证失效。
- 推迟到 ADR-024 复议触发条件之一（"V0 进入第一个 Aggregate Repository 实现时"），届时一并处理。

**G2 demo Repository**：

- 会在 Domain 层引入"伪聚合"（`RuntimePing`），污染领域模型，违反 [ADR-022](no-mediator-self-domain-event-dispatcher.md) 中"Domain 只承载真实业务概念"的约束。
- 烟雾测试用 `__schema_version` 自身的读取就够（H1），不需要业务表参与。

**G3 直接做 inbox / Memory Aggregate Repository**：

- [ADR-014](mvp-first-slice-chat-inbox-read-side.md) 的产品流程页还没把 inbox / Memory 的字段、生命周期、状态机定义清楚；现在做 Repository 会反向锁死产品边界。
- S5 的目标是"骨架通电"，不是"第一刀业务"；混入业务会让本切片的影响面失控。

**H2 独立 db-ping endpoint**：

- 把内部实现（"我们用了一个数据库"）泄露到 URL 表面；前端 / 桌面壳没有"分别检查 db / 运行时"的需求。
- 多一个 endpoint 多一份维护成本（架构断言、测试用例、版本契约）。

**H3 只在测试里**：

- 违反 [ADR-017](engineering-skeleton-v0.md) "V0 通电要在运行时可观察"的精神；运行时无法人工验证 SQLite 是否就绪。
- Electron 桌面壳启动后仍需要看到"数据库 ready" 才能切到主界面；没有运行时信号会让壳层做盲启动。

**I2 单行版本表**：

- 丢失迁移历史，无法回溯"哪次启动应用了哪些迁移、什么时间"。
- 出现迁移失败时，无法判断是"第 N 条 SQL 跑了一半"还是"全部成功后 crash"。

**I3 没有版本表**：

- 所有 SQL 必须 `CREATE TABLE IF NOT EXISTS`，但 `ALTER TABLE` 在 SQLite 没有 idempotent 形态（`ADD COLUMN IF NOT EXISTS` 不存在）。一旦有第一条 alter 就崩。
- 无法判断"这是第一次启动还是第 100 次启动"。

**J2 temp file**：

- 测试间清理脆弱（断言失败时文件残留）；CI 并发跑测试时撞文件锁。
- macOS / Windows / Linux 的 `Path.GetTempFileName()` 行为不一致（macOS 自动清理周期不确定）。

**J3 真实 path 覆盖**：

- 测试污染开发者的 app-data 目录；CI runner 上 app-data 路径可能根本不可写。
- 与 J1 的 in-memory shared 形态相比，没有任何收益。

**K2 不新增架构断言**：

- Persistence 层是新增的、最容易被穿透的边界（实际工程中 Application 调 Dapper / Application 直接 import `SqliteConnection` 是高频反模式）。
- [ADR-020](architecture-test-assertion-strategy.md) 已确立"架构断言要在新增层时同步建立 baseline"，否则代码先漂移再补断言会失败成本更高。

## 决策

采用：A1 + B1 + C1 + D1 + E1 + F1 + G1 + H1 + I1 + J1 + K1。

### 1. 端口与命名空间

Application 层 `Abstractions/` 下新增以下端口：

```text
src/Dawning.AgentOS.Application/Abstractions/
  Persistence/
    IDbConnectionFactory.cs        # Task<DbConnection> OpenAsync(CancellationToken)
    ISchemaInitializer.cs          # Task InitializeAsync(CancellationToken)
  Hosting/
    IAppDataPathProvider.cs        # string GetDatabasePath()
```

接口签名固定为：

```csharp
namespace Dawning.AgentOS.Application.Abstractions.Persistence;

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);
}

public interface ISchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
```

```csharp
namespace Dawning.AgentOS.Application.Abstractions.Hosting;

public interface IAppDataPathProvider
{
    string GetDatabasePath();
}
```

返回 `System.Data.Common.DbConnection`（abstract base）而非 `IDbConnection`，因为 Dapper async API（`QueryAsync` / `ExecuteAsync`）在 `DbConnection` 上才有完整签名。`DbConnection` 来自 BCL `System.Data.Common`，Application 层无需新增 NuGet 依赖。

### 2. Infrastructure 适配器

Infrastructure 层 `Persistence/` 与 `Hosting/` 下新增以下实现：

```text
src/Dawning.AgentOS.Infrastructure/
  Persistence/
    SqliteConnectionFactory.cs              # IDbConnectionFactory 实现
    SqliteSchemaInitializer.cs              # ISchemaInitializer 实现（嵌入资源 SQL + __schema_version）
    SchemaInitializerHostedService.cs       # IHostedService → 启动时调 InitializeAsync
    Migrations/
      0001_init_schema_version.sql          # 第一条迁移：创建 __schema_version 表本身
  Hosting/
    AppDataPathProvider.cs                  # IAppDataPathProvider 实现
  Options/
    SqliteOptions.cs                        # 可选 override（DatabasePath 显式设值）
```

`SqliteConnectionFactory`：

- 注入 `IAppDataPathProvider` 与 `IOptions<SqliteOptions>`。
- `OpenAsync` 拼接 connection string：`Data Source={path};Foreign Keys=True;Cache=Shared`，然后 `await connection.OpenAsync(cancellationToken)` 后返回。
- `SqliteOptions.DatabasePath` 非空时直接使用该值（用于测试覆盖）；为空时调 `IAppDataPathProvider.GetDatabasePath()`。
- 启用 WAL：在 `OpenAsync` 内首次连接执行 `PRAGMA journal_mode=WAL;`（幂等）。

`SqliteSchemaInitializer`：

- 注入 `IDbConnectionFactory`、`ILogger<SqliteSchemaInitializer>`。
- 启动序列：① 打开连接；② 创建 `__schema_version` 表（如不存在）；③ 读取已应用 `version` 集合；④ 扫描嵌入资源 `Persistence.Migrations.*.sql`；⑤ 按文件名前缀 `NNNN` 排序；⑥ 跳过已应用的；⑦ 未应用的逐条单事务执行，写入 `__schema_version`。
- 失败时记日志 + 抛异常；让 IHostedService 把启动 fail-fast。

`SchemaInitializerHostedService`：

- 注入 `IServiceScopeFactory`（因为 `ISchemaInitializer` 依赖 `IDbConnectionFactory`，后者是 Scoped）。
- `StartAsync(CancellationToken)` 中创建 scope → 解析 `ISchemaInitializer` → 调 `InitializeAsync`。
- `StopAsync` 空实现。

`AppDataPathProvider`：

- 跨平台：
  - **Windows**：`Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` 拼 `dawning-agent-os/agentos.db`。
  - **macOS**：`Environment.GetFolderPath(SpecialFolder.ApplicationData)`（即 `~/Library/Application Support`）拼 `dawning-agent-os/agentos.db`。
  - **Linux**：优先 `Environment.GetEnvironmentVariable("XDG_DATA_HOME")`；否则 fallback `~/.local/share`，拼 `dawning-agent-os/agentos.db`。
- 构造时 `Directory.CreateDirectory(...)` 确保目录存在；幂等。

### 3. 迁移文件约定

- 文件命名 `NNNN_<snake_case_description>.sql`，`NNNN` 是 4 位零填充单调递增整数；从 `0001` 开始。
- 单文件以单事务包裹（`SqliteSchemaInitializer` 在执行时 `BEGIN; ... COMMIT;`）。
- 文件以 `<EmbeddedResource Include="Persistence/Migrations/*.sql" />` 嵌入到 `Dawning.AgentOS.Infrastructure.dll`。
- 资源逻辑名格式：`Dawning.AgentOS.Infrastructure.Persistence.Migrations.NNNN_*.sql`（按 csproj 默认 root namespace 推算）。

第一条迁移 `0001_init_schema_version.sql`：

```sql
CREATE TABLE IF NOT EXISTS __schema_version (
    version    INTEGER NOT NULL PRIMARY KEY,
    applied_at TEXT    NOT NULL
);
```

后续迁移由 ADR-014 第一切片（inbox / Memory）的实现切片承担，本 ADR 不预生成。

### 4. RuntimeStatus 扩展

`Dawning.AgentOS.Application.Runtime.RuntimeStatus` record 新增 `DatabaseStatus Database` 子记录：

```csharp
public sealed record DatabaseStatus(
    bool Ready,
    long? SchemaVersion,
    string? FilePath);
```

`RuntimeStatus` record 形态变为：

```csharp
public sealed record RuntimeStatus(
    string Status,
    TimeSpan Uptime,
    DatabaseStatus Database);
```

`RuntimeAppService`：

- 新增构造函数依赖 `IDbConnectionFactory + IAppDataPathProvider`。
- `GetStatusAsync` 中：
  - 调 `IAppDataPathProvider.GetDatabasePath()` 获取 `FilePath`。
  - 打开连接，读 `SELECT MAX(version) FROM __schema_version`；成功 → `Ready=true`、`SchemaVersion=结果`；异常 → `Ready=false`、`SchemaVersion=null`、把异常 message 写日志但**不让** endpoint 报 500（health endpoint 必须始终 200）。
- 不让数据库不可用阻断 health 报告，是产品级决定：桌面壳需要看到"db not ready" 信号才能展示"数据库初始化中..." 这类 UI 状态，而不是 500 一刀切。

### 5. DI 注册扩展

`InfrastructureServiceCollectionExtensions.AddInfrastructure()` 在现有注册之后追加：

```csharp
services.AddSingleton<IAppDataPathProvider, AppDataPathProvider>();
services.AddOptions<SqliteOptions>();
services.AddScoped<IDbConnectionFactory, SqliteConnectionFactory>();
services.AddScoped<ISchemaInitializer, SqliteSchemaInitializer>();
services.AddHostedService<SchemaInitializerHostedService>();
```

`Program.cs` 不变（仍然只调 `AddApplication() → AddInfrastructure() → AddApi()`）；持久化对 Api 层透明。

### 6. 测试形态

新增项目 `tests/Dawning.AgentOS.Infrastructure.Tests/`（如已存在则在其中补用例）：

- `SqliteSchemaInitializerTests`：
  - `InitializeAsync_AppliesSeedMigration_OnFirstRun` —— 首次跑写入 `version=1`。
  - `InitializeAsync_IsIdempotent_OnSecondRun` —— 二次跑不再写入新行、不抛错。
  - `InitializeAsync_WritesAppliedAtAsIso8601Utc` —— 写入 `applied_at` 为 ISO-8601 UTC 字符串。
- `SqliteConnectionFactoryTests`：
  - `OpenAsync_OpensSqliteConnection_WithForeignKeysEnabled` —— 验证 `PRAGMA foreign_keys` 返回 `1`。
  - `OpenAsync_UsesSqliteOptionsDatabasePath_WhenSet` —— Options 覆盖路径生效。
  - `OpenAsync_FallsBackToAppDataPathProvider_WhenOptionsEmpty` —— Mock `IAppDataPathProvider` 返回固定值。

`Dawning.AgentOS.Api.Tests` 中扩展：

- `RuntimeEndpointsTests.GetStatus_ReturnsDatabaseReadyTrue_WhenSchemaInitialized` —— 验证 `/api/runtime/status` 返回 `database.ready=true` 且 `database.schemaVersion>=1`。
- `DawningAgentOsApiFactory` 重写 `ConfigureWebHost`：注入 `SqliteOptions { DatabasePath = "Data Source=test_<guid>;Mode=Memory;Cache=Shared" }`，让测试在 in-memory 共享缓存下运行。

`Dawning.AgentOS.Application.Tests` 中扩展：

- `RuntimeAppServiceTests.GetStatusAsync_ReturnsDatabaseNotReady_WhenConnectionThrows` —— Mock `IDbConnectionFactory.OpenAsync` 抛异常，验证 endpoint 仍返回 200 + `Ready=false`。

### 7. 架构测试新增（K1）

在 `tests/Dawning.AgentOS.Architecture.Tests/LayeringTests.cs` 新增：

- `Application_DoesNotReferencePersistencePackages` —— 通过 `Assembly.GetReferencedAssemblies()` 断言 `Dawning.AgentOS.Application` 不直接引用 `Microsoft.Data.Sqlite` / `Dapper` / `Dawning.ORM.Dapper`。
- `Domain_AndDomainServices_DoNotReferencePersistencePackages` —— 同上，断言两个 Domain 装配。
- `IDbConnectionFactory_IsInApplicationAbstractionsPersistenceNamespace` —— 用 NetArchTest 断言 `IDbConnectionFactory` / `ISchemaInitializer` 类型必须在 `Dawning.AgentOS.Application.Abstractions.Persistence` 命名空间，`IAppDataPathProvider` 必须在 `Dawning.AgentOS.Application.Abstractions.Hosting`。

锚点选择遵循 [ADR-020](architecture-test-assertion-strategy.md)：使用 `typeof(global::Dawning.AgentOS.Application.Abstractions.Persistence.IDbConnectionFactory).Assembly`，不依赖 magic string。

### 8. 不在范围内

本 ADR 明确**不**承诺以下内容；它们由后续 ADR 与切片实施承担：

- 任何 Aggregate / Aggregate Repository / `IUnitOfWork` 实现（推迟到第一个真实 Aggregate 出现，与 ADR-018 §IUnitOfWork 形态联动复议）。
- 数据库连接池调优、并发写策略。
- 多设备 / 云同步、备份恢复。
- 静态加密（SQLCipher / SEE）。
- 任何业务表（inbox / Memory Ledger / interest weights / operation log）的 schema。
- 数据库回滚 / down migration（V0 不支持回滚；破坏性变更通过 forward-only 迁移处理）。
- LLM provider 相关持久化（API key 加密存储、调用日志）。

### 9. 影响

**正向影响**：

- [ADR-017](engineering-skeleton-v0.md) 中"完成 SQLite/Dapper 验证" 的 V0 通电 gate 解锁，可以进入 ADR-014 第一切片。
- 端口（A1）+ 平台路径（B1）+ 嵌入资源迁移（C1）+ IHostedService 触发（D1）+ per-call 连接（E1）的组合，每一项都对应一个未来的扩展点（云同步 → 替 `IAppDataPathProvider`、加密 → 替 `IDbConnectionFactory` 包装、迁移历史查询 → 直接读 `__schema_version`），不预先实现但保留位置。
- F1 + G1 推迟 `IUnitOfWork` / Repository 到真实 Aggregate 出现，避免空壳代码反复重写；此项决策与 [ADR-022](no-mediator-self-domain-event-dispatcher.md) §10 未尽事宜中"事务策略"待办联动，由第一个 Aggregate 实施切片同时落地。
- H1 让"数据库就绪"成为 `RuntimeStatus` 的一等公民，桌面壳在启动屏可以观察到该信号，前端契约稳定。
- K1 三条架构断言把 persistence 边界在新增时即建立 baseline，与 [ADR-020](architecture-test-assertion-strategy.md) 一致。

**代价 / 风险**：

- `RuntimeStatus` 形态变化（新增 `DatabaseStatus`）会让前端 typed client 需要同步更新；V0 阶段桌面壳未消费 status，影响为零，但本 ADR 接受未来类似破坏性变更的可能。
- `SqliteSchemaInitializer` 的"扫描嵌入资源 + 排序 + 单事务执行" 共约 50 行代码；该路径若有 bug，会让所有启动失败。通过 `SqliteSchemaInitializerTests` 三条测试覆盖核心路径，但仍有 corner case（嵌入资源命名约定被破坏、迁移文件 BOM 头）。
- IHostedService 同步迁移让冷启动多 ~50ms（首次跑一次空表创建）；后续启动只读不写，约 ~5ms。可接受。
- `Microsoft.Data.Sqlite` 与 `Dawning.ORM.Dapper` 的版本对齐由 `Directory.Build.props` / `dotnet list package` 兜底；若 SDK 升级让最低 .NET 版本变化，须同步本 ADR 的复议。
- WAL 模式在某些 NTFS / Windows 杀毒软件下会让 `.db-wal` 临时文件被误判；本 ADR 接受此风险，复议条件已写入"或 SQLite 单文件无法承载"。
- `__schema_version` 表名以 `__` 双下划线前缀显式标记"系统表"。该约定仅适用于本项目内部；未来引入备份 / 同步工具链时需要把此表加入排除列表。

## 复议触发条件

- V0 进入第一个 Aggregate Repository 实现：触发 `IUnitOfWork` 形态决策（[ADR-018](backend-architecture-equinox-reference.md) 中"胖入口"具体落地）+ 事务边界策略（[ADR-022](no-mediator-self-domain-event-dispatcher.md) §10 未尽事宜）+ Repository 命名后缀自动注册的复议。
- 迁移 SQL 数量 ≥ 10 / 出现需要回滚的破坏性 schema 变更：触发 C1（嵌入资源 SQL）→ C2（DbUp / FluentMigrator）的整体复议。
- 多设备同步 / 云后端进入项目：触发 B1（平台特定 app-data 路径）整体复议；`IAppDataPathProvider` 可能需要扩展为"主存储 + 同步快照" 双路径。
- `Microsoft.Data.Sqlite` / `Dawning.ORM.Dapper` 与目标 .NET 主版本不再兼容：触发整个持久化栈复议。
- SQLite 单文件无法承载（>1GB / 高并发写入冲突 / WAL 异常）：触发是否换 LiteDB / DuckDB / 服务端数据库的复议。
- 隐私要求需要静态加密（如桌面壳合规审计）：触发 SQLCipher / SEE 引入决策；`SqliteConnectionFactory` 形态需要相应扩展。
- ADR-018 中 `IUnitOfWork` 胖入口形态被整体复议：本 ADR 中"F1 不前置 UoW"决策同步复议。

## 相关页面

- [ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端](mvp-desktop-stack-electron-aspnetcore.md)：上游决策；本 ADR 是其 SQLite + Dapper + `Dawning.ORM.Dapper` 选项的落地形态。
- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：本 ADR 解锁其"完成 SQLite/Dapper 验证" gate。
- [ADR-018 后端架构参考 Equinox：DDD + MediatR + Result 模式](backend-architecture-equinox-reference.md)：本 ADR 显式推迟其 `IUnitOfWork` 胖入口落地，由第一个 Aggregate Repository 切片承担。
- [ADR-022 去 MediatR：自研领域事件分发器与 AppService 立面](no-mediator-self-domain-event-dispatcher.md)：本 ADR 处理 §10 未尽事宜中"事务策略 / Repository 注册"的前置准备（端口建立 + 推迟实现）。
- [ADR-023 Api 入口立面：AppService 接入与 V0 端点形态](api-entry-facade-and-v0-endpoints.md)：本 ADR 不修改 Api 层契约，只扩展 `RuntimeStatus` DTO 形态。
- [ADR-020 架构测试断言策略](architecture-test-assertion-strategy.md)：本 ADR 新增的三条 persistence 边界断言遵循其类型级 + 锚点反射纪律。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 是 S5 持久化骨架落地前的方案确认产物。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
