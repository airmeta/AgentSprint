using Air.Cloud.Core.Standard.DataBase.Model;

namespace AgentSprint.Model.Modules.Common;

public abstract class EntityBase : IEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public DateTime? UpdateTime { get; set; }

    public int IsDelete { get; set; }
}

