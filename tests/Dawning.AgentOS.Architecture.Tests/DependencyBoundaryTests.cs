using System.Xml.Linq;

namespace Dawning.AgentOS.Architecture.Tests;

public sealed class DependencyBoundaryTests
{
  [Theory]
  [InlineData("Dawning.AgentOS.Domain", "Dawning.AgentOS.Domain.Services")]
  [InlineData("Dawning.AgentOS.Domain", "Dawning.AgentOS.Application")]
  [InlineData("Dawning.AgentOS.Domain", "Dawning.AgentOS.Infrastructure")]
  [InlineData("Dawning.AgentOS.Domain", "Dawning.AgentOS.Api")]
  [InlineData("Dawning.AgentOS.Domain.Services", "Dawning.AgentOS.Application")]
  [InlineData("Dawning.AgentOS.Domain.Services", "Dawning.AgentOS.Infrastructure")]
  [InlineData("Dawning.AgentOS.Domain.Services", "Dawning.AgentOS.Api")]
  [InlineData("Dawning.AgentOS.Application", "Dawning.AgentOS.Infrastructure")]
  [InlineData("Dawning.AgentOS.Application", "Dawning.AgentOS.Api")]
  public void Project_references_follow_layer_direction(
    string projectName,
    string forbiddenReference
  )
  {
    var project = LoadProject(projectName);

    Assert.DoesNotContain(forbiddenReference, project.ProjectReferences);
  }

  [Theory]
  [InlineData("Dawning.AgentOS.Domain")]
  [InlineData("Dawning.AgentOS.Domain.Services")]
  public void Domain_projects_do_not_reference_framework_or_infrastructure_packages(
    string projectName
  )
  {
    var project = LoadProject(projectName);
    var forbiddenPackages = new[]
    {
      "Dapper",
      "Dawning.ORM.Dapper",
      "Microsoft.AspNetCore",
      "Microsoft.Data.Sqlite",
      "Microsoft.Extensions.Configuration",
      "Microsoft.Extensions.Logging",
      "MediatR",
    };

    foreach (var forbiddenPackage in forbiddenPackages)
    {
      Assert.DoesNotContain(
        project.PackageReferences,
        package => package.StartsWith(forbiddenPackage, StringComparison.Ordinal)
      );
    }
  }

  [Fact]
  public void Domain_services_project_only_references_domain_project()
  {
    var project = LoadProject("Dawning.AgentOS.Domain.Services");

    Assert.Equal(new[] { "Dawning.AgentOS.Domain" }, project.ProjectReferences);
    Assert.Empty(project.PackageReferences);
  }

  [Fact]
  public void Application_project_does_not_reference_adapter_packages()
  {
    var project = LoadProject("Dawning.AgentOS.Application");
    var forbiddenPackages = new[]
    {
      "Dapper",
      "Dawning.ORM.Dapper",
      "Microsoft.Data.Sqlite",
      "Yarp",
      "MailKit",
    };

    foreach (var forbiddenPackage in forbiddenPackages)
    {
      Assert.DoesNotContain(
        project.PackageReferences,
        package => package.StartsWith(forbiddenPackage, StringComparison.Ordinal)
      );
    }
  }

  [Fact]
  public void Api_source_does_not_use_sqlite_or_dapper_directly()
  {
    var apiDirectory = Path.Combine(RepositoryRoot, "src", "Dawning.AgentOS.Api");
    var sourceText = string.Join(
      Environment.NewLine,
      Directory
        .EnumerateFiles(apiDirectory, "*.cs", SearchOption.AllDirectories)
        .Select(File.ReadAllText)
    );

    Assert.DoesNotContain("Dapper", sourceText, StringComparison.Ordinal);
    Assert.DoesNotContain("Sqlite", sourceText, StringComparison.Ordinal);
  }

  private static ProjectSnapshot LoadProject(string projectName)
  {
    var projectPath = Path.Combine(RepositoryRoot, "src", projectName, $"{projectName}.csproj");
    var document = XDocument.Load(projectPath);
    var projectReferences = document
      .Descendants("ProjectReference")
      .Select(element => element.Attribute("Include")?.Value)
      .Where(include => !string.IsNullOrWhiteSpace(include))
      .Select(include => Path.GetFileNameWithoutExtension(include!))
      .OfType<string>()
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();
    var packageReferences = document
      .Descendants("PackageReference")
      .Select(element => element.Attribute("Include")?.Value)
      .Where(include => !string.IsNullOrWhiteSpace(include))
      .OfType<string>()
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    return new ProjectSnapshot(projectReferences, packageReferences);
  }

  private static string RepositoryRoot
  {
    get
    {
      var directory = new DirectoryInfo(AppContext.BaseDirectory);
      while (
        directory is not null
        && !File.Exists(Path.Combine(directory.FullName, "Dawning.AgentOS.slnx"))
      )
      {
        directory = directory.Parent;
      }

      return directory?.FullName
        ?? throw new InvalidOperationException("Could not locate repository root.");
    }
  }

  private sealed record ProjectSnapshot(string[] ProjectReferences, string[] PackageReferences);
}
