SET NAMES utf8mb4;

INSERT INTO sys_prompt_template (
  Id,
  AgentEnvironment,
  Code,
  Name,
  Content,
  Description,
  Sort,
  Status,
  CreateTime,
  UpdateTime,
  IsDelete
)
VALUES (
  'prompt-requirement-decomposition',
  'codex',
  'requirement_decomposition',
  'AI任务拆解提示词',
  '{{taskCountInstruction}}

请结合下面的需求内容生成研发任务拆解草案。任务标题要可执行，任务内容要写清楚交付范围、关键步骤和验收点。不要生成任务优先级，任务优先级由需求优先级继承。

需求ID：
{{requirementId}}

需求标题：
{{requirementTitle}}

需求描述：
{{requirementDescription}}

需求优先级：
{{requirementPriority}}

拆解补充要求：
{{instruction}}',
  '变量：{{requirementId}}、{{requirementTitle}}、{{requirementDescription}}、{{requirementPriority}}、{{instruction}}、{{taskCountInstruction}}。',
  30,
  1,
  UTC_TIMESTAMP(6),
  NULL,
  0
)
ON DUPLICATE KEY UPDATE
  Name = VALUES(Name),
  Content = VALUES(Content),
  Description = VALUES(Description),
  Sort = VALUES(Sort),
  Status = VALUES(Status),
  IsDelete = 0,
  UpdateTime = UTC_TIMESTAMP(6);
