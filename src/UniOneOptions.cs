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

    private double _timeout = 30;

    /// <summary>Request timeout, seconds.</summary>
    public double Timeout
    {
        get => _timeout;
        set
        {
            _timeout = value;
            TimeoutSet = true;
        }
    }

    /// <summary>Timeout was set (configuration or code): UNIONE_TIMEOUT is not used.</summary>
    internal bool TimeoutSet { get; private set; }

    /// <summary>Webhook endpoint settings.</summary>
    public WebhookOptions Webhook { get; set; } = new();

    /// <summary>Webhook endpoint settings.</summary>
    public class WebhookOptions
    {
        /// <summary>API key the notifications are signed with; null: <see cref="ApiKey"/>.</summary>
        public string? Key { get; set; }

        private string _path = "/unione/webhook";

        /// <summary>Path of the endpoint.</summary>
        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                PathSet = true;
            }
        }

        /// <summary>Path was set (configuration or code): UNIONE_WEBHOOK_PATH is not used.</summary>
        internal bool PathSet { get; private set; }
    }
}
