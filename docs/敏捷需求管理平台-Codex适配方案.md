# 敏捷需求管理平台 Codex 适配方案

本文描述一个面向 Codex 自动化研发的需求管理平台设计。目标是让项目经理、架构师、产品经理、开发、测试共同参与，并通过 MCP 服务把需求、Bug、项目规则、环境信息、设备状态和 Codex 执行过程串起来。

核心目标不是替代 Git，也不是把所有命令写进 prompt，而是让平台成为“任务与权限中枢”，让仓库成为“代码与项目文档中枢”，让 Codex 通过 MCP 领取任务、执行开发、发布测试环境、回写结果。

## 1. 总体定位

平台负责：

- 项目创建和成员管理
- 角色权限
- 项目初始化资料
- 需求生命周期
- Bug 生命周期
- 开发设备绑定
- Codex 上线、心跳、任务租约
- 测试环境发布记录
- 任务状态看板
- 审计日志

Git 仓库负责：

- 代码
- `docs/project` 项目规则
- `docs/works` 需求快照
- `docs/delivery` 交付记录
- 分支、commit、tag

MCP 服务负责：

- 给 Codex 暴露可控工具
- 下发项目上下文
- 下发可领取任务
- 接收 Codex 状态事件
- 接收部署、测试、Bug 修复结果
- 避免把密钥直接拼进 prompt

Codex 负责：

- 拉取仓库
- 读取 docs
- 使用 skill 执行流程
- 接取需求或 Bug
- 自动编码
- 自动测试、打包、发布测试环境
- 回写任务状态
- 推送代码

## 2. 角色与职责

### 2.1 项目经理

- 创建项目
- 指定项目成员和角色
- 审核产品经理提交的需求
- 指定架构师参与初始化
- 查看项目进度、风险、发布记录
- 关闭需求或版本

### 2.2 架构师

- 配置项目仓库地址
- 配置测试环境信息
- 协调 SSH key、部署账号、测试服务器资源
- 上传或维护项目初始 docs
- 配置 Codex 规则和 skill
- 审核架构影响较大的需求

### 2.3 产品经理

- 创建需求
- 编写验收标准
- 提交给项目经理评审
- 根据评审意见修改需求
- 验收已测试需求

### 2.4 开发

- 绑定开发设备
- 查看参与项目
- 获取快速连接提示词
- 启动 Codex 接入项目
- 选择“接取下一个任务”“等待平台分配”“继续在途任务”等简单动作
- 由 Codex 接取需求或 Bug 并完成编码
- 关注 Codex 输出的风险、冲突、测试失败

开发在系统中不需要直接操作复杂流程。开发的核心职责是确保设备在线、确认任务策略、处理 Codex 明确要求人工判断的阻塞点。后续可以进一步优化为完全被动分配：平台根据优先级、技能标签、设备状态和任务租约自动唤起或提示开发接入。

### 2.5 测试

- 查看已开发待测试需求
- 基于需求提交 Bug
- 通过 Codex 或平台标记测试中、测试通过、测试不通过
- 验证测试环境发布结果

### 2.6 Codex Agent

平台中应把每个 Codex 会话或设备视为一个 Agent 实例：

- 归属研发人员
- 绑定设备
- 绑定项目
- 有在线状态和心跳
- 有当前任务租约
- 有执行日志和事件流

## 3. 核心对象模型

### 3.1 Project

字段建议：

- `id`
- `name`
- `code`
- `repo_url`
- `default_branch`
- `test_branch_policy`
- `mcp_endpoint`
- `docs_path`
- `status`
- `owner_project_manager_id`
- `architect_ids`
- `created_at`

### 3.2 ProjectMember

- `project_id`
- `user_id`
- `role`: project_manager / architect / product / developer / tester
- `permission_scope`
- `status`

### 3.3 ProjectEnvironment

- `project_id`
- `env`: test / staging / prod
- `ssh_host_alias`
- `deploy_user`
- `deploy_path`
- `service_name`
- `health_check_url`
- `log_path`
- `deploy_command_ref`
- `rollback_command_ref`
- `secret_ref`

注意：平台可以存 Secret 引用，不应把 Secret 明文下发到提示词。

### 3.4 Requirement

- `id`
- `project_id`
- `title`
- `description`
- `priority`
- `status`
- `review_status`
- `created_by`
- `reviewed_by`
- `assignee_agent_id`
- `assignee_user_id`
- `branch`
- `work_doc_path`
- `delivery_doc_path`
- `test_url`
- `latest_commit`
- `created_at`
- `updated_at`

### 3.5 Bug

- `id`
- `project_id`
- `requirement_id`
- `title`
- `description`
- `severity`
- `status`
- `reproduce_steps`
- `expected_result`
- `actual_result`
- `created_by_tester_id`
- `assignee_agent_id`
- `branch`
- `latest_commit`

### 3.6 DeveloperDevice

- `id`
- `user_id`
- `device_name`
- `fingerprint`
- `local_ip`
- `os`
- `codex_version`
- `ssh_key_public_fingerprint`
- `status`
- `last_seen_at`

### 3.7 CodexSession

- `id`
- `device_id`
- `user_id`
- `project_id`
- `status`: online / busy / idle / offline
- `current_task_type`
- `current_task_id`
- `workspace_path`
- `branch`
- `last_heartbeat_at`

### 3.8 TaskLease

任务租约是关键对象，用来解决“关机、断网、换设备、重复接取”的问题。

- `id`
- `project_id`
- `task_type`: requirement / bug
- `task_id`
- `session_id`
- `device_id`
- `user_id`
- `status`: active / completed / expired / released / force_released
- `lease_expires_at`
- `heartbeat_at`
- `branch`
- `local_worktree_hint`

仓库里的临时目录只能作为本地恢复辅助，不能作为唯一任务锁。平台必须持有租约。

### 3.9 DeploymentRecord

- `id`
- `project_id`
- `task_type`
- `task_id`
- `env`
- `branch`
- `commit`
- `artifact`
- `test_url`
- `status`
- `deployed_by_session_id`
- `deployed_at`
- `rollback_info`

