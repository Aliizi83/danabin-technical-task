namespace Danatadbir.Application.ReadingService.Dtos;

/// <param name="AcceptableReadings">Readings the buckets were computed from.</param>
/// <param name="ExcludedUnacceptable">Readings in the range left out because a rule was violated.</param>
public record AggregationResultDto(
    string DeviceId,
    string Metric,
    DateTime From,
    DateTime To,
    int BucketSeconds,
    int AcceptableReadings,
    int ExcludedUnacceptable,
    IReadOnlyList<BucketDto> Buckets);
