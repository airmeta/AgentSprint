import { requestClient } from '#/api/request';

export namespace SprintMvpApi {
  export interface CreateProjectRequest {
    architectId: string;
    backendTechStack: string;
    code: string;
    description?: string;
    developerIds: string[];
    frontendTechStack: string;
    name: string;
    productManagerIds: string[];
    projectManagerId: string;
    repositoryUrl?: string;
    testerIds: string[];
    testEnvironmentId?: string;
    testEnvironmentUrl?: string;
  }

  export interface UpdateProjectRequest {
    architectId: string;
    backendTechStack: string;
    description?: string;
    developerIds: string[];
    frontendTechStack: string;
    name: string;
    productManagerIds: string[];
    projectManagerId: string;
    repositoryUrl?: string;
    testerIds: string[];
    testEnvironmentId?: string;
    testEnvironmentUrl?: string;
  }

  export interface Project {
    architectId?: string;
    backendTechStack?: string;
    id: string;
    code: string;
    description?: string;
    developerIds: string[];
    frontendTechStack?: string;
    name: string;
    productManagerIds: string[];
    projectManagerId?: string;
    repositoryUrl?: string;
    testerIds: string[];
    testEnvironmentId?: string;
    testEnvironmentUrl?: string;
    status: string;
    createdBy: string;
    createTime: string;
  }

  export interface CreateRequirementRequest {
    description?: string;
    endpointId?: string;
    moduleId?: string;
    priority?: number;
    projectId: string;
    skillIds?: string[];
    stakeholders?: string;
    title: string;
  }

  export interface UpdateRequirementRequest {
    description?: string;
    priority?: number;
    skillIds?: string[];
    stakeholders?: string;
    title: string;
  }

  export interface CreateSkillRequest {
    code: string;
    content: string;
    description?: string;
    name: string;
  }

  export interface UpdateSkillRequest {
    content: string;
    description?: string;
    name: string;
    status?: string;
  }

  export interface Skill {
    code: string;
    content: string;
    createTime: string;
    createdBy: string;
    description?: string;
    id: string;
    name: string;
    status: string;
  }

  export interface SubmitReviewRequest {
    reviewerIds: string[];
  }

  export interface DecideReviewRequest {
    comment?: string;
  }

  export interface DecomposeRequirementRequest {
    assignmentMode?: 'auto' | 'manual';
    instruction?: string;
    taskCount?: number;
  }

  export interface ProjectEndpoint {
    code: string;
    createTime: string;
    createdBy: string;
    developerIds: string[];
    id: string;
    name: string;
    ownerId?: string;
    projectId: string;
    skillIds: string[];
    sort: number;
    status: string;
    testerIds: string[];
    type: string;
  }

  export interface CreateProjectEndpointRequest {
    code: string;
    developerIds?: string[];
    name: string;
    ownerId?: string;
    projectId: string;
    skillIds?: string[];
    sort?: number;
    testerIds?: string[];
    type: string;
  }

  export interface UpdateProjectEndpointRequest {
    developerIds?: string[];
    name: string;
    ownerId?: string;
    skillIds?: string[];
    sort?: number;
    status?: string;
    testerIds?: string[];
    type: string;
  }

  export interface FeatureModule {
    code: string;
    createTime: string;
    createdBy: string;
    description?: string;
    developerIds: string[];
    endpointId: string;
    id: string;
    name: string;
    ownerId?: string;
    projectId: string;
    sort: number;
    status: string;
    testerIds: string[];
  }

  export interface CreateFeatureModuleRequest {
    code: string;
    description?: string;
    developerIds?: string[];
    endpointId: string;
    name: string;
    ownerId?: string;
    projectId: string;
    sort?: number;
    testerIds?: string[];
  }

  export interface UpdateFeatureModuleRequest {
    description?: string;
    developerIds?: string[];
    name: string;
    ownerId?: string;
    sort?: number;
    status?: string;
    testerIds?: string[];
  }

  export interface AssignTaskRequest {
    assigneeId: string;
  }

