using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AgileServices;
using AgentSprint.Service.Services.SecurityServices;

namespace AgentSprint.Service.Impls.AgileServices;

public sealed class DigitalWorkerManagementService :
    AgentSprintServiceBase,
    IDigitalWorkerManagementService
{
    private readonly IDigitalWorkerDomain _workerDomain;
    private readonly IWorkerSessionDomain _sessionDomain;
    private readonly IWorkerCommandDomain _commandDomain;
    private readonly IWorkerCommandLogDomain _commandLogDomain;
    private readonly IWorkerCommandLogBuffer _commandLogBuffer;
    private readonly IWorkerRunDomain _runDomain;
    private readonly IWorkerEventDomain _eventDomain;
    private readonly ISprintProjectDomain _projectDomain;
    private readonly ISprintDevelopmentTaskDomain _taskDomain;
    private readonly ISprintBugDomain _bugDomain;
    private readonly ISprintTaskLeaseDomain _leaseDomain;
    private readonly IAgentTokenDomain _agentTokenDomain;
    private readonly IDigitalWorkerDeployTemplateDomain _deployTemplateDomain;
    private readonly IDigitalWorkerDeployRenderDomain _deployRenderDomain;
    private readonly IDigitalWorkerStartupProbeResultDomain _startupProbeResultDomain;

    /// <summary>
    /// zh-cn: 创建数字员工管理服务，聚合主档、会话、命令、运行和事件领域对象，供后台管理端维护 AgentSprint.Worker。
    /// en-us: Creates the digital-worker management service by aggregating worker, session, command, run, and event domains for the admin backend.
    /// </summary>
    public DigitalWorkerManagementService(
        IDigitalWorkerDomain workerDomain,
        IWorkerSessionDomain sessionDomain,
        IWorkerCommandDomain commandDomain,
        IWorkerCommandLogDomain commandLogDomain,
        IWorkerCommandLogBuffer commandLogBuffer,
        IWorkerRunDomain runDomain,
        IWorkerEventDomain eventDomain,
        ISprintProjectDomain projectDomain,
        ISprintDevelopmentTaskDomain taskDomain,
        ISprintBugDomain bugDomain,
        ISprintTaskLeaseDomain leaseDomain,
        IAgentTokenDomain agentTokenDomain,
        IDigitalWorkerDeployTemplateDomain deployTemplateDomain,
        IDigitalWorkerDeployRenderDomain deployRenderDomain,
        IDigitalWorkerStartupProbeResultDomain startupProbeResultDomain)
    {
        _workerDomain = workerDomain;
        _sessionDomain = sessionDomain;
        _commandDomain = commandDomain;
        _commandLogDomain = commandLogDomain;
        _commandLogBuffer = commandLogBuffer;
        _runDomain = runDomain;
        _eventDomain = eventDomain;
        _projectDomain = projectDomain;
        _taskDomain = taskDomain;
        _bugDomain = bugDomain;
        _leaseDomain = leaseDomain;
        _agentTokenDomain = agentTokenDomain;
        _deployTemplateDomain = deployTemplateDomain;
        _deployRenderDomain = deployRenderDomain;
        _startupProbeResultDomain = startupProbeResultDomain;
    }

    /// <inheritdoc />
    public async Task<DigitalWorkerResult> CreateWorkerAsync(CreateDigitalWorkerRequest request, string userId)
    {
        var code = await ResolveWorkerCodeAsync(request.Code);
        var name = NormalizeRequired(request.Name, "Worker name is required.");
        var agentUserId = NormalizeRequired(request.AgentUserId, "Agent user is required.");
        var duplicated = await _workerDomain.ListAsync(entity => entity.Code == code);
        if (duplicated.Count > 0)
        {
            throw new InvalidOperationException("Worker code already exists.");
        }

        var entity = new DigitalWorkerEntity
        {
            Code = code,
            Name = name,
            AgentUserId = agentUserId,
            AgentTokenId = NormalizeOptional(request.AgentTokenId),
            ActiveAgentTokenKey = NormalizeOptional(request.AgentTokenId),
            ProjectIds = SerializeIds(NormalizeIds(request.ProjectIds)),
            EndpointIds = SerializeIds(NormalizeIds(request.EndpointIds)),
            SkillIds = SerializeIds(NormalizeIds(request.SkillIds)),
            EmployeeType = NormalizeEmployeeType(request.EmployeeType),
            WorkerType = NormalizeWorkerType(request.WorkerType),
            Status = DigitalWorkerStatuses.Inactive,
            MaxConcurrentRuns = NormalizeRange(request.MaxConcurrentRuns, 1, 1, 10),
            HeartbeatTimeoutSeconds = NormalizeHeartbeatTimeout(request.HeartbeatTimeoutSeconds, 90),
            PollIntervalSeconds = NormalizeRange(request.PollIntervalSeconds, 15, 5, 300),
            IdleMaxIntervalSeconds = NormalizeRange(request.IdleMaxIntervalSeconds, 180, 15, 3600),
            MaxRunMinutes = NormalizeRange(request.MaxRunMinutes, 60, 1, 1440),
            WorkspaceRoot = NormalizePath(request.WorkspaceRoot, "/workspaces"),
            RunsRoot = NormalizePath(request.RunsRoot, "/runs"),
            CodexHome = NormalizePath(request.CodexHome, "/codex-home"),
            SandboxMode = NormalizeSandboxMode(request.SandboxMode),
            RunSmokeOnStartup = request.RunSmokeOnStartup ?? false,
            SmokePrompt = NormalizeOptional(request.SmokePrompt) ?? "你好",
            AiPlatformCode = NormalizeAiPlatformCode(request.AiPlatformCode),
            CodexProvider = NormalizeOptional(request.CodexProvider) ?? "openai",
            CodexModel = NormalizeOptional(request.CodexModel) ?? "gpt-5.4",
            OpenAiBaseUrl = NormalizeOptional(request.OpenAiBaseUrl),
            ConfigVersion = 1,
            Description = NormalizeOptional(request.Description),
            CreatedBy = userId
        };
        await EnsureAgentTokenAvailableAsync(entity.AgentTokenId, null);
        await _workerDomain.CreateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<DigitalWorkerResult> UpdateWorkerAsync(string id, UpdateDigitalWorkerRequest request)
    {
        var entity = await GetWorkerOrThrowAsync(id);
        entity.Name = NormalizeRequired(request.Name, "Worker name is required.");
        entity.AgentUserId = NormalizeRequired(request.AgentUserId, "Agent user is required.");
        var nextAgentTokenId = NormalizeOptional(request.AgentTokenId);
        await EnsureAgentTokenAvailableAsync(nextAgentTokenId, entity.Id);
        entity.AgentTokenId = nextAgentTokenId;
        entity.ActiveAgentTokenKey = entity.IsDelete == 0 ? nextAgentTokenId : null;
        entity.ProjectIds = SerializeIds(NormalizeIds(request.ProjectIds));
        entity.EndpointIds = SerializeIds(NormalizeIds(request.EndpointIds));
        entity.SkillIds = SerializeIds(NormalizeIds(request.SkillIds));
        entity.EmployeeType = NormalizeEmployeeType(request.EmployeeType ?? entity.EmployeeType);
        entity.WorkerType = NormalizeWorkerType(request.WorkerType ?? entity.WorkerType);
        entity.Status = NormalizeWorkerStatus(request.Status ?? entity.Status);
        entity.MaxConcurrentRuns = NormalizeRange(request.MaxConcurrentRuns, entity.MaxConcurrentRuns, 1, 10);
        entity.HeartbeatTimeoutSeconds = NormalizeHeartbeatTimeout(request.HeartbeatTimeoutSeconds, entity.HeartbeatTimeoutSeconds);
        entity.PollIntervalSeconds = NormalizeRange(request.PollIntervalSeconds, entity.PollIntervalSeconds, 5, 300);
        entity.IdleMaxIntervalSeconds = NormalizeRange(request.IdleMaxIntervalSeconds, entity.IdleMaxIntervalSeconds, 15, 3600);
        entity.MaxRunMinutes = NormalizeRange(request.MaxRunMinutes, entity.MaxRunMinutes, 1, 1440);
        entity.WorkspaceRoot = NormalizePath(request.WorkspaceRoot, entity.WorkspaceRoot);
        entity.RunsRoot = NormalizePath(request.RunsRoot, entity.RunsRoot);
        entity.CodexHome = NormalizePath(request.CodexHome, entity.CodexHome);
        entity.SandboxMode = NormalizeSandboxMode(request.SandboxMode ?? entity.SandboxMode);
        entity.RunSmokeOnStartup = request.RunSmokeOnStartup ?? entity.RunSmokeOnStartup;
        entity.SmokePrompt = NormalizeOptional(request.SmokePrompt) ?? entity.SmokePrompt;
        entity.AiPlatformCode = NormalizeAiPlatformCode(request.AiPlatformCode ?? entity.AiPlatformCode);
        entity.CodexProvider = NormalizeOptional(request.CodexProvider) ?? entity.CodexProvider;
        entity.CodexModel = NormalizeOptional(request.CodexModel) ?? entity.CodexModel;
        entity.OpenAiBaseUrl = NormalizeOptional(request.OpenAiBaseUrl);
        entity.ConfigVersion += 1;
        entity.Description = NormalizeOptional(request.Description);
        await _workerDomain.UpdateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DigitalWorkerResult>> ListWorkersAsync(
        string? status = null,
        string? workerType = null,
        string? keyword = null)
    {
        var normalizedStatus = NormalizeOptional(status);
        var normalizedType = NormalizeOptional(workerType);
        var normalizedKeyword = NormalizeOptional(keyword);
        var entities = await _workerDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(normalizedStatus) || entity.Status == normalizedStatus) &&
            (string.IsNullOrWhiteSpace(normalizedType) || entity.WorkerType == normalizedType));
        var latestSessions = await LoadLatestSessionsAsync(entities.Select(entity => entity.Id));
        return entities
            .Where(entity =>
                string.IsNullOrWhiteSpace(normalizedKeyword) ||
                TextContains(normalizedKeyword, entity.Code, entity.Name, entity.Description))
            .OrderByDescending(entity => entity.UpdateTime ?? entity.CreateTime)
            .Select(entity => ToResult(entity, latestSessions.GetValueOrDefault(entity.Id)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DigitalWorkerDetailResult> GetWorkerDetailAsync(string id)
    {
        var worker = await GetWorkerOrThrowAsync(id);
        var sessions = await _sessionDomain.ListAsync(entity => entity.WorkerId == worker.Id);
        var latestSession = sessions
            .OrderByDescending(entity => entity.LastHeartbeatAt ?? entity.StartedAt)
            .FirstOrDefault();
        WorkerRunEntity? currentRun = null;
        if (!string.IsNullOrWhiteSpace(latestSession?.CurrentRunId))
        {
            currentRun = await _runDomain.GetAsync(latestSession.CurrentRunId);
        }

        var commands = await _commandDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            entity.Status == WorkerCommandStatuses.Pending);
        var probeResults = await ListStartupProbeResultsAsync(worker.Id);

        return new DigitalWorkerDetailResult(
            ToResult(worker),
            latestSession is null ? null : ToResult(latestSession),
            currentRun is null ? null : ToResult(currentRun),
            commands.OrderBy(entity => entity.CreateTime).Select(ToResult).ToList(),
            probeResults);
    }

    /// <inheritdoc />
    public async Task<DigitalWorkerResult> SetWorkerStatusAsync(string id, SetDigitalWorkerStatusRequest request)
    {
        var entity = await GetWorkerOrThrowAsync(id);
        var nextStatus = NormalizeWorkerStatus(request.Status);
        if (nextStatus == DigitalWorkerStatuses.Idle)
        {
            await EnsureWorkerCanManuallyRestoreIdleAsync(entity);
        }

        if (nextStatus == DigitalWorkerStatuses.Disabled)
        {
            await EnsureWorkerCanDisableAsync(entity);
        }

        entity.Status = nextStatus;
        await _workerDomain.UpdateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<DigitalWorkerInstallRenderResult> GenerateInstallAsync(
        string id,
        GenerateDigitalWorkerInstallRequest request,
        string userId)
    {
        var worker = await GetWorkerOrThrowAsync(id);
        if (worker.Status == DigitalWorkerStatuses.Disabled)
        {
            throw new InvalidOperationException("Disabled worker cannot generate install assets.");
        }

        var template = await ResolveDeployTemplateAsync(request.TemplateId);
        ValidateDeployTemplate(template);
        var agentToken = await ResolveWorkerAgentTokenValueAsync(worker);
        var apiBaseUrl = NormalizeOptional(request.ApiBaseUrl) ?? "http://api:5000";
        var render = new DigitalWorkerDeployRenderEntity
        {
            WorkerId = worker.Id,
            TemplateId = template.Id,
            TemplateVersion = template.Version,
            PlainSecretEnabled = request.PlainSecretEnabled,
            CreatedBy = userId
        };
        var placeholderValues = BuildDeployPlaceholderValues(worker, template, apiBaseUrl, agentToken, render.Id);
        var composeValues = new Dictionary<string, string>(placeholderValues, StringComparer.OrdinalIgnoreCase);
        if (!request.PlainSecretEnabled)
        {
            composeValues["agentToken"] = "${AGENTSPRINT_AGENT_TOKEN}";
        }

        render.RenderedCompose = RenderTemplate(template.ComposeTemplate, composeValues);
        render.RenderedEnv = request.PlainSecretEnabled
            ? null
            : $"AGENTSPRINT_AGENT_TOKEN={agentToken}{Environment.NewLine}";
        render.PlaceholderValuesJson = JsonSerializer.Serialize(placeholderValues);
        ValidateRenderedInstall(render, request.PlainSecretEnabled, agentToken);
        await _deployRenderDomain.CreateAsync(render);
        worker.RuntimeProfile = template.RuntimeProfile;
        worker.BackendTechCapabilities = NormalizeBackendTechCapabilities(template.BackendTechCapabilities);
        await _workerDomain.UpdateAsync(worker);
        return ToResult(render);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DigitalWorkerDeployTemplateResult>> ListDeployTemplatesAsync(
        string? status = null,
        string? keyword = null)
    {
        var normalizedStatus = NormalizeOptional(status);
        var normalizedKeyword = NormalizeOptional(keyword);
        var statusValue = int.TryParse(normalizedStatus, out var parsedStatus) ? parsedStatus : (int?)null;
        var templates = await _deployTemplateDomain.ListAsync(entity =>
            (!statusValue.HasValue || entity.Status == statusValue.Value));
        return templates
            .Where(entity =>
                string.IsNullOrWhiteSpace(normalizedKeyword) ||
                TextContains(normalizedKeyword, entity.Code, entity.Name, entity.Description, entity.RuntimeProfile, entity.BackendTechCapabilities))
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.Code)
            .ThenByDescending(entity => entity.Version)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DigitalWorkerDeployTemplateResult> CreateDeployTemplateAsync(SaveDigitalWorkerDeployTemplateRequest request)
    {
        var entity = new DigitalWorkerDeployTemplateEntity
        {
            Code = NormalizeTemplateCode(request.Code),
            Name = NormalizeRequired(request.Name, "Deploy template name is required."),
            Description = NormalizeOptional(request.Description),
            RuntimeProfile = NormalizeRequired(request.RuntimeProfile, "Runtime profile is required."),
            BackendTechCapabilities = NormalizeBackendTechCapabilities(request.BackendTechCapabilities),
            ComposeTemplate = NormalizeRequired(request.ComposeTemplate, "Deploy template compose content is required."),
            DockerfileExtension = NormalizeOptional(request.DockerfileExtension),
            Version = NormalizeRange(request.Version, 1, 1, 9999),
            Sort = NormalizeRange(request.Sort, 100, 0, 9999),
            Status = NormalizeTemplateStatus(request.Status)
        };
        await EnsureDeployTemplateCodeAvailableAsync(entity.Code, null);
        ValidateDeployTemplate(entity);
        await _deployTemplateDomain.CreateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<DigitalWorkerDeployTemplateResult> UpdateDeployTemplateAsync(
        string id,
        SaveDigitalWorkerDeployTemplateRequest request)
    {
        var entity = await GetDeployTemplateOrThrowAsync(id);
        var code = NormalizeTemplateCode(request.Code);
        await EnsureDeployTemplateCodeAvailableAsync(code, entity.Id);
        entity.Code = code;
        entity.Name = NormalizeRequired(request.Name, "Deploy template name is required.");
        entity.Description = NormalizeOptional(request.Description);
        entity.RuntimeProfile = NormalizeRequired(request.RuntimeProfile, "Runtime profile is required.");
        entity.BackendTechCapabilities = NormalizeBackendTechCapabilities(request.BackendTechCapabilities);
        entity.ComposeTemplate = NormalizeRequired(request.ComposeTemplate, "Deploy template compose content is required.");
        entity.DockerfileExtension = NormalizeOptional(request.DockerfileExtension);
        entity.Version = NormalizeRange(request.Version, entity.Version, 1, 9999);
        entity.Sort = NormalizeRange(request.Sort, entity.Sort, 0, 9999);
        entity.Status = NormalizeTemplateStatus(request.Status);
        ValidateDeployTemplate(entity);
        await _deployTemplateDomain.UpdateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<DigitalWorkerResult> DeleteWorkerAsync(string id)
    {
        var entity = await GetWorkerOrThrowAsync(id);
        if (entity.Status != DigitalWorkerStatuses.Disabled)
        {
            throw new InvalidOperationException("Only disabled workers can be deleted.");
        }

        await EnsureWorkerHasNoUnfinishedWorkAsync(entity);
        var result = ToResult(entity);
        entity.ActiveAgentTokenKey = null;
        entity.IsDelete = 1;
        await _workerDomain.UpdateAsync(entity);
        return result;
    }

    /// <inheritdoc />
    public async Task<WorkerCommandResult> CreateCommandAsync(CreateWorkerCommandRequest request, string userId)
    {
        var worker = await GetWorkerOrThrowAsync(request.WorkerId);
        if (!CanQueueWorkerCommand(worker))
        {
            throw new InvalidOperationException("Worker is not available for queued commands.");
        }

        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            await GetSessionOrThrowAsync(request.SessionId);
        }

        var commandType = NormalizeCommandType(request.CommandType);
        var payloadJson = NormalizeOptional(request.PayloadJson);
        var title = await ResolveCommandTitleAsync(worker, commandType, payloadJson, request.Title);
        await EnsureWorkerCanReceiveCommandAsync(worker, commandType, payloadJson);

        var entity = new WorkerCommandEntity
        {
            WorkerId = worker.Id,
            SessionId = NormalizeOptional(request.SessionId),
            CommandType = commandType,
            Title = title,
            PayloadJson = payloadJson,
            ExpiresAt = request.ExpiresAt,
            CreatedBy = userId
        };
        await _commandDomain.CreateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkerCommandResult>> ListCommandsAsync(
        string? workerId = null,
        string? sessionId = null,
        string? commandType = null,
        string? status = null)
    {
        var normalizedWorkerId = await ResolveWorkerFilterIdAsync(workerId);
        var normalizedSessionId = NormalizeOptional(sessionId);
        var normalizedCommandType = NormalizeOptional(commandType);
        var normalizedStatus = NormalizeOptional(status);
        var entities = await _commandDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(normalizedWorkerId) || entity.WorkerId == normalizedWorkerId) &&
            (string.IsNullOrWhiteSpace(normalizedSessionId) || entity.SessionId == normalizedSessionId) &&
            (string.IsNullOrWhiteSpace(normalizedCommandType) || entity.CommandType == normalizedCommandType) &&
            (string.IsNullOrWhiteSpace(normalizedStatus) || entity.Status == normalizedStatus));
        return entities
            .OrderByDescending(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<WorkerCommandLogSnapshotResult?> GetCommandLogSnapshotAsync(string commandId)
    {
        var normalizedCommandId = NormalizeRequired(commandId, "Command id is required.");
        var buffered = _commandLogBuffer.Get(normalizedCommandId);
        if (buffered is not null)
        {
            return buffered;
        }

        var logs = await _commandLogDomain.ListAsync(entity => entity.CommandId == normalizedCommandId);
        var latest = logs
            .OrderByDescending(entity => entity.CreateTime)
            .FirstOrDefault();
        return latest is null
            ? null
            : new WorkerCommandLogSnapshotResult(
                latest.CommandId,
                latest.RunId,
                latest.SessionId,
                latest.InstanceId,
                latest.LogText,
                0,
                true,
                latest.CompletedAt ?? latest.UpdateTime ?? latest.CreateTime);
    }

    /// <inheritdoc />
    public async Task<WorkerCommandResult> ReplayCommandAsync(string commandId, string userId)
    {
        var source = await _commandDomain.GetAsync(commandId)
            ?? throw new InvalidOperationException("Worker command does not exist.");
        var worker = await GetWorkerOrThrowAsync(source.WorkerId);
        if (!CanQueueWorkerCommand(worker))
        {
            throw new InvalidOperationException("Worker is not available for queued commands.");
        }

        var commandType = NormalizeCommandType(source.CommandType);
        var payloadJson = NormalizeOptional(source.PayloadJson);
        await EnsureWorkerCanReceiveCommandAsync(worker, commandType, payloadJson);

        var entity = new WorkerCommandEntity
        {
            WorkerId = worker.Id,
            CommandType = commandType,
            Title = ResolveCommandTitle(source),
            PayloadJson = payloadJson,
            CreatedBy = userId
        };
        await _commandDomain.CreateAsync(entity);
        return ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkerSessionResult>> ListSessionsAsync(string? workerId = null, string? status = null)
    {
        var normalizedWorkerId = await ResolveWorkerFilterIdAsync(workerId);
        var normalizedStatus = NormalizeOptional(status);
        var entities = await _sessionDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(normalizedWorkerId) || entity.WorkerId == normalizedWorkerId) &&
            (string.IsNullOrWhiteSpace(normalizedStatus) || entity.Status == normalizedStatus));
        return entities
            .OrderByDescending(entity => entity.LastHeartbeatAt ?? entity.StartedAt)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkerRunResult>> ListRunsAsync(
        string? workerId = null,
        string? sessionId = null,
        string? targetType = null,
        string? targetId = null,
        string? status = null)
    {
        var normalizedWorkerId = await ResolveWorkerFilterIdAsync(workerId);
        var normalizedSessionId = NormalizeOptional(sessionId);
        var normalizedTargetType = NormalizeOptional(targetType);
        var normalizedTargetId = NormalizeOptional(targetId);
        var normalizedStatus = NormalizeOptional(status);
        var entities = await _runDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(normalizedWorkerId) || entity.WorkerId == normalizedWorkerId) &&
            (string.IsNullOrWhiteSpace(normalizedSessionId) || entity.SessionId == normalizedSessionId) &&
            (string.IsNullOrWhiteSpace(normalizedTargetType) || entity.TargetType == normalizedTargetType) &&
            (string.IsNullOrWhiteSpace(normalizedTargetId) || entity.TargetId == normalizedTargetId) &&
            (string.IsNullOrWhiteSpace(normalizedStatus) || entity.Status == normalizedStatus));
        return entities
            .OrderByDescending(entity => entity.StartedAt)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkerEventResult>> ListEventsAsync(
        string? workerId = null,
        string? sessionId = null,
        string? runId = null,
        string? eventType = null)
    {
        var normalizedWorkerId = await ResolveWorkerFilterIdAsync(workerId);
        var normalizedSessionId = NormalizeOptional(sessionId);
        var normalizedRunId = NormalizeOptional(runId);
        var normalizedEventType = NormalizeOptional(eventType);
        var entities = await _eventDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(normalizedWorkerId) || entity.WorkerId == normalizedWorkerId) &&
            (string.IsNullOrWhiteSpace(normalizedSessionId) || entity.SessionId == normalizedSessionId) &&
            (string.IsNullOrWhiteSpace(normalizedRunId) || entity.RunId == normalizedRunId) &&
            (string.IsNullOrWhiteSpace(normalizedEventType) || entity.EventType == normalizedEventType));
        return entities
            .OrderByDescending(entity => entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StartupProbeResult>> ListStartupProbeResultsAsync(string workerId)
    {
        var worker = await GetWorkerOrThrowAsync(workerId);
        var results = await _startupProbeResultDomain.ListAsync(entity => entity.WorkerId == worker.Id);
        return results
            .OrderByDescending(entity => entity.ReportedAt)
            .ThenBy(entity => entity.ProbeCode)
            .Select(ToResult)
            .ToList();
    }

    private async Task<DigitalWorkerEntity> GetWorkerOrThrowAsync(string id)
    {
        var entity = await _workerDomain.GetAsync(id);
        if (entity is null)
        {
            var workers = await _workerDomain.ListAsync(worker => worker.Code == id);
            entity = workers.SingleOrDefault();
        }

        return entity ?? throw new InvalidOperationException("Worker does not exist.");
    }

    private async Task<string?> ResolveWorkerFilterIdAsync(string? workerId)
    {
        var normalizedWorkerId = NormalizeOptional(workerId);
        if (normalizedWorkerId is null)
        {
            return null;
        }

        return (await GetWorkerOrThrowAsync(normalizedWorkerId)).Id;
    }

    private async Task<WorkerSessionEntity> GetSessionOrThrowAsync(string id)
    {
        var entity = await _sessionDomain.GetAsync(id);
        return entity ?? throw new InvalidOperationException("Worker session does not exist.");
    }

    private async Task EnsureAgentTokenAvailableAsync(string? agentTokenId, string? currentWorkerId)
    {
        if (string.IsNullOrWhiteSpace(agentTokenId))
        {
            return;
        }

        var duplicated = await _workerDomain.ListAsync(entity =>
            entity.ActiveAgentTokenKey == agentTokenId &&
            entity.Id != currentWorkerId);
        if (duplicated.Count > 0)
        {
            throw new InvalidOperationException("Agent Token is already bound to another active digital worker.");
        }
    }

    private async Task<DigitalWorkerDeployTemplateEntity> ResolveDeployTemplateAsync(string? templateId)
    {
        var normalizedTemplateId = NormalizeOptional(templateId);
        DigitalWorkerDeployTemplateEntity? template = null;
        if (normalizedTemplateId is not null)
        {
            template = await _deployTemplateDomain.GetAsync(normalizedTemplateId);
            if (template is null)
            {
                var byCode = await _deployTemplateDomain.ListAsync(entity => entity.Code == normalizedTemplateId);
                template = byCode.SingleOrDefault();
            }
        }

        if (template is null)
        {
            var templates = await _deployTemplateDomain.ListAsync(entity =>
                entity.Status == DigitalWorkerTemplateStatuses.Enabled);
            template = templates
                .OrderBy(entity => entity.Sort)
                .ThenByDescending(entity => entity.Version)
                .FirstOrDefault();
        }

        return template ?? throw new InvalidOperationException("No enabled digital worker deploy template is configured.");
    }

    private async Task<DigitalWorkerDeployTemplateEntity> GetDeployTemplateOrThrowAsync(string id)
    {
        var normalized = NormalizeRequired(id, "Deploy template id is required.");
        var template = await _deployTemplateDomain.GetAsync(normalized);
        if (template is null)
        {
            var templates = await _deployTemplateDomain.ListAsync(entity => entity.Code == normalized);
            template = templates.SingleOrDefault();
        }

        return template ?? throw new InvalidOperationException("Deploy template does not exist.");
    }

    private async Task EnsureDeployTemplateCodeAvailableAsync(string code, string? currentTemplateId)
    {
        var duplicated = await _deployTemplateDomain.ListAsync(entity =>
            entity.Code == code &&
            entity.Id != currentTemplateId &&
            entity.IsDelete == 0);
        if (duplicated.Count > 0)
        {
            throw new InvalidOperationException("Deploy template code already exists.");
        }
    }

    private static void ValidateDeployTemplate(DigitalWorkerDeployTemplateEntity template)
    {
        var compose = NormalizeRequired(template.ComposeTemplate, "Deploy template compose content is required.");
        if (!compose.Contains("{{apiBaseUrl}}", StringComparison.OrdinalIgnoreCase) &&
            !compose.Contains("AgentSprint__ApiBaseUrl", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Deploy template must include AgentSprint API base URL.");
        }

        if (!compose.Contains("{{agentToken}}", StringComparison.OrdinalIgnoreCase) &&
            !compose.Contains("AGENTSPRINT_AGENT_TOKEN", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Deploy template must include Agent Token placeholder or env reference.");
        }

        if (!compose.Contains("{{workspaceRoot}}", StringComparison.OrdinalIgnoreCase) ||
            !compose.Contains("{{runsRoot}}", StringComparison.OrdinalIgnoreCase) ||
            !compose.Contains("{{codexHome}}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Deploy template must include isolated workspace, runs, and Codex home placeholders.");
        }

        if (compose.Contains("container_name: math-codex-worker-1", StringComparison.OrdinalIgnoreCase) ||
            compose.Contains("container_name: agentsprint-worker", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Deploy template cannot hardcode a specific worker container name.");
        }

        if (!compose.Contains("{{serviceName}}", StringComparison.OrdinalIgnoreCase) &&
            !compose.Contains("{{containerName}}", StringComparison.OrdinalIgnoreCase) &&
            !compose.Contains("{{workerCode}}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Deploy template must use worker-specific service or container placeholders.");
        }

        var dockerfileExtension = NormalizeOptional(template.DockerfileExtension);
        if (dockerfileExtension is not null &&
            (dockerfileExtension.Contains(" rm -rf / ", StringComparison.OrdinalIgnoreCase) ||
             dockerfileExtension.Contains("curl ", StringComparison.OrdinalIgnoreCase) ||
             dockerfileExtension.Contains("wget ", StringComparison.OrdinalIgnoreCase) ||
             dockerfileExtension.Contains("mkfs", StringComparison.OrdinalIgnoreCase) ||
             dockerfileExtension.Contains(":(){", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Deploy template Dockerfile extension contains unsafe commands.");
        }
    }

    private async Task<string> ResolveWorkerAgentTokenValueAsync(DigitalWorkerEntity worker)
    {
        if (string.IsNullOrWhiteSpace(worker.AgentTokenId))
        {
            throw new InvalidOperationException("Worker Agent Token is required before generating install assets.");
        }

        var token = await _agentTokenDomain.GetAsync(worker.AgentTokenId)
            ?? throw new InvalidOperationException("Worker Agent Token does not exist.");
        if (token.Status != 1 || token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Worker Agent Token is invalid or expired.");
        }

        return NormalizeRequired(token.TokenValue, "Worker Agent Token value is empty.");
    }

    private static Dictionary<string, string> BuildDeployPlaceholderValues(
        DigitalWorkerEntity worker,
        DigitalWorkerDeployTemplateEntity template,
        string apiBaseUrl,
        string agentToken,
        string workerDeployRenderId)
    {
        var serviceName = SanitizeServiceName(worker.Code);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["workerCode"] = worker.Code,
            ["workerName"] = worker.Name,
            ["serviceName"] = serviceName,
            ["containerName"] = serviceName,
            ["imageName"] = ResolveWorkerImageName(template.RuntimeProfile),
            ["apiBaseUrl"] = apiBaseUrl,
            ["agentToken"] = agentToken,
            ["workspaceRoot"] = worker.WorkspaceRoot,
            ["runsRoot"] = worker.RunsRoot,
            ["codexHome"] = worker.CodexHome,
            ["runtimeProfile"] = template.RuntimeProfile,
            ["workerDeployRenderId"] = workerDeployRenderId
        };
    }

    private static string SanitizeServiceName(string code)
    {
        var builder = new StringBuilder();
        foreach (var character in code.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        var value = builder.ToString().Trim('-', '_');
        return string.IsNullOrWhiteSpace(value) ? "agentsprint-worker" : value;
    }

    private static string ResolveWorkerImageName(string runtimeProfile)
    {
        return runtimeProfile.Equals("dotnet-default", StringComparison.OrdinalIgnoreCase)
            ? "agentsprint-worker:latest"
            : $"agentsprint-worker-{runtimeProfile}:latest";
    }

    private static string RenderTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        var result = template;
        foreach (var (key, value) in values)
        {
            result = result.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static void ValidateRenderedInstall(
        DigitalWorkerDeployRenderEntity render,
        bool plainSecretEnabled,
        string agentToken)
    {
        if (render.RenderedCompose.Contains("{{", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Rendered compose contains unresolved placeholders.");
        }

        if (!render.RenderedCompose.Contains("AgentSprint__WorkerDeployRenderId", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Rendered compose must include AgentSprint__WorkerDeployRenderId.");
        }

        if (!render.RenderedCompose.Contains("AgentSprint__ApiBaseUrl", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Rendered compose must include AgentSprint__ApiBaseUrl.");
        }

        if (!render.RenderedCompose.Contains("AgentSprint__AgentToken", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Rendered compose must include AgentSprint__AgentToken.");
        }

        if (!plainSecretEnabled && render.RenderedCompose.Contains(agentToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Agent Token cannot appear in docker-compose.yml when plain secret output is disabled.");
        }
    }

    private async Task EnsureWorkerCanDisableAsync(DigitalWorkerEntity worker)
    {
        if (worker.Status != DigitalWorkerStatuses.Maintenance)
        {
            throw new InvalidOperationException("Worker can be disabled only from maintenance status.");
        }

        await EnsureWorkerHasNoUnfinishedWorkAsync(worker);
    }

    private async Task EnsureWorkerCanManuallyRestoreIdleAsync(DigitalWorkerEntity worker)
    {
        if (worker.Status is DigitalWorkerStatuses.Inactive or DigitalWorkerStatuses.Starting)
        {
            throw new InvalidOperationException("Worker can enter idle only after required startup probes pass.");
        }

        if (worker.Status == DigitalWorkerStatuses.Disabled)
        {
            throw new InvalidOperationException("Disabled worker cannot be restored to idle directly.");
        }

        await EnsureWorkerHasNoUnfinishedWorkAsync(worker);
        await EnsureLatestRequiredStartupProbesPassedAsync(worker.Id);
    }

    private async Task EnsureLatestRequiredStartupProbesPassedAsync(string workerId)
    {
        var results = await _startupProbeResultDomain.ListAsync(entity => entity.WorkerId == workerId);
        var latestByProbe = results
            .Where(entity => entity.Required)
            .GroupBy(entity => entity.ProbeCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entity => entity.ReportedAt).First())
            .ToList();
        if (latestByProbe.Count == 0 || latestByProbe.Any(entity => !entity.Passed))
        {
            throw new InvalidOperationException("Worker cannot be restored to idle until all required startup probes have passed.");
        }
    }

    private async Task EnsureWorkerHasNoUnfinishedWorkAsync(DigitalWorkerEntity worker)
    {
        var blockingCommands = await _commandDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            (entity.Status == WorkerCommandStatuses.Pending ||
             entity.Status == WorkerCommandStatuses.Acked ||
             entity.Status == WorkerCommandStatuses.Running));
        var blockingRuns = await _runDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            (entity.Status == WorkerRunStatuses.Pending ||
             entity.Status == WorkerRunStatuses.Running));
        var blockingTasks = await _taskDomain.ListAsync(entity =>
            entity.AssigneeId == worker.AgentUserId &&
            entity.AssigneeType == SprintTaskAssigneeTypes.DigitalWorker &&
            entity.Status != SprintDevelopmentTaskStatuses.Completed);
        var blockingBugs = await _bugDomain.ListAsync(entity =>
            entity.DeveloperId == worker.AgentUserId &&
            entity.Status != SprintBugStatuses.Closed &&
            entity.Status != SprintBugStatuses.FixedReadyForRegression);
        var now = DateTime.UtcNow;
        var blockingLeases = await _leaseDomain.ListAsync(entity =>
            entity.OwnerId == worker.AgentUserId &&
            entity.Status == SprintTaskLeaseStatuses.Active &&
            entity.ExpiresAt > now);

        var messages = new List<string>();
        if (blockingCommands.Count > 0)
        {
            messages.Add($"pending/running commands {blockingCommands.Count}");
        }

        if (blockingRuns.Count > 0)
        {
            messages.Add($"pending/running runs {blockingRuns.Count}");
        }

        if (blockingTasks.Count > 0)
        {
            messages.Add($"unfinished development tasks {blockingTasks.Count}");
        }

        if (blockingBugs.Count > 0)
        {
            messages.Add($"unfinished bugs {blockingBugs.Count}");
        }

        if (blockingLeases.Count > 0)
        {
            messages.Add($"active task leases {blockingLeases.Count}");
        }

        if (messages.Count > 0)
        {
            throw new InvalidOperationException("Worker still has unfinished work: " + string.Join(", ", messages) + ".");
        }
    }

    internal static bool CanQueueWorkerCommand(DigitalWorkerEntity worker)
    {
        return worker.Status is
            DigitalWorkerStatuses.Idle or
            DigitalWorkerStatuses.Working or
            DigitalWorkerStatuses.Active;
    }

    internal static string NormalizeBackendTechCapabilities(string? value)
    {
        var capabilities = ParseTechStack(value).ToList();
        return capabilities.Count == 0 ? "dotnet" : string.Join(",", capabilities);
    }

    internal static void EnsureBackendTechCovered(DigitalWorkerEntity worker, string? backendTechStack)
    {
        var required = ParseTechStack(backendTechStack).ToList();
        if (required.Count == 0)
        {
            return;
        }

        var capabilities = ParseTechStack(worker.BackendTechCapabilities).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(item => !capabilities.Contains(item)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Worker backend tech capability does not cover project stack: {string.Join(", ", missing)}.");
        }
    }

    private static IEnumerable<string> ParseTechStack(string? value)
    {
        return (value ?? string.Empty)
            .Split([',', ';', '/', '|', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeTechStackItem)
            .Where(item => item is not null)
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeTechStackItem(string value)
    {
        var normalized = value.Trim().TrimStart('.').ToLowerInvariant();
        return normalized switch
        {
            "" => null,
            "c#" or "csharp" or "aspnetcore" or "asp.net" or "asp.netcore" or "net" or "netcore" => "dotnet",
            "dotnet" or "java" or "node" or "python" or "go" or "php" or "ruby" => normalized,
            "nodejs" or "javascript" or "typescript" => "node",
            "python3" => "python",
            "golang" => "go",
            _ => null
        };
    }

    internal static DigitalWorkerResult ToResult(DigitalWorkerEntity entity)
    {
        return ToResult(entity, null);
    }

    internal static DigitalWorkerResult ToResult(DigitalWorkerEntity entity, WorkerSessionEntity? latestSession)
    {
        return new DigitalWorkerResult(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.AgentUserId,
            entity.AgentTokenId,
            DeserializeIds(entity.ProjectIds),
            DeserializeIds(entity.EndpointIds),
            DeserializeIds(entity.SkillIds),
            entity.EmployeeType,
            entity.WorkerType,
            entity.Status,
            entity.RuntimeProfile,
            entity.BackendTechCapabilities,
            entity.MaxConcurrentRuns,
            entity.HeartbeatTimeoutSeconds,
            entity.PollIntervalSeconds,
            entity.IdleMaxIntervalSeconds,
            entity.MaxRunMinutes,
            entity.WorkspaceRoot,
            entity.RunsRoot,
            entity.CodexHome,
            entity.SandboxMode,
            entity.RunSmokeOnStartup,
            entity.SmokePrompt,
            entity.AiPlatformCode,
            entity.CodexProvider,
            entity.CodexModel,
            entity.OpenAiBaseUrl,
            entity.ConfigVersion,
            entity.Description,
            entity.CreatedBy,
            entity.CreateTime,
            entity.UpdateTime,
            latestSession?.LastHeartbeatAt,
            latestSession?.Status,
            BuildRuntimeSummary(latestSession));
    }

    internal static DigitalWorkerDeployTemplateResult ToResult(DigitalWorkerDeployTemplateEntity entity)
    {
        return new DigitalWorkerDeployTemplateResult(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.RuntimeProfile,
            entity.BackendTechCapabilities,
            entity.ComposeTemplate,
            entity.DockerfileExtension,
            entity.Version,
            entity.Sort,
            entity.Status,
            entity.CreateTime,
            entity.UpdateTime);
    }

    private async Task<IReadOnlyDictionary<string, WorkerSessionEntity>> LoadLatestSessionsAsync(IEnumerable<string> workerIds)
    {
        var ids = workerIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return new Dictionary<string, WorkerSessionEntity>();
        }

        var sessions = await _sessionDomain.ListAsync(entity => ids.Contains(entity.WorkerId));
        return sessions
            .GroupBy(entity => entity.WorkerId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(entity => entity.LastHeartbeatAt ?? entity.StartedAt)
                    .First(),
                StringComparer.Ordinal);
    }

    private static string? BuildRuntimeSummary(WorkerSessionEntity? session)
    {
        if (session is null)
        {
            return null;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(session.ContainerId))
        {
            parts.Add($"容器 {session.ContainerId[..Math.Min(12, session.ContainerId.Length)]}");
        }

        if (!string.IsNullOrWhiteSpace(session.HostName))
        {
            parts.Add(session.HostName);
        }

        if (!string.IsNullOrWhiteSpace(session.CodexVersion))
        {
            parts.Add($"Codex {session.CodexVersion}");
        }

        if (!string.IsNullOrWhiteSpace(session.GitVersion))
        {
            parts.Add(session.GitVersion);
        }

        if (!string.IsNullOrWhiteSpace(session.DotnetVersion))
        {
            parts.Add($".NET {session.DotnetVersion}");
        }

        if (!string.IsNullOrWhiteSpace(session.NodeVersion))
        {
            parts.Add($"Node {session.NodeVersion}");
        }

        return parts.Count == 0 ? null : string.Join(" / ", parts);
    }

    private async Task<string> ResolveWorkerCodeAsync(string? requestedCode)
    {
        var normalized = NormalizeOptional(requestedCode);
        if (normalized is not null)
        {
            return normalized;
        }

        var todayPrefix = $"dw-{DateTime.UtcNow:yyyyMMdd}-";
        var existing = await _workerDomain.ListAsync(entity => entity.Code.StartsWith(todayPrefix));
        var maxSequence = existing
            .Select(entity => entity.Code[todayPrefix.Length..])
            .Select(suffix => int.TryParse(suffix, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{todayPrefix}{maxSequence + 1:0000}";
    }

    internal static WorkerSessionResult ToResult(WorkerSessionEntity entity)
    {
        return new WorkerSessionResult(
            entity.Id,
            entity.WorkerId,
            entity.InstanceId,
            entity.HostName,
            entity.ContainerId,
            entity.Status,
            entity.CodexVersion,
            entity.GitVersion,
            entity.DotnetVersion,
            entity.NodeVersion,
            entity.ConfigTomlExists,
            entity.CodexHome,
            entity.WorkspaceRoot,
            entity.RunsRoot,
            entity.CurrentRunId,
            entity.ErrorSummary,
            entity.LastHeartbeatAt,
            entity.StartedAt,
            entity.StoppedAt);
    }

    internal static WorkerCommandResult ToResult(WorkerCommandEntity entity)
    {
        return new WorkerCommandResult(
            entity.Id,
            entity.WorkerId,
            entity.SessionId,
            entity.CommandType,
            ResolveCommandTitle(entity),
            entity.PayloadJson,
            entity.Status,
            entity.AckedAt,
            entity.StartedAt,
            entity.CompletedAt,
            entity.ExpiresAt,
            entity.ResultJson,
            entity.ChangedFilesJson,
            entity.GitCommitId,
            entity.Error,
            entity.CreatedBy,
            entity.CreateTime);
    }

    internal static WorkerRunResult ToResult(WorkerRunEntity entity)
    {
        return new WorkerRunResult(
            entity.Id,
            entity.WorkerId,
            entity.SessionId,
            entity.CommandId,
            entity.RunType,
            entity.TargetType,
            entity.TargetId,
            entity.Status,
            entity.WorkspacePath,
            entity.PromptPath,
            entity.StdoutPath,
            entity.StderrPath,
            entity.FinalPath,
            entity.ManifestPath,
            entity.ExitCode,
            entity.TimedOut,
            entity.Error,
            entity.StartedAt,
            entity.CompletedAt);
    }

    internal static WorkerEventResult ToResult(WorkerEventEntity entity)
    {
        return new WorkerEventResult(
            entity.Id,
            entity.WorkerId,
            entity.SessionId,
            entity.RunId,
            entity.EventType,
            entity.Level,
            entity.Message,
            entity.PayloadJson,
            entity.CreateTime);
    }

    internal static WorkerCommandLogResult ToResult(WorkerCommandLogEntity entity)
    {
        return new WorkerCommandLogResult(
            entity.Id,
            entity.WorkerId,
            entity.SessionId,
            entity.InstanceId,
            entity.CommandId,
            entity.RunId,
            entity.LogText,
            entity.StartedAt,
            entity.CompletedAt,
            entity.CreateTime);
    }

    internal static DigitalWorkerInstallRenderResult ToResult(DigitalWorkerDeployRenderEntity entity)
    {
        return new DigitalWorkerInstallRenderResult(
            entity.Id,
            entity.WorkerId,
            entity.TemplateId,
            entity.TemplateVersion,
            entity.RenderedCompose,
            entity.RenderedEnv,
            entity.PlainSecretEnabled,
            entity.PlaceholderValuesJson,
            entity.CreateTime);
    }

    internal static StartupProbeConfigResult ToResult(DigitalWorkerStartupProbeConfigEntity entity)
    {
        return new StartupProbeConfigResult(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Command,
            entity.ExpectedPattern,
            entity.Required,
            entity.Sort);
    }

    internal static StartupProbeResult ToResult(DigitalWorkerStartupProbeResultEntity entity)
    {
        return new StartupProbeResult(
            entity.Id,
            entity.WorkerId,
            entity.SessionId,
            entity.InstanceId,
            entity.WorkerDeployRenderId,
            entity.ProbeConfigId,
            entity.ProbeCode,
            entity.ProbeName,
            entity.Command,
            entity.ExitCode,
            entity.Stdout,
            entity.Stderr,
            entity.Error,
            entity.Passed,
            entity.Required,
            entity.ReportedAt,
            entity.CreateTime);
    }

    internal static string NormalizeWorkerType(string? value)
    {
        var normalized = NormalizeOptional(value) ?? DigitalWorkerTypes.Codex;
        return normalized switch
        {
            DigitalWorkerTypes.Codex => normalized,
            _ => DigitalWorkerTypes.Codex
        };
    }

    internal static string NormalizeEmployeeType(string? value)
    {
        var normalized = NormalizeOptional(value) ?? DigitalWorkerEmployeeTypes.Development;
        return normalized switch
        {
            DigitalWorkerEmployeeTypes.Operations or
            DigitalWorkerEmployeeTypes.Development or
            DigitalWorkerEmployeeTypes.Audit or
            DigitalWorkerEmployeeTypes.Test or
            DigitalWorkerEmployeeTypes.Product => normalized,
            _ => DigitalWorkerEmployeeTypes.Development
        };
    }

    internal static string NormalizeWorkerStatus(string? value)
    {
        var normalized = NormalizeOptional(value) ?? DigitalWorkerStatuses.Inactive;
        return normalized switch
        {
            DigitalWorkerStatuses.Inactive or
            DigitalWorkerStatuses.Starting or
            DigitalWorkerStatuses.Idle or
            DigitalWorkerStatuses.Working or
            DigitalWorkerStatuses.Maintenance or
            DigitalWorkerStatuses.Disabled => normalized,
            DigitalWorkerStatuses.Active => DigitalWorkerStatuses.Idle,
            _ => DigitalWorkerStatuses.Inactive
        };
    }

    internal static string NormalizeTemplateCode(string? value)
    {
        var normalized = NormalizeRequired(value, "Deploy template code is required.").ToLowerInvariant();
        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new InvalidOperationException("Deploy template code can only contain letters, numbers, underscores, hyphens, or dots.");
        }

        return normalized;
    }

    internal static int NormalizeTemplateStatus(int? value)
    {
        return value == DigitalWorkerTemplateStatuses.Disabled
            ? DigitalWorkerTemplateStatuses.Disabled
            : DigitalWorkerTemplateStatuses.Enabled;
    }

    internal static string NormalizeSessionStatus(string? value)
    {
        var normalized = NormalizeOptional(value) ?? WorkerSessionStatuses.Idle;
        return normalized switch
        {
            WorkerSessionStatuses.Starting or
            WorkerSessionStatuses.Idle or
            WorkerSessionStatuses.Busy or
            WorkerSessionStatuses.AuthRequired or
            WorkerSessionStatuses.Error or
            WorkerSessionStatuses.Offline or
            WorkerSessionStatuses.Expired => normalized,
            _ => WorkerSessionStatuses.Error
        };
    }

    internal static string NormalizeCommandType(string? value)
    {
        var normalized = NormalizeOptional(value) ?? WorkerCommandTypes.Smoke;
        return normalized switch
        {
            WorkerCommandTypes.Smoke or
            WorkerCommandTypes.StartTask or
            WorkerCommandTypes.StartBug or
            WorkerCommandTypes.CancelCurrentRun or
            WorkerCommandTypes.StopAfterCurrent or
            WorkerCommandTypes.ReloadConfig or
            WorkerCommandTypes.CodeAudit or
            WorkerCommandTypes.CodeAuditIndexSync => normalized,
            _ => WorkerCommandTypes.Smoke
        };
    }

    private async Task EnsureWorkerCanReceiveCommandAsync(
        DigitalWorkerEntity worker,
        string commandType,
        string? payloadJson)
    {
        if (commandType == WorkerCommandTypes.StartTask)
        {
            var taskId = ReadRequiredPayloadId(payloadJson, "taskId", "task_id");
            var task = await _taskDomain.GetAsync(taskId)
                ?? throw new InvalidOperationException("Target task does not exist.");
            EnsureWorkerCanReceiveAssignedTarget(worker, task.AssigneeId);
            await EnsureWorkerCanUseProjectBackendTechAsync(worker, task.ProjectId);
            return;
        }

        if (commandType == WorkerCommandTypes.StartBug)
        {
            var bugId = ReadRequiredPayloadId(payloadJson, "bugId", "bug_id");
            var bug = await _bugDomain.GetAsync(bugId)
                ?? throw new InvalidOperationException("Target bug does not exist.");
            EnsureWorkerCanReceiveAssignedTarget(worker, bug.DeveloperId);
            await EnsureWorkerCanUseProjectBackendTechAsync(worker, bug.ProjectId);
        }
    }

    private async Task EnsureWorkerCanUseProjectBackendTechAsync(DigitalWorkerEntity worker, string projectId)
    {
        var project = await _projectDomain.GetAsync(projectId);
        if (project is null)
        {
            return;
        }

        EnsureBackendTechCovered(worker, project.BackendTechStack);
    }

    private async Task<string> ResolveCommandTitleAsync(
        DigitalWorkerEntity worker,
        string commandType,
        string? payloadJson,
        string? requestedTitle)
    {
        var normalizedTitle = NormalizeOptional(requestedTitle);
        if (!string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return normalizedTitle;
        }

        if (commandType == WorkerCommandTypes.StartTask)
        {
            var taskId = ReadRequiredPayloadId(payloadJson, "taskId", "task_id");
            var task = await _taskDomain.GetAsync(taskId)
                ?? throw new InvalidOperationException("Target task does not exist.");
            return NormalizeOptional(task.Title) ?? CommandTypeTitle(commandType);
        }

        if (commandType == WorkerCommandTypes.StartBug)
        {
            var bugId = ReadRequiredPayloadId(payloadJson, "bugId", "bug_id");
            var bug = await _bugDomain.GetAsync(bugId)
                ?? throw new InvalidOperationException("Target bug does not exist.");
            return NormalizeOptional(bug.Title) ?? CommandTypeTitle(commandType);
        }

        return NormalizeOptional(worker.Name) ?? CommandTypeTitle(commandType);
    }

    private static string ResolveCommandTitle(WorkerCommandEntity entity)
    {
        return NormalizeOptional(entity.Title) ?? CommandTypeTitle(entity.CommandType);
    }

    private static string CommandTypeTitle(string commandType)
    {
        return commandType switch
        {
            WorkerCommandTypes.StartTask => "Start task",
            WorkerCommandTypes.StartBug => "Start bug",
            WorkerCommandTypes.CancelCurrentRun => "Cancel current run",
            WorkerCommandTypes.StopAfterCurrent => "Stop after current",
            WorkerCommandTypes.ReloadConfig => "Reload config",
            WorkerCommandTypes.CodeAudit => "Code audit",
            WorkerCommandTypes.CodeAuditIndexSync => "Code audit index sync",
            WorkerCommandTypes.Smoke => "Smoke",
            _ => commandType
        };
    }

    private static void EnsureWorkerCanReceiveAssignedTarget(DigitalWorkerEntity worker, string? assigneeId)
    {
        if (!string.Equals(NormalizeOptional(assigneeId), worker.AgentUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Target is assigned to another user.");
        }
    }

    private static string ReadRequiredPayloadId(string? payloadJson, params string[] names)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException("Worker command target id is required.");
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            foreach (var name in names)
            {
                if (document.RootElement.TryGetProperty(name, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString()!.Trim();
                }
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Worker command payload is not valid JSON.");
        }

        throw new InvalidOperationException("Worker command target id is required.");
    }

    internal static string NormalizeCommandStatus(string? value)
    {
        var normalized = NormalizeOptional(value) ?? WorkerCommandStatuses.Pending;
        return normalized switch
        {
            WorkerCommandStatuses.Pending or
            WorkerCommandStatuses.Acked or
            WorkerCommandStatuses.Running or
            WorkerCommandStatuses.Succeeded or
            WorkerCommandStatuses.Failed or
            WorkerCommandStatuses.Cancelled or
            WorkerCommandStatuses.Expired => normalized,
            _ => WorkerCommandStatuses.Pending
        };
    }

    internal static string NormalizeRunType(string? value)
    {
        var normalized = NormalizeOptional(value) ?? WorkerRunTypes.Command;
        return normalized switch
        {
            WorkerRunTypes.Smoke or WorkerRunTypes.Task or WorkerRunTypes.Bug or WorkerRunTypes.Command or WorkerRunTypes.CodeAudit => normalized,
            _ => WorkerRunTypes.Command
        };
    }

    internal static string NormalizeRunStatus(string? value)
    {
        var normalized = NormalizeOptional(value) ?? WorkerRunStatuses.Pending;
        return normalized switch
        {
            WorkerRunStatuses.Pending or
            WorkerRunStatuses.Running or
            WorkerRunStatuses.Success or
            WorkerRunStatuses.CodexFailed or
            WorkerRunStatuses.Timeout or
            WorkerRunStatuses.Cancelled or
            WorkerRunStatuses.Blocked or
            WorkerRunStatuses.McpFailed => normalized,
            _ => WorkerRunStatuses.CodexFailed
        };
    }

    internal static string NormalizeEventLevel(string? value)
    {
        var normalized = NormalizeOptional(value) ?? WorkerEventLevels.Info;
        return normalized switch
        {
            WorkerEventLevels.Info or WorkerEventLevels.Warn or WorkerEventLevels.Error => normalized,
            _ => WorkerEventLevels.Info
        };
    }

    internal static string NormalizeRequired(string? value, string message)
    {
        return NormalizeOptional(value) ?? throw new InvalidOperationException(message);
    }

    internal static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static int NormalizePositive(int? value, int fallback)
    {
        return value.GetValueOrDefault(fallback) > 0 ? value.GetValueOrDefault(fallback) : fallback;
    }

    internal static int NormalizeRange(int? value, int fallback, int min, int max)
    {
        var normalized = NormalizePositive(value, fallback);
        return Math.Clamp(normalized, min, max);
    }

    internal static int NormalizeHeartbeatTimeout(int? value, int fallback)
    {
        var normalized = NormalizePositive(value, fallback);
        return normalized switch
        {
            30 or 60 or 90 or 120 => normalized,
            _ => fallback is 30 or 60 or 90 or 120 ? fallback : 90
        };
    }

    internal static string NormalizePath(string? value, string fallback)
    {
        return NormalizeOptional(value) ?? fallback;
    }

    internal static string NormalizeSandboxMode(string? value)
    {
        var normalized = NormalizeOptional(value) ?? "workspace-write";
        return normalized switch
        {
            "danger-full-access" or "read-only" or "workspace-write" => normalized,
            _ => "workspace-write"
        };
    }

    internal static IReadOnlyList<string> NormalizeIds(IReadOnlyList<string>? ids)
    {
        return (ids ?? [])
            .Select(NormalizeOptional)
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    internal static string? SerializeIds(IReadOnlyList<string> ids)
    {
        return ids.Count == 0 ? null : string.Join(",", ids);
    }

    internal static IReadOnlyList<string> DeserializeIds(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
    }

    private static bool TextContains(string keyword, params string?[] values)
    {
        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAiPlatformCode(string? value)
    {
        var normalized = NormalizeOptional(value) ?? "openai";
        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '_' and not '-' and not '.'))
        {
            throw new InvalidOperationException("AI platform code can only contain letters, numbers, underscores, hyphens, or dots.");
        }

        return normalized.ToLowerInvariant();
    }
}

public sealed class DigitalWorkerRuntimeService :
    AgentSprintServiceBase,
    IDigitalWorkerRuntimeService
{
    private const int AgentTokenLength = 64;
    private const string CodexAgentEnvironment = "codex";
    private const string DigitalWorkerTaskPromptTemplateCode = "digital_worker_task_execution";

    private readonly IAgentTokenDomain _agentTokenDomain;
    private readonly ISystemConfigurationService _configurationService;
    private readonly IDigitalWorkerDomain _workerDomain;
    private readonly ISprintProjectDomain _projectDomain;
    private readonly IGitRepositoryDomain _gitRepositoryDomain;
    private readonly IGitAccountDomain _gitAccountDomain;
    private readonly ISprintRequirementDomain _requirementDomain;
    private readonly ISprintDevelopmentTaskDomain _taskDomain;
    private readonly ISprintBugDomain _bugDomain;
    private readonly ISprintSkillDomain _skillDomain;
    private readonly IPromptTemplateDomain _promptTemplateDomain;
    private readonly IWorkerSessionDomain _sessionDomain;
    private readonly IWorkerCommandDomain _commandDomain;
    private readonly IWorkerCommandLogDomain _commandLogDomain;
    private readonly IWorkerCommandLogBuffer _commandLogBuffer;
    private readonly IWorkerRunDomain _runDomain;
    private readonly IWorkerEventDomain _eventDomain;
    private readonly IDigitalWorkerStartupProbeConfigDomain _startupProbeConfigDomain;
    private readonly IDigitalWorkerStartupProbeResultDomain _startupProbeResultDomain;

    /// <summary>
    /// zh-cn: 创建数字员工运行时服务，供 AgentSprint.Worker 调用注册、心跳、命令 ACK、运行记录和事件上报接口。
    /// en-us: Creates the digital-worker runtime service used by AgentSprint.Worker for registration, heartbeat, command ACK, run records, and event reporting.
    /// </summary>
    /// <summary>
    /// zh-cn: 创建数字员工运行时服务，供 AgentSprint.Worker 调用注册、心跳、命令 ACK、提示词组装、运行记录、敏捷状态回写和事件上报接口；服务会在平台侧校验数字员工范围，避免受控端绕过任务边界。
    /// en-us: Creates the digital-worker runtime service used by AgentSprint.Worker for registration, heartbeat, command ACK, prompt assembly, run records, agile status updates, and event reporting; the service validates worker scope on the platform side so the controlled endpoint cannot bypass task boundaries.
    /// </summary>
    public DigitalWorkerRuntimeService(
        IDigitalWorkerDomain workerDomain,
        IAgentTokenDomain agentTokenDomain,
        ISystemConfigurationService configurationService,
        ISprintProjectDomain projectDomain,
        IGitRepositoryDomain gitRepositoryDomain,
        IGitAccountDomain gitAccountDomain,
        ISprintRequirementDomain requirementDomain,
        ISprintDevelopmentTaskDomain taskDomain,
        ISprintBugDomain bugDomain,
        ISprintSkillDomain skillDomain,
        IPromptTemplateDomain promptTemplateDomain,
        IWorkerSessionDomain sessionDomain,
        IWorkerCommandDomain commandDomain,
        IWorkerCommandLogDomain commandLogDomain,
        IWorkerCommandLogBuffer commandLogBuffer,
        IWorkerRunDomain runDomain,
        IWorkerEventDomain eventDomain,
        IDigitalWorkerStartupProbeConfigDomain startupProbeConfigDomain,
        IDigitalWorkerStartupProbeResultDomain startupProbeResultDomain)
    {
        _workerDomain = workerDomain;
        _agentTokenDomain = agentTokenDomain;
        _configurationService = configurationService;
        _projectDomain = projectDomain;
        _gitRepositoryDomain = gitRepositoryDomain;
        _gitAccountDomain = gitAccountDomain;
        _requirementDomain = requirementDomain;
        _taskDomain = taskDomain;
        _bugDomain = bugDomain;
        _skillDomain = skillDomain;
        _promptTemplateDomain = promptTemplateDomain;
        _sessionDomain = sessionDomain;
        _commandDomain = commandDomain;
        _commandLogDomain = commandLogDomain;
        _commandLogBuffer = commandLogBuffer;
        _runDomain = runDomain;
        _eventDomain = eventDomain;
        _startupProbeConfigDomain = startupProbeConfigDomain;
        _startupProbeResultDomain = startupProbeResultDomain;
    }

    /// <inheritdoc />
    public async Task<WorkerRuntimeConfigResult> GetRuntimeConfigAsync(string workerId)
    {
        var worker = await GetWorkerOrThrowAsync(workerId);
        EnsureWorkerCanPullRuntimeConfig(worker);
        if (worker.Status == DigitalWorkerStatuses.Inactive)
        {
            worker.Status = DigitalWorkerStatuses.Starting;
            await _workerDomain.UpdateAsync(worker);
        }

        string? agentToken = null;
        if (!string.IsNullOrWhiteSpace(worker.AgentTokenId))
        {
            var token = await _agentTokenDomain.GetAsync(worker.AgentTokenId);
            if (token is null || token.Status != 1 || token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Worker Agent Token is invalid or expired.");
            }

            agentToken = token.TokenValue;
        }

        var aiPlatform = await ResolveAiPlatformAsync(worker);
        var projectId = DigitalWorkerManagementService.DeserializeIds(worker.ProjectIds).FirstOrDefault();
        return new WorkerRuntimeConfigResult(
            worker.Id,
            worker.Code,
            worker.Name,
            projectId,
            projectId,
            worker.WorkspaceRoot,
            worker.RunsRoot,
            worker.CodexHome,
            worker.PollIntervalSeconds,
            worker.IdleMaxIntervalSeconds,
            worker.MaxRunMinutes,
            worker.SandboxMode,
            worker.RunSmokeOnStartup,
            worker.SmokePrompt ?? "你好",
            aiPlatform.Provider,
            aiPlatform.Model,
            aiPlatform.OpenAiBaseUrl,
            aiPlatform.ApiKey,
            agentToken,
            worker.ConfigVersion);
    }

    /// <inheritdoc />
    public async Task<WorkerRuntimeConfigResult> GetRuntimeConfigByAgentTokenAsync(string agentToken)
    {
        var token = await ResolveAgentTokenAsync(agentToken);
        var workers = await _workerDomain.ListAsync(worker => worker.AgentTokenId == token.Id);
        var worker = workers.SingleOrDefault()
            ?? throw new InvalidOperationException("Agent Token is not bound to a digital worker.");
        EnsureWorkerCanPullRuntimeConfig(worker);

        token.LastUsedAt = DateTime.UtcNow;
        await _agentTokenDomain.UpdateAsync(token);
        if (worker.Status == DigitalWorkerStatuses.Inactive)
        {
            worker.Status = DigitalWorkerStatuses.Starting;
            await _workerDomain.UpdateAsync(worker);
        }

        return await BuildRuntimeConfigAsync(worker, token.TokenValue);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StartupProbeConfigResult>> ListStartupProbeConfigsAsync()
    {
        var configs = await _startupProbeConfigDomain.ListAsync(entity =>
            entity.Status == DigitalWorkerStartupProbeStatuses.Enabled);
        return configs
            .OrderBy(entity => entity.Sort)
            .ThenBy(entity => entity.Code)
            .Select(DigitalWorkerManagementService.ToResult)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<WorkerPromptResult> GetWorkPromptAsync(string workerId, string targetType, string targetId)
    {
        var worker = await GetWorkerOrThrowAsync(workerId);
        EnsureWorkerCanRunManagedWork(worker);
        var context = await BuildPromptContextAsync(worker, targetType, targetId);
        var template = await GetDigitalWorkerPromptTemplateAsync();
        var renderedPrompt = RenderPromptTemplate(template.Content, BuildPromptVariables(worker, context)).Trim();
        var prompt = string.Join(
                Environment.NewLine + Environment.NewLine,
                BuildWorkerPromptContextSection(worker, context),
                renderedPrompt)
            .Trim();
        return new WorkerPromptResult(
            context.TargetType,
            context.TargetId,
            template.Code,
            template.Name,
            prompt,
            context);
    }

    /// <inheritdoc />
    public async Task<WorkerWorkCompletionResult> CompleteWorkAsync(string workerId, string targetType, string targetId)
    {
        var worker = await GetWorkerOrThrowAsync(workerId);
        EnsureWorkerCanRunManagedWork(worker);
        var normalizedTargetType = NormalizeTargetType(targetType);
        var normalizedTargetId = DigitalWorkerManagementService.NormalizeRequired(targetId, "Target id is required.");
        if (normalizedTargetType == WorkerRunTypes.Task)
        {
            var task = await GetTaskOrThrowAsync(normalizedTargetId);
            await EnsureWorkerCanUseProjectAsync(worker, task.ProjectId);
            EnsureWorkerCanCompleteAssignee(worker, task.AssigneeId);
            task.Status = SprintDevelopmentTaskStatuses.Completed;
            task.CompletedAt ??= DateTime.UtcNow;
            task.StartedAt ??= task.CompletedAt;
            await _taskDomain.UpdateAsync(task);
            await CompleteTaskRequirementIfReadyAsync(task);
            return new WorkerWorkCompletionResult(normalizedTargetType, normalizedTargetId, task.Status);
        }

        var bug = await GetBugOrThrowAsync(normalizedTargetId);
        await EnsureWorkerCanUseProjectAsync(worker, bug.ProjectId);
        EnsureWorkerCanCompleteAssignee(worker, bug.DeveloperId);
        bug.Status = SprintBugStatuses.FixedReadyForRegression;
        bug.FixedAt ??= DateTime.UtcNow;
        await _bugDomain.UpdateAsync(bug);
        await MarkRequirementReadyIfBugClearedAsync(bug);
        return new WorkerWorkCompletionResult(normalizedTargetType, normalizedTargetId, bug.Status);
    }

    /// <inheritdoc />
    public async Task<WorkerSessionResult> RegisterSessionAsync(RegisterWorkerSessionRequest request)
    {
        var worker = await GetWorkerOrThrowAsync(request.WorkerId);
        EnsureWorkerCanPullRuntimeConfig(worker);
        if (worker.Status == DigitalWorkerStatuses.Inactive)
        {
            worker.Status = DigitalWorkerStatuses.Starting;
            await _workerDomain.UpdateAsync(worker);
        }

        var oldSessions = await _sessionDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            entity.Status != WorkerSessionStatuses.Offline &&
            entity.Status != WorkerSessionStatuses.Expired);
        foreach (var oldSession in oldSessions)
        {
            oldSession.Status = WorkerSessionStatuses.Expired;
            oldSession.StoppedAt ??= DateTime.UtcNow;
            await _sessionDomain.UpdateAsync(oldSession);
            await FailUnfinishedSessionWorkAsync(
                worker,
                oldSession,
                "Worker session expired because a new worker instance registered.");
        }

        if (oldSessions.Count > 0)
        {
            await RestoreWorkerStatusAfterRunAsync(worker);
        }

        var status = string.IsNullOrWhiteSpace(request.ErrorSummary)
            ? WorkerSessionStatuses.Starting
            : WorkerSessionStatuses.Error;
        var entity = new WorkerSessionEntity
        {
            WorkerId = worker.Id,
            InstanceId = DigitalWorkerManagementService.NormalizeRequired(request.InstanceId, "InstanceId is required."),
            HostName = DigitalWorkerManagementService.NormalizeOptional(request.HostName),
            ContainerId = DigitalWorkerManagementService.NormalizeOptional(request.ContainerId),
            Status = status,
            CodexVersion = DigitalWorkerManagementService.NormalizeOptional(request.CodexVersion),
            GitVersion = DigitalWorkerManagementService.NormalizeOptional(request.GitVersion),
            DotnetVersion = DigitalWorkerManagementService.NormalizeOptional(request.DotnetVersion),
            NodeVersion = DigitalWorkerManagementService.NormalizeOptional(request.NodeVersion),
            ConfigTomlExists = request.ConfigTomlExists,
            CodexHome = DigitalWorkerManagementService.NormalizeOptional(request.CodexHome),
            WorkspaceRoot = DigitalWorkerManagementService.NormalizeOptional(request.WorkspaceRoot),
            RunsRoot = DigitalWorkerManagementService.NormalizeOptional(request.RunsRoot),
            ErrorSummary = DigitalWorkerManagementService.NormalizeOptional(request.ErrorSummary),
            LastHeartbeatAt = DateTime.UtcNow
        };
        await _sessionDomain.CreateAsync(entity);
        await CreateEventAsync(worker.Id, entity.Id, null, "worker_started", WorkerEventLevels.Info, "Worker session registered.", null);
        return DigitalWorkerManagementService.ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<WorkerHeartbeatResult> HeartbeatAsync(WorkerHeartbeatRequest request)
    {
        var worker = await GetWorkerOrThrowAsync(request.WorkerId);
        EnsureWorkerCanHeartbeat(worker);
        var session = await GetSessionOrThrowAsync(request.SessionId);
        EnsureSessionBelongsToWorker(session, worker.Id);

        session.Status = CanWorkerReturnCommands(worker)
            ? DigitalWorkerManagementService.NormalizeSessionStatus(request.Status)
            : WorkerSessionStatuses.Offline;
        session.CurrentRunId = DigitalWorkerManagementService.NormalizeOptional(request.CurrentRunId);
        session.ErrorSummary = DigitalWorkerManagementService.NormalizeOptional(request.ErrorSummary);
        session.LastHeartbeatAt = DateTime.UtcNow;
        if (session.Status == WorkerSessionStatuses.Offline)
        {
            session.StoppedAt ??= DateTime.UtcNow;
        }

        await _sessionDomain.UpdateAsync(session);
        await UpdateWorkerStatusFromHeartbeatAsync(worker, session);
        await ExpireStaleSessionsAsync(worker);
        await ExpireCommandsAsync(worker.Id);

        var commands = await _commandDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            entity.Status == WorkerCommandStatuses.Pending &&
            (entity.SessionId == null || entity.SessionId == session.Id));
        var dispatchableCommands = commands
            .Where(entity => CanDispatchCommand(worker, session.Status, entity.CommandType))
            .ToList();

        return new WorkerHeartbeatResult(
            worker.Id,
            session.Id,
            session.Status,
            ResolveNextIntervalSeconds(session.Status),
            dispatchableCommands.OrderBy(entity => entity.CreateTime)
                .Select(DigitalWorkerManagementService.ToResult)
                .ToList());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StartupProbeResult>> ReportStartupProbeResultsAsync(
        ReportStartupProbeResultsRequest request)
    {
        var worker = await GetWorkerOrThrowAsync(request.WorkerId);
        EnsureWorkerCanHeartbeat(worker);
        var sessionId = DigitalWorkerManagementService.NormalizeRequired(request.SessionId, "SessionId is required.");
        var session = await GetSessionOrThrowAsync(sessionId);
        EnsureSessionBelongsToWorker(session, worker.Id);

        var instanceId = DigitalWorkerManagementService.NormalizeRequired(request.InstanceId, "InstanceId is required.");
        var previousResults = await _startupProbeResultDomain.ListAsync(entity => entity.WorkerId == worker.Id);
        foreach (var previousResult in previousResults)
        {
            if (previousResult.SessionId != sessionId || previousResult.InstanceId != instanceId)
            {
                await _startupProbeResultDomain.DeleteAsync(previousResult.Id);
            }
        }

        var entities = new List<DigitalWorkerStartupProbeResultEntity>();
        var currentResults = previousResults
            .Where(entity => entity.SessionId == sessionId && entity.InstanceId == instanceId)
            .GroupBy(entity => entity.ProbeConfigId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(entity => entity.ReportedAt).First(), StringComparer.Ordinal);
        foreach (var item in request.Results)
        {
            var probeConfigId = DigitalWorkerManagementService.NormalizeRequired(item.ProbeConfigId, "Probe config id is required.");
            var entity = currentResults.GetValueOrDefault(probeConfigId);
            var isNew = entity is null;
            entity ??= new DigitalWorkerStartupProbeResultEntity
            {
                WorkerId = worker.Id,
                SessionId = sessionId,
                InstanceId = instanceId,
                ProbeConfigId = probeConfigId
            };
            entity.WorkerDeployRenderId = DigitalWorkerManagementService.NormalizeOptional(request.WorkerDeployRenderId);
            entity.ProbeCode = DigitalWorkerManagementService.NormalizeRequired(item.ProbeCode, "Probe code is required.");
            entity.ProbeName = DigitalWorkerManagementService.NormalizeRequired(item.ProbeName, "Probe name is required.");
            entity.Command = DigitalWorkerManagementService.NormalizeRequired(item.Command, "Probe command is required.");
            entity.ExitCode = item.ExitCode;
            entity.Stdout = TrimProbeText(item.Stdout);
            entity.Stderr = TrimProbeText(item.Stderr);
            entity.Error = DigitalWorkerManagementService.NormalizeOptional(item.Error);
            entity.Passed = item.Passed;
            entity.Required = item.Required;
            entity.ReportedAt = DateTime.UtcNow;
            if (isNew)
            {
                await _startupProbeResultDomain.CreateAsync(entity);
                currentResults[probeConfigId] = entity;
            }
            else
            {
                await _startupProbeResultDomain.UpdateAsync(entity);
            }

            entities.Add(entity);
        }

        var requiredProbeResults = entities.Where(entity => entity.Required).ToList();
        var requiredProbesPassed = requiredProbeResults.Count > 0 && requiredProbeResults.All(entity => entity.Passed);
        var inferredCapabilities = InferBackendCapabilitiesFromProbeResults(entities);
        if (!string.IsNullOrWhiteSpace(inferredCapabilities))
        {
            worker.BackendTechCapabilities = inferredCapabilities;
        }

        if (requiredProbesPassed)
        {
            if (worker.Status is DigitalWorkerStatuses.Inactive or DigitalWorkerStatuses.Starting or DigitalWorkerStatuses.Active)
            {
                worker.Status = DigitalWorkerStatuses.Idle;
                await _workerDomain.UpdateAsync(worker);
            }
        }
        else if (worker.Status == DigitalWorkerStatuses.Inactive)
        {
            worker.Status = DigitalWorkerStatuses.Starting;
            await _workerDomain.UpdateAsync(worker);
        }

        await CreateEventAsync(
            worker.Id,
            sessionId,
            null,
            "worker_startup_probe_reported",
            requiredProbesPassed ? WorkerEventLevels.Info : WorkerEventLevels.Warn,
            "Worker startup probe results reported.",
            JsonSerializer.Serialize(new
            {
                total = entities.Count,
                passed = entities.Count(entity => entity.Passed),
                requiredFailed = entities.Count(entity => entity.Required && !entity.Passed),
                requiredPassed = requiredProbesPassed,
                backendTechCapabilities = inferredCapabilities,
                request.WorkerDeployRenderId
            }));
        return entities.Select(DigitalWorkerManagementService.ToResult).ToList();
    }

    /// <inheritdoc />
    public async Task<WorkerCommandResult> AckCommandAsync(string commandId, AckWorkerCommandRequest request)
    {
        var command = await GetCommandOrThrowAsync(commandId);
        var session = await GetSessionOrThrowAsync(request.SessionId);
        EnsureCommandCanUseSession(command, session);
        if (command.Status == WorkerCommandStatuses.Pending)
        {
            command.Status = WorkerCommandStatuses.Acked;
            command.SessionId ??= session.Id;
            command.AckedAt = DateTime.UtcNow;
            await _commandDomain.UpdateAsync(command);
        }

        return DigitalWorkerManagementService.ToResult(command);
    }

    /// <inheritdoc />
    public async Task<WorkerCommandResult> StartCommandAsync(string commandId, AckWorkerCommandRequest request)
    {
        var command = await GetCommandOrThrowAsync(commandId);
        var session = await GetSessionOrThrowAsync(request.SessionId);
        EnsureCommandCanUseSession(command, session);
        if (command.Status is WorkerCommandStatuses.Pending or WorkerCommandStatuses.Acked)
        {
            command.Status = WorkerCommandStatuses.Running;
            command.SessionId ??= session.Id;
            command.AckedAt ??= DateTime.UtcNow;
            command.StartedAt = DateTime.UtcNow;
            await _commandDomain.UpdateAsync(command);
        }

        return DigitalWorkerManagementService.ToResult(command);
    }

    /// <inheritdoc />
    public async Task<WorkerRunResult> StartRunAsync(StartWorkerRunRequest request)
    {
        var worker = await GetWorkerOrThrowAsync(request.WorkerId);
        var session = await GetSessionOrThrowAsync(request.SessionId);
        EnsureSessionBelongsToWorker(session, worker.Id);
        var entity = new WorkerRunEntity
        {
            WorkerId = worker.Id,
            SessionId = session.Id,
            CommandId = DigitalWorkerManagementService.NormalizeOptional(request.CommandId),
            RunType = DigitalWorkerManagementService.NormalizeRunType(request.RunType),
            TargetType = DigitalWorkerManagementService.NormalizeOptional(request.TargetType),
            TargetId = DigitalWorkerManagementService.NormalizeOptional(request.TargetId),
            Status = DigitalWorkerManagementService.NormalizeRunStatus(request.Status),
            WorkspacePath = DigitalWorkerManagementService.NormalizeOptional(request.WorkspacePath),
            PromptPath = DigitalWorkerManagementService.NormalizeOptional(request.PromptPath),
            StdoutPath = DigitalWorkerManagementService.NormalizeOptional(request.StdoutPath),
            StderrPath = DigitalWorkerManagementService.NormalizeOptional(request.StderrPath),
            FinalPath = DigitalWorkerManagementService.NormalizeOptional(request.FinalPath),
            ManifestPath = DigitalWorkerManagementService.NormalizeOptional(request.ManifestPath)
        };
        await _runDomain.CreateAsync(entity);
        if (entity.Status == WorkerRunStatuses.Running)
        {
            if (worker.Status == DigitalWorkerStatuses.Idle)
            {
                worker.Status = DigitalWorkerStatuses.Working;
                await _workerDomain.UpdateAsync(worker);
            }

            await MarkTargetStartedAsync(worker, entity);
        }

        session.Status = WorkerSessionStatuses.Busy;
        session.CurrentRunId = entity.Id;
        session.LastHeartbeatAt = DateTime.UtcNow;
        await _sessionDomain.UpdateAsync(session);
        await CreateEventAsync(worker.Id, session.Id, entity.Id, "codex_started", WorkerEventLevels.Info, "Worker run started.", null);
        return DigitalWorkerManagementService.ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<WorkerRunResult> FinishRunAsync(string runId, FinishWorkerRunRequest request)
    {
        var run = await GetRunOrThrowAsync(runId);
        run.Status = DigitalWorkerManagementService.NormalizeRunStatus(request.Status);
        run.ExitCode = request.ExitCode;
        run.TimedOut = request.TimedOut;
        run.Error = DigitalWorkerManagementService.NormalizeOptional(request.Error);
        run.CompletedAt = DateTime.UtcNow;
        await _runDomain.UpdateAsync(run);

        var session = await _sessionDomain.GetAsync(run.SessionId);
        if (session is not null)
        {
            session.Status = run.Status == WorkerRunStatuses.Success
                ? WorkerSessionStatuses.Idle
                : WorkerSessionStatuses.Error;
            session.CurrentRunId = null;
            session.ErrorSummary = run.Error;
            session.LastHeartbeatAt = DateTime.UtcNow;
            await _sessionDomain.UpdateAsync(session);
        }

        if (!string.IsNullOrWhiteSpace(run.CommandId))
        {
            var command = await _commandDomain.GetAsync(run.CommandId);
            if (command is not null)
            {
                command.Status = run.Status == WorkerRunStatuses.Success
                    ? WorkerCommandStatuses.Succeeded
                    : WorkerCommandStatuses.Failed;
                command.CompletedAt = DateTime.UtcNow;
                command.ResultJson = DigitalWorkerManagementService.NormalizeOptional(request.ResultJson);
                command.ChangedFilesJson = DigitalWorkerManagementService.NormalizeOptional(request.ChangedFilesJson);
                command.GitCommitId = DigitalWorkerManagementService.NormalizeOptional(request.GitCommitId);
                command.Error = run.Error;
                await _commandDomain.UpdateAsync(command);
            }
        }

        var worker = await _workerDomain.GetAsync(run.WorkerId);
        if (worker is not null)
        {
            await RestoreWorkerStatusAfterRunAsync(worker);
        }

        await CreateEventAsync(run.WorkerId, run.SessionId, run.Id, "codex_finished", ResolveRunEventLevel(run.Status), "Worker run finished.", request.ResultJson);
        return DigitalWorkerManagementService.ToResult(run);
    }

    private async Task MarkTargetStartedAsync(DigitalWorkerEntity worker, WorkerRunEntity run)
    {
        var normalizedTargetType = DigitalWorkerManagementService.NormalizeOptional(run.TargetType);
        if (normalizedTargetType is null && run.RunType == WorkerRunTypes.Task)
        {
            normalizedTargetType = WorkerRunTypes.Task;
        }

        var normalizedTargetId = DigitalWorkerManagementService.NormalizeOptional(run.TargetId);
        if (normalizedTargetType != WorkerRunTypes.Task || string.IsNullOrWhiteSpace(normalizedTargetId))
        {
            return;
        }

        var task = await GetTaskOrThrowAsync(normalizedTargetId);
        await EnsureWorkerCanUseProjectAsync(worker, task.ProjectId);
        EnsureWorkerCanCompleteAssignee(worker, task.AssigneeId);
        if (task.Status == SprintDevelopmentTaskStatuses.Assigned)
        {
            task.Status = SprintDevelopmentTaskStatuses.InProgress;
            task.StartedAt ??= DateTime.UtcNow;
            await _taskDomain.UpdateAsync(task);
        }

        await MarkRequirementDevelopingFromTaskAsync(task, worker.AgentUserId);
    }

    private static bool CanDispatchCommand(DigitalWorkerEntity worker, string sessionStatus, string commandType)
    {
        if (commandType is WorkerCommandTypes.CancelCurrentRun or WorkerCommandTypes.StopAfterCurrent)
        {
            return sessionStatus == WorkerSessionStatuses.Busy;
        }

        if (commandType == WorkerCommandTypes.ReloadConfig)
        {
            return true;
        }

        return CanWorkerReturnCommands(worker) && sessionStatus == WorkerSessionStatuses.Idle;
    }

    private async Task MarkRequirementDevelopingFromTaskAsync(SprintDevelopmentTaskEntity task, string userId)
    {
        var requirement = await GetRequirementOrThrowAsync(task.RequirementId);
        if (requirement.Status is
            SprintRequirementStatuses.Approved or
            SprintRequirementStatuses.ReadyForDevelopment or
            SprintRequirementStatuses.Decomposed)
        {
            requirement.Status = SprintRequirementStatuses.Developing;
        }
        else if (requirement.Status != SprintRequirementStatuses.Developing)
        {
            return;
        }

        requirement.DeveloperId = DigitalWorkerManagementService.NormalizeOptional(task.AssigneeId)
            ?? DigitalWorkerManagementService.NormalizeOptional(userId);
        await _requirementDomain.UpdateAsync(requirement);
    }

    /// <inheritdoc />
    public async Task<WorkerEventResult> ReportEventAsync(ReportWorkerEventRequest request)
    {
        var worker = await GetWorkerOrThrowAsync(request.WorkerId);
        var entity = await CreateEventAsync(
            worker.Id,
            DigitalWorkerManagementService.NormalizeOptional(request.SessionId),
            DigitalWorkerManagementService.NormalizeOptional(request.RunId),
            DigitalWorkerManagementService.NormalizeRequired(request.EventType, "Event type is required."),
            DigitalWorkerManagementService.NormalizeEventLevel(request.Level),
            DigitalWorkerManagementService.NormalizeRequired(request.Message, "Event message is required."),
            DigitalWorkerManagementService.NormalizeOptional(request.PayloadJson));
        return DigitalWorkerManagementService.ToResult(entity);
    }

    /// <inheritdoc />
    public async Task<WorkerCommandLogSnapshotResult> AppendCommandLogAsync(
        string workerId,
        AppendWorkerCommandLogRequest request)
    {
        var worker = await GetWorkerOrThrowAsync(workerId);
        EnsureWorkerCanRunManagedWork(worker);
        var command = await GetCommandOrThrowAsync(DigitalWorkerManagementService.NormalizeRequired(
            request.CommandId,
            "Command id is required."));
        if (command.WorkerId != worker.Id)
        {
            throw new InvalidOperationException("Worker command does not belong to the current worker.");
        }

        var sessionId = DigitalWorkerManagementService.NormalizeOptional(request.SessionId) ??
            DigitalWorkerManagementService.NormalizeOptional(command.SessionId);
        WorkerSessionEntity? session = null;
        if (sessionId is not null)
        {
            session = await GetSessionOrThrowAsync(sessionId);
            EnsureSessionBelongsToWorker(session, worker.Id);
        }

        var runId = DigitalWorkerManagementService.NormalizeOptional(request.RunId);
        if (runId is not null)
        {
            var run = await GetRunOrThrowAsync(runId);
            if (run.WorkerId != worker.Id || (sessionId is not null && run.SessionId != sessionId))
            {
                throw new InvalidOperationException("Worker run does not belong to the current worker session.");
            }
        }

        var instanceId = DigitalWorkerManagementService.NormalizeOptional(request.InstanceId) ??
            session?.InstanceId ??
            "unknown";
        var snapshot = _commandLogBuffer.Append(
            worker.Id,
            command.Id,
            sessionId,
            runId,
            instanceId,
            request.Chunk,
            request.Sequence,
            request.Completed);
        if (request.Completed)
        {
            await PersistCommandLogAsync(worker.Id, command.Id, snapshot, request.StartedAt, request.CompletedAt);
            _commandLogBuffer.Remove(command.Id);
        }

        return snapshot;
    }

    private async Task PersistCommandLogAsync(
        string workerId,
        string commandId,
        WorkerCommandLogSnapshotResult snapshot,
        DateTime? startedAt,
        DateTime? completedAt)
    {
        var existing = (await _commandLogDomain.ListAsync(entity => entity.CommandId == commandId))
            .OrderByDescending(entity => entity.CreateTime)
            .FirstOrDefault();
        if (existing is null)
        {
            await _commandLogDomain.CreateAsync(new WorkerCommandLogEntity
            {
                WorkerId = workerId,
                CommandId = commandId,
                SessionId = snapshot.SessionId,
                InstanceId = snapshot.InstanceId,
                RunId = snapshot.RunId,
                LogText = snapshot.LogText,
                StartedAt = startedAt,
                CompletedAt = completedAt ?? DateTime.UtcNow
            });
        }
        else
        {
            existing.SessionId = snapshot.SessionId;
            existing.InstanceId = snapshot.InstanceId;
            existing.RunId = snapshot.RunId;
            existing.LogText = snapshot.LogText;
            existing.StartedAt = startedAt;
            existing.CompletedAt = completedAt ?? DateTime.UtcNow;
            await _commandLogDomain.UpdateAsync(existing);
        }

        await TrimCommandLogsAsync(workerId, snapshot.InstanceId);
    }

    private async Task TrimCommandLogsAsync(string workerId, string instanceId)
    {
        var logs = await _commandLogDomain.ListAsync(entity =>
            entity.WorkerId == workerId &&
            entity.InstanceId == instanceId);
        foreach (var expired in logs
            .OrderByDescending(entity => entity.CreateTime)
            .Skip(200))
        {
            await _commandLogDomain.DeleteAsync(expired.Id);
        }
    }

    private async Task<DigitalWorkerEntity> GetWorkerOrThrowAsync(string id)
    {
        var entity = await _workerDomain.GetAsync(id);
        if (entity is null)
        {
            var workers = await _workerDomain.ListAsync(worker => worker.Code == id);
            entity = workers.SingleOrDefault();
        }

        return entity ?? throw new InvalidOperationException("Worker does not exist.");
    }

    private static void EnsureWorkerActive(DigitalWorkerEntity worker)
    {
        EnsureWorkerCanRunManagedWork(worker);
    }

    private static void EnsureWorkerCanRunManagedWork(DigitalWorkerEntity worker)
    {
        if (worker.Status is DigitalWorkerStatuses.Disabled or DigitalWorkerStatuses.Maintenance)
        {
            throw new InvalidOperationException("Worker is not available for managed work.");
        }
    }

    private static void EnsureWorkerCanPullRuntimeConfig(DigitalWorkerEntity worker)
    {
        if (worker.Status == DigitalWorkerStatuses.Disabled || worker.IsDelete != 0)
        {
            throw new InvalidOperationException("Worker is disabled or deleted.");
        }
    }

    private static void EnsureWorkerCanHeartbeat(DigitalWorkerEntity worker)
    {
        if (worker.Status == DigitalWorkerStatuses.Disabled || worker.IsDelete != 0)
        {
            throw new InvalidOperationException("Worker is disabled or deleted.");
        }
    }

    private static bool CanWorkerReturnCommands(DigitalWorkerEntity worker)
    {
        return worker.Status is DigitalWorkerStatuses.Idle or DigitalWorkerStatuses.Working or DigitalWorkerStatuses.Active;
    }

    private async Task UpdateWorkerStatusFromHeartbeatAsync(DigitalWorkerEntity worker, WorkerSessionEntity session)
    {
        if (worker.Status == DigitalWorkerStatuses.Inactive)
        {
            worker.Status = DigitalWorkerStatuses.Starting;
            await _workerDomain.UpdateAsync(worker);
            return;
        }

        if (worker.Status == DigitalWorkerStatuses.Working && session.Status == WorkerSessionStatuses.Idle)
        {
            await RestoreWorkerStatusAfterRunAsync(worker);
        }
    }

    private async Task RestoreWorkerStatusAfterRunAsync(DigitalWorkerEntity worker)
    {
        if (worker.Status == DigitalWorkerStatuses.Maintenance)
        {
            return;
        }

        var runningRuns = await _runDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            (entity.Status == WorkerRunStatuses.Pending || entity.Status == WorkerRunStatuses.Running));
        var runningCommands = await _commandDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            (entity.Status == WorkerCommandStatuses.Acked || entity.Status == WorkerCommandStatuses.Running));
        if (runningRuns.Count == 0 && runningCommands.Count == 0 && worker.Status == DigitalWorkerStatuses.Working)
        {
            worker.Status = DigitalWorkerStatuses.Idle;
            await _workerDomain.UpdateAsync(worker);
        }
    }

    private static string? TrimProbeText(string? value)
    {
        var normalized = DigitalWorkerManagementService.NormalizeOptional(value);
        return normalized is null || normalized.Length <= 4000 ? normalized : normalized[..4000];
    }

    private static string? InferBackendCapabilitiesFromProbeResults(
        IReadOnlyCollection<DigitalWorkerStartupProbeResultEntity> results)
    {
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results.Where(entity => entity.Passed))
        {
            var code = result.ProbeCode;
            var command = result.Command;
            if (ContainsProbeSignal(code, command, "dotnet"))
            {
                capabilities.Add("dotnet");
            }

            if (ContainsProbeSignal(code, command, "java"))
            {
                capabilities.Add("java");
            }

            if (ContainsProbeSignal(code, command, "python"))
            {
                capabilities.Add("python");
            }

            if (ContainsProbeSignal(code, command, "node"))
            {
                capabilities.Add("node");
            }

            if (ContainsProbeSignal(code, command, "go"))
            {
                capabilities.Add("go");
            }
        }

        return capabilities.Count == 0
            ? null
            : DigitalWorkerManagementService.NormalizeBackendTechCapabilities(string.Join(",", capabilities.OrderBy(item => item)));
    }

    private static bool ContainsProbeSignal(string? code, string? command, string signal)
    {
        return (!string.IsNullOrWhiteSpace(code) && code.Contains(signal, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(command) && command.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<WorkerPromptContextResult> BuildPromptContextAsync(
        DigitalWorkerEntity worker,
        string targetType,
        string targetId)
    {
        var normalizedTargetType = NormalizeTargetType(targetType);
        var normalizedTargetId = DigitalWorkerManagementService.NormalizeRequired(targetId, "Target id is required.");
        if (normalizedTargetType == WorkerRunTypes.Task)
        {
            var task = await GetTaskOrThrowAsync(normalizedTargetId);
            var requirement = await GetRequirementOrThrowAsync(task.RequirementId);
            var project = await GetProjectOrThrowAsync(task.ProjectId);
            await EnsureWorkerCanUseProjectAsync(worker, project.Id);
            EnsureWorkerCanCompleteAssignee(worker, task.AssigneeId);
            var skillContext = await BuildSkillContextAsync(worker, requirement);
            var gitConfig = await ResolveGitRepositoryConfigAsync(project);
            return CreateTaskPromptContext(worker, project, requirement, task, skillContext, gitConfig);
        }

        var bug = await GetBugOrThrowAsync(normalizedTargetId);
        var bugRequirement = await GetRequirementOrThrowAsync(bug.RequirementId);
        var bugProject = await GetProjectOrThrowAsync(bug.ProjectId);
        await EnsureWorkerCanUseProjectAsync(worker, bugProject.Id);
        EnsureWorkerCanCompleteAssignee(worker, bug.DeveloperId);
        var bugSkillContext = await BuildSkillContextAsync(worker, bugRequirement);
        var bugGitConfig = await ResolveGitRepositoryConfigAsync(bugProject);
        return CreateBugPromptContext(worker, bugProject, bugRequirement, bug, bugSkillContext, bugGitConfig);
    }

    private static WorkerPromptContextResult CreateTaskPromptContext(
        DigitalWorkerEntity worker,
        SprintProjectEntity project,
        SprintRequirementEntity requirement,
        SprintDevelopmentTaskEntity task,
        string skillContext,
        WorkerGitRepositoryConfig? gitConfig)
    {
        var repositoryReference = SanitizeRepositoryReference(gitConfig?.RepositoryUrl);
        return new WorkerPromptContextResult(
            WorkerRunTypes.Task,
            task.Id,
            project.Id,
            project.Code,
            project.Name,
            gitConfig?.RepositoryId,
            gitConfig?.AccountId,
            repositoryReference,
            gitConfig?.RepositoryUrl,
            gitConfig?.DefaultBranch,
            gitConfig?.Username,
            gitConfig?.AccessToken,
            gitConfig?.CommitAuthorName,
            gitConfig?.CommitAuthorEmail,
            BuildWorkspacePath(worker.WorkspaceRoot, project.Code),
            requirement.Id,
            requirement.Title,
            requirement.Description,
            requirement.Status,
            requirement.EndpointId,
            requirement.ModuleId,
            task.Id,
            task.Title,
            task.Description,
            null,
            null,
            null,
            null,
            null,
            skillContext,
            $"/worker-runtime/work/{WorkerRunTypes.Task}/{task.Id}/complete",
            "Worker will call the platform completion API after Codex exits successfully; Codex must not call AgentSprint MCP.");
    }

    private static WorkerPromptContextResult CreateBugPromptContext(
        DigitalWorkerEntity worker,
        SprintProjectEntity project,
        SprintRequirementEntity requirement,
        SprintBugEntity bug,
        string skillContext,
        WorkerGitRepositoryConfig? gitConfig)
    {
        var repositoryReference = SanitizeRepositoryReference(gitConfig?.RepositoryUrl);
        return new WorkerPromptContextResult(
            WorkerRunTypes.Bug,
            bug.Id,
            project.Id,
            project.Code,
            project.Name,
            gitConfig?.RepositoryId,
            gitConfig?.AccountId,
            repositoryReference,
            gitConfig?.RepositoryUrl,
            gitConfig?.DefaultBranch,
            gitConfig?.Username,
            gitConfig?.AccessToken,
            gitConfig?.CommitAuthorName,
            gitConfig?.CommitAuthorEmail,
            BuildWorkspacePath(worker.WorkspaceRoot, project.Code),
            requirement.Id,
            requirement.Title,
            requirement.Description,
            requirement.Status,
            requirement.EndpointId,
            requirement.ModuleId,
            null,
            null,
            null,
            bug.Id,
            bug.Title,
            bug.Description,
            bug.Environment,
            bug.Severity,
            skillContext,
            $"/worker-runtime/work/{WorkerRunTypes.Bug}/{bug.Id}/complete",
            "Worker will call the platform completion API after Codex exits successfully; Codex must not call AgentSprint MCP.");
    }

    private async Task<PromptTemplateEntity> GetDigitalWorkerPromptTemplateAsync()
    {
        var templates = await _promptTemplateDomain.ListAsync(entity =>
            entity.AgentEnvironment == CodexAgentEnvironment &&
            entity.Code == DigitalWorkerTaskPromptTemplateCode &&
            entity.Status == 1);
        return templates.OrderBy(entity => entity.Sort).FirstOrDefault()
            ?? throw new InvalidOperationException($"Codex prompt template '{DigitalWorkerTaskPromptTemplateCode}' is not configured.");
    }

    private static string BuildWorkerPromptContextSection(
        DigitalWorkerEntity worker,
        WorkerPromptContextResult context)
    {
        return string.Join(
            Environment.NewLine,
            "Digital worker runtime context:",
            $"- Worker ID: {worker.Id}",
            $"- Worker code: {worker.Code}",
            $"- Worker name: {worker.Name}",
            $"- Agent user ID: {worker.AgentUserId}",
            $"- Employee type: {worker.EmployeeType}",
            $"- Worker type: {worker.WorkerType}",
            $"- Worker status: {worker.Status}",
            $"- Config version: {worker.ConfigVersion}",
            $"- Workspace root: {worker.WorkspaceRoot}",
            $"- Current workspace path: {context.WorkspacePath}",
            $"- Runs/log root: {worker.RunsRoot}",
            $"- Codex Home: {worker.CodexHome}",
            $"- Sandbox mode: {worker.SandboxMode}",
            $"- Run smoke on startup: {(worker.RunSmokeOnStartup ? "true" : "false")}",
            $"- Smoke prompt: {worker.SmokePrompt ?? string.Empty}",
            $"- Max run minutes: {worker.MaxRunMinutes}",
            $"- Poll interval seconds: {worker.PollIntervalSeconds}",
            $"- Idle max interval seconds: {worker.IdleMaxIntervalSeconds}",
            $"- Heartbeat timeout seconds: {worker.HeartbeatTimeoutSeconds}",
            $"- Max concurrent runs: {worker.MaxConcurrentRuns}",
            $"- Codex provider: {worker.CodexProvider}",
            $"- Codex model: {worker.CodexModel}",
            $"- OpenAI base URL: {worker.OpenAiBaseUrl ?? string.Empty}",
            $"- Project IDs: {worker.ProjectIds ?? string.Empty}",
            $"- Endpoint IDs: {worker.EndpointIds ?? string.Empty}",
            $"- Skill IDs: {worker.SkillIds ?? string.Empty}",
            $"- Description: {worker.Description ?? string.Empty}");
    }

    private static IReadOnlyDictionary<string, string> BuildPromptVariables(
        DigitalWorkerEntity worker,
        WorkerPromptContextResult context)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["workerId"] = worker.Id,
            ["workerCode"] = worker.Code,
            ["workerName"] = worker.Name,
            ["agentUserId"] = worker.AgentUserId,
            ["employeeType"] = worker.EmployeeType,
            ["workerType"] = worker.WorkerType,
            ["workerStatus"] = worker.Status,
            ["maxConcurrentRuns"] = worker.MaxConcurrentRuns.ToString(),
            ["heartbeatTimeoutSeconds"] = worker.HeartbeatTimeoutSeconds.ToString(),
            ["pollIntervalSeconds"] = worker.PollIntervalSeconds.ToString(),
            ["idleMaxIntervalSeconds"] = worker.IdleMaxIntervalSeconds.ToString(),
            ["maxRunMinutes"] = worker.MaxRunMinutes.ToString(),
            ["workspaceRoot"] = worker.WorkspaceRoot,
            ["runsRoot"] = worker.RunsRoot,
            ["codexHome"] = worker.CodexHome,
            ["sandboxMode"] = worker.SandboxMode,
            ["runSmokeOnStartup"] = worker.RunSmokeOnStartup ? "true" : "false",
            ["smokePrompt"] = worker.SmokePrompt ?? string.Empty,
            ["codexProvider"] = worker.CodexProvider,
            ["codexModel"] = worker.CodexModel,
            ["openAiBaseUrl"] = worker.OpenAiBaseUrl ?? string.Empty,
            ["configVersion"] = worker.ConfigVersion.ToString(),
            ["workerDescription"] = worker.Description ?? string.Empty,
            ["projectIds"] = worker.ProjectIds ?? string.Empty,
            ["endpointIds"] = worker.EndpointIds ?? string.Empty,
            ["skillIds"] = worker.SkillIds ?? string.Empty,
            ["targetType"] = context.TargetType,
            ["targetId"] = context.TargetId,
            ["projectId"] = context.ProjectId,
            ["projectCode"] = context.ProjectCode,
            ["projectName"] = context.ProjectName,
            ["repositoryReference"] = context.RepositoryReference ?? string.Empty,
            ["workspacePath"] = context.WorkspacePath ?? string.Empty,
            ["requirementId"] = context.RequirementId,
            ["requirementTitle"] = context.RequirementTitle,
            ["requirementDescription"] = context.RequirementDescription ?? string.Empty,
            ["requirementStatus"] = context.RequirementStatus ?? string.Empty,
            ["endpointId"] = context.EndpointId ?? string.Empty,
            ["moduleId"] = context.ModuleId ?? string.Empty,
            ["taskId"] = context.TaskId ?? string.Empty,
            ["taskTitle"] = context.TaskTitle ?? string.Empty,
            ["taskDescription"] = context.TaskDescription ?? string.Empty,
            ["bugId"] = context.BugId ?? string.Empty,
            ["bugTitle"] = context.BugTitle ?? string.Empty,
            ["bugDescription"] = context.BugDescription ?? string.Empty,
            ["bugEnvironment"] = context.BugEnvironment ?? string.Empty,
            ["bugSeverity"] = context.BugSeverity ?? string.Empty,
            ["skillContext"] = context.SkillContext ?? string.Empty,
            ["completionApiPath"] = context.CompletionApiPath,
            ["completionInstruction"] = context.CompletionInstruction
        };
    }

    private static string RenderPromptTemplate(
        string template,
        IReadOnlyDictionary<string, string> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string BuildWorkspacePath(string workspaceRoot, string projectCode)
    {
        var root = string.IsNullOrWhiteSpace(workspaceRoot) ? "/workspaces" : workspaceRoot.Trim();
        var code = string.IsNullOrWhiteSpace(projectCode) ? "_unscoped" : projectCode.Trim();
        return root.TrimEnd('/', '\\') + "/" + code.Trim('/', '\\');
    }

    private async Task<string> BuildSkillContextAsync(DigitalWorkerEntity worker, SprintRequirementEntity requirement)
    {
        var skillIds = DigitalWorkerManagementService.DeserializeIds(requirement.SkillIds)
            .Concat(DigitalWorkerManagementService.DeserializeIds(worker.SkillIds))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (skillIds.Count == 0)
        {
            return string.Empty;
        }

        var skills = await _skillDomain.ListAsync(entity =>
            skillIds.Contains(entity.Id) &&
            entity.Status == SprintSkillStatuses.Active);
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            skills.OrderBy(entity => entity.Code)
                .Select(entity => $"## {entity.Name} ({entity.Code}){Environment.NewLine}{entity.Content}"));
    }

    private async Task CompleteTaskRequirementIfReadyAsync(SprintDevelopmentTaskEntity task)
    {
        var tasks = await _taskDomain.ListAsync(entity => entity.RequirementId == task.RequirementId);
        if (tasks.Count == 0 || tasks.Any(entity => entity.Id != task.Id && entity.Status != SprintDevelopmentTaskStatuses.Completed))
        {
            return;
        }

        var requirement = await GetRequirementOrThrowAsync(task.RequirementId);
        requirement.Status = SprintRequirementStatuses.ReadyForTest;
        requirement.DevelopmentCompletedAt ??= DateTime.UtcNow;
        await _requirementDomain.UpdateAsync(requirement);
    }

    private async Task MarkRequirementReadyIfBugClearedAsync(SprintBugEntity bug)
    {
        var bugs = await _bugDomain.ListAsync(entity =>
            entity.RequirementId == bug.RequirementId &&
            entity.Status != SprintBugStatuses.Closed);
        if (bugs.Any(entity => entity.Id != bug.Id))
        {
            return;
        }

        var requirement = await GetRequirementOrThrowAsync(bug.RequirementId);
        requirement.Status = SprintRequirementStatuses.ReadyForTest;
        requirement.DevelopmentCompletedAt ??= DateTime.UtcNow;
        await _requirementDomain.UpdateAsync(requirement);
    }

    private async Task EnsureWorkerCanUseProjectAsync(DigitalWorkerEntity worker, string projectId)
    {
        var scopedProjectIds = DigitalWorkerManagementService.DeserializeIds(worker.ProjectIds);
        if (scopedProjectIds.Count > 0 && !scopedProjectIds.Contains(projectId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Worker is not allowed to access the target project.");
        }

        await GetProjectOrThrowAsync(projectId);
    }

    private static void EnsureWorkerCanCompleteAssignee(DigitalWorkerEntity worker, string? assigneeId)
    {
        if (!string.IsNullOrWhiteSpace(assigneeId) &&
            !string.Equals(assigneeId, worker.AgentUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Target is assigned to another user.");
        }
    }

    private static string NormalizeTargetType(string? targetType)
    {
        var normalized = DigitalWorkerManagementService.NormalizeRequired(targetType, "Target type is required.");
        return normalized switch
        {
            WorkerRunTypes.Task => WorkerRunTypes.Task,
            WorkerRunTypes.Bug => WorkerRunTypes.Bug,
            _ => throw new InvalidOperationException("Unsupported worker target type.")
        };
    }

    private async Task<SprintProjectEntity> GetProjectOrThrowAsync(string id)
    {
        return await _projectDomain.GetAsync(id)
            ?? throw new InvalidOperationException("Project does not exist.");
    }

    private async Task<WorkerGitRepositoryConfig?> ResolveGitRepositoryConfigAsync(SprintProjectEntity project)
    {
        var repositoryId = DigitalWorkerManagementService.NormalizeOptional(project.GitRepositoryId);
        if (repositoryId is null)
        {
            return null;
        }

        var repository = await _gitRepositoryDomain.GetAsync(repositoryId)
            ?? throw new InvalidOperationException("Git repository does not exist.");
        if (repository.Status != GitRepositoryStatuses.Active)
        {
            throw new InvalidOperationException("Git repository is not active.");
        }

        var accountId = DigitalWorkerManagementService.NormalizeOptional(project.GitAccountId)
            ?? DigitalWorkerManagementService.NormalizeOptional(repository.GitAccountId);
        string? username = null;
        string? accessToken = null;
        string? commitAuthorName = null;
        string? commitAuthorEmail = null;
        if (accountId is not null)
        {
            var account = await _gitAccountDomain.GetAsync(accountId)
                ?? throw new InvalidOperationException("Git account does not exist.");
            if (account.Status != GitAccountStatuses.Active)
            {
                throw new InvalidOperationException("Git account is not active.");
            }

            username = account.Username;
            accessToken = account.AccessToken;
            commitAuthorName = account.CommitAuthorName;
            commitAuthorEmail = account.CommitAuthorEmail;
        }

        return new WorkerGitRepositoryConfig(
            repository.Id,
            accountId,
            repository.RepositoryUrl,
            DigitalWorkerManagementService.NormalizeOptional(repository.DefaultBranch),
            username,
            accessToken,
            commitAuthorName,
            commitAuthorEmail);
    }

    private async Task<SprintRequirementEntity> GetRequirementOrThrowAsync(string id)
    {
        return await _requirementDomain.GetAsync(id)
            ?? throw new InvalidOperationException("Requirement does not exist.");
    }

    private async Task<SprintDevelopmentTaskEntity> GetTaskOrThrowAsync(string id)
    {
        return await _taskDomain.GetAsync(id)
            ?? throw new InvalidOperationException("Task does not exist.");
    }

    private async Task<SprintBugEntity> GetBugOrThrowAsync(string id)
    {
        return await _bugDomain.GetAsync(id)
            ?? throw new InvalidOperationException("Bug does not exist.");
    }

    private static string? SanitizeRepositoryReference(string? repositoryUrl)
    {
        var normalized = DigitalWorkerManagementService.NormalizeOptional(repositoryUrl);
        if (normalized is null)
        {
            return null;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
        {
            return normalized;
        }

        var builder = new UriBuilder(uri)
        {
            Password = string.Empty,
            UserName = string.Empty
        };
        return builder.Uri.ToString();
    }

    private async Task<AgentTokenEntity> ResolveAgentTokenAsync(string agentToken)
    {
        var normalizedToken = DigitalWorkerManagementService.NormalizeRequired(
            agentToken,
            "Agent Token is required.");
        if (normalizedToken.Length != AgentTokenLength)
        {
            throw new InvalidOperationException("Agent Token is invalid.");
        }

        var token = await _agentTokenDomain.FindByTokenHashAsync(HashAgentToken(normalizedToken));
        if (!IsUsableAgentToken(token))
        {
            throw new InvalidOperationException("Agent Token is invalid or expired.");
        }

        return token!;
    }

    private async Task<WorkerRuntimeConfigResult> BuildRuntimeConfigAsync(DigitalWorkerEntity worker, string? agentToken)
    {
        EnsureWorkerCanPullRuntimeConfig(worker);

        var aiPlatform = await ResolveAiPlatformAsync(worker);
        var projectId = DigitalWorkerManagementService.DeserializeIds(worker.ProjectIds).FirstOrDefault();
        return new WorkerRuntimeConfigResult(
            worker.Id,
            worker.Code,
            worker.Name,
            projectId,
            projectId,
            worker.WorkspaceRoot,
            worker.RunsRoot,
            worker.CodexHome,
            worker.PollIntervalSeconds,
            worker.IdleMaxIntervalSeconds,
            worker.MaxRunMinutes,
            worker.SandboxMode,
            worker.RunSmokeOnStartup,
            worker.SmokePrompt ?? "你好",
            aiPlatform.Provider,
            aiPlatform.Model,
            aiPlatform.OpenAiBaseUrl,
            aiPlatform.ApiKey,
            agentToken,
            worker.ConfigVersion);
    }

    private async Task<ResolvedAiPlatform> ResolveAiPlatformAsync(DigitalWorkerEntity worker)
    {
        var platformCode = string.IsNullOrWhiteSpace(worker.AiPlatformCode) ? "openai" : worker.AiPlatformCode;
        var platform = await _configurationService.GetAiPlatformRuntimeAsync(platformCode);
        if (platform is null)
        {
            return new ResolvedAiPlatform(
                worker.CodexProvider,
                worker.CodexModel,
                null,
                worker.OpenAiBaseUrl);
        }

        return new ResolvedAiPlatform(
            platform.Provider,
            platform.Model,
            platform.ApiKey,
            platform.OpenAiBaseUrl);
    }

    private static bool IsUsableAgentToken(AgentTokenEntity? token)
    {
        return token is not null &&
            token.Status == 1 &&
            token.RevokedAt is null &&
            token.ExpiresAt > DateTime.UtcNow;
    }

    private static string HashAgentToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private async Task<WorkerSessionEntity> GetSessionOrThrowAsync(string id)
    {
        var entity = await _sessionDomain.GetAsync(id);
        return entity ?? throw new InvalidOperationException("Worker session does not exist.");
    }

    private async Task<WorkerCommandEntity> GetCommandOrThrowAsync(string id)
    {
        var entity = await _commandDomain.GetAsync(id);
        return entity ?? throw new InvalidOperationException("Worker command does not exist.");
    }

    private async Task<WorkerRunEntity> GetRunOrThrowAsync(string id)
    {
        var entity = await _runDomain.GetAsync(id);
        return entity ?? throw new InvalidOperationException("Worker run does not exist.");
    }

    private async Task ExpireStaleSessionsAsync(DigitalWorkerEntity worker)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-worker.HeartbeatTimeoutSeconds);
        var sessions = await _sessionDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            entity.LastHeartbeatAt < cutoff &&
            entity.Status != WorkerSessionStatuses.Offline &&
            entity.Status != WorkerSessionStatuses.Expired);
        foreach (var session in sessions)
        {
            session.Status = WorkerSessionStatuses.Expired;
            session.StoppedAt ??= DateTime.UtcNow;
            await _sessionDomain.UpdateAsync(session);
            await FailUnfinishedSessionWorkAsync(
                worker,
                session,
                "Worker session heartbeat timed out.");
        }

        await RestoreWorkerStatusAfterRunAsync(worker);
    }

    private async Task FailUnfinishedSessionWorkAsync(
        DigitalWorkerEntity worker,
        WorkerSessionEntity session,
        string reason)
    {
        var now = DateTime.UtcNow;
        var runs = await _runDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            entity.SessionId == session.Id &&
            (entity.Status == WorkerRunStatuses.Pending || entity.Status == WorkerRunStatuses.Running));
        foreach (var run in runs)
        {
            run.Status = WorkerRunStatuses.Blocked;
            run.CompletedAt ??= now;
            run.Error = reason;
            await _runDomain.UpdateAsync(run);
        }

        var commands = await _commandDomain.ListAsync(entity =>
            entity.WorkerId == worker.Id &&
            entity.SessionId == session.Id &&
            (entity.Status == WorkerCommandStatuses.Acked || entity.Status == WorkerCommandStatuses.Running));
        foreach (var command in commands)
        {
            command.Status = WorkerCommandStatuses.Failed;
            command.CompletedAt ??= now;
            command.Error = reason;
            await _commandDomain.UpdateAsync(command);
        }

        if (runs.Count > 0 || commands.Count > 0)
        {
            await CreateEventAsync(
                worker.Id,
                session.Id,
                null,
                "worker_session_work_failed",
                WorkerEventLevels.Warn,
                reason,
                JsonSerializer.Serialize(new
                {
                    failedRuns = runs.Count,
                    failedCommands = commands.Count
                }));
        }
    }

    private async Task ExpireCommandsAsync(string workerId)
    {
        var now = DateTime.UtcNow;
        var commands = await _commandDomain.ListAsync(entity =>
            entity.WorkerId == workerId &&
            entity.Status == WorkerCommandStatuses.Pending &&
            entity.ExpiresAt != null &&
            entity.ExpiresAt < now);
        foreach (var command in commands)
        {
            command.Status = WorkerCommandStatuses.Expired;
            command.CompletedAt = now;
            await _commandDomain.UpdateAsync(command);
        }
    }

    private static void EnsureSessionBelongsToWorker(WorkerSessionEntity session, string workerId)
    {
        if (session.WorkerId != workerId)
        {
            throw new InvalidOperationException("Worker session does not belong to the worker.");
        }
    }

    private static void EnsureCommandCanUseSession(WorkerCommandEntity command, WorkerSessionEntity session)
    {
        if (command.WorkerId != session.WorkerId)
        {
            throw new InvalidOperationException("Worker command does not belong to the session worker.");
        }

        if (!string.IsNullOrWhiteSpace(command.SessionId) && command.SessionId != session.Id)
        {
            throw new InvalidOperationException("Worker command is assigned to another session.");
        }
    }

    private async Task<WorkerEventEntity> CreateEventAsync(
        string workerId,
        string? sessionId,
        string? runId,
        string eventType,
        string level,
        string message,
        string? payloadJson)
    {
        var entity = new WorkerEventEntity
        {
            WorkerId = workerId,
            SessionId = sessionId,
            RunId = runId,
            EventType = eventType,
            Level = level,
            Message = message,
            PayloadJson = payloadJson
        };
        await _eventDomain.CreateAsync(entity);
        return entity;
    }

    private static int ResolveNextIntervalSeconds(string status)
    {
        return status == WorkerSessionStatuses.Busy ? 10 : 15;
    }

    private static string ResolveRunEventLevel(string status)
    {
        return status == WorkerRunStatuses.Success ? WorkerEventLevels.Info : WorkerEventLevels.Error;
    }

    private sealed record WorkerGitRepositoryConfig(
        string RepositoryId,
        string? AccountId,
        string RepositoryUrl,
        string? DefaultBranch,
        string? Username,
        string? AccessToken,
        string? CommitAuthorName,
        string? CommitAuthorEmail);

    private sealed record ResolvedAiPlatform(
        string Provider,
        string Model,
        string? ApiKey,
        string? OpenAiBaseUrl);
}
