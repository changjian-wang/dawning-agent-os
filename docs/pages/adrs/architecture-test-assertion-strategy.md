---
title: ADR-020 架构测试断言策略：层级用 assembly 引用 + 类型级用 NetArchTest 到具体类型名
type: adr
subtype: tooling
canonical: true
summary: 架构测试采用双层断言：层依赖方向用 Assembly.GetReferencedAssemblies() 做精确名比对，类型禁用规则用 NetArchTest.Rules 但只到具体类型全名；不使用命名空间前缀字符串作为断言依据；正向"必须引用"断言推迟到该层有真实类型绑定后再补。
tags: [agent]
sources: []
created: 2026-04-30
updated: 2026-05-01
verified_at: 2026-05-01
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/engineering-skeleton-v0.md, pages/adrs/backend-architecture-equinox-reference.md, pages/adrs/testing-stack-nunit-v0.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-30
adr_revisit_when: "NetArchTest 升级修复前缀匹配语义；或正向 layering 断言（必须引用 X）成为常态需求；或出现非 .NET / 非托管引用方向需要断言时；或本项目改用 Roslyn analyzer 替代 NetArchTest 时。"
---

# ADR-020 架构测试断言策略：层级用 assembly 引用 + 类型级用 NetArchTest 到具体类型名

> 架构测试采用双层断言：层依赖方向用 `Assembly.GetReferencedAssemblies()` 做精确名比对，类型禁用规则用 NetArchTest.Rules 但只到具体类型全名；不使用命名空间前缀字符串作为断言依据；正向"必须引用"断言推迟到该层有真实类型绑定后再补。

## 背景

[ADR-018](backend-architecture-equinox-reference.md) 已锁定 NetArchTest.Rules v2.x 作为架构测试库，[ADR-019](testing-stack-nunit-v0.md) 已锁定 NUnit + 原生 `Assert.That`。S2（Domain / Domain.Services / Architecture.Tests scaffold）落地时发现两个写架构测试时绕不过去的语义陷阱，必须在第一刀就形成规范，否则后续每加一个新层都会重新踩。

### 陷阱一：NetArchTest 命名空间前缀匹配

`NetArchTest.Rules` 的 `HaveDependencyOn(name)` / `HaveDependencyOnAny(...)` 内部实现是对类型 `FullName` 做 `StartsWith(name, StringComparison.InvariantCultureIgnoreCase)`，**不**按命名空间段切分。这造成两个直接后果：

- `HaveDependencyOn("Dawning.AgentOS.Domain")` 会同时命中 `Dawning.AgentOS.Domain.Core.*`，因为后者是前者的字符串前缀。结果是 Domain.Core 的类型被错误判为"违反了不依赖 Domain"的规则。
- `HaveDependencyOn("MediatR")` 会同时命中 `MediatR.Contracts.INotification`。Domain.Core 合法依赖 `MediatR.Contracts.INotification`（[ADR-018](backend-architecture-equinox-reference.md) 已显式允许），却被判违规。

S2 第一次跑测试时就因为这两条同时踩雷，`DomainCore_DoesNotDependOnDomain_OrDomainServices` 与 `DomainCore_DoesNotReferenceMediatRMainPackage` 双双假阳性失败。

### 陷阱二：Roslyn 编译期 metadata reference pruning

.NET 编译器在写出 assembly metadata 时会裁掉**当前编译没有任何类型绑定的 ProjectReference**（即源码中没有任何对该 assembly 中类型的引用）。这是标准行为，不是 NetArchTest 的问题。后果：

- S2 阶段 `Dawning.AgentOS.Domain` 项目通过 `<ProjectReference>` 声明引用 `Dawning.AgentOS.Domain.Core`，但 Domain 内部尚无任何类型派生自 `Entity<TId>` / `AggregateRoot<TId>` 或返回 `Result<T>`。
- 此时 `Dawning.AgentOS.Domain.dll` 的 `GetReferencedAssemblies()` 看不到 `Dawning.AgentOS.Domain.Core`，"Domain 必须引用 Domain.Core"这种正向断言会产生**假阴性**（实际上声明了引用，但运行期看不见）。
- 一旦下一刀业务切片在 Domain 中引入第一个派生自 Domain.Core 基类的类型，引用立刻在元数据中重新出现。

如果不区分负向（禁止方向）与正向（必须引用）断言，前者稳定可信，后者会随业务切片进度时灵时不灵，给 CI 带来无法解释的红绿振荡。

### 不立 ADR 的代价

- 每个新层（S3 InboxItem 聚合、S3+ Application、Infra.Data 等）首次写架构测试时都会重新踩这两个坑。
- 不同 contributor 可能各自找出"恰好通过"的写法（比如把命名空间字符串补长、把 type 引用换成具体类名），但理由不写下来就无法跨次复用。
- 未来某次 NetArchTest 升级修了前缀语义、或某次编译器行为变化（如默认开启 reference assembly trimming），断言失败时无人能解释当年决策依据。

