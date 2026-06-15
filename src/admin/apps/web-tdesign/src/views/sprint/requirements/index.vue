<script lang="ts" setup>
import type { SprintMvpApi, SprintUserApi } from '#/api/sprint/mvp';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';
import type { PrimaryTableCol } from 'tdesign-vue-next';

import { computed, onActivated, onMounted, reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { IconifyIcon } from '@vben/icons';

import {
  Button as TButton,
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  Link as TLink,
  MessagePlugin,
  Select as TSelect,
  Space as TSpace,
  Switch as TSwitch,
  Table as TTable,
  Tag as TTag,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

import {
  closeRequirementApi,
  completeRequirementDevelopmentApi,
  convertRequirementFeedbackApi,
  convertRequirementSourcesApi,
  createRequirementFeedbackApi,
  createRequirementApi,
  deleteDraftRequirementApi,
  decomposeRequirementApi,
  listDevelopmentTasksApi,
  listFeatureSuggestionsApi,
  listFeatureModulesApi,
  listProjectEndpointsApi,
  listRequirementFeedbackApi,
  listProjectsApi,
  listRequirementReviewsApi,
  listRequirementsApi,
  listSkillsApi,
  listUserOptionsApi,
  submitRequirementReviewApi,
  updateRequirementApi,
  voidRequirementApi,
} from '#/api/sprint/mvp';
import { listDigitalWorkersApi, type AutomationApi } from '#/api/automation/workers';
import {
  requiredArrayRule,
  requiredRule,
  validateForm,
} from '#/views/_shared/form-rules';
import { formatDateTime } from '#/views/_shared/date-format';
import { confirmAndClose } from '#/views/_shared/dialog-confirm';
import { withSerialColumn } from '#/views/_shared/table-columns';

import ProjectSecondaryListShell from '#/components/project-secondary-list-shell/project-secondary-list-shell.vue';
import MarkdownEditor from '../_shared/markdown-editor.vue';
import { renderMarkdown } from '../_shared/markdown';
import SkillSelectOption from '../_shared/skill-select-option.vue';
import '../_shared/table-layout.css';

const convertingFeedback = ref(false);
const decomposing = ref(false);
const feedbackSaving = ref(false);
const loading = ref(false);
const requirementSaving = ref(false);
const reviewSubmitting = ref(false);
const router = useRouter();
const selectedProjectId = ref('');
const selectedRequirement = ref<SprintMvpApi.Requirement>();
const selectedRequirementKeys = ref<Array<number | string>>([]);
const editorVisible = ref(false);
const requirementFormRef = ref<FormInstanceFunctions>();
const detailVisible = ref(false);
const reviewVisible = ref(false);
const reviewFormRef = ref<FormInstanceFunctions>();
const decomposeVisible = ref(false);
const feedbackVisible = ref(false);
const feedbackFormRef = ref<FormInstanceFunctions>();
const convertFeedbackVisible = ref(false);
const convertFeedbackFormRef = ref<FormInstanceFunctions>();
const projects = ref<SprintMvpApi.Project[]>([]);
const endpoints = ref<SprintMvpApi.ProjectEndpoint[]>([]);
const modules = ref<SprintMvpApi.FeatureModule[]>([]);
const skills = ref<SprintMvpApi.Skill[]>([]);
const requirements = ref<SprintMvpApi.Requirement[]>([]);
const developmentTasks = ref<SprintMvpApi.DevelopmentTask[]>([]);
const digitalWorkers = ref<AutomationApi.DigitalWorker[]>([]);
const requirementFeedback = ref<SprintMvpApi.RequirementFeedback[]>([]);
const requirementFeedbackMap = ref<Record<string, SprintMvpApi.RequirementFeedback[]>>({});
const requirementSuggestionMap = ref<Record<string, SprintMvpApi.FeatureSuggestion[]>>({});
const requirementReviews = ref<SprintMvpApi.RequirementReview[]>([]);
const users = ref<SprintUserApi.UserOption[]>([]);
const selectedFeedback = ref<SprintMvpApi.RequirementFeedback>();
const selectedFeedbackTaskId = ref('');
const expandedRequirementIds = ref<Array<number | string>>([]);

const filters = reactive({
  health: '',
  requirementInfo: '',
  status: '',
});
const requirementForm = reactive({
  description: '',
  endpointId: '',
  moduleId: '',
  priority: 3,
  projectId: '',
  requiresReview: true,
  skillIds: [] as string[],
  stakeholderIds: [] as string[],
  title: '',
});
const reviewForm = reactive({
  reviewerIds: [] as string[],
});
const decomposeForm = reactive({
  assigneeId: '',
  assigneeType: 0 as 0 | 1,
  assignmentMode: 'auto' as 'auto' | 'manual',
  instruction: '',
});
const feedbackForm = reactive({
  content: '',
  title: '',
});
const convertFeedbackForm = reactive({
  description: '',
  feedbackIds: [] as string[],
  priority: 3,
  remark: '',
  stakeholderIds: [] as string[],
  suggestionIds: [] as string[],
  title: '',
});
const requirementRules: FormRules<typeof requirementForm> = {
  endpointId: requiredRule('请选择端', 'change'),
  moduleId: requiredRule('璇烽€夋嫨鍔熻兘妯″潡', 'change'),
  priority: requiredRule('请选择优先级', 'change'),
  projectId: requiredRule('请选择所属项目', 'change'),
  title: requiredRule('请输入需求标题'),
};
const reviewRules: FormRules<typeof reviewForm> = {
  reviewerIds: requiredArrayRule('请选择评审人'),
};
const feedbackRules: FormRules<typeof feedbackForm> = {
  title: requiredRule('请输入反馈标题'),
};
const convertFeedbackRules: FormRules<typeof convertFeedbackForm> = {
  priority: requiredRule('请选择优先级', 'change'),
  title: requiredRule('请输入后续需求标题'),
};
const pagination = reactive({
  current: 1,
  pageSize: 30,
});

const projectOptions = computed(() =>
  projects.value.map((project) => ({
    label: `${project.code} 璺?${project.name}`,
    value: project.id,
  })),
);
const userOptions = computed(() =>
  users.value.map((user) => ({
    label: `${user.displayName} (${user.username})`,
    value: user.id,
  })),
);
const decomposeAssigneeOptions = computed(() => {
  if (decomposeForm.assigneeType === 1) {
    return digitalWorkers.value
      .filter((worker) => worker.status === 'active')
      .map((worker) => ({
        label: `${worker.name} (${worker.code})`,
        value: worker.agentUserId,
      }));
  }

  const developerIds = selectedRequirement.value
    ? resolveRequirementDeveloperIds(selectedRequirement.value)
    : [];
  const optionIds = developerIds.length > 0 ? developerIds : users.value.map((user) => user.id);

  return optionIds.map((id) => ({
    label: userMap.value[id]
      ? `${userMap.value[id].displayName} (${userMap.value[id].username})`
      : id,
    value: id,
  }));
});
const skillOptions = computed(() =>
  skills.value.map((skill) => ({
    label: `${skill.code} - ${skill.name}`,
    skill,
    value: skill.id,
  })),
);
const priorityOptions = [
  { label: '加急', value: 1 },
  { label: '姝ｅ父', value: 2 },
  { label: '可延后', value: 3 },
];
const userMap = computed(() => Object.fromEntries(users.value.map((item) => [item.id, item])));
const userNameMap = computed(() => Object.fromEntries(users.value.map((item) => [item.username, item])));
const endpointMap = computed(() =>
  Object.fromEntries(endpoints.value.map((endpoint) => [endpoint.id, endpoint])),
);
const endpointOptions = computed(() =>
  endpoints.value
    .filter((endpoint) => !requirementForm.projectId || endpoint.projectId === requirementForm.projectId)
    .map((endpoint) => ({
      label: endpoint.name,
      value: endpoint.id,
    })),
);
const moduleOptions = computed(() =>
  modules.value
    .filter(
      (module) =>
        (!requirementForm.projectId || module.projectId === requirementForm.projectId) &&
        (!requirementForm.endpointId || module.endpointId === requirementForm.endpointId),
    )
    .map((module) => ({
      label: module.name,
      value: module.id,
    })),
);
const selectedProjectName = computed(
  () => projects.value.find((item) => item.id === selectedProjectId.value)?.name,
);
const selectedProject = computed(() =>
  projects.value.find((item) => item.id === selectedProjectId.value),
);
const convertFeedbackOptions = computed(() =>
  (selectedRequirement.value ? getRequirementFeedback(selectedRequirement.value.id) : [])
    .filter((feedback) => feedback.status === 'open')
    .map((feedback) => ({ label: feedback.title, value: feedback.id })),
);
const convertSuggestionOptions = computed(() =>
  (selectedRequirement.value ? requirementSuggestionMap.value[selectedRequirement.value.id] || [] : [])
    .filter((suggestion) => suggestion.status === 'open')
    .map((suggestion) => ({
      label: suggestion.content.length > 48 ? `${suggestion.content.slice(0, 48)}...` : suggestion.content,
      value: suggestion.id,
    })),
);
const childRequirementsBySource = computed(() => {
  const map: Record<string, SprintMvpApi.Requirement[]> = {};
  for (const requirement of requirements.value) {
    if (!requirement.sourceRequirementId) continue;
    const children = map[requirement.sourceRequirementId] || [];
    children.push(requirement);
    map[requirement.sourceRequirementId] = children;
  }
  return map;
});
const developmentTasksByRequirement = computed(() => {
  const map: Record<string, SprintMvpApi.DevelopmentTask[]> = {};
  for (const task of developmentTasks.value) {
    const tasks = map[task.requirementId] || [];
    tasks.push(task);
    map[task.requirementId] = tasks;
  }

  for (const tasks of Object.values(map)) {
    tasks.sort(
      (left, right) =>
        left.priority - right.priority || right.createTime.localeCompare(left.createTime),
    );
  }

  return map;
});
const visibleRequirementIds = computed(() =>
  new Set(requirements.value.map((requirement) => requirement.id)),
);

const statusText: Record<string, string> = {
  approved: '待拆解',
  completed: '已完成',
  decomposed: '待推进',
  developing: '已推进',
  draft: '鑽夌',
  pending_fix: '待修复',
  pending_review: '待评审',
  ready_development: '待拆解',
  ready_test: '待测试',
  rejected: '璇勫椹冲洖',
  tested: '已测试',
  testing: '测试中',
  voided: '已作废',
};

const healthTheme: Record<string, 'default' | 'primary' | 'success' | 'warning'> = {
  primary: 'primary',
  success: 'success',
  voided: 'default',
  warn: 'warning',
};
const healthText: Record<string, string> = {
  primary: '推进中',
  success: '鍋ュ悍',
  voided: '已作废',
  warn: '有缺陷',
};
const reviewStatusText: Record<string, string> = {
  approved: '宸查€氳繃',
  pending: '待评审',
  rejected: '已驳回',
};
const feedbackStatusText: Record<string, string> = {
  closed: '已关闭',
  converted: '已转需求',
  open: '待处理',
};
const taskStatusText: Record<string, string> = {
  assigned: '已指派',
  completed: '已完成',
  in_progress: '推进中',
  pending_assign: '待指派',
};
const decomposeAllowedStatuses = new Set(['approved', 'ready_development', 'decomposed']);
const feedbackAllowedStatuses = new Set(['tested', 'completed']);
const editAllowedStatuses = new Set(['draft']);
const reviewAllowedStatuses = new Set(['draft', 'rejected']);

const columns: PrimaryTableCol[] = [
  { colKey: 'row-select', type: 'single', width: 48 },
  { colKey: 'title', ellipsis: true, title: '闇€姹傚悕' },
  { colKey: 'endpointId', title: '鎵€灞炵', width: 140 },
  { colKey: 'status', title: '状态', width: 120 },
  { colKey: 'health', title: '鍋ュ悍', width: 90 },
  { colKey: 'priority', title: '优先级', width: 90 },
  { colKey: 'createdBy', title: '浜у搧缁忕悊', width: 130 },
  { colKey: 'stakeholders', title: '干系人', width: 160 },
  { colKey: 'actions', title: '鎿嶄綔', width: 100 },
];
const statusOptions = computed(() =>
  Object.entries(statusText).map(([value, label]) => ({ label, value })),
);
const healthOptions = computed(() =>
  Object.entries(healthText).map(([value, label]) => ({ label, value })),
);
const selectedRequirementForAction = computed(() =>
  requirements.value.find((requirement) => requirement.id === selectedRequirementKeys.value[0]),
);
const canSubmitSelectedRequirement = computed(() =>
  Boolean(selectedRequirementForAction.value && canSubmitReview(selectedRequirementForAction.value)),
);
const canDecomposeSelectedRequirement = computed(() =>
  Boolean(
    selectedRequirementForAction.value &&
      decomposeAllowedStatuses.has(selectedRequirementForAction.value.status),
  ),
);
const canDeleteDraftSelectedRequirement = computed(() =>
  selectedRequirementForAction.value?.status === 'draft',
);
const canVoidSelectedRequirement = computed(() =>
  selectedRequirementForAction.value?.status === 'rejected',
);
const canCloseSelectedRequirement = computed(() =>
  selectedRequirementForAction.value?.status === 'tested',
);
const canCreateSelectedFeedback = computed(() =>
  Boolean(selectedRequirementForAction.value && canCreateFeedback(selectedRequirementForAction.value)),
);

const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  pageSizeOptions: [30, 50, 100, 200],
  showJumper: true,
  showPageSize: true,
  size: 'small' as const,
  total: requirements.value.length,
}));

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

