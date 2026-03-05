using FluentAssertions;
using FluentValidation;
using MediatR;
using NSubstitute;
using Seed.Application.Common.Behaviors;

namespace Seed.UnitTests.Common;

public record TestBehaviorRequest(string Name) : IRequest<string>;

public class TestBehaviorRequestValidator : AbstractValidator<TestBehaviorRequest>
{
    public TestBehaviorRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Should_Call_Next_When_No_Validators()
    {
        var validators = Enumerable.Empty<IValidator<TestBehaviorRequest>>();
        var behavior = new ValidationBehavior<TestBehaviorRequest, string>(validators);
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns("ok");

        var result = await behavior.Handle(new TestBehaviorRequest("test"), next, CancellationToken.None);

        result.Should().Be("ok");
        await next.Received(1)();
    }

    [Fact]
    public async Task Should_Call_Next_When_Validation_Passes()
    {
        var validator = new TestBehaviorRequestValidator();
        var behavior = new ValidationBehavior<TestBehaviorRequest, string>(new[] { validator });
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns("ok");

        var result = await behavior.Handle(new TestBehaviorRequest("valid"), next, CancellationToken.None);

        result.Should().Be("ok");
        await next.Received(1)();
    }

    [Fact]
    public async Task Should_Throw_ValidationException_When_Validation_Fails()
    {
        var validator = new TestBehaviorRequestValidator();
        var behavior = new ValidationBehavior<TestBehaviorRequest, string>(new[] { validator });
        var next = Substitute.For<RequestHandlerDelegate<string>>();

        var act = () => behavior.Handle(new TestBehaviorRequest(""), next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await next.DidNotReceive()();
    }
}
