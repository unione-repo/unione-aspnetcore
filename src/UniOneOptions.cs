namespace UniOne.Wrapper;

/// <summary>
/// Settings of the UniOne API: section "UniOne" of the configuration; empty values are taken from the
/// environment variables UNIONE_API_KEY, UNIONE_HOST, UNIONE_TIMEOUT, UNIONE_WEBHOOK_KEY, UNIONE_WEBHOOK_PATH.
/// </summary>
public class UniOneOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string Section = "UniOne";

    /// <summary>User or project API key (required).</summary>
    public string? ApiKey { get; set; }

    /// <summary>API host (datacenter) or a full base URL; null: the default one.</summary>
    public string? Host { get; set; }

    /// <summary>Request timeout, seconds.</summary>
    public double Timeout { get; set; } = 30;

    /// <summary>Webhook endpoint settings.</summary>
    public WebhookOptions Webhook { get; set; } = new();

    /// <summary>Webhook endpoint settings.</summary>
    public class WebhookOptions
    {
        /// <summary>API key the notifications are signed with; null: <see cref="ApiKey"/>.</summary>
        public string? Key { get; set; }

        /// <summary>Path of the endpoint.</summary>
        public string Path { get; set; } = "/unione/webhook";
    }
}