function syncExpandedRequirementIds() {
  expandedRequirementIds.value = expandedRequirementIds.value.filter((key) => {
    const requirementId = String(key);
    return visibleRequirementIds.value.has(requirementId);
  });
}

function canCreateFeedback(requirement: SprintMvpApi.Requirement) {
  return feedbackAllowedStatuses.has(requirement.status) && !requirement.sourceFeedbackId;
}

function canEditRequirement(requirement: SprintMvpApi.Requirement) {
  return editAllowedStatuses.has(requirement.status);
}

function canSubmitReview(requirement: SprintMvpApi.Requirement) {
  return reviewAllowedStatuses.has(requirement.status);
}

function getRequirementFeedback(requirementId: string) {
  return requirementFeedbackMap.value[requirementId] || [];
}

function getRequirementTasks(requirementId: string) {
  return developmentTasksByRequirement.value[requirementId] || [];
}

function resolveRequirementDeveloperIds(requirement: SprintMvpApi.Requirement) {
  const moduleDevelopers = modules.value.find((module) => module.id === requirement.moduleId)?.developerIds || [];
  if (moduleDevelopers.length > 0) return [...new Set(moduleDevelopers)];

  const endpointDevelopers = endpoints.value.find((endpoint) => endpoint.id === requirement.endpointId)?.developerIds || [];
  if (endpointDevelopers.length > 0) return [...new Set(endpointDevelopers)];

  const projectDevelopers = projects.value.find((project) => project.id === requirement.projectId)?.developerIds || [];
  return [...new Set(projectDevelopers)];
}

function deserializeStakeholders(value?: string) {
  if (!value) return [];
  const userByUsername = Object.fromEntries(users.value.map((user) => [user.username, user.id]));
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
    .map((item) => userByUsername[item] || item)
    .filter((item, index, all) => all.indexOf(item) === index);
}

function serializeStakeholders(userIds: string[]) {
  const userById = userMap.value;
  return userIds
    .map((id) => userById[id]?.username || id)
    .filter(Boolean)
    .join(',');
}

function resolveStakeholderNames(value?: string) {
  const ids = deserializeStakeholders(value);
  if (ids.length === 0) return '未填写';
  return ids
    .map((id) => userMap.value[id]?.displayName || userMap.value[id]?.username || id)
    .join('、');
}