### 3.10 AgentEvent

- `id`
- `session_id`
- `project_id`
- `task_id`
- `event_type`
- `payload`
- `created_at`

事件示例：

- `agent_online`
- `repo_cloned`
- `docs_loaded`
- `task_claimed`
- `coding_started`
- `tests_started`
- `tests_passed`
- `tests_failed`
- `deploy_started`
- `deploy_succeeded`
- `deploy_failed`
- `code_pushed`
- `task_ready_for_test`

### 3.11 TestPlan

测试计划表示一次面向需求或 Bug 修复结果的测试轮次。MVP 阶段先以需求测试和回归测试为主，不强制建设完整测试用例库。

- `id`
- `project_id`
- `requirement_id`
- `bug_id`
- `name`
- `env`: test / staging
- `test_url`
- `status`: pending / testing / passed / failed / blocked / closed
- `created_by`
- `started_at`
- `completed_at`
- `summary`

### 3.12 TestExecution

测试执行记录用于记录测试人员对某个测试计划的实际执行结论。测试不通过时可以绑定或创建 Bug。

- `id`
- `test_plan_id`
- `requirement_id`
- `bug_id`
- `tester_id`
- `result`: passed / failed / blocked
- `actual_result`
- `evidence`
- `created_bug_id`
- `executed_at`

### 3.13 TestCase

测试用例库作为第二阶段能力。MVP 可以先不强制维护 `TestCase`，但模型上预留：

- `id`
- `project_id`
- `requirement_id`
- `title`
- `precondition`
- `steps`
- `expected_result`
- `priority`
- `status`

## 4. 项目初始化流程

### 4.1 项目经理创建项目

项目经理填写：

- 项目名称
- 项目标识
- 项目成员
- 默认流程模板
- 产品经理、架构师、开发、测试人员

项目状态：

```text
草稿 -> 初始化中 -> 可开发 -> 归档
```

### 4.2 架构师初始化项目

架构师配置：

- Git 仓库地址
- 数字员工 Git 账户名和密码或访问令牌
- 默认分支
- 测试环境服务器
- SSH Host 或连接方式
- 部署方式
- 数据库信息
- 项目规则 docs
- Codex skill
- 禁止访问的生产资源

初始化后，平台生成或要求仓库包含：

```text
docs/project/*.md
docs/works/
docs/delivery/
AGENTS.md
```

### 4.3 SSH Key 和密钥处理

建议原则：

- 平台可以保存公钥、指纹、Secret 引用。
- 平台不把私钥明文拼进快速连接提示词。
- 开发设备上由开发者或企业设备管理工具安装私钥。
- Codex 通过本地 SSH config 或安全凭证访问测试环境。
- MCP 工具只返回连接别名、权限范围、文档位置，不返回 Secret 明文。

如果确实需要平台托管密钥，必须做到：

- 加密存储
- 权限隔离
- 审计访问
- 短期凭证
- 不进入 prompt
- 不进入 Git
- 不进入日志

## 5. 需求流程

### 5.1 产品经理创建需求

需求状态：

```text
草稿 -> 待项目经理评审 -> 已通过-待开发 -> 开发中 -> 已开发-待测试 -> 测试中 -> 已测试 -> 已完成
```

驳回流程：

```text
待项目经理评审 -> 已驳回 -> 草稿
```

测试失败流程：

```text
测试中 -> 测试不通过 -> 待修复 -> 修复中 -> 已开发-待测试
```

### 5.2 项目经理评审需求

评审内容：

- 是否属于本项目
- 是否描述清楚
- 验收标准是否可测
- 是否需要架构师评审
- 是否需要拆分
- 优先级是否合理

通过后：

- 平台状态变为 `已通过-待开发`
- 生成或更新 `docs/works/REQ-xxxx_待开发.md`
- 推送到仓库

### 5.3 Codex 接取需求

Codex 通过 MCP 请求：

```text
claim_next_requirement(project_id, device_id, session_id)
```

平台校验：

- 用户是否是项目开发
- 设备是否已绑定
- 是否有活跃任务
- 是否存在可领取需求
- 需求是否已评审通过

领取成功后：

- 平台创建 `TaskLease`
- 平台状态变为 `开发中`
- Codex 在仓库中把需求文件改为 `_设备-用户-正在开发`
- Codex commit + push
- Codex 开始编码

## 6. Bug 流程

### 6.1 测试提交 Bug

Bug 必须绑定需求：

- 需求 ID
- 测试环境
- 复现步骤
- 预期结果
- 实际结果
- 截图或日志
- 严重级别

Bug 状态：

```text
待确认 -> 待修复 -> 修复中 -> 已修复-待回归 -> 回归中 -> 已关闭
```

驳回：

```text
待确认 -> 非缺陷 / 重复 / 暂不处理
```

### 6.2 Codex 接取 Bug

Codex 优先处理当前需求关联 Bug：

```text
claim_next_bug(project_id, requirement_id, device_id, session_id)
```

平台校验：

- Bug 是否属于当前研发人员或当前需求
- 是否已有他人租约
- 当前代码分支是否匹配

Bug 修复完成后：

- 运行相关测试
- 部署测试环境
- 回写 Bug 为 `已修复-待回归`
- 推送代码
- 记录 DeploymentRecord

## 6A. 测试流程

测试环节是需求闭环的一等流程，不应只依赖需求状态字段。平台需要保存测试轮次、测试执行结果、测试证据以及失败后关联 Bug 的关系。

### 6A.1 创建测试计划

当需求进入 `已开发-待测试` 后，测试人员或平台可以创建测试计划：

```text
已开发-待测试 -> 创建测试计划 -> 测试中
```

测试计划应绑定：

- 项目
- 需求
- 测试环境
- 测试地址
- 关联部署记录
- 测试负责人

### 6A.2 提交测试结果

测试执行结果分为：

```text
passed
failed
blocked
```

测试通过：

```text
测试中 -> 已测试 -> 产品或项目经理验收 -> 已完成
```

验收后的产品回馈继续形成下一轮需求：

