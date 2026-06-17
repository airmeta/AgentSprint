namespace AgentSprint.Model.Modules.Agile.Workers;

public static class WorkerPlatformActorNames
{
    /// <summary>
    /// <para>zh-cn: 平台端 Akka 业务域名称，对应 AkkaSettings:Domains 的键，用于承载 Worker 向平台推送的轻量消息。</para>
    /// <para>en-us: Platform-side Akka domain name, matching the AkkaSettings:Domains key, used for lightweight Worker-to-platform messages.</para>
    /// </summary>
    public const string Domain = "AgentSprintPlatform";

    /// <summary>
    /// <para>zh-cn: 平台 API 节点的 Akka 角色，只有带该角色的节点会注册平台接收 Actor。</para>
    /// <para>en-us: Akka role for platform API nodes; only nodes with this role register platform receiving actors.</para>
    /// </summary>
    public const string Role = "agentsprint-platform";

    /// <summary>
    /// <para>zh-cn: 平台业务域 Actor 名称前缀，避免和 Worker 端 Actor 重名。</para>
    /// <para>en-us: Actor-name prefix for the platform domain, avoiding collisions with Worker actors.</para>
    /// </summary>
    public const string ActorNamePrefix = "agentsprint-platform";

    /// <summary>
    /// <para>zh-cn: Worker 命令日志接收 Actor 基础名称。</para>
    /// <para>en-us: Base actor name for receiving Worker command logs.</para>
    /// </summary>
    public const string WorkerCommandLogReceiver = "worker-command-log-receiver";

    /// <summary>
    /// <para>zh-cn: Worker 命令日志接收 Actor 的最终注册名称，Worker 通过该名称投递日志增量。</para>
    /// <para>en-us: Final registered name of the Worker command-log receiver; Workers send log chunks to this name.</para>
    /// </summary>
    public const string WorkerCommandLogReceiverRegisteredName = ActorNamePrefix + "-" + WorkerCommandLogReceiver;
}

