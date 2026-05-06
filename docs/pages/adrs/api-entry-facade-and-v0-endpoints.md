---
title: ADR-023 Api 入口立面：AppService 接入与 V0 端点形态
type: adr
subtype: architecture
canonical: true
summary: Api 层以 Minimal API 端点组形式消费 Application AppService 立面；V0 用 RuntimeAppService 当 health；不引入 ApiResult 包装、Swagger、MapHealthChecks 与 API 版本控制；AppService 自动注册由 Application 层 AddApplication 扩展承担。
tags: [agent, security]
sources: []
created: 2026-05-01
updated: 2026-05-01
verified_at: 2026-05-01
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/engineering-skeleton-v0.md, pages/adrs/backend-architecture-equinox-reference.md, pages/adrs/no-mediator-self-domain-event-dispatcher.md, pages/adrs/architecture-test-assertion-strategy.md, pages/adrs/testing-stack-nunit-v0.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-01
adr_revisit_when: "出现需要对外暴露的公共 HTTP API（非本地 Electron 消费）；或后端要被多个非桌面客户端消费导致需要 OpenAPI / API 版本控制；或 endpoint 数量超过 30 让 Minimal API 文件组织失控；或需要 ASP.NET Core 内置 IHealthCheck 协议（如 Kubernetes liveness / readiness）；或 ProblemDetails 不能承载新出现的错误维度（如多语言错误、字段级错误集合）。"
---

# ADR-023 Api 入口立面：AppService 接入与 V0 端点形态

> Api 层以 Minimal API 端点组形式消费 Application AppService 立面；V0 用 RuntimeAppService 当 health；不引入 ApiResult 包装、Swagger、MapHealthChecks 与 API 版本控制；AppService 自动注册由 Application 层 AddApplication 扩展承担。

## 背景

[ADR-017](engineering-skeleton-v0.md) 已确定 V0 工程骨架包含 `Dawning.AgentOS.Api/` 项目，以 ASP.NET Core 本地 API 宿主形态承载 health endpoint 与 startup token middleware；其目录约束（`Endpoints/Middleware/Options/`）、`Program.cs` 不堆注册细节、调用集中 DI 扩展等已经写死。[ADR-018](backend-architecture-equinox-reference.md) 在 ADR-017 之上参考 Equinox v1.10 写了"Services.Api 末端通过 Result<T>.ToHttpResult() 扩展统一映射为 ProblemDetails"以及 Mediator / Pipeline Behaviors 心智。[ADR-022](no-mediator-self-domain-event-dispatcher.md) 已经把 Mediator / Pipeline Behaviors 整块推翻，让 Application 层改成 OO AppService 立面（`IXxxAppService` 接口 + `Services/XxxAppService` 实现）。

S3 切片落地后 Application 层已经按 ADR-022 形态就绪：`IRuntimeAppService.GetStatusAsync()` 返回 `Result<RuntimeStatus>`，背后是 `RuntimeAppService` 注入 `IClock + IRuntimeStartTimeProvider`。但 Api 层项目本身还未创建：`src/Dawning.AgentOS.Api/` 不存在，`tests/Dawning.AgentOS.Api.Tests/` 也不存在。

进入 S4 创建 Api 之前需要回答的工程问题：

- 端点风格用 Minimal API 还是 Controller？ADR-017 写了"Minimal API endpoint groups"但没钉死 endpoint 文件组织方式。
- 端点和 AppService 立面如何对位？1:1 文件夹？1:N 文件？
- 响应壳要 `ApiResult<T>` 包装还是直接 DTO + `ProblemDetails`？ADR-018 提到 ProblemDetails 但没排除包装层。
- `Result<T>` 到 HTTP 的映射规则放在哪一层、用什么形态？
- AppService 自动注册（ADR-022 §10 未尽事宜之一）由谁做？放 Application 层还是 Api 层？
- V0 是否引入 Swagger / OpenAPI、`MapHealthChecks` / `IHealthCheck`、API 版本控制？
- middleware 顺序（异常处理、startup token、routing、endpoints）？
- Api 项目命名是 `Dawning.AgentOS.Api`（ADR-017）还是 `Dawning.AgentOS.Services.Api`（ADR-018 沿用 Equinox 的命名）？两份 ADR 文本不一致需要消歧。
- Api 层架构测试断言要扩展到哪些维度？

