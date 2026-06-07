using System.Linq.Expressions;

using AgentSprint.Model.Modules.Common;
using AgentSprint.Model.Modules.Security.Domains;

using Air.Cloud.Core.Attributes;
using Air.Cloud.EntityFrameWork.Core.Repositories;

using Microsoft.EntityFrameworkCore;

namespace AgentSprint.Domain.Impls.Common;

[IgnoreScanning]
public abstract class EntityDomainBase<TEntity> : IEntityDomainBase<TEntity>
    where TEntity : EntityBase, new()
{
    protected EntityDomainBase(IRepository<TEntity> repository)
    {
        Repository = repository;
    }

    protected IRepository<TEntity> Repository { get; }

    public virtual async Task<string> CreateAsync(TEntity entity)
    {
        await Repository.InsertAsync(entity);
        return entity.Id;
    }

    public virtual Task<TEntity?> GetAsync(string id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? Task.FromResult<TEntity?>(null)
            : Repository.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsDelete == 0);
    }

    public virtual async Task<IList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null)
    {
        IQueryable<TEntity> query = Repository.DetachedEntities.Where(entity => entity.IsDelete == 0);
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync();
    }

    /// <summary>
    /// zh-cn: 查询实体集合且不自动过滤软删除记录，主要用于维护唯一索引约束下的关系恢复和去重；调用方必须显式处理 IsDelete 状态，避免把已删除数据暴露给普通业务读接口。
    /// en-us: Lists entities without automatically filtering soft-deleted records, primarily for restoring and deduplicating unique-indexed maintenance relations; callers must explicitly handle IsDelete so deleted data is not exposed through normal business reads.
    /// </summary>
    /// <param name="predicate">
    /// zh-cn: 可选过滤条件，会直接应用在包含软删除记录的查询上。
    /// en-us: Optional predicate applied to the query that includes soft-deleted records.
    /// </param>
    /// <returns>
    /// zh-cn: 返回包含匹配软删除记录在内的实体集合。
    /// en-us: Returns matching entities including soft-deleted records.
    /// </returns>
    public virtual async Task<IList<TEntity>> ListIncludingDeletedAsync(Expression<Func<TEntity, bool>>? predicate = null)
    {
        IQueryable<TEntity> query = Repository.DetachedEntities;
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync();
    }

    public virtual async Task<string> UpdateAsync(TEntity entity)
    {
        entity.UpdateTime = DateTime.UtcNow;
        await Repository.UpdateAsync(entity);
        return entity.Id;
    }

    public virtual async Task<bool> DeleteAsync(string id)
    {
        var entity = await GetAsync(id);
        if (entity is null)
        {
            return true;
        }

        entity.IsDelete = 1;
        entity.UpdateTime = DateTime.UtcNow;
        await Repository.UpdateIncludeAsync(entity, [nameof(entity.IsDelete), nameof(entity.UpdateTime)]);
        return true;
    }
}
