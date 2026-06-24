SET NAMES utf8mb4;

-- AgentSprint manual database initialization and schema evolution script.
-- Execute this file manually after deployment or schema changes. The API no longer runs database initialization on startup.
-- Target: MySQL/MariaDB database selected by the current connection.

DELIMITER $$
DROP PROCEDURE IF EXISTS agentsprint_add_column_if_not_exists$$
CREATE PROCEDURE agentsprint_add_column_if_not_exists(IN p_table_name VARCHAR(128), IN p_column_name VARCHAR(128), IN p_alter_sql TEXT)
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table_name AND COLUMN_NAME = p_column_name
  ) THEN
    SET @agentsprint_sql = p_alter_sql;
    PREPARE agentsprint_stmt FROM @agentsprint_sql;
    EXECUTE agentsprint_stmt;
    DEALLOCATE PREPARE agentsprint_stmt;
  END IF;
END$$

DROP PROCEDURE IF EXISTS agentsprint_create_index_if_not_exists$$
CREATE PROCEDURE agentsprint_create_index_if_not_exists(IN p_table_name VARCHAR(128), IN p_index_name VARCHAR(128), IN p_create_sql TEXT)
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table_name AND INDEX_NAME = p_index_name
  ) THEN
    SET @agentsprint_sql = p_create_sql;
    PREPARE agentsprint_stmt FROM @agentsprint_sql;
    EXECUTE agentsprint_stmt;
    DEALLOCATE PREPARE agentsprint_stmt;
  END IF;
END$$
DELIMITER ;