如果让 S4 落地代码"边写边定"，会重蹈 ADR-021 → ADR-022 的覆辙：先实现再补 ADR，期间方案漂移。

本页是 Api 入口立面的方案确认页。它在 ADR-017 / ADR-018 / ADR-022 既有契约之上，把 Api 层的形态钉死，作为 S4 实施的依据；按 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)，代码落地仍须在本 ADR 接受后才进行。

## 备选方案

端点风格：

- **A1** Minimal API + `Endpoints/<Feature>/<Feature>Endpoints.cs` 静态类 + `MapXxx(IEndpointRouteBuilder)` 扩展方法。
- **A2** ASP.NET Core Controller + `[ApiController]` + Action attribute routing。
- **A3** Minimal API 但所有 endpoint 直接写在 `Program.cs`。

响应壳：

- **B1** 不引入包装，成功返回 DTO，失败转 `ProblemDetails`（RFC 7807）。
- **B2** 自定义 `ApiResult<T> { bool Success; string Code; string? Message; T? Data; long Timestamp; string? TraceId }` 包装所有响应。
- **B3** 成功返回 DTO，失败也返回 `ApiResult<T>`（混合形态）。

`Result<T>` → HTTP 映射：

- **C1** `IResult` 扩展 `result.ToHttpResult()` 在 Api 项目内提供，endpoint 显式调用。
- **C2** 全局 endpoint filter 自动检测 `Result<T>` 返回并映射。
- **C3** Application 层直接返回 `IResult`（让 Application 感知 HTTP）。

AppService 自动注册：

- **D1** Application 层 `AddApplication()` 扩展扫描 `Application.Services` 命名空间下 `XxxAppService` 类，按 `IXxxAppService` 注册 Scoped。
- **D2** Api 层 `AddApi()` 扩展扫描 Application 程序集做注册。
- **D3** Api 项目 `Program.cs` 显式列出所有 `services.AddScoped<IRuntimeAppService, RuntimeAppService>()`。

OpenAPI / Swagger：

- **E1** V0 不引入。
- **E2** V0 引入 Swashbuckle.AspNetCore 但只在 Development 环境暴露 UI。
- **E3** V0 引入 .NET 9 内置 `Microsoft.AspNetCore.OpenApi`。

Health endpoint：

- **F1** 用 `IRuntimeAppService.GetStatusAsync()` 当 health；映射到 `/api/runtime/status`。
- **F2** 引入 ASP.NET Core 内置 `MapHealthChecks("/health")` + 自定义 `IHealthCheck`。
- **F3** 同时提供 F1 + F2（`/api/runtime/status` 业务状态、`/health` K8s 协议）。

API 版本控制：

- **G1** V0 不引入；URL 不带 `/v1/` 前缀。
- **G2** 引入 `Asp.Versioning.Http` 包，URL 段或 header 携带版本。

middleware 顺序：

- **H1** `UseExceptionHandler(ProblemDetails)` → startup token → routing → endpoints。
- **H2** routing → startup token → endpoints → 异常 filter。
- **H3** 自定义全局异常 middleware 在最外层 + token middleware 在 routing 之前。

项目命名（Api csproj 名称）：

- **I1** `Dawning.AgentOS.Api`（与 ADR-017 一致）。
- **I2** `Dawning.AgentOS.Services.Api`（沿用 Equinox v1.10 命名，与 ADR-018 文本一致）。

测试形态：

- **J1** `Dawning.AgentOS.Api.Tests` 项目，NUnit + `Microsoft.AspNetCore.Mvc.Testing` 的 `WebApplicationFactory`，做 in-memory 集成测试。
- **J2** 不写 Api 层独立测试，依赖 Application.Tests + Architecture.Tests 间接覆盖。

架构测试新增：

- **K1** 新增三条断言：① `Api_DoesNotReferenceDomainOrInfrastructureDirectly`、② `Api_EndpointsNamespace_OnlyContainsStaticClasses`、③ `Api_DoesNotReferencePersistenceOrORMPackages`。
- **K2** 不新增，沿用现有 LayeringTests。

