using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniOne.Api.Webhook.Model;
using UniOne.Wrapper;
using Xunit;

// the tests change the UNIONE_* environment variables of the process
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace UniOne.AspNetCore.Tests;

/// <summary>
/// Tests of the wrapper: registration from the configuration and the environment, the client of the container
/// against a local mock of the API, the webhook endpoint on a TestServer. No request leaves the machine.
/// </summary>
public sealed class WrapperTests : IDisposable
{
    private const string ApiKey = "test-key";
    private const string Prefix = "UNIONE_";
    private const string Path = "/unione/webhook";

    // events_by_user with two events; "auth" is signed by WebhookSignature.Sign()
    private const string Notification = """
        {
          "auth": "00000000000000000000000000000000",
          "events_by_user": [
            {
              "user_id": 1,
              "project_id": "6432890213745872",
              "events": [
                {
                  "event_name": "transactional_email_status",
                  "event_data": {
                    "job_id": "1a3Q2V-0000OZ-S0",
                    "email": "user@example.com",
                    "status": "delivered",
                    "event_time": "2015-11-30 15:09:42"
                  }
                },
                {
                  "event_name": "transactional_spam_block",
                  "event_data": {
                    "block_time": "2015-11-30 15:09:42",
                    "block_type": "one_smtp",
                    "domain": "example.com",
                    "SMTP_blocks_count": 1,
                    "domain_status": "blocked"
                  }
                }
              ]
            }
          ]
        }
        """;

    private static readonly string[] Variables = { "API_KEY", "HOST", "TIMEOUT", "WEBHOOK_KEY", "WEBHOOK_PATH" };

    private readonly Dictionary<string, string?> _saved = new();

    public WrapperTests()
    {
        // every test starts without the variables of the wrapper
        foreach (var name in Variables)
        {
            _saved[Prefix + name] = Environment.GetEnvironmentVariable(Prefix + name);
            Environment.SetEnvironmentVariable(Prefix + name, null);
        }
    }

