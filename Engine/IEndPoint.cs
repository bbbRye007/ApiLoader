namespace Canal.Ingestion.ApiLoader.Engine;
internal interface IEndpoint
{
    string ResourceName { get; } // ie "carriers"
    int ResourceVersion { get; } // ie 4
}
