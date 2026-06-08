import { requestClient } from '#/api/request';

export namespace SystemApi {
  export interface Association {
    associationType: string;
    id: string;
    sourceEntityId: string;
    targetEntityId: string;
  }

  export interface User {
    avatar?: string;
    displayName: string;
    email?: string;
    id: string;
    phoneNumber?: string;
    roleIds: string[];
    status: number;
    username: string;
  }

  export interface Role {
    code: string;
    description?: string;
    id: string;
    menuIds: string[];
    name: string;
    permissionIds: string[];
    status: number;
  }

  export interface Menu {
    component?: string;
    icon?: string;
    id: string;
    name: string;
    parentId?: string;
    path: string;
    sort: number;
    status: number;
    type: number;
  }

  export interface Permission {
    code: string;
    id: string;
    menuId?: string;
    name: string;
  }

  export interface AgentToken {
    createTime: string;
    createdBy: string;
    createdByUsername: string;
    expiresAt: string;
    id: string;
    lastUsedAt?: string;
    maskedToken: string;
    name: string;
    ownerDisplayName: string;
    ownerUserId: string;
    ownerUsername: string;
    projectId?: string;
    revokedAt?: string;
    revokedBy?: string;
    status: number;
    token: string;
  }

  export interface CreatedAgentToken {
    id: string;
    metadata: AgentToken;
    token: string;
  }

  export interface Configuration {
    description?: string;
    id: string;
    key: string;
    status: number;
    value: string;
  }

  export interface CodeName {
    code: string;
    description?: string;
    id: string;
    name: string;
    status: number;
  }

  export interface Department {
    code: string;
    id: string;
    name: string;
    parentId?: string;
    sort: number;
    status: number;
  }

  export interface DictionaryType {
    code: string;
    description?: string;
    id: string;
    name: string;
    sort: number;
    status: number;
  }

  export interface DictionaryItem {
    code: string;
    description?: string;
    dictionaryTypeId: string;
    id: string;
    name: string;
    sort: number;
    status: number;
  }

  export interface RuntimeEnvironment {
    apiBaseUrl?: string;
    code: string;
    composeFilePath?: string;
    deployRoot?: string;
    description?: string;
    dockerDirectory?: string;
    endpointId?: string;
    environmentType: string;
    frontendProxyApiUrl?: string;
    frontendUrl?: string;
    id: string;
    localPackagePaths?: string;
    mcpEndpoint?: string;
    moduleId?: string;
    name: string;
    projectId?: string;
    remotePackagePath?: string;
    sort: number;
    status: number;
  }

  export interface RuntimeEnvironmentContainer {
    containerPort: number;
    description?: string;
    hostPort: number;
    id: string;
    name: string;
    protocol: string;
    runtimeEnvironmentId: string;
    sort: number;
    status: number;
  }

  export interface PromptTemplate {
    agentEnvironment: string;
    code: string;
    content: string;
    description?: string;
    id: string;
    name: string;
    sort: number;
    status: number;
  }
}

export function listSystemUsersApi() {
  return requestClient.get<SystemApi.User[]>('/system/users');
}

export function saveSystemUserApi(data: Partial<SystemApi.User> & { password?: string }) {
  return requestClient.post<SystemApi.User>('/system/users', data);
}

export function deleteSystemUserApi(id: string) {
  return requestClient.delete<boolean>(`/system/users/${id}`);
}

export function listSystemRolesApi() {
  return requestClient.get<SystemApi.Role[]>('/system/roles');
}

export function saveSystemRoleApi(data: Partial<SystemApi.Role>) {
  return requestClient.post<SystemApi.Role>('/system/roles', data);
}

export function deleteSystemRoleApi(id: string) {
  return requestClient.delete<boolean>(`/system/roles/${id}`);
}

export function listSystemMenusApi() {
  return requestClient.get<SystemApi.Menu[]>('/system/menus');
}

export function saveSystemMenuApi(data: Partial<SystemApi.Menu>) {
  return requestClient.post<SystemApi.Menu>('/system/menus', data);
}

export function deleteSystemMenuApi(id: string) {
  return requestClient.delete<boolean>(`/system/menus/${id}`);
}

export function listSystemPermissionsApi() {
  return requestClient.get<SystemApi.Permission[]>('/system/permissions');
}

export function saveSystemPermissionApi(data: Partial<SystemApi.Permission>) {
  return requestClient.post<SystemApi.Permission>('/system/permissions', data);
}

export function deleteSystemPermissionApi(id: string) {
  return requestClient.delete<boolean>(`/system/permissions/${id}`);
}

export function listAgentTokensApi() {
  return requestClient.get<SystemApi.AgentToken[]>('/system/agent-tokens');
}

export function createAgentTokenApi(data: {
  expiresAt: string;
  name: string;
  ownerUserId?: string;
  projectId?: string;
}) {
  return requestClient.post<SystemApi.CreatedAgentToken>('/system/agent-tokens', data);
}

export function revokeAgentTokenApi(id: string) {
  return requestClient.delete<boolean>(`/system/agent-tokens/${id}`);
}

export function listSystemConfigurationsApi() {
  return requestClient.get<SystemApi.Configuration[]>('/system/configurations');
}