  export interface Requirement {
    approvedAt?: string;
    closedAt?: string;
    createTime: string;
    createdBy: string;
    description?: string;
    developerId?: string;
    developmentCompletedAt?: string;
    id: string;
    endpointId?: string;
    priority: number;
    moduleId?: string;
    projectId: string;
    reviewedBy?: string;
    skillIds: string[];
    status: string;
    stakeholders?: string;
    submittedAt?: string;
    testedAt?: string;
    testUrl?: string;
    health: 'primary' | 'success' | 'warn';
    sourceFeedbackId?: string;
    sourceRequirementId?: string;
    title: string;
    voidedAt?: string;
  }

  export interface RequirementReview {
    comment?: string;
    createTime: string;
    id: string;
    projectId: string;
    requirementId: string;
    reviewedAt?: string;
    reviewerId: string;
    status: string;
  }

  export interface RequirementReviewItem {
    project: Project;
    requirement: Requirement;
    reviews: RequirementReview[];
  }

  export interface DevelopmentTask {
    assignedAt?: string;
    assignedBy?: string;
    assigneeId?: string;
    completedAt?: string;
    createTime: string;
    createdBy: string;
    description?: string;
    id: string;
    priority: number;
    projectId: string;
    prompt?: string;
    requirementId: string;
    startedAt?: string;
    status: string;
    title: string;
    updateTime?: string;
  }

  export interface TaskPromptSection {
    content: string;
    notes: string[];
    title: string;
    usage: string[];
  }

  export interface TaskPrompt {
    mcpSetupPrompt: TaskPromptSection;
    prompt: string;
    taskExecutionPrompt: TaskPromptSection;
    taskId: string;
  }

  export interface ClaimTaskRequest {
    ownerDevice?: string;
  }

  export interface CreateFeedbackRequest {
    content?: string;
    developmentTaskId?: string;
    title: string;
  }

  export interface ConvertFeedbackRequest {
    description?: string;
    priority?: number;
    remark?: string;
    stakeholders?: string;
    title: string;
  }

  export interface ConvertRequirementSourcesRequest {
    description?: string;
    feedbackIds?: string[];
    priority?: number;
    remark?: string;
    stakeholders?: string;
    suggestionIds?: string[];
    title: string;
  }

  export interface RequirementFeedback {
    closedAt?: string;
    content?: string;
    convertedAt?: string;
    convertedRequirementId?: string;
    createTime: string;
    createdBy: string;
    developmentTaskId?: string;
    id: string;
    projectId: string;
    requirementId: string;
    status: string;
    title: string;
  }

  export interface CompleteDevelopmentRequest {
    testUrl?: string;
  }

  export interface CreateBugRequest {
    description?: string;
    environment?: string;
    projectId: string;
    requirementId: string;
    severity?: string;
    testExecutionId?: string;
    testPlanId?: string;
    title: string;
  }

  export interface CreateFeatureSuggestionRequest {
    content: string;
    endpointId?: string;
    moduleId?: string;
    projectId: string;
    requirementId?: string;
  }

  export interface FeatureSuggestion {
    content: string;
    convertedAt?: string;
    convertedRequirementId?: string;
    createTime: string;
    createdBy: string;
    endpointId?: string;
    id: string;
    moduleId?: string;
    projectId: string;
    requirementId?: string;
    status: string;
  }

  export interface Bug {
    createTime: string;
    createdBy: string;
    description?: string;
    developerId?: string;
    environment: string;
    fixedAt?: string;
    id: string;
    projectId: string;
    requirementId: string;
    severity: string;
    status: string;
    testExecutionId?: string;
    testPlanId?: string;
    title: string;
  }

  export interface Lease {
    completedAt?: string;
    createTime: string;
    expiresAt: string;
    id: string;
    leaseToken: string;
    ownerDevice?: string;
    ownerId: string;
    projectId: string;
    status: string;
    targetId: string;
    targetType: string;
  }

