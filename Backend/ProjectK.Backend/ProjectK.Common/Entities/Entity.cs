namespace ProjectK.Common.Entities;

public abstract class Entity
{
    protected Entity()
    {
        CreatedDate = UpdatedDate = DateTime.UtcNow;
    }
    public virtual DateTime CreatedDate { get; set; }
    public virtual DateTime UpdatedDate { get; set; }
}
