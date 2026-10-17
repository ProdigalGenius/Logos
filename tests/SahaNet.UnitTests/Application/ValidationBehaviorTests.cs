using FluentValidation;
using MediatR;
using SahaNet.Application.Behaviors;

namespace SahaNet.UnitTests.Application;

public class ValidationBehaviorTests
{
    public sealed record Ping(string Port) : IRequest<string>;

    private sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(x => x.Port).NotEmpty();
    }

    [Fact]
    public async Task InvalidRequest_ThrowsValidationException()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PingValidator()]);

        var act = () => behavior.Handle(new Ping(string.Empty), () => Task.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ValidRequest_CallsNext()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PingValidator()]);

        var response = await behavior.Handle(new Ping("Sarıyer"), () => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
    }

    [Fact]
    public async Task NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<Ping, string>([]);

        var response = await behavior.Handle(new Ping(string.Empty), () => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
    }
}