## 备选方案

- **方案 A**：架构测试全部用 NetArchTest，断言以命名空间字符串前缀表达层依赖方向。Equinox v1.10 蓝本默认写法。
- **方案 B**：架构测试全部用 `Assembly.GetReferencedAssemblies()` + 反射，不引入 NetArchTest，把 ADR-018 的 NetArchTest 选择回退。
- **方案 C**：双层断言：层依赖方向用 `Assembly.GetReferencedAssemblies()` 做精确程序集名比对（负向为主，正向暂缓），类型级禁用规则用 NetArchTest.Rules 但只到具体类型全名（如 `MediatR.IMediator` / `MediatR.IRequestHandler`），禁止使用命名空间前缀。
- **方案 D**：用 Roslyn analyzer 自定义诊断规则替代 NetArchTest。

## 被否决方案与理由

**方案 A 全部用 NetArchTest 命名空间前缀**：

- 直接踩陷阱一。`HaveDependencyOn("Dawning.AgentOS.Domain")` 误伤 Domain.Core；`HaveDependencyOn("MediatR")` 误伤 Domain.Core 合法依赖的 `MediatR.Contracts.INotification`。
- 解决方案是把字符串补到歧义之外（例如 `"Dawning.AgentOS.Domain."` 加点号），但这是约定靠人记忆，没有编译器或 analyzer 协助；新人无法发现。
- 命名空间前缀本质上不是层边界：`Dawning.AgentOS.Domain` 与 `Dawning.AgentOS.Domain.Core` 是两个独立 assembly，但共享前缀。把 assembly 边界用 namespace 字符串近似，是用错抽象。

**方案 B 全部用反射 + 回退 NetArchTest**：

- 反射断言 layering 表达力强，但断言"某层不能使用 IMediator 类型"这种类型级规则要手写 IL/metadata 扫描，工程量远高于 NetArchTest。
- 回退 NetArchTest 与 [ADR-018](backend-architecture-equinox-reference.md) 既定决策冲突，需先 supersede ADR-018 中相关章节，连锁影响过大。
- ADR-018 选 NetArchTest 的核心理由（DSL 表达力、社区维护、Equinox 蓝本对齐）在类型级断言场景下仍然成立，没必要全盘抛弃。

**方案 D Roslyn analyzer 自定义诊断**：

- 表达力最强，能在编辑期实时反馈、能拒绝 PR 而非等到测试运行；但开发成本高（Roslyn analyzer 项目模板、诊断 ID 注册、调试体验弱、需要单独打包）。
- 对一个 V0 桌面 App 项目过度，与"依赖最小化"取向冲突。
- 复议触发条件中已写入：若 NetArchTest 失修或正向断言成为常态，再评估迁移到 Roslyn analyzer。

## 决策

采用方案 C：双层断言，分而治之。

### 1. 层依赖方向：用 assembly 引用做精确比对

层级断言用 `Assembly.GetReferencedAssemblies()` 返回的 `AssemblyName.Name`（精确程序集名，无前缀歧义）做集合包含 / 不包含判断：

```csharp
private static HashSet<string> ReferencedAssemblyNames(Assembly assembly)
    => assembly.GetReferencedAssemblies()
        .Select(n => n.Name ?? string.Empty)
        .ToHashSet(StringComparer.Ordinal);

[Test]
public void Domain_DoesNotReferenceDomainServices()
{
    var refs = ReferencedAssemblyNames(typeof(SomeDomainType).Assembly);
    Assert.That(refs, Does.Not.Contain("Dawning.AgentOS.Domain.Services"));
}
```

要点：

- 比对单位是**精确 assembly 名**（`Name` 属性），而非命名空间前缀；`Dawning.AgentOS.Domain` 与 `Dawning.AgentOS.Domain.Core` 是不同字符串，不再混淆。
- 比对算法是**集合包含**（`Does.Contain` / `Does.Not.Contain`），不是 `StartsWith`。
- 断言用 `StringComparer.Ordinal` 而非默认 `OrdinalIgnoreCase`，避免大小写引发的误判。

### 2. 层依赖方向只写负向断言

**只断言"不能引用 X"，暂不断言"必须引用 X"**。

理由是陷阱二：编译器对未实际绑定类型的 ProjectReference 做 metadata pruning，正向断言会随业务切片进度时灵时不灵。负向断言不受此影响，因为"被裁掉的引用"本来就符合"不存在某引用"的负向期待。

正向断言的引入条件：

- 该层至少有一个具体类型实际绑定基类 / 上游接口（例如 Domain 中的某个 `class InboxItem : AggregateRoot<InboxItemId>`）。
- 此时 `GetReferencedAssemblies()` 才会稳定包含上游 assembly。
- 加正向断言的同一 commit 必须确认该层确有非空业务类型，注释写清楚"此断言依赖至少一个具体绑定"。

### 3. 类型级禁用规则：用 NetArchTest 到具体类型全名

