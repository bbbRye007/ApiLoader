using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Canal.Ingestion.ApiLoader.Adapters;

namespace Canal.Ingestion.ApiLoader.Model;

public enum FetchStatus { NotAttempted, Success, RetryImmediately, RetryTransient, FailPermanent }

public sealed class FetchResult
{
    [SetsRequiredMembers]
    public FetchResult(IVendorAdapter vendorAdapter, IngestionRun ingestionRun, Request request)
    {
        IngestionRun = ingestionRun;
        Request = request;
        Failures = [];
        VendorAdapter = vendorAdapter;
    }

    public readonly IVendorAdapter VendorAdapter;
    public required IngestionRun IngestionRun { get; init; }
    public required Request Request { get; init; }

    public List<FetchFailure> Failures { get; init; }

    public FetchStatus FetchOutcome { get; set; } = FetchStatus.NotAttempted;
    public bool FetchSucceeded => FetchOutcome == FetchStatus.Success;

    public int NrAttempts => Failures.Count + (FetchSucceeded ? 1 : 0);

    public int? TotalPages { get; set; }
    public int PageNr { get; set; } = 1;
    public int? TotalElements { get; set; }
    public int? PageSize { get; set; }

    /// <summary>
    /// Opaque continuation state set by the adapter after a successful fetch.
    /// For cursor/token APIs: the cursor string. For next-URL APIs: the next URL.
    /// Null means "no more pages" (page-number/offset adapters that use math can leave this null).
    /// </summary>
    public string? ContinuationToken { get; set; }

    public string PageId => VendorAdapter.ComputePageId(Request, PageNr);

    public DateTimeOffset? RequestedUtc { get; set; }
    public DateTimeOffset? ReceivedUtc { get; set; }

    public Uri? RequestUri { get; set; }

    public long? ResponseTimeMs
        => ReceivedUtc == default || RequestedUtc == default ? null
            : (long)Math.Max(0d, (ReceivedUtc.GetValueOrDefault() - RequestedUtc.GetValueOrDefault()).TotalMilliseconds);

    private readonly object _payloadLock = new();
    private string _content = string.Empty;
    private long? _payloadBytesCache;
    private string? _payloadSha256Cache;

    public string Content
    {
        get => _content;
        set
        {
            _content = value ?? string.Empty;

            // Content is mutated during retry loops; cached payload metrics must follow.
            _payloadBytesCache = null;
            _payloadSha256Cache = null;
        }
    }

    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }

    public HttpStatusCode? HttpStatusCode { get; set; }
    public int AttemptNr { get; set; }

    public IReadOnlyDictionary<string, string> EffectiveRequestHeaders { get; set; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Kept for convenience, but avoid using this in hot paths for large payloads.
    public byte[] PayloadAsBytes => Encoding.UTF8.GetBytes(Content);

    public long PayloadBytes
    {
        get
        {
            EnsurePayloadMetrics();
            return _payloadBytesCache ?? 0L;
        }
    }

    public string? PayloadSha256
    {
        get
        {
            EnsurePayloadMetrics();
            return _payloadSha256Cache;
        }
    }

    private void EnsurePayloadMetrics()
    {
        if (_payloadBytesCache is not null && _payloadSha256Cache is not null) return;

        lock (_payloadLock)
        {
            if (_payloadBytesCache is not null && _payloadSha256Cache is not null) return;

            // Compute UTF-8 byte length + SHA-256 without materializing a full byte[] for the whole payload.
            var encoder = Encoding.UTF8.GetEncoder();
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            long totalBytes = 0;
            var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);

            try
            {
                var s = _content ?? string.Empty;
                var span = s.AsSpan();

                while (!span.IsEmpty)
                {
                    encoder.Convert(span, buffer, flush: false, out var charsUsed, out var bytesUsed, out _);
                    if (bytesUsed > 0)
                    {
                        hasher.AppendData(buffer.AsSpan(0, bytesUsed));
                        totalBytes += bytesUsed;
                    }

                    span = span.Slice(charsUsed);
                }

                // Flush any encoder state.
                encoder.Convert(ReadOnlySpan<char>.Empty, buffer, flush: true, out _, out var finalBytesUsed, out _);
                if (finalBytesUsed > 0)
                {
                    hasher.AppendData(buffer.AsSpan(0, finalBytesUsed));
                    totalBytes += finalBytesUsed;
                }

                _payloadBytesCache = totalBytes;
                _payloadSha256Cache = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public string MetaDataJson => VendorAdapter.BuildMetaDataJson(this);
}
