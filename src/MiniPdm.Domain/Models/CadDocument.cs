using MiniPdm.Domain.Enums;

namespace MiniPdm.Domain.Models;

public class CadDocument
{
    public int FormatVersion { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ObjectType Type { get; set; }
    public string? Designation { get; set; }
    public string Name { get; set; } = string.Empty;
    public CadProperties Properties { get; set; } = new();
    public List<CadComponent> Components { get; set; } = new();
}

public class CadProperties
{
    public string? Material { get; set; }
    public decimal? Mass { get; set; }
}

public class CadComponent
{
    public string File { get; set; } = string.Empty;
    public int Count { get; set; }
}
