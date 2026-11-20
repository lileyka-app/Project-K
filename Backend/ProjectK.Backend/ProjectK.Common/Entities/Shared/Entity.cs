namespace ProjectK.Common.Entities.Shared;

public abstract class Entity
{
    protected Entity()
    {
        CreatedDate = UpdatedDate = DateTime.UtcNow;
    }
    public virtual DateTime CreatedDate { get; set; }
    public virtual DateTime UpdatedDate { get; set; }
}
