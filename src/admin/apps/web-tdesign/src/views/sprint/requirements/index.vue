<script lang="ts" setup>
import type { SprintMvpApi, SprintUserApi } from '#/api/sprint/mvp';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { computed, onActivated, onMounted, reactive, ref, watch } from 'vue';

import {
  Button as TButton,
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  InputNumber as TInputNumber,
  Link as TLink,
  MessagePlugin,
  Select as TSelect,
  Space as TSpace,
  Table as TTable,
  Tag as TTag,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

import {
  closeRequirementApi,
  completeRequirementDevelopmentApi,
  convertRequirementFeedbackApi,
  createRequirementFeedbackApi,
  createRequirementApi,
  decomposeRequirementApi,
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
import {
  requiredArrayRule,
  requiredRule,
  validateForm,
} from '#/views/_shared/form-rules';

import MarkdownEditor from '../_shared/markdown-editor.vue';
import { renderMarkdown } from '../_shared/markdown';
import '../_shared/table-layout.css';

const loading = ref(false);
const selectedProjectId = ref('');
const selectedRequirement = ref<SprintMvpApi.Requirement>();
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
const requirementFeedback = ref<SprintMvpApi.RequirementFeedback[]>([]);
const requirementFeedbackMap = ref<Record<string, SprintMvpApi.RequirementFeedback[]>>({});
const requirementReviews = ref<SprintMvpApi.RequirementReview[]>([]);
const users = ref<SprintUserApi.UserOption[]>([]);
const selectedFeedback = ref<SprintMvpApi.RequirementFeedback>();
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
  skillIds: [] as string[],
  stakeholders: '',
  title: '',
});
const reviewForm = reactive({
  reviewerIds: [] as string[],
});
const decomposeForm = reactive({
  assignmentMode: 'auto' as 'auto' | 'manual',
  instruction: '',
});
const feedbackForm = reactive({
  content: '',
  title: '',
});
const convertFeedbackForm = reactive({
  description: '',
  priority: 3,
  stakeholders: '',
  title: '',
});
const requirementRules: FormRules<typeof requirementForm> = {
  endpointId: requiredRule('请选择端', 'change'),
  moduleId: requiredRule('请选择功能模块', 'change'),
  priority: requiredRule('请选择优先级', 'change'),
  projectId: requiredRule('请选择所属项目', 'change'),
  title: requiredRule('请输入需求标题'),
};
const reviewRules: FormRules<typeof reviewForm> = {
  reviewerIds: requiredArrayRule('请选择评审人'),
};
const feedbackRules: FormRules<typeof feedbackForm> = {
  title: requiredRule('请输入回馈标题'),
};
const convertFeedbackRules: FormRules<typeof convertFeedbackForm> = {
  priority: requiredRule('请选择优先级', 'change'),
  title: requiredRule('请输入后续需求标题'),
};
const pagination = reactive({
  current: 1,
  pageSize: 10,
});

const projectOptions = computed(() =>
  projects.value.map((project) => ({
    label: `${project.code} · ${project.name}`,
    value: project.id,
  })),
);
const userOptions = computed(() =>
  users.value.map((user) => ({
    label: `${user.displayName} (${user.username})`,
    value: user.id,
  })),
);
const skillOptions = computed(() =>
  skills.value.map((skill) => ({ label: `${skill.code} - ${skill.name}`, value: skill.id })),
);
const userMap = computed(() => Object.fromEntries(users.value.map((item) => [item.id, item])));
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
const visibleRequirementIds = computed(() =>
  new Set(filteredRequirements.value.map((requirement) => requirement.id)),
);

const statusText: Record<string, string> = {
  approved: '评审通过',
  completed: '已完成',
  decomposed: '已拆解',
  developing: '进行中',
  draft: '草稿',
  pending_fix: '待修复',
  pending_review: '待评审',
  ready_development: '待开发',
  ready_test: '待测试',
  rejected: '评审驳回',
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
  success: '健康',
  voided: '已作废',
  warn: '有缺陷',
};
const reviewStatusText: Record<string, string> = {
  approved: '已通过',
  pending: '待评审',
  rejected: '已驳回',
};
const feedbackStatusText: Record<string, string> = {
  closed: '已关闭',
  converted: '已转需求',
  open: '待处理',
};
const decomposeAllowedStatuses = new Set(['approved', 'ready_development', 'decomposed']);
const feedbackAllowedStatuses = new Set(['tested', 'completed']);
const editAllowedStatuses = new Set(['draft']);
const reviewAllowedStatuses = new Set(['draft', 'rejected']);

