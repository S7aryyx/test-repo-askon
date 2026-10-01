using MiniPdm.Domain.Enums;

namespace MiniPdm.Domain.Entities;

public class ObjectVersion
{
    public Guid Id { get; set; }
    public Guid ObjectId { get; set; }
    public int VersionNo { get; set; }
    public ObjectState State { get; set; }
    public string? Material { get; set; }
    public decimal? MassKg { get; set; }
    public DateTime CreatedAt { get; set; }
}
