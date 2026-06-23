using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Agile.Dtos;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.AgileServices;
using AgentSprint.Service.Services.SecurityServices;

namespace AgentSprint.Service.Impls.AgileServices;

public sealed class RequirementDecompositionPreviewService : AgentSprintServiceBase, IRequirementDecompositionPreviewService
{
    private const string CodexAgentEnvironment = "codex";
    private const string RequirementDecompositionPromptTemplateCode = "requirement_decomposition";
    private const int TextColumnLimit = 65535;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly ISystemConfigurationService _configurationService;
    private readonly IRequirementDecompositionService _fallbackService;
    private readonly ISprintProjectDomain _projectDomain;
    private readonly IPromptTemplateDomain _promptTemplateDomain;
    private readonly ISprintRequirementDomain _requirementDomain;
    private readonly ISprintRequirementDecompositionPreviewDomain _previewDomain;
    private readonly HttpClient _httpClient;

    public RequirementDecompositionPreviewService(
        ISystemConfigurationService configurationService,
        IRequirementDecompositionService fallbackService,
        ISprintProjectDomain projectDomain,
        IPromptTemplateDomain promptTemplateDomain,
        ISprintRequirementDomain requirementDomain,
        ISprintRequirementDecompositionPreviewDomain previewDomain,
        HttpClient httpClient)
    {
        _configurationService = configurationService;
        _fallbackService = fallbackService;
        _projectDomain = projectDomain;
        _promptTemplateDomain = promptTemplateDomain;
        _requirementDomain = requirementDomain;
        _previewDomain = previewDomain;
        _httpClient = httpClient;
    }

    public async Task<SprintRequirementDecompositionPreviewResult> PreviewAsync(
        string requirementId,
        string? instruction,
        int? taskCount,
        string? aiPlatformCode,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var requirement = await _requirementDomain.GetAsync(requirementId)
            ?? throw new InvalidOperationException("Requirement does not exist.");
        var project = await _projectDomain.GetAsync(requirement.ProjectId)
            ?? throw new InvalidOperationException("Project does not exist.");
        var configuredAiPlatformCode = NormalizeRequired(
            project.AiPlatformCode,
            "Project AI platform is required.");
        var previousStatus = requirement.Status;

        await SetRequirementStatusAsync(requirement, SprintRequirementStatuses.AiDecomposing);

        try
        {
            var rawContent = await GenerateDraftJsonAsync(requirement, instruction, taskCount, configuredAiPlatformCode, cancellationToken);
            var tasks = ParseDrafts(rawContent, requirement.Priority);
            if (tasks.Count == 0)
            {
                throw new InvalidOperationException("AI decomposition returned no tasks.");
            }

            return await SavePreviewAsync(requirement, instruction, configuredAiPlatformCode, userId, "ai", tasks, rawContent, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            try
            {
                var fallback = await _fallbackService.PreviewAsync(requirement, instruction, taskCount);
                return await SavePreviewAsync(requirement, instruction, configuredAiPlatformCode, userId, "local", fallback, null, ex.Message);
            }
            catch
            {
                await SetRequirementStatusAsync(requirement, previousStatus);
                throw;
            }
        }
    }

    public async Task<IReadOnlyList<SprintRequirementDecompositionPreviewResult>> ListAsync(string requirementId)
    {
        var normalizedRequirementId = NormalizeOptional(requirementId)
            ?? throw new InvalidOperationException("Requirement id is required.");

        return (await _previewDomain.ListAsync(entity => entity.RequirementId == normalizedRequirementId))
            .OrderByDescending(entity => entity.CreateTime)
            .Select(MapPreview)
            .ToList();
    }

    public async Task<SprintRequirementDecompositionPreviewResult> SaveDraftAsync(
        string requirementId,
        SaveSprintRequirementDecompositionPreviewRequest request,
        string userId)
    {
        var requirement = await _requirementDomain.GetAsync(requirementId)
            ?? throw new InvalidOperationException("Requirement does not exist.");
        var existingTasks = Array.Empty<SprintDevelopmentTaskDraft>();
        var requirementPriority = Math.Clamp(requirement.Priority, 1, 9);
        SprintRequirementDecompositionPreviewEntity entity;

        if (!string.IsNullOrWhiteSpace(request.PreviewId))
        {
            entity = await _previewDomain.GetAsync(request.PreviewId.Trim())
                ?? throw new InvalidOperationException("Decomposition preview does not exist.");
            if (entity.RequirementId != requirement.Id)
            {
                throw new InvalidOperationException("Decomposition preview does not belong to this requirement.");
            }

            if (entity.Status == SprintRequirementDecompositionPreviewStatuses.Confirmed)
            {
                throw new InvalidOperationException("Decomposition preview is already confirmed.");
            }

            existingTasks = ReadTaskDrafts(entity.TaskJson).ToArray();
            var tasks = NormalizeDrafts(request.Tasks, requirementPriority, existingTasks);
            entity.TaskJson = Truncate(JsonSerializer.Serialize(tasks, JsonOptions), TextColumnLimit);
            entity.Instruction = TruncateOptional(NormalizeOptional(request.Instruction), TextColumnLimit);
            entity.ErrorMessage = null;
            entity.UpdateTime = DateTime.UtcNow;
            await _previewDomain.UpdateAsync(entity);
        }
        else
        {
            var tasks = NormalizeDrafts(request.Tasks, requirementPriority, existingTasks);
            entity = new SprintRequirementDecompositionPreviewEntity
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                Source = "local",
                Status = SprintRequirementDecompositionPreviewStatuses.Draft,
                TaskJson = Truncate(JsonSerializer.Serialize(tasks, JsonOptions), TextColumnLimit),
                Instruction = TruncateOptional(NormalizeOptional(request.Instruction), TextColumnLimit),
                CreatedBy = userId
            };
            await _previewDomain.CreateAsync(entity);
        }

        await SetRequirementStatusAsync(requirement, SprintRequirementStatuses.AiDecomposed);
        return MapPreview(entity);
    }