function resolveUserName(userId?: string) {
  if (!userId) return '未指定';
  const user = userMap.value[userId] || userNameMap.value[userId];
  return user?.displayName || user?.username || userId;
}

function resolveEndpointName(endpointId?: string) {
  if (!endpointId) return '未归属';
  return endpointMap.value[endpointId]?.name || endpointId;
}

function resolvePriorityText(priority: number) {
  return priorityOptions.find((item) => item.value === priority)?.label || `娴兼ê鍘涚痪?${priority}`;
}

function getRequirementFollowUpItems(requirement: SprintMvpApi.Requirement) {
  const feedbackItems = getRequirementFeedback(requirement.id).map((feedback) => ({
    content: feedback.content || '閺嗗倹妫ら崘鍛啇',
    createdAt: feedback.createTime,
    feedback,
    id: `feedback:${feedback.id}`,
    status: feedback.status,
    title: feedback.title,
    type: 'feedback' as const,
  }));
  const childItems = (childRequirementsBySource.value[requirement.id] || []).map((child) => ({
    child,
    content: child.description || '閺嗗倹妫ら崘鍛啇',
    createdAt: child.createTime,
    id: `requirement:${child.id}`,
    status: child.status,
    title: child.title,
    type: 'requirement' as const,
  }));
  return [...feedbackItems, ...childItems].sort((left, right) =>
    right.createdAt.localeCompare(left.createdAt),
  );
}

async function ensureRequirementFeedback(requirementId: string) {
  if (requirementFeedbackMap.value[requirementId]) return;
  const feedback = await listRequirementFeedbackApi(requirementId);
  requirementFeedbackMap.value = {
    ...requirementFeedbackMap.value,
    [requirementId]: feedback,
  };
}

async function ensureRequirementSuggestions(requirement: SprintMvpApi.Requirement) {
  if (requirementSuggestionMap.value[requirement.id]) return;
  const suggestions = await listFeatureSuggestionsApi({
    projectId: requirement.projectId,
    requirementId: requirement.id,
  });
  requirementSuggestionMap.value = {
    ...requirementSuggestionMap.value,
    [requirement.id]: suggestions,
  };
}

async function handleExpandedRowKeysChange(keys: Array<number | string>) {
  expandedRequirementIds.value = keys;
  await Promise.all(keys.map((key) => ensureRequirementFeedback(String(key))));
  syncExpandedRequirementIds();
}

function resetForm() {
  const projectId = selectedProjectId.value || projects.value[0]?.id || '';
  const endpointId = endpoints.value.find((endpoint) => endpoint.projectId === projectId)?.id || '';
  const endpoint = endpoints.value.find((item) => item.id === endpointId);
  Object.assign(requirementForm, {
    description: '',
    endpointId,
    moduleId:
      modules.value.find(
        (module) => module.projectId === projectId && module.endpointId === endpointId,
      )?.id || '',
    priority: 3,
    projectId,
    requiresReview: true,
    skillIds: [...(endpoint?.skillIds || [])],
    stakeholderIds: [],
    title: '',
  });
}

function openCreate() {
  selectedRequirement.value = undefined;
  resetForm();
  editorVisible.value = true;
}

function openEdit(requirement: SprintMvpApi.Requirement) {
  if (!canEditRequirement(requirement)) {
    MessagePlugin.warning('需求提交评审后不支持编辑');
    return;
  }
  selectedRequirement.value = requirement;
  Object.assign(requirementForm, {
    description: requirement.description || '',
    endpointId: requirement.endpointId || '',
    moduleId: requirement.moduleId || '',
    priority: requirement.priority,
    projectId: requirement.projectId,
    requiresReview: true,
    skillIds: [...(requirement.skillIds || [])],
    stakeholderIds: deserializeStakeholders(requirement.stakeholders),
    title: requirement.title,
  });
  editorVisible.value = true;
}

async function openDetail(requirement: SprintMvpApi.Requirement) {
  selectedRequirement.value = requirement;
  const [reviews, feedback] = await Promise.all([
    listRequirementReviewsApi(requirement.id),
    listRequirementFeedbackApi(requirement.id),
  ]);
  requirementReviews.value = reviews;
  requirementFeedback.value = feedback;
  requirementFeedbackMap.value = {
    ...requirementFeedbackMap.value,
    [requirement.id]: feedback,
  };
  detailVisible.value = true;
}

function openReview(requirement: SprintMvpApi.Requirement) {
  selectedRequirement.value = requirement;
  reviewForm.reviewerIds = users.value
    .filter((user) => requirement.stakeholders?.includes(user.username) || user.username === 'admin')
    .map((user) => user.id);
  reviewVisible.value = true;
}

function openDecompose(requirement: SprintMvpApi.Requirement) {
  if (!decomposeAllowedStatuses.has(requirement.status)) {
    MessagePlugin.warning('需求评审通过后才能拆解任务');
    return;
  }
  selectedRequirement.value = requirement;
  decomposeForm.assigneeId = '';
  decomposeForm.assigneeType = 0;
  decomposeForm.assignmentMode = 'auto';
  decomposeForm.instruction = '';
  decomposeVisible.value = true;
}

function handleDecomposeAssigneeTypeChange() {
  decomposeForm.assigneeId = '';
}

function openFeedback(requirement: SprintMvpApi.Requirement, task?: SprintMvpApi.DevelopmentTask) {
  if (!canCreateFeedback(requirement)) {
    MessagePlugin.warning('閸ョ偤顩潪顒佸床閸戣櫣娈戦崥搴ｇ敾闂団偓濮瑰倷绗夐弨顖涘瘮閸愬秵顐奸崶鐐侯洯');
    return;
  }
  if (task && task.status !== 'completed') {
    MessagePlugin.warning('娴犲懎鍑＄€瑰本鍨氭禒璇插閺€顖涘瘮鐠佹澘缍嶉崶鐐侯洯');
    return;
  }
  selectedRequirement.value = requirement;
  selectedFeedbackTaskId.value = task?.id || '';
  Object.assign(feedbackForm, {
    content: task?.description || '',
    title: task ? `娴犺濮熼崶鐐侯洯 - ${task.title}` : '',
  });
  feedbackVisible.value = true;
}

async function openConvertFeedback(feedback: SprintMvpApi.RequirementFeedback) {
  selectedFeedback.value = feedback;
  if (selectedRequirement.value) {
    await ensureRequirementSuggestions(selectedRequirement.value);
  }
  Object.assign(convertFeedbackForm, {
    description: feedback.content || '',
    feedbackIds: [feedback.id],
    priority: selectedRequirement.value?.priority || 3,
    remark: '',
    stakeholderIds: deserializeStakeholders(selectedRequirement.value?.stakeholders),
    suggestionIds: [] as string[],
    title: feedback.title,
  });
  convertFeedbackVisible.value = true;
}

async function openConvertFeedbackFromRequirement(
  requirement: SprintMvpApi.Requirement,
  feedback: SprintMvpApi.RequirementFeedback,
) {
  selectedRequirement.value = requirement;
  await ensureRequirementFeedback(requirement.id);
  await openConvertFeedback(feedback);
}

async function loadProjects() {
  [projects.value, endpoints.value, modules.value, skills.value] = await Promise.all([
    listProjectsApi(),
    listProjectEndpointsApi(),
    listFeatureModulesApi(),
    listSkillsApi(true),
  ]);
  selectedProjectId.value ||= projects.value[0]?.id || '';
}