```text
已测试/已完成 -> 记录产品回馈 -> 转为后续草稿需求 -> 提交评审 -> 任务拆解 -> 开发 -> 测试 -> 验收
```

后续需求必须保留 `source_requirement_id` 和 `source_feedback_id`，用于追踪来源需求和来源回馈；转出的后续需求仍从草稿开始，不能绕过评审、拆解、开发和测试流程。

测试不通过：

```text
测试中 -> 测试不通过 -> 待修复
```

测试阻塞：

```text
测试中 -> blocked
```

阻塞原因必须记录到 `TestExecution.actual_result` 或 `evidence`。

### 6A.3 失败关联 Bug

测试不通过时必须能绑定或创建 Bug：

- 如果是新问题，创建 Bug 并绑定需求、测试计划和执行记录。
- 如果是已有问题，测试执行记录绑定已有 Bug。
- Bug 修复后创建回归测试计划或复用原测试计划进入回归。

回归流程：

```text
已修复-待回归 -> 回归中 -> 回归通过 -> 已关闭
已修复-待回归 -> 回归中 -> 回归不通过 -> 待修复
```

### 6A.4 MVP 测试范围

第一版测试域先做：

- `TestPlan`
- `TestExecution`
- 测试失败绑定 Bug
- 测试计划列表和详情
- 提交测试结果
- 简单测试报告字段
- 产品回馈记录和回馈转后续需求

第一版暂不强制实现完整测试用例库、测试步骤编排、自动化测试平台对接和覆盖率统计。

## 7. 开发设备绑定

开发登录平台后：

1. 创建设备或选择已有设备。
2. 上传或登记 SSH 公钥指纹。
3. 平台生成设备绑定码。
4. Codex 快速连接时上报设备信息。
5. 平台确认设备归属。

设备字段：

- 设备名
- 操作系统
- 本地用户名
- SSH 公钥指纹
- Codex 版本
- 最近在线时间

设备可见范围：

- 开发只能看到自己绑定的设备。
- 项目经理可以看到项目内设备在线状态。
- 架构师可以看到设备接入诊断信息，但不应看到私钥。

## 8. 快速连接提示词

快速连接不是密钥载体，而是“让 Codex 知道如何接入平台和项目”的启动说明。

示例：

```text
请接入需求管理平台并初始化当前项目。

平台 MCP：
- endpoint: https://mcp.example.com/projects/PROJ001
- project_code: PROJ001
- device_code: DEV-8F3K2

仓库：
- repo_url: git@example.com:org/project.git
- default_branch: main

请执行：
1. 通过 MCP 注册当前 Codex session
2. 校验当前开发设备绑定状态
3. 拉取或更新仓库
4. 读取 AGENTS.md 和 docs/project/*.md
5. 向 MCP 发送 docs_loaded 上线通知
6. 检查是否存在当前设备的在途任务
7. 如果有在途任务，继续推进
8. 如果没有在途任务，询问是否接取下一个需求或 Bug

禁止：
- 输出密钥
- 访问生产环境
- 跳过平台任务租约
```

快速连接可以包含：

- MCP endpoint
- project code
- device code
- repo url
- default branch
- skill name
- 本地 workspace 建议路径

快速连接不应包含：

- SSH 私钥
- 数据库密码
- token 明文
- 生产服务器凭证

## 9. Codex 上线流程

Codex 接入项目后：

1. 注册 session：`register_session`
2. 校验设备：`verify_device`
3. 获取项目连接配置：`get_project_bootstrap`
4. 拉取仓库
5. 读取 docs
6. 上报：`notify_docs_loaded`
7. 查询在途任务：`get_active_lease`
8. 如果有任务，继续推进
9. 如果无任务，领取需求或 Bug

上线事件：

```json
{
  "event_type": "agent_online",
  "project_id": "PROJ001",
  "device_id": "DEV001",
  "workspace_path": "/work/project",
  "branch": "feature/REQ-1001"
}
```

## 10. 在途任务恢复

用户希望“关机后再打开 Codex，输入继续推进即可继续在途任务”，这需要三层恢复信息。

### 10.1 平台租约

平台保存：

- 当前任务
- 当前设备
- 当前分支
- 最近 commit
- 租约状态
- 心跳时间

### 10.2 仓库状态

仓库保存：

- 需求文件状态
- 分支
- commit
- delivery 记录
- docs

### 10.3 本地临时目录

本地可保存：

```text
.codex-work/
  active-task.json
  last-plan.md
  last-test-result.md
  last-deploy-result.md
```

这个目录不提交 Git。

`.gitignore`：

```gitignore
.codex-work/
```

`active-task.json` 示例：

```json
{
  "project_id": "PROJ001",
  "task_type": "requirement",
  "task_id": "REQ-1001",
  "lease_id": "LEASE-9001",
  "branch": "feature/REQ-1001",
  "work_doc": "docs/works/REQ-1001_devbox01-zhangsan-正在开发.md",
  "last_step": "tests_failed"
}
```

恢复流程：

1. Codex 向 MCP 查询 active lease。
2. 对照本地 `.codex-work/active-task.json`。
3. 校验当前分支和工作区。
4. pull/rebase 最新代码。
5. 从最后失败或中断步骤继续。

如果平台有 lease，但本地目录丢失，以平台和 Git 为准。

如果本地目录有任务，但平台 lease 已释放，停止并询问用户。

## 11. MCP 工具设计

建议 MCP 暴露这些工具。

### 11.1 Session

```text
register_session(project_code, device_code, workspace_path)
heartbeat(session_id, status, current_task)
close_session(session_id)
```

当前实现状态：

- 已新增本地 stdio MCP 服务 `src/api/AgentSprint.Mcp`。
- Codex 客户端通过 MCP 拉起该服务，服务内部登录 AgentSprint API 并桥接任务、项目和 Skill 上下文。
- 接入说明见 `docs/AgentSprint-MCP接入说明.md`。

### 11.2 Project

```text
get_project_bootstrap(project_code)
get_project_docs_manifest(project_id)
notify_docs_loaded(session_id, docs_summary)
```

