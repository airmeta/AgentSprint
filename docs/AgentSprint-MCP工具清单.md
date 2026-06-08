# AgentSprint MCP 工具清单

## 使用定位

AgentSprint MCP 用于让 Codex 客户端通过 `agentsprint` MCP 服务获取平台上下文、领取任务/缺陷、回写任务状态，并按平台规则继续查找下一工作项。

Codex 侧不应只依赖静态提示词。推荐提示词只告诉 Codex：

1. 先调用 `agentsprint.register_session`。
2. 再调用 `agentsprint.get_agent_skill_pack`。
3. 通过 `agentsprint.get_task_prompt` 或 `agentsprint.get_project_bootstrap` 获取上下文。
4. 完成并验证后调用 `agentsprint.complete_my_task`。
5. 根据 `complete_my_task.next_work`，或主动调用 `agentsprint.get_next_work` / `agentsprint.claim_next_work` 继续处理后续工作。

## 通用返回包装

所有 MCP 工具调用都会经过 MCP 协议包装，外层返回：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `content` | array | MCP 文本内容，通常包含格式化后的 JSON 字符串。 |
| `structuredContent` | object/array | 工具真实结构化返回值，Codex 应优先读取这个字段。 |

下面各工具的“返回字段”均指 `structuredContent` 内部结构。

## 工具总览

| 工具名 | 作用 | 必填参数 | 可选参数 | 返回字段 |
| --- | --- | --- | --- | --- |
| `register_session` | 注册本地 Codex 会话并验证平台身份。 | 无 | `project_code`, `device_code`, `workspace_path`, `username`, `password` | `session_id`, `project_code`, `device_code`, `workspace_path`, `api_base_url`, `user` |
| `get_mcp_tool_guide` | 返回 MCP 工具使用指南，供 Codex 自举读取流程、参数、返回结构和投递方式。 | 无 | `format`，可选 `summary` 或 `full`，默认 `summary` | `format`, `purpose`, `recommended_flow`, `next_work_priority`, `delivery_options`, `safety_rules`, `tools`, `return_shapes`, `prompt_snippet` |
| `get_agent_skill_pack` | 获取 Codex 执行任务需要遵守的 Skill、规则和验证命令。 | 无 | `project_code` | `project_code`, `workspace_path`, `required_skills`, `backend_rules`, `frontend_rules`, `verification_commands` |
| `get_project_bootstrap` | 获取项目启动上下文，适合任务开始时整体拉取项目、需求、任务和规则。 | 无 | `project_code` | `project`, `project_code`, `project_id`, `workspace_path`, `api_base_url`, `current_user`, `skill_pack`, `requirements`, `tasks` |
| `list_task_hall` | 查询当前登录用户可见的任务大厅任务。 | 无 | `project_id`, `requirement_id`, `assignee_id`, `status`, `primary_only` | `SprintDevelopmentTaskResult[]` |
| `list_my_tasks` | 查询当前登录用户名下的任务。 | 无 | 无 | `SprintDevelopmentTaskResult[]` |
| `get_task_prompt` | 获取指定任务推进提示词、任务详情、需求详情和 Skill 包。 | `task_id` | 无 | `task_id`, `task_prompt`, `task_detail`, `requirement_detail`, `skill_pack`, `workspace_path`, `codex_instruction` |
| `complete_my_task` | 当前任务完成并本地验证通过后回写完成状态，同时返回下一工作项建议。 | `task_id` | `project_id`, `requirement_id`, `primary_only`, `owner_device` | `completed_task`, `next_work` |
| `assign_task` | 将拆解任务指派给指定开发人员。 | `task_id`, `assignee_id` | 无 | `SprintDevelopmentTaskResult` |
| `get_next_work` | 查询当前 Codex 用户下一项最高优先级工作，不改变平台状态。 | 无 | `project_id`, `requirement_id`, `owner_device`, `primary_only`, `session_id`, `idle_round` | `kind`, `reason`, `item`, `claim_supported`, `claim_note`, `polling`, `session` |
| `claim_next_work` | 自动领取下一项最高优先级工作。 | 无 | `project_id`, `requirement_id`, `owner_device`, `primary_only`, `session_id`, `idle_round` | `kind`, `reason`, `item`, `claim`, `polling`, `session` |
| `list_test_plans` | 查询测试计划。 | 无 | `project_id`, `requirement_id` | `TestPlanResult[]` |
| `list_bugs` | 查询缺陷。 | 无 | `project_id`, `requirement_id` | `SprintBugResult[]` |
| `list_my_open_bugs` | 查询当前 Codex 用户已完成过的需求下仍未关闭的缺陷。 | 无 | `project_id`, `requirement_id` | `SprintBugResult[]` |
| `claim_bug` | 领取指定缺陷并创建修复租约。 | `bug_id` | `owner_device` | `SprintTaskLeaseResult` |
| `fix_bug` | 本地修复并验证后，将指定缺陷标记为待回归。 | `bug_id` | 无 | `SprintBugResult` |
| `append_agent_event` | 记录本地 Codex 会话事件。当前只返回确认结果。 | 无 | `event_type`, `payload` | `accepted`, `event_type`, `payload`, `recorded_at` |
| `heartbeat` | 发送本地 Codex 心跳并返回轮询与会话状态。不承担推送监听。 | 无 | `session_id`, `status`, `current_task`, `idle_round` | `accepted`, `session_id`, `status`, `current_task`, `recorded_at`, `polling`, `session` |
| `close_session` | 关闭本地 Codex MCP 会话。当前只返回确认结果。 | 无 | `session_id` | `accepted`, `session_id`, `closed_at` |

