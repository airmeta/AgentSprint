using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Model.Modules.Agile.Workers;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AgileServices;

namespace AgentSprint.Service.Impls.AgileServices;

public sealed class CodeAuditService : AgentSprintServiceBase, ICodeAuditService
{
    private const string CodexAgentEnvironment = "codex";
    private const string CodeAuditPromptTemplateCode = "code_audit";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICodeAuditTaskDomain _taskDomain;
    private readonly ICodeAuditResultDomain _resultDomain;
    private readonly ICodeAuditFileDomain _fileDomain;
    private readonly ISprintProjectDomain _projectDomain;
    private readonly IGitAccountDomain _gitAccountDomain;
    private readonly IGitRepositoryDomain _repositoryDomain;
    private readonly ISprintSkillDomain _skillDomain;
    private readonly ISprintDevelopmentTaskDomain _developmentTaskDomain;
    private readonly ISprintRequirementDomain _requirementDomain;
    private readonly ISprintFeatureModuleDomain _moduleDomain;
    private readonly IDigitalWorkerDomain _workerDomain;
    private readonly IWorkerCommandDomain _commandDomain;
    private readonly IWorkerRunDomain _runDomain;
    private readonly IPromptTemplateDomain _promptTemplateDomain;

    public CodeAuditService(
        ICodeAuditTaskDomain taskDomain,
        ICodeAuditResultDomain resultDomain,
        ICodeAuditFileDomain fileDomain,
        ISprintProjectDomain projectDomain,
        IGitAccountDomain gitAccountDomain,
        IGitRepositoryDomain repositoryDomain,
        ISprintSkillDomain skillDomain,
        ISprintDevelopmentTaskDomain developmentTaskDomain,
        ISprintRequirementDomain requirementDomain,
        ISprintFeatureModuleDomain moduleDomain,
        IDigitalWorkerDomain workerDomain,
        IWorkerCommandDomain commandDomain,
        IWorkerRunDomain runDomain,
        IPromptTemplateDomain promptTemplateDomain)
    {
        _taskDomain = taskDomain;
        _resultDomain = resultDomain;
        _fileDomain = fileDomain;
        _projectDomain = projectDomain;
        _gitAccountDomain = gitAccountDomain;
        _repositoryDomain = repositoryDomain;
        _skillDomain = skillDomain;
        _developmentTaskDomain = developmentTaskDomain;
        _requirementDomain = requirementDomain;
        _moduleDomain = moduleDomain;
        _workerDomain = workerDomain;
        _commandDomain = commandDomain;
        _runDomain = runDomain;
        _promptTemplateDomain = promptTemplateDomain;
    }

    public async Task<CodeAuditTaskResult> CreateTaskAsync(CreateCodeAuditTaskRequest request, string userId)
    {
        var project = await _projectDomain.GetAsync(NormalizeRequired(request.ProjectId, "Project id is required."))
            ?? throw new InvalidOperationException("Project does not exist.");
        var repositoryId = NormalizeOptional(project.GitRepositoryId)
            ?? throw new InvalidOperationException("Project does not bind a Git repository.");
        var repository = await _repositoryDomain.GetAsync(repositoryId)
            ?? throw new InvalidOperationException("Git repository does not exist.");
        if (repository.Status != GitRepositoryStatuses.Active)
        {
            throw new InvalidOperationException("Git repository is not active.");
        }

        var worker = await _workerDomain.GetAsync(NormalizeRequired(request.WorkerId, "Worker id is required."))
            ?? throw new InvalidOperationException("Worker does not exist.");
        if (!DigitalWorkerManagementService.CanQueueWorkerCommand(worker))
        {
            throw new InvalidOperationException("Worker is not available for queued commands.");
        }

        EnsureWorkerCanUseProject(worker, project.Id);
        DigitalWorkerManagementService.EnsureBackendTechCovered(worker, project.BackendTechStack);

        var auditTargetType = NormalizeAuditTargetType(request.AuditTargetType);
        SprintDevelopmentTaskEntity? sourceTask = null;
        SprintRequirementEntity? requirement = null;
        SprintFeatureModuleEntity? module = null;
        WorkerRunEntity? sourceRun = null;
        WorkerCommandEntity? sourceCommand = null;
        if (auditTargetType == CodeAuditTargetTypes.DevelopmentTask)
        {
            var taskId = NormalizeRequired(request.TargetId, "Task id is required for development-task audit.");
            sourceTask = await _developmentTaskDomain.GetAsync(taskId)
                ?? throw new InvalidOperationException("Development task does not exist.");
            if (sourceTask.ProjectId != project.Id)
            {
                throw new InvalidOperationException("Development task does not belong to the selected project.");
            }

            requirement = await _requirementDomain.GetAsync(sourceTask.RequirementId);
            sourceRun = await ResolveLatestTaskRunAsync(sourceTask.Id);
            if (!string.IsNullOrWhiteSpace(sourceRun?.CommandId))
            {
                sourceCommand = await _commandDomain.GetAsync(sourceRun.CommandId);
            }
        }
        else if (auditTargetType == CodeAuditTargetTypes.Files ||
                 auditTargetType == CodeAuditTargetTypes.Folders ||
                 auditTargetType == CodeAuditTargetTypes.ReleasePreflight)
        {
            _ = NormalizeRequired(request.ScopeJson, "Scope JSON is required for scoped code audit.");
        }
        else if (auditTargetType == CodeAuditTargetTypes.RequirementModule)
        {
            var moduleId = NormalizeRequired(request.TargetId, "Module id is required for requirement-module audit.");
            module = await _moduleDomain.GetAsync(moduleId)
                ?? throw new InvalidOperationException("Feature module does not exist.");
            if (module.ProjectId != project.Id)
            {
                throw new InvalidOperationException("Feature module does not belong to the selected project.");
            }
        }
        else if (auditTargetType == CodeAuditTargetTypes.FeatureDescription)
        {
            _ = NormalizeRequired(request.Instruction, "Feature description is required for feature-description audit.");
        }

        var branch = NormalizeOptional(request.Branch)
            ?? NormalizeOptional(repository.DefaultBranch)
            ?? "main";
        var selectedSkillIds = SerializeIds(NormalizeIds(request.SelectedSkillIds));
        var entity = new CodeAuditTaskEntity
        {
            ProjectId = project.Id,
            GitRepositoryId = repository.Id,
            Branch = branch,
            WorkerId = worker.Id,
            AuditTargetType = auditTargetType,
            TargetId = NormalizeOptional(request.TargetId),
            SourceTaskId = sourceTask?.Id,
            SourceCommandId = sourceCommand?.Id,
            SourceRunId = sourceRun?.Id,
            SourceGitCommitId = sourceCommand?.GitCommitId,
            HeadCommitId = sourceCommand?.GitCommitId,
            RequirementId = sourceTask?.RequirementId ?? requirement?.Id,
            ModuleId = requirement?.ModuleId ?? module?.Id,
            ScopeJson = NormalizeOptional(request.ScopeJson),
            SelectedSkillIds = selectedSkillIds,
            Instruction = NormalizeOptional(request.Instruction),
            Status = CodeAuditTaskStatuses.Pending,
            CreatedBy = userId
        };

        await CreateTaskWithCommandAsync(entity, userId);

        return ToResult(entity);
    }

