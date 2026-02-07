using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Canal.Ingestion.ApiLoader.Model;
public sealed record FetchFailure
{
    [SetsRequiredMembers]
    public FetchFailure(int attemptNr, DateTimeOffset requestedUtc, DateTimeOffset failedUtc, HttpStatusCode? statusCode, string exceptionMessage, string responseBody)
    {
        AttemptNr = attemptNr;
        RequestedUtc = requestedUtc;
        FailedUtc = failedUtc;
        StatusCode = statusCode;
        ExceptionMessage = exceptionMessage;
        ResponseBody = responseBody;
    }
    public required int AttemptNr { get; init;}
    public required DateTimeOffset RequestedUtc { get; init;}
    public required DateTimeOffset FailedUtc { get; init;}
    public required HttpStatusCode? StatusCode { get; init; }
    public required string ExceptionMessage { get; init; } = string.Empty;
    public required string ResponseBody { get; init; } = string.Empty;
}
