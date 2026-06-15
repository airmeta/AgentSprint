namespace AgentSprint.Worker.Actors;

/// <summary>
/// <para>zh-cn:集中定义 AgentSprint Worker 使用的 Akka 业务域、角色和 Actor 注册名称，确保配置、特性和消息发送入口使用同一组稳定值。</para>
/// <para>en-us:Centralizes the Akka domain, role, and actor registration names used by AgentSprint Worker so configuration, attributes, and message sending share stable values.</para>
/// </summary>
public static class WorkerActorNames
{
    /// <summary>
    /// <para>zh-cn:Worker Actor 所属业务域，对应 `AkkaSettings:Domains` 的键。</para>
    /// <para>en-us:The Worker actor business domain, matching the `AkkaSettings:Domains` key.</para>
    /// </summary>
    public const string Domain = "AgentSprintWorker";

    /// <summary>
    /// <para>zh-cn:当前 Worker 节点角色；带该角色声明的 Actor 只会在配置了相同角色的节点注册。</para>
    /// <para>en-us:The current Worker node role; actors declaring this role are registered only on nodes configured with the same role.</para>
    /// </summary>
    public const string Role = "agentsprint-worker";

    /// <summary>
    /// <para>zh-cn:业务域 Actor 名称前缀，用于得到最终注册名并避免与其他业务域同名 Actor 冲突。</para>
    /// <para>en-us:The business-domain actor name prefix used to build final registration names and avoid collisions with actors from other domains.</para>
    /// </summary>
    public const string ActorNamePrefix = "agentsprint-worker";

    /// <summary>
    /// <para>zh-cn:事件上报 Actor 的基础名称，供 `AkkaActorAttribute` 自动注册时使用。</para>
    /// <para>en-us:The event reporting actor base name used by `AkkaActorAttribute` during automatic registration.</para>
    /// </summary>
    public const string EventReporter = "event-reporter";

    /// <summary>
    /// <para>zh-cn:事件上报 Actor 的最终注册名称，业务代码通过该名称投递 Worker 事件消息。</para>
    /// <para>en-us:The final registered event reporting actor name used by business code to send Worker event messages.</para>
    /// </summary>
    public const string EventReporterRegisteredName = ActorNamePrefix + "-" + EventReporter;
}
