# AgentSprint.Tester 开发排程

## 1. 目标定位

`AgentSprint.Tester` 是独立于 `AgentSprint.Worker` 的测试受控端，运行在独立容器中，用来接取测试计划、测试用例和测试评审任务，并通过浏览器自动化与遥测能力完成 Web 自动化测试闭环。

它参考 `AgentSprint.Worker` 的受控端模式，但不复用 Codex 执行内核：

- `AgentSprint.Worker` 负责开发任务、缺陷修复、代码变更。
- `AgentSprint.Tester` 负责测试计划评审、测试用例生成、浏览器自动化执行、遥测证据采集和测试结果回写。
- 平台仍是事实源，负责测试计划、测试用例、租约、命令、运行记录、事件、证据和缺陷状态。
- Tester 只做受控执行，不绕过平台权限、状态机和审计。

目标闭环：

```text
创建测试计划
  -> 关联需求、任务、缺陷、Git 变更
  -> AI 测试评审
  -> AI 生成结构化测试用例
  -> 指派 AgentSprint.Tester
  -> Tester 领取测试计划或测试用例
  -> Vue 源码/路由/API 变更分析
  -> Playwright 浏览器执行
  -> 采集请求、响应、console、截图、trace、DOM/可访问性快照
  -> 断言与失败分析
  -> 回写测试执行结果、证据和缺陷
```

## 2. 核心设计原则

### 2.1 AI 规划，Tester 确定性执行

AI 不直接在浏览器里自由操作。AI 的职责是根据需求、任务、Git 变更、页面能力模型和测试计划生成结构化测试用例 DSL。

Tester 的职责是解释 DSL，并映射为确定性的 Playwright 操作。

```text
自然语言测试计划
  -> AI 测试评审
  -> 测试用例 DSL
  -> Action Executor
  -> Playwright API
  -> Telemetry Collector
  -> Test Report
```

### 2.2 源码分析辅助，运行时验证兜底

AgentSprint 的优势是测试计划可以直接或间接关联需求、任务、缺陷、Git 变更文件。Tester 可以先分析变更中的 Vue 源码、路由、API 文件和组件代码，推断页面能力与测试入口。

但源码分析只产生候选目标，最终仍由浏览器运行时验证：

- 源码告诉系统“页面大概率有什么按钮、表单、接口和状态变化”。
- 浏览器运行时告诉系统“当前账号、当前环境下是否真的可见、可点、可输入、可断言”。

### 2.3 通用性优先，测试锚点增强可靠性

不是所有页面都有 `data-testid`。Tester 的定位策略不能依赖测试锚点，但应优先使用它。

定位优先级：

```text
1. data-testid / data-qa / data-cy
2. role + name
3. label / placeholder
4. 可见文本
5. DOM 结构语义：form、table、dialog、input type、aria
6. Vue 源码分析产生的候选语义
7. 截图 + OCR + 坐标
8. AI 基于截图、DOM 摘要、可访问性树的修复建议
```

执行模式：

- `strict`：只执行高置信度定位，失败即停止。
- `assisted`：低置信度时调用 AI 选择候选或生成修复建议。
- `exploratory`：允许 AI 边观察边尝试，适合探索性测试，不作为稳定回归默认模式。

## 3. 建议新增项目

新增后端受控端项目：

```text
src/api/AgentSprint.Tester/
```

建议组件：

| 组件 | 职责 |
| --- | --- |
| `Program.cs` | Tester 宿主入口、依赖注入、配置加载 |
| `TesterOptions` | 读取 Tester 运行配置 |
| `AgentSprintTesterService` | 主循环，继承 `BackgroundService` |
| `AgentSprintTesterApiClient` | 调用 AgentSprint 平台 API |
| `TesterSessionManager` | 注册、心跳、下线、会话状态 |
| `TesterCommandLoop` | 轮询测试命令和测试租约 |
| `BrowserEnvironmentProbe` | 检查 dotnet、node、Playwright、浏览器依赖 |
| `BrowserTestRunner` | 管理 Playwright browser/context/page 生命周期 |
| `TestCaseDslExecutor` | 将测试步骤 DSL 映射为 Playwright 操作 |
| `LocatorResolver` | 多信号元素定位和置信度评分 |
| `VueSourceAnalyzer` | 分析 Vue 源码、路由、组件、事件、API 调用 |
| `PageCapabilitySnapshotter` | 生成按钮、输入框、表格、弹窗、菜单、请求能力快照 |
| `TelemetryCollector` | 采集 network、console、page error、截图、trace、HAR |
| `AssertionEngine` | 执行文本、元素、URL、请求、响应、视觉断言 |
| `TestRunLogger` | 保存本地 artifacts、manifest 和运行日志 |
| `SecretRedactor` | 对 token、cookie、password、authorization 等敏感信息脱敏 |

