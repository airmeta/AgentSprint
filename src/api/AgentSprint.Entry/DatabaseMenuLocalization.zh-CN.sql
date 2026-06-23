-- AgentSprint menu localization patch.
-- Execute manually when the existing sys_menu records need Chinese display names.
-- This script only updates sys_menu.Name by Path. It does not change routes, components, IDs, roles, or permissions.

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS sys_menu_name_backup_20260623 AS
SELECT Id, Path, Name, Type, Sort, Status, CreateTime, UpdateTime, IsDelete
FROM sys_menu;

UPDATE sys_menu
SET Name = CASE Path
  WHEN '/dashboard/workspace' THEN '工作台'
  WHEN '/sprint' THEN '协同研发'
  WHEN '/sprint/project' THEN '项目管理'
  WHEN '/sprint/projects' THEN '项目列表'
  WHEN '/sprint/projects/detail/:id' THEN '项目详情'
  WHEN '/sprint/multi-endpoints' THEN '多端管理'
  WHEN '/sprint/product' THEN '产品管理'
  WHEN '/sprint/requirements' THEN '需求管理'
  WHEN '/sprint/requirements/detail/:id' THEN '需求详情'
  WHEN '/sprint/reviews' THEN '需求评审'
  WHEN '/sprint/worker' THEN '研发任务'
  WHEN '/sprint/my-tasks' THEN '我的任务'
  WHEN '/sprint/tasks' THEN '任务大厅'
  WHEN '/sprint/tasks/detail/:id' THEN '任务详情'
  WHEN '/sprint/test' THEN '测试管理'
  WHEN '/sprint/tests' THEN '测试计划'
  WHEN '/sprint/defects' THEN '缺陷管理'
  WHEN '/sprint/defects/detail/:id' THEN '缺陷详情'
  WHEN '/sprint/git' THEN '代码仓库管理'
  WHEN '/sprint/git/accounts' THEN '代码账号'
  WHEN '/sprint/git/repositories' THEN '代码仓库'
  WHEN '/system' THEN '系统管理'
  WHEN '/system/users' THEN '用户管理'
  WHEN '/system/roles' THEN '角色管理'
  WHEN '/system/roles/authorize/:id' THEN '角色授权'
  WHEN '/system/menus' THEN '菜单管理'
  WHEN '/system/dictionaries' THEN '字典管理'
  WHEN '/system/configurations' THEN '系统配置'
  WHEN '/system/departments' THEN '部门管理'
  WHEN '/system/assignments' THEN '组织任命'
  WHEN '/operations' THEN '运维管理'
  WHEN '/operations/scripts' THEN '运维脚本'
  WHEN '/operations/environments' THEN '运行环境'
  WHEN '/code-review' THEN '代码评审'
  WHEN '/code-review/tasks' THEN '评审任务'
  WHEN '/code-review/results' THEN '评审结果'
  WHEN '/global-config' THEN '全局配置'
  WHEN '/global-config/ai-platforms' THEN '智能平台'
  WHEN '/global-config/ai-conversations' THEN '智能会话'
  WHEN '/global-config/prompt-templates' THEN '提示词模板'
  WHEN '/global-config/skills' THEN '技能配置'
  WHEN '/automation' THEN '自动化'
  WHEN '/automation/digital-workers' THEN '数字员工'
  WHEN '/automation/digital-workers/:id/command-audit' THEN '命令审计'
  WHEN '/automation/mcp-sessions' THEN '协议会话'
  WHEN '/security' THEN '安全管理'
  WHEN '/system/agent-tokens' THEN '访问令牌'
  ELSE Name
END
WHERE Path IN (
  '/dashboard/workspace',
  '/sprint',
  '/sprint/project',
  '/sprint/projects',
  '/sprint/projects/detail/:id',
  '/sprint/multi-endpoints',
  '/sprint/product',
  '/sprint/requirements',
  '/sprint/requirements/detail/:id',
  '/sprint/reviews',
  '/sprint/worker',
  '/sprint/my-tasks',
  '/sprint/tasks',
  '/sprint/tasks/detail/:id',
  '/sprint/test',
  '/sprint/tests',
  '/sprint/defects',
  '/sprint/defects/detail/:id',
  '/sprint/git',
  '/sprint/git/accounts',
  '/sprint/git/repositories',
  '/system',
  '/system/users',
  '/system/roles',
  '/system/roles/authorize/:id',
  '/system/menus',
  '/system/dictionaries',
  '/system/configurations',
  '/system/departments',
  '/system/assignments',
  '/operations',
  '/operations/scripts',
  '/operations/environments',
  '/code-review',
  '/code-review/tasks',
  '/code-review/results',
  '/global-config',
  '/global-config/ai-platforms',
  '/global-config/ai-conversations',
  '/global-config/prompt-templates',
  '/global-config/skills',
  '/automation',
  '/automation/digital-workers',
  '/automation/digital-workers/:id/command-audit',
  '/automation/mcp-sessions',
  '/security',
  '/system/agent-tokens'
);