当前已实现 `get_project_bootstrap`，返回项目、需求、任务、当前用户、工作区和 Skill 包；`get_project_docs_manifest` 与 `notify_docs_loaded` 待后续接仓库文档 manifest。

### 11.3 Requirement

```text
list_claimable_requirements(project_id)
claim_requirement(project_id, session_id, requirement_id?)
update_requirement_status(requirement_id, status, payload)
append_requirement_event(requirement_id, event)
```

当前首版通过 `list_task_hall`、`list_my_tasks`、`get_task_prompt`、`assign_task`、`complete_my_task` 覆盖任务领取和回写的最小 Codex 自动化链路。

### 11.4 Bug

```text
list_claimable_bugs(project_id, requirement_id?)
claim_bug(project_id, session_id, bug_id?)
update_bug_status(bug_id, status, payload)
append_bug_event(bug_id, event)
```

### 11.5 Lease

```text
get_active_lease(session_id)
renew_lease(lease_id)
release_lease(lease_id, reason)
complete_lease(lease_id, result)
```

### 11.6 Deployment

```text
notify_deploy_started(task_id, env, branch, commit)
notify_deploy_finished(task_id, env, status, test_url, artifact, rollback)
```

### 11.7 Test

```text
create_test_plan(requirement_id, env, test_url)
start_test_plan(test_plan_id)
submit_test_execution(test_plan_id, result, actual_result, evidence, bug_id?)
complete_test_plan(test_plan_id, status, summary)
list_test_plans(project_id, requirement_id?)
```

### 11.8 Audit

```text
append_agent_event(session_id, event_type, payload)
```

当前已实现 `append_agent_event`、`heartbeat`、`close_session` 的 MCP 返回确认；后续应落库为会话审计表。

## 12. Codex 执行状态机

Codex session 状态：

```text
offline
online
idle
claiming
coding
testing
building
deploying
waiting_for_user
blocked
completed
```

任务执行状态：

```text
claimed
repo_ready
docs_loaded
implementation_started
implementation_done
tests_running
tests_passed
build_passed
deploy_running
deploy_passed
ready_for_test
```

失败状态：

```text
claim_failed
merge_conflict
tests_failed
build_failed
deploy_failed
smoke_failed
permission_denied
missing_docs
```

## 13. 平台与仓库状态同步

建议策略：

- 平台状态是任务调度主状态。
- 仓库 `docs/works` 是可审计快照和离线可读状态。
- Codex 每次状态变化同时更新平台和仓库。
- 如果二者冲突，以平台租约为准，但必须生成冲突事件给项目经理或架构师处理。

状态同步示例：

1. 平台 requirement: `已通过-待开发`
2. 仓库文件：`REQ-1001_待开发.md`
3. Codex claim 成功
4. 平台 requirement: `开发中`
5. 仓库文件：`REQ-1001_devbox01-zhangsan-正在开发.md`
6. Codex 开发部署完成
7. 平台 requirement: `已开发-待测试`
8. 仓库文件：`REQ-1001_已开发-待测试.md`

## 14. 页面模块建议

### 14.1 项目经理视图

- 项目列表
- 项目成员
- 需求评审
- 进度看板
- 风险任务
- 设备在线情况
- 测试环境发布记录

### 14.2 架构师视图

- 项目初始化向导
- 仓库配置
- 测试环境配置
- docs 模板上传
- skill 配置
- SSH key 指纹和连接诊断
- 禁止资源配置

### 14.3 产品经理视图

- 创建需求
- 编辑需求
- 提交评审
- 查看评审意见
- 查看测试状态
- 验收关闭

### 14.4 开发视图

- 我的项目
- 我的设备
- 快速连接提示词
- 当前 Codex session
- 在途任务
- 接取下一个任务
- 等待平台分配
- 继续在途任务
- Codex 执行日志
- 测试环境地址
- 待处理 Bug

开发视图应尽量轻量，不要求开发手工维护需求状态。建议只暴露三类主操作：

1. 接取：让 Codex 主动领取当前项目下一个可开发需求或 Bug。
2. 等待：保持设备和 session 在线，等待项目经理、架构师或平台策略分配任务。
3. 继续：恢复当前在途任务，Codex 根据平台 lease、Git 分支和本地临时状态继续执行。

需求状态、Bug 状态、部署记录、交付记录由 Codex 通过 MCP 和 Git 自动更新。


### 14.5 测试视图

- 待测试需求
- 测试中
- 测试计划
- 测试执行记录
- 提 Bug
- 回归测试
- 测试通过
- 测试不通过反馈
- 测试报告

## 15. 权限模型

建议按项目授权：

| 操作 | 项目经理 | 架构师 | 产品 | 开发 | 测试 |
| --- | --- | --- | --- | --- | --- |
| 创建项目 | 是 | 否 | 否 | 否 | 否 |
| 配置仓库 | 是 | 是 | 否 | 否 | 否 |
| 配置环境 | 是 | 是 | 否 | 否 | 否 |
| 创建需求 | 是 | 是 | 是 | 否 | 否 |
| 评审需求 | 是 | 可参与 | 否 | 否 | 否 |
| 接取需求 | 否 | 可配置 | 否 | 触发 Codex | 否 |
| 提交 Bug | 是 | 是 | 是 | 是 | 是 |
| 接取 Bug | 否 | 否 | 否 | 触发 Codex | 否 |
| 测试通过 | 否 | 否 | 否 | 否 | 是 |
| 关闭需求 | 是 | 否 | 是 | 否 | 否 |

开发的“接取”权限本质上是触发 Codex 请求平台分配任务，不是绕过平台直接修改任务状态。真正的领取、租约创建、状态变更必须由 MCP 服务校验后完成。

## 16. 安全边界

必须避免：

- 把 SSH 私钥放进快速连接提示词
- 把数据库密码写入 docs
- Codex 直接拿生产权限
- 设备未绑定就能领取任务
- 没有租约也能回写任务状态
- 在平台和 Git 状态冲突时自动覆盖
- 日志记录 secret 明文

建议：

