-- AgentSprint baseline dictionary seed.
-- This script is idempotent and can be executed after DatabaseInitializer.sql.

SET NAMES utf8mb4;

INSERT INTO sys_dictionary_type (
  Id, Code, Name, Description, Sort, Status, CreateTime, UpdateTime, IsDelete
)
VALUES
  ('ee190e49aa21459783f65e1dbe240650', 'frontend_tech_stack', '前端技术栈', 'Project frontend technology stack options.', 10, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('decd70677429429a8b1bcdd04bc73ca6', 'backend_tech_stack', '后端技术栈', 'Project backend technology stack options.', 20, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('cdb215bcdea14487aaeff0bfd42ad02e', 'runtime_container_type', '运行服务类型', 'Runtime service host/container type options used by environment service management.', 30, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('eb4147a9712343078c14e48d64c0b4ab', 'ai_platform_support', 'AI平台支持', 'AI platform options used by prompt template management.', 40, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('09837a2162fe4bfebe6cf37b3a00b705', 'digital_worker_employee_type', '数字员工类型', 'Digital worker employee type options used by worker management.', 50, 1, UTC_TIMESTAMP(6), NULL, 0)
ON DUPLICATE KEY UPDATE
  Name = VALUES(Name),
  Description = VALUES(Description),
  Sort = VALUES(Sort),
  Status = VALUES(Status),
  IsDelete = 0,
  UpdateTime = UTC_TIMESTAMP(6);

INSERT INTO sys_dictionary_item (
  Id, DictionaryTypeId, Code, Name, Description, Sort, Status, CreateTime, UpdateTime, IsDelete
)
VALUES
  ('4fc68e7cac97488a8a41e2a9d895a29e', 'ee190e49aa21459783f65e1dbe240650', 'vue3', 'Vue 3', NULL, 10, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('aaeab4839ecb480d88765acfcc42e85c', 'ee190e49aa21459783f65e1dbe240650', 'vite', 'Vite', NULL, 20, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('824b5645da9d4e80a7e534c2987e0726', 'ee190e49aa21459783f65e1dbe240650', 'tdesign', 'TDesign', NULL, 30, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('b9bca4a2c3fa4b2fab4a281d02177893', 'ee190e49aa21459783f65e1dbe240650', 'typescript', 'TypeScript', NULL, 40, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('9cbdb4cbeef24aabb7fd74886ea78883', 'decd70677429429a8b1bcdd04bc73ca6', 'dotnet', '.NET', NULL, 10, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('ddd8f4878f9d4f02b0fe247199d70fb1', 'decd70677429429a8b1bcdd04bc73ca6', 'ef-core', 'EF Core', NULL, 20, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('2a7dea480ee044bcb7935d2653be4db5', 'decd70677429429a8b1bcdd04bc73ca6', 'mysql', 'MySQL', NULL, 30, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('f247eda549ff41df8a3bf2cbfce6b187', 'decd70677429429a8b1bcdd04bc73ca6', 'mcp', 'MCP', NULL, 40, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('94c104482ebf4f56bcb6485b04bbd0b0', 'cdb215bcdea14487aaeff0bfd42ad02e', '0', 'Docker', NULL, 10, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('81fa44a7575948bd9f85a9e3802953d9', 'cdb215bcdea14487aaeff0bfd42ad02e', '1', 'K3S', NULL, 20, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('7d73df8ba7164a2d820da605467f4801', 'cdb215bcdea14487aaeff0bfd42ad02e', '2', 'K8S', NULL, 30, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('591eb347a7ac4103b53be852dcf4f78d', 'cdb215bcdea14487aaeff0bfd42ad02e', '3', 'Tomcat', NULL, 40, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('7e35685300e447c2ad065c9c743a4b73', 'cdb215bcdea14487aaeff0bfd42ad02e', '4', 'Nginx', NULL, 50, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('541a50fc447247c5899c5ba5a7b2e92f', 'cdb215bcdea14487aaeff0bfd42ad02e', '9', 'Other', NULL, 90, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('3243c8bf765748309c2a0904675db370', 'eb4147a9712343078c14e48d64c0b4ab', 'codex', 'Codex', NULL, 10, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('6333edbc2cdf4f9a92b1ea0c76b2a362', 'eb4147a9712343078c14e48d64c0b4ab', 'claude_code', 'ClaudeCode', NULL, 20, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('6636f0f59cc74521a35a3a7d90c1bcf5', 'eb4147a9712343078c14e48d64c0b4ab', 'work_buddy', 'WorkBuddy', NULL, 30, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('b9a4b6c713b8472182db44814ed26518', 'eb4147a9712343078c14e48d64c0b4ab', 'open_claw', 'OpenClaw', NULL, 40, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('c8ed188afe6e4cdd9479fb40dfab3af2', '09837a2162fe4bfebe6cf37b3a00b705', 'operations', '运维', NULL, 10, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('16ed1c004c214515a20acebd5c2bbe02', '09837a2162fe4bfebe6cf37b3a00b705', 'development', '研发', NULL, 20, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('d61b914aaa084d66a55ba5ac3bf89786', '09837a2162fe4bfebe6cf37b3a00b705', 'audit', '审计', NULL, 30, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('02ab3780278845899380521166b64f2e', '09837a2162fe4bfebe6cf37b3a00b705', 'test', '测试', NULL, 40, 1, UTC_TIMESTAMP(6), NULL, 0),
  ('8b32efc8eccf416698bf93fa00f825fa', '09837a2162fe4bfebe6cf37b3a00b705', 'product', '产品', NULL, 50, 1, UTC_TIMESTAMP(6), NULL, 0)
ON DUPLICATE KEY UPDATE
  DictionaryTypeId = VALUES(DictionaryTypeId),
  Name = VALUES(Name),
  Description = VALUES(Description),
  Sort = VALUES(Sort),
  Status = VALUES(Status),
  IsDelete = 0,
  UpdateTime = UTC_TIMESTAMP(6);