基础目录：

```text
/tester-home
/workspaces
/test-runs
/browser-cache
```

## 4. 平台模型建议

保留现有 `test_plan` 和 `test_execution` 作为测试计划入口和最终执行结果。

新增测试用例、评审、运行和遥测模型：

```text
test_case
- Id
- ProjectId
- RequirementId
- TestPlanId
- SourceType
- SourceId
- Title
- Priority
- Preconditions
- StepsJson
- ExpectedResult
- AutomationMode
- Status
- CreatedBy
```

```text
test_review
- Id
- ProjectId
- RequirementId
- TestPlanId
- SourceContextJson
- ReviewPrompt
- ReviewResultJson
- CoverageSummary
- RiskSummary
- Status
- CreatedBy
```

```text
test_case_run
- Id
- TestPlanId
- TestCaseId
- TesterId
- TesterSessionId
- TesterRunId
- Status
- StartedAt
- CompletedAt
- Summary
- Error
```

```text
test_telemetry_event
- Id
- TestPlanId
- TestCaseId
- TestCaseRunId
- StepIndex
- EventType
- Level
- Message
- PayloadJson
- ArtifactPath
- CreatedAt
```

建议事件类型：

```text
action_started
action_finished
locator_resolved
locator_failed
network_request
network_response
console_message
page_error
screenshot_saved
trace_saved
assertion_passed
assertion_failed
ai_repair_suggested
```

## 5. Tester 命令和运行类型

可以新增与 Worker 平行的 Tester 命令，也可以在平台已有 `WorkerCommand` 模型上泛化命名。建议先保持清晰边界，使用 Tester 专属常量。

命令类型：

```text
start_test_review
start_test_plan
start_test_case
cancel_current_run
stop_after_current
reload_config
smoke
```

运行类型：

```text
test_review
test_plan
test_case
browser_smoke
command
```

目标类型：

```text
test_plan
test_case
requirement
bug
development_task
```

## 6. 测试用例 DSL

测试步骤必须是结构化 DSL，不直接存自然语言操作。

示例：

```json
{
  "caseId": "tc_login_001",
  "name": "登录成功后进入工作台",
  "mode": "assisted",
  "variables": {
    "username": "admin",
    "password": "***"
  },
  "steps": [
    {
      "action": "goto",
      "url": "{{testUrl}}"
    },
    {
      "action": "fill",
      "target": {
        "intent": "用户名输入框",
        "candidates": [
          { "type": "testId", "value": "login-username" },
          { "type": "label", "value": "用户名" },
          { "type": "placeholder", "value": "请输入用户名" }
        ]
      },
      "value": "{{username}}"
    },
    {
      "action": "fill",
      "target": {
        "intent": "密码输入框",
        "candidates": [
          { "type": "testId", "value": "login-password" },
          { "type": "label", "value": "密码" },
          { "type": "css", "value": "input[type=password]" }
        ]
      },
      "value": "{{password}}"
    },
    {
      "action": "click",
      "target": {
        "intent": "登录按钮",
        "candidates": [
          { "type": "role", "role": "button", "name": "登录" },
          { "type": "text", "value": "登录" },
          { "type": "css", "value": "button[type=submit]" }
        ]
      }
    },
    {
      "action": "assertResponse",
      "urlContains": "/api/login",
      "status": 200
    },
    {
      "action": "assertUrl",
      "contains": "/dashboard"
    },
    {
      "action": "screenshot",
      "label": "login-success"
    }
  ]
}
```

第一版支持动作：

```text
goto
click
fill
select
check
upload
press
waitForText
waitForUrl
waitForResponse
assertVisible
assertText
assertUrl
assertRequest
assertResponse
assertConsoleNoError
assertTableRow
clickInRow
submitDialog
screenshot
saveTrace
```

## 7. Vue 源码分析设计

### 7.1 输入来源

源码分析输入：

- 测试计划关联的需求。
- 需求关联的开发任务。
- 任务关联的 Git 分支、提交、变更文件。
- 项目路由文件。
- Vue 单文件组件。
- API 客户端文件。
- 组件库使用方式。

