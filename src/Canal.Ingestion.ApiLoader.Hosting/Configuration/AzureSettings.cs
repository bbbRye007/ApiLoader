namespace Canal.Ingestion.ApiLoader.Hosting.Configuration;

public sealed class AzureSettings
{
    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Validates that all required fields are non-empty. Call before constructing ADLS credentials.
    /// </summary>
    public void Validate()
    {
        List<string>? missing = null;
        if (string.IsNullOrWhiteSpace(AccountName)) (missing ??= []).Add(nameof(AccountName));
        if (string.IsNullOrWhiteSpace(ContainerName)) (missing ??= []).Add(nameof(ContainerName));
        if (string.IsNullOrWhiteSpace(TenantId)) (missing ??= []).Add(nameof(TenantId));
        if (string.IsNullOrWhiteSpace(ClientId)) (missing ??= []).Add(nameof(ClientId));
        if (string.IsNullOrWhiteSpace(ClientSecret)) (missing ??= []).Add(nameof(ClientSecret));

        if (missing is not null)
            throw new InvalidOperationException(
                $"ADLS storage requires Azure settings. Missing or empty: {string.Join(", ", missing)}. " +
                "Set these in appsettings.json under the 'Azure' section or via environment variables.");
    }
}
