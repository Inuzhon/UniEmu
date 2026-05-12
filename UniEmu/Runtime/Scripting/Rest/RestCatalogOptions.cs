namespace UniEmu.Runtime.Scripting.Rest;

internal sealed class RestCatalogOptions : Dictionary<string, RestClientOptions>
{
    public const string SectionName = "UniEmu:RestCatalog";
}

internal sealed class RestClientOptions
{
    public string? BaseUrl { get; init; }

    public int TimeoutSeconds { get; init; } = 5;

    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, RestOperationOptions> Operations { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class RestOperationOptions
{
    public string? Method { get; init; }

    public string? Path { get; init; }

    public string? Response { get; init; }
}
