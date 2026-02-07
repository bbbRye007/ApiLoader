namespace Canal.Ingestion.ApiLoader.Model;

/// <summary>
/// Controls when fetched pages are persisted to storage during a Load() call.
/// </summary>
public enum SaveBehavior
{
    /// <summary>Persist all results after every page has been fetched (default).</summary>
    AfterAll,

    /// <summary>Persist each page to storage as soon as it is fetched.</summary>
    PerPage,

    /// <summary>Do not persist results to storage.</summary>
    None
}
