namespace Danatadbir.Infrastructure.Options;

public class InfluxDbOptions
{
    public const string SectionName = "InfluxDb";

    public string Url { get; set; } = default!;
    public string Token { get; set; } = default!;
    public string Organization { get; set; } = default!;
    public string Bucket { get; set; } = default!;
}
