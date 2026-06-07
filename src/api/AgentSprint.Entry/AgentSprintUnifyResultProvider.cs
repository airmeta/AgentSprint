using Air.Cloud.WebApp.DataValidation.Internal;
using Air.Cloud.WebApp.FriendlyException.Internal;
using Air.Cloud.WebApp.UnifyResult;
using Air.Cloud.WebApp.UnifyResult.Attributes;
using Air.Cloud.WebApp.UnifyResult.Options;
using Air.Cloud.WebApp.UnifyResult.Providers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;

namespace AgentSprint.Entry;

/// <summary>
/// <para>zh-cn:AgentSprint 统一返回结果提供器，将 Air.Cloud 动态 Service 和兼容 Controller 的响应规范化为前端现有的 code/data/message 结构；已包装的 ApiResponse 会原样返回，避免兼容入口被二次包装。</para>
/// <para>en-us:AgentSprint unified result provider that normalizes Air.Cloud dynamic service and compatibility controller responses to the frontend's existing code/data/message contract; already wrapped ApiResponse values are returned unchanged to avoid double wrapping compatibility endpoints.</para>
/// </summary>
[UnifyModel(typeof(ApiResponse<>))]
public sealed class AgentSprintUnifyResultProvider : IUnifyResultProvider
{
    /// <summary>
    /// <para>zh-cn:将框架捕获的异常转换为统一错误响应，状态码同时写入 code 字段，消息优先使用异常元数据中的错误文本。</para>
    /// <para>en-us:Converts framework-captured exceptions to unified error responses, writing the HTTP status code into code and preferring the error text from exception metadata.</para>
    /// </summary>
    /// <param name="context">
    /// <para>zh-cn:MVC 异常上下文。</para>
    /// <para>en-us:MVC exception context.</para>
    /// </param>
    /// <param name="metadata">
    /// <para>zh-cn:Air.Cloud 解析出的异常元数据。</para>
    /// <para>en-us:Exception metadata resolved by Air.Cloud.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:统一 JSON 错误响应。</para>
    /// <para>en-us:Unified JSON error response.</para>
    /// </returns>
    public IActionResult OnException(ExceptionContext context, ExceptionMetadata metadata)
    {
        return new JsonResult(ApiResponse<object>.Error(ResolveMessage(metadata.Errors), metadata.StatusCode))
        {
            StatusCode = metadata.StatusCode
        };
    }

    /// <summary>
    /// <para>zh-cn:将成功执行结果包装为 code=0 的统一响应；若 action 已经返回 ApiResponse，则直接透传，保持旧 Controller 行为兼容。</para>
    /// <para>en-us:Wraps successful action results as code=0 unified responses; if an action already returns ApiResponse, it is passed through to preserve legacy controller compatibility.</para>
    /// </summary>
    /// <param name="context">
    /// <para>zh-cn:Action 执行完成上下文。</para>
    /// <para>en-us:Completed action execution context.</para>
    /// </param>
    /// <param name="data">
    /// <para>zh-cn:Action 原始返回数据。</para>
    /// <para>en-us:Raw data returned by the action.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:统一 JSON 成功响应。</para>
    /// <para>en-us:Unified JSON success response.</para>
    /// </returns>
    public IActionResult OnSucceeded(ActionExecutedContext context, object data)
    {
        if (IsApiResponse(data))
        {
            return new JsonResult(data);
        }

        return new JsonResult(ApiResponse<object>.Ok(data));
    }

    /// <summary>
    /// <para>zh-cn:将模型验证失败转换为 code=400 的统一响应，并保留框架生成的验证摘要作为 message。</para>
    /// <para>en-us:Converts model validation failures to code=400 unified responses and keeps the framework-generated validation summary as message.</para>
    /// </summary>
    /// <param name="context">
    /// <para>zh-cn:Action 执行前上下文。</para>
    /// <para>en-us:Action executing context.</para>
    /// </param>
    /// <param name="metadata">
    /// <para>zh-cn:验证失败元数据。</para>
    /// <para>en-us:Validation failure metadata.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:统一 JSON 验证错误响应。</para>
    /// <para>en-us:Unified JSON validation error response.</para>
    /// </returns>
    public IActionResult OnValidateFailed(ActionExecutingContext context, ValidationMetadata metadata)
    {
        var statusCode = metadata.StatusCode ?? StatusCodes.Status400BadRequest;
        return new JsonResult(ApiResponse<object>.Error(metadata.Message ?? ResolveMessage(metadata.ValidationResult), statusCode))
        {
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// <para>zh-cn:将 401、403、404 等无 Action 结果的状态码响应转换为统一 JSON 结构，便于前端请求拦截器按同一协议处理。</para>
    /// <para>en-us:Converts status-code responses such as 401, 403, and 404 without action results to unified JSON so the frontend interceptor can handle one response contract.</para>
    /// </summary>
    /// <param name="context">
    /// <para>zh-cn:当前 HTTP 上下文。</para>
    /// <para>en-us:Current HTTP context.</para>
    /// </param>
    /// <param name="statusCode">
    /// <para>zh-cn:响应状态码。</para>
    /// <para>en-us:Response status code.</para>
    /// </param>
    /// <param name="unifyResultSettings">
    /// <para>zh-cn:统一返回运行配置。</para>
    /// <para>en-us:Unified result runtime settings.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:异步写入任务。</para>
    /// <para>en-us:Task that writes the response asynchronously.</para>
    /// </returns>
    public async Task OnResponseStatusCodes(
        HttpContext context,
        int statusCode,
        UnifyResultSettingsOptions? unifyResultSettings = default)
    {
        UnifyContext.SetResponseStatusCodes(context, statusCode, unifyResultSettings);
        await context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Error(ReasonPhrases.GetReasonPhrase(statusCode), statusCode));
    }

    private static bool IsApiResponse(object data)
    {
        var dataType = data?.GetType();
        return dataType is not null &&
            dataType.IsGenericType &&
            dataType.GetGenericTypeDefinition() == typeof(ApiResponse<>);
    }

    private static string ResolveMessage(object? errors)
    {
        if (errors is null)
        {
            return "Request failed.";
        }

        if (errors is string message)
        {
            return message;
        }

        return errors.ToString() ?? "Request failed.";
    }
}