async function loadRequirements() {
  loading.value = true;
  try {
    const [nextRequirements, nextTasks] = await Promise.all([
      listRequirementsApi(selectedProjectId.value || undefined, {
        health: filters.health || undefined,
        keyword: filters.requirementInfo || undefined,
        status: filters.status || undefined,
      }),
      listDevelopmentTasksApi({ projectId: selectedProjectId.value || undefined }),
    ]);
    requirements.value = nextRequirements;
    developmentTasks.value = nextTasks;
    selectedRequirementKeys.value = selectedRequirementKeys.value.filter((id) =>
      nextRequirements.some((requirement) => requirement.id === id),
    );
    pagination.current = 1;
    const feedbackEntries = await Promise.all(
      requirements.value
        .filter((requirement) => feedbackAllowedStatuses.has(requirement.status))
        .map(async (requirement) => [
          requirement.id,
          await listRequirementFeedbackApi(requirement.id),
        ] as const),
    );
    requirementFeedbackMap.value = Object.fromEntries(feedbackEntries);
    const suggestionEntries = await Promise.all(
      requirements.value
        .filter((requirement) => feedbackAllowedStatuses.has(requirement.status))
        .map(async (requirement) => [
          requirement.id,
          await listFeatureSuggestionsApi({
            projectId: requirement.projectId,
            requirementId: requirement.id,
          }),
        ] as const),
    );
    requirementSuggestionMap.value = Object.fromEntries(suggestionEntries);
    syncExpandedRequirementIds();
    if (selectedRequirement.value) {
      selectedRequirement.value = requirements.value.find(
        (item) => item.id === selectedRequirement.value?.id,
      );
    }
  } finally {
    loading.value = false;
  }
}

function goTaskAdvance(task: SprintMvpApi.DevelopmentTask) {
  router.push(`/sprint/tasks/detail/${task.id}`);
}

async function queryRequirements() {
  handleLocalFilterChange();
  await loadRequirements();
}

async function resetFilters() {
  selectedProjectId.value = projects.value[0]?.id || '';
  selectedRequirementKeys.value = [];
  Object.assign(filters, {
    health: '',
    requirementInfo: '',
    status: '',
  });
  await loadRequirements();
}

function handleLocalFilterChange() {
  selectedRequirementKeys.value = [];
  pagination.current = 1;
}

function getSelectedRequirementOrWarn() {
  if (!selectedRequirementForAction.value) {
    MessagePlugin.warning('请先选择一条需求');
    return undefined;
  }
  return selectedRequirementForAction.value;
}

function openSelectedDecompose() {
  const requirement = getSelectedRequirementOrWarn();
  if (!requirement || !canDecomposeSelectedRequirement.value) return;
  openDecompose(requirement);
}

function openSelectedFeedback() {
  const requirement = getSelectedRequirementOrWarn();
  if (!requirement || !canCreateSelectedFeedback.value) return;
  openFeedback(requirement);
}

function openSelectedReview() {
  const requirement = getSelectedRequirementOrWarn();
  if (!requirement || !canSubmitSelectedRequirement.value) return;
  openReview(requirement);
}

function deleteSelectedDraftRequirement() {
  const requirement = getSelectedRequirementOrWarn();
  if (!requirement || !canDeleteDraftSelectedRequirement.value) return;
  deleteDraftRequirement(requirement);
}

function voidSelectedRequirement() {
  const requirement = getSelectedRequirementOrWarn();
  if (!requirement || !canVoidSelectedRequirement.value) return;
  voidRequirement(requirement);
}

function closeSelectedRequirement() {
  const requirement = getSelectedRequirementOrWarn();
  if (!requirement || !canCloseSelectedRequirement.value) return;
  closeRequirement(requirement);
}

async function saveRequirement() {
  if (requirementSaving.value) return;
  if (!(await validateForm(requirementFormRef.value))) return;
  if (!requirementForm.projectId) {
    MessagePlugin.warning('璇峰厛閫夋嫨椤圭洰');
    return;
  }
  if (!requirementForm.title.trim()) {
    MessagePlugin.warning('需求标题不能为空');
    return;
  }
  if (!requirementForm.endpointId || !requirementForm.moduleId) {
    MessagePlugin.warning('璇烽€夋嫨绔拰鍔熻兘妯″潡');
    return;
  }

  requirementSaving.value = true;
  try {
    if (selectedRequirement.value) {
      if (!canEditRequirement(selectedRequirement.value)) {
        MessagePlugin.warning('需求提交评审后不支持编辑');
        editorVisible.value = false;
        return;
      }
      await updateRequirementApi(selectedRequirement.value.id, {
        description: requirementForm.description,
        priority: requirementForm.priority,
        stakeholders: serializeStakeholders(requirementForm.stakeholderIds),
        skillIds: [...requirementForm.skillIds],
        title: requirementForm.title,
      });
    } else {
      await createRequirementApi({
        description: requirementForm.description,
        endpointId: requirementForm.endpointId,
        moduleId: requirementForm.moduleId,
        priority: requirementForm.priority,
        projectId: requirementForm.projectId,
        requiresReview: requirementForm.requiresReview,
        skillIds: [...requirementForm.skillIds],
        stakeholders: serializeStakeholders(requirementForm.stakeholderIds),
        title: requirementForm.title,
      });
    }

    MessagePlugin.success('闇€姹傚凡淇濆瓨');
    editorVisible.value = false;
    if (!selectedRequirement.value && selectedProjectId.value !== requirementForm.projectId) {
      selectedProjectId.value = requirementForm.projectId;
    }
    await loadRequirements();
  } finally {
    requirementSaving.value = false;
  }
}
async function submitReview() {
  if (reviewSubmitting.value) return;
  if (!selectedRequirement.value) return;
  if (!(await validateForm(reviewFormRef.value))) return;
  const reviewerIds = [...reviewForm.reviewerIds];
  if (reviewerIds.length === 0) {
    MessagePlugin.warning('请选择评审人');
    return;
  }

  reviewSubmitting.value = true;
  try {
    await submitRequirementReviewApi(selectedRequirement.value.id, { reviewerIds });
    MessagePlugin.success('已提交需求评审');
    reviewVisible.value = false;
    await loadRequirements();
  } finally {
    reviewSubmitting.value = false;
  }
}
async function decomposeRequirement() {
  if (decomposing.value) return;
  if (!selectedRequirement.value) return;
  const manualAssigneeId = decomposeForm.assigneeId.trim();
  if (decomposeForm.assignmentMode === 'manual' && !manualAssigneeId) {
    MessagePlugin.warning('璇烽€夋嫨鐮斿彂浜哄憳');
    return;
  }
  decomposing.value = true;
  try {
    await decomposeRequirementApi(selectedRequirement.value.id, {
      assignmentMode: decomposeForm.assignmentMode,
      assigneeId: decomposeForm.assignmentMode === 'manual' ? manualAssigneeId : undefined,
      assigneeType: decomposeForm.assignmentMode === 'manual' ? decomposeForm.assigneeType : undefined,
      instruction: decomposeForm.instruction,
    });
    MessagePlugin.success('任务拆解已生成');
    decomposeVisible.value = false;
    await loadRequirements();
  } finally {
    decomposing.value = false;
  }
}
async function voidRequirement(requirement: SprintMvpApi.Requirement) {
  await voidRequirementApi(requirement.id);
  MessagePlugin.success('闂団偓濮瑰倸鍑℃担婊冪熬');
  detailVisible.value = false;
  await loadRequirements();
}

function deleteDraftRequirement(requirement: SprintMvpApi.Requirement) {
  confirmAndClose({
    body: `纭鍒犻櫎鑽夌闇€姹傘€?{requirement.title}銆嶏紵`,
    confirmBtn: '鍒犻櫎',
    header: '删除草稿需求',
    onConfirm: async () => {
      await deleteDraftRequirementApi(requirement.id);
      MessagePlugin.success('鑽夌闇€姹傚凡鍒犻櫎');
      detailVisible.value = false;
      await loadRequirements();
    },
  });
}

async function closeRequirement(requirement: SprintMvpApi.Requirement) {
  await closeRequirementApi(requirement.id);
  MessagePlugin.success('需求已作废');
  detailVisible.value = false;
  await loadRequirements();
}

async function completeRequirementDevelopment(requirement: SprintMvpApi.Requirement) {
  await completeRequirementDevelopmentApi(requirement.id, {});
  MessagePlugin.success('需求已确认开发完成，进入待测试');
  detailVisible.value = false;
  await loadRequirements();
}