## 被否决方案与理由

**A2 Controller**：

- ADR-017 已定 Minimal API。
- 本地 API 给同仓库 Electron 消费，不需要 ApiController + 模型绑定 + filter pipeline 的全套 MVC 心智。
- Minimal API 的端点组扩展方法形态比 Controller 更轻、更易做"1 个 AppService = 1 个 Endpoints 文件"的视觉对位。

**A3 全部写在 Program.cs**：

- 第一个端点这样写没问题，第三个开始 `Program.cs` 就开始臃肿。
- 与 ADR-017 中"`Program.cs` 不堆注册细节"的精神冲突，应同样避免堆 endpoint 细节。

**B2 / B3 ApiResult 包装**：

- 包装层适用于"跨团队 SDK / 公共 API / 多客户端"场景；本项目 V0 是本地 API 给同仓库 Electron 消费，typed client 直接看 DTO 类型是最简的契约。
- 失败用 `ApiResult` 还要前端做 `if (result.Success)` 兜一层；改用 `ProblemDetails`，前端 axios / fetch 在非 2xx 上直接走 catch 分支，更顺手。
- 后续真要包装也容易加：在 endpoint filter 或 response writer 里包一层即可，迁移成本低于现在就引入。
- `ProblemDetails` 是 ASP.NET Core 一等公民，框架自带异常 → ProblemDetails 转换、`Results.Problem(...)` API、客户端工具链支持。

**C2 全局 endpoint filter 自动映射 Result\<T\>**：

- 写起来魔法，调试时第一反应找不到映射点；显式 `result.ToHttpResult()` 一目了然。
- filter 顺序 / 短路语义会把 endpoint 控制权藏到框架里，可读性下降。
- V0 endpoint 数量极少，"每个 endpoint 显式调一次扩展方法"的重复代价低于"引入一层魔法"。

**C3 Application 直接返回 IResult**：

- 让 Application 感知 HTTP，违反 Clean Architecture 边界，与 ADR-022 把 Application 设计成"纯 use case 编排"的方向冲突。
- Application.Tests 会被迫引用 ASP.NET Core，污染测试依赖图。

**D2 Api 层做 AppService 注册**：

- Application 才知道自己的 Services 长什么样；Api 来扫 Application 程序集是反向依赖。
- Api 项目升级或被替换（如未来跑 grpc 而非 HTTP）时，AppService 注册逻辑会被一起带走，得重做。

**D3 Program.cs 显式列出每个 AppService**：

- 机械工作量随业务模块线性增长，与 ADR-018 已经确立的"按命名后缀自动注册"基线冲突。
- 漏注册只能在运行时抛 DI 异常，体验差。

**E2 / E3 V0 引入 Swagger / OpenAPI**：

- 本地 API 给同仓库前端消费，前端可以维护一个手写 / 生成的 typed client；V0 不存在多消费者。
- OpenAPI 文档的真实价值在跨团队 / 公开发布场景，V0 没有。
- 引入会带新 NuGet 依赖、新启动时间、新 Development / Production 分支逻辑，性价比低。
- 一旦未来需要对外暴露，再引入即可，迁移成本主要是补 attribute / `WithOpenApi()`，不会被本 ADR 卡死。

**F2 / F3 MapHealthChecks + IHealthCheck**：

- `IHealthCheck` 协议是为 K8s liveness / readiness probe、负载均衡器健康路由这类外部消费方设计的；V0 桌面 App 没有这种消费者。
- `IRuntimeAppService.GetStatusAsync()` 已经存在并返回业务级状态（uptime、组件状态可扩展），完全够 V0 用。
- 同时提供两套（F3）会让"哪个是真正的 health"含混，产生维护两套代码 / 两套断言的代价。
- 复议触发条件已写入 front matter：未来真接入 K8s 等外部 probe 时再引入。

**G2 API 版本控制**：

- V0 只有一个版本、一个消费方。引入版本控制是为还没出现的问题做工程。
- URL 段 `/v1/` 前缀容易吸引未来"为了 v2 重写一次旧 endpoint"的反模式；版本控制应在真需要时再引入。

**H2 / H3 中间件顺序变体**：

