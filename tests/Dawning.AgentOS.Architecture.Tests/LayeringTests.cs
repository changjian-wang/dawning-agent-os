using System.Reflection;
using NetArchTest.Rules;
using NUnit.Framework;

namespace Dawning.AgentOS.Architecture.Tests;

/// <summary>
/// Pins the dependency direction of the layered architecture. New src
/// projects must register their layering rule here before they are merged.
/// </summary>
/// <remarks>
/// <para>
/// Two assertion styles are intentionally mixed:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Layer direction</b> uses <see cref="Assembly.GetReferencedAssemblies"/>
///       with exact <c>Name</c> comparison. Project assembly names are unique
///       identifiers, so contains / not-contains over a hash set is unambiguous.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Type-level bans</b> (e.g. forbidding pipeline interfaces from a
///       given layer) use NetArchTest with concrete type full names such as
///       <c>"MediatR.IMediator"</c>, never bare namespace prefixes.
///     </description>
///   </item>
/// </list>
/// <para>
/// Two pitfalls justify the split:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       NetArchTest matches dependencies with <c>StartsWith</c> against the
///       full type name. <c>HaveDependencyOn("Dawning.AgentOS.Domain")</c>
///       would falsely fire on <c>Dawning.AgentOS.Domain.Core.*</c>. Hence
///       layer rules avoid NetArchTest entirely, and type-level bans always
///       name the concrete forbidden type.
///     </description>
///   </item>
///   <item>
///     <description>
///       The C# compiler removes ProjectReferences from emitted assembly
///       metadata when no source type actually binds to a type from that
///       reference. Until a layer has at least one concrete type derived from
///       an upstream base class (or returning an upstream value), positive
///       "must reference X" assertions are flaky. Only forbidden-direction
///       assertions are written here; positive assertions are added once a
///       layer has real type bindings.
///     </description>
///   </item>
/// </list>
/// </remarks>
[TestFixture]
public sealed class LayeringTests
{
    private const string DomainName = "Dawning.AgentOS.Domain";
    private const string DomainServicesName = "Dawning.AgentOS.Domain.Services";
    private const string ApplicationName = "Dawning.AgentOS.Application";

    private static readonly Assembly DomainCore =
        typeof(global::Dawning.AgentOS.Domain.Core.Result).Assembly;

    private static readonly Assembly Domain =
        typeof(global::Dawning.AgentOS.Domain.Permissions.ActionLevel).Assembly;

    private static readonly Assembly DomainServices =
        typeof(global::Dawning.AgentOS.Domain.Services.Permissions.IActionClassifier).Assembly;

    private static readonly Assembly Application =
        typeof(global::Dawning.AgentOS.Application.Interfaces.IRuntimeAppService).Assembly;

    private static readonly Assembly Api =
        typeof(global::Dawning.AgentOS.Api.Endpoints.Runtime.RuntimeEndpoints).Assembly;

    [Test]
    public void DomainCore_DoesNotReferenceAnyExternalPackages()
    {
        // Per ADR-022 Domain.Core has zero external dependencies. The
        // BCL is allowed (it surfaces as System.* / Microsoft.CSharp /
        // netstandard / etc. depending on TFM); any third-party package
        // reference here is a regression. The earlier MediatR.Contracts
        // dependency was removed alongside the rest of the MediatR stack.
        var refs = ReferencedAssemblyNames(DomainCore);

        var forbidden = new[]
        {
            "MediatR",
            "MediatR.Contracts",
            "Dapper",
            "Microsoft.AspNetCore",
            "Microsoft.Data.Sqlite",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Configuration.Abstractions",
        };

        foreach (var name in forbidden)
        {
            Assert.That(refs, Does.Not.Contain(name), $"Domain.Core must not reference '{name}'.");
        }
    }

    [Test]
    public void DomainCore_DoesNotReferenceProjectAssemblies()
    {
        var refs = ReferencedAssemblyNames(DomainCore);

        Assert.Multiple(() =>
        {
            Assert.That(refs, Does.Not.Contain(DomainName));
            Assert.That(refs, Does.Not.Contain(DomainServicesName));
        });
    }

    [Test]
    public void Domain_DoesNotReferenceDomainServices()
    {
        var refs = ReferencedAssemblyNames(Domain);

        // Forbidden direction: Domain must never depend on Domain.Services.
        // The reverse (Domain.Services -> Domain) is allowed.
        Assert.That(refs, Does.Not.Contain(DomainServicesName));
    }

