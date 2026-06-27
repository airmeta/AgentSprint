-- AgentSprint menu localization patch.
-- Execute manually when the existing sys_menu records need Chinese display names.
-- This script only updates sys_menu.Name by Path. It does not change routes, components, IDs, roles, or permissions.
-- Chinese names are written as UTF-8 hex literals to avoid terminal/client encoding drift.

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS sys_menu_name_backup_20260626 AS
SELECT Id, Path, Name, Type, Sort, Status, CreateTime, UpdateTime, IsDelete
FROM sys_menu;

UPDATE sys_menu
SET Name = CASE Path
  WHEN '/dashboard/workspace' THEN CONVERT(UNHEX('E5B7A5E4BD9CE58FB0') USING utf8mb4)
  WHEN '/sprint' THEN CONVERT(UNHEX('E58D8FE5908CE7A094E58F91') USING utf8mb4)
  WHEN '/sprint/project' THEN CONVERT(UNHEX('E9A1B9E79BAEE7AEA1E79086') USING utf8mb4)
  WHEN '/sprint/projects' THEN CONVERT(UNHEX('E9A1B9E79BAEE58897E8A1A8') USING utf8mb4)
  WHEN '/sprint/projects/detail/:id' THEN CONVERT(UNHEX('E9A1B9E79BAEE8AFA6E68385') USING utf8mb4)
  WHEN '/sprint/multi-endpoints' THEN CONVERT(UNHEX('E5A49AE7ABAFE7AEA1E79086') USING utf8mb4)
  WHEN '/sprint/product' THEN CONVERT(UNHEX('E4BAA7E59381E7AEA1E79086') USING utf8mb4)
  WHEN '/sprint/proposals' THEN CONVERT(UNHEX('E68F90E6A188E7AEA1E79086') USING utf8mb4)
  WHEN '/sprint/requirements' THEN CONVERT(UNHEX('E99C80E6B182E7AEA1E79086') USING utf8mb4)
  WHEN '/sprint/requirements/detail/:id' THEN CONVERT(UNHEX('E99C80E6B182E8AFA6E68385') USING utf8mb4)
  WHEN '/sprint/reviews' THEN CONVERT(UNHEX('E99C80E6B182E8AF84E5AEA1') USING utf8mb4)
  WHEN '/sprint/ai-chat' THEN CONVERT(UNHEX('E699BAE883BDE4BC9AE8AF9D') USING utf8mb4)
  WHEN '/sprint/worker' THEN CONVERT(UNHEX('E7A094E58F91E4BBBBE58AA1') USING utf8mb4)
  WHEN '/sprint/my-tasks' THEN CONVERT(UNHEX('E68891E79A84E4BBBBE58AA1') USING utf8mb4)
  WHEN '/sprint/tasks' THEN CONVERT(UNHEX('E4BBBBE58AA1E5A4A7E58E85') USING utf8mb4)
  WHEN '/sprint/tasks/detail/:id' THEN CONVERT(UNHEX('E4BBBBE58AA1E8AFA6E68385') USING utf8mb4)
  WHEN '/sprint/test' THEN CONVERT(UNHEX('E6B58BE8AF95E7AEA1E79086') USING utf8mb4)
  WHEN '/sprint/tests' THEN CONVERT(UNHEX('E6B58BE8AF95E8AEA1E58892') USING utf8mb4)
  WHEN '/sprint/defects' THEN CONVERT(UNHEX('E7BCBAE999B7E7AEA1E79086') USING utf8mb4)
  WHEN '/sprint/defects/detail/:id' THEN CONVERT(UNHEX('E7BCBAE999B7E8AFA6E68385') USING utf8mb4)
  WHEN '/sprint/git' THEN CONVERT(UNHEX('E4BBA3E7A081E4BB93E5BA93E7AEA1E79086') USING utf8mb4)
  WHEN '/sprint/git/accounts' THEN CONVERT(UNHEX('E4BBA3E7A081E8B4A6E58FB7') USING utf8mb4)
  WHEN '/sprint/git/repositories' THEN CONVERT(UNHEX('E4BBA3E7A081E4BB93E5BA93') USING utf8mb4)
  WHEN '/system' THEN CONVERT(UNHEX('E7B3BBE7BB9FE7AEA1E79086') USING utf8mb4)
  WHEN '/system/users' THEN CONVERT(UNHEX('E794A8E688B7E7AEA1E79086') USING utf8mb4)
  WHEN '/system/roles' THEN CONVERT(UNHEX('E8A792E889B2E7AEA1E79086') USING utf8mb4)
  WHEN '/system/roles/authorize/:id' THEN CONVERT(UNHEX('E8A792E889B2E68E88E69D83') USING utf8mb4)
  WHEN '/system/menus' THEN CONVERT(UNHEX('E88F9CE58D95E7AEA1E79086') USING utf8mb4)
  WHEN '/system/dictionaries' THEN CONVERT(UNHEX('E5AD97E585B8E7AEA1E79086') USING utf8mb4)
  WHEN '/system/configurations' THEN CONVERT(UNHEX('E7B3BBE7BB9FE9858DE7BDAE') USING utf8mb4)
  WHEN '/system/departments' THEN CONVERT(UNHEX('E983A8E997A8E7AEA1E79086') USING utf8mb4)
  WHEN '/system/assignments' THEN CONVERT(UNHEX('E7BB84E7BB87E4BBBBE591BD') USING utf8mb4)
  WHEN '/operations' THEN CONVERT(UNHEX('E8BF90E7BBB4E7AEA1E79086') USING utf8mb4)
  WHEN '/operations/scripts' THEN CONVERT(UNHEX('E8BF90E7BBB4E8849AE69CAC') USING utf8mb4)
  WHEN '/operations/environments' THEN CONVERT(UNHEX('E8BF90E8A18CE78EAFE5A283') USING utf8mb4)
  WHEN '/security' THEN CONVERT(UNHEX('E5AE89E585A8E7AEA1E79086') USING utf8mb4)
  WHEN '/system/agent-tokens' THEN CONVERT(UNHEX('E8AEBFE997AEE4BBA4E7898C') USING utf8mb4)
  WHEN '/code-review' THEN CONVERT(UNHEX('E4BBA3E7A081E8AF84E5AEA1') USING utf8mb4)
  WHEN '/code-review/tasks' THEN CONVERT(UNHEX('E8AF84E5AEA1E4BBBBE58AA1') USING utf8mb4)
  WHEN '/code-review/files' THEN CONVERT(UNHEX('E8AF84E5AEA1E69687E4BBB6') USING utf8mb4)
  WHEN '/code-review/results' THEN CONVERT(UNHEX('E8AF84E5AEA1E7BB93E69E9C') USING utf8mb4)
  WHEN '/global-config' THEN CONVERT(UNHEX('E585A8E5B180E9858DE7BDAE') USING utf8mb4)
  WHEN '/global-config/ai-platforms' THEN CONVERT(UNHEX('E699BAE883BDE5B9B3E58FB0') USING utf8mb4)
  WHEN '/global-config/ai-conversations' THEN CONVERT(UNHEX('E699BAE883BDE4BC9AE8AF9D') USING utf8mb4)
  WHEN '/global-config/prompt-templates' THEN CONVERT(UNHEX('E68F90E7A4BAE8AF8DE6A8A1E69DBF') USING utf8mb4)
  WHEN '/global-config/skills' THEN CONVERT(UNHEX('E68A80E883BDE9858DE7BDAE') USING utf8mb4)
  WHEN '/automation' THEN CONVERT(UNHEX('E887AAE58AA8E58C96') USING utf8mb4)
  WHEN '/automation/digital-workers' THEN CONVERT(UNHEX('E695B0E5AD97E59198E5B7A5') USING utf8mb4)
  WHEN '/automation/digital-workers/:id/command-audit' THEN CONVERT(UNHEX('E591BDE4BBA4E5AEA1E8AEA1') USING utf8mb4)
  WHEN '/automation/mcp-sessions' THEN CONVERT(UNHEX('E58D8FE8AEAEE4BC9AE8AF9D') USING utf8mb4)
  ELSE Name
END,
UpdateTime = UTC_TIMESTAMP(6)
WHERE Path IN (
  '/dashboard/workspace',
  '/sprint',
  '/sprint/project',
  '/sprint/projects',
  '/sprint/projects/detail/:id',
  '/sprint/multi-endpoints',
  '/sprint/product',
  '/sprint/proposals',
  '/sprint/requirements',
  '/sprint/requirements/detail/:id',
  '/sprint/reviews',
  '/sprint/ai-chat',
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
  '/security',
  '/system/agent-tokens',
  '/code-review',
  '/code-review/tasks',
  '/code-review/files',
  '/code-review/results',
  '/global-config',
  '/global-config/ai-platforms',
  '/global-config/ai-conversations',
  '/global-config/prompt-templates',
  '/global-config/skills',
  '/automation',
  '/automation/digital-workers',
  '/automation/digital-workers/:id/command-audit',
  '/automation/mcp-sessions'
);