    private async Task<SprintRequirementDecompositionPreviewResult> SavePreviewAsync(
        SprintRequirementEntity requirement,
        string? instruction,
        string? aiPlatformCode,
        string userId,
        string source,
        IReadOnlyList<SprintDevelopmentTaskDraft> tasks,
        string? rawContent,
        string? errorMessage)
    {
        var entity = new SprintRequirementDecompositionPreviewEntity
        {
            ProjectId = requirement.ProjectId,
            RequirementId = requirement.Id,
            Source = source,
            Status = SprintRequirementDecompositionPreviewStatuses.Draft,
            TaskJson = Truncate(JsonSerializer.Serialize(tasks, JsonOptions), TextColumnLimit),
            RawContent = TruncateOptional(rawContent, TextColumnLimit),
            Instruction = TruncateOptional(NormalizeOptional(instruction), TextColumnLimit),
            AiPlatformCode = NormalizeOptional(aiPlatformCode),
            ErrorMessage = TruncateOptional(errorMessage, TextColumnLimit),
            CreatedBy = userId
        };

        await _previewDomain.CreateAsync(entity);
        await SetRequirementStatusAsync(requirement, SprintRequirementStatuses.AiDecomposed);
        return MapPreview(entity);
    }

    private async Task SetRequirementStatusAsync(SprintRequirementEntity requirement, string status)
    {
        requirement.Status = status;
        requirement.UpdateTime = DateTime.UtcNow;
        await _requirementDomain.UpdateAsync(requirement);
    }

