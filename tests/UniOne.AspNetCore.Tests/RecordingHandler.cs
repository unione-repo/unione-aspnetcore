using System.Threading;
using System.Threading.Tasks;
using UniOne.Api.Webhook.Model;
using UniOne.Wrapper;

namespace UniOne.AspNetCore.Tests;

/// <summary>The webhook handler of the tests: puts what it gets into <see cref="Received"/>.</summary>
public sealed class RecordingHandler : IUniOneWebhookHandler
{
    private readonly Received _received;

    public RecordingHandler(Received received)
    {
        _received = received;
    }

    public Task HandleAsync(WebhookCallback callback, CancellationToken cancellationToken)
    {
        _received.Items.Enqueue(callback);
        return Task.CompletedTask;
    }

    public Task HandleEventAsync(
        WebhookCallbackEventsByUserItemEventsItem e,
        WebhookCallbackEventsByUserItem user,
        CancellationToken cancellationToken)
    {
        _received.Items.Enqueue(e);
        return Task.CompletedTask;
    }
}