## 关键返回对象

### `SprintDevelopmentTaskResult`

| 字段 | 说明 |
| --- | --- |
| `id` | 任务标识。 |
| `projectId` | 项目标识。 |
| `requirementId` | 需求标识。 |
| `endpointId` | 端标识，可能为空。 |
| `moduleId` | 模块标识，可能为空。 |
| `title` | 任务标题。 |
| `description` | 任务说明。 |
| `status` | 任务状态：`pending_assign`, `assigned`, `in_progress`, `completed`。 |
| `priority` | 优先级。 |
| `assigneeId` | 负责人。 |
| `assignedBy` | 指派人。 |
| `createdBy` | 创建人。 |
| `prompt` | 任务提示词。 |
| `assignedAt`, `startedAt`, `completedAt`, `updateTime`, `createTime` | 时间字段。 |

### `SprintBugResult`

| 字段 | 说明 |
| --- | --- |
| `id` | 缺陷标识。 |
| `projectId` | 项目标识。 |
| `requirementId` | 需求标识。 |
| `testPlanId` | 测试计划标识，可能为空。 |
| `testExecutionId` | 测试执行标识，可能为空。 |
| `title` | 缺陷标题。 |
| `description` | 缺陷说明。 |
| `environment` | 环境。 |
| `severity` | 严重程度：`critical`, `major`, `minor`, `trivial`。 |
| `status` | 缺陷状态：`open`, `fixing`, `fixed_ready_regression`, `closed`。 |
| `createdBy` | 提交人。 |
| `developerId` | 当前修复开发人员。 |
| `fixedAt`, `createTime` | 时间字段。 |

### `SprintTaskLeaseResult`

| 字段 | 说明 |
| --- | --- |
| `id` | 租约标识。 |
| `projectId` | 项目标识。 |
| `targetType` | 租约目标类型：`requirement` 或 `bug`。 |
| `targetId` | 目标标识。 |
| `ownerId` | 租约持有人。 |
| `ownerDevice` | 设备或会话标识。 |
| `leaseToken` | 租约令牌。 |
| `status` | 租约状态：`active`, `completed`, `released`。 |
| `expiresAt`, `completedAt`, `createTime` | 时间字段。 |

### `next_work`

`get_next_work`、`claim_next_work` 和 `complete_my_task.next_work` 使用同一结构：

| 字段 | 说明 |
| --- | --- |
| `kind` | 工作类型：`bug`, `task`, `none`。 |
| `reason` | 为什么推荐这一项。 |
| `item` | 推荐工作项。`kind=bug` 时是 `SprintBugResult`，`kind=task` 时是 `SprintDevelopmentTaskResult`。 |
| `claim_supported` | 仅查询时可能出现，表示是否支持通过 `claim_next_work` 领取。 |
| `claim_note` | 领取说明。 |
| `claim` | 仅 `claim_next_work` 成功领取时出现。领取 Bug 时是 `SprintTaskLeaseResult`；领取待分配任务时是 `SprintDevelopmentTaskResult`。 |
| `polling` | 建议轮询策略。即使 `kind=none` 也会返回，用于告诉 Codex 何时继续检查。 |
| `session` | 会话协作状态。当前返回协议状态；后续接入后台会话管理后可用于下线控制。 |

