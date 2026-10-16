using System.Threading;
using System.Threading.Tasks;
using UniOne.Api.Webhook.Model;

namespace UniOne.Wrapper;

/// <summary>
/// Handler of the webhook notifications with a valid signature. Must be fast: the API waits for 200 OK for
/// 3 seconds only. Both methods do nothing by default.
/// </summary>
public interface IUniOneWebhookHandler
{
    /// <summary>The whole notification.</summary>
    Task HandleAsync(WebhookCallback callback, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>One event of the notification (email status, spam block...) and its user/project.</summary>
    Task HandleEventAsync(WebhookCallbackEventsByUserItemEventsItem e, WebhookCallbackEventsByUserItem user, CancellationToken cancellationToken) => Task.CompletedTask;
}