async function saveFeedback() {
  if (feedbackSaving.value) return;
  if (!selectedRequirement.value) return;
  if (!(await validateForm(feedbackFormRef.value))) return;
  if (!feedbackForm.title.trim()) {
    MessagePlugin.warning('鍥為鏍囬涓嶈兘涓虹┖');
    return;
  }

  feedbackSaving.value = true;
  try {
    await createRequirementFeedbackApi(selectedRequirement.value.id, {
      content: feedbackForm.content,
      developmentTaskId: selectedFeedbackTaskId.value || undefined,
      title: feedbackForm.title,
    });
    MessagePlugin.success('反馈已记录');
    feedbackVisible.value = false;
    requirementFeedback.value = await listRequirementFeedbackApi(selectedRequirement.value.id);
    requirementFeedbackMap.value = {
      ...requirementFeedbackMap.value,
      [selectedRequirement.value.id]: requirementFeedback.value,
    };
  } finally {
    feedbackSaving.value = false;
  }
}
async function convertFeedback() {
  if (convertingFeedback.value) return;
  if (!selectedRequirement.value) return;
  if (!(await validateForm(convertFeedbackFormRef.value))) return;
  if (!convertFeedbackForm.title.trim()) {
    MessagePlugin.warning('后续需求标题不能为空');
    return;
  }

  if (convertFeedbackForm.feedbackIds.length === 0 && convertFeedbackForm.suggestionIds.length === 0) {
    MessagePlugin.warning('璇烽€夋嫨鑷冲皯涓€涓洖棣堟垨浼樺寲寤鸿');
    return;
  }

  convertingFeedback.value = true;
  try {
    const feedback = selectedFeedback.value;
    if (
      feedback &&
      convertFeedbackForm.feedbackIds.length === 1 &&
      convertFeedbackForm.suggestionIds.length === 0 &&
      feedback.id === convertFeedbackForm.feedbackIds[0]
    ) {
      await convertRequirementFeedbackApi(
        selectedRequirement.value.id,
        feedback.id,
        {
          description: convertFeedbackForm.description,
          priority: convertFeedbackForm.priority,
          remark: convertFeedbackForm.remark,
          stakeholders: serializeStakeholders(convertFeedbackForm.stakeholderIds),
          title: convertFeedbackForm.title,
        },
      );
    } else {
      await convertRequirementSourcesApi(selectedRequirement.value.id, {
        description: convertFeedbackForm.description,
        feedbackIds: [...convertFeedbackForm.feedbackIds],
        priority: convertFeedbackForm.priority,
        remark: convertFeedbackForm.remark,
        stakeholders: serializeStakeholders(convertFeedbackForm.stakeholderIds),
        suggestionIds: [...convertFeedbackForm.suggestionIds],
        title: convertFeedbackForm.title,
      });
    }
    MessagePlugin.success('反馈已转为后续需求');
    convertFeedbackVisible.value = false;
    requirementFeedback.value = await listRequirementFeedbackApi(selectedRequirement.value.id);
    const suggestions = await listFeatureSuggestionsApi({
      projectId: selectedRequirement.value.projectId,
      requirementId: selectedRequirement.value.id,
    });
    requirementFeedbackMap.value = {
      ...requirementFeedbackMap.value,
      [selectedRequirement.value.id]: requirementFeedback.value,
    };
    requirementSuggestionMap.value = {
      ...requirementSuggestionMap.value,
      [selectedRequirement.value.id]: suggestions,
    };
    await loadRequirements();
  } finally {
    convertingFeedback.value = false;
  }
}
onMounted(async () => {
  [users.value, digitalWorkers.value] = await Promise.all([
    listUserOptionsApi(),
    listDigitalWorkersApi({ status: 'active' }),
  ]);
  await loadProjects();
  await loadRequirements();
});

watch(
  () => requirementForm.projectId,
  (projectId) => {
    if (selectedRequirement.value) return;
    const endpointId = endpoints.value.find((endpoint) => endpoint.projectId === projectId)?.id || '';
    const endpoint = endpoints.value.find((item) => item.id === endpointId);
    requirementForm.endpointId = endpointId;
    requirementForm.skillIds = [...(endpoint?.skillIds || [])];
    requirementForm.moduleId =
      modules.value.find(
        (module) => module.projectId === projectId && module.endpointId === endpointId,
      )?.id || '';
  },
);

watch(
  () => requirementForm.endpointId,
  (endpointId) => {
    if (selectedRequirement.value) return;
    const endpoint = endpoints.value.find((item) => item.id === endpointId);
    requirementForm.skillIds = [...(endpoint?.skillIds || [])];
    requirementForm.moduleId =
      modules.value.find(
        (module) => module.projectId === requirementForm.projectId && module.endpointId === endpointId,
      )?.id || '';
  },
);

watch(selectedProjectId, async () => {
  await loadRequirements();
});

watch(requirements, syncExpandedRequirementIds);

onActivated(async () => {
  await loadRequirements();
});
</script>

