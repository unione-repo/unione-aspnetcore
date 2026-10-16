using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniOne.Api.Webhook;
using UniOne.Api.Webhook.Model;
using UniOne.Client;

namespace UniOne.Wrapper;

/// <summary>Webhook endpoint of the UniOne API.</summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the webhook endpoint: GET/HEAD -> 200 (URL check of webhook/set), POST -> signature check, then every
    /// <see cref="IUniOneWebhookHandler"/>; 401 on a wrong signature, 400 on a body that is not a notification.
    /// </summary>
    /// <param name="endpoints">Application or route group.</param>
    /// <param name="path">Path of the endpoint; null: UniOne:Webhook:Path (/unione/webhook).</param>
    public static IEndpointConventionBuilder MapUniOneWebhook(this IEndpointRouteBuilder endpoints, string? path = null)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<UniOneOptions>>().Value;
        return endpoints.MapMethods(path ?? options.Webhook.Path, new[] { "GET", "HEAD", "POST" }, (Delegate)HandleAsync);
    }

    private static async Task<IResult> HandleAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return Results.Text("OK");
        }

        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted);
        }
        var options = context.RequestServices.GetRequiredService<IOptions<UniOneOptions>>().Value;
        var key = string.IsNullOrEmpty(options.Webhook.Key) ? options.ApiKey : options.Webhook.Key;
        if (!WebhookSignature.Verify(body, key ?? ""))
        {
            return Results.Text("Invalid signature", statusCode: StatusCodes.Status401Unauthorized);
        }

        WebhookCallback callback;
        try
        {
            callback = WebhookApi.ParseCallback(body);
        }
        catch (Exception e) when (e is JsonException or ApiException)
        {
            return Results.Text("Invalid body", statusCode: StatusCodes.Status400BadRequest);
        }

        var handlers = context.RequestServices.GetServices<IUniOneWebhookHandler>().ToList();
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(callback, context.RequestAborted);
        }
        foreach (var user in callback.EventsByUser ?? new())
        {
            foreach (var e in user.Events ?? new())
            {
                foreach (var handler in handlers)
                {
                    await handler.HandleEventAsync(e, user, context.RequestAborted);
                }
            }
        }
        return Results.Text("OK");
    }
}
