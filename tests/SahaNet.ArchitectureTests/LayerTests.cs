using System.Reflection;
using NetArchTest.Rules;

namespace SahaNet.ArchitectureTests;

public class LayerTests
{
    private const string DomainNs = "SahaNet.Domain";
    private const string ApplicationNs = "SahaNet.Application";
    private const string InfrastructureNs = "SahaNet.Infrastructure";
    private const string ApiNs = "SahaNet.Api";
    private const string ModulesNs = "SahaNet.Modules";

    private static readonly Assembly Domain = typeof(SahaNet.Domain.Common.ValueObject).Assembly;
    private static readonly Assembly Application = typeof(SahaNet.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(SahaNet.Infrastructure.DependencyInjection).Assembly;

    /// <summary>Yeni modül eklendiğinde bu listeye eklenmelidir.</summary>
    private static readonly string[] ModuleAssemblyNames =
    [
        typeof(SahaNet.Modules.Fishing.FishingModule).Assembly.GetName().Name!,
        typeof(SahaNet.Modules.Scrap.ScrapModule).Assembly.GetName().Name!,
    ];

    public static TheoryData<string> ModuleAssemblies => new(ModuleAssemblyNames);

    [Fact]
    public void Domain_HasNoDependencies_OnOtherLayersOrFrameworks()
    {
        var result = Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs, ModulesNs, "MediatR", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Application_DoesNotDependOn_InfrastructureApiOrModules()
    {
        var result = Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs, ModulesNs, "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOn_ApiOrModules()
    {
        var result = Types.InAssembly(Infrastructure).ShouldNot()
            .HaveDependencyOnAny(ApiNs, ModulesNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies))]
    public void Modules_DependOnlyOnCore(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);
        var otherModules = ModuleAssemblyNames
            .Where(name => name != assemblyName)
            .ToArray();

        var forbidden = new[] { InfrastructureNs, ApiNs }
            .Concat(otherModules)
            .ToArray();

        var result = Types.InAssembly(assembly).ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies))]
    public void Modules_DoNotReferenceOtherModuleAssemblies(string assemblyName)
    {
        var referenced = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name);

        referenced.Should().NotContain(name => name != null && name.StartsWith(ModulesNs, StringComparison.Ordinal) && name != assemblyName);
        referenced.Should().NotContain(new[] { InfrastructureNs, ApiNs });
    }

    private static string Describe(TestResult result) =>
        "şu tipler katman kuralını ihlal ediyor: " + string.Join(", ", result.FailingTypeNames ?? []);
}