    public void Dispose()
    {
        foreach (var variable in _saved)
        {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }
    }

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddUniOne(configuration);
        return services.BuildServiceProvider();
    }

    private static UniOneOptions Options(ServiceProvider provider)
    {
        return provider.GetRequiredService<IOptions<UniOneOptions>>().Value;
    }

    [Fact]
    public void ClientIsASingletonConfiguredFromTheConfiguration()
    {
        using var provider = Services(new()
        {
            ["UniOne:ApiKey"] = ApiKey,
            ["UniOne:Host"] = "http://127.0.0.1:9/api",
        });

        var client = provider.GetRequiredService<UniOneClient>();

        Assert.Same(client, provider.GetRequiredService<UniOneClient>());
        Assert.Equal(ApiKey, Options(provider).ApiKey);
        Assert.Equal("http://127.0.0.1:9/api", Options(provider).Host);
    }

    [Fact]
    public void EnvironmentFillsWhatTheConfigurationDoesNotSet()
    {
        Environment.SetEnvironmentVariable(Prefix + "API_KEY", "env-key");
        Environment.SetEnvironmentVariable(Prefix + "HOST", "http://127.0.0.1:9/env");
        Environment.SetEnvironmentVariable(Prefix + "TIMEOUT", "12");
        Environment.SetEnvironmentVariable(Prefix + "WEBHOOK_KEY", "env-hook-key");
        Environment.SetEnvironmentVariable(Prefix + "WEBHOOK_PATH", "/env/hook");
        using var provider = Services(new());

        var options = Options(provider);

        Assert.Equal("env-key", options.ApiKey);
        Assert.Equal("http://127.0.0.1:9/env", options.Host);
        Assert.Equal(12d, options.Timeout);
        Assert.Equal("env-hook-key", options.Webhook.Key);
        Assert.Equal("/env/hook", options.Webhook.Path);
        Assert.NotNull(provider.GetRequiredService<UniOneClient>());
    }

    [Fact]
    public void ConfigurationWinsOverTheEnvironment()
    {
        foreach (var name in Variables)
        {
            Environment.SetEnvironmentVariable(Prefix + name, name == "TIMEOUT" ? "12" : "/env/" + name);
        }
        using var provider = Services(new()
        {
            ["UniOne:ApiKey"] = "config-key",
            ["UniOne:Host"] = "http://127.0.0.1:9/config",
            ["UniOne:Timeout"] = "5",
            ["UniOne:Webhook:Key"] = "config-hook-key",
            ["UniOne:Webhook:Path"] = "/config/hook",
        });

        var options = Options(provider);

        Assert.Equal("config-key", options.ApiKey);
        Assert.Equal("http://127.0.0.1:9/config", options.Host);
        Assert.Equal(5d, options.Timeout);
        Assert.Equal("config-hook-key", options.Webhook.Key);
        Assert.Equal("/config/hook", options.Webhook.Path);
    }

    [Fact]
    public void MissingApiKeyFailsWhenTheClientIsResolved()
    {
        using var provider = Services(new());

        var e = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<UniOneClient>());

        Assert.Contains(Prefix + "API_KEY", e.Message);
    }

    [Fact]
    public async Task ClientCallsTheConfiguredHostWithThePlatformOfTheWrapper()
    {
        await using var mock = await MockApi.StartAsync();
        using var provider = Services(new()
        {
            ["UniOne:ApiKey"] = ApiKey,
            ["UniOne:Host"] = mock.Url + "/api",
        });

        await provider.GetRequiredService<UniOneClient>().System.PingAsync();

        var call = Assert.Single(mock.Calls);
        Assert.Equal("/api/system/ping.json", call.Path);
        Assert.Equal("aspnetcore.2.3.2,dotnet-sdk.2.3.1", call.Platform);
        Assert.Equal(ApiKey, call.ApiKey);
    }

    private static async Task<WebApplication> WebhookApp(Dictionary<string, string?> settings, bool map = true,
        bool fallbackPolicy = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton<Received>();
        builder.Services.AddUniOne(builder.Configuration).AddWebhookHandler<RecordingHandler>();
        if (fallbackPolicy)
        {
            // every endpoint needs an authenticated user, unless it allows anonymous requests
            builder.Services.AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        }

        var app = builder.Build();
        if (fallbackPolicy)
        {
            app.UseAuthorization();
        }

        if (map)
        {
            app.MapUniOneWebhook();
        }

        await app.StartAsync();
        return app;
    }

    private static Dictionary<string, string?> Settings(string? webhookKey = null, string? path = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["UniOne:ApiKey"] = ApiKey,
            ["UniOne:Host"] = "http://127.0.0.1:9/api",
        };
        if (webhookKey != null)
        {
            settings["UniOne:Webhook:Key"] = webhookKey;
        }

        if (path != null)
        {
            settings["UniOne:Webhook:Path"] = path;
        }

        return settings;
    }

    private static Task<HttpResponseMessage> Post(WebApplication app, string body, string path = Path)
    {
        return app.GetTestClient().PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"));
    }

    [Fact]
    public async Task WebhookAnswersGet()
    {
        await using var app = await WebhookApp(Settings());

        var response = await app.GetTestClient().GetAsync(Path);

        Assert.Equal(200, (int)response.StatusCode);
    }

    [Fact]
    public async Task SignedNotificationReachesTheHandler()
    {
        await using var app = await WebhookApp(Settings());

        var response = await Post(app, WebhookSignature.Sign(Notification, ApiKey));

        Assert.Equal(200, (int)response.StatusCode);
        var received = app.Services.GetRequiredService<Received>().Items.ToList();
        Assert.Equal(3, received.Count);
        Assert.IsType<WebhookCallback>(received[0]);
        var first = Assert.IsType<WebhookCallbackEventsByUserItemEventsItem>(received[1]);
        Assert.Equal("user@example.com", first.EventData?.Email);
        Assert.IsType<WebhookCallbackEventsByUserItemEventsItem>(received[2]);
    }

    [Fact]
    public async Task WrongSignatureIsRejected()
    {
        await using var app = await WebhookApp(Settings());
        var body = WebhookSignature.Sign(Notification, ApiKey).Replace("user@", "evil@");

        var response = await Post(app, body);

        Assert.Equal(401, (int)response.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<Received>().Items);
    }

    [Fact]
    public async Task SignedBodyThatIsNotANotificationIsABadRequest()
    {
        await using var app = await WebhookApp(Settings());
        var notANotification = "{\"auth\":\"" + WebhookSignature.Placeholder + "\",\"events_by_user\":\"x\"}";
        var body = WebhookSignature.Sign(notANotification, ApiKey);

        var response = await Post(app, body);

        Assert.Equal(400, (int)response.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<Received>().Items);
    }

    [Fact]
    public async Task WebhookKeyIsUsedInsteadOfTheApiKey()
    {
        await using var app = await WebhookApp(Settings(webhookKey: "hook-key"));

        var signed = await Post(app, WebhookSignature.Sign(Notification, "hook-key"));
        var withApiKey = await Post(app, WebhookSignature.Sign(Notification, ApiKey));

        Assert.Equal(200, (int)signed.StatusCode);
        Assert.Equal(401, (int)withApiKey.StatusCode);
    }

    [Fact]
    public async Task WebhookIsAbsentUntilMapped()
    {
        await using var app = await WebhookApp(Settings(), map: false);

        var response = await app.GetTestClient().GetAsync(Path);

        Assert.Equal(404, (int)response.StatusCode);
    }

    [Fact]
    public async Task WebhookOnTheConfiguredPathIsOpenUnderAFallbackPolicy()
    {
        await using var app = await WebhookApp(Settings(path: "/custom/hook"), fallbackPolicy: true);

        var get = await app.GetTestClient().GetAsync("/custom/hook");
        var post = await Post(app, WebhookSignature.Sign(Notification, ApiKey), "/custom/hook");

        Assert.Equal(200, (int)get.StatusCode);
        Assert.Equal(200, (int)post.StatusCode);
    }
}