    [Test]
    public void Domain_DoesNotReferenceFrameworkOrInfraPackages()
    {
        var refs = ReferencedAssemblyNames(Domain);

        // Forbidden: any infra / framework dependency. The BCL (System.*) and
        // Domain.Core remain allowed.
        var forbidden = new[]
        {
            "MediatR",
            "MediatR.Contracts",
            "Microsoft.AspNetCore",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Configuration.Abstractions",
            "Microsoft.Data.Sqlite",
            "Dapper",
        };

        foreach (var name in forbidden)
        {
            Assert.That(refs, Does.Not.Contain(name), $"Domain must not reference '{name}'.");
        }
    }

    [Test]
    public void DomainServices_DoesNotReferenceUpstreamLayers()
    {
        var refs = ReferencedAssemblyNames(DomainServices);

        // Application / Infra projects don't exist yet; these names will
        // start firing the day someone wires the wrong direction.
        var forbiddenProjectRefs = new[]
        {
            "Dawning.AgentOS.Application",
            "Dawning.AgentOS.Infra.Data",
            "Dawning.AgentOS.Infra.CrossCutting.Bus",
            "Dawning.AgentOS.Infra.CrossCutting.Security",
            "Dawning.AgentOS.Infra.CrossCutting.IoC",
            "Dawning.AgentOS.Services.Api",
        };

        foreach (var name in forbiddenProjectRefs)
        {
            Assert.That(
                refs,
                Does.Not.Contain(name),
                $"Domain.Services must not reference '{name}'."
            );
        }
    }

    [Test]
    public void DomainServices_DoesNotReferenceFrameworkOrInfraPackages()
    {
        var refs = ReferencedAssemblyNames(DomainServices);

        var forbidden = new[]
        {
            "MediatR",
            "MediatR.Contracts",
            "Microsoft.AspNetCore",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Configuration.Abstractions",
            "Microsoft.Data.Sqlite",
            "Dapper",
        };

        foreach (var name in forbidden)
        {
            Assert.That(
                refs,
                Does.Not.Contain(name),
                $"Domain.Services must not reference '{name}'."
            );
        }
    }

