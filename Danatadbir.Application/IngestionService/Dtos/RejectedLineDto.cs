namespace Danatadbir.Application.IngestionService.Dtos;

public record RejectedLineDto(int LineNumber, RejectionReason Reason, string Detail);
