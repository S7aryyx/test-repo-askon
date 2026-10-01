namespace MiniPdm.Domain.Entities;

public class BomLink
{
    public Guid Id { get; set; }
    public Guid ParentVersionId { get; set; }
    public Guid ChildObjectId { get; set; }
    public int Quantity { get; set; }
}