    [Test]
    public void Domain_DoesNotUseMediatRMainPackageTypes()
    {
        // Belt-and-braces: even if a transitive package showed up, no
        // Domain type may use the main MediatR pipeline interfaces.
        var result = Types
            .InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MediatR.IMediator",
                "MediatR.IRequestHandler",
                "MediatR.IPipelineBehavior"
            )
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, FormatFailures(result));
    }

    [Test]
    public void DomainServices_DoesNotUseMediatRMainPackageTypes()
    {
        var result = Types
            .InAssembly(DomainServices)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MediatR.IMediator",
                "MediatR.IRequestHandler",
                "MediatR.IPipelineBehavior"
            )
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, FormatFailures(result));
    }

    [Test]
    public void Application_DoesNotReferenceInfraOrApiLayers()
    {
        // ADR-021 places Application above Infra and Services.Api in the
        // dependency graph: ports declared here, implemented downstream.
        var refs = ReferencedAssemblyNames(Application);

        var forbidden = new[]
        {
            "Dawning.AgentOS.Infra.Data",
            "Dawning.AgentOS.Infra.CrossCutting.Bus",
            "Dawning.AgentOS.Infra.CrossCutting.Security",
            "Dawning.AgentOS.Infra.CrossCutting.IoC",
            "Dawning.AgentOS.Services.Api",
        };

        foreach (var name in forbidden)
        {
            Assert.That(refs, Does.Not.Contain(name), $"Application must not reference '{name}'.");
        }
    }

    [Test]
    public void Application_DoesNotReferenceFrameworkAdapterPackages()
    {
        // Per ADR-022 the Application layer no longer references MediatR or
        // MediatR.Contracts. It must also not reference any concrete
        // framework adapter: web stack, DI container, logging implementation,
        // persistence driver, etc. Those belong in Infra.* projects.
        //
        // Per ADR-023 §5 the Application layer hosts the AddApplication
        // composition extension and therefore depends on the
        // abstractions-only package
        // Microsoft.Extensions.DependencyInjection.Abstractions. The
        // concrete container Microsoft.Extensions.DependencyInjection
        // remains forbidden here; only the abstractions package is allowed.
        var refs = ReferencedAssemblyNames(Application);

        var forbidden = new[]
        {
            "MediatR",
            "MediatR.Contracts",
            "Microsoft.AspNetCore",
            "Microsoft.AspNetCore.App",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Configuration.Abstractions",
            "Microsoft.Data.Sqlite",
            "Dapper",
        };

        foreach (var name in forbidden)
        {
            Assert.That(refs, Does.Not.Contain(name), $"Application must not reference '{name}'.");
        }
    }

    [Test]
    public void Application_AbstractionsFolder_OnlyContainsInterfaces()
    {
        // Per ADR-022 / ADR-024 Application/Abstractions/ holds ports
        // (IClock, IRuntimeStartTimeProvider, IDomainEventDispatcher,
        // IDbConnectionFactory, ISchemaInitializer, IAppDataPathProvider)
        // implemented by Infra.* projects. A concrete class appearing
        // anywhere under this namespace tree is the early signal that
        // the layout discipline is slipping, and must fail the build.
        var result = Types
            .InAssembly(Application)
            .That()
            .ResideInNamespaceStartingWith("Dawning.AgentOS.Application.Abstractions")
            .Should()
            .BeInterfaces()
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, FormatFailures(result));
    }

    [Test]
    public void Application_InterfacesFolder_OnlyContainsInterfaces()
    {
        // Per ADR-022 Application/Interfaces/ holds AppService facade
        // contracts (e.g. IRuntimeAppService) consumed by the API layer.
        // Concrete classes belong in Application/Services/, not here.
        var result = Types
            .InAssembly(Application)
            .That()
            .ResideInNamespace("Dawning.AgentOS.Application.Interfaces")
            .Should()
            .BeInterfaces()
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, FormatFailures(result));
    }

    [Test]
    public void Application_ServicesNamespace_OnlyContainsConcreteClasses()
    {
        // Per ADR-022 Application/Services/ holds the concrete AppService
        // implementations of contracts declared in Application/Interfaces/.
        // Interfaces or abstract classes belong elsewhere.
        var result = Types
            .InAssembly(Application)
            .That()
            .ResideInNamespace("Dawning.AgentOS.Application.Services")
            .Should()
            .BeClasses()
            .And()
            .NotBeAbstract()
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, FormatFailures(result));
    }

    [Test]
    public void Api_DoesNotReferenceDomainProjectsDirectly()
    {
        // Per ADR-023 §10 the API project is the composition root: it may
        // reference Application (to consume AppService facades) and
        // Infrastructure (for AddInfrastructure() in Program.cs), but it
        // must not reach into the business-bearing domain layers.
        // Domain.Core is the shared-kernel primitive layer (Result<T>,
        // DomainError, IDomainEvent) used by every layer including the
        // Api result mapping; that reference is intentional and surfaces
        // through Application's project graph regardless.
        var refs = ReferencedAssemblyNames(Api);

        var forbidden = new[] { "Dawning.AgentOS.Domain", "Dawning.AgentOS.Domain.Services" };

        foreach (var name in forbidden)
        {
            Assert.That(refs, Does.Not.Contain(name), $"Api must not directly reference '{name}'.");
        }
    }

    [Test]
    public void Api_EndpointsNamespace_OnlyContainsStaticClasses()
    {
        // Per ADR-023 §2 every endpoint registration class is a static
        // class providing a Map<Feature>Endpoints extension method.
        // NetArchTest models 'static' as 'sealed + abstract' on the
        // underlying type metadata. The rule is scoped to types named
        // *Endpoints so that purely-Api response DTOs (records) can
        // coexist alongside their endpoint group without violating the
        // contract intent (which is about the registration shape, not
        // about what other types may live under the Endpoints folder).
        var result = Types
            .InAssembly(Api)
            .That()
            .ResideInNamespaceStartingWith("Dawning.AgentOS.Api.Endpoints")
            .And()
            .HaveNameEndingWith("Endpoints")
            .Should()
            .BeClasses()
            .And()
            .BeSealed()
            .And()
            .BeAbstract()
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, FormatFailures(result));
    }

    [Test]
    public void Api_DoesNotReferencePersistenceOrORMPackages()
    {
        // Per ADR-023 §10 the API project, like Application, must not
        // depend on persistence drivers or ORM packages. Those belong in
        // the Infrastructure project, reached through DI only.
        var refs = ReferencedAssemblyNames(Api);

        var forbidden = new[] { "Dapper", "Microsoft.Data.Sqlite", "Dawning.ORM.Dapper" };

        foreach (var name in forbidden)
        {
            Assert.That(refs, Does.Not.Contain(name), $"Api must not reference '{name}'.");
        }
    }

    [Test]
    public void Application_DoesNotReferencePersistencePackages()
    {
        // Per ADR-024 §K1 the Application layer holds only persistence
        // *ports* (IDbConnectionFactory, ISchemaInitializer); the actual
        // SQLite driver, Dapper and the Dawning.ORM.Dapper SDK live in
        // Infrastructure and must not leak into Application.
        var refs = ReferencedAssemblyNames(Application);

        var forbidden = new[] { "Microsoft.Data.Sqlite", "Dapper", "Dawning.ORM.Dapper" };

        foreach (var name in forbidden)
        {
            Assert.That(
                refs,
                Does.Not.Contain(name),
                $"Application must not reference persistence package '{name}' (ADR-024 §K1)."
            );
        }
    }

    [Test]
    public void DomainAndDomainServices_DoNotReferencePersistencePackages()
    {
        // Per ADR-024 §K1 the Domain and Domain.Services layers must
        // also be free of any persistence dependency. This is already
        // covered transitively by their existing
        // ...DoesNotReferenceFrameworkOrInfraPackages assertions, but a
        // dedicated test gives a clearer signal when a regression
        // happens specifically on the persistence axis.
        var domainRefs = ReferencedAssemblyNames(Domain);
        var domainServicesRefs = ReferencedAssemblyNames(DomainServices);

        var forbidden = new[] { "Microsoft.Data.Sqlite", "Dapper", "Dawning.ORM.Dapper" };

        foreach (var name in forbidden)
        {
            Assert.That(
                domainRefs,
                Does.Not.Contain(name),
                $"Domain must not reference persistence package '{name}' (ADR-024 §K1)."
            );
            Assert.That(
                domainServicesRefs,
                Does.Not.Contain(name),
                $"Domain.Services must not reference persistence package '{name}' (ADR-024 §K1)."
            );
        }
    }

    [Test]
    public void PersistencePorts_LiveInApplicationAbstractionsPersistenceNamespace()
    {
        // Per ADR-024 §1 the persistence ports
        // (IDbConnectionFactory, ISchemaInitializer) belong in
        // Dawning.AgentOS.Application.Abstractions.Persistence; the
        // hosting port (IAppDataPathProvider) belongs in
        // Dawning.AgentOS.Application.Abstractions.Hosting. Anchoring
        // these by reflection keeps the assertion stable across renames.
        var dbConnectionFactoryNamespace =
            typeof(global::Dawning.AgentOS.Application.Abstractions.Persistence.IDbConnectionFactory).Namespace;
        var schemaInitializerNamespace =
            typeof(global::Dawning.AgentOS.Application.Abstractions.Persistence.ISchemaInitializer).Namespace;
        var appDataPathProviderNamespace =
            typeof(global::Dawning.AgentOS.Application.Abstractions.Hosting.IAppDataPathProvider).Namespace;

        Assert.Multiple(() =>
        {
            Assert.That(
                dbConnectionFactoryNamespace,
                Is.EqualTo("Dawning.AgentOS.Application.Abstractions.Persistence"),
                "IDbConnectionFactory must live in Application.Abstractions.Persistence."
            );
            Assert.That(
                schemaInitializerNamespace,
                Is.EqualTo("Dawning.AgentOS.Application.Abstractions.Persistence"),
                "ISchemaInitializer must live in Application.Abstractions.Persistence."
            );
            Assert.That(
                appDataPathProviderNamespace,
                Is.EqualTo("Dawning.AgentOS.Application.Abstractions.Hosting"),
                "IAppDataPathProvider must live in Application.Abstractions.Hosting."
            );
        });
    }

    [Test]
    public void IInboxRepository_LivesInDomainInboxNamespace()
    {
        // Per ADR-026 §H1 the aggregate repository port lives in the
        // Domain layer (DDD-canonical placement) and talks exclusively
        // in domain types. A future move to Application.Abstractions
        // should fail this assertion to force a fresh ADR.
        var inboxRepositoryType = typeof(global::Dawning.AgentOS.Domain.Inbox.IInboxRepository);

        Assert.Multiple(() =>
        {
            Assert.That(
                inboxRepositoryType.Namespace,
                Is.EqualTo("Dawning.AgentOS.Domain.Inbox"),
                "IInboxRepository must live under Domain/Inbox per ADR-026 §H1."
            );
            Assert.That(
                inboxRepositoryType.Assembly,
                Is.EqualTo(Domain),
                "IInboxRepository must ship in the Dawning.AgentOS.Domain assembly."
            );
        });
    }

    private static HashSet<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly
            .GetReferencedAssemblies()
            .Select(n => n.Name ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

    private static string FormatFailures(TestResult result)
    {
        if (result.IsSuccessful || result.FailingTypes is null)
        {
            return string.Empty;
        }

        var names = result.FailingTypes.Select(t => t.FullName ?? t.Name);
        return "Failing types:\n  " + string.Join("\n  ", names);
    }
}
