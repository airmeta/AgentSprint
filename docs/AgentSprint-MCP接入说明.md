# AgentSprint MCP 接入说明

## 定位

AgentSprint MCP 是本地 Codex 客户端连接平台的必需通道。它负责把平台任务、项目上下文、Skill 规则、验证命令和任务状态回写能力传给 Codex，避免每个任务都靠手工复制提示词和重复配置环境。

当前首版采用远程 HTTP MCP 服务；Codex 客户端通过接入配置连接 MCP，MCP 内部使用 Agent Token 换取 AgentSprint API 访问身份并调用业务接口。

## 启动前置

API 必须先运行：

```powershell
$env:ConnectionStrings__AgentSprintConnectionString='server=192.168.100.162;port=3306;database=agentsprint;user=root;password=Aa123456!;Allow User Variables=True;UseAffectedRows=False;CharSet=utf8mb4;'
$env:Database__AutoInitialize='true'
$env:Database__UseInMemorySecurity='true'
dotnet run --project F:\AI\AgentSprint\src\api\AgentSprint.Entry\AgentSprint.Entry.csproj --urls http://localhost:5000
```

## Codex MCP 配置

推荐从“我的任务/任务推进”中点击“复制接入配置”，选择一个有效令牌记录，粘贴创建令牌时保存的完整令牌，确认后系统会把最终接入提示词写入剪切板。

接入流程：

1. 开发者先手动拉取项目代码，在 Codex 中打开该项目工作区，并配置本次工作所需的行为权限。
2. 将剪切板中的接入提示词发送到一个基于该项目的新对话，由 Codex 自动配置 `agentsprint` MCP。
3. MCP 可用后，再复制“任务推进”提示词到项目对话中发送，让 Codex 通过 MCP 拉取任务、需求和 Skill 上下文并推进。

初次接入提示词会要求 Codex 只修改 MCP 配置，不修改项目代码。需要写入的 Codex TOML 配置如下：

```toml
[mcp_servers.agentsprint]
url = "http://192.168.80.101:5010/mcp"

[mcp_servers.agentsprint.headers]
Authorization = "Bearer <这里填令牌>"
```

默认只配置 MCP endpoint 和 Authorization，不要默认写入 `X-AgentSprint-Api-Base-Url`。只有在用户明确提供“远程 MCP 服务可访问的 AgentSprint API 地址”时，才追加：

```toml
"X-AgentSprint-Api-Base-Url" = "<远程 MCP 服务可访问的 AgentSprint API 地址>"
```

不要把 `http://localhost:5000` 固定写入 `X-AgentSprint-Api-Base-Url`，因为这里的 localhost 对远程 MCP 服务来说通常表示 MCP 服务所在机器，不一定是当前 Codex 开发机。

完整令牌只在创建时展示一次，令牌列表只展示掩码，不能从掩码恢复明文。不要把 Agent Token、数据库密码、SSH 私钥或服务器连接串写入任务提示词正文、Git 仓库或聊天记录。

## 工具清单

- `register_session`：注册本地 Codex 会话并登录 AgentSprint。
- `get_agent_skill_pack`：返回 AgentSprint 必需 Skill、后端规则、前端规则和验证命令。
- `get_project_bootstrap`：返回项目、需求、任务、当前用户、工作区和 Skill 包。
- `list_task_hall`：查询当前登录人可见的任务大厅。
- `list_my_tasks`：查询当前登录人的任务。
- `get_task_prompt`：返回任务推进提示词和 Skill 包。
- `assign_task`：把任务指派给开发人员。
- `complete_my_task`：开发完成并通过本地验证后回写任务完成。
- `list_test_plans`：查询测试计划。
- `list_bugs`：查询缺陷。
- `heartbeat`、`append_agent_event`、`close_session`：记录本地 Codex 会话状态和事件。

## 首版边界

- MCP 首版桥接平台 API，不直接读写 MySQL。
- `append_agent_event`、`heartbeat`、`close_session` 当前返回确认结果，后续应落库为会话审计。
- 租约续期、发布通知、仓库文档 manifest 后续按文档第 11 章继续扩展。

## 验证

协议测试：

```powershell
dotnet test F:\AI\AgentSprint\src\api\AgentSprint.Tests\AgentSprint.Tests.csproj --filter AgentSprintMcpServerTests
```

stdio 手工验证示例：

```powershell
'{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | dotnet run --project F:\AI\AgentSprint\src\api\AgentSprint.Mcp\AgentSprint.Mcp.csproj
```
