namespace Canal.Ingestion.ApiLoader.Hosting.Configuration;

public sealed class LoaderSettings
{
    public string Environment { get; set; } = "UNDEFINED";
    public int MaxRetries { get; set; } = 5;
    public int MinRetryDelayMs { get; set; } = 100;
    public int MaxDop { get; set; } = 8;
    public string SaveBehavior { get; set; } = "PerPage";
    public bool SaveWatermark { get; set; } = true;
    /// <summary>
    /// Reserved: not currently wired into the Hosting layer. EndpointLoader reads
    /// EndpointDefinition.DefaultLookbackDays instead. Retained for future override
    /// scenarios and configuration discoverability.
    /// </summary>
    public int LookbackDays { get; set; } = 90;
    public string Storage { get; set; } = "adls";
    public string LocalStoragePath { get; set; } = @"C:\Temp\ApiLoaderOutput";

    /// <summary>
    /// Creates a shallow copy so CLI overrides do not mutate the shared instance.
    /// </summary>
    public LoaderSettings Snapshot() => new()
    {
        Environment = Environment,
        MaxRetries = MaxRetries,
        MinRetryDelayMs = MinRetryDelayMs,
        MaxDop = MaxDop,
        SaveBehavior = SaveBehavior,
        SaveWatermark = SaveWatermark,
        LookbackDays = LookbackDays,
        Storage = Storage,
        LocalStoragePath = LocalStoragePath
    };
}
