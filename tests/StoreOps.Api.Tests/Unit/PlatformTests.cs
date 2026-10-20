using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StoreOps.Api.Common.Errors;
using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Tests.Unit;

public sealed class PlatformTests
{
    [Fact]
    public async Task EventBus_FailingSubscriber_DoesNotStopOtherSubscribers()
    {
        var recorder = new RecordingSubscriber();
        using var provider = new ServiceCollection()
            .AddSingleton<IEventSubscriber<TestEvent>>(new FailingSubscriber())
            .AddSingleton<IEventSubscriber<TestEvent>>(recorder)
            .BuildServiceProvider();
        var bus = new InMemoryEventBus(provider, NullLogger<InMemoryEventBus>.Instance);

        await bus.PublishAsync(new TestEvent(DateTimeOffset.UnixEpoch));

        Assert.Equal(1, recorder.Count);
    }

    [Fact]
    public async Task ExceptionHandler_UnknownException_Returns500WithoutLeakingDetails()
    {
        var handler = new AppErrorExceptionHandler(NullLogger<AppErrorExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("secret connection string"), CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Contains(ErrorCodes.Internal, body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", body, StringComparison.Ordinal);
    }

    [Fact]
    public void BindingFailure_WithoutParameterName_UsesGenericMessage()
    {
        var error = AppErrorExceptionHandler.FromBindingFailure(new BadHttpRequestException("Something odd."));

        Assert.Equal("The request is invalid.", error.Message);
    }

    private sealed record TestEvent(DateTimeOffset OccurredAt) : IIntegrationEvent;

    private sealed class FailingSubscriber : IEventSubscriber<TestEvent>
    {
        public Task HandleAsync(TestEvent integrationEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("subscriber failure");
    }

    private sealed class RecordingSubscriber : IEventSubscriber<TestEvent>
    {
        public int Count { get; private set; }

        public Task HandleAsync(TestEvent integrationEvent, CancellationToken cancellationToken)
        {
            Count++;
            return Task.CompletedTask;
        }
    }
}