- 所有敏感凭证只传引用
- MCP 工具做权限校验
- 每个工具调用写审计日志
- 测试和生产环境权限物理隔离
- Codex 只拥有测试环境所需最小权限
- 任务租约设置过期时间
- 心跳超时后任务进入可接管或待确认状态

## 17. MVP 版本建议

第一版不要做完整 ALM，可以先做最小闭环。

### 17.1 MVP 范围

- 用户和角色
- 项目创建
- 仓库地址与数字员工 Git 凭据配置
- docs 上传
- 需求创建和项目经理评审
- 开发设备绑定
- 快速连接提示词
- MCP session 注册
- 需求领取和租约
- Codex 状态事件上报
- 测试环境发布记录
- 测试计划和测试执行
- Bug 提交和修复
- 简单看板

### 17.1.1 管理端菜单拆分

MVP 阶段不再只暴露一个“敏捷闭环”菜单，而是先按后续业务边界拆出独立菜单和路由，避免后续实现列表、详情和权限点时重复回归菜单结构。

左侧可见菜单：

- `敏捷研发 / 项目管理`
- `敏捷研发 / 需求管理`
- `敏捷研发 / 需求评审`
- `敏捷研发 / 任务大厅`
- `敏捷研发 / 我的任务`
- `敏捷研发 / 测试验证`
- `敏捷研发 / 缺陷管理`

详情路由先注册但不显示在左侧菜单：

- `/sprint/mvp`（旧 MVP 工作台，仅保留隐藏调试入口，不作为业务菜单验收项）
- `/sprint/projects/detail/:id`
- `/sprint/requirements/detail/:id`
- `/sprint/tasks/detail/:id`
- `/sprint/defects/detail/:id`

这些详情路由必须带 `hideInMenu=true`，并通过 `activePath` 高亮对应列表菜单。当前阶段列表和详情页面可以先使用 MVP 占位壳，真实表格、筛选、详情字段和操作按钮在对应模块推进时替换。

### 17.2 MVP 不做

- 复杂排期
- 工时统计
- 甘特图
- 生产发布审批
- 自动生成架构
- 跨项目资源调度
- 复杂权限继承

## 18. 最小闭环流程

1. 项目经理创建项目。
2. 架构师配置仓库、测试环境、docs、skill。
3. 产品经理创建需求。
4. 项目经理评审通过。
5. 平台生成 `REQ-xxxx_待开发.md` 并推送仓库。
6. 开发登录平台，复制快速连接提示词。
7. 开发把提示词交给 Codex。
8. Codex 注册 session，拉取仓库，读取 docs，上报上线。
9. 开发选择“接取下一个任务”或保持“等待平台分配”。
10. Codex 根据开发选择或平台分配领取需求，平台生成 lease。
11. Codex 改 docs/works 状态并 push。
12. Codex 开发、测试、构建、部署测试环境。
13. Codex 回写部署记录和交付记录。
14. Codex 推送代码，状态变为 `已开发-待测试`。
15. 测试人员创建测试计划并开始测试。
16. 测试人员提交测试执行结果，失败时绑定或创建 Bug。
17. Codex 接取 Bug 并修复。
18. 测试人员执行回归测试。
19. 测试通过后产品或项目经理关闭需求。

## 19. 关键设计结论

1. 平台必须有任务租约，不能只依赖仓库临时目录。
2. 快速连接提示词只放连接元信息，不放密钥。
3. Codex 通过 MCP 工具接取需求、回写状态、发送上线通知。
4. 仓库 docs 是项目规则和可审计状态快照，不是唯一调度中心。
5. 在途任务本地目录可以用于恢复，但平台 lease 才是权威。
6. 设备绑定是必要的，否则无法解决多设备并行和权限审计。
7. 测试环境自动部署必须和生产权限隔离。
8. 先做 MVP 跑通闭环，再扩展成完整敏捷研发平台。
9. 开发侧交互应保持极简，复杂流程由 Codex、MCP 和平台状态机承担。

## 20. 开发排程

本排程按最小 MVP 闭环推进，先保证菜单、列表、详情、状态流和核心操作可跑通。所有后端代码继续遵循 `Skill Air.Cloud.xxx` 风格，保持 `Model`、`Domain`、`Repository`、`Service`、`Entry` 五层边界。

管理端统一交互原则：

- 列表页主体只承载标题、筛选工具栏、列表、卡片和只读摘要。
- 新增、编辑、提交、评审、指派、执行结果、缺陷提交等带保存语义的表单必须放入 `Dialog` 或 `Drawer`，不得直接铺在页面主体。
- 页面筛选控件使用轻量工具栏呈现，不使用业务表单样式，避免和新增/编辑表单混淆。
- 详情优先使用抽屉承载，复杂详情页可以作为隐藏菜单路由存在，但列表内操作仍应保持当前上下文。

### 20.1 第一阶段：菜单结构和项目管理

目标：先固定管理端菜单和项目入口，避免后续模块重复调整路由和权限点。

交付项：

- 删除左侧可见菜单 `敏捷研发 / 任务拆解`。
- 新增左侧可见菜单 `敏捷研发 / 任务大厅`、`敏捷研发 / 我的任务`。
- 保留 `敏捷研发 / 项目管理`、`敏捷研发 / 需求管理`、`敏捷研发 / 需求评审`、`敏捷研发 / 测试验证`、`敏捷研发 / 缺陷管理`。
- 项目管理继续使用卡片视图，不改成表格。
- 项目卡片调整为紧凑尺寸，参考宽度 `300px`、高度 `150px`。
- 项目卡片只展示主要信息，并提供 `编辑`、`详情`、`统计` 操作链接。
- 点击项目操作链接时打开项目详情抽屉，抽屉宽度为页面 `60%`。

验收点：

- 左侧菜单与本排程一致。
- 项目卡片在桌面端一屏可展示更多项目，不再使用过大的卡片。
- 项目详情以抽屉打开，不跳出当前项目管理上下文。
- 后端动态菜单需要过滤历史遗留的 `MVP 工作台` 和 `任务拆解` 独立菜单；真实菜单接口只返回 `项目管理`、`需求管理`、`需求评审`、`任务大厅`、`我的任务`、`测试验证`、`缺陷管理` 这些可见入口，详情路由保持隐藏。

