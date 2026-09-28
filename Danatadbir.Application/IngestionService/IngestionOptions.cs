namespace Danatadbir.Application.IngestionService;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public string DefaultFilePath { get; set; } = "Data/readings.jsonl";

    public int WriteBatchSize { get; set; } = 500;

    public int RejectionSampleLimit { get; set; } = 20;
}
