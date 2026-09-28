namespace Danatadbir.Application.IngestionService.Dtos;

public enum RejectionReason
{
    Malformed,
    InvalidField,
    UnknownSensorOrMetric,
    Duplicate
}
