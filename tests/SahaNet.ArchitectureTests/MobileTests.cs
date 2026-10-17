using NetArchTest.Rules;

namespace SahaNet.ArchitectureTests;

public class MobileTests
{
    [Fact]
    public void MobileCore_DoesNotDependOnServerAssemblies()
    {
        var result = Types.InAssembly(typeof(SahaNet.Mobile.Core.Api.SahaNetApiClient).Assembly).ShouldNot()
            .HaveDependencyOnAny("SahaNet.Domain", "SahaNet.Application", "SahaNet.Infrastructure", "SahaNet.Api", "SahaNet.Modules", "Microsoft.EntityFrameworkCore", "MediatR")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
