namespace MiniPdm.Domain.Models;

public class BomNode
{
    public Guid ObjectId { get; set; }
    public Guid VersionId { get; set; }
    public Guid? ParentObjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Level { get; set; }
    public Guid[] Path { get; set; } = Array.Empty<Guid>();
}

public class SpecificationRow
{
    public Guid ObjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal? MassKg { get; set; }
    public decimal? TotalMassKg => MassKg.HasValue ? MassKg.Value * Quantity : null;
}

public class MassCalculationResult
{
    public bool Success { get; set; }
    public decimal? MassKg { get; set; }
    public List<string> MissingMassItems { get; } = new();
    public string? Error { get; set; }
}
