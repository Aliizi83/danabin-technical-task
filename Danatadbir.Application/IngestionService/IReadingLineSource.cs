namespace Danatadbir.Application.IngestionService;

public interface IReadingLineSource
{
    bool Exists(string path);

    IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken = default);
}
