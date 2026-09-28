using Danatadbir.Domain.Entities.Common;

namespace Danatadbir.Domain.Entities;

public class Sensor : BaseEntity
{
    public string ExternalId { get; set; } = default!;

    public string Title { get; set; } = default!;
}