const columns = [
  { colKey: 'title', ellipsis: true, title: '需求名' },
  { colKey: 'status', title: '状态', width: 120 },
  { colKey: 'health', title: '健康', width: 90 },
  { colKey: 'priority', title: '优先级', width: 90 },
  { colKey: 'createdBy', title: '产品经理', width: 130 },
  { colKey: 'stakeholders', title: '干系人', width: 160 },
  { colKey: 'actions', title: '操作', width: 280 },
];
const statusOptions = computed(() =>
  Object.entries(statusText).map(([value, label]) => ({ label, value })),
);
const healthOptions = computed(() =>
  Object.entries(healthText).map(([value, label]) => ({ label, value })),
);
const filteredRequirements = computed(() => {
  const requirementInfo = filters.requirementInfo.trim().toLowerCase();
  return requirements.value.filter(
    (requirement) =>
      (!requirementInfo ||
        requirement.title.toLowerCase().includes(requirementInfo) ||
        (requirement.description || '').toLowerCase().includes(requirementInfo) ||
        (requirement.stakeholders || '').toLowerCase().includes(requirementInfo)) &&
      (!filters.status || requirement.status === filters.status) &&
      (!filters.health || requirement.health === filters.health),
  );
});

const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  pageSizeOptions: [10, 20, 50],
  showJumper: true,
  showPageSize: true,
  size: 'small' as const,
  total: filteredRequirements.value.length,
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

