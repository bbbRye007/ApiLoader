namespace Canal.Ingestion.ApiLoader.Hosting.Configuration;

public sealed class LoaderSettings
{
    public string Environment { get; set; } = "UNDEFINED";
    public int MaxRetries { get; set; } = 5;
    public int MinRetryDelayMs { get; set; } = 100;
    public int MaxDop { get; set; } = 8;
    public string SaveBehavior { get; set; } = "PerPage";
    public bool SaveWatermark { get; set; } = true;
    public string Storage { get; set; } = "adls";
    /// <summary>
    /// Root folder for local file storage. Relative paths resolve against the working directory.
    /// </summary>
    public string LocalStoragePath { get; set; } = "ApiLoaderOutput";

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
        Storage = Storage,
        LocalStoragePath = LocalStoragePath
    };
}
