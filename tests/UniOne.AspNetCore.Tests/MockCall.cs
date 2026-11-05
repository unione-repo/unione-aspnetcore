namespace UniOne.AspNetCore.Tests;

/// <summary>A request of the client as the mock got it.</summary>
public sealed record MockCall(string Path, string Platform, string ApiKey);