export function saveSystemConfigurationApi(data: Partial<SystemApi.Configuration>) {
  return requestClient.post<SystemApi.Configuration>('/system/configurations', data);
}

export function deleteSystemConfigurationApi(id: string) {
  return requestClient.delete<boolean>(`/system/configurations/${id}`);
}

export function listUserGroupsApi() {
  return requestClient.get<SystemApi.CodeName[]>('/system/user-groups');
}

export function saveUserGroupApi(data: Partial<SystemApi.CodeName>) {
  return requestClient.post<SystemApi.CodeName>('/system/user-groups', data);
}

export function deleteUserGroupApi(id: string) {
  return requestClient.delete<boolean>(`/system/user-groups/${id}`);
}

export function listRoleGroupsApi() {
  return requestClient.get<SystemApi.CodeName[]>('/system/role-groups');
}

export function saveRoleGroupApi(data: Partial<SystemApi.CodeName>) {
  return requestClient.post<SystemApi.CodeName>('/system/role-groups', data);
}

export function deleteRoleGroupApi(id: string) {
  return requestClient.delete<boolean>(`/system/role-groups/${id}`);
}

export function listDepartmentsApi() {
  return requestClient.get<SystemApi.Department[]>('/system/departments');
}

export function saveDepartmentApi(data: Partial<SystemApi.Department>) {
  return requestClient.post<SystemApi.Department>('/system/departments', data);
}

export function deleteDepartmentApi(id: string) {
  return requestClient.delete<boolean>(`/system/departments/${id}`);
}

export function listAssignmentsApi() {
  return requestClient.get<SystemApi.CodeName[]>('/system/assignments');
}

export function saveAssignmentApi(data: Partial<SystemApi.CodeName>) {
  return requestClient.post<SystemApi.CodeName>('/system/assignments', data);
}

export function deleteAssignmentApi(id: string) {
  return requestClient.delete<boolean>(`/system/assignments/${id}`);
}

export function listDictionaryTypesApi() {
  return requestClient.get<SystemApi.DictionaryType[]>('/system/dictionary-types');
}

export function saveDictionaryTypeApi(data: Partial<SystemApi.DictionaryType>) {
  return requestClient.post<SystemApi.DictionaryType>('/system/dictionary-types', data);
}

export function deleteDictionaryTypeApi(id: string) {
  return requestClient.delete<boolean>(`/system/dictionary-types/${id}`);
}

export function listDictionaryItemsApi(dictionaryTypeId?: string) {
  return requestClient.get<SystemApi.DictionaryItem[]>('/system/dictionary-items', {
    params: dictionaryTypeId ? { dictionaryTypeId } : undefined,
  });
}

export function saveDictionaryItemApi(data: Partial<SystemApi.DictionaryItem>) {
  return requestClient.post<SystemApi.DictionaryItem>('/system/dictionary-items', data);
}

export function deleteDictionaryItemApi(id: string) {
  return requestClient.delete<boolean>(`/system/dictionary-items/${id}`);
}

export function listRuntimeEnvironmentsApi(params?: {
  endpointId?: string;
  moduleId?: string;
  projectId?: string;
}) {
  return requestClient.get<SystemApi.RuntimeEnvironment[]>('/system/runtime-environments', {
    params,
  });
}

export function saveRuntimeEnvironmentApi(data: Partial<SystemApi.RuntimeEnvironment>) {
  return requestClient.post<SystemApi.RuntimeEnvironment>('/system/runtime-environments', data);
}

export function deleteRuntimeEnvironmentApi(id: string) {
  return requestClient.delete<boolean>(`/system/runtime-environments/${id}`);
}

export function listRuntimeEnvironmentContainersApi(runtimeEnvironmentId: string) {
  return requestClient.get<SystemApi.RuntimeEnvironmentContainer[]>('/system/runtime-environment-containers', {
    params: { runtimeEnvironmentId },
  });
}

export function saveRuntimeEnvironmentContainerApi(data: Partial<SystemApi.RuntimeEnvironmentContainer>) {
  return requestClient.post<SystemApi.RuntimeEnvironmentContainer>('/system/runtime-environment-containers', data);
}

export function deleteRuntimeEnvironmentContainerApi(id: string) {
  return requestClient.delete<boolean>(`/system/runtime-environment-containers/${id}`);
}

export function listPromptTemplatesApi(agentEnvironment = 'codex') {
  return requestClient.get<SystemApi.PromptTemplate[]>('/system/prompt-templates', {
    params: { agentEnvironment },
  });
}

export function savePromptTemplateApi(data: Partial<SystemApi.PromptTemplate>) {
  return requestClient.post<SystemApi.PromptTemplate>('/system/prompt-templates', data);
}

export function deletePromptTemplateApi(id: string) {
  return requestClient.delete<boolean>(`/system/prompt-templates/${id}`);
}

export function listAssociationsApi() {
  return requestClient.get<SystemApi.Association[]>('/system/associations');
}

export function createAssociationApi(data: Omit<SystemApi.Association, 'id'>) {
  return requestClient.post<SystemApi.Association>('/system/associations', data);
}

export function deleteAssociationApi(id: string) {
  return requestClient.delete<boolean>(`/system/associations/${id}`);
}
