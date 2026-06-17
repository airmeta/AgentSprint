using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AgentSprint.Model.Modules.Agile;
using AgentSprint.Model.Modules.Agile.Domains;
using AgentSprint.Model.Modules.Security;
using AgentSprint.Model.Modules.Security.Domains;
using AgentSprint.Model.Modules.Security.Dtos;
using AgentSprint.Model.Modules.Tests;
using AgentSprint.Model.Modules.Tests.Domains;
using AgentSprint.Service.Services;
using AgentSprint.Service.Services.SecurityServices;

namespace AgentSprint.Service.Impls.SecurityServices;

public sealed class AiConversationService : AgentSprintServiceBase, IAiConversationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly IAiConversationDomain _conversationDomain;
    private readonly ISystemConfigurationService _configurationService;
    private readonly ISprintProjectDomain _projectDomain;
    private readonly ISprintRequirementDomain _requirementDomain;
    private readonly ISprintDevelopmentTaskDomain _taskDomain;
    private readonly ISprintBugDomain _bugDomain;
    private readonly ITestPlanDomain _testPlanDomain;
    private readonly HttpClient _httpClient;

    public AiConversationService(
        IAiConversationDomain conversationDomain,
        ISystemConfigurationService configurationService,
        ISprintProjectDomain projectDomain,
        ISprintRequirementDomain requirementDomain,
        ISprintDevelopmentTaskDomain taskDomain,
        ISprintBugDomain bugDomain,
        ITestPlanDomain testPlanDomain,
        HttpClient httpClient)
    {
        _conversationDomain = conversationDomain;
        _configurationService = configurationService;
        _projectDomain = projectDomain;
        _requirementDomain = requirementDomain;
        _taskDomain = taskDomain;
        _bugDomain = bugDomain;
        _testPlanDomain = testPlanDomain;
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<AiConversationResult>> ListConversationsAsync(
        string? keyword = null,
        string? projectId = null,
        string? requirementId = null,
        string? taskId = null,
        string? testPlanId = null,
        string? bugId = null,
        string? status = null)
    {
        var normalizedKeyword = NormalizeOptional(keyword);
        var normalizedProjectId = NormalizeOptional(projectId);
        var normalizedRequirementId = NormalizeOptional(requirementId);
        var normalizedTaskId = NormalizeOptional(taskId);
        var normalizedTestPlanId = NormalizeOptional(testPlanId);
        var normalizedBugId = NormalizeOptional(bugId);
        var normalizedStatus = NormalizeOptional(status);

        return (await _conversationDomain.ListAsync())
            .Where(entity =>
                MatchOptional(normalizedProjectId, entity.ProjectId) &&
                MatchOptional(normalizedRequirementId, entity.RequirementId) &&
                MatchOptional(normalizedTaskId, entity.TaskId) &&
                MatchOptional(normalizedTestPlanId, entity.TestPlanId) &&
                MatchOptional(normalizedBugId, entity.BugId) &&
                MatchOptional(normalizedStatus, entity.Status) &&
                (string.IsNullOrWhiteSpace(normalizedKeyword) ||
                    TextContains(
                        normalizedKeyword,
                        entity.Title,
                        entity.AiPlatformCode,
                        entity.Provider,
                        entity.Model,
                        entity.UserMessage,
                        entity.AssistantMessage,
                        entity.ErrorMessage)))
            .OrderByDescending(entity => entity.StartedAt)
            .ThenByDescending(entity => entity.CreateTime)
            .Select(MapConversation)
            .ToList();
    }

    public async Task<AiConversationResult> StartConversationAsync(StartAiConversationRequest request, string createdBy)
    {
        ValidateRequired(request.Message, "Message is required.");
        if (string.IsNullOrWhiteSpace(request.RequirementId) &&
            string.IsNullOrWhiteSpace(request.TaskId) &&
            string.IsNullOrWhiteSpace(request.TestPlanId) &&
            string.IsNullOrWhiteSpace(request.BugId))
        {
            throw new InvalidOperationException("At least one associated requirement, task, test plan, or bug is required.");
        }

        var platformCode = string.IsNullOrWhiteSpace(request.AiPlatformCode) ? "openai" : request.AiPlatformCode.Trim();
        var platform = await _configurationService.GetAiPlatformRuntimeAsync(platformCode)
            ?? throw new InvalidOperationException("AI platform is not available.");

        var context = await BuildContextAsync(request);
        var contextSnapshot = JsonSerializer.Serialize(context, JsonOptions);
        var title = ResolveTitle(request, context);
        var entity = new AiConversationEntity
        {
            Title = title,
            AiPlatformCode = platform.Code,
            Provider = platform.Provider,
            Model = platform.Model,
            ProjectId = context.Project?.Id ?? NormalizeOptional(request.ProjectId),
            RequirementId = context.Requirement?.Id ?? NormalizeOptional(request.RequirementId),
            TaskId = context.Task?.Id ?? NormalizeOptional(request.TaskId),
            TestPlanId = context.TestPlan?.Id ?? NormalizeOptional(request.TestPlanId),
            BugId = context.Bug?.Id ?? NormalizeOptional(request.BugId),
            CreatedBy = createdBy,
            StartedAt = DateTime.UtcNow,
            ContextSnapshot = Truncate(contextSnapshot, 16384),
            UserMessage = Truncate(request.Message.Trim(), 8192),
            Status = AiConversationStatuses.Completed
        };

        try
        {
            entity.AssistantMessage = Truncate(await SendChatAsync(platform, contextSnapshot, entity.UserMessage), 16384);
            entity.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            entity.Status = AiConversationStatuses.Failed;
            entity.ErrorMessage = Truncate(ex.Message, 2048);
            entity.CompletedAt = DateTime.UtcNow;
        }

        await _conversationDomain.CreateAsync(entity);
        return MapConversation(entity);
    }

    private async Task<AiConversationContext> BuildContextAsync(StartAiConversationRequest request)
    {
        var requirement = await GetOptionalAsync(_requirementDomain, request.RequirementId, "Requirement");
        var task = await GetOptionalAsync(_taskDomain, request.TaskId, "Task");
        var testPlan = await GetOptionalAsync(_testPlanDomain, request.TestPlanId, "Test plan");
        var bug = await GetOptionalAsync(_bugDomain, request.BugId, "Bug");
        var projectId = NormalizeOptional(request.ProjectId) ??
            requirement?.ProjectId ??
            task?.ProjectId ??
            testPlan?.ProjectId ??
            bug?.ProjectId;
        var project = await GetOptionalAsync(_projectDomain, projectId, "Project");

        return new AiConversationContext(
            project is null ? null : new ContextItem(project.Id, project.Name, project.Code, project.Status, project.Description),
            requirement is null ? null : new ContextItem(requirement.Id, requirement.Title, requirement.Status, $"priority:{requirement.Priority}", requirement.Description),
            task is null ? null : new ContextItem(task.Id, task.Title, task.Status, $"priority:{task.Priority};assignee:{task.AssigneeId}", task.Description ?? task.Prompt),
            testPlan is null ? null : new ContextItem(testPlan.Id, testPlan.Name, testPlan.Status, testPlan.Environment, testPlan.Summary ?? testPlan.TestUrl),
            bug is null ? null : new ContextItem(bug.Id, bug.Title, bug.Status, $"{bug.Severity};{bug.Environment}", bug.Description));
    }

    private async Task<string> SendChatAsync(AiPlatformRuntimeResult platform, string contextSnapshot, string userMessage)
    {
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
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", platform.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(
                new ChatCompletionRequest(
                    platform.Model,
                    [
                        new ChatMessage("system", "你是烛照协同平台的AI助手。回答前必须基于已提供的业务上下文，无法确定时说明缺失信息。"),
                        new ChatMessage("system", $"业务上下文快照:\n{contextSnapshot}"),
                        new ChatMessage("user", userMessage)
                    ]),
                JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AI chat request failed: {(int)response.StatusCode} {body}");
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions);
        return completion?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
            ?? throw new InvalidOperationException("AI chat response is empty.");
    }

    private static async Task<TEntity?> GetOptionalAsync<TEntity>(
        IEntityDomainBase<TEntity> domain,
        string? id,
        string name)
        where TEntity : AgentSprint.Model.Modules.Common.EntityBase, new()
    {
        var normalized = NormalizeOptional(id);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return await domain.GetAsync(normalized)
            ?? throw new InvalidOperationException($"{name} does not exist.");
    }

    private static AiConversationResult MapConversation(AiConversationEntity entity)
    {
        return new AiConversationResult(
            entity.Id,
            entity.Title,
            entity.AiPlatformCode,
            entity.Provider,
            entity.Model,
            entity.ProjectId,
            entity.RequirementId,
            entity.TaskId,
            entity.TestPlanId,
            entity.BugId,
            entity.CreatedBy,
            entity.Status,
            entity.StartedAt,
            entity.CompletedAt,
            entity.ContextSnapshot,
            entity.UserMessage,
            entity.AssistantMessage,
            entity.ErrorMessage);
    }

    private static string ResolveTitle(StartAiConversationRequest request, AiConversationContext context)
    {
        var explicitTitle = NormalizeOptional(request.Title);
        if (!string.IsNullOrWhiteSpace(explicitTitle))
        {
            return Truncate(explicitTitle, 128);
        }

        return Truncate(
            context.Requirement?.Title ??
            context.Task?.Title ??
            context.TestPlan?.Title ??
            context.Bug?.Title ??
            request.Message.Trim(),
            128);
    }

    private static bool MatchOptional(string? expected, string? actual)
    {
        return string.IsNullOrWhiteSpace(expected) ||
            string.Equals(expected, actual, StringComparison.Ordinal);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool TextContains(string keyword, params string?[] values)
    {
        return values.Any(value => value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record AiConversationContext(
        ContextItem? Project,
        ContextItem? Requirement,
        ContextItem? Task,
        ContextItem? TestPlan,
        ContextItem? Bug);

    private sealed record ContextItem(
        string Id,
        string Title,
        string? CodeOrType,
        string Status,
        string? Content);

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        double Temperature = 0.2);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(ChatMessage? Message);
}
