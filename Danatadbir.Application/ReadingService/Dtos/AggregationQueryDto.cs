namespace Danatadbir.Application.ReadingService.Dtos;

public record AggregationQueryDto(string? DeviceId, string? Metric, DateTime From, DateTime To, int BucketSeconds);