    private async Task<string> GenerateDraftJsonAsync(
        SprintRequirementEntity requirement,
        string? instruction,
        int? taskCount,
        string? aiPlatformCode,
        CancellationToken cancellationToken)
    {
        var platformCode = NormalizeRequired(aiPlatformCode, "Project AI platform is required.");
        var platform = await _configurationService.GetAiPlatformRuntimeAsync(platformCode)
            ?? throw new InvalidOperationException("AI platform is not available.");

        if (!string.Equals(platform.Provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only OpenAI-compatible chat platforms are supported.");
        }

        if (string.IsNullOrWhiteSpace(platform.ApiKey))
        {
            throw new InvalidOperationException("AI platform API key is not configured.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(platform.OpenAiBaseUrl)
            ? "https://api.openai.com/v1"
            : platform.OpenAiBaseUrl.TrimEnd('/');
        var prompt = await BuildDecompositionPromptAsync(requirement, instruction, taskCount);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", platform.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(
                new ChatCompletionRequest(
                    platform.Model,
                    [
                        new ChatMessage("system", BuildJsonOutputSystemPrompt()),
                        new ChatMessage("user", prompt)
                    ],
                    ResponseFormat: new ChatResponseFormat("json_object")),
                JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AI decomposition request failed: {(int)response.StatusCode} {body}");
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
        return completion?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
            ?? throw new InvalidOperationException("AI decomposition response is empty.");
    }

    private async Task<string> BuildDecompositionPromptAsync(
        SprintRequirementEntity requirement,
        string? instruction,
        int? taskCount)
    {
        var template = await GetDecompositionPromptTemplateAsync();
        return RenderPromptTemplate(
            template.Content,
            BuildPromptVariables(requirement, instruction, taskCount)).Trim();
    }

    private async Task<PromptTemplateEntity> GetDecompositionPromptTemplateAsync()
    {
        var templates = await _promptTemplateDomain.ListAsync(entity =>
            entity.AgentEnvironment == CodexAgentEnvironment &&
            entity.Code == RequirementDecompositionPromptTemplateCode &&
            entity.Status == 1);
        return templates.OrderBy(entity => entity.Sort).FirstOrDefault()
            ?? throw new InvalidOperationException($"Codex prompt template '{RequirementDecompositionPromptTemplateCode}' is not configured.");
    }

    private static IReadOnlyDictionary<string, string> BuildPromptVariables(
        SprintRequirementEntity requirement,
        string? instruction,
        int? taskCount)
    {
        var countText = taskCount is > 0
            ? $"请生成 {taskCount.Value} 条任务。"
            : "请按需求复杂度生成合理数量的任务，避免机械固定模板。";

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["requirementId"] = requirement.Id,
            ["requirementTitle"] = requirement.Title,
            ["requirementDescription"] = NormalizeOptional(requirement.Description) ?? "(无)",
            ["requirementPriority"] = requirement.Priority.ToString(),
            ["instruction"] = NormalizeOptional(instruction) ?? "(无)",
            ["taskCountInstruction"] = countText
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

    private static string BuildJsonOutputSystemPrompt()
    {
        return """
            你是 AgentSprint 的敏捷研发任务拆解助手。
            你只能输出 JSON 对象，不要输出 Markdown、说明文字或代码块。
            JSON 结构必须是：{"tasks":[{"title":"...","description":"..."}]}。
            title 必须是可执行的开发任务标题，不要复述需求标题。
            description 必须写清楚交付范围、关键步骤和验证点。
            """;
    }

    private static IReadOnlyList<SprintDevelopmentTaskDraft> ParseDrafts(string rawContent, int defaultPriority)
    {
        var payload = JsonSerializer.Deserialize<PreviewPayload>(ExtractJsonObject(rawContent), JsonOptions)
            ?? throw new InvalidOperationException("AI decomposition JSON cannot be parsed.");

        return (payload.Tasks ?? [])
            .Select(task => new SprintDevelopmentTaskDraft(
                NormalizeOptional(task.Title) ?? string.Empty,
                NormalizeOptional(task.Description),
                Math.Clamp(defaultPriority, 1, 9),
                NewDraftId()))
            .Where(task => !string.IsNullOrWhiteSpace(task.Title))
            .Take(20)
            .ToList();
    }

    private static string ExtractJsonObject(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        throw new InvalidOperationException("AI decomposition response does not contain a JSON object.");
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequired(string? value, string message)
    {
        return NormalizeOptional(value) ?? throw new InvalidOperationException(message);
    }

    private static IReadOnlyList<SprintDevelopmentTaskDraft> NormalizeDrafts(
        IReadOnlyList<SprintDevelopmentTaskDraft>? drafts,
        int priority,
        IReadOnlyList<SprintDevelopmentTaskDraft>? existingDrafts = null)
    {
        if (drafts is null || drafts.Count == 0)
        {
            throw new InvalidOperationException("At least one decomposition task is required.");
        }

        var existingIds = (existingDrafts ?? [])
            .Select(task => NormalizeOptional(task.Id))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        return drafts
            .Select(task => new SprintDevelopmentTaskDraft(
                NormalizeRequired(task.Title, "Task title is required."),
                NormalizeRequired(task.Description, "Task description is required."),
                Math.Clamp(priority, 1, 9),
                ResolveDraftId(task.Id, existingIds)))
            .Take(20)
            .ToList();
    }

    private static string ResolveDraftId(string? requestedId, ISet<string> existingIds)
    {
        var normalizedId = NormalizeOptional(requestedId);
        if (normalizedId is not null && existingIds.Remove(normalizedId))
        {
            return normalizedId;
        }

        return NewDraftId();
    }

    private static string NewDraftId()
    {
        return $"draft_{Guid.NewGuid():N}";
    }

    private static SprintRequirementDecompositionPreviewResult MapPreview(SprintRequirementDecompositionPreviewEntity entity)
    {
        return new SprintRequirementDecompositionPreviewResult(
            entity.Id,
            entity.RequirementId,
            entity.ProjectId,
            entity.Source,
            entity.Status,
            ReadTaskDrafts(entity.TaskJson),
            entity.RawContent,
            entity.Instruction,
            entity.AiPlatformCode,
            entity.ErrorMessage,
            entity.CreatedBy,
            entity.ConfirmedBy,
            entity.ConfirmedAt,
            entity.UpdateTime,
            entity.CreateTime);
    }

    private static IReadOnlyList<SprintDevelopmentTaskDraft> ReadTaskDrafts(string taskJson)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<SprintDevelopmentTaskDraft>>(taskJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? TruncateOptional(string? value, int maxLength)
    {
        return value is null ? null : Truncate(value, maxLength);
    }

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        double Temperature = 0.2,
        [property: JsonPropertyName("response_format")]
        ChatResponseFormat? ResponseFormat = null);

    private sealed record ChatResponseFormat(string Type);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(ChatMessage? Message);

    private sealed record PreviewPayload(IReadOnlyList<PreviewTask>? Tasks);

    private sealed record PreviewTask(string? Title, string? Description, int? Priority);
}