<template>
  <ProjectSecondaryListShell
    v-model:selected-project-id="selectedProjectId"
    class="requirements-page"
    :loading="loading"
    :projects="projects"
    @refresh="loadRequirements"
  >
    <template #header>
      <section class="sprint-page-title">
        <h2>需求管理</h2>
        <p>维护需求、评审、拆解、测试闭环和验收后的产品反馈。</p>
      </section>
    </template>

    <template #project-meta="{ project }">
      <span>缁忕悊 {{ resolveUserName(project.projectManagerId) }}</span>
    </template>

    <template #workspace-header>
        <div class="workspace-head">
          <div>
            <h3>{{ selectedProject?.name || '璇烽€夋嫨椤圭洰' }}</h3>
            <p>{{ selectedProject?.code || '-' }}</p>
          </div>
          <TButton theme="primary" :disabled="!selectedProjectId" @click="openCreate">
            <template #icon>
              <IconifyIcon icon="lucide:plus" />
            </template>
            新增需求
          </TButton>
        </div>
    </template>

    <section class="sprint-filter-panel">
      <div class="sprint-filter-grid">
        <div class="sprint-filter-field">
          <span>状态</span>
          <TSelect
            v-model="filters.status"
            :options="statusOptions"
            clearable
            placeholder="全部状态"
            @change="handleLocalFilterChange"
          />
        </div>
        <div class="sprint-filter-field">
          <span>健康度</span>
          <TSelect
            v-model="filters.health"
            :options="healthOptions"
            clearable
            placeholder="全部健康状态"
            @change="handleLocalFilterChange"
          />
        </div>
        <div class="sprint-filter-field">
          <span>需求信息</span>
          <TInput
            v-model="filters.requirementInfo"
            clearable
            placeholder="闇€姹傚悕銆佸唴瀹广€佸共绯讳汉"
            @change="handleLocalFilterChange"
          />
        </div>
        <div class="sprint-filter-actions">
          <TButton theme="primary" :loading="loading" @click="queryRequirements">
            <template #icon>
              <IconifyIcon icon="lucide:search" />
            </template>
            鏌ヨ
          </TButton>
          <TButton theme="default" :disabled="loading" @click="resetFilters">
            <template #icon>
              <IconifyIcon icon="lucide:rotate-ccw" />
            </template>
            閲嶇疆
          </TButton>
        </div>
      </div>
    </section>

    <section class="sprint-table-panel">
      <div class="sprint-table-header">
        <h3>{{ selectedProjectName || '需求列表' }}</h3>
        <div class="sprint-table-actions">
          <TButton
            theme="primary"
            :disabled="!canSubmitSelectedRequirement"
            @click="openSelectedReview"
          >
            <template #icon>
              <IconifyIcon icon="lucide:clipboard-check" />
            </template>
            绔嬮」鎺ㄨ繘
          </TButton>
          <TButton
            theme="primary"
            :disabled="!canDecomposeSelectedRequirement"
            @click="openSelectedDecompose"
          >
            <template #icon>
              <IconifyIcon icon="lucide:list-tree" />
            </template>
            浠诲姟鎷嗚В
          </TButton>
          <TButton
            theme="success"
            :disabled="!canCloseSelectedRequirement"
            @click="closeSelectedRequirement"
          >
            <template #icon>
              <IconifyIcon icon="lucide:check-circle" />
            </template>
            楠屾敹鍏抽棴
          </TButton>
          <TButton
            theme="primary"
            variant="outline"
            :disabled="!canCreateSelectedFeedback"
            @click="openSelectedFeedback"
          >
            <template #icon>
              <IconifyIcon icon="lucide:message-square" />
            </template>
            璁板綍鍥為
          </TButton>
          <TButton
            theme="danger"
            variant="outline"
            :disabled="!canDeleteDraftSelectedRequirement"
            @click="deleteSelectedDraftRequirement"
          >
            <template #icon>
              <IconifyIcon icon="lucide:trash-2" />
            </template>
            鍒犻櫎
          </TButton>
          <TButton
            theme="danger"
            :disabled="!canVoidSelectedRequirement"
            @click="voidSelectedRequirement"
          >
            <template #icon>
              <IconifyIcon icon="lucide:ban" />
            </template>
            浣滃簾
          </TButton>
          <TButton shape="circle" variant="outline" title="鍒锋柊" :loading="loading" @click="loadRequirements">
            <IconifyIcon icon="lucide:refresh-cw" />
          </TButton>
        </div>
      </div>

      <TTable
        row-key="id"
        class="sprint-compact-table"
        :columns="withSerialColumn(columns, { offset: () => (pagination.current - 1) * pagination.pageSize })"
        :data="requirements"
        :expand-on-row-click="false"
        :expanded-row-keys="expandedRequirementIds"
        :loading="loading"
        :pagination="tablePagination"
        :selected-row-keys="selectedRequirementKeys"
        size="small"
        row-selection-type="single"
        select-on-row-click
        hover
        stripe
        @expand-change="handleExpandedRowKeysChange"
        @page-change="handlePageChange"
        @select-change="selectedRequirementKeys = $event"
      >
        <template #expandedRow="{ row }">
          <div class="requirement-expanded">
            <section class="expanded-section">
              <h4>浠诲姟鎷嗚В</h4>
              <div v-if="getRequirementTasks(row.id).length === 0" class="expanded-empty">
                鏆傛棤鎷嗚В浠诲姟
              </div>
              <div
                v-for="task in getRequirementTasks(row.id)"
                :key="task.id"
                class="expanded-task-row"
              >
                <TTag variant="light">{{ taskStatusText[task.status] || task.status }}</TTag>
                <strong>{{ task.title }}</strong>
                <span>{{ task.assigneeId || '未指派' }}</span>
                <span>浼樺厛绾?{{ task.priority }}</span>
                <TSpace class="sprint-row-actions expanded-actions">
                  <TLink v-if="task.status !== 'completed'" theme="primary" @click="goTaskAdvance(task)">
                    <IconifyIcon icon="lucide:play" />
                    浠诲姟鎺ㄨ繘
                  </TLink>
                </TSpace>
                <p>{{ task.description || '鏆傛棤浠诲姟璇存槑' }}</p>
              </div>
            </section>

            <section class="expanded-section">
              <h4>反馈与子需求</h4>
              <div v-if="getRequirementFollowUpItems(row).length === 0" class="expanded-empty">
                鏆傛棤鍥為涓庡瓙闇€姹?              </div>
              <div
                v-for="item in getRequirementFollowUpItems(row)"
                :key="item.id"
                class="expanded-item"
              >
                <TTag :theme="item.type === 'feedback' ? 'warning' : 'primary'" variant="light">
                  {{ item.type === 'feedback' ? '反馈' : '需求' }}
                </TTag>
                <TTag variant="light">
                  {{
                    item.type === 'feedback'
                      ? feedbackStatusText[item.status] || item.status
                      : statusText[item.status] || item.status
                  }}
                </TTag>
                <strong>{{ item.title }}</strong>
                <span>{{ formatDateTime(item.createdAt) }}</span>
                <TSpace class="sprint-row-actions expanded-actions">
                  <template v-if="item.type === 'feedback'">
                    <TLink
                      v-if="item.feedback.status === 'open'"
                      theme="primary"
                      @click="openConvertFeedbackFromRequirement(row, item.feedback)"
                    >
                      <IconifyIcon icon="lucide:arrow-right" />
                      杞渶姹?                    </TLink>
                  </template>
                  <template v-else>
                    <TLink
                      v-if="canSubmitReview(item.child)"
                      theme="primary"
                      @click="openReview(item.child)"
                    >
                      <IconifyIcon icon="lucide:clipboard-check" />
                      鎻愪氦璇勫
                    </TLink>
                    <TLink
                      v-if="decomposeAllowedStatuses.has(item.child.status)"
                      theme="primary"
                      @click="openDecompose(item.child)"
                    >
                      <IconifyIcon icon="lucide:list-tree" />
                      浠诲姟鎷嗚В
                    </TLink>
                    <TLink
                      v-if="item.child.status === 'developing' || item.child.status === 'pending_fix'"
                      theme="success"
                      @click="completeRequirementDevelopment(item.child)"
                    >
                      <IconifyIcon icon="lucide:check" />
                      瀹屾垚寮€鍙?                    </TLink>
                    <TLink
                      v-if="item.child.status === 'tested'"
                      theme="success"
                      @click="closeRequirement(item.child)"
                    >
                      <IconifyIcon icon="lucide:check-circle" />
                      楠屾敹鍏抽棴
                    </TLink>
                    <TLink theme="primary" @click="openDetail(item.child)">
                      <IconifyIcon icon="lucide:eye" />
                      璇︽儏
                    </TLink>
                  </template>
                </TSpace>
                <p>{{ item.content }}</p>
              </div>
            </section>
          </div>
        </template>
        <template #endpointId="{ row }">
          {{ resolveEndpointName(row.endpointId) }}
        </template>
        <template #status="{ row }">
          <TTag variant="light">{{ statusText[row.status] || row.status }}</TTag>
        </template>
        <template #health="{ row }">
          <TTag :theme="healthTheme[row.health] || 'primary'" variant="light">
            {{ healthText[row.health] || row.health }}
          </TTag>
        </template>
        <template #priority="{ row }">
          <TTag variant="light">{{ resolvePriorityText(row.priority) }}</TTag>
        </template>
        <template #createdBy="{ row }">
          {{ resolveUserName(row.createdBy) }}
        </template>
        <template #stakeholders="{ row }">
          {{ resolveStakeholderNames(row.stakeholders) }}
        </template>
        <template #actions="{ row }">
          <TSpace class="sprint-row-actions">
            <TLink v-if="canEditRequirement(row)" theme="primary" @click="openEdit(row)">
              <IconifyIcon icon="lucide:pencil" />
              缂栬緫
            </TLink>
            <TLink v-else theme="primary" @click="openDetail(row)">
              <IconifyIcon icon="lucide:eye" />
              璇︽儏
            </TLink>
          </TSpace>
        </template>
      </TTable>
    </section>

    <TDrawer
      v-model:visible="editorVisible"
      :size="'60%'"
      :header="selectedRequirement ? '编辑需求' : '新增需求'"
      :confirm-btn="{ content: '淇濆瓨', loading: requirementSaving }"
      @confirm="saveRequirement"
    >
      <TForm ref="requirementFormRef" :data="requirementForm" :rules="requirementRules" label-width="90px">
        <div class="requirement-relation-row">
          <TFormItem label="所属项目" name="projectId">
            <TSelect
              v-model="requirementForm.projectId"
              :disabled="!!selectedRequirement"
              :options="projectOptions"
            />
          </TFormItem>
          <TFormItem label="端" name="endpointId">
            <TSelect
              v-model="requirementForm.endpointId"
              :disabled="!!selectedRequirement"
              :options="endpointOptions"
              placeholder="请选择端"
            />
          </TFormItem>
          <TFormItem label="鍔熻兘妯″潡" name="moduleId">
            <TSelect
              v-model="requirementForm.moduleId"
              :disabled="!!selectedRequirement"
              :options="moduleOptions"
              placeholder="璇烽€夋嫨鍔熻兘妯″潡"
            />
          </TFormItem>
        </div>
        <TFormItem label="需求标题" name="title">
          <TInput v-model="requirementForm.title" />
        </TFormItem>
        <TFormItem label="优先级" name="priority">
          <div class="priority-options">
            <TButton
              v-for="item in priorityOptions"
              :key="item.value"
              :theme="requirementForm.priority === item.value ? 'primary' : 'default'"
              :variant="requirementForm.priority === item.value ? 'base' : 'outline'"
              @click="requirementForm.priority = item.value"
            >
              {{ item.label }}
            </TButton>
          </div>
        </TFormItem>
        <TFormItem label="干系人">
          <TSelect
            v-model="requirementForm.stakeholderIds"
            multiple
            filterable
            :options="userOptions"
            placeholder="选择干系人"
          />
        </TFormItem>
        <TFormItem label="Skill">
          <TSelect v-model="requirementForm.skillIds" multiple filterable :options="skillOptions">
            <template #option="{ option }">
              <SkillSelectOption :skill="option.skill" />
            </template>
          </TSelect>
        </TFormItem>
        <TFormItem v-if="!selectedRequirement" label="需要评审">
          <TSwitch v-model="requirementForm.requiresReview" />
        </TFormItem>
        <TFormItem label="需求内容" class="markdown-form-item">
          <MarkdownEditor
            v-model="requirementForm.description"
            :height="560"
            placeholder="使用 Markdown 编写需求背景、目标、验收标准。"
          />
        </TFormItem>
      </TForm>
    </TDrawer>

    <TDrawer
      v-model:visible="detailVisible"
      :footer="false"
      :size="'60%'"
      :header="selectedRequirement?.title || '需求详情'"
    >
      <article v-if="selectedRequirement" class="detail">
        <TTag :theme="healthTheme[selectedRequirement.health] || 'primary'" variant="light">
          {{ healthText[selectedRequirement.health] || selectedRequirement.health }}
        </TTag>
        <h3>{{ selectedRequirement.title }}</h3>
        <article
          class="markdown-preview detail-markdown"
          v-html="renderMarkdown(selectedRequirement.description || '暂无需求内容')"
        ></article>
        <dl>
          <dt>状态</dt>
          <dd>{{ statusText[selectedRequirement.status] || selectedRequirement.status }}</dd>
          <dt>浜у搧缁忕悊</dt>
          <dd>{{ resolveUserName(selectedRequirement.createdBy) }}</dd>
          <dt>干系人</dt>
          <dd>{{ resolveStakeholderNames(selectedRequirement.stakeholders) }}</dd>
        </dl>
        <section v-if="requirementReviews.length > 0" class="review-history">
          <h4>璇勫璁板綍</h4>
          <div
            v-for="review in requirementReviews"
            :key="review.id"
            class="review-history__item"
          >
            <TTag variant="light">{{ reviewStatusText[review.status] || review.status }}</TTag>
            <strong>
              {{ userMap[review.reviewerId]?.displayName || review.reviewerId }}
            </strong>
            <span>{{ formatDateTime(review.reviewedAt || review.createTime) }}</span>
            <p>{{ review.comment || '鏆傛棤鎰忚' }}</p>
          </div>
        </section>
        <section class="feedback-history">
          <h4>浜у搧鍥為</h4>
          <div v-if="requirementFeedback.length === 0" class="feedback-empty">
            鏆傛棤鍥為
          </div>
          <div
            v-for="feedback in requirementFeedback"
            :key="feedback.id"
            class="feedback-history__item"
          >
            <TTag variant="light">
              {{ feedbackStatusText[feedback.status] || feedback.status }}
            </TTag>
            <strong>{{ feedback.title }}</strong>
            <span>{{ formatDateTime(feedback.createTime) }}</span>
            <p>{{ feedback.content || '鏆傛棤鍐呭' }}</p>
            <TButton
              v-if="feedback.status === 'open'"
              size="small"
              theme="primary"
              variant="outline"
              @click="openConvertFeedback(feedback)"
            >
              <template #icon>
                <IconifyIcon icon="lucide:arrow-right" />
              </template>
              转后续需求
            </TButton>
          </div>
        </section>
        <div class="detail-actions">
          <TSpace>
            <TButton
              v-if="canEditRequirement(selectedRequirement)"
              theme="primary"
              @click="openEdit(selectedRequirement)"
            >
              <template #icon>
                <IconifyIcon icon="lucide:pencil" />
              </template>
              缂栬緫
            </TButton>
            <TButton
              v-if="decomposeAllowedStatuses.has(selectedRequirement.status)"
              theme="primary"
              @click="openDecompose(selectedRequirement)"
            >
              <template #icon>
                <IconifyIcon icon="lucide:list-tree" />
              </template>
              浠诲姟鎷嗚В
            </TButton>
            <TButton
              v-if="canSubmitReview(selectedRequirement)"
              theme="primary"
              @click="openReview(selectedRequirement)"
            >
              <template #icon>
                <IconifyIcon icon="lucide:clipboard-check" />
              </template>
              绔嬮」鎺ㄨ繘
            </TButton>
            <TButton
              v-if="selectedRequirement.status === 'draft'"
              theme="danger"
              variant="outline"
              @click="deleteDraftRequirement(selectedRequirement)"
            >
              <template #icon>
                <IconifyIcon icon="lucide:trash-2" />
              </template>
              鍒犻櫎鑽夌
            </TButton>
            <TButton
              v-if="selectedRequirement.status === 'rejected'"
              theme="danger"
              @click="voidRequirement(selectedRequirement)"
            >
              <template #icon>
                <IconifyIcon icon="lucide:ban" />
              </template>
              浣滃簾闇€姹?            </TButton>
            <TButton
              v-if="selectedRequirement.status === 'tested'"
              theme="success"
              @click="closeRequirement(selectedRequirement)"
            >
              <template #icon>
                <IconifyIcon icon="lucide:check-circle" />
              </template>
              楠屾敹鍏抽棴
            </TButton>
            <TButton
              v-if="canCreateFeedback(selectedRequirement)"
              theme="primary"
              variant="outline"
              @click="openFeedback(selectedRequirement)"
            >
              <template #icon>
                <IconifyIcon icon="lucide:message-square" />
              </template>
              璁板綍鍥為
            </TButton>
          </TSpace>
        </div>
      </article>
    </TDrawer>

    <TDrawer
      v-model:visible="reviewVisible"
      :size="'40%'"
      header="提交需求评审"
      :confirm-btn="{ content: '鎻愪氦', loading: reviewSubmitting }"
      @confirm="submitReview"
    >
      <TForm ref="reviewFormRef" :data="reviewForm" :rules="reviewRules" label-width="90px">
        <TFormItem label="评审人" name="reviewerIds">
          <TSelect
            v-model="reviewForm.reviewerIds"
            multiple
            :options="userOptions"
            placeholder="选择评审人"
          />
        </TFormItem>
      </TForm>
    </TDrawer>

    <TDrawer
      v-model:visible="decomposeVisible"
      :size="'60%'"
      header="AI 浠诲姟鎷嗚В"
      :confirm-btn="{ content: '鐢熸垚浠诲姟', loading: decomposing }"
      @confirm="decomposeRequirement"
    >
      <TForm :data="decomposeForm" label-width="110px">
        <TFormItem label="浠诲姟鍒嗘淳">
          <TSelect
            v-model="decomposeForm.assignmentMode"
            :options="[
              { label: '鑷姩鍒嗘淳', value: 'auto' },
              { label: '鎵嬪姩鎸囨淳', value: 'manual' },
            ]"
          />
        </TFormItem>
        <TFormItem v-if="decomposeForm.assignmentMode === 'manual'" label="鎸囨淳绫诲瀷">
          <TSelect
            v-model="decomposeForm.assigneeType"
            :options="[
              { label: '鍛樺伐', value: 0 },
              { label: '鏁板瓧鍛樺伐', value: 1 },
            ]"
            @change="handleDecomposeAssigneeTypeChange"
          />
        </TFormItem>
        <TFormItem
          v-if="decomposeForm.assignmentMode === 'manual'"
          :label="decomposeForm.assigneeType === 1 ? '鏁板瓧鍛樺伐' : '鐮斿彂浜哄憳'"
        >
          <TSelect
            v-model="decomposeForm.assigneeId"
            filterable
            :options="decomposeAssigneeOptions"
            :placeholder="decomposeForm.assigneeType === 1 ? '閫夋嫨鏁板瓧鍛樺伐' : '閫夋嫨鐮斿彂浜哄憳'"
          />
        </TFormItem>
        <TFormItem label="鎷嗚В琛ュ厖瑕佹眰" class="markdown-form-item">
          <MarkdownEditor
            v-model="decomposeForm.instruction"
            :height="420"
            placeholder="填写拆解补充要求，留空则按需求内容生成默认任务。"
          />
        </TFormItem>
      </TForm>
    </TDrawer>

    <TDrawer
      v-model:visible="feedbackVisible"
      :size="'60%'"
      header="璁板綍浜у搧鍥為"
      :confirm-btn="{ content: '淇濆瓨', loading: feedbackSaving }"
      @confirm="saveFeedback"
    >
      <TForm ref="feedbackFormRef" :data="feedbackForm" :rules="feedbackRules" label-width="90px">
        <TFormItem label="鏍囬" name="title">
          <TInput v-model="feedbackForm.title" />
        </TFormItem>
        <TFormItem label="鍐呭" class="markdown-form-item">
          <MarkdownEditor
            v-model="feedbackForm.content"
            :height="420"
            placeholder="璁板綍楠屾敹鍚庣殑鏂版兂娉曘€佽ˉ鍏呰寖鍥存垨浼樺寲寤鸿"
          />
        </TFormItem>
      </TForm>
    </TDrawer>

    <TDrawer
      v-model:visible="convertFeedbackVisible"
      :size="'60%'"
      header="转为后续需求"
      :confirm-btn="{ content: '鍒涘缓鑽夌', loading: convertingFeedback }"
      @confirm="convertFeedback"
    >
      <TForm
        ref="convertFeedbackFormRef"
        :data="convertFeedbackForm"
        :rules="convertFeedbackRules"
        label-width="90px"
      >
        <TFormItem label="鏍囬" name="title">
          <TInput v-model="convertFeedbackForm.title" />
        </TFormItem>
        <TFormItem label="鍥為鏉ユ簮">
          <TSelect
            v-model="convertFeedbackForm.feedbackIds"
            :options="convertFeedbackOptions"
            clearable
            multiple
          />
        </TFormItem>
        <TFormItem label="寤鸿鏉ユ簮">
          <TSelect
            v-model="convertFeedbackForm.suggestionIds"
            :options="convertSuggestionOptions"
            clearable
            multiple
          />
        </TFormItem>
        <TFormItem label="优先级" name="priority">
          <div class="priority-options">
            <TButton
              v-for="item in priorityOptions"
              :key="item.value"
              :theme="convertFeedbackForm.priority === item.value ? 'primary' : 'default'"
              :variant="convertFeedbackForm.priority === item.value ? 'base' : 'outline'"
              @click="convertFeedbackForm.priority = item.value"
            >
              {{ item.label }}
            </TButton>
          </div>
        </TFormItem>
        <TFormItem label="干系人">
          <TSelect
            v-model="convertFeedbackForm.stakeholderIds"
            multiple
            filterable
            :options="userOptions"
            placeholder="选择干系人"
          />
        </TFormItem>
        <TFormItem label="澶囨敞">
          <TTextarea
            v-model="convertFeedbackForm.remark"
            class="drawer-textarea drawer-textarea--short"
            placeholder="填写转需求备注"
          />
        </TFormItem>
        <TFormItem label="需求内容" class="markdown-form-item">
          <MarkdownEditor
            v-model="convertFeedbackForm.description"
            :height="360"
            placeholder="鍚庣画闇€姹備細淇濈暀鏉ユ簮闇€姹傚拰鏉ユ簮鍥為"
          />
        </TFormItem>
      </TForm>
    </TDrawer>
  </ProjectSecondaryListShell>
