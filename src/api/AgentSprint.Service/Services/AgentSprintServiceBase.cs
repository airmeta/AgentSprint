using Air.Cloud.Core.Standard.DynamicServer;

namespace AgentSprint.Service.Services;

/// <summary>
/// <para>zh-cn:AgentSprint 应用服务基类，用于让业务 Service 被 Air.Cloud 动态 API 和依赖注入扫描识别；继承类型默认按瞬时生命周期注册，并可作为动态控制器暴露公开方法。</para>
/// <para>en-us:Base class for AgentSprint application services so Air.Cloud dynamic API and dependency injection scanning can discover them; derived services are registered as transient dependencies and can expose public methods as dynamic controllers.</para>
/// </summary>
public abstract class AgentSprintServiceBase : IDynamicService, ITransient
{
}
