using Danatadbir.Application.IngestionService;
using Microsoft.Extensions.Hosting;

namespace Danatadbir.Infrastructure.Services.Ingestion;

public class JsonlFileReadingLineSource(IHostEnvironment environment) : IReadingLineSource
{
    public bool Exists(string path) => File.Exists(Resolve(path));

    public async IAsyncEnumerable<string> ReadLinesAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(Resolve(path));

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
            yield return line;
    }

    private string Resolve(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        var fromContentRoot = Path.Combine(environment.ContentRootPath, path);

        if (File.Exists(fromContentRoot))
            return fromContentRoot;

        var parent = Directory.GetParent(environment.ContentRootPath)?.FullName;

        return parent is null ? fromContentRoot : Path.Combine(parent, path);
    }
}
