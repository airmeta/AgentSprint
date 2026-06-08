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
3. MCP 可用后，再复制“任务推进”提示词到项目对话中发送。任务推进提示词只保留项目编码、任务 ID 和仓库引用，Codex 必须调用 `get_task_prompt(task_id)` 拉取任务、需求和 Skill 上下文后再推进。

初次接入提示词会要求 Codex 只修改 MCP 配置，不修改项目代码。需要写入的 Codex TOML 配置如下：

```toml
[mcp_servers.agentsprint]
url = "http://192.168.80.101:5010/mcp"
http_headers = { Authorization = "Bearer <这里填令牌>" }
```

默认只配置 MCP endpoint 和 Authorization，不要默认写入 `X-AgentSprint-Api-Base-Url`。Codex HTTP MCP 请求头必须使用 `http_headers` 字段，不要使用 `[mcp_servers.agentsprint.headers]` 子表。只有在用户明确提供“远程 MCP 服务可访问的 AgentSprint API 地址”时，才追加到 `http_headers`：

```toml
http_headers = {
  Authorization = "Bearer <这里填令牌>",
  "X-AgentSprint-Api-Base-Url" = "<远程 MCP 服务可访问的 AgentSprint API 地址>"
}
```

不要把 `http://localhost:5000` 固定写入 `X-AgentSprint-Api-Base-Url`，因为这里的 localhost 对远程 MCP 服务来说通常表示 MCP 服务所在机器，不一定是当前 Codex 开发机。

完整令牌只在创建时展示一次，令牌列表只展示掩码，不能从掩码恢复明文。不要把 Agent Token、数据库密码、SSH 私钥或服务器连接串写入任务提示词正文、Git 仓库或聊天记录。

## 工具清单

完整参数和返回字段见 [AgentSprint-MCP工具清单](./AgentSprint-MCP工具清单.md)。

- `register_session`：注册本地 Codex 会话并登录 AgentSprint。
- `get_mcp_tool_guide`：返回 MCP 工具使用指南，供 Codex 自举读取工具用途、参数、返回结构和推荐调用流程。
- `get_agent_skill_pack`：返回 AgentSprint 必需 Skill、后端规则、前端规则和验证命令。
- `get_project_bootstrap`：返回项目、需求、任务、当前用户、工作区和 Skill 包。
- `list_task_hall`：查询当前登录人可见的任务大厅。
- `list_my_tasks`：查询当前登录人的任务。
- `get_task_prompt`：按任务 ID 返回任务推进提示词、任务详情、需求详情和 Skill 包；Codex 应以返回的 `task_detail`、`requirement_detail` 为准，不从复制提示词正文解析需求内容。
- `assign_task`：把任务指派给开发人员。
- `complete_my_task`：开发完成并通过本地验证后回写任务完成，并返回推荐的下一工作项。
- `get_next_work`：不改变平台状态，按“我完成过的需求缺陷、我已分配任务、可见待分配任务”的顺序返回下一工作项。
- `claim_next_work`：按同一优先级自动领取下一工作项；缺陷会调用缺陷领取，待分配任务会尝试指派给当前 Codex 用户。
- `list_test_plans`：查询测试计划。
- `list_bugs`：查询缺陷。
- `list_my_open_bugs`：查询当前 Codex 用户已经完成过的需求下仍未关闭的缺陷。
- `claim_bug`：领取缺陷并创建修复租约。
- `fix_bug`：本地修复并验证后，将已领取缺陷标记为待回归。
- `heartbeat`、`append_agent_event`、`close_session`：记录本地 Codex 会话状态和事件。

## 任务推进建议

“任务推进”复制提示词不再内嵌需求正文、任务标题或任务说明，避免长需求挤占提示词并导致状态回写只能靠文本匹配。Codex 收到提示词后应先注册会话，再用提示词中的 `任务 ID` 调用 `get_task_prompt`，并在完成时使用同一个 `task_id` 调用 `complete_my_task`。

任务推进提示词建议要求 Codex 在完成当前任务后继续调用 `complete_my_task` 并检查其 `next_work` 字段；如果需要主动接取后续工作，可调用 `claim_next_work`。`claim_next_work` 的优先级如下：

1. 当前用户已经完成过的需求下，测试新提出且未关闭的缺陷。
2. 当前用户已经被分配或正在推进的开发任务。
3. 当前用户可见的待分配任务大厅任务。

`heartbeat` 仍用于会话状态记录，不承担推送式监听能力；Codex 应通过 `get_next_work` 或 `claim_next_work` 主动轮询平台最新工作项。`complete_my_task.next_work`、`get_next_work`、`claim_next_work` 和 `heartbeat` 会返回 `polling` 与 `session` 状态：

- `polling.strategy = linear` 表示线性退避轮询。
- `polling.next_interval_seconds = min(30 + idle_round * 30, 180)`，最长 180 秒。
- `polling.next_poll_after` 是建议的下一次轮询时间。
- `session.offline_requested = true` 或 `polling.should_continue = false` 时，Codex 应停止领取新工作，调用 `close_session` 并结束轮询。

管理端接口返回的任务推进提示词已包含以下内容：

```text
完成当前任务并调用 agentsprint.complete_my_task 后，必须读取返回的 next_work。
如果 next_work.kind = bug 或 task，继续处理或调用 agentsprint.claim_next_work 领取。
如果 next_work.kind = none，不要把会话视为结束；读取 next_work.polling，等待 polling.next_interval_seconds 后再次调用 agentsprint.get_next_work。
连续空闲轮询时，将 idle_round 加 1 后传回 get_next_work 或 heartbeat；发现工作项后将 idle_round 重置为 0。
如果 session.offline_requested = true 或 polling.should_continue = false，调用 agentsprint.close_session 并停止轮询。
```

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