`claim_next_work` 的优先级：

1. 当前用户已经完成过的需求下仍未关闭的缺陷。
2. `/mvp/tasks/my` 返回的当前用户未完成任务，按 `in_progress`、`assigned`、`pending_assign` 优先级处理；真正开始处理前应调用 `claim_next_work` 创建 `development_task` 租约，避免同一令牌的多个 Codex 窗口同时推进同一个任务。
3. 当前用户可见的待分配任务大厅任务。

远程 HTTP MCP 请求必须携带当前登录人的 `Authorization: Bearer <token>`、`X-AgentSprint-Access-Token` 或 `X-AgentSprint-Agent-Token`；否则 MCP 无法按后台当前人读取 `/mvp/tasks/my`。

### `polling`

| 字段 | 说明 |
| --- | --- |
| `should_continue` | 是否建议继续轮询。 |
| `strategy` | 当前为 `linear`。 |
| `base_interval_seconds` | 基础间隔，当前 30 秒。 |
| `step_seconds` | 每次连续空闲增加的秒数，当前 30 秒。 |
| `max_interval_seconds` | 最大间隔，当前 180 秒。 |
| `idle_round` | 调用方传入的连续空闲轮询轮次。 |
| `next_interval_seconds` | 下次轮询间隔，公式为 `min(30 + idle_round * 30, 180)`。 |
| `next_poll_after` | 建议的下一次轮询时间。 |

### `session`

| 字段 | 说明 |
| --- | --- |
| `session_id` | 本地 Codex 会话标识。 |
| `status` | 协作状态，例如 `online`、`working`、`idle`、`closed`。 |
| `offline_requested` | 是否请求 Codex 协作式下线。当前为 false；后续可由后台会话管理控制。 |
| `stop_reason` | 停止原因。当前为空；后续可由后台会话管理返回。 |

## 推荐提示词片段

可以把下面片段加入平台生成的任务提示词：

```text
完成当前任务并通过本地验证后，调用 agentsprint.complete_my_task 回写任务状态。
读取返回值中的 next_work：
- kind = bug：优先处理测试新提出的缺陷；如需要领取，调用 agentsprint.claim_next_work 或 agentsprint.claim_bug。
- kind = task：继续处理已分配任务，或通过 agentsprint.claim_next_work 领取待分配任务。
- kind = none：说明当前没有可继续处理的工作项，但不要直接结束会话；读取 next_work.polling，等待 polling.next_interval_seconds 后再次调用 agentsprint.get_next_work。
连续空闲轮询时，将 idle_round 加 1 传回 get_next_work 或 heartbeat；发现工作项后将 idle_round 重置为 0。
如果 session.offline_requested = true 或 polling.should_continue = false，调用 agentsprint.close_session 并停止轮询。
不要把 SSH 私钥、数据库密码、Agent Token 等敏感明文写入提示词、代码、日志或 MCP 响应。
```

## 是否可以发送给 Codex 客户端

可以，但推荐方式不同：

| 方式 | 是否推荐 | 适用场景 | 注意事项 |
| --- | --- | --- | --- |
| 提示词 | 推荐用于短指令 | 让 Codex 知道启动顺序和完成后继续查下一项。 | 不建议把完整工具清单每次都塞进提示词，容易过长；提示词只保留关键流程。 |
| HTTP 链接 | 推荐用于完整清单 | 将本文档发布到 GitHub Raw、内网站点或平台文档页，让 Codex 按需打开。 | 链接必须是 Codex 可访问地址；不要在 URL 或页面中放令牌、密码、私钥。 |
| Skill | 推荐用于长期规范 | 把 MCP 使用流程、优先级、验证要求做成 Codex Skill，让每个任务自动套用。 | Skill 需要安装到 Codex 客户端环境；更新 Skill 后要确保客户端拿到新版本。 |
| MCP `tools/list` | 最可靠 | Codex 已经连上 MCP 后，直接读取实时工具 schema。 | 这是工具参数的权威来源；提示词和文档只作为行为引导。 |

推荐组合：

1. 平台提示词只放“执行顺序”和“完成后查 `next_work`”。
2. 完整工具清单通过 HTTP 文档链接提供。
3. 经常复用的执行规范做成 Skill。
4. Codex 已连上 MCP 后，优先调用 `get_mcp_tool_guide` 获取人类可读的流程说明，再以 MCP `tools/list` 和各工具返回的 `structuredContent` 为准。

