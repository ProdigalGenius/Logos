using NetArchTest.Rules;

namespace SahaNet.ArchitectureTests;

public class NamingTests
{
    [Fact]
    public void Validators_AreSealed_AndEndWithValidator()
    {
        var result = Types.InAssembly(typeof(SahaNet.Application.DependencyInjection).Assembly)
            .That().Inherit(typeof(FluentValidation.AbstractValidator<>))
            .Should().BeSealed().And().HaveNameEndingWith("Validator", StringComparison.Ordinal)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
