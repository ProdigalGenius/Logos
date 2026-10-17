using SahaNet.Domain.Common;

namespace SahaNet.UnitTests.Domain;

public class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_ValueAccess_Throws()
    {
        var result = Result.Failure<int>(new Error("x.fail", "Hata"));

        result.IsFailure.Should().BeTrue();
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Failure_WithNoneError_Throws()
    {
        var act = () => Result.Failure(Error.None);

        act.Should().Throw<ArgumentException>();
    }
}