- H2 把异常 filter 放最里层会漏掉 routing 阶段的异常（极端情况下 endpoint 路径解析失败的 500）。
- H3 引入自定义 middleware 而非 `UseExceptionHandler` 会丢失框架自带的 ProblemDetails 序列化、`StatusCodePages` 集成等便利。
- H1 是 .NET 8/9/10 官方文档推荐顺序，无需偏离。

**I2 项目命名 Services.Api**：

- ADR-017（更早决策）已经使用 `Dawning.AgentOS.Api`，且现有已落地的命名空间约定（如 `tests/Dawning.AgentOS.Api.Tests` 假定的）也指向 `Dawning.AgentOS.Api`。
- ADR-018 文本里的 `Services.Api` 是直接照搬 Equinox v1.10 命名，没有"为什么本项目需要 Services 前缀"的论证；本项目没有"多个非 HTTP 服务进程"，前缀冗余。
- 选 I2 反而要修 ADR-017 的项目划分树。

**J2 不写 Api 测试**：

- ADR-017 验证清单要求"未携带 startup token 的本地请求被拒绝"，没有 Api 测试就要靠手测，不可重复。
- `WebApplicationFactory` 是 .NET 推荐的轻量集成测试形态，启动 in-memory host，与单元测试同跑速度可接受。

**K2 不新增架构断言**：

- Api 项目刚创建是建立断言 baseline 的最佳时机；让代码先漂移再补断言会失败成本更高（与 ADR-020 架构测试断言策略一致）。

## 决策

采用：A1 + B1 + C1 + D1 + E1 + F1 + G1 + H1 + I1 + J1 + K1。

### 1. 项目命名与目录

Api 项目命名为 `Dawning.AgentOS.Api`（与 ADR-017 一致，纠正 ADR-018 中 `Services.Api` 的命名漂移）。

目录结构：

```text
src/Dawning.AgentOS.Api/
  Endpoints/
    Runtime/
      RuntimeEndpoints.cs              # static class，提供 MapRuntimeEndpoints 扩展
  Middleware/
    StartupTokenMiddleware.cs          # 校验 startup token；S4 阶段实现
  Options/
    ApiHostOptions.cs                  # 端口、token 等启动参数
    StartupTokenOptions.cs
  Results/
    ResultHttpExtensions.cs            # Result<T>.ToHttpResult() 扩展
  DependencyInjection/
    ApiServiceCollectionExtensions.cs  # AddApi() 扩展（middleware / options 注册）
  Program.cs                           # composition root：AddApplication → AddInfrastructure → AddApi → MapXxxEndpoints

tests/Dawning.AgentOS.Api.Tests/
  Endpoints/
    Runtime/
      RuntimeEndpointsTests.cs         # WebApplicationFactory 集成测试
  Helpers/
    DawningAgentOsApiFactory.cs        # WebApplicationFactory<Program>
```

`Endpoints/` 目录与 Application `Interfaces/` 目录形成 1:1 映射（每个 `IXxxAppService` 对应一个 `Endpoints/<Feature>/<Feature>Endpoints.cs`）。

### 2. Endpoint 风格

- 采用 Minimal API；endpoint 组以静态扩展方法形式存在：

```csharp
public static class RuntimeEndpoints
{
    public static IEndpointRouteBuilder MapRuntimeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/runtime");

        group.MapGet("/status", async (
            IRuntimeAppService appService,
            CancellationToken cancellationToken) =>
        {
            var result = await appService.GetStatusAsync(cancellationToken);
            return result.ToHttpResult();
        });

        return routes;
    }
}
```

- `Program.cs` 只调用：`app.MapRuntimeEndpoints();`，不写具体 endpoint。
- URL 形态为小写 kebab-case：`/api/<feature>/<action-or-resource>`；不引入 `/v1/` 前缀。

### 3. 响应壳：直接 DTO + ProblemDetails

- 成功路径：endpoint 返回 `Results.Ok(value)` 等同于直接序列化 DTO。
- 失败路径：endpoint 返回 `Results.Problem(...)`，框架按 RFC 7807 序列化为 `application/problem+json`。
- Application 层不感知 HTTP；任何 HTTP 状态映射只发生在 Api 层 endpoint 边界。

