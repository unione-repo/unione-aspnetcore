using System;
using System.Globalization;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UniOne.Client;

namespace UniOne.Wrapper;

/// <summary>Registration of the UniOne API client.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="UniOne.UniOneClient"/> as a singleton, configured from the section "UniOne" of the
    /// configuration and the UNIONE_* environment variables.
    /// </summary>
    public static UniOneBuilder AddUniOne(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<UniOneOptions>().Bind(configuration.GetSection(UniOneOptions.Section));
        return services.AddUniOne(_ => { });
    }

    /// <summary>Registers <see cref="UniOne.UniOneClient"/> as a singleton configured in code.</summary>
    public static UniOneBuilder AddUniOne(this IServiceCollection services, Action<UniOneOptions> configure)
    {
        services.AddOptions<UniOneOptions>().Configure(configure).PostConfigure(FromEnvironment);
        services.TryAddSingleton(static sp =>
        {
            var options = sp.GetRequiredService<IOptions<UniOneOptions>>().Value;
            if (string.IsNullOrEmpty(options.ApiKey))
            {
                throw new InvalidOperationException("UniOne: set UniOne:ApiKey in the configuration or UNIONE_API_KEY.");
            }
            var timeout = TimeSpan.FromSeconds(options.Timeout);
            var config = new Configuration(options.ApiKey)
            {
                BaseUrl = string.IsNullOrEmpty(options.Host) ? Configuration.DefaultBaseUrl : Configuration.BaseUrlFor(options.Host),
                Timeout = timeout,
                Platform = "aspnetcore.2.1.3",
            };
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
            {
                Timeout = timeout,
            };
            return new UniOne.UniOneClient(config, http);
        });
        return new UniOneBuilder(services);
    }

    private static void FromEnvironment(UniOneOptions options)
    {
        static string? Env(string name)
        {
            var value = Environment.GetEnvironmentVariable("UNIONE_" + name);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            options.ApiKey = Env("API_KEY");
        }
        if (string.IsNullOrEmpty(options.Host))
        {
            options.Host = Env("HOST");
        }
        if (Env("TIMEOUT") is { } timeout && double.TryParse(timeout, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            options.Timeout = seconds;
        }
        if (string.IsNullOrEmpty(options.Webhook.Key))
        {
            options.Webhook.Key = Env("WEBHOOK_KEY");
        }
        if (Env("WEBHOOK_PATH") is { } path)
        {
            options.Webhook.Path = path;
        }
    }
}

/// <summary>Result of <c>AddUniOne()</c>: registers the webhook handlers.</summary>
public class UniOneBuilder
{
    public UniOneBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    /// <summary>Registers a scoped webhook handler; every registered handler gets each notification.</summary>
    public UniOneBuilder AddWebhookHandler<T>() where T : class, IUniOneWebhookHandler
    {
        Services.AddScoped<IUniOneWebhookHandler, T>();
        return this;
    }
}
