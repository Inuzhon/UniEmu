namespace UniEmu.Hosting;

/// <summary>
/// Strongly typed settings from the UniEmu configuration section.
/// </summary>
public sealed class UniEmuOptions
{
    public const string SectionName = "UniEmu";

    public bool DisableRuntime { get; set; }

    public bool SkipStartupDatabase { get; set; }

    public bool SeedData { get; set; }

    public bool DisableStaticAssets { get; set; }

    public bool EnableStaticAssetCompression { get; set; }

    public bool EnableStaticAssetCaching { get; set; }

    public int DispatcherBlockCheckIntervalSeconds
    {
        get;
        set => field = Math.Max(1, value);
    } = 5;
}