### 4. `Result<T>` → HTTP 映射

`Api/Results/ResultHttpExtensions.cs` 提供：

```csharp
public static IResult ToHttpResult<T>(this Result<T> result)
{
    if (result.IsSuccess) return Results.Ok(result.Value);

    var errors = result.Errors;
    var hasFieldError = errors.Any(e => e.Field is not null);
    var statusCode = hasFieldError ? 400 : 422;

    return Results.Problem(
        statusCode: statusCode,
        title: hasFieldError ? "Validation failed" : "Business rule violation",
        detail: errors[0].Message,
        extensions: new Dictionary<string, object?>
        {
            ["errors"] = errors.Select(e => new { e.Code, e.Message, e.Field }),
        });
}
```

映射规则：

- `Success` → `200 OK` + DTO。
- `Failure`，错误集合中存在 `Field is not null` → `400 Bad Request` + ProblemDetails（字段级校验失败）。
- `Failure`，错误均无 `Field` → `422 Unprocessable Entity` + ProblemDetails（业务规则违反）。
- `404 Not Found` 由 endpoint 内部根据 `Result.Errors[0].Code == "NotFound"` 等业务约定显式返回（V0 暂无 not-found use case，遇到时再细化规则）。

### 5. AppService 自动注册：归属 Application 层

- Application 项目提供 `AddApplication(this IServiceCollection services)` 扩展，位于 `Dawning.AgentOS.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`。
- 实现采用反射扫描 `typeof(IRuntimeAppService).Assembly`：所有 `Dawning.AgentOS.Application.Services` 命名空间下的具体类，若实现了任一 `Dawning.AgentOS.Application.Interfaces.I*AppService` 接口，则按 `interface → impl` 注册为 Scoped。
- 不引入 Scrutor 等三方扫描库；V0 用十几行手写反射即可。
- 为了能定义 `IServiceCollection` 扩展方法，Application 项目获得**单一窄依赖** `Microsoft.Extensions.DependencyInjection.Abstractions`（abstractions-only 包，包含 `IServiceCollection` / `ServiceLifetime` / `ServiceDescriptor`，不含任何容器实现）。该例外**只允许 Application 层**，Domain.Core / Domain / Domain.Services 仍然零外部依赖；具体容器实现 `Microsoft.Extensions.DependencyInjection` 仍被 Application 层架构测试明确禁止。架构测试 §10 第三条断言会在禁止列表中保留 `Microsoft.Extensions.DependencyInjection`，但放行 `Microsoft.Extensions.DependencyInjection.Abstractions`。
- Api 项目不知道 AppService 的存在；`Program.cs` 只调 `services.AddApplication()`。
- 这一项同时落地 ADR-022 §10 未尽事宜的"AppService 自动注册"待办。

### 6. DI 组合（Program.cs）

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()        // Application 层：AppService 自动注册 + 应用层 options
    .AddInfrastructure()     // Infrastructure 层：S4 落地，含 IClock / IRuntimeStartTimeProvider / IDomainEventDispatcher 实现
    .AddApi(builder.Configuration); // Api 层：middleware、ProblemDetails 配置、ApiHostOptions

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<StartupTokenMiddleware>();
app.UseRouting();

app.MapRuntimeEndpoints();

