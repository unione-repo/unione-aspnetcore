using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace UniOne.AspNetCore.Tests;

/// <summary>Mock of the API on 127.0.0.1 (a free port): answers every method with a success.</summary>
public sealed class MockApi : IAsyncDisposable
{
    private readonly WebApplication _app;

    private MockApi(WebApplication app)
    {
        _app = app;
    }

    public ConcurrentQueue<MockCall> Calls { get; } = new();

    public string Url => _app.Urls.First();

    public static async Task<MockApi> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var mock = new MockApi(app);
        app.Urls.Add("http://127.0.0.1:0");
        app.Run(async context =>
        {
            var request = context.Request;
            mock.Calls.Enqueue(new MockCall(
                request.Path.Value ?? "",
                request.Query["platform"].ToString(),
                request.Headers["X-API-KEY"].ToString()));
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"status":"success"}""");
        });
        await app.StartAsync();
        return mock;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