</template>
<style scoped>
.requirements-page {
  display: flex;
  flex-direction: column;
}

.requirement-relation-row {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}

.requirement-expanded {
  display: grid;
  gap: 14px;
  padding: 12px 16px;
  background: var(--td-bg-color-page);
}

.expanded-section h4 {
  margin: 0 0 10px;
  font-size: 14px;
  line-height: 20px;
}

.expanded-item,
.expanded-task-row {
  display: grid;
  grid-template-columns: auto auto minmax(180px, 1fr) auto auto;
  gap: 8px 10px;
  align-items: center;
  padding: 8px 0;
  border-top: 1px solid var(--td-component-border);
}

.expanded-task-row {
  grid-template-columns: auto minmax(220px, 1fr) 120px 90px auto;
}

.expanded-item p,
.expanded-task-row p {
  grid-column: 1 / -1;
  margin: 0;
  color: var(--td-text-color-secondary);
  line-height: 20px;
}

.expanded-actions {
  justify-content: flex-end;
}

.expanded-empty {
  color: var(--td-text-color-secondary);
}

.markdown-form-item :deep(.t-form__controls-content) {
  display: block;
}

.markdown-form-item :deep(.md-editor-content) {
  min-height: 480px;
}

.markdown-preview {
  min-height: 320px;
}