  export interface Summary {
    activeLeaseCount: number;
    completedRequirementCount: number;
    developingRequirementCount: number;
    openBugCount: number;
    projectCount: number;
    readyRequirementCount: number;
    readyTestRequirementCount: number;
    requirementCount: number;
    testingRequirementCount: number;
  }
}

export namespace SprintTestApi {
  export interface CreatePlanRequest {
    bugId?: string;
    environment?: string;
    name: string;
    projectId: string;
    requirementId: string;
    testUrl?: string;
  }

  export interface CompletePlanRequest {
    status: string;
    summary?: string;
  }

  export interface SubmitExecutionRequest {
    actualResult?: string;
    bugId?: string;
    createdBugId?: string;
    evidence?: string;
    result: string;
  }

  export interface UpdateExecutionBugRequest {
    bugId?: string;
    createdBugId?: string;
  }

  export interface TestPlan {
    bugId?: string;
    completedAt?: string;
    createTime: string;
    createdBy: string;
    environment: string;
    id: string;
    name: string;
    projectId: string;
    requirementId: string;
    startedAt?: string;
    status: string;
    summary?: string;
    testerId?: string;
    testUrl?: string;
  }

  export interface TestExecution {
    actualResult?: string;
    bugId?: string;
    createTime: string;
    createdBugId?: string;
    evidence?: string;
    executedAt: string;
    id: string;
    requirementId: string;
    result: string;
    testerId: string;
    testPlanId: string;
  }
}

export namespace SprintUserApi {
  export interface UserOption {
    displayName: string;
    id: string;
    username: string;
  }
}

export function createProjectApi(data: SprintMvpApi.CreateProjectRequest) {
  return requestClient.post<SprintMvpApi.Project>('/mvp/projects', data);
}

export function listProjectsApi() {
  return requestClient.get<SprintMvpApi.Project[]>('/mvp/projects');
}

export function updateProjectApi(
  id: string,
  data: SprintMvpApi.UpdateProjectRequest,
) {
  return requestClient.put<SprintMvpApi.Project>(`/mvp/projects/${id}`, data);
}

export function createSkillApi(data: SprintMvpApi.CreateSkillRequest) {
  return requestClient.post<SprintMvpApi.Skill>('/mvp/skills', data);
}

export function listSkillsApi(activeOnly = false) {
  return requestClient.get<SprintMvpApi.Skill[]>('/mvp/skills', {
    params: { activeOnly },
  });
}

export function updateSkillApi(
  id: string,
  data: SprintMvpApi.UpdateSkillRequest,
) {
  return requestClient.put<SprintMvpApi.Skill>(`/mvp/skills/${id}`, data);
}

export function createProjectEndpointApi(
  data: SprintMvpApi.CreateProjectEndpointRequest,
) {
  return requestClient.post<SprintMvpApi.ProjectEndpoint>('/mvp/endpoints', data);
}

export function listProjectEndpointsApi(projectId?: string) {
  return requestClient.get<SprintMvpApi.ProjectEndpoint[]>('/mvp/endpoints', {
    params: { projectId },
  });
}

export function updateProjectEndpointApi(
  id: string,
  data: SprintMvpApi.UpdateProjectEndpointRequest,
) {
  return requestClient.put<SprintMvpApi.ProjectEndpoint>(
    `/mvp/endpoints/${id}`,
    data,
  );
}

export function createFeatureModuleApi(
  data: SprintMvpApi.CreateFeatureModuleRequest,
) {
  return requestClient.post<SprintMvpApi.FeatureModule>('/mvp/modules', data);
}

export function listFeatureModulesApi(projectId?: string, endpointId?: string) {
  return requestClient.get<SprintMvpApi.FeatureModule[]>('/mvp/modules', {
    params: { projectId, endpointId },
  });
}

export function updateFeatureModuleApi(
  id: string,
  data: SprintMvpApi.UpdateFeatureModuleRequest,
) {
  return requestClient.put<SprintMvpApi.FeatureModule>(
    `/mvp/modules/${id}`,
    data,
  );
}