function getRequirementFollowUpItems(requirement: SprintMvpApi.Requirement) {
  const feedbackItems = getRequirementFeedback(requirement.id).map((feedback) => ({
    content: feedback.content || '暂无内容',
    createdAt: feedback.createTime,
    feedback,
    id: `feedback:${feedback.id}`,
    status: feedback.status,
    title: feedback.title,
    type: 'feedback' as const,
  }));
  const childItems = (childRequirementsBySource.value[requirement.id] || []).map((child) => ({
    child,
    content: child.description || '暂无内容',
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
    skillIds: [...(endpoint?.skillIds || [])],
    stakeholders: '',
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
    skillIds: [...(requirement.skillIds || [])],
    stakeholders: requirement.stakeholders || '',
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
  decomposeForm.assignmentMode = 'auto';
  decomposeForm.instruction = '';
  decomposeVisible.value = true;
}

function openFeedback(requirement: SprintMvpApi.Requirement) {
  if (!canCreateFeedback(requirement)) {
    MessagePlugin.warning('回馈转换出的后续需求不支持再次回馈');
    return;
  }
  selectedRequirement.value = requirement;
  Object.assign(feedbackForm, {
    content: '',
    title: '',
  });
  feedbackVisible.value = true;
}

function openConvertFeedback(feedback: SprintMvpApi.RequirementFeedback) {
  selectedFeedback.value = feedback;
  Object.assign(convertFeedbackForm, {
    description: feedback.content || '',
    priority: selectedRequirement.value?.priority || 3,
    stakeholders: selectedRequirement.value?.stakeholders || '',
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
  openConvertFeedback(feedback);
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
    requirements.value = await listRequirementsApi(selectedProjectId.value || undefined);
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

async function queryRequirements() {
  await loadRequirements();
}

async function resetFilters() {
  selectedProjectId.value = projects.value[0]?.id || '';
  Object.assign(filters, {
    health: '',
    requirementInfo: '',
    status: '',
  });
  await loadRequirements();
}

function handleLocalFilterChange() {
  pagination.current = 1;
}

async function saveRequirement() {
  if (!(await validateForm(requirementFormRef.value))) return;
  if (!requirementForm.projectId) {
    MessagePlugin.warning('请先选择项目');
    return;
  }
  if (!requirementForm.title.trim()) {
    MessagePlugin.warning('需求标题不能为空');
    return;
  }
  if (!requirementForm.endpointId || !requirementForm.moduleId) {
    MessagePlugin.warning('请选择端和功能模块');
    return;
  }

  if (selectedRequirement.value) {
    if (!canEditRequirement(selectedRequirement.value)) {
      MessagePlugin.warning('需求提交评审后不支持编辑');
      editorVisible.value = false;
      return;
    }
    await updateRequirementApi(selectedRequirement.value.id, {
      description: requirementForm.description,
      priority: requirementForm.priority,
      stakeholders: requirementForm.stakeholders,
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
      skillIds: [...requirementForm.skillIds],
      stakeholders: requirementForm.stakeholders,
      title: requirementForm.title,
    });
  }

  MessagePlugin.success('需求已保存');
  editorVisible.value = false;
  if (!selectedRequirement.value && selectedProjectId.value !== requirementForm.projectId) {
    selectedProjectId.value = requirementForm.projectId;
  }
  await loadRequirements();
}

async function submitReview() {
  if (!selectedRequirement.value) return;
  if (!(await validateForm(reviewFormRef.value))) return;
  const reviewerIds = [...reviewForm.reviewerIds];
  if (reviewerIds.length === 0) {
    MessagePlugin.warning('请选择评审人');
    return;
  }

  await submitRequirementReviewApi(selectedRequirement.value.id, { reviewerIds });
  MessagePlugin.success('已提交需求评审');
  reviewVisible.value = false;
  await loadRequirements();
}

async function decomposeRequirement() {
  if (!selectedRequirement.value) return;
  await decomposeRequirementApi(selectedRequirement.value.id, {
    assignmentMode: decomposeForm.assignmentMode,
    instruction: decomposeForm.instruction,
  });
  MessagePlugin.success('任务拆解已生成');
  decomposeVisible.value = false;
  await loadRequirements();
}

async function voidRequirement(requirement: SprintMvpApi.Requirement) {
  await voidRequirementApi(requirement.id);
  MessagePlugin.success('需求已作废');
  detailVisible.value = false;
  await loadRequirements();
}

async function closeRequirement(requirement: SprintMvpApi.Requirement) {
  await closeRequirementApi(requirement.id);
  MessagePlugin.success('需求已验收关闭');
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
  if (!selectedRequirement.value) return;
  if (!(await validateForm(feedbackFormRef.value))) return;
  if (!feedbackForm.title.trim()) {
    MessagePlugin.warning('回馈标题不能为空');
    return;
  }

  await createRequirementFeedbackApi(selectedRequirement.value.id, {
    content: feedbackForm.content,
    title: feedbackForm.title,
  });
  MessagePlugin.success('回馈已记录');
  feedbackVisible.value = false;
  requirementFeedback.value = await listRequirementFeedbackApi(selectedRequirement.value.id);
  requirementFeedbackMap.value = {
    ...requirementFeedbackMap.value,
    [selectedRequirement.value.id]: requirementFeedback.value,
  };
}

async function convertFeedback() {
  if (!selectedRequirement.value || !selectedFeedback.value) return;
  if (!(await validateForm(convertFeedbackFormRef.value))) return;
  if (!convertFeedbackForm.title.trim()) {
    MessagePlugin.warning('后续需求标题不能为空');
    return;
  }

  await convertRequirementFeedbackApi(
    selectedRequirement.value.id,
    selectedFeedback.value.id,
    {
      description: convertFeedbackForm.description,
      priority: convertFeedbackForm.priority,
      stakeholders: convertFeedbackForm.stakeholders,
      title: convertFeedbackForm.title,
    },
  );
  MessagePlugin.success('回馈已转为后续需求');
  convertFeedbackVisible.value = false;
  requirementFeedback.value = await listRequirementFeedbackApi(selectedRequirement.value.id);
  requirementFeedbackMap.value = {
    ...requirementFeedbackMap.value,
    [selectedRequirement.value.id]: requirementFeedback.value,
  };
  await loadRequirements();
}

onMounted(async () => {
  users.value = await listUserOptionsApi();
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
  expandedRequirementIds.value = [];
  await loadRequirements();
});

watch(filteredRequirements, syncExpandedRequirementIds);

onActivated(async () => {
  await loadRequirements();
});
</script>

<template>
  <div class="requirements-page sprint-list-page">
    <section class="sprint-page-title">
      <h2>需求管理</h2>
      <p>维护需求、评审、拆解、测试闭环和验收后的产品回馈。</p>
    </section>

    <section class="sprint-filter-panel">
      <div class="sprint-filter-grid">
        <label class="sprint-filter-field">
          <span>当前项目</span>
          <TSelect
            v-model="selectedProjectId"
            :options="projectOptions"
            clearable
            placeholder="全部项目"
          />
        </label>
        <label class="sprint-filter-field">
          <span>状态</span>
          <TSelect
            v-model="filters.status"
            :options="statusOptions"
            clearable
            placeholder="全部状态"
            @change="handleLocalFilterChange"
          />
        </label>
        <label class="sprint-filter-field">
          <span>健康</span>
          <TSelect
            v-model="filters.health"
            :options="healthOptions"
            clearable
            placeholder="全部健康状态"
            @change="handleLocalFilterChange"
          />
        </label>
        <label class="sprint-filter-field">
          <span>需求信息</span>
          <TInput
            v-model="filters.requirementInfo"
            clearable
            placeholder="需求名、内容、干系人"
            @change="handleLocalFilterChange"
          />
        </label>
        <div class="sprint-filter-actions">
          <TButton theme="primary" @click="queryRequirements">查询</TButton>
          <TButton variant="outline" @click="resetFilters">重置</TButton>
        </div>
      </div>
    </section>

    <section class="sprint-table-panel">
      <div class="sprint-table-header">
        <h3>{{ selectedProjectName || '需求列表' }}</h3>
        <div class="sprint-table-actions">
          <TButton shape="circle" variant="outline" title="刷新" @click="loadRequirements">↻</TButton>
          <TButton theme="primary" @click="openCreate">新增需求</TButton>
        </div>
      </div>

      <TTable
        row-key="id"
        class="sprint-compact-table"
        :columns="columns"
        :data="filteredRequirements"
        :expand-on-row-click="false"
        :expanded-row-keys="expandedRequirementIds"
        :loading="loading"
        :pagination="tablePagination"
        size="small"
        hover
        @expand-change="handleExpandedRowKeysChange"
        @page-change="handlePageChange"
      >
        <template #expandedRow="{ row }">
          <div class="requirement-expanded">
            <h4>回馈与子需求</h4>
            <div v-if="getRequirementFollowUpItems(row).length === 0" class="expanded-empty">
              暂无回馈与子需求
            </div>
            <div
              v-for="item in getRequirementFollowUpItems(row)"
              :key="item.id"
              class="expanded-item"
            >
              <TTag :theme="item.type === 'feedback' ? 'warning' : 'primary'" variant="light">
                {{ item.type === 'feedback' ? '回馈' : '需求' }}
              </TTag>
              <TTag variant="light">
                {{
                  item.type === 'feedback'
                    ? feedbackStatusText[item.status] || item.status
                    : statusText[item.status] || item.status
                }}
              </TTag>
              <strong>{{ item.title }}</strong>
              <span>{{ item.createdAt }}</span>
              <TSpace class="sprint-row-actions expanded-actions">
                <TLink
                  v-if="item.type === 'feedback' && item.feedback.status === 'open'"
                  theme="primary"
                  @click="openConvertFeedbackFromRequirement(row, item.feedback)"
                >
                  转需求
                </TLink>
                <TLink
                  v-if="item.type === 'requirement' && canSubmitReview(item.child)"
                  theme="primary"
                  @click="openReview(item.child)"
                >
                  提交评审
                </TLink>
                <TLink
                  v-if="item.type === 'requirement' && decomposeAllowedStatuses.has(item.child.status)"
                  theme="primary"
                  @click="openDecompose(item.child)"
                >
                  任务拆解
                </TLink>
                <TLink
                  v-if="
                    item.type === 'requirement' &&
                    (item.child.status === 'developing' || item.child.status === 'pending_fix')
                  "
                  theme="success"
                  @click="completeRequirementDevelopment(item.child)"
                >
                  完成开发
                </TLink>
                <TLink
                  v-if="item.type === 'requirement' && item.child.status === 'tested'"
                  theme="success"
                  @click="closeRequirement(item.child)"
                >
                  验收关闭
                </TLink>
                <TLink
                  v-if="item.type === 'requirement'"
                  theme="primary"
                  @click="openDetail(item.child)"
                >
                  详情
                </TLink>
              </TSpace>
              <p>{{ item.content }}</p>
            </div>
          </div>
        </template>
        <template #status="{ row }">
          <TTag variant="light">{{ statusText[row.status] || row.status }}</TTag>
        </template>
        <template #health="{ row }">
          <TTag :theme="healthTheme[row.health] || 'primary'" variant="light">
            {{ healthText[row.health] || row.health }}
          </TTag>
        </template>
        <template #actions="{ row }">
          <TSpace class="sprint-row-actions">
            <TLink v-if="canEditRequirement(row)" theme="primary" @click="openEdit(row)">
              编辑
            </TLink>
            <TLink v-else theme="primary" @click="openDetail(row)">详情</TLink>
            <TLink
              v-if="decomposeAllowedStatuses.has(row.status)"
              theme="primary"
              @click="openDecompose(row)"
            >
              任务拆解
            </TLink>
            <TLink v-if="canSubmitReview(row)" theme="primary" @click="openReview(row)">
              立项推进
            </TLink>
            <TLink v-if="row.status === 'rejected'" theme="danger" @click="voidRequirement(row)">
              作废
            </TLink>
            <TLink v-if="row.status === 'tested'" theme="success" @click="closeRequirement(row)">
              验收关闭
            </TLink>
            <TLink v-if="canCreateFeedback(row)" theme="primary" @click="openFeedback(row)">
              记录回馈
            </TLink>
          </TSpace>
        </template>
      </TTable>
    </section>

    <TDrawer
      v-model:visible="editorVisible"
      :size="'60%'"
      :header="selectedRequirement ? '编辑需求' : '新增需求'"
      @confirm="saveRequirement"
    >
      <TForm ref="requirementFormRef" :data="requirementForm" :rules="requirementRules" label-width="90px">
        <TFormItem label="Skill">
          <TSelect v-model="requirementForm.skillIds" multiple filterable :options="skillOptions" />
        </TFormItem>
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
        <TFormItem label="功能模块" name="moduleId">
          <TSelect
            v-model="requirementForm.moduleId"
            :disabled="!!selectedRequirement"
            :options="moduleOptions"
            placeholder="请选择功能模块"
          />
        </TFormItem>
        <TFormItem label="需求标题" name="title">
          <TInput v-model="requirementForm.title" />
        </TFormItem>
        <TFormItem label="优先级" name="priority">
          <TInputNumber v-model="requirementForm.priority" :min="1" :max="5" />
        </TFormItem>
        <TFormItem label="干系人">
          <TInput v-model="requirementForm.stakeholders" placeholder="admin,pm-1,arch-1" />
        </TFormItem>
        <TFormItem label="需求内容" class="markdown-form-item">
          <MarkdownEditor
            v-model="requirementForm.description"
            :height="420"
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
          <dt>产品经理</dt>
          <dd>{{ selectedRequirement.createdBy }}</dd>
          <dt>干系人</dt>
          <dd>{{ selectedRequirement.stakeholders || '未填写' }}</dd>
        </dl>
        <section v-if="requirementReviews.length > 0" class="review-history">
          <h4>评审记录</h4>
          <div
            v-for="review in requirementReviews"
            :key="review.id"
            class="review-history__item"
          >
            <TTag variant="light">{{ reviewStatusText[review.status] || review.status }}</TTag>
            <strong>
              {{ userMap[review.reviewerId]?.displayName || review.reviewerId }}
            </strong>
            <span>{{ review.reviewedAt || review.createTime }}</span>
            <p>{{ review.comment || '暂无意见' }}</p>
          </div>
        </section>
        <section class="feedback-history">
          <h4>产品回馈</h4>
          <div v-if="requirementFeedback.length === 0" class="feedback-empty">
            暂无回馈
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
            <span>{{ feedback.createTime }}</span>
            <p>{{ feedback.content || '暂无内容' }}</p>
            <TButton
              v-if="feedback.status === 'open'"
              size="small"
              theme="primary"
              variant="outline"
              @click="openConvertFeedback(feedback)"
            >
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
              编辑
            </TButton>
            <TButton
              v-if="decomposeAllowedStatuses.has(selectedRequirement.status)"
              theme="primary"
              @click="openDecompose(selectedRequirement)"
            >
              任务拆解
            </TButton>
            <TButton
              v-if="canSubmitReview(selectedRequirement)"
              theme="primary"
              @click="openReview(selectedRequirement)"
            >
              立项推进
            </TButton>
            <TButton
              v-if="selectedRequirement.status === 'rejected'"
              theme="danger"
              @click="voidRequirement(selectedRequirement)"
            >
              作废需求
            </TButton>
            <TButton
              v-if="selectedRequirement.status === 'tested'"
              theme="success"
              @click="closeRequirement(selectedRequirement)"
            >
              验收关闭
            </TButton>
            <TButton
              v-if="canCreateFeedback(selectedRequirement)"
              theme="primary"
              variant="outline"
              @click="openFeedback(selectedRequirement)"
            >
              记录回馈
            </TButton>
          </TSpace>
        </div>
      </article>
    </TDrawer>

    <TDrawer
      v-model:visible="reviewVisible"
      :size="'40%'"
      header="提交需求评审"
      confirm-btn="提交"
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
      :size="'40%'"
      header="AI 任务拆解"
      confirm-btn="生成任务"
      @confirm="decomposeRequirement"
    >
      <TForm :data="decomposeForm" label-width="90px">
        <TFormItem label="任务分派">
          <TSelect
            v-model="decomposeForm.assignmentMode"
            :options="[
              { label: '自动分派', value: 'auto' },
              { label: '手动指派', value: 'manual' },
            ]"
          />
        </TFormItem>
      </TForm>
      <TTextarea
        v-model="decomposeForm.instruction"
        class="drawer-textarea"
        placeholder="填写拆解补充要求，留空则按需求内容生成默认任务。"
      />
    </TDrawer>

    <TDrawer
      v-model:visible="feedbackVisible"
      :size="'40%'"
      header="记录产品回馈"
      confirm-btn="保存"
      @confirm="saveFeedback"
    >
      <TForm ref="feedbackFormRef" :data="feedbackForm" :rules="feedbackRules" label-width="90px">
        <TFormItem label="标题" name="title">
          <TInput v-model="feedbackForm.title" />
        </TFormItem>
        <TFormItem label="内容">
          <TTextarea
            v-model="feedbackForm.content"
            class="drawer-textarea"
            placeholder="记录验收后的新想法、补充范围或优化建议"
          />
        </TFormItem>
      </TForm>
    </TDrawer>

    <TDrawer
      v-model:visible="convertFeedbackVisible"
      :size="'60%'"
      header="转为后续需求"
      confirm-btn="创建草稿"
      @confirm="convertFeedback"
    >
      <TForm
        ref="convertFeedbackFormRef"
        :data="convertFeedbackForm"
        :rules="convertFeedbackRules"
        label-width="90px"
      >
        <TFormItem label="标题" name="title">
          <TInput v-model="convertFeedbackForm.title" />
        </TFormItem>
        <TFormItem label="优先级" name="priority">
          <TInputNumber v-model="convertFeedbackForm.priority" :min="1" :max="5" />
        </TFormItem>
        <TFormItem label="干系人">
          <TInput v-model="convertFeedbackForm.stakeholders" placeholder="admin,pm-1,arch-1" />
        </TFormItem>
        <TFormItem label="需求内容" class="markdown-form-item">
          <MarkdownEditor
            v-model="convertFeedbackForm.description"
            :height="360"
            placeholder="后续需求会保留来源需求和来源回馈"
          />
        </TFormItem>
      </TForm>
    </TDrawer>
  </div>
</template>

<style scoped>
.requirements-page {
  display: flex;
  flex-direction: column;
}

.requirement-expanded {
  display: grid;
  gap: 10px;
  padding: 12px 16px;
  background: var(--td-bg-color-page);
}

.requirement-expanded h4 {
  margin: 0 0 10px;
  font-size: 14px;
  line-height: 20px;
}

.expanded-item {
  display: grid;
  grid-template-columns: auto auto minmax(180px, 1fr) auto auto;
  gap: 8px 10px;
  align-items: center;
  padding: 8px 0;
  border-top: 1px solid var(--td-component-border);
}

.expanded-item p {
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

.markdown-preview {
  min-height: 320px;
}

.drawer-textarea {
  min-height: 180px;
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
  .expanded-item {
    grid-template-columns: 1fr;
  }

  .expanded-actions {
    justify-content: flex-start;
  }
}
</style>
