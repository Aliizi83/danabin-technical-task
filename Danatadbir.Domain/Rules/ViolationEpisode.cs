namespace Danatadbir.Domain.Rules;
public record ViolationEpisode(DateTime StartTs, DateTime EndTs, double PeakValue, int ReadingCount)
{
    public TimeSpan Duration => EndTs - StartTs;
}
