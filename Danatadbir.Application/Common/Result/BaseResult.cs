using System.Net;

namespace Danatadbir.Application.Common.Result;

public record BaseResult
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public int StatusCode { get; init; } = (int)HttpStatusCode.OK;
    public PaginationMetaData? PaginationMetaData { get; init; }

    public static BaseResult Ok(string? message = null) =>
        new() { Success = true, Message = message, StatusCode = (int)HttpStatusCode.OK };

    public static BaseResult Failure(HttpStatusCode statusCode, string message) =>
        new() { Success = false, Message = message, StatusCode = (int)statusCode };
}

public record BaseResult<T> : BaseResult
{
    public T? Data { get; init; }

    public static BaseResult<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message, StatusCode = (int)HttpStatusCode.OK };

    public static BaseResult<T> Ok(T data, PaginationMetaData paginationMetaData) =>
        new()
        {
            Success = true,
            Data = data,
            StatusCode = (int)HttpStatusCode.OK,
            PaginationMetaData = paginationMetaData
        };

    public static new BaseResult<T> Failure(HttpStatusCode statusCode, string message) =>
        new() { Success = false, Message = message, StatusCode = (int)statusCode };
}