export function createRequirementApi(
  data: SprintMvpApi.CreateRequirementRequest,
) {
  return requestClient.post<SprintMvpApi.Requirement>(
    '/mvp/requirements',
    data,
  );
}

export function listRequirementsApi(projectId?: string) {
  return requestClient.get<SprintMvpApi.Requirement[]>('/mvp/requirements', {
    params: { projectId },
  });
}

export function updateRequirementApi(
  id: string,
  data: SprintMvpApi.UpdateRequirementRequest,
) {
  return requestClient.put<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}`,
    data,
  );
}

export function submitRequirementReviewApi(
  id: string,
  data: SprintMvpApi.SubmitReviewRequest,
) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/submit-review`,
    data,
  );
}

export function listMyPendingReviewsApi() {
  return requestClient.get<SprintMvpApi.RequirementReviewItem[]>(
    '/mvp/reviews/my-pending',
  );
}

export function listRequirementReviewsApi(id: string) {
  return requestClient.get<SprintMvpApi.RequirementReview[]>(
    `/mvp/requirements/${id}/reviews`,
  );
}

export function createRequirementFeedbackApi(
  id: string,
  data: SprintMvpApi.CreateFeedbackRequest,
) {
  return requestClient.post<SprintMvpApi.RequirementFeedback>(
    `/mvp/requirements/${id}/feedback`,
    data,
  );
}

export function listRequirementFeedbackApi(id: string) {
  return requestClient.get<SprintMvpApi.RequirementFeedback[]>(
    `/mvp/requirements/${id}/feedback`,
  );
}

export function convertRequirementFeedbackApi(
  id: string,
  feedbackId: string,
  data: SprintMvpApi.ConvertFeedbackRequest,
) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/feedback/${feedbackId}/convert`,
    data,
  );
}

export function convertRequirementSourcesApi(
  id: string,
  data: SprintMvpApi.ConvertRequirementSourcesRequest,
) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/convert-sources`,
    data,
  );
}

export function createFeatureSuggestionApi(
  data: SprintMvpApi.CreateFeatureSuggestionRequest,
) {
  return requestClient.post<SprintMvpApi.FeatureSuggestion>(
    '/mvp/suggestions',
    data,
  );
}

export function listFeatureSuggestionsApi(params?: {
  moduleId?: string;
  projectId?: string;
  requirementId?: string;
}) {
  return requestClient.get<SprintMvpApi.FeatureSuggestion[]>('/mvp/suggestions', {
    params,
  });
}

export function approveRequirementReviewApi(
  id: string,
  data: SprintMvpApi.DecideReviewRequest,
) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/review/approve`,
    data,
  );
}

export function rejectRequirementReviewApi(
  id: string,
  data: SprintMvpApi.DecideReviewRequest,
) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/review/reject`,
    data,
  );
}

export function approveRequirementApi(id: string) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/approve`,
  );
}

export function voidRequirementApi(id: string) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/void`,
  );
}

export function deleteDraftRequirementApi(id: string) {
  return requestClient.delete<boolean>(`/mvp/requirements/${id}`);
}

export function decomposeRequirementApi(
  id: string,
  data: SprintMvpApi.DecomposeRequirementRequest,
) {
  return requestClient.post<SprintMvpApi.DevelopmentTask[]>(
    `/mvp/requirements/${id}/decompose`,
    data,
  );
}

export function listDevelopmentTasksApi(params?: {
  assigneeId?: string;
  projectId?: string;
  requirementId?: string;
  status?: string;
}) {
  return requestClient.get<SprintMvpApi.DevelopmentTask[]>('/mvp/tasks', {
    params,
  });
}

export function listMyDevelopmentTasksApi() {
  return requestClient.get<SprintMvpApi.DevelopmentTask[]>('/mvp/tasks/my');
}

export function assignDevelopmentTaskApi(
  id: string,
  data: SprintMvpApi.AssignTaskRequest,
) {
  return requestClient.post<SprintMvpApi.DevelopmentTask>(
    `/mvp/tasks/${id}/assign`,
    data,
  );
}

