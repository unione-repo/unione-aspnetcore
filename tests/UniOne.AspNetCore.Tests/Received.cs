using System.Collections.Concurrent;

namespace UniOne.AspNetCore.Tests;

/// <summary>What the webhook handlers got: the notification, then each event.</summary>
public sealed class Received
{
    public ConcurrentQueue<object> Items { get; } = new();
}
