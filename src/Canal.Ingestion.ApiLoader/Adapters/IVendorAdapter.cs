using System.Net;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Adapters;

public interface IVendorAdapter
{
    string IngestionDomain { get; } // ie "Telematics"
    string VendorName { get; } // ie "TruckerCloud"
    string BaseUrl { get; } // ie "https://api.truckercloud.com/api/"

    bool IsExternalSource { get; } // ie "true" because Trucker Cloud Apis are considered "an external source of data"
    HttpClient HttpClient { get; init; }

    Uri BuildRequestUri(Request request);


    string ComputeRequestId(Request request);
    string ComputeAttemptId(Request request, int attemptNr);

    // used for metadata/identity; not owned by FetchService.
    string ComputePageId(Request request, int pageNr);

    Task ApplyRequestHeadersAsync(HttpRequestMessage httpRequest, Request request, CancellationToken cancellationToken);

    void PostProcessSuccessfulResponse(FetchResult result);

    FetchStatus RefineFetchOutcome(Request request, HttpStatusCode? statusCode, string content, string? contentType, FetchStatus currentOutcome);

    string BuildFailureMessage(HttpStatusCode? statusCode, string? reasonPhrase, FetchStatus outcome, string content, Exception? ex = null);

    /// <summary>
    /// Vendor-owned metadata serialization policy (redaction, empties, etc).
    /// </summary>
    string BuildMetaDataJson(FetchResult result);

    /// <summary>
    /// Vendor-owned request sequencing (paging/cursors/continuations/etc).
    /// </summary>
    ValueTask<Request?> GetNextRequestAsync(Request seedRequest, FetchResult? previousResult, int stepNr, CancellationToken cancellationToken);

    string ResourceNameFriendly(string resourceName);
}
