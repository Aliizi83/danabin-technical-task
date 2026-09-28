using Danatadbir.Domain.Entities.Common;

namespace Danatadbir.Domain.Entities;

public class Metric : BaseEntity
{
    public string Key { get; set; } = default!;

    public string Title { get; set; } = default!;

    public string? Unit { get; set; }
}