### 20.2 第二阶段：需求管理

目标：需求管理页形成“左项目树 + 右需求列表”的主工作台，产品经理可以围绕项目创建、编辑、推进需求。

交付项：

- 需求管理页面左侧展示项目树。
- 右侧展示需求列表，并根据左侧选中的项目过滤。
- 需求状态按真实流程维护，至少覆盖 `草稿`、`待评审`、`评审驳回`、`评审通过`、`已拆解`、`进行中`、`已完成`、`已作废`。
- 每条需求提供 `编辑`、`详情`、`任务拆解` 操作。
- `新增`、`编辑` 使用抽屉承载表单加 Markdown 编辑器，支持维护需求正文。
- 需求保存后展示 `立项推进` 按钮。
- 点击 `立项推进` 打开提交评审弹框，允许选择评审人。
- 需求编辑和提交评审必须由需求创建人/产品经理本人发起，服务端必须拒绝非创建人直接调用编辑或提交评审接口。
- 需求详情中允许需求创建人/产品经理本人对已驳回需求执行作废，服务端必须拒绝非创建人直接调用作废接口。
- 需求列表展示健康状态：存在缺陷标记为 `warn`，正在推进标记为 `primary`，已推进完成标记为 `success`。

验收点：

- 切换左侧项目树时，右侧需求列表正确过滤。
- 需求可编辑 Markdown 内容并保存。
- 草稿需求可提交评审，需求编辑、提交评审和驳回后作废仅允许创建人/产品经理本人执行。
- 健康状态能在列表中直接识别。

### 20.3 第三阶段：需求评审

目标：需求评审只处理“待我评审”的事项，并支持预览、通过、驳回。

交付项：

- 需求评审列表只展示当前登录人需要处理的评审项。
- 列表字段包含需求名、项目名、产品经理、干系人、提交时间、当前评审状态。
- 每条评审项提供 `评审`、`预览` 操作。
- `预览` 只展示需求内容、项目信息和评审进度。
- `评审` 包含预览能力，并额外提供 `通过`、`驳回` 操作。
- 所有评审人通过后，需求才进入评审通过状态。
- 任一评审人驳回后，需求进入评审驳回状态，并回到产品经理处理。

验收点：

- 非当前评审人的待审项不出现在列表。
- 全员通过前，需求不会提前进入下一阶段。
- 驳回后产品经理可以在需求详情中看到驳回结果；只有需求创建人/产品经理本人可以作废。

### 20.4 第四阶段：AI 任务拆解和任务大厅

目标：任务拆解作为需求操作存在，不再作为独立菜单；拆解结果进入任务大厅统一管理。

交付项：

- 从左侧菜单移除 `任务拆解`。
- 在需求列表和需求详情中保留 `任务拆解` 操作。
- 所有评审人通过后，需求进入 `已通过`，不自动生成任务；产品经理或项目管理角色需要在需求列表/详情中点击 `任务拆解` 后，任务才进入任务大厅。
- 点击 `任务拆解` 调用拆解入口；如果该需求已存在任务明细，只返回既有任务，不重复生成。
- 拆解入口支持可选 `taskCount` 配置；未配置时默认只生成 1 条任务，只有显式配置数量时才生成多条任务。
- 拆解后的任务明细需要持久化，绑定项目和需求。
- 新增 `任务大厅` 页面，展示当前登录人参与项目下的拆解任务。
- 任务大厅支持按项目、需求、状态、负责人过滤。
- 任务大厅列表的 `详情` 在当前页面打开只读抽屉，隐藏详情路由仅作为直接访问兜底。
- 任务拆解抽屉在手动指派模式下可以选择研发人员，生成任务时会直接指派给该研发人员；未选择时保留待指派。
- 架构师、产品经理、项目经理可以在任务大厅把任务指派给具体研发人员。
- 普通开发角色进入任务大厅时只展示自己的任务，不展示负责人筛选；负责人筛选和指派入口仅对架构师、产品经理、项目经理和超级管理员开放。
- MVP 当前落地最小 `ProjectMember` 项目成员表：创建项目、创建需求、提交评审、任务指派和测试计划/执行会自动维护项目参与人；任务大厅的“参与项目”按成员项目范围约束，普通开发只返回指派给自己的任务。

验收点：

- 需求全员评审通过后不会自动生成任务，手动触发 `任务拆解` 后才保存任务明细并在任务大厅看到待指派任务。
- 手动触发任务拆解不会重复生成已有任务。
- 任务大厅能看到当前人参与项目范围内的拆解任务；普通开发只能看到自己的任务。
- 具备权限的角色可以完成任务指派。

### 20.5 第五阶段：我的任务和任务推进

目标：研发人员可以查看自己被指派的任务，并复制提示词交给本地 Codex 推进。

交付项：

- 新增 `我的任务` 页面，展示当前登录人的所有任务。
- 我的任务列表展示项目、需求、任务标题、任务状态、优先级、指派人、更新时间。
- 我的任务列表的 `详情` 在当前页面打开只读抽屉，不跳出当前任务上下文。
- 每条任务提供 `任务推进` 按钮。
- 点击 `任务推进` 弹出提示词内容。
- 提示词只包含任务上下文、项目引用、仓库引用和操作指令，不包含 SSH 私钥、数据库密码等敏感明文。
- 研发人员手动复制提示词到本地 Codex 后继续后续开发流程。

验收点：

- 研发人员只能看到自己的任务。
- 任务推进弹框能生成可复制提示词。
- 提示词不泄漏敏感凭证。
- 非任务负责人不能获取任务推进提示词，也不能完成该任务。

### 20.6 第六阶段：测试验证

目标：测试验证作为需求闭环的一等流程，不只依赖需求状态字段，测试人员可以围绕需求创建测试计划、提交测试结果，并在失败时联动缺陷。

交付项：