CREATE TABLE IF NOT EXISTS sys_user (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Username varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  DisplayName varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  PasswordHash varchar(256) CHARACTER SET utf8mb4 NOT NULL,
  Email varchar(128) CHARACTER SET utf8mb4 NULL,
  PhoneNumber varchar(32) CHARACTER SET utf8mb4 NULL,
  Avatar varchar(512) CHARACTER SET utf8mb4 NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_user_Username (Username)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_role (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_role_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_menu (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ParentId varchar(64) CHARACTER SET utf8mb4 NULL,
  Path varchar(256) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Component varchar(256) CHARACTER SET utf8mb4 NULL,
  Icon varchar(128) CHARACTER SET utf8mb4 NULL,
  Sort int NOT NULL,
  Type int NOT NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_permission (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  MenuId varchar(64) CHARACTER SET utf8mb4 NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_permission_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_user_role (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  UserId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  RoleId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_user_role_UserId_RoleId (UserId, RoleId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_role_menu (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  RoleId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  MenuId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_role_menu_RoleId_MenuId (RoleId, MenuId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_role_permission (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  RoleId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  PermissionId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_role_permission_RoleId_PermissionId (RoleId, PermissionId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_agent_token (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  TokenHash varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  TokenValue varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  TokenPrefix varchar(8) CHARACTER SET utf8mb4 NOT NULL,
  TokenSuffix varchar(8) CHARACTER SET utf8mb4 NOT NULL,
  OwnerUserId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NULL,
  ExpiresAt datetime(6) NOT NULL,
  LastUsedAt datetime(6) NULL,
  RevokedAt datetime(6) NULL,
  RevokedBy varchar(64) CHARACTER SET utf8mb4 NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  CONSTRAINT PK_sys_agent_token PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_agent_token_TokenHash (TokenHash),
  INDEX IX_sys_agent_token_OwnerUserId_Status (OwnerUserId, Status),
  INDEX IX_sys_agent_token_ProjectId (ProjectId)
) CHARACTER SET=utf8mb4;

CALL agentsprint_add_column_if_not_exists('sys_agent_token', 'TokenValue', 'ALTER TABLE sys_agent_token ADD COLUMN TokenValue varchar(128) CHARACTER SET utf8mb4 NOT NULL DEFAULT '''';');

CREATE TABLE IF NOT EXISTS sys_configuration (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  `Key` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `Value` varchar(2048) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_configuration_Key (`Key`)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS ai_conversation (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Title varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  AiPlatformCode varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Provider varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Model varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NULL,
  TaskId varchar(64) CHARACTER SET utf8mb4 NULL,
  TestPlanId varchar(64) CHARACTER SET utf8mb4 NULL,
  BugId varchar(64) CHARACTER SET utf8mb4 NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  StartedAt datetime(6) NOT NULL,
  CompletedAt datetime(6) NULL,
  ContextSnapshot text CHARACTER SET utf8mb4 NOT NULL,
  UserMessage text CHARACTER SET utf8mb4 NOT NULL,
  AssistantMessage text CHARACTER SET utf8mb4 NULL,
  ErrorMessage varchar(2048) CHARACTER SET utf8mb4 NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_ai_conversation_ProjectId_CreateTime (ProjectId, CreateTime),
  INDEX IX_ai_conversation_Targets (RequirementId, TaskId, TestPlanId, BugId),
  INDEX IX_ai_conversation_Platform_Status (AiPlatformCode, Status)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS test_plan (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  BugId varchar(64) CHARACTER SET utf8mb4 NULL,
  TesterId varchar(64) CHARACTER SET utf8mb4 NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Environment varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  TestUrl varchar(512) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  StartedAt datetime(6) NULL,
  CompletedAt datetime(6) NULL,
  Summary varchar(1024) CHARACTER SET utf8mb4 NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_test_plan_ProjectId_RequirementId (ProjectId, RequirementId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS test_execution (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  TestPlanId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  BugId varchar(64) CHARACTER SET utf8mb4 NULL,
  TesterId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Result varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  ActualResult varchar(2048) CHARACTER SET utf8mb4 NULL,
  Evidence varchar(2048) CHARACTER SET utf8mb4 NULL,
  CreatedBugId varchar(64) CHARACTER SET utf8mb4 NULL,
  ExecutedAt datetime(6) NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_test_execution_TestPlanId (TestPlanId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_user_group (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_user_group_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_role_group (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_role_group_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_department (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ParentId varchar(64) CHARACTER SET utf8mb4 NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Sort int NOT NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_department_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS assignment (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_assignment_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_dictionary_type (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Sort int NOT NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_dictionary_type_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_dictionary_item (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  DictionaryTypeId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Sort int NOT NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_dictionary_item_Type_Code (DictionaryTypeId, Code),
  INDEX IX_sys_dictionary_item_DictionaryTypeId (DictionaryTypeId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_entity_association (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  SourceEntityId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  TargetEntityId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  AssociationType varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_entity_association_Source_Target_Type (
    SourceEntityId,
    TargetEntityId,
    AssociationType
  )
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_runtime_environment (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NULL,
  EndpointId varchar(64) CHARACTER SET utf8mb4 NULL,
  ModuleId varchar(64) CHARACTER SET utf8mb4 NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  EnvironmentType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(1024) CHARACTER SET utf8mb4 NULL,
  ServerIps varchar(1024) CHARACTER SET utf8mb4 NULL,
  FrontendUrl varchar(512) CHARACTER SET utf8mb4 NULL,
  ApiBaseUrl varchar(512) CHARACTER SET utf8mb4 NULL,
  FrontendProxyApiUrl varchar(512) CHARACTER SET utf8mb4 NULL,
  McpEndpoint varchar(512) CHARACTER SET utf8mb4 NULL,
  DeployRoot varchar(512) CHARACTER SET utf8mb4 NULL,
  DockerDirectory varchar(512) CHARACTER SET utf8mb4 NULL,
  RemotePackagePath varchar(512) CHARACTER SET utf8mb4 NULL,
  ComposeFilePath varchar(512) CHARACTER SET utf8mb4 NULL,
  LocalPackagePaths varchar(2048) CHARACTER SET utf8mb4 NULL,
  Sort int NOT NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_runtime_environment_ProjectId_Code (ProjectId, Code),
  INDEX IX_sys_runtime_environment_Project_Endpoint_Module (ProjectId, EndpointId, ModuleId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_runtime_environment_container (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  RuntimeEnvironmentId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  ContainerType int NOT NULL,
  ServerIp varchar(64) CHARACTER SET utf8mb4 NULL,
  HostPort int NOT NULL,
  ContainerPort int NOT NULL,
  Protocol varchar(16) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Prompt text CHARACTER SET utf8mb4 NULL,
  DeployScript text CHARACTER SET utf8mb4 NULL,
  Sort int NOT NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_runtime_environment_container_Environment_Name (RuntimeEnvironmentId, Name),
  INDEX IX_sys_runtime_environment_container_RuntimeEnvironmentId (RuntimeEnvironmentId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sys_prompt_template (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  AgentEnvironment varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Content varchar(8192) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Sort int NOT NULL,
  Status int NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sys_prompt_template_Environment_Code (AgentEnvironment, Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_project (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  TestEnvironmentUrl varchar(512) CHARACTER SET utf8mb4 NULL,
  TestEnvironmentId varchar(64) CHARACTER SET utf8mb4 NULL,
  AiPlatformCode varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'openai',
  Description varchar(2048) CHARACTER SET utf8mb4 NULL,
  FrontendTechStack varchar(512) CHARACTER SET utf8mb4 NULL,
  BackendTechStack varchar(512) CHARACTER SET utf8mb4 NULL,
  ProjectManagerId varchar(64) CHARACTER SET utf8mb4 NULL,
  ProductManagerIds varchar(512) CHARACTER SET utf8mb4 NULL,
  DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  ArchitectId varchar(64) CHARACTER SET utf8mb4 NULL,
  SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sprint_project_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_skill (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Type varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'development',
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Content varchar(8192) CHARACTER SET utf8mb4 NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sprint_skill_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_project_endpoint (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Type varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  OwnerId varchar(64) CHARACTER SET utf8mb4 NULL,
  DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  Sort int NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sprint_project_endpoint_ProjectId_Code (ProjectId, Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_feature_module (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  EndpointId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(1024) CHARACTER SET utf8mb4 NULL,
  OwnerId varchar(64) CHARACTER SET utf8mb4 NULL,
  DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  Sort int NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sprint_feature_module_ProjectId_EndpointId_Code (ProjectId, EndpointId, Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_project_member (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  UserId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Role varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sprint_project_member_ProjectId_UserId_Role (ProjectId, UserId, Role)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_requirement (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  EndpointId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  ModuleId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Title varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(2048) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Priority int NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Stakeholders varchar(512) CHARACTER SET utf8mb4 NULL,
  ReviewedBy varchar(64) CHARACTER SET utf8mb4 NULL,
  DeveloperId varchar(64) CHARACTER SET utf8mb4 NULL,
  TestUrl varchar(512) CHARACTER SET utf8mb4 NULL,
  ApprovedAt datetime(6) NULL,
  SubmittedAt datetime(6) NULL,
  DevelopmentCompletedAt datetime(6) NULL,
  TestedAt datetime(6) NULL,
  ClosedAt datetime(6) NULL,
  VoidedAt datetime(6) NULL,
  SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_sprint_requirement_ProjectId_Status (ProjectId, Status)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_feature_suggestion (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  EndpointId varchar(64) CHARACTER SET utf8mb4 NULL,
  ModuleId varchar(64) CHARACTER SET utf8mb4 NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NULL,
  Content varchar(2048) CHARACTER SET utf8mb4 NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  ConvertedRequirementId varchar(64) CHARACTER SET utf8mb4 NULL,
  ConvertedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_sprint_feature_suggestion_ProjectId_ModuleId_RequirementId (ProjectId, ModuleId, RequirementId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_requirement_review (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  ReviewerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Comment varchar(512) CHARACTER SET utf8mb4 NULL,
  SubmitReason varchar(1024) CHARACTER SET utf8mb4 NULL,
  ReviewedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sprint_requirement_review_RequirementId_ReviewerId (RequirementId, ReviewerId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_development_task (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  DevelopmentTaskId varchar(64) CHARACTER SET utf8mb4 NULL,
  Title varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(2048) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Priority int NOT NULL,
  AssigneeId varchar(64) CHARACTER SET utf8mb4 NULL,
  AssigneeType int NOT NULL DEFAULT 0,
  AssignedBy varchar(64) CHARACTER SET utf8mb4 NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Prompt varchar(8192) CHARACTER SET utf8mb4 NULL,
  AssignedAt datetime(6) NULL,
  StartedAt datetime(6) NULL,
  CompletedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_sprint_development_task_ProjectId_RequirementId_AssigneeId (ProjectId, RequirementId, AssigneeId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_requirement_decomposition_preview (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Source varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  TaskJson text CHARACTER SET utf8mb4 NOT NULL,
  RawContent text CHARACTER SET utf8mb4 NULL,
  Instruction text CHARACTER SET utf8mb4 NULL,
  AiPlatformCode varchar(64) CHARACTER SET utf8mb4 NULL,
  ErrorMessage text CHARACTER SET utf8mb4 NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  ConfirmedBy varchar(64) CHARACTER SET utf8mb4 NULL,
  ConfirmedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_sprint_req_decomp_preview_req_status_time (RequirementId, Status, CreateTime)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_bug (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  TestPlanId varchar(64) CHARACTER SET utf8mb4 NULL,
  TestExecutionId varchar(64) CHARACTER SET utf8mb4 NULL,
  Title varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Description varchar(2048) CHARACTER SET utf8mb4 NULL,
  Environment varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Severity varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'major',
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  DeveloperId varchar(64) CHARACTER SET utf8mb4 NULL,
  FixedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_sprint_bug_ProjectId_RequirementId (ProjectId, RequirementId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_task_lease (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  TargetType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  TargetId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  ActiveTargetKey varchar(128) CHARACTER SET utf8mb4 NULL,
  OwnerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  OwnerDevice varchar(128) CHARACTER SET utf8mb4 NULL,
  LeaseToken varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  ExpiresAt datetime(6) NOT NULL,
  CompletedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_sprint_task_lease_LeaseToken (LeaseToken),
  UNIQUE INDEX IX_sprint_task_lease_ActiveTargetKey (ActiveTargetKey),
  INDEX IX_sprint_task_lease_ProjectId_OwnerId_Status (ProjectId, OwnerId, Status)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS sprint_requirement_feedback (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Title varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Content varchar(2048) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  ConvertedRequirementId varchar(64) CHARACTER SET utf8mb4 NULL,
  ConvertedAt datetime(6) NULL,
  ClosedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_sprint_requirement_feedback_RequirementId_Status (RequirementId, Status)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS git_account (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  Username varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  AccessToken varchar(512) CHARACTER SET utf8mb4 NULL,
  CommitAuthorName varchar(128) CHARACTER SET utf8mb4 NULL,
  CommitAuthorEmail varchar(256) CHARACTER SET utf8mb4 NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_git_account_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS git_repository (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  RepositoryUrl varchar(512) CHARACTER SET utf8mb4 NOT NULL,
  DefaultBranch varchar(64) CHARACTER SET utf8mb4 NULL,
  GitAccountId varchar(64) CHARACTER SET utf8mb4 NULL,
  LocalPath varchar(512) CHARACTER SET utf8mb4 NULL,
  Description varchar(512) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_git_repository_Code (Code)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS git_branch_operation (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  RepositoryId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  AccountId varchar(64) CHARACTER SET utf8mb4 NULL,
  OperationType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  BranchName varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  SourceBranch varchar(128) CHARACTER SET utf8mb4 NULL,
  BackupBranch varchar(128) CHARACTER SET utf8mb4 NULL,
  CommitHash varchar(64) CHARACTER SET utf8mb4 NULL,
  CommitMessage varchar(512) CHARACTER SET utf8mb4 NULL,
  PushedAt datetime(6) NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Message varchar(2048) CHARACTER SET utf8mb4 NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_git_branch_operation_RepositoryId_OperationType (RepositoryId, OperationType),
  INDEX IX_git_branch_operation_RepositoryId_BranchName (RepositoryId, BranchName)
) CHARACTER SET=utf8mb4;

CALL agentsprint_add_column_if_not_exists('git_account', 'CommitAuthorName', 'ALTER TABLE git_account ADD COLUMN CommitAuthorName varchar(128) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('git_account', 'CommitAuthorEmail', 'ALTER TABLE git_account ADD COLUMN CommitAuthorEmail varchar(256) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'GitRepositoryId', 'ALTER TABLE sprint_project ADD COLUMN GitRepositoryId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'GitAccountId', 'ALTER TABLE sprint_project ADD COLUMN GitAccountId varchar(64) CHARACTER SET utf8mb4 NULL;');

CREATE TABLE IF NOT EXISTS digital_worker (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  Code varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Name varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  AgentUserId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  AgentTokenId varchar(64) CHARACTER SET utf8mb4 NULL,
  ProjectIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  EndpointIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  EmployeeType varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'development',
  WorkerType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  MaxConcurrentRuns int NOT NULL,
  HeartbeatTimeoutSeconds int NOT NULL,
  PollIntervalSeconds int NOT NULL DEFAULT 15,
  IdleMaxIntervalSeconds int NOT NULL DEFAULT 180,
  MaxRunMinutes int NOT NULL DEFAULT 60,
  WorkspaceRoot varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT '/workspaces',
  RunsRoot varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT '/runs',
  CodexHome varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT '/codex-home',
  SandboxMode varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'workspace-write',
  RunSmokeOnStartup tinyint(1) NOT NULL DEFAULT 0,
  SmokePrompt varchar(1024) CHARACTER SET utf8mb4 NULL,
  AiPlatformCode varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'openai',
  CodexProvider varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'openai',
  CodexModel varchar(128) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'gpt-5.4',
  OpenAiBaseUrl varchar(512) CHARACTER SET utf8mb4 NULL,
  ConfigVersion int NOT NULL DEFAULT 1,
  Description varchar(1024) CHARACTER SET utf8mb4 NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_digital_worker_Code (Code),
  INDEX IX_digital_worker_AgentUserId_Status (AgentUserId, Status)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS worker_session (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  InstanceId varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  HostName varchar(128) CHARACTER SET utf8mb4 NULL,
  ContainerId varchar(128) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  CodexVersion varchar(128) CHARACTER SET utf8mb4 NULL,
  GitVersion varchar(128) CHARACTER SET utf8mb4 NULL,
  DotnetVersion varchar(128) CHARACTER SET utf8mb4 NULL,
  NodeVersion varchar(128) CHARACTER SET utf8mb4 NULL,
  ConfigTomlExists tinyint(1) NOT NULL,
  CodexHome varchar(512) CHARACTER SET utf8mb4 NULL,
  WorkspaceRoot varchar(512) CHARACTER SET utf8mb4 NULL,
  RunsRoot varchar(512) CHARACTER SET utf8mb4 NULL,
  CurrentRunId varchar(64) CHARACTER SET utf8mb4 NULL,
  ErrorSummary varchar(1024) CHARACTER SET utf8mb4 NULL,
  LastHeartbeatAt datetime(6) NULL,
  StartedAt datetime(6) NOT NULL,
  StoppedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_worker_session_WorkerId_Status (WorkerId, Status),
  INDEX IX_worker_session_WorkerId_InstanceId (WorkerId, InstanceId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS worker_command (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  SessionId varchar(64) CHARACTER SET utf8mb4 NULL,
  CommandType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Title varchar(256) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
  PayloadJson text CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  AckedAt datetime(6) NULL,
  StartedAt datetime(6) NULL,
  CompletedAt datetime(6) NULL,
  ExpiresAt datetime(6) NULL,
  ResultJson text CHARACTER SET utf8mb4 NULL,
  ChangedFilesJson text CHARACTER SET utf8mb4 NULL,
  GitCommitId varchar(64) CHARACTER SET utf8mb4 NULL,
  Error varchar(1024) CHARACTER SET utf8mb4 NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_worker_command_WorkerId_Status (WorkerId, Status),
  INDEX IX_worker_command_SessionId_Status (SessionId, Status)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS worker_command_log (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  SessionId varchar(64) CHARACTER SET utf8mb4 NULL,
  InstanceId varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  CommandId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  RunId varchar(64) CHARACTER SET utf8mb4 NULL,
  LogText longtext CHARACTER SET utf8mb4 NOT NULL,
  StartedAt datetime(6) NULL,
  CompletedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_worker_command_log_WorkerId_InstanceId_CreateTime (WorkerId, InstanceId, CreateTime),
  INDEX IX_worker_command_log_CommandId_CreateTime (CommandId, CreateTime)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS worker_run (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  SessionId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  CommandId varchar(64) CHARACTER SET utf8mb4 NULL,
  RunType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  TargetType varchar(32) CHARACTER SET utf8mb4 NULL,
  TargetId varchar(64) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  WorkspacePath varchar(512) CHARACTER SET utf8mb4 NULL,
  PromptPath varchar(512) CHARACTER SET utf8mb4 NULL,
  StdoutPath varchar(512) CHARACTER SET utf8mb4 NULL,
  StderrPath varchar(512) CHARACTER SET utf8mb4 NULL,
  FinalPath varchar(512) CHARACTER SET utf8mb4 NULL,
  ManifestPath varchar(512) CHARACTER SET utf8mb4 NULL,
  ExitCode int NULL,
  TimedOut tinyint(1) NOT NULL,
  Error varchar(1024) CHARACTER SET utf8mb4 NULL,
  StartedAt datetime(6) NOT NULL,
  CompletedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_worker_run_WorkerId_SessionId (WorkerId, SessionId),
  INDEX IX_worker_run_TargetType_TargetId (TargetType, TargetId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS worker_event (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  SessionId varchar(64) CHARACTER SET utf8mb4 NULL,
  RunId varchar(64) CHARACTER SET utf8mb4 NULL,
  EventType varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Level varchar(16) CHARACTER SET utf8mb4 NOT NULL,
  Message varchar(1024) CHARACTER SET utf8mb4 NOT NULL,
  PayloadJson text CHARACTER SET utf8mb4 NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_worker_event_WorkerId_CreateTime (WorkerId, CreateTime)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS code_audit_task (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  GitRepositoryId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Branch varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  WorkerId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  AuditTargetType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  TargetId varchar(64) CHARACTER SET utf8mb4 NULL,
  SourceTaskId varchar(64) CHARACTER SET utf8mb4 NULL,
  SourceCommandId varchar(64) CHARACTER SET utf8mb4 NULL,
  AuditCommandId varchar(64) CHARACTER SET utf8mb4 NULL,
  SourceRunId varchar(64) CHARACTER SET utf8mb4 NULL,
  SourceGitCommitId varchar(64) CHARACTER SET utf8mb4 NULL,
  BaseCommitId varchar(64) CHARACTER SET utf8mb4 NULL,
  HeadCommitId varchar(64) CHARACTER SET utf8mb4 NULL,
  CurrentBranchHeadCommitId varchar(64) CHARACTER SET utf8mb4 NULL,
  RequirementId varchar(64) CHARACTER SET utf8mb4 NULL,
  ModuleId varchar(64) CHARACTER SET utf8mb4 NULL,
  ScopeJson text CHARACTER SET utf8mb4 NULL,
  SelectedSkillIds varchar(1024) CHARACTER SET utf8mb4 NULL,
  Instruction varchar(2048) CHARACTER SET utf8mb4 NULL,
  Status varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  Conclusion varchar(32) CHARACTER SET utf8mb4 NULL,
  WorkspaceDirtyReason varchar(1024) CHARACTER SET utf8mb4 NULL,
  CreatedBy varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  StartedAt datetime(6) NULL,
  CompletedAt datetime(6) NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_code_audit_task_Project_Status (ProjectId, Status),
  INDEX IX_code_audit_task_Worker_Status (WorkerId, Status),
  INDEX IX_code_audit_task_AuditCommandId (AuditCommandId),
  INDEX IX_code_audit_task_Target (AuditTargetType, TargetId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS code_audit_result (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  AuditTaskId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  WorkerCommandId varchar(64) CHARACTER SET utf8mb4 NULL,
  WorkerRunId varchar(64) CHARACTER SET utf8mb4 NULL,
  GitCommitId varchar(64) CHARACTER SET utf8mb4 NULL,
  Branch varchar(128) CHARACTER SET utf8mb4 NULL,
  ChangedFilesJson text CHARACTER SET utf8mb4 NULL,
  PromptSnapshot longtext CHARACTER SET utf8mb4 NULL,
  SkillContextSnapshot longtext CHARACTER SET utf8mb4 NULL,
  RawResult longtext CHARACTER SET utf8mb4 NULL,
  StructuredResultJson longtext CHARACTER SET utf8mb4 NULL,
  Conclusion varchar(32) CHARACTER SET utf8mb4 NULL,
  IssuesJson longtext CHARACTER SET utf8mb4 NULL,
  AnnotationIssuesJson longtext CHARACTER SET utf8mb4 NULL,
  ManualCheckItemsJson longtext CHARACTER SET utf8mb4 NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  INDEX IX_code_audit_result_AuditTaskId (AuditTaskId),
  INDEX IX_code_audit_result_WorkerCommandId (WorkerCommandId)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS code_audit_file (
  Id varchar(255) CHARACTER SET utf8mb4 NOT NULL,
  ProjectId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  GitRepositoryId varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  Branch varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  FileType varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  FilePath varchar(1024) CHARACTER SET utf8mb4 NOT NULL,
  FilePathHash varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  FileContentHash varchar(64) CHARACTER SET utf8mb4 NULL,
  AuditStatus varchar(32) CHARACTER SET utf8mb4 NOT NULL,
  LastAuditTaskId varchar(64) CHARACTER SET utf8mb4 NULL,
  LastAuditResultId varchar(64) CHARACTER SET utf8mb4 NULL,
  LastAuditAt datetime(6) NULL,
  LastCommitId varchar(64) CHARACTER SET utf8mb4 NULL,
  IssueCount int NOT NULL,
  BlockingIssueCount int NOT NULL,
  HighIssueCount int NOT NULL,
  MediumIssueCount int NOT NULL,
  LowIssueCount int NOT NULL,
  Summary varchar(1024) CHARACTER SET utf8mb4 NULL,
  CreateTime datetime(6) NOT NULL,
  UpdateTime datetime(6) NULL,
  IsDelete int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE INDEX IX_code_audit_file_Project_Repo_Branch_PathHash (ProjectId, GitRepositoryId, Branch, FilePathHash),
  INDEX IX_code_audit_file_Project_Status (ProjectId, AuditStatus),
  INDEX IX_code_audit_file_Project_FileType (ProjectId, FileType),
  INDEX IX_code_audit_file_Repo_Branch (GitRepositoryId, Branch),
  INDEX IX_code_audit_file_LastAuditAt (LastAuditAt)
) CHARACTER SET=utf8mb4;

CALL agentsprint_add_column_if_not_exists('worker_command', 'Title', 'ALTER TABLE worker_command ADD COLUMN Title varchar(256) CHARACTER SET utf8mb4 NOT NULL DEFAULT '''' AFTER CommandType;');

CALL agentsprint_add_column_if_not_exists('worker_command', 'ChangedFilesJson', 'ALTER TABLE worker_command ADD COLUMN ChangedFilesJson text CHARACTER SET utf8mb4 NULL AFTER ResultJson;');

CALL agentsprint_add_column_if_not_exists('worker_command', 'GitCommitId', 'ALTER TABLE worker_command ADD COLUMN GitCommitId varchar(64) CHARACTER SET utf8mb4 NULL AFTER ChangedFilesJson;');

CALL agentsprint_add_column_if_not_exists('code_audit_task', 'AuditCommandId', 'ALTER TABLE code_audit_task ADD COLUMN AuditCommandId varchar(64) CHARACTER SET utf8mb4 NULL AFTER SourceCommandId;');

CALL agentsprint_create_index_if_not_exists('code_audit_task', 'IX_code_audit_task_AuditCommandId', 'CREATE INDEX IX_code_audit_task_AuditCommandId ON code_audit_task (AuditCommandId);');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'EmployeeType', 'ALTER TABLE digital_worker ADD COLUMN EmployeeType varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''development'' AFTER SkillIds;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'PollIntervalSeconds', 'ALTER TABLE digital_worker ADD COLUMN PollIntervalSeconds int NOT NULL DEFAULT 15 AFTER HeartbeatTimeoutSeconds;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'IdleMaxIntervalSeconds', 'ALTER TABLE digital_worker ADD COLUMN IdleMaxIntervalSeconds int NOT NULL DEFAULT 180 AFTER PollIntervalSeconds;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'MaxRunMinutes', 'ALTER TABLE digital_worker ADD COLUMN MaxRunMinutes int NOT NULL DEFAULT 60 AFTER IdleMaxIntervalSeconds;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'WorkspaceRoot', 'ALTER TABLE digital_worker ADD COLUMN WorkspaceRoot varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''/workspaces'' AFTER MaxRunMinutes;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'RunsRoot', 'ALTER TABLE digital_worker ADD COLUMN RunsRoot varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''/runs'' AFTER WorkspaceRoot;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'CodexHome', 'ALTER TABLE digital_worker ADD COLUMN CodexHome varchar(512) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''/codex-home'' AFTER RunsRoot;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'SandboxMode', 'ALTER TABLE digital_worker ADD COLUMN SandboxMode varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''workspace-write'' AFTER CodexHome;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'RunSmokeOnStartup', 'ALTER TABLE digital_worker ADD COLUMN RunSmokeOnStartup tinyint(1) NOT NULL DEFAULT 0 AFTER SandboxMode;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'SmokePrompt', 'ALTER TABLE digital_worker ADD COLUMN SmokePrompt varchar(1024) CHARACTER SET utf8mb4 NULL AFTER RunSmokeOnStartup;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'AiPlatformCode', 'ALTER TABLE digital_worker ADD COLUMN AiPlatformCode varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''openai'' AFTER SmokePrompt;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'CodexProvider', 'ALTER TABLE digital_worker ADD COLUMN CodexProvider varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''openai'' AFTER SmokePrompt;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'CodexModel', 'ALTER TABLE digital_worker ADD COLUMN CodexModel varchar(128) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''gpt-5.4'' AFTER CodexProvider;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'OpenAiBaseUrl', 'ALTER TABLE digital_worker ADD COLUMN OpenAiBaseUrl varchar(512) CHARACTER SET utf8mb4 NULL AFTER CodexModel;');

CALL agentsprint_add_column_if_not_exists('digital_worker', 'ConfigVersion', 'ALTER TABLE digital_worker ADD COLUMN ConfigVersion int NOT NULL DEFAULT 1 AFTER OpenAiBaseUrl;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'Description', 'ALTER TABLE sprint_project ADD COLUMN Description varchar(2048) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'FrontendTechStack', 'ALTER TABLE sprint_project ADD COLUMN FrontendTechStack varchar(512) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'BackendTechStack', 'ALTER TABLE sprint_project ADD COLUMN BackendTechStack varchar(512) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'ProjectManagerId', 'ALTER TABLE sprint_project ADD COLUMN ProjectManagerId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'ProductManagerIds', 'ALTER TABLE sprint_project ADD COLUMN ProductManagerIds varchar(512) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'DeveloperIds', 'ALTER TABLE sprint_project ADD COLUMN DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'TesterIds', 'ALTER TABLE sprint_project ADD COLUMN TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'ArchitectId', 'ALTER TABLE sprint_project ADD COLUMN ArchitectId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'TestEnvironmentId', 'ALTER TABLE sprint_project ADD COLUMN TestEnvironmentId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project', 'AiPlatformCode', 'ALTER TABLE sprint_project ADD COLUMN AiPlatformCode varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''openai'' AFTER TestEnvironmentId;');

UPDATE sprint_project
SET AiPlatformCode = 'openai'
WHERE AiPlatformCode IS NULL OR AiPlatformCode = '';

UPDATE sys_configuration
SET `Value` = JSON_SET(`Value`, '$.provider', 'openai')
WHERE `Key` LIKE 'AiPlatform:%'
  AND JSON_UNQUOTE(JSON_EXTRACT(`Value`, '$.openAiBaseUrl')) IS NOT NULL
  AND JSON_UNQUOTE(JSON_EXTRACT(`Value`, '$.openAiBaseUrl')) <> ''
  AND LOWER(JSON_UNQUOTE(JSON_EXTRACT(`Value`, '$.provider'))) <> 'openai';

CALL agentsprint_add_column_if_not_exists('sprint_project', 'SkillIds', 'ALTER TABLE sprint_project ADD COLUMN SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_skill', 'Type', 'ALTER TABLE sprint_skill ADD COLUMN Type varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''development'';');

CALL agentsprint_add_column_if_not_exists('sys_runtime_environment', 'ServerIps', 'ALTER TABLE sys_runtime_environment ADD COLUMN ServerIps varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sys_runtime_environment_container', 'ContainerType', 'ALTER TABLE sys_runtime_environment_container ADD COLUMN ContainerType int NOT NULL DEFAULT 0;');

CALL agentsprint_add_column_if_not_exists('sys_runtime_environment_container', 'ServerIp', 'ALTER TABLE sys_runtime_environment_container ADD COLUMN ServerIp varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sys_runtime_environment_container', 'Prompt', 'ALTER TABLE sys_runtime_environment_container ADD COLUMN Prompt text CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sys_runtime_environment_container', 'DeployScript', 'ALTER TABLE sys_runtime_environment_container ADD COLUMN DeployScript text CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project_endpoint', 'OwnerId', 'ALTER TABLE sprint_project_endpoint ADD COLUMN OwnerId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project_endpoint', 'DeveloperIds', 'ALTER TABLE sprint_project_endpoint ADD COLUMN DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project_endpoint', 'TesterIds', 'ALTER TABLE sprint_project_endpoint ADD COLUMN TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_project_endpoint', 'SkillIds', 'ALTER TABLE sprint_project_endpoint ADD COLUMN SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_feature_module', 'OwnerId', 'ALTER TABLE sprint_feature_module ADD COLUMN OwnerId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_feature_module', 'DeveloperIds', 'ALTER TABLE sprint_feature_module ADD COLUMN DeveloperIds varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_feature_module', 'TesterIds', 'ALTER TABLE sprint_feature_module ADD COLUMN TesterIds varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_requirement', 'Stakeholders', 'ALTER TABLE sprint_requirement ADD COLUMN Stakeholders varchar(512) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_requirement', 'EndpointId', 'ALTER TABLE sprint_requirement ADD COLUMN EndpointId varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT '''';');

CALL agentsprint_add_column_if_not_exists('sprint_requirement', 'ModuleId', 'ALTER TABLE sprint_requirement ADD COLUMN ModuleId varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT '''';');

CALL agentsprint_add_column_if_not_exists('sprint_requirement', 'SubmittedAt', 'ALTER TABLE sprint_requirement ADD COLUMN SubmittedAt datetime(6) NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_requirement', 'VoidedAt', 'ALTER TABLE sprint_requirement ADD COLUMN VoidedAt datetime(6) NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_requirement', 'SourceRequirementId', 'ALTER TABLE sprint_requirement ADD COLUMN SourceRequirementId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_requirement', 'SourceFeedbackId', 'ALTER TABLE sprint_requirement ADD COLUMN SourceFeedbackId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_requirement', 'SkillIds', 'ALTER TABLE sprint_requirement ADD COLUMN SkillIds varchar(1024) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_development_task', 'AssignedBy', 'ALTER TABLE sprint_development_task ADD COLUMN AssignedBy varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_development_task', 'AssigneeType', 'ALTER TABLE sprint_development_task ADD COLUMN AssigneeType int NOT NULL DEFAULT 0;');

ALTER TABLE sprint_development_task MODIFY COLUMN Prompt varchar(8192) CHARACTER SET utf8mb4 NULL;

CALL agentsprint_add_column_if_not_exists('sprint_feature_suggestion', 'ConvertedRequirementId', 'ALTER TABLE sprint_feature_suggestion ADD COLUMN ConvertedRequirementId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_feature_suggestion', 'ConvertedAt', 'ALTER TABLE sprint_feature_suggestion ADD COLUMN ConvertedAt datetime(6) NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_requirement_feedback', 'DevelopmentTaskId', 'ALTER TABLE sprint_requirement_feedback ADD COLUMN DevelopmentTaskId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_requirement_review', 'SubmitReason', 'ALTER TABLE sprint_requirement_review ADD COLUMN SubmitReason varchar(1024) CHARACTER SET utf8mb4 NULL AFTER Comment;');

CALL agentsprint_add_column_if_not_exists('test_plan', 'TesterId', 'ALTER TABLE test_plan ADD COLUMN TesterId varchar(64) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_add_column_if_not_exists('sprint_bug', 'Severity', 'ALTER TABLE sprint_bug ADD COLUMN Severity varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''major'';');

CALL agentsprint_add_column_if_not_exists('sprint_task_lease', 'ActiveTargetKey', 'ALTER TABLE sprint_task_lease ADD COLUMN ActiveTargetKey varchar(128) CHARACTER SET utf8mb4 NULL;');

CALL agentsprint_create_index_if_not_exists('sprint_task_lease', 'IX_sprint_task_lease_ActiveTargetKey', 'CREATE UNIQUE INDEX IX_sprint_task_lease_ActiveTargetKey ON sprint_task_lease (ActiveTargetKey);');

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
  'prompt-code-audit',
  'codex',
  'code_audit',
  '代码审计提示词',
  '你是 AgentSprint 的代码审计助手。本次任务只允许审计代码，不允许修改代码、不允许生成补丁、不允许提交代码、不允许执行会改变仓库状态的命令。

审计目标类型：
{{auditTargetType}}

任务ID：
{{taskId}}

需求ID：
{{requirementId}}

模块ID：
{{moduleId}}

审计范围：
{{scope}}

仓库：
{{repository}}

分支：
{{branch}}

已变更文件：
{{changedFiles}}

代码变更 Diff：
{{diff}}

代码上下文：
{{codeContext}}

审计 Skill：
{{skillContext}}

补充要求：
{{instruction}}

审计要求：
1. 只审计给定范围内的代码。如果范围来自任务ID，则重点审计该任务实际产生的 Git 变更。
2. 审计前提是工作区已经拉取最新代码；如果上下文显示代码不是最新状态，必须把它列为阻断问题。
3. 必须结合审计 Skill 中的项目规范、框架规范、注释规范、安全规范和验证规范进行判断；如果 Skill 与通用规则冲突，优先遵循 Skill。
4. 重点识别线上故障、安全风险、数据错误、兼容性破坏、并发问题、权限绕过、异常处理缺失、事务边界错误、接口契约破坏和可维护性显著下降的问题。
5. 必须检查注释质量。注释要求精确到类、方法、方法参数级别：
   - 类注释需要说明职责、边界、关键约束。
   - 方法注释需要说明用途、输入输出、异常、重要副作用。
   - 参数注释需要说明每个参数的含义、取值范围、空值规则、单位或格式。
   - 如果注释缺失、过期、误导或与实现不一致，需要指出具体类、方法、参数。
6. 不要输出泛泛的代码风格建议，除非它会造成明确缺陷或维护风险。
7. 不要建议直接改代码，只给审计结论、风险说明和修复方向。
8. 如果没有发现明确问题，直接说明未发现阻断性问题，并列出仍需人工确认的风险点。

输出格式：
## 审计结论
- 结论：通过 / 需修改 / 阻断
- 范围：简述本次实际审计范围

## 问题列表
按严重程度从高到低输出。每个问题包含：
- 严重程度：阻断 / 高 / 中 / 低
- 位置：文件、类、方法、参数，能精确就精确
- 问题：具体问题描述
- 影响：可能造成的后果
- 触发条件：什么情况下会发生
- 修复方向：只描述方向，不直接改代码

## 注释检查
列出类、方法、参数级注释问题；没有问题则说明注释检查未发现明确问题。

## 人工确认项
列出需要人工结合业务判断的点。',
  '变量：{{auditTargetType}}、{{taskId}}、{{requirementId}}、{{moduleId}}、{{scope}}、{{repository}}、{{branch}}、{{changedFiles}}、{{diff}}、{{codeContext}}、{{skillContext}}、{{instruction}}。',
  40,
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

DROP PROCEDURE IF EXISTS agentsprint_add_column_if_not_exists;
DROP PROCEDURE IF EXISTS agentsprint_create_index_if_not_exists;