app.Run();
```

调用顺序固定为：`AddApplication() → AddInfrastructure() → AddApi()`。

### 7. OpenAPI / Swagger / API 版本控制 / IHealthCheck：V0 全部不引入

- 不引入 Swashbuckle / Microsoft.AspNetCore.OpenApi。
- 不引入 Asp.Versioning.Http。
- 不引入 `MapHealthChecks` / `IHealthCheck` 协议；ADR-017 要求的 health endpoint 由 `GET /api/runtime/status`（背后是 `IRuntimeAppService.GetStatusAsync`）承担。
- 复议触发条件已写入 front matter。

### 8. Middleware 顺序

```text
UseExceptionHandler(ProblemDetails)   # 异常兜底 → ProblemDetails
UseStatusCodePages                    # 4xx / 5xx 默认 ProblemDetails 化
UseMiddleware<StartupTokenMiddleware>  # 缺 token / 错 token → 401
UseRouting
[endpoints]
```

`StartupTokenMiddleware` 在 routing 之前拒绝未授权请求，避免 endpoint 在未鉴权状态下执行任何代码。

### 9. 测试

新增 `tests/Dawning.AgentOS.Api.Tests/` 项目：

- 引用 `Dawning.AgentOS.Api`、`Microsoft.AspNetCore.Mvc.Testing`、NUnit、Moq。
- 提供 `DawningAgentOsApiFactory : WebApplicationFactory<Program>`；override `ConfigureWebHost` 替换 `IClock` / `IRuntimeStartTimeProvider` 为测试 fake，避免对真实时间敏感。
- V0 测试用例：
  - `GetStatus_ReturnsOkWithRuntimeStatusDto_WhenTokenIsValid`
  - `GetStatus_Returns401_WhenTokenIsMissing`
  - `GetStatus_Returns401_WhenTokenIsInvalid`
- `Program.cs` 中加入 `public partial class Program;` 以让 `WebApplicationFactory<Program>` 的反射查找成功。

### 10. 架构测试新增

在 `tests/Dawning.AgentOS.Architecture.Tests/LayeringTests.cs` 新增三条 Api 层断言并调整一条 Application 层断言：

- `Api_DoesNotReferenceDomainProjectsDirectly`：通过 `Assembly.GetReferencedAssemblies()` 断言 `Dawning.AgentOS.Api` 不直接引用 `Dawning.AgentOS.Domain` / `Dawning.AgentOS.Domain.Services`。Api 作为 composition root 允许引用 `Dawning.AgentOS.Application`（消费 AppService 立面）与 `Dawning.AgentOS.Infrastructure`（在 `Program.cs` 调 `AddInfrastructure()` 接入）；同时允许引用 `Dawning.AgentOS.Domain.Core` —— 它是共享基元层（`Result<T>` / `DomainError` / `IDomainEvent`），`ResultHttpExtensions` 需要直接引用 `Result<T>` 才能完成映射，且 Application 的传递依赖图本就把 Domain.Core 拉进 Api 的引用清单，这是依据 §6 DI 组合的明确例外。
- `Api_EndpointsNamespace_OnlyContainsStaticClasses`：`Dawning.AgentOS.Api.Endpoints` 命名空间下所有类型必须是 `static`，借此守 endpoint-as-extension-method 形态。
- `Api_DoesNotReferencePersistenceOrORMPackages`：与 Application 层同样断言禁用 Dapper / Microsoft.Data.Sqlite / Dawning.ORM.Dapper 等 persistence 包。
- 调整 `Application_DoesNotReferenceFrameworkAdapterPackages`：从禁止列表中移除 `Microsoft.Extensions.DependencyInjection.Abstractions`（因 §5 `AddApplication` 扩展需要），但保留 `Microsoft.Extensions.DependencyInjection`（具体容器）以及其它框架包。
- 锚点选择遵循 [ADR-020](architecture-test-assertion-strategy.md)：使用 `typeof(global::Dawning.AgentOS.Api.Endpoints.Runtime.RuntimeEndpoints).Assembly`，不依赖 magic string。

## 影响

**正向影响**：

- Api 层的目录结构、端点风格、响应形态、错误映射、middleware 顺序、AppService 注册位置全部在写代码前确定，避免 S3 → S4 之间出现 ADR-021 → ADR-022 那种"实现完发现要推翻"的循环。
- "1 个 AppService = 1 个 Endpoints 文件 = 1 个 Endpoints 测试文件"形成可机械扩张的切片单位，第二刀业务（聊天 / inbox / Memory Ledger）只需复制这条模板。
- 不引入 ApiResult / Swagger / 版本控制 / IHealthCheck，意味着 V0 除 Microsoft.AspNetCore.Mvc.Testing（仅测试项目）之外不增加 NuGet 依赖；保持依赖图清爽。
- 项目命名歧义在 ADR 层面被消除：`Dawning.AgentOS.Api` 是唯一正确名称，ADR-018 中 `Services.Api` 命名漂移由本 ADR 显式纠正。
- AppService 自动注册落地后 ADR-022 §10 三项未尽事宜中已完成一项；剩余两项（DomainEventDispatcher 实现、事务策略）继续归 S4 完成。
- 架构测试新增三条 Api 层断言，与 ADR-020 类型级断言纪律一致，建立 Api 边界 baseline。

**代价 / 风险**：

- `ResultHttpExtensions` 的映射规则（"有 Field → 400，无 Field → 422"）是产品级约定；如果未来错误模型新增维度（如多语言、严重级别、字段集合），需要扩展或重写映射函数。本 ADR 接受此风险，留 `404` 等具体规则待真用例出现时细化。
- Minimal API 不像 Controller 那样自带 `[ProducesResponseType]` 文档化，未来真要 OpenAPI 时需要在 endpoint 末端补 `.Produces<RuntimeStatus>(200).ProducesProblem(401)` 这类描述符；迁移点已识别。
- `WebApplicationFactory<Program>` 要求 `Program.cs` 顶层语句配 `public partial class Program;`；这是 .NET 集成测试惯例，但容易在重构时漏改。
- AppService 自动注册基于命名约定（`I*AppService` ↔ `*AppService`）；约定一旦被破坏（例如出现 `IRuntimeReporter`，实现 `RuntimeAppService` 同时实现两个接口），自动注册行为可能不符合直觉。架构测试断言"Services 命名空间仅含具体类、Interfaces 命名空间仅含接口"在一定程度上守住，但不能完全防御。
- 不引入 Swagger 意味着前端 typed client 维护成本由开发者手动承担；V0 端点数 ≤ 5，可控；端点数膨胀后会进入复议条件。
- StartupTokenMiddleware 在 V0 阶段还未实现（ADR-017 验证清单要求），本 ADR 给出位置约定，但具体 token 校验逻辑、token 存储、刷新策略不在本 ADR 范围；S4 落地时仍可能触发小型补丁 ADR。

## 复议触发条件

- 出现需要对外暴露的公共 HTTP API（非本地 Electron 消费）：触发 `OpenAPI / Swagger / API 版本控制 / ApiResult 包装层` 决策的整体复议。
- 后端被多个非桌面客户端（CLI、移动端、第三方 SDK）消费：同上。
- Endpoint 数量 ≥ 30 时检查：① `Endpoints/<Feature>/<Feature>Endpoints.cs` 静态类形态是否仍可读、② `Program.cs` 中 `Map*Endpoints()` 列表是否过长。
- 需要 Kubernetes liveness / readiness probe 或外部负载均衡器健康路由：触发 `IHealthCheck / MapHealthChecks` 决策复议。
- `ProblemDetails` 不能承载新出现的错误维度（多语言错误、字段级错误集合、严重级别）：触发响应壳决策复议。
- ADR-022 因其它原因被整体 supersede：本 ADR 同步复议是否仍适用新的 Application 立面形态。
- ADR-017 项目划分树发生重大调整：本 ADR 中目录约束需同步检查。

## 相关页面

- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：上游决策；本 ADR 是其 `Dawning.AgentOS.Api` 项目的具体形态落实，并消歧 ADR-018 中 `Services.Api` 命名漂移。
- [ADR-018 后端架构参考 Equinox：DDD + MediatR + Result 模式](backend-architecture-equinox-reference.md)：本 ADR 沿用其 Result 模式，ProblemDetails 错误映射策略；其 Mediator / Pipeline 部分已被 ADR-022 整块 supersede，本 ADR 继承 ADR-022 后的状态。
- [ADR-020 架构测试断言策略](architecture-test-assertion-strategy.md)：本 ADR 新增的三条 Api 层架构断言遵循其类型级 + 锚点反射的纪律。
- [ADR-022 去 MediatR：自研领域事件分发器与 AppService 立面](no-mediator-self-domain-event-dispatcher.md)：本 ADR 是其 §10 未尽事宜中"AppService 自动注册"的完成方案；同时把 AppService 立面如何在 Api 层被消费定义清楚。
- [ADR-019 测试栈 NUnit V0](testing-stack-nunit-v0.md)：本 ADR 新增的 `Dawning.AgentOS.Api.Tests` 项目沿用 NUnit + Moq。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 是 S4 Api 层落地前的方案确认产物。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