- `测试验证` 页面展示测试计划列表，支持按项目和需求过滤。
- 新增测试计划时必须绑定项目和需求，可维护测试环境和测试地址。
- 测试服务端必须校验测试计划绑定的需求真实存在，且需求所属项目必须与测试计划项目一致。
- 创建测试计划和提交测试执行时，服务端记录测试人员为项目参与人，便于后续参与项目范围查询。
- 测试计划支持 `待执行`、`测试中`、`已通过`、`未通过`、`已阻塞` 状态。
- 每个测试计划提供 `详情`、`开始测试`、`提交结果` 操作。
- `开始测试` 由测试服务在服务端同步推进需求进入测试中，管理端不再额外调用需求状态接口。
- `提交结果` 支持通过、失败、阻塞三类结果，并保存实际结果和证据。
- 测试通过时由测试服务在服务端同步推进需求为已测试。
- 测试失败时必须选择自动创建缺陷或关联当前需求下已有未关闭缺陷。
- 测试服务端必须拒绝未绑定 `bug_id` 或 `created_bug_id` 的失败执行记录，避免绕过管理端约束只保存失败记录。
- 自动创建缺陷时必须绑定项目、需求、测试计划和测试执行记录。
- 关联已有缺陷时执行记录保存 `bug_id` 关联，避免重复创建同一问题。
- 关联已有缺陷且测试结果失败时，已有缺陷必须重新进入未修复状态，需求同步退回待修复。
- 测试计划详情展示基础信息和执行记录。

验收点：

- 测试计划可以按项目、需求筛选。
- 测试执行结果可持久化，并能在详情中查看。
- 测试通过后需求进入已测试状态。
- 测试失败自动创建缺陷时，缺陷能反向影响需求健康状态。
- 测试失败关联已有缺陷时，执行记录能展示已有缺陷关联，不新建重复缺陷。
- 测试失败不允许只保存失败记录而不绑定或创建缺陷。

### 20.7 第七阶段：缺陷管理和需求健康状态

目标：缺陷围绕项目和需求提交，并反向影响需求列表健康状态。

交付项：

- 缺陷管理支持按项目提交缺陷。
- 新增缺陷时必须选择具体需求。
- 缺陷记录保存项目、需求、标题、描述、严重级别、状态、提交人、处理人。
- 缺陷列表的 `详情` 在当前页面打开只读抽屉，展示缺陷基础信息、描述和关联测试执行记录。
- 产品经理在需求列表中可以看到当前需求健康状态。
- 需求存在未关闭缺陷时，健康状态显示 `warn`。
- 需求正在推进且无阻塞缺陷时，健康状态显示 `primary`。
- 需求推进完成且无未关闭缺陷时，健康状态显示 `success`。
- 同一需求存在多个未关闭缺陷时，修复其中一个缺陷不能提前恢复需求状态；必须所有未关闭缺陷都关闭后，需求健康状态才可恢复。

验收点：

- 缺陷必须绑定项目和需求。
- 新增或关闭缺陷后，需求列表健康状态同步变化。
- 多缺陷场景下，任一缺陷未关闭时需求仍保持风险状态。
- 产品经理能通过需求列表快速识别风险需求。

### 20.8 验收顺序

1. 先验收菜单结构和路由：确认左侧菜单、隐藏详情路由、菜单高亮正确。
2. 再验收项目管理：确认卡片尺寸、操作链接、详情抽屉符合要求。
3. 再验收需求管理：确认项目树过滤、需求编辑、立项推进、作废、健康状态。
4. 再验收需求评审：确认待我评审、预览、通过、驳回、全员通过规则。
5. 再验收任务拆解和任务大厅：确认 AI 拆解、任务持久化、任务指派。
6. 再验收我的任务：确认当前人任务列表和任务推进提示词。
7. 再验收测试验证：确认测试计划、开始测试、提交结果、失败创建缺陷。
8. 最后验收缺陷管理：确认缺陷绑定需求并反向刷新需求健康状态。

### 20.9 当前推进证据

记录时间：2026-06-07。

已验证事实：

