using Danatadbir.Application.IngestionService.Dtos;
using Danatadbir.Domain.Entities;

namespace Danatadbir.Infrastructure.Services.Ingestion;

public readonly record struct ParseOutcome(SensorData? Reading, RejectionReason? Reason, string Detail)
{
    public bool IsParsed => Reading is not null;

    public static ParseOutcome Parsed(SensorData reading) => new(reading, null, string.Empty);

    public static ParseOutcome Rejected(RejectionReason reason, string detail) => new(null, reason, detail);
}