    public async Task<IReadOnlyList<CodeAuditTaskResult>> ListTasksAsync(
        string? projectId = null,
        string? status = null,
        string? auditTargetType = null,
        string? keyword = null)
    {
        var normalizedProjectId = NormalizeOptional(projectId);
        var normalizedStatus = NormalizeOptional(status);
        var normalizedType = NormalizeOptional(auditTargetType);
        var normalizedKeyword = NormalizeOptional(keyword);
        var entities = await _taskDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(normalizedProjectId) || entity.ProjectId == normalizedProjectId) &&
            (string.IsNullOrWhiteSpace(normalizedStatus) || entity.Status == normalizedStatus) &&
            (string.IsNullOrWhiteSpace(normalizedType) || entity.AuditTargetType == normalizedType));
        return entities
            .Where(entity =>
                string.IsNullOrWhiteSpace(normalizedKeyword) ||
                TextContains(normalizedKeyword, entity.Id, entity.TargetId, entity.Branch, entity.Conclusion))
            .OrderByDescending(entity => entity.UpdateTime ?? entity.CreateTime)
            .Select(ToResult)
            .ToList();
    }

    public async Task<IReadOnlyList<CodeAuditResultListItem>> ListResultsAsync(
        string? projectId = null,
        string? status = null,
        string? keyword = null)
    {
        var tasks = await ListTasksAsync(projectId, status, null, keyword);
        if (tasks.Count == 0)
        {
            return [];
        }

        var taskIds = tasks.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        var results = await _resultDomain.ListAsync(entity => taskIds.Contains(entity.AuditTaskId));
        var latestResults = results
            .GroupBy(entity => entity.AuditTaskId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => ToResult(group.OrderByDescending(entity => entity.CreateTime).First()),
                StringComparer.Ordinal);
        return tasks
            .OrderByDescending(task => task.CompletedAt ?? task.StartedAt ?? task.UpdateTime ?? task.CreateTime)
            .Select(task => new CodeAuditResultListItem(
                task,
                latestResults.GetValueOrDefault(task.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<CodeAuditFileResult>> ListFilesAsync(
        string? projectId = null,
        string? branch = null,
        string? auditStatus = null,
        string? fileType = null,
        string? keyword = null)
    {
        var normalizedProjectId = NormalizeOptional(projectId);
        var normalizedBranch = NormalizeOptional(branch);
        var normalizedStatus = NormalizeOptional(auditStatus);
        var normalizedFileType = NormalizeOptional(fileType);
        var normalizedKeyword = NormalizeOptional(keyword);
        var files = await _fileDomain.ListAsync(entity =>
            (string.IsNullOrWhiteSpace(normalizedProjectId) || entity.ProjectId == normalizedProjectId) &&
            (string.IsNullOrWhiteSpace(normalizedBranch) || entity.Branch == normalizedBranch) &&
            (string.IsNullOrWhiteSpace(normalizedStatus) || entity.AuditStatus == normalizedStatus) &&
            (string.IsNullOrWhiteSpace(normalizedFileType) || entity.FileType == normalizedFileType));
        return files
            .Where(entity =>
                string.IsNullOrWhiteSpace(normalizedKeyword) ||
                TextContains(normalizedKeyword, entity.FilePath, entity.FileType, entity.Summary))
            .OrderBy(entity => entity.FilePath, StringComparer.Ordinal)
            .Select(ToResult)
            .ToList();
    }

    public async Task<CodeAuditTaskDetailResult> GetTaskAsync(string id)
    {
        var task = await GetTaskOrThrowAsync(id);
        return new CodeAuditTaskDetailResult(ToResult(task), await ResolveResultAsync(task.Id));
    }

    public async Task<CodeAuditReleaseReportResult> GetReleaseReportAsync(string id)
    {
        var task = await GetTaskOrThrowAsync(id);
        var result = await ResolveResultEntityAsync(task.Id);
        var parsed = ParseAuditResult(
            result?.RawResult,
            result?.StructuredResultJson,
            result?.IssuesJson,
            result?.AnnotationIssuesJson,
            result?.ManualCheckItemsJson);
        var changedFiles = ResolveAuditFilePaths(result?.ChangedFilesJson, parsed.Issues, parsed.AnnotationIssues)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var allIssues = parsed.Issues.Concat(parsed.AnnotationIssues).ToList();
        var blocking = allIssues
            .Where(item => IsBlockingSeverity(item.Severity) || string.Equals(task.Status, CodeAuditTaskStatuses.Blocked, StringComparison.Ordinal))
            .Select(item => NormalizeOptional(item.Problem) ?? NormalizeOptional(item.Location) ?? "Blocking audit issue")
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToList();
        var high = allIssues.Count(item => string.Equals(item.Severity, "high", StringComparison.OrdinalIgnoreCase));
        var medium = allIssues.Count(item => string.Equals(item.Severity, "medium", StringComparison.OrdinalIgnoreCase));
        var low = allIssues.Count(item => string.Equals(item.Severity, "low", StringComparison.OrdinalIgnoreCase));
        var canRelease = task.Status == CodeAuditTaskStatuses.Passed &&
            !string.Equals(task.Conclusion, CodeAuditConclusions.Blocked, StringComparison.Ordinal) &&
            blocking.Count == 0 &&
            parsed.ManualCheckItems.Count == 0;
        return new CodeAuditReleaseReportResult(
            task.Id,
            task.ProjectId,
            task.GitRepositoryId,
            result?.Branch ?? task.Branch,
            task.Status,
            task.Conclusion,
            result?.GitCommitId ?? task.HeadCommitId ?? task.SourceGitCommitId,
            task.BaseCommitId,
            task.HeadCommitId,
            task.CurrentBranchHeadCommitId,
            changedFiles.Count,
            allIssues.Count,
            blocking.Count,
            high,
            medium,
            low,
            parsed.ManualCheckItems.Count,
            canRelease,
            blocking,
            parsed.ManualCheckItems,
            task.CompletedAt);
    }

    public async Task<CodeAuditTaskResult> CancelTaskAsync(string id, string userId)
    {
        var task = await GetTaskOrThrowAsync(id);
        if (task.Status != CodeAuditTaskStatuses.Pending)
        {
            throw new InvalidOperationException("Only pending code audit tasks can be cancelled.");
        }

        if (!string.IsNullOrWhiteSpace(task.AuditCommandId))
        {
            var command = await _commandDomain.GetAsync(task.AuditCommandId);
            if (command is not null)
            {
                if (command.Status != WorkerCommandStatuses.Pending)
                {
                    throw new InvalidOperationException("Code audit command has already been picked up and cannot be cancelled from the task queue.");
                }

                command.Status = WorkerCommandStatuses.Cancelled;
                command.CompletedAt = DateTime.UtcNow;
                command.Error = $"Code audit task was cancelled by {NormalizeOptional(userId) ?? "unknown"}.";
                await _commandDomain.UpdateAsync(command);
            }
        }

        task.Status = CodeAuditTaskStatuses.Cancelled;
        task.Conclusion = CodeAuditConclusions.Blocked;
        task.CompletedAt = DateTime.UtcNow;
        task.WorkspaceDirtyReason = "Code audit task was cancelled before execution.";
        await _taskDomain.UpdateAsync(task);
        return ToResult(task);
    }

    public async Task<CodeAuditTaskResult> RetryTaskAsync(string id, string userId)
    {
        var source = await GetTaskOrThrowAsync(id);
        if (source.Status is CodeAuditTaskStatuses.Pending or CodeAuditTaskStatuses.Running)
        {
            throw new InvalidOperationException("Pending or running code audit tasks cannot be retried.");
        }

        var entity = new CodeAuditTaskEntity
        {
            ProjectId = source.ProjectId,
            GitRepositoryId = source.GitRepositoryId,
            Branch = source.Branch,
            WorkerId = source.WorkerId,
            AuditTargetType = source.AuditTargetType,
            TargetId = source.TargetId,
            SourceTaskId = source.SourceTaskId,
            SourceCommandId = source.SourceCommandId,
            SourceRunId = source.SourceRunId,
            SourceGitCommitId = source.SourceGitCommitId,
            BaseCommitId = source.BaseCommitId,
            HeadCommitId = source.HeadCommitId,
            CurrentBranchHeadCommitId = source.CurrentBranchHeadCommitId,
            RequirementId = source.RequirementId,
            ModuleId = source.ModuleId,
            ScopeJson = source.ScopeJson,
            SelectedSkillIds = source.SelectedSkillIds,
            Instruction = source.Instruction,
            Status = CodeAuditTaskStatuses.Pending,
            CreatedBy = userId
        };
        await CreateTaskWithCommandAsync(entity, userId);
        return ToResult(entity);
    }

    public async Task<WorkerCommandResult> CreateIndexSyncCommandAsync(
        CreateCodeAuditIndexSyncCommandRequest request,
        string userId)
    {
        var project = await _projectDomain.GetAsync(NormalizeRequired(request.ProjectId, "Project id is required."))
            ?? throw new InvalidOperationException("Project does not exist.");
        var repositoryId = NormalizeOptional(project.GitRepositoryId)
            ?? throw new InvalidOperationException("Project does not bind a Git repository.");
        var repository = await _repositoryDomain.GetAsync(repositoryId)
            ?? throw new InvalidOperationException("Git repository does not exist.");
        var worker = await _workerDomain.GetAsync(NormalizeRequired(request.WorkerId, "Worker id is required."))
            ?? throw new InvalidOperationException("Worker does not exist.");
        if (!DigitalWorkerManagementService.CanQueueWorkerCommand(worker))
        {
            throw new InvalidOperationException("Worker is not available for queued commands.");
        }

        EnsureWorkerCanUseProject(worker, project.Id);
        DigitalWorkerManagementService.EnsureBackendTechCovered(worker, project.BackendTechStack);
        var gitAccount = await ResolveGitAccountAsync(project, repository);
        var branch = NormalizeOptional(request.Branch) ?? NormalizeOptional(repository.DefaultBranch) ?? "main";
        var payload = JsonSerializer.Serialize(
            new
            {
                projectId = project.Id,
                projectCode = project.Code,
                gitRepositoryId = repository.Id,
                repositoryUrl = repository.RepositoryUrl,
                branch,
                gitUsername = gitAccount?.Username,
                gitAccessToken = gitAccount?.AccessToken
            },
            JsonOptions);
        var command = new WorkerCommandEntity
        {
            WorkerId = worker.Id,
            CommandType = WorkerCommandTypes.CodeAuditIndexSync,
            Title = $"Code audit index sync {project.Code}/{branch}",
            PayloadJson = payload,
            CreatedBy = userId
        };
        await _commandDomain.CreateAsync(command);
        return DigitalWorkerManagementService.ToResult(command);
    }

    public async Task<CodeAuditExecutionContextResult> GetExecutionContextAsync(string id, string workerId)
    {
        var task = await GetTaskOrThrowAsync(id);
        return await BuildExecutionContextAsync(
            task,
            workerId,
            await ResolveChangedFilesJsonAsync(task),
            Diff: null,
            CodeContext: null,
            GitContextWarnings: null);
    }

    public async Task<CodeAuditExecutionContextResult> PrepareExecutionContextAsync(
        string id,
        string workerId,
        PrepareCodeAuditContextRequest request)
    {
        var task = await GetTaskOrThrowAsync(id);
        task.BaseCommitId = NormalizeOptional(request.BaseCommitId) ?? task.BaseCommitId;
        task.HeadCommitId = NormalizeOptional(request.HeadCommitId) ?? task.HeadCommitId;
        task.CurrentBranchHeadCommitId = NormalizeOptional(request.CurrentBranchHeadCommitId) ?? task.CurrentBranchHeadCommitId;
        task.Branch = NormalizeOptional(request.Branch) ?? task.Branch;
        if (!string.IsNullOrWhiteSpace(request.WorkerRunId))
        {
            task.SourceRunId ??= request.WorkerRunId;
        }

        await _taskDomain.UpdateAsync(task);

        var warnings = BuildGitContextWarnings(request);
        return await BuildExecutionContextAsync(
            task,
            workerId,
            NormalizeOptional(request.ChangedFilesJson) ?? await ResolveChangedFilesJsonAsync(task),
            NormalizeOptional(request.Diff),
            NormalizeOptional(request.CodeContext),
            warnings);
    }

    public async Task<CodeAuditFileIndexSyncResult> SyncFileIndexAsync(SyncCodeAuditFileIndexRequest request)
    {
        var project = await _projectDomain.GetAsync(NormalizeRequired(request.ProjectId, "Project id is required."))
            ?? throw new InvalidOperationException("Project does not exist.");
        var repository = await _repositoryDomain.GetAsync(NormalizeRequired(request.GitRepositoryId, "Git repository id is required."))
            ?? throw new InvalidOperationException("Git repository does not exist.");
        if (project.GitRepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Git repository does not belong to the selected project.");
        }

        var branch = NormalizeRequired(request.Branch, "Branch is required.");
        var commitId = NormalizeOptional(request.CommitId);
        var existingFiles = await _fileDomain.ListAsync(entity =>
            entity.ProjectId == project.Id &&
            entity.GitRepositoryId == repository.Id &&
            entity.Branch == branch);
        var existingByHash = existingFiles
            .GroupBy(entity => entity.FilePathHash, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(entity => entity.UpdateTime ?? entity.CreateTime).First(),
                StringComparer.Ordinal);
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);
        var created = 0;
        var updated = 0;

        foreach (var item in request.Files)
        {
            var normalizedPath = NormalizeFilePath(item.FilePath);
            if (normalizedPath is null)
            {
                continue;
            }

            var pathHash = HashText(normalizedPath);
            if (!seenHashes.Add(pathHash))
            {
                continue;
            }

            var contentHash = NormalizeOptional(item.FileContentHash);
            var fileType = NormalizeOptional(item.FileType) ?? ResolveFileType(normalizedPath);
            if (!existingByHash.TryGetValue(pathHash, out var file))
            {
                var createdFile = new CodeAuditFileEntity
                {
                    ProjectId = project.Id,
                    GitRepositoryId = repository.Id,
                    Branch = branch,
                    FilePath = normalizedPath,
                    FilePathHash = pathHash,
                    FileContentHash = contentHash,
                    FileType = fileType,
                    AuditStatus = CodeAuditFileStatuses.NotAudited,
                    LastCommitId = commitId,
                    Summary = "File discovered by repository index sync."
                };
                await _fileDomain.CreateAsync(createdFile);
                existingByHash[pathHash] = createdFile;
                created++;
                continue;
            }

            var contentChanged = !string.Equals(file.FileContentHash, contentHash, StringComparison.Ordinal);
            var metadataChanged =
                !string.Equals(file.FilePath, normalizedPath, StringComparison.Ordinal) ||
                !string.Equals(file.FileType, fileType, StringComparison.Ordinal) ||
                !string.Equals(file.LastCommitId, commitId, StringComparison.Ordinal) ||
                file.AuditStatus == CodeAuditFileStatuses.Deleted;
            if (!contentChanged && !metadataChanged)
            {
                continue;
            }

            var updateSucceeded = await UpdateCurrentAuditFileAsync(file, current =>
            {
                current.FilePath = normalizedPath;
                current.FileType = fileType;
                current.FileContentHash = contentHash;
                current.LastCommitId = commitId;
                if (contentChanged || file.AuditStatus == CodeAuditFileStatuses.Deleted)
                {
                    current.AuditStatus = CodeAuditFileStatuses.NotAudited;
                    current.Summary = contentChanged
                        ? "File content changed since last index sync."
                        : "File restored by repository index sync.";
                }
            });

            if (updateSucceeded)
            {
                updated++;
            }
        }

        var deleted = 0;
        foreach (var file in existingFiles.Where(file => !seenHashes.Contains(file.FilePathHash)))
        {
            if (file.AuditStatus == CodeAuditFileStatuses.Deleted)
            {
                continue;
            }

            var updateSucceeded = await UpdateCurrentAuditFileAsync(file, current =>
            {
                current.AuditStatus = CodeAuditFileStatuses.Deleted;
                current.LastCommitId = commitId;
                current.Summary = "File no longer exists in the latest repository index.";
            });
            if (updateSucceeded)
            {
                deleted++;
            }
        }

        return new CodeAuditFileIndexSyncResult(request.Files.Count, created, updated, deleted);
    }

    private async Task<CodeAuditExecutionContextResult> BuildExecutionContextAsync(
        CodeAuditTaskEntity task,
        string workerId,
        string? changedFilesJson,
        string? Diff,
        string? CodeContext,
        string? GitContextWarnings)
    {
        var worker = await _workerDomain.GetAsync(NormalizeRequired(workerId, "Worker id is required."))
            ?? throw new InvalidOperationException("Worker does not exist.");
        if (worker.Status is not (DigitalWorkerStatuses.Idle or DigitalWorkerStatuses.Working))
        {
            throw new InvalidOperationException("Worker is not available.");
        }

        if (task.WorkerId != worker.Id)
        {
            throw new InvalidOperationException("Code audit task is assigned to another worker.");
        }

        var project = await _projectDomain.GetAsync(task.ProjectId)
            ?? throw new InvalidOperationException("Project does not exist.");
        EnsureWorkerCanUseProject(worker, project.Id);

        var repository = await _repositoryDomain.GetAsync(task.GitRepositoryId)
            ?? throw new InvalidOperationException("Git repository does not exist.");
        if (repository.Status != GitRepositoryStatuses.Active)
        {
            throw new InvalidOperationException("Git repository is not active.");
        }

        var gitAccount = await ResolveGitAccountAsync(project, repository);
        var sourceTask = string.IsNullOrWhiteSpace(task.SourceTaskId)
            ? null
            : await _developmentTaskDomain.GetAsync(task.SourceTaskId);
        var requirement = string.IsNullOrWhiteSpace(task.RequirementId)
            ? null
            : await _requirementDomain.GetAsync(task.RequirementId);
        var template = await GetCodeAuditPromptTemplateAsync();
        var skillContext = await BuildSkillContextAsync(worker, task, requirement);
        var scopeDescription = BuildScopeDescription(task, sourceTask, requirement);
        var targetSummary = BuildTargetSummary(task, sourceTask, requirement);
        var codeContext = CombineContext(CodeContext, targetSummary);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auditTargetType"] = task.AuditTargetType,
            ["taskId"] = task.SourceTaskId ?? task.TargetId ?? task.Id,
            ["requirementId"] = task.RequirementId ?? string.Empty,
            ["moduleId"] = task.ModuleId ?? string.Empty,
            ["scope"] = scopeDescription ?? task.ScopeJson ?? string.Empty,
            ["repository"] = SanitizeRepositoryReference(repository.RepositoryUrl) ?? repository.Code,
            ["branch"] = task.Branch,
            ["changedFiles"] = changedFilesJson ?? string.Empty,
            ["diff"] = Diff ?? "Worker will run inside the prepared Git workspace. Use git diff/read-only inspection as needed; do not change files.",
            ["codeContext"] = codeContext ?? string.Empty,
            ["skillContext"] = skillContext,
            ["instruction"] = task.Instruction ?? string.Empty
        };
        var renderedPrompt = RenderPromptTemplate(template.Content, variables).Trim();
        var prompt = string.Join(
                Environment.NewLine + Environment.NewLine,
                BuildExecutionContextSection(worker, task, project, repository, sourceTask, requirement, changedFilesJson),
                renderedPrompt)
            .Trim();
        var promptSnapshot = JsonSerializer.Serialize(
            new
            {
                template.Id,
                template.Code,
                template.Name,
                variables,
                gitContextWarnings = GitContextWarnings,
                prompt
            },
            JsonOptions);

        return new CodeAuditExecutionContextResult(
            ToResult(task),
            project.Code,
            project.Name,
            repository.RepositoryUrl,
            SanitizeRepositoryReference(repository.RepositoryUrl),
            NormalizeOptional(repository.DefaultBranch),
            gitAccount?.Username,
            gitAccount?.AccessToken,
            gitAccount?.CommitAuthorName,
            gitAccount?.CommitAuthorEmail,
            template.Code,
            template.Name,
            prompt,
            promptSnapshot,
            skillContext,
            task.SourceGitCommitId,
            task.SourceRunId,
            task.SourceCommandId,
            changedFilesJson,
            Diff,
            codeContext,
            GitContextWarnings,
            scopeDescription,
            targetSummary);
    }

    public async Task<CodeAuditResultResult?> GetResultAsync(string taskId)
    {
        _ = await GetTaskOrThrowAsync(taskId);
        return await ResolveResultAsync(taskId);
    }

    public async Task<CodeAuditTaskResult> MarkTaskRunningAsync(string taskId, string? workerRunId = null)
    {
        var task = await GetTaskOrThrowAsync(taskId);
        task.Status = CodeAuditTaskStatuses.Running;
        task.StartedAt ??= DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(workerRunId))
        {
            task.SourceRunId ??= workerRunId;
        }

        await _taskDomain.UpdateAsync(task);
        return ToResult(task);
    }

    public async Task<CodeAuditTaskDetailResult> CompleteTaskAsync(string taskId, CompleteCodeAuditTaskRequest request)
    {
        var task = await GetTaskOrThrowAsync(taskId);
        task.Status = NormalizeTaskStatus(request.Status);
        task.Conclusion = NormalizeOptional(request.Conclusion) ?? ResolveConclusion(task.Status);
        task.CompletedAt = DateTime.UtcNow;
        task.WorkspaceDirtyReason = NormalizeOptional(request.WorkspaceDirtyReason);
        if (!string.IsNullOrWhiteSpace(request.WorkerRunId))
        {
            task.SourceRunId ??= request.WorkerRunId;
        }

        await _taskDomain.UpdateAsync(task);

        var existing = (await _resultDomain.ListAsync(entity => entity.AuditTaskId == task.Id))
            .OrderByDescending(entity => entity.CreateTime)
            .FirstOrDefault();
        var currentResult = existing is null ? null : await _resultDomain.GetAsync(existing.Id);
        var parsed = ParseAuditResult(
            request.RawResult,
            request.StructuredResultJson,
            request.IssuesJson,
            request.AnnotationIssuesJson,
            request.ManualCheckItemsJson);
        var result = currentResult ?? new CodeAuditResultEntity
        {
            AuditTaskId = task.Id
        };
        result.WorkerCommandId = task.AuditCommandId;
        result.WorkerRunId = NormalizeOptional(request.WorkerRunId) ?? task.SourceRunId;
        result.GitCommitId = NormalizeOptional(request.GitCommitId) ?? task.HeadCommitId;
        result.Branch = NormalizeOptional(request.Branch) ?? task.Branch;
        result.ChangedFilesJson = NormalizeOptional(request.ChangedFilesJson);
        result.PromptSnapshot = NormalizeOptional(request.PromptSnapshot);
        result.SkillContextSnapshot = NormalizeOptional(request.SkillContextSnapshot);
        result.RawResult = NormalizeOptional(request.RawResult);
        result.StructuredResultJson = NormalizeOptional(request.StructuredResultJson);
        result.Conclusion = task.Conclusion;
        result.IssuesJson = parsed.IssuesJson;
        result.AnnotationIssuesJson = parsed.AnnotationIssuesJson;
        result.ManualCheckItemsJson = parsed.ManualCheckItemsJson;
        if (currentResult is null)
        {
            await _resultDomain.CreateAsync(result);
        }
        else
        {
            await _resultDomain.UpdateAsync(result);
        }

        await UpsertAuditFilesAsync(task, result, parsed);
        return new CodeAuditTaskDetailResult(ToResult(task), ToResult(result));
    }

    private async Task<WorkerRunEntity?> ResolveLatestTaskRunAsync(string taskId)
    {
        var runs = await _runDomain.ListAsync(entity =>
            entity.TargetType == WorkerRunTypes.Task &&
            entity.TargetId == taskId);
        return runs
            .OrderByDescending(entity => entity.CompletedAt ?? entity.StartedAt)
            .FirstOrDefault();
    }

    private async Task<GitAccountEntity?> ResolveGitAccountAsync(
        SprintProjectEntity project,
        GitRepositoryEntity repository)
    {
        var accountId = NormalizeOptional(project.GitAccountId) ?? NormalizeOptional(repository.GitAccountId);
        if (accountId is null)
        {
            return null;
        }

        var account = await _gitAccountDomain.GetAsync(accountId)
            ?? throw new InvalidOperationException("Git account does not exist.");
        if (account.Status != GitAccountStatuses.Active)
        {
            throw new InvalidOperationException("Git account is not active.");
        }

        return account;
    }

    private async Task<PromptTemplateEntity> GetCodeAuditPromptTemplateAsync()
    {
        var templates = await _promptTemplateDomain.ListAsync(entity =>
            entity.AgentEnvironment == CodexAgentEnvironment &&
            entity.Code == CodeAuditPromptTemplateCode &&
            entity.Status == 1);
        return templates.OrderBy(entity => entity.Sort).FirstOrDefault()
            ?? throw new InvalidOperationException($"Codex prompt template '{CodeAuditPromptTemplateCode}' is not configured.");
    }

    private async Task<string> BuildSkillContextAsync(
        DigitalWorkerEntity worker,
        CodeAuditTaskEntity task,
        SprintRequirementEntity? requirement)
    {
        var skillIds = DeserializeIds(task.SelectedSkillIds)
            .Concat(DeserializeIds(requirement?.SkillIds))
            .Concat(DeserializeIds(worker.SkillIds))
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

    private async Task<string?> ResolveChangedFilesJsonAsync(CodeAuditTaskEntity task)
    {
        if (string.IsNullOrWhiteSpace(task.SourceCommandId))
        {
            return null;
        }

        var command = await _commandDomain.GetAsync(task.SourceCommandId);
        return NormalizeOptional(command?.ChangedFilesJson);
    }

    private static string BuildExecutionContextSection(
        DigitalWorkerEntity worker,
        CodeAuditTaskEntity task,
        SprintProjectEntity project,
        GitRepositoryEntity repository,
        SprintDevelopmentTaskEntity? sourceTask,
        SprintRequirementEntity? requirement,
        string? changedFilesJson)
    {
        return string.Join(
            Environment.NewLine,
            "AgentSprint code audit execution context:",
            $"- Worker ID: {worker.Id}",
            $"- Worker code: {worker.Code}",
            $"- Worker name: {worker.Name}",
            $"- Audit task ID: {task.Id}",
            $"- Audit command ID: {task.AuditCommandId ?? string.Empty}",
            $"- Audit target type: {task.AuditTargetType}",
            $"- Audit target ID: {task.TargetId ?? string.Empty}",
            $"- Project ID: {project.Id}",
            $"- Project code: {project.Code}",
            $"- Project name: {project.Name}",
            $"- Repository ID: {repository.Id}",
            $"- Repository reference: {SanitizeRepositoryReference(repository.RepositoryUrl) ?? repository.Code}",
            $"- Branch: {task.Branch}",
            $"- Source task: {FormatNamedReference(sourceTask?.Id, sourceTask?.Title)}",
            $"- Requirement: {FormatNamedReference(requirement?.Id, requirement?.Title)}",
            $"- Source command ID: {task.SourceCommandId ?? string.Empty}",
            $"- Source run ID: {task.SourceRunId ?? string.Empty}",
            $"- Source commit ID: {task.SourceGitCommitId ?? string.Empty}",
            $"- Base commit ID: {task.BaseCommitId ?? string.Empty}",
            $"- Head commit ID: {task.HeadCommitId ?? string.Empty}",
            $"- Current branch head commit ID: {task.CurrentBranchHeadCommitId ?? string.Empty}",
            $"- Changed files JSON: {changedFilesJson ?? string.Empty}",
            "- Read-only rule: do not edit files, do not create commits, do not push, and do not call AgentSprint completion APIs.");
    }

    private static string? BuildGitContextWarnings(PrepareCodeAuditContextRequest request)
    {
        var warnings = new List<string>();
        var explicitWarning = NormalizeOptional(request.Warning);
        if (!string.IsNullOrWhiteSpace(explicitWarning))
        {
            warnings.Add(explicitWarning);
        }

        if (request.SourceCommitReachable == false)
        {
            warnings.Add("Source commit is not reachable from the prepared branch head; verify whether the audited commit belongs to this branch.");
        }

        if (request.SourceCommitBehindHead == true)
        {
            warnings.Add("Source commit is behind current branch head; audit diff is anchored to the source commit while the workspace contains newer branch changes.");
        }

        return warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings.Distinct(StringComparer.Ordinal));
    }

    private static string? CombineContext(string? preparedContext, string? targetSummary)
    {
        var parts = new[] { NormalizeOptional(preparedContext), NormalizeOptional(targetSummary) }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        return parts.Length == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static string? BuildScopeDescription(
        CodeAuditTaskEntity task,
        SprintDevelopmentTaskEntity? sourceTask,
        SprintRequirementEntity? requirement)
    {
        return task.AuditTargetType switch
        {
            CodeAuditTargetTypes.DevelopmentTask => string.Join(
                " | ",
                new[]
                {
                    FormatNamedReference(sourceTask?.Id, sourceTask?.Title),
                    FormatNamedReference(requirement?.Id, requirement?.Title)
                }.Where(value => !string.IsNullOrWhiteSpace(value))),
            CodeAuditTargetTypes.Files or CodeAuditTargetTypes.Folders or CodeAuditTargetTypes.ReleasePreflight => NormalizeOptional(task.ScopeJson),
            CodeAuditTargetTypes.RequirementModule => FormatNamedReference(task.ModuleId, requirement?.Title),
            CodeAuditTargetTypes.FeatureDescription => NormalizeOptional(task.Instruction),
            _ => NormalizeOptional(task.ScopeJson)
        };
    }

    private static string? BuildTargetSummary(
        CodeAuditTaskEntity task,
        SprintDevelopmentTaskEntity? sourceTask,
        SprintRequirementEntity? requirement)
    {
        var lines = new List<string>();
        if (sourceTask is not null)
        {
            lines.Add($"Task: {sourceTask.Title}");
            if (!string.IsNullOrWhiteSpace(sourceTask.Description))
            {
                lines.Add($"Task description: {sourceTask.Description}");
            }

            if (!string.IsNullOrWhiteSpace(sourceTask.Prompt))
            {
                lines.Add($"Task prompt: {sourceTask.Prompt}");
            }
        }

        if (requirement is not null)
        {
            lines.Add($"Requirement: {requirement.Title}");
            if (!string.IsNullOrWhiteSpace(requirement.Description))
            {
                lines.Add($"Requirement description: {requirement.Description}");
            }
        }

        if (!string.IsNullOrWhiteSpace(task.ScopeJson))
        {
            lines.Add($"Scope JSON: {task.ScopeJson}");
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private async Task<CodeAuditTaskEntity> GetTaskOrThrowAsync(string id)
    {
        return await _taskDomain.GetAsync(NormalizeRequired(id, "Audit task id is required."))
            ?? throw new InvalidOperationException("Code audit task does not exist.");
    }

    private async Task<CodeAuditResultResult?> ResolveResultAsync(string taskId)
    {
        var result = await ResolveResultEntityAsync(taskId);
        return result is null ? null : ToResult(result);
    }

    private async Task<CodeAuditResultEntity?> ResolveResultEntityAsync(string taskId)
    {
        return (await _resultDomain.ListAsync(entity => entity.AuditTaskId == taskId))
            .OrderByDescending(entity => entity.CreateTime)
            .FirstOrDefault();
    }

    private async Task CreateTaskWithCommandAsync(CodeAuditTaskEntity entity, string userId)
    {
        var command = new WorkerCommandEntity
        {
            WorkerId = entity.WorkerId,
            CommandType = WorkerCommandTypes.CodeAudit,
            Title = $"Code audit {entity.Id}",
            PayloadJson = JsonSerializer.Serialize(new CodeAuditCommandPayload(entity.Id)),
            CreatedBy = userId
        };
        entity.AuditCommandId = command.Id;

        await _taskDomain.CreateAsync(entity);
        try
        {
            await _commandDomain.CreateAsync(command);
        }
        catch
        {
            await _taskDomain.DeleteAsync(entity.Id);
            throw;
        }
    }

    private async Task UpsertAuditFilesAsync(
        CodeAuditTaskEntity task,
        CodeAuditResultEntity result,
        ParsedAuditResult parsed)
    {
        var filePaths = ResolveAuditFilePaths(result.ChangedFilesJson, parsed.Issues, parsed.AnnotationIssues).ToList();
        if (filePaths.Count == 0)
        {
            return;
        }

        var existingFiles = await _fileDomain.ListAsync(entity =>
            entity.ProjectId == task.ProjectId &&
            entity.GitRepositoryId == task.GitRepositoryId &&
            entity.Branch == (result.Branch ?? task.Branch));
        var existingByHash = existingFiles
            .GroupBy(entity => entity.FilePathHash, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(entity => entity.UpdateTime ?? entity.CreateTime).First(),
                StringComparer.Ordinal);
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var filePath in filePaths)
        {
            var normalizedPath = NormalizeFilePath(filePath);
            if (normalizedPath is null)
            {
                continue;
            }

            var pathHash = HashText(normalizedPath);
            if (!seenHashes.Add(pathHash))
            {
                continue;
            }

            existingByHash.TryGetValue(pathHash, out var file);
            var stats = CountIssuesForFile(normalizedPath, parsed.Issues, parsed.AnnotationIssues);
            if (file is null)
            {
                file = new CodeAuditFileEntity
                {
                    ProjectId = task.ProjectId,
                    GitRepositoryId = task.GitRepositoryId,
                    Branch = result.Branch ?? task.Branch,
                    FilePath = normalizedPath,
                    FilePathHash = pathHash,
                    FileType = ResolveFileType(normalizedPath)
                };
                ApplyAuditFileResult(file, task, result, stats);
                await _fileDomain.CreateAsync(file);
                existingByHash[pathHash] = file;
            }
            else
            {
                await UpdateCurrentAuditFileAsync(file, current =>
                {
                    current.FilePath = normalizedPath;
                    current.FileType = ResolveFileType(normalizedPath);
                    ApplyAuditFileResult(current, task, result, stats);
                });
            }
        }
    }

    private async Task<bool> UpdateCurrentAuditFileAsync(
        CodeAuditFileEntity snapshot,
        Action<CodeAuditFileEntity> applyChanges)
    {
        var current = await _fileDomain.GetAsync(snapshot.Id);
        if (current is null)
        {
            return false;
        }

        applyChanges(current);
        await _fileDomain.UpdateAsync(current);
        return true;
    }

    private static void ApplyAuditFileResult(
        CodeAuditFileEntity file,
        CodeAuditTaskEntity task,
        CodeAuditResultEntity result,
        AuditFileIssueStats stats)
    {
        file.LastAuditTaskId = task.Id;
        file.LastAuditResultId = result.Id;
        file.LastAuditAt = DateTime.UtcNow;
        file.LastCommitId = result.GitCommitId ?? task.HeadCommitId ?? task.SourceGitCommitId;
        file.FileContentHash = null;
        file.IssueCount = stats.Total;
        file.BlockingIssueCount = stats.Blocking;
        file.HighIssueCount = stats.High;
        file.MediumIssueCount = stats.Medium;
        file.LowIssueCount = stats.Low;
        file.AuditStatus = stats.Total > 0 || task.Status is CodeAuditTaskStatuses.NeedsChanges or CodeAuditTaskStatuses.Blocked
            ? CodeAuditFileStatuses.Abnormal
            : CodeAuditFileStatuses.Normal;
        file.Summary = stats.Total > 0
            ? $"发现 {stats.Total} 个审计问题"
            : "本次审计未发现明确问题";
    }

    private static ParsedAuditResult ParseAuditResult(
        string? rawResult,
        string? structuredResultJson,
        string? issuesJson,
        string? annotationIssuesJson,
        string? manualCheckItemsJson)
    {
        var issues = ParseIssueList(issuesJson, "issues")
            .Concat(ParseIssueList(structuredResultJson, "issues"))
            .Concat(ParseMarkdownIssues(rawResult))
            .DistinctBy(item => $"{item.Location}|{item.Problem}|{item.Severity}")
            .ToList();
        var annotationIssues = ParseIssueList(annotationIssuesJson, "annotationIssues")
            .Concat(ParseIssueList(structuredResultJson, "annotationIssues"))
            .Concat(ParseMarkdownAnnotationIssues(rawResult))
            .DistinctBy(item => $"{item.Location}|{item.Problem}|{item.Severity}")
            .ToList();
        var manualCheckItems = ParseStringList(manualCheckItemsJson, "manualCheckItems")
            .Concat(ParseStringList(structuredResultJson, "manualCheckItems"))
            .Concat(ParseMarkdownManualCheckItems(rawResult))
            .Select(NormalizeOptional)
            .Where(item => item is not null)
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new ParsedAuditResult(
            issues,
            annotationIssues,
            manualCheckItems,
            JsonSerializer.Serialize(issues, JsonOptions),
            JsonSerializer.Serialize(annotationIssues, JsonOptions),
            JsonSerializer.Serialize(manualCheckItems, JsonOptions));
    }

    private static IReadOnlyList<AuditIssueItem> ParseIssueList(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var element = document.RootElement;
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var property))
            {
                element = property;
            }

            if (element.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return element.EnumerateArray()
                .Select(ParseIssueElement)
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static AuditIssueItem? ParseIssueElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = NormalizeOptional(element.GetString());
            return value is null ? null : new AuditIssueItem(null, value, null, null);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new AuditIssueItem(
            NormalizeOptional(ReadString(element, "severity", "level")),
            NormalizeOptional(ReadString(element, "location", "path", "file")),
            NormalizeOptional(ReadString(element, "problem", "message", "title", "description")),
            NormalizeOptional(ReadString(element, "direction", "fix", "recommendation")));
    }

    private static IReadOnlyList<string> ParseStringList(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var element = document.RootElement;
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var property))
            {
                element = property;
            }

            if (element.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Select(NormalizeOptional)
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<AuditIssueItem> ParseMarkdownIssues(string? rawResult)
    {
        return ParseMarkdownSection(rawResult, "## 问题列表")
            .Select(ParseMarkdownIssueBlock)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
    }

    private static IReadOnlyList<AuditIssueItem> ParseMarkdownAnnotationIssues(string? rawResult)
    {
        return ParseMarkdownSection(rawResult, "## 注释检查")
            .Select(line => new AuditIssueItem("annotation", ExtractInlineValue(line, "位置") ?? ExtractFilePath(line), StripBullet(line), null))
            .Where(item => !string.IsNullOrWhiteSpace(item.Problem) &&
                !item.Problem.Contains("未发现明确问题", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IReadOnlyList<string> ParseMarkdownManualCheckItems(string? rawResult)
    {
        return ParseMarkdownSection(rawResult, "## 人工确认项")
            .Select(StripBullet)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static IEnumerable<string> ParseMarkdownSection(string? rawResult, string heading)
    {
        if (string.IsNullOrWhiteSpace(rawResult))
        {
            yield break;
        }

        var inSection = false;
        foreach (var rawLine in rawResult.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = string.Equals(line, heading, StringComparison.Ordinal);
                continue;
            }

            if (inSection)
            {
                yield return line;
            }
        }
    }

    private static AuditIssueItem? ParseMarkdownIssueBlock(string line)
    {
        var text = StripBullet(line);
        if (string.IsNullOrWhiteSpace(text) || text.Contains("未发现", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new AuditIssueItem(
            ExtractInlineValue(text, "严重程度"),
            ExtractInlineValue(text, "位置") ?? ExtractFilePath(text),
            ExtractInlineValue(text, "问题") ?? text,
            ExtractInlineValue(text, "修复方向"));
    }

    private static IEnumerable<string> ResolveAuditFilePaths(
        string? changedFilesJson,
        IReadOnlyList<AuditIssueItem> issues,
        IReadOnlyList<AuditIssueItem> annotationIssues)
    {
        foreach (var path in ParseChangedFilePaths(changedFilesJson))
        {
            yield return path;
        }

        foreach (var issue in issues.Concat(annotationIssues))
        {
            var path = ExtractFilePath(issue.Location) ?? ExtractFilePath(issue.Problem);
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> ParseChangedFilePaths(string? changedFilesJson)
    {
        if (string.IsNullOrWhiteSpace(changedFilesJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(changedFilesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return document.RootElement.EnumerateArray()
                .Select(element => element.ValueKind switch
                {
                    JsonValueKind.String => NormalizeFilePath(element.GetString()),
                    JsonValueKind.Object => NormalizeFilePath(ReadString(element, "path", "filePath", "file")),
                    _ => null
                })
                .Where(path => path is not null)
                .Select(path => path!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static AuditFileIssueStats CountIssuesForFile(
        string normalizedPath,
        IReadOnlyList<AuditIssueItem> issues,
        IReadOnlyList<AuditIssueItem> annotationIssues)
    {
        var related = issues.Concat(annotationIssues)
            .Where(issue =>
                TextContains(normalizedPath, issue.Location, issue.Problem) ||
                string.Equals(NormalizeFilePath(ExtractFilePath(issue.Location)), normalizedPath, StringComparison.Ordinal))
            .ToList();
        return new AuditFileIssueStats(
            related.Count,
            related.Count(item => IsSeverity(item.Severity, "阻断", "blocking", "blocker", "blocked")),
            related.Count(item => IsSeverity(item.Severity, "高", "high", "critical", "major")),
            related.Count(item => IsSeverity(item.Severity, "中", "medium", "middle")),
            related.Count(item => IsSeverity(item.Severity, "低", "low", "minor", "trivial", "annotation")));
    }

    private static bool IsSeverity(string? value, params string[] candidates)
    {
        return value is not null && candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBlockingSeverity(string? value)
    {
        return IsSeverity(value, "阻断", "blocking", "blocker", "blocked", "critical");
    }

    private static string? ExtractInlineValue(string? text, string key)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var marker = key + "：";
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            marker = key + ":";
            start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        }

        if (start < 0)
        {
            return null;
        }

        var value = text[(start + marker.Length)..].Trim();
        var next = value.IndexOf(" - ", StringComparison.Ordinal);
        return NormalizeOptional(next >= 0 ? value[..next] : value);
    }

    private static string StripBullet(string line)
    {
        return line.Trim().TrimStart('-', '*').Trim();
    }

    private static string? ExtractFilePath(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var candidates = text.Split([' ', '\t', '，', ',', '；', ';', '：', ':', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries);
        return candidates.Select(NormalizeFilePath).FirstOrDefault(path => path is not null);
    }

    private static string? NormalizeFilePath(string? path)
    {
        path = NormalizeOptional(path)?.Trim('`', '"', '\'');
        if (path is null)
        {
            return null;
        }

        path = path.Replace('\\', '/');
        while (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        if (path.StartsWith("../", StringComparison.Ordinal) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains("://", StringComparison.Ordinal) ||
            !path.Contains('/', StringComparison.Ordinal) && !path.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        return path;
    }

    private static string ResolveFileType(string path)
    {
        var extension = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(extension) ? "unknown" : extension.TrimStart('.').ToLowerInvariant();
    }

    private static string HashText(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string? ReadString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
            }
        }

        return null;
    }

    private static CodeAuditTaskResult ToResult(CodeAuditTaskEntity entity)
    {
        return new CodeAuditTaskResult(
            entity.Id,
            entity.ProjectId,
            entity.GitRepositoryId,
            entity.Branch,
            entity.WorkerId,
            entity.AuditTargetType,
            entity.TargetId,
            entity.SourceTaskId,
            entity.SourceCommandId,
            entity.AuditCommandId,
            entity.SourceRunId,
            entity.SourceGitCommitId,
            entity.BaseCommitId,
            entity.HeadCommitId,
            entity.CurrentBranchHeadCommitId,
            entity.RequirementId,
            entity.ModuleId,
            entity.ScopeJson,
            DeserializeIds(entity.SelectedSkillIds),
            entity.Instruction,
            entity.Status,
            entity.Conclusion,
            entity.WorkspaceDirtyReason,
            entity.CreatedBy,
            entity.StartedAt,
            entity.CompletedAt,
            entity.CreateTime,
            entity.UpdateTime);
    }

    private static CodeAuditResultResult ToResult(CodeAuditResultEntity entity)
    {
        return new CodeAuditResultResult(
            entity.Id,
            entity.AuditTaskId,
            entity.WorkerCommandId,
            entity.WorkerRunId,
            entity.GitCommitId,
            entity.Branch,
            entity.ChangedFilesJson,
            entity.PromptSnapshot,
            entity.SkillContextSnapshot,
            entity.RawResult,
            entity.StructuredResultJson,
            entity.Conclusion,
            entity.IssuesJson,
            entity.AnnotationIssuesJson,
            entity.ManualCheckItemsJson,
            entity.CreateTime,
            entity.UpdateTime);
    }

    private static CodeAuditFileResult ToResult(CodeAuditFileEntity entity)
    {
        return new CodeAuditFileResult(
            entity.Id,
            entity.ProjectId,
            entity.GitRepositoryId,
            entity.Branch,
            entity.FileType,
            entity.FilePath,
            entity.FileContentHash,
            entity.AuditStatus,
            entity.LastAuditTaskId,
            entity.LastAuditResultId,
            entity.LastAuditAt,
            entity.LastCommitId,
            entity.IssueCount,
            entity.BlockingIssueCount,
            entity.HighIssueCount,
            entity.MediumIssueCount,
            entity.LowIssueCount,
            entity.Summary,
            entity.CreateTime,
            entity.UpdateTime);
    }

    private static string NormalizeAuditTargetType(string? value)
    {
        var normalized = NormalizeRequired(value, "Audit target type is required.");
        return normalized switch
        {
            CodeAuditTargetTypes.DevelopmentTask or
            CodeAuditTargetTypes.Files or
            CodeAuditTargetTypes.Folders or
            CodeAuditTargetTypes.RequirementModule or
            CodeAuditTargetTypes.FeatureDescription or
            CodeAuditTargetTypes.ReleasePreflight => normalized,
            _ => throw new InvalidOperationException("Unsupported audit target type.")
        };
    }

    private static string NormalizeTaskStatus(string? value)
    {
        var normalized = NormalizeRequired(value, "Audit task status is required.");
        return normalized switch
        {
            CodeAuditTaskStatuses.Pending or
            CodeAuditTaskStatuses.Running or
            CodeAuditTaskStatuses.Passed or
            CodeAuditTaskStatuses.NeedsChanges or
            CodeAuditTaskStatuses.Blocked or
            CodeAuditTaskStatuses.Failed or
            CodeAuditTaskStatuses.Cancelled => normalized,
            _ => throw new InvalidOperationException("Unsupported audit task status.")
        };
    }

    private static string? ResolveConclusion(string status)
    {
        return status switch
        {
            CodeAuditTaskStatuses.Passed => CodeAuditConclusions.Passed,
            CodeAuditTaskStatuses.NeedsChanges => CodeAuditConclusions.NeedsChanges,
            CodeAuditTaskStatuses.Blocked => CodeAuditConclusions.Blocked,
            _ => null
        };
    }

    private static void EnsureWorkerCanUseProject(DigitalWorkerEntity worker, string projectId)
    {
        var scopedProjectIds = DeserializeIds(worker.ProjectIds);
        if (scopedProjectIds.Count > 0 && !scopedProjectIds.Contains(projectId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Worker is not allowed to access the target project.");
        }
    }

    private static string NormalizeRequired(string? value, string message)
    {
        var normalized = NormalizeOptional(value);
        return normalized ?? throw new InvalidOperationException(message);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<string> NormalizeIds(IReadOnlyList<string>? values)
    {
        return values?
            .Select(NormalizeOptional)
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
    }

    private static string? SerializeIds(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? null : string.Join(',', values);
    }

    private static IReadOnlyList<string> DeserializeIds(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
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

    private static string? SanitizeRepositoryReference(string? repositoryUrl)
    {
        var normalized = NormalizeOptional(repositoryUrl);
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

    private static string? FormatNamedReference(string? id, string? name)
    {
        id = NormalizeOptional(id);
        name = NormalizeOptional(name);
        return (id, name) switch
        {
            (null, null) => null,
            (not null, null) => id,
            (null, not null) => name,
            _ => $"{id} - {name}"
        };
    }

    private static bool TextContains(string keyword, params string?[] values)
    {
        return values.Any(value => value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true);
    }

    private sealed record CodeAuditCommandPayload(string AuditTaskId);

    private sealed record ParsedAuditResult(
        IReadOnlyList<AuditIssueItem> Issues,
        IReadOnlyList<AuditIssueItem> AnnotationIssues,
        IReadOnlyList<string> ManualCheckItems,
        string IssuesJson,
        string AnnotationIssuesJson,
        string ManualCheckItemsJson);

    private sealed record AuditIssueItem(
        string? Severity,
        string? Location,
        string? Problem,
        string? Direction);

    private sealed record AuditFileIssueStats(
        int Total,
        int Blocking,
        int High,
        int Medium,
        int Low);
}