类型级断言（"某类型不应被某层使用"）保留 NetArchTest，但**禁止用命名空间前缀**作为断言依据。改用具体类型全名（含命名空间）：

```csharp
[Test]
public void Domain_DoesNotUseMediatRMainPackageTypes()
{
    var result = Types.InAssembly(domainAssembly)
        .ShouldNot()
        .HaveDependencyOnAny(
            "MediatR.IMediator",
            "MediatR.IRequestHandler",
            "MediatR.IPipelineBehavior")
        .GetResult();

    Assert.That(result.IsSuccessful, Is.True);
}
```

要点：

- 列出禁用类型必须**到类名**，不写"`MediatR`"这种短前缀；否则陷阱一会让 `MediatR.Contracts.INotification` 误命中。
- 列表是白名单的反面：明确写出禁止的具体上游契约（IMediator / IRequestHandler / IPipelineBehavior 等），而不是"禁止整个命名空间"。
- 新增禁用类型时直接追加字符串，不需要修改 helper。

### 4. 命名空间字符串前缀断言：禁止使用

不允许在架构测试中以 `HaveDependencyOn("Some.Namespace")` 形式做层级断言。所有层级断言走第 1 条路径（assembly 引用），所有类型级断言走第 3 条路径（具体类型全名）。

例外：明确知道某命名空间无子层、无歧义时，可以用具体子命名空间字符串作为类型级禁用的简写（例如 `HaveDependencyOnAny("Microsoft.AspNetCore.Mvc")` 禁止 Domain 触碰 ASP.NET Core MVC 类型）。这种用法属于陷阱一的退化情况，需逐条 review。

### 5. NetArchTest 升级 / 替换的处置

- NetArchTest 后续版本若引入"按命名空间段精确匹配"的 API，可考虑放宽第 4 条；该改动需在新 ADR 中明确，不在本 ADR 默认放开。
- 若决定迁移到 Roslyn analyzer（`docs/pages/rules/architecture-test-assertion-strategy.md` 暂未规划），写新 ADR supersede 本 ADR。

### 6. 失败信息要求

NetArchTest 断言失败默认信息只给"`IsSuccessful = false`"，调试体验差。所有 NetArchTest 断言必须配合 helper 输出 `result.FailingTypes` 清单：

```csharp
private static string FormatFailures(TestResult result)
{
    if (result.IsSuccessful || result.FailingTypes is null) return string.Empty;
    var names = result.FailingTypes.Select(t => t.FullName ?? t.Name);
    return "Failing types:\n  " + string.Join("\n  ", names);
}
```

assembly 引用断言失败时，NUnit `Does.Contain` / `Does.Not.Contain` 已有较好默认信息，无需额外格式化。

## 影响

**正向影响**：

- S2 起所有架构测试遵循同一断言模型；新层（S3 InboxItem、Application、Infra.Data 等）首次写架构测试时直接 copy helper，不再重新踩两个陷阱。
- 层断言走精确 assembly 名，编译器表达力（精确程序集 identity）与断言表达力对齐；不再用 namespace 字符串近似 assembly 边界。
- 类型级断言保留 NetArchTest 的 DSL 优势；只是约束写法到具体类型，规避前缀语义。
- 失败信息明确指出违规类型，CI 红线有可追溯线索。

**代价 / 风险**：

- 正向 layering 断言（"必须引用 Domain.Core"）暂时缺失，依赖 Code Review 阻挡"故意把 ProjectReference 删了"的恶意修改。复议条件：S3 业务聚合落地后，立即给所有有非空类型绑定的层补正向断言。
- NetArchTest 禁用类型清单维护是机械工作，每加一个外部框架（FluentValidation / Microsoft.SemanticKernel / OpenAI SDK 等）都要决定哪些层禁用、写到 helper 列表里。可以考虑在某层引入新依赖时，PR 必须同时更新 LayeringTests 禁用列表，作为 Code Review checklist。
- 编译器 metadata pruning 行为是 .NET 的标准行为，但若未来 .NET SDK 默认开启更激进的 reference assembly trimming，第 1 条 helper 可能需要切换到读 csproj `<ProjectReference>` 节点（解析 XML），而不是 `GetReferencedAssemblies()`。复议触发条件中已写入。
- 跨仓库一致性：dawning / dawning-assistant 暂未采用此策略；本 ADR 不约束这两个仓库。

## 复议触发条件

`adr_revisit_when` 已写入 front matter，本节不重复。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：产品契约与 wiki 收录范围。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：V0 测试项目划分。
- [ADR-018 后端架构参考 Equinox：DDD + MediatR + Result 模式](backend-architecture-equinox-reference.md)：架构测试库（NetArchTest.Rules）选型与依赖方向规则。
- [ADR-019 测试栈：NUnit + Moq + NetArchTest](testing-stack-nunit-v0.md)：测试框架与断言风格。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：实现前先方案、后确认、再执行。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