### 7.2 分析目标

`VueSourceAnalyzer` 需要输出页面能力模型：

```json
{
  "route": "/sprint/tests",
  "title": "测试计划",
  "components": ["TButton", "TForm", "TTable", "TDialog"],
  "actions": [
    {
      "intent": "新增测试计划",
      "text": "新增测试计划",
      "handler": "openCreate",
      "candidate": {
        "type": "role",
        "role": "button",
        "name": "新增测试计划"
      }
    }
  ],
  "forms": [
    {
      "intent": "新增测试计划表单",
      "fields": ["项目", "需求", "测试计划名称", "测试地址"]
    }
  ],
  "tables": [
    {
      "intent": "测试计划列表",
      "columns": ["测试计划", "需求", "环境", "状态", "操作"]
    }
  ],
  "apiCalls": [
    {
      "method": "POST",
      "path": "/test/plans",
      "caller": "createTestPlanApi"
    }
  ]
}
```

### 7.3 源码分析限制

源码分析不能单独作为最终操作依据，因为以下情况只有运行时才能确认：

- `v-if`、`v-show`、权限控制导致按钮不渲染或不可见。
- 表格行按钮依赖后端数据状态。
- 弹窗和下拉框可能 Teleport 到 `body`。
- i18n、slot、父组件传参会改变最终文案。
- 异步接口返回前元素不存在。
- 移动端和桌面端布局不同。

因此源码分析只生成候选定位和测试风险点，必须经过 Playwright 运行时校验。

## 8. 浏览器遥测设计

第一版使用 `.NET Playwright`，默认浏览器为 Chromium，也支持配置使用 Google Chrome / Edge。

需要采集：

- 页面截图。
- Playwright trace。
- HAR。
- 请求 URL、method、headers、postData。
- 响应 status、headers、body 摘要。
- console log/warn/error。
- uncaught exception 和 page error。
- 当前 URL、title。
- DOM 摘要。
- accessibility tree 摘要。
- localStorage、sessionStorage、cookie 摘要。

Wireshark 不作为主抓包方案，只作为底层网络排障补充。业务级请求响应应优先通过 Playwright network event、HAR 或 Chrome DevTools Protocol 获取。

## 9. 失败自愈与人工确认

当步骤失败时，Tester 收集：

- 当前步骤。
- 定位候选。
- 定位失败原因。
- 当前截图。
- 当前 URL。
- 当前可访问按钮、输入框、表格、弹窗摘要。
- 相关请求响应。
- console/page error。

AI 可以生成修复建议：

```json
{
  "repair": {
    "target": {
      "type": "role",
      "role": "button",
      "name": "保存"
    },
    "confidence": 0.82,
    "reason": "页面实际主按钮文案为保存，而不是提交。"
  }
}
```

处理规则：

- `strict` 模式只记录建议，不自动重试。
- `assisted` 模式允许高置信度建议自动重试一次。
- `exploratory` 模式允许多步探索，但必须完整记录所有尝试。
- 所有修复建议都要落库，不能静默改写测试用例。

## 10. 阶段排程

### 第 1 阶段：Tester 最小可运行骨架

预计周期：2-3 天。

开发内容：

- 新增 `AgentSprint.Tester` 项目。
- 实现配置模型和 `BackgroundService` 主循环。
- 检查 dotnet、node、Playwright、浏览器可用性。
- 实现本地 smoke run。
- 保存本地运行目录：
  - `/test-runs/{runId}/run.json`
  - `/test-runs/{runId}/stdout.log`
  - `/test-runs/{runId}/screenshots`
  - `/test-runs/{runId}/trace.zip`

验收标准：

- Tester 容器可启动。
- 能打开指定 URL 并截图。
- 能捕获 console error。
- 能保存 trace 和 run manifest。

### 第 2 阶段：平台会话、心跳和命令接入

预计周期：3-4 天。

开发内容：

- 参考 Worker 模式新增 Tester 注册、心跳、命令 ACK、Run、Event 接口。
- 平台新增 Tester 主档或扩展 DigitalWorker 员工类型为 `test`。
- 支持命令：
  - `smoke`
  - `start_test_plan`
  - `start_test_case`
  - `cancel_current_run`
  - `stop_after_current`
  - `reload_config`
- Tester 上报 session、heartbeat、run、event。

验收标准：

- 平台能看到 Tester online、idle、busy、offline。
- 平台能下发一次 smoke 命令。
- Tester 执行后平台能看到 run 和事件。

