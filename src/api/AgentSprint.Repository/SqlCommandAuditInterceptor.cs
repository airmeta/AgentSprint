using Microsoft.EntityFrameworkCore.Diagnostics;

using System.Data.Common;

namespace AgentSprint.Repository;

public sealed class SqlCommandAuditInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}