- 后端测试：`dotnet test F:\AI\AgentSprint\src\api\AgentSprint.slnx --no-restore` 已通过，结果为 83 passed / 0 failed / 0 skipped。存在 `NU1900` 包漏洞源访问警告，未影响测试执行。
- 前端类型检查：`corepack pnpm -F @vben/web-tdesign run typecheck` 已通过。
- 运行服务：接口常驻在 `http://localhost:5000`，管理端常驻在 `http://localhost:5999`。
- 最终验收数据集：使用 `20260607003037` 后缀完成完整闭环数据；项目为 `FINAL-20260607003037 / Final acceptance project 20260607003037`。
- 菜单接口：`/menu/all` 返回可见敏捷菜单 `项目管理`、`需求管理`、`需求评审`、`任务大厅`、`我的任务`、`测试验证`、`缺陷管理`，详情路由带 `hideInMenu=true`，未返回历史 `MVP 工作台` 和 `任务拆解` 可见入口。
- 项目管理最终页面态：`docs/evidence/final-projects-20260607003037.png` 覆盖项目卡片列表；`docs/evidence/final-project-drawer-20260607003037.png` 覆盖最终项目详情抽屉。运行态测得抽屉内容宽度约 `779.5px / 1299px`，符合 60% 抽屉要求，且展示项目编码、仓库地址、测试环境和状态。
- 需求管理最终页面态：`docs/evidence/final-requirements-20260607003037.png` 覆盖项目树过滤、需求列表和健康状态；作废分支显示 `已作废/已作废`，交付分支显示 `待测试/健康`。
- 任务大厅最终页面态：`docs/evidence/final-tasks-20260607003037.png` 覆盖需求拆解后生成的任务列表、任务状态、负责人和行内 `详情/指派` 操作。
- 我的任务最终页面态：`docs/evidence/final-my-tasks-20260607003037.png` 覆盖当前开发人的任务列表和 `任务推进` 操作。
- 测试验证最终页面态：`docs/evidence/final-tests-20260607003037.png` 覆盖测试计划列表和未通过状态。
- 缺陷管理最终页面态：`docs/evidence/final-defects-20260607003037.png` 覆盖缺陷列表、需求绑定和已关闭状态。
- 需求评审最终页面态：`docs/evidence/final-reviews-20260607003037.png` 覆盖待我评审列表、评审和预览入口。
- 评审后任务大厅：登录 `admin/123456` 后，评审通过的需求需要先在 `需求管理` 触发 `任务拆解`，随后 `/mvp/tasks` 返回对应待指派任务；未配置 `taskCount` 时管理端 `任务大厅` 页面可见默认生成的 1 条待指派任务。
- 任务大厅刷新：`任务大厅` 页面已在页签/路由重新激活时重新拉取项目、需求和任务数据，避免从 `需求评审` 返回时因 KeepAlive 缓存停留在旧列表；运行态打开 `http://localhost:5999/sprint/tasks` 可直接看到待指派任务。
- 项目经理任务大厅：修正 `project_manager` 角色的任务大厅权限口径，项目经理作为任务指派管理角色可查看参与项目内已拆解任务；运行态登录 `project-manager/123456` 调 `/mvp/tasks` 返回参与项目内任务，包含 `pending_assign` 待指派任务。
- 多角色联调：开发模式已支持 `admin`、`pm`、`architect`、`project-manager`、`developer`、`tester` 使用统一密码 `123456` 登录，同时继续连接 MySQL 业务库；`developer` 登录后 `/user/info` 返回 `roles=["developer"]`，`pm` 返回 `roles=["pm"]`。
- 角色权限运行态：使用 `pm/123456` 创建 `ROLE-20260607000756` 项目和 `Role runtime requirement 20260607000756` 需求，评审通过后手动拆解默认生成 1 条任务；`pm` 可将任务指派给 `dev-1`，需求进入 `developing`；`developer/123456` 调 `/mvp/tasks` 和 `/mvp/tasks/my` 只返回指派给 `dev-1` 的 1 条任务，且任务推进提示词包含对应需求名；`developer` 直接调用任务指派接口返回 403。
- 角色页面态：管理端登录页账号选项已替换为 AgentSprint 本地角色账号；使用 `developer/123456` 登录后打开 `/sprint/tasks`，页面显示 `仅显示我的任务`，列表只显示 `Role runtime requirement 20260607000756` 的 1 条当前人任务，行内只有 `详情`，不展示 `指派` 操作。
- 直接调用拒绝：`tester/123456` 对 `dev-1` 任务调用 `/mvp/tasks/{id}/prompt` 和 `/mvp/tasks/{id}/complete` 均被服务端拒绝；`tester/123456` 对 `pm-1` 创建的需求调用编辑和提交评审接口也被服务端拒绝。
- 评审推进修复：修复评审记录更新后立即重新查询可能读到旧快照导致需求停留在 `pending_review` 的问题；新增 `ApproveRequirementReviewAsync_AdvancesWhenReviewQueryReturnsPersistedSnapshot` 覆盖全员通过后推进到 `approved` 且不自动生成任务的回归路径。
- 任务完成推进修复：修复任务完成后立即重新查询任务列表可能读到旧快照导致需求停留在 `developing` 的问题；新增 `CompleteDevelopmentTaskAsync_AdvancesRequirementWhenTaskQueryReturnsPersistedSnapshot` 覆盖全部任务完成后推进到 `ready_test` 的回归路径。
- 测试和缺陷闭环：`Role runtime requirement 20260607000756` 的拆解任务全部完成后需求进入 `ready_test/success`；`tester/123456` 创建测试计划并启动后需求进入测试流程，提交失败执行并绑定缺陷 `Runtime failing defect 20260607001916` 后需求进入 `pending_fix/warn`；`developer/123456` 领取并修复缺陷后需求回到 `ready_test/warn`；`tester/123456` 关闭缺陷后需求回到 `ready_test/success`。
- 驳回作废闭环：`pm/123456` 创建 `VOID-20260607002222` 项目和 `Void runtime requirement 20260607002222` 需求，提交 `arch-1` 评审后由 `architect/123456` 驳回；`tester/123456` 直接调用作废接口返回 400，`pm/123456` 作废成功，最终需求状态为 `voided` 并写入 `VoidedAt`。
- 作废健康状态：作废需求的 `Health` 现在返回 `voided`，管理端需求列表中 `Void runtime requirement 20260607002222` 的状态列和健康列均显示 `已作废`，不再误显示为 `推进中`。
- 测试/缺陷页面态：测试验证和缺陷管理页面默认不再强制选择第一个项目/需求，首次进入展示全量列表；`/sprint/tests` 可直接看到 `Runtime fail plan 20260607001916`、需求 `Role runtime requirement 20260607000756` 和 `未通过` 状态；`/sprint/defects` 可直接看到 `Runtime failing defect 20260607001916` 和 `已关闭` 状态。
- 管理端运行态抽样：`/sprint/tasks`、`/sprint/tests`、`/sprint/defects`、`/sprint/requirements`、`/sprint/projects`、`/sprint/reviews`、`/sprint/my-tasks` 均能渲染对应业务标题、列表区域和关键操作。
- 测试服务加固：失败执行绑定已有缺陷或自动创建缺陷时，服务端会在保存执行记录前校验缺陷归属；`SubmitExecutionAsync_RejectsCreatedBugFromDifferentRequirementBeforeSavingExecution` 覆盖了不属于当前需求的 created bug 不落库执行记录的回归路径。
- 产品回馈闭环补充：新增 `sprint_requirement_feedback`，已测试/已完成需求可以记录产品回馈；开放回馈可转换为后续草稿需求，并在新需求中保留 `SourceRequirementId` 和 `SourceFeedbackId`，后续需求继续提交评审、拆解、开发、测试和验收。

验收结论：

- 20.8 的菜单、项目、需求、评审、任务大厅、我的任务、测试验证和缺陷管理均已有接口、页面、截图和自动化测试证据。
- 产品回馈已可从已测试/已完成需求继续生成下一轮可追踪需求，闭环从单次交付扩展为连续迭代闭环。
- 当前仍保留的风险是页面截图为关键状态截图，不是完整录屏；若后续正式交付需要审计级证据，可在同一数据集上补录连续操作视频。