export function getDevelopmentTaskPromptApi(id: string) {
  return requestClient.get<SprintMvpApi.TaskPrompt>(`/mvp/tasks/${id}/prompt`);
}

export function completeDevelopmentTaskApi(id: string) {
  return requestClient.post<SprintMvpApi.DevelopmentTask>(`/mvp/tasks/${id}/complete`);
}

export function claimRequirementApi(
  id: string,
  data: SprintMvpApi.ClaimTaskRequest,
) {
  return requestClient.post<SprintMvpApi.Lease>(
    `/mvp/requirements/${id}/claim`,
    data,
  );
}

export function completeRequirementDevelopmentApi(
  id: string,
  data: SprintMvpApi.CompleteDevelopmentRequest,
) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/complete-development`,
    data,
  );
}

export function startRequirementTestingApi(id: string) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/start-testing`,
  );
}

export function markRequirementTestedApi(id: string) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/mark-tested`,
  );
}

export function closeRequirementApi(id: string) {
  return requestClient.post<SprintMvpApi.Requirement>(
    `/mvp/requirements/${id}/close`,
  );
}

export function createBugApi(data: SprintMvpApi.CreateBugRequest) {
  return requestClient.post<SprintMvpApi.Bug>('/mvp/bugs', data);
}

export function listBugsApi(projectId?: string, requirementId?: string) {
  return requestClient.get<SprintMvpApi.Bug[]>('/mvp/bugs', {
    params: { projectId, requirementId },
  });
}

export function claimBugApi(id: string, data: SprintMvpApi.ClaimTaskRequest) {
  return requestClient.post<SprintMvpApi.Lease>(`/mvp/bugs/${id}/claim`, data);
}

export function fixBugApi(id: string) {
  return requestClient.post<SprintMvpApi.Bug>(`/mvp/bugs/${id}/fix`);
}

export function closeBugApi(id: string) {
  return requestClient.post<SprintMvpApi.Bug>(`/mvp/bugs/${id}/close`);
}

export function listActiveLeasesApi() {
  return requestClient.get<SprintMvpApi.Lease[]>('/mvp/leases/active');
}

export function getMvpSummaryApi() {
  return requestClient.get<SprintMvpApi.Summary>('/mvp/summary');
}

export function listUserOptionsApi() {
  return requestClient.get<SprintUserApi.UserOption[]>('/user/options');
}

export function createTestPlanApi(data: SprintTestApi.CreatePlanRequest) {
  return requestClient.post<SprintTestApi.TestPlan>('/test/plans', data);
}

export function listTestPlansApi(projectId?: string, requirementId?: string) {
  return requestClient.get<SprintTestApi.TestPlan[]>('/test/plans', {
    params: { projectId, requirementId },
  });
}

export function startTestPlanApi(id: string) {
  return requestClient.post<SprintTestApi.TestPlan>(`/test/plans/${id}/start`);
}

export function completeTestPlanApi(
  id: string,
  data: SprintTestApi.CompletePlanRequest,
) {
  return requestClient.post<SprintTestApi.TestPlan>(
    `/test/plans/${id}/complete`,
    data,
  );
}

export function submitTestExecutionApi(
  id: string,
  data: SprintTestApi.SubmitExecutionRequest,
) {
  return requestClient.post<SprintTestApi.TestExecution>(
    `/test/plans/${id}/executions`,
    data,
  );
}

export function updateTestExecutionBugApi(
  planId: string,
  executionId: string,
  data: SprintTestApi.UpdateExecutionBugRequest,
) {
  return requestClient.put<SprintTestApi.TestExecution>(
    `/test/plans/${planId}/executions/${executionId}/bug`,
    data,
  );
}

export function listTestExecutionsApi(id: string) {
  return requestClient.get<SprintTestApi.TestExecution[]>(
    `/test/plans/${id}/executions`,
  );
}
