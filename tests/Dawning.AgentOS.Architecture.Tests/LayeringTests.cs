using System.Reflection;
using NetArchTest.Rules;
using NUnit.Framework;

namespace Dawning.AgentOS.Architecture.Tests;

/// <summary>
/// Pins the dependency direction of the layered architecture defined by
/// ADR-017 / ADR-018. Layering rules use assembly references (the actual
/// project boundary), while type-level rules use NetArchTest. New src
/// projects must register here before they are merged.
/// </summary>
[TestFixture]
public sealed class LayeringTests
{
    private const string DomainName = "Dawning.AgentOS.Domain";
    private const string DomainServicesName = "Dawning.AgentOS.Domain.Services";

    private static readonly Assembly DomainCore = typeof(global::Dawning.AgentOS.Domain.Core.Result).Assembly;

    private static readonly Assembly Domain = typeof(global::Dawning.AgentOS.Domain.Permissions.ActionLevel).Assembly;

    private static readonly Assembly DomainServices = typeof(global::Dawning.AgentOS.Domain.Services.Permissions.IActionClassifier).Assembly;

    [Test]
    public void DomainCore_DoesNotReferenceMainMediatRPackage()
    {
        // Domain.Core may reference MediatR.Contracts (the abstraction
        // supplying INotification). The main MediatR package, which pulls in
        // handlers / pipeline / DI plumbing, must not leak into the domain.
        var refs = ReferencedAssemblyNames(DomainCore);

        Assert.That(refs, Does.Not.Contain("MediatR"));
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
            Assert.That(refs, Does.Not.Contain(name), $"Domain.Services must not reference '{name}'.");
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
            Assert.That(refs, Does.Not.Contain(name), $"Domain.Services must not reference '{name}'.");
        }
    }

    [Test]
    public void Domain_DoesNotUseMediatRMainPackageTypes()
    {
        // Belt-and-braces: even if a transitive package showed up, no
        // Domain type may use the main MediatR pipeline interfaces.
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MediatR.IMediator",
                "MediatR.IRequestHandler",
                "MediatR.IPipelineBehavior")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, FormatFailures(result));
    }

    [Test]
    public void DomainServices_DoesNotUseMediatRMainPackageTypes()
    {
        var result = Types.InAssembly(DomainServices)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MediatR.IMediator",
                "MediatR.IRequestHandler",
                "MediatR.IPipelineBehavior")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, FormatFailures(result));
    }

    private static HashSet<string> ReferencedAssemblyNames(Assembly assembly)
        => assembly.GetReferencedAssemblies()
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
