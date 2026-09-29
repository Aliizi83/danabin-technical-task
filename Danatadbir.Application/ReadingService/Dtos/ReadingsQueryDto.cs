namespace Danatadbir.Application.ReadingService.Dtos;

public record ReadingsQueryDto(
    string? DeviceId,
    string? Metric,
    DateTime From,
    DateTime To,
    int Page = 1,
    int PageSize = 100);
