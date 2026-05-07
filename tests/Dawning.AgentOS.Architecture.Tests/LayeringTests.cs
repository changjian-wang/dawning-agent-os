using System.Reflection;
using System.Runtime.CompilerServices;
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
    private const string AbstractionsName = "Dawning.AgentOS.Abstractions";
    private const string DomainCoreName = "Dawning.AgentOS.Domain.Core";

    private static readonly Assembly DomainCore =
        typeof(global::Dawning.AgentOS.Domain.Core.Result).Assembly;

    private static readonly Assembly Abstractions =
        typeof(global::Dawning.AgentOS.Abstractions.IClock).Assembly;

    private static readonly Assembly Domain =
        typeof(global::Dawning.AgentOS.Domain.Permissions.ActionLevel).Assembly;

    private static readonly Assembly DomainServices =
        typeof(global::Dawning.AgentOS.Domain.Services.Permissions.IActionClassifier).Assembly;

    private static readonly Assembly Application =
        typeof(global::Dawning.AgentOS.Application.Interfaces.IRuntimeAppService).Assembly;

    private static readonly Assembly Infrastructure =
        typeof(global::Dawning.AgentOS.Infrastructure.Persistence.SqliteConnectionFactory).Assembly;

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
    public void Infrastructure_DoesNotReferenceApplication()
    {
        // Per ADR-037 D5 Infrastructure must not depend on Application.
        // The contract / port surface that Infrastructure adapters bind to
        // lives in the Dawning.AgentOS.Abstractions assembly (and, for the
        // domain-event dispatcher port, in Dawning.AgentOS.Domain.Core).
        // Pulling in Application would re-introduce the rebuild coupling
        // that ADR-037 was created to eliminate.
        var refs = ReferencedAssemblyNames(Infrastructure);

        Assert.Multiple(() =>
        {
            Assert.That(
                refs,
                Does.Not.Contain(ApplicationName),
                "Infrastructure must not reference Application (ADR-037 D5)."
            );
            Assert.That(
                refs,
                Does.Contain(AbstractionsName),
                "Infrastructure must reference Abstractions for ports + LLM DTOs."
            );
            Assert.That(
                refs,
                Does.Contain(DomainCoreName),
                "Infrastructure must reach Domain.Core for IDomainEventDispatcher (transitive via Domain or Abstractions)."
            );
        });
    }

    [Test]
    public void Abstractions_OnlyReferencesDomainCoreSharedKernel()
    {
        // Per ADR-037 D1 + D5 the Abstractions assembly is allowed exactly
        // one project reference: Domain.Core, used as a shared kernel for
        // Result<T> / DomainError. References to Domain (aggregates),
        // Domain.Services (cross-aggregate domain logic), Application
        // (use cases), Infrastructure (adapters), or Api (composition
        // root) would invert the dependency direction the ADR pins down.
        var refs = ReferencedAssemblyNames(Abstractions);

        var forbidden = new[]
        {
            DomainName,
            DomainServicesName,
            ApplicationName,
            "Dawning.AgentOS.Infrastructure",
            "Dawning.AgentOS.Api",
        };

        foreach (var name in forbidden)
        {
            Assert.That(
                refs,
                Does.Not.Contain(name),
                $"Abstractions must not reference '{name}' (ADR-037 D5)."
            );
        }

        // The single allowed dependency must actually be present; if it
        // is silently dropped the shared-kernel primitives Result<T> /
        // DomainError stop being available and consumers break in a
        // confusing way at compile time.
        Assert.That(
            refs,
            Does.Contain(DomainCoreName),
            "Abstractions must reference Domain.Core for Result<T> / DomainError shared kernel (ADR-037 D1)."
        );
    }

    [Test]
    public void Abstractions_DoesNotReferenceFrameworkAdapterPackages()
    {
        // Per ADR-037 D1 + D6 Abstractions must stay free of any framework
        // adapter package. It is the contract surface, not the binding
        // surface; concrete drivers (SQLite, Dapper, Dawning.ORM.Dapper),
        // web stack (AspNetCore), and previously-removed pipeline
        // (MediatR) all belong in Infrastructure or Api.
        var refs = ReferencedAssemblyNames(Abstractions);

        var forbidden = new[]
        {
            "Microsoft.Data.Sqlite",
            "Dapper",
            "Dawning.ORM.Dapper",
            "Microsoft.AspNetCore",
            "Microsoft.AspNetCore.App",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Hosting",
            "Microsoft.Extensions.Hosting.Abstractions",
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Options",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions",
            "MediatR",
            "MediatR.Contracts",
        };

        foreach (var name in forbidden)
        {
            Assert.That(
                refs,
                Does.Not.Contain(name),
                $"Abstractions must not reference framework / adapter package '{name}' (ADR-037 D6)."
            );
        }
    }

    [Test]
    public void DomainCore_DispatcherSignaturesOnlyReferenceBclAndDomainCore()
    {
        // Per ADR-037 D2 IDomainEventDispatcher lives in Domain.Core
        // alongside IDomainEvent (Vernon IDDD §8). Domain.Core stays
        // dependency-free, which means the dispatcher's DispatchAsync
        // signatures must only mention BCL types (Task,
        // CancellationToken, IEnumerable<T>) and Domain.Core's own
        // IDomainEvent. A regression that, say, adds an
        // ILogger / IServiceProvider parameter would silently pull a
        // framework dependency into Domain.Core.
        var dispatcher = typeof(global::Dawning.AgentOS.Domain.Core.IDomainEventDispatcher);

        var allowedAssemblies = new HashSet<string>(StringComparer.Ordinal)
        {
            DomainCoreName,
            // BCL surfaces by various assembly identities depending on
            // TFM; allow the System.* family wholesale below.
        };

        var disallowedReferences = new List<string>();
        foreach (var method in dispatcher.GetMethods())
        {
            var typesInSignature = new List<Type> { method.ReturnType };
            typesInSignature.AddRange(method.GetParameters().Select(p => p.ParameterType));

            // Recursively flatten generic arguments so e.g.
            // IEnumerable<IDomainEvent> validates IDomainEvent too.
            var flattened = new List<Type>();
            void Flatten(Type t)
            {
                flattened.Add(t);
                if (t.IsGenericType)
                {
                    foreach (var arg in t.GetGenericArguments())
                    {
                        Flatten(arg);
                    }
                }
            }
            foreach (var t in typesInSignature)
            {
                Flatten(t);
            }

            foreach (var t in flattened)
            {
                var assemblyName = t.Assembly.GetName().Name ?? string.Empty;
                var isBcl =
                    assemblyName.StartsWith("System", StringComparison.Ordinal)
                    || assemblyName.Equals("mscorlib", StringComparison.Ordinal)
                    || assemblyName.Equals("netstandard", StringComparison.Ordinal);
                if (isBcl || allowedAssemblies.Contains(assemblyName))
                {
                    continue;
                }
                disallowedReferences.Add(
                    $"{method.Name} signature uses {t.FullName ?? t.Name} from {assemblyName}"
                );
            }
        }

        Assert.That(
            disallowedReferences,
            Is.Empty,
            "IDomainEventDispatcher signatures must only reference BCL + Domain.Core types (ADR-037 D2)."
        );
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
        //
        // Per ADR-038 §决策 F1 (silent-degrade memory retrieval needs a
        // warn-level log) the same abstractions-only carve-out is extended
        // to Microsoft.Extensions.Logging.Abstractions: the Application
        // layer may take an ILogger<T> dependency without binding any sink
        // implementation. The concrete provider package
        // Microsoft.Extensions.Logging remains forbidden — only the
        // abstractions package is allowed, mirroring the DI rule.
        var refs = ReferencedAssemblyNames(Application);

        var forbidden = new[]
        {
            "MediatR",
            "MediatR.Contracts",
            "Microsoft.AspNetCore",
            "Microsoft.AspNetCore.App",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Logging",
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
    public void AbstractionsAssembly_OnlyContainsInterfacesAndDtos()
    {
        // Per ADR-037 the Dawning.AgentOS.Abstractions assembly hosts:
        // - 6 ports (interfaces): IClock, IRuntimeStartTimeProvider,
        //   IAppDataPathProvider, IDbConnectionFactory, ISchemaInitializer,
        //   ILlmProvider.
        // - LLM DTOs (records / enums / static factory): LlmCompletion,
        //   LlmRequest, LlmMessage, LlmStreamChunk, LlmRole,
        //   LlmStreamChunkKind, LlmErrors.
        // A concrete service class (instance-able non-record class with
        // mutable state or behaviour) appearing in this assembly is the
        // early signal that adapter or use-case logic is leaking back into
        // Abstractions and must fail the build.
        //
        // NetArchTest doesn't natively model records or static classes, so
        // the predicate is expressed in plain reflection: a type is allowed
        // when it is an interface, an enum, a static class (sealed +
        // abstract), or a record (sealed class with a synthesized <Clone>$
        // instance method).
        static bool IsAllowedAbstractionsType(Type t)
        {
            if (t.IsInterface || t.IsEnum)
            {
                return true;
            }

            if (t.IsClass && t.IsSealed && t.IsAbstract)
            {
                // C# `static class` compiles to sealed + abstract.
                return true;
            }

            if (t.IsClass && t.IsSealed)
            {
                // C# record compiles to a sealed class carrying a
                // synthesized <Clone>$ instance method (any access).
                var cloneMethod = t.GetMethod(
                    "<Clone>$",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                if (cloneMethod is not null)
                {
                    return true;
                }
            }

            return false;
        }

        var disallowed = Abstractions
            .GetTypes()
            .Where(IsAuthoredType)
            .Where(t => !IsAllowedAbstractionsType(t))
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        Assert.That(
            disallowed,
            Is.Empty,
            "Abstractions must contain only interfaces, enums, records, or static classes (ADR-037)."
        );
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
    public void PersistencePorts_LiveInAbstractionsPersistenceNamespace()
    {
        // Per ADR-024 §1 + ADR-037 the persistence ports
        // (IDbConnectionFactory, ISchemaInitializer) belong in
        // Dawning.AgentOS.Abstractions.Persistence; the
        // hosting port (IAppDataPathProvider) belongs in
        // Dawning.AgentOS.Abstractions.Hosting. Anchoring
        // these by reflection keeps the assertion stable across renames.
        var dbConnectionFactoryNamespace =
            typeof(global::Dawning.AgentOS.Abstractions.Persistence.IDbConnectionFactory).Namespace;
        var schemaInitializerNamespace =
            typeof(global::Dawning.AgentOS.Abstractions.Persistence.ISchemaInitializer).Namespace;
        var appDataPathProviderNamespace =
            typeof(global::Dawning.AgentOS.Abstractions.Hosting.IAppDataPathProvider).Namespace;

        Assert.Multiple(() =>
        {
            Assert.That(
                dbConnectionFactoryNamespace,
                Is.EqualTo("Dawning.AgentOS.Abstractions.Persistence"),
                "IDbConnectionFactory must live in Dawning.AgentOS.Abstractions.Persistence."
            );
            Assert.That(
                schemaInitializerNamespace,
                Is.EqualTo("Dawning.AgentOS.Abstractions.Persistence"),
                "ISchemaInitializer must live in Dawning.AgentOS.Abstractions.Persistence."
            );
            Assert.That(
                appDataPathProviderNamespace,
                Is.EqualTo("Dawning.AgentOS.Abstractions.Hosting"),
                "IAppDataPathProvider must live in Dawning.AgentOS.Abstractions.Hosting."
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

    [Test]
    public void IChatSessionRepository_LivesInDomainChatNamespace()
    {
        // Per ADR-032 §决策 D2 / L1 the chat-side repository port
        // mirrors IInboxRepository's placement: Domain layer, scoped
        // namespace, talks only in domain types. Surfacing this as its
        // own test makes a future move to Application.Abstractions a
        // forced ADR conversation.
        var chatRepositoryType = typeof(global::Dawning.AgentOS.Domain.Chat.IChatSessionRepository);

        Assert.Multiple(() =>
        {
            Assert.That(
                chatRepositoryType.Namespace,
                Is.EqualTo("Dawning.AgentOS.Domain.Chat"),
                "IChatSessionRepository must live under Domain/Chat per ADR-032 §决策 D2."
            );
            Assert.That(
                chatRepositoryType.Assembly,
                Is.EqualTo(Domain),
                "IChatSessionRepository must ship in the Dawning.AgentOS.Domain assembly."
            );
        });
    }

    [Test]
    public void PersistenceEntities_LayoutIsLockedDown()
    {
        // Per ADR-036 + the persistence-repository-conventions rule the
        // layout is bidirectional:
        //   1. Every type under Persistence.Entities.{aggregate}/ must
        //      carry [Dawning.ORM.Dapper.Table] (i.e., it is a PO).
        //   2. Every type with that attribute in the Infrastructure
        //      assembly must live under Persistence.Entities.*.
        // Asserting both directions catches drift in either direction:
        // a non-PO file accidentally placed under Entities/, or a PO
        // file dropped into the legacy {Chat,Inbox,Memory}/ folders.
        const string EntitiesNamespacePrefix =
            "Dawning.AgentOS.Infrastructure.Persistence.Entities.";

        var allTypes = Infrastructure.GetTypes().Where(IsAuthoredType).ToArray();

        var entitiesWithoutTable = allTypes
            .Where(t =>
                t.Namespace is not null
                && t.Namespace.StartsWith(EntitiesNamespacePrefix, StringComparison.Ordinal)
            )
            .Where(t => t.GetCustomAttribute<global::Dawning.ORM.Dapper.TableAttribute>() is null)
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        var taggedOutsideEntities = allTypes
            .Where(t =>
                t.GetCustomAttribute<global::Dawning.ORM.Dapper.TableAttribute>() is not null
            )
            .Where(t =>
                t.Namespace is null
                || !t.Namespace.StartsWith(EntitiesNamespacePrefix, StringComparison.Ordinal)
            )
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                entitiesWithoutTable,
                Is.Empty,
                "Types under Persistence.Entities.* must carry [Dawning.ORM.Dapper.Table] (ADR-036)."
            );
            Assert.That(
                taggedOutsideEntities,
                Is.Empty,
                "Types with [Dawning.ORM.Dapper.Table] must live under Persistence.Entities.* (ADR-036)."
            );
        });
    }

    [Test]
    public void PersistenceRepositories_LayoutIsLockedDown()
    {
        // Per ADR-036 + the persistence-repository-conventions rule:
        //   1. Every concrete class under Persistence.Repositories.* must
        //      implement at least one Domain-side I*Repository interface.
        //   2. Every concrete class in the Infrastructure assembly that
        //      implements such an interface must live under
        //      Persistence.Repositories.*.
        // Direction (2) is the load-bearing one: it forbids a regressed
        // sibling like InboxRepository.cs reappearing under
        // Persistence/Inbox/ instead of Persistence/Repositories/Inbox/.
        const string RepositoriesNamespacePrefix =
            "Dawning.AgentOS.Infrastructure.Persistence.Repositories.";
        const string DomainNamespacePrefix = "Dawning.AgentOS.Domain.";

        static bool IsDomainRepositoryInterface(Type i) =>
            i.IsInterface
            && i.Namespace is { } ns
            && ns.StartsWith(DomainNamespacePrefix, StringComparison.Ordinal)
            && i.Name.EndsWith("Repository", StringComparison.Ordinal);

        var allTypes = Infrastructure.GetTypes().Where(IsAuthoredType).ToArray();

        var repositoriesWithoutInterface = allTypes
            .Where(t =>
                t.IsClass
                && !t.IsAbstract
                && t.Namespace is not null
                && t.Namespace.StartsWith(RepositoriesNamespacePrefix, StringComparison.Ordinal)
            )
            .Where(t => !t.GetInterfaces().Any(IsDomainRepositoryInterface))
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        var repositoriesOutsidePath = allTypes
            .Where(t =>
                t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(IsDomainRepositoryInterface)
            )
            .Where(t =>
                t.Namespace is null
                || !t.Namespace.StartsWith(RepositoriesNamespacePrefix, StringComparison.Ordinal)
            )
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                repositoriesWithoutInterface,
                Is.Empty,
                "Types under Persistence.Repositories.* must implement a Domain.*.I*Repository interface (ADR-036)."
            );
            Assert.That(
                repositoriesOutsidePath,
                Is.Empty,
                "Concrete Domain.*.I*Repository implementations must live under Persistence.Repositories.* (ADR-036)."
            );
        });
    }

    private static HashSet<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly
            .GetReferencedAssemblies()
            .Select(n => n.Name ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Filter that excludes compiler-generated infrastructure (async
    /// state machines, lambda display classes, anonymous types) from
    /// reflection-based architecture assertions. These show up as
    /// nested types named like <c>&lt;&gt;c</c> or
    /// <c>&lt;Method&gt;d__N</c> and would otherwise pollute the
    /// "all types under namespace X" enumerations with noise that has
    /// nothing to do with the layout rule being asserted.
    /// </summary>
    private static bool IsAuthoredType(Type type) =>
        type.GetCustomAttribute<CompilerGeneratedAttribute>() is null
        && !type.Name.Contains('<', StringComparison.Ordinal);

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
