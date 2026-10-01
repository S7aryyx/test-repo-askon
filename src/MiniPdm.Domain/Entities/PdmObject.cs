using MiniPdm.Domain.Enums;

namespace MiniPdm.Domain.Entities;

public class PdmObject
{
    public Guid Id { get; set; }
    public ObjectType ObjectType { get; set; }
    public string? Designation { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? CurrentVersionId { get; set; }
}
