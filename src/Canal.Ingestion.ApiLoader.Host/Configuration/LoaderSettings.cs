namespace Canal.Ingestion.ApiLoader.Host.Configuration;

public sealed class LoaderSettings
{
    public string Environment { get; set; } = "UNDEFINED";
    public int MaxRetries { get; set; } = 5;
    public int MinRetryDelayMs { get; set; } = 100;
    public int MaxDop { get; set; } = 8;
    public string SaveBehavior { get; set; } = "PerPage";
    public bool SaveWatermark { get; set; } = true;
    public int LookbackDays { get; set; } = 90;
    public string Storage { get; set; } = "adls";
    public string LocalStoragePath { get; set; } = "./ingestion-output";
}