.drawer-textarea {
  min-height: 180px;
}

.drawer-textarea--short {
  min-height: 88px;
}

.markdown-preview {
  max-height: 520px;
  padding: 12px;
  overflow: auto;
  background: var(--td-bg-color-page);
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
  word-break: break-word;
}

.detail h3 {
  margin: 14px 0;
}

.detail-markdown {
  min-height: 120px;
  margin-bottom: 14px;
}

.markdown-preview :deep(h1),
.markdown-preview :deep(h2),
.markdown-preview :deep(h3),
.markdown-preview :deep(p),
.markdown-preview :deep(ul) {
  margin-top: 0;
}

.markdown-preview :deep(code) {
  padding: 1px 4px;
  background: var(--td-bg-color-container-hover);
  border-radius: 4px;
}

.markdown-preview :deep(pre) {
  padding: 10px;
  overflow: auto;
  background: var(--td-bg-color-container-hover);
  border-radius: 6px;
}

.detail dl {
  display: grid;
  grid-template-columns: 90px minmax(0, 1fr);
  gap: 10px;
}

.detail-actions {
  margin-top: 20px;
}

.feedback-history,
.review-history {
  margin-top: 20px;
}

.feedback-history h4,
.review-history h4 {
  margin: 0 0 12px;
}

.feedback-history__item,
.review-history__item {
  display: grid;
  grid-template-columns: auto 120px minmax(160px, 1fr);
  gap: 8px 12px;
  align-items: center;
  padding: 12px 0;
  border-top: 1px solid var(--td-component-border);
}

.feedback-history__item p,
.review-history__item p {
  grid-column: 1 / -1;
  margin: 0;
  color: var(--td-text-color-secondary);
}

.feedback-empty {
  color: var(--td-text-color-secondary);
}

.detail dt {
  color: var(--td-text-color-secondary);
}

.detail dd {
  margin: 0;
}

@media (max-width: 960px) {
  .requirement-relation-row {
    grid-template-columns: 1fr;
  }

  .expanded-item {
    grid-template-columns: 1fr;
  }

  .expanded-task-row {
    grid-template-columns: 1fr;
  }

  .expanded-actions {
    justify-content: flex-start;
  }
}
</style>
