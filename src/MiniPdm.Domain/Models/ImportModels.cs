namespace MiniPdm.Domain.Models;

public enum ImportSeverity
{
    Accepted,
    Warning,
    Error
}

public class ImportItem
{
    public string FileName { get; set; } = string.Empty;
    public ImportSeverity Severity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ImportResult
{
    public List<ImportItem> Items { get; } = new();
    public int AcceptedCount => Items.Count(x => x.Severity == ImportSeverity.Accepted);
    public int RejectedCount => Items.Count(x => x.Severity == ImportSeverity.Error);
    public int WarningCount => Items.Count(x => x.Severity == ImportSeverity.Warning);
}