### 第 3 阶段：测试计划到测试用例 DSL

预计周期：4-5 天。

开发内容：

- 新增 `test_case`、`test_review` 模型和接口。
- 基于测试计划、需求、任务、缺陷、Git 变更生成测试评审上下文。
- AI 输出结构化测试用例 DSL。
- DSL schema 校验。
- 前端页面支持查看测试评审、测试用例、步骤和覆盖点。

验收标准：

- 测试计划能生成一组测试用例。
- 每个测试用例包含结构化 steps。
- steps 能通过 schema 校验。
- 评审结果能显示覆盖风险、缺失场景和建议补测点。

### 第 4 阶段：Vue 源码与 Git 变更分析

预计周期：5-7 天。

开发内容：

- 根据测试计划追溯需求、任务和 Git 变更文件。
- 识别 Vue、TS、API、router 文件。
- 解析 Vue SFC 的 template/script。
- 提取按钮、表单、弹窗、表格、事件处理器、API 调用。
- 生成页面能力模型。
- 将页面能力模型注入测试用例生成提示词。

验收标准：

- 对变更中的 Vue 页面能生成页面能力摘要。
- 能识别新增/修改的按钮、表单字段、接口调用。
- AI 生成的测试步骤优先使用源码分析得出的候选目标。
- 源码候选和运行时定位结果能在报告中对应展示。

### 第 5 阶段：DSL 执行器与多信号定位

预计周期：5-7 天。

开发内容：

- 实现 `TestCaseDslExecutor`。
- 实现 `LocatorResolver`。
- 支持常用动作：
  - `goto`
  - `click`
  - `fill`
  - `select`
  - `waitForResponse`
  - `assertResponse`
  - `assertText`
  - `assertUrl`
  - `screenshot`
- 支持定位置信度评分。
- 支持 `strict`、`assisted`、`exploratory` 模式。

验收标准：

- 能执行登录、创建、列表查询、详情查看等常见 Web 流程。
- 没有测试锚点的页面能通过 role、label、placeholder、text 完成基础自动化。
- 低置信度定位会进入明确的失败或辅助修复流程。

### 第 6 阶段：遥测采集和执行报告

预计周期：4-6 天。

开发内容：

- 实现 `TelemetryCollector`。
- 采集 network、console、page error、screenshot、trace、HAR。
- 新增 `test_case_run`、`test_telemetry_event`。
- 运行结果关联截图、请求、响应、console、断言。
- 前端展示测试步骤证据链。

验收标准：

- 每个步骤可看到动作、截图、关键请求响应和断言结果。
- 失败步骤能看到定位失败、console error 或接口错误证据。
- 报告能汇总通过、失败、阻塞、缺陷建议。

### 第 7 阶段：失败分析、缺陷回写和回归闭环

预计周期：4-6 天。

开发内容：

- AI 分析失败原因。
- 自动生成缺陷草稿。
- 支持失败测试执行关联已有 Bug 或新建 Bug。
- 缺陷修复完成后支持回归测试计划重新执行。
- 测试通过后推进需求状态。

验收标准：

- 失败测试能生成可读的缺陷标题、复现步骤、证据链接。
- 已有 Bug 可关联失败执行。
- 回归测试通过后需求和缺陷状态能按现有规则推进。

## 11. 安全要求

- 不把 password、token、cookie、authorization、set-cookie 写入明文日志。
- 请求响应 body 默认做大小限制和敏感字段脱敏。
- 截图和 trace 视为敏感测试证据，按项目权限访问。
- Tester 容器使用独立 Agent Token。
- 浏览器 profile、cookie、storage 按项目或账号隔离。
- 测试账号池需要明确环境、权限和数据清理策略。
- `exploratory` 模式默认只允许在测试环境启用。

## 12. MVP 优先级

优先做：

1. `AgentSprint.Tester` 项目骨架。
2. Playwright 打开页面、截图、console 捕获。
3. Tester 注册、心跳、Run、Event。
4. 测试用例 DSL schema。
5. `goto`、`click`、`fill`、`assertResponse`、`screenshot`。
6. network、console、trace 采集。
7. 测试计划执行结果回写。

暂缓做：

1. 完整视觉 OCR。
2. 移动端真机测试。
3. Wireshark 主链路抓包。
4. 多浏览器并发矩阵。
5. 高级性能指标和 Core Web Vitals。
6. 完整自动自愈闭环。

