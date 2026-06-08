<script lang="ts" setup>
import type { SprintMvpApi, SprintUserApi } from '#/api/sprint/mvp';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { computed, onActivated, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useUserStore } from '@vben/stores';

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
  Table as TTable,
  Tag as TTag,
} from 'tdesign-vue-next';

import {
  assignDevelopmentTaskApi,
  listDevelopmentTasksApi,
  listProjectsApi,
  listRequirementsApi,
  listUserOptionsApi,
} from '#/api/sprint/mvp';
import { requiredRule, validateForm } from '#/views/_shared/form-rules';

import '../_shared/table-layout.css';

const assigning = ref(false);
const loading = ref(false);
const assignVisible = ref(false);
const assignFormRef = ref<FormInstanceFunctions>();
const projects = ref<SprintMvpApi.Project[]>([]);
const requirements = ref<SprintMvpApi.Requirement[]>([]);
const tasks = ref<SprintMvpApi.DevelopmentTask[]>([]);
const users = ref<SprintUserApi.UserOption[]>([]);
const currentTask = ref<SprintMvpApi.DevelopmentTask>();
const initialized = ref(false);
const userStore = useUserStore();
const router = useRouter();

const filters = reactive({
  assigneeId: '',
  projectId: '',
  requirementId: '',
  status: '',
});
const assignForm = reactive({
  assigneeId: '',
});
const assignRules: FormRules<typeof assignForm> = {
  assigneeId: requiredRule('请选择开发人员', 'change'),
};
const pagination = reactive({
  current: 1,
  pageSize: 10,
});

const projectOptions = computed(() =>
  projects.value.map((item) => ({ label: `${item.code} · ${item.name}`, value: item.id })),
);
const requirementOptions = computed(() =>
  requirements.value
    .filter((item) => !filters.projectId || item.projectId === filters.projectId)
    .map((item) => ({ label: item.title, value: item.id })),
);
const requirementMap = computed(() =>
  Object.fromEntries(requirements.value.map((item) => [item.id, item])),
);
const projectMap = computed(() => Object.fromEntries(projects.value.map((item) => [item.id, item])));
const userOptions = computed(() =>
  users.value.map((user) => ({
    label: `${user.displayName} (${user.username})`,
    value: user.id,
  })),
);
const userMap = computed(() => Object.fromEntries(users.value.map((item) => [item.id, item])));
const canAssignTask = computed(() =>
  userStore.userRoles.some((role) =>
    ['architect', 'pm', 'project_manager', 'super'].includes(role),
  ),
);

const columns = [
  { colKey: 'title', title: '任务标题' },
  { colKey: 'projectId', title: '项目', width: 160 },
  { colKey: 'requirementId', title: '需求', width: 200 },
  { colKey: 'status', title: '状态', width: 130 },
  { colKey: 'priority', title: '优先级', width: 90 },
  { colKey: 'assigneeId', title: '负责人', width: 140 },
  { colKey: 'assignedBy', title: '指派人', width: 140 },
  { colKey: 'actions', title: '操作', width: 160 },
];

const statusText: Record<string, string> = {
  assigned: '已指派',
  completed: '已完成',
  in_progress: '推进中',
  pending_assign: '待指派',
};
const statusOptions = [
  { label: '待指派', value: 'pending_assign' },
  { label: '已指派', value: 'assigned' },
  { label: '推进中', value: 'in_progress' },
  { label: '已完成', value: 'completed' },
];
const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  pageSizeOptions: [10, 20, 50],
  showJumper: true,
  showPageSize: true,
  size: 'small' as const,
  total: tasks.value.length,
}));

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

async function loadBase() {
  [projects.value, requirements.value, users.value] = await Promise.all([
    listProjectsApi(),
    listRequirementsApi(),
    listUserOptionsApi(),
  ]);
}

async function loadTasks() {
  loading.value = true;
  try {
    tasks.value = await listDevelopmentTasksApi({
      assigneeId: canAssignTask.value ? filters.assigneeId || undefined : undefined,
      projectId: filters.projectId || undefined,
      requirementId: filters.requirementId || undefined,
      status: filters.status || undefined,
    });
    pagination.current = 1;
  } finally {
    loading.value = false;
  }
}

async function queryTasks() {
  await loadTasks();
}

async function resetFilters() {
  Object.assign(filters, {
    assigneeId: '',
    projectId: '',
    requirementId: '',
    status: '',
  });
  await loadTasks();
}

function handleProjectFilterChange() {
  filters.requirementId = '';
}

function openAssign(task: SprintMvpApi.DevelopmentTask) {
  currentTask.value = task;
  assignForm.assigneeId = task.assigneeId || '';
  assignVisible.value = true;
}

function openDetail(task: SprintMvpApi.DevelopmentTask) {
  router.push(`/sprint/tasks/detail/${task.id}`);
}

async function assignTask() {
  if (assigning.value) return;
  if (!canAssignTask.value) {
    MessagePlugin.warning('当前角色不能指派任务');
    assignVisible.value = false;
    return;
  }
  if (!currentTask.value || !assignForm.assigneeId.trim()) {
    await validateForm(assignFormRef.value);
    MessagePlugin.warning('负责人不能为空');
    return;
  }
  if (!(await validateForm(assignFormRef.value))) return;

  assigning.value = true;
  try {
    await assignDevelopmentTaskApi(currentTask.value.id, {
      assigneeId: assignForm.assigneeId.trim(),
    });
    MessagePlugin.success('任务已指派');
    assignVisible.value = false;
    await loadTasks();
  } finally {
    assigning.value = false;
  }
}

async function refreshPage() {
  await loadBase();
  await loadTasks();
}

onMounted(async () => {
  await refreshPage();
  initialized.value = true;
});

onActivated(async () => {
  if (initialized.value) {
    await refreshPage();
  }
});
</script>

<template>
  <div class="tasks-page sprint-list-page">
    <section class="sprint-page-title">
      <h2>任务大厅</h2>
      <p>统一管理需求拆解后的任务，并指派给具体开发人员。</p>
    </section>

    <section class="sprint-filter-panel">
      <div class="sprint-filter-grid">
        <label class="sprint-filter-field">
          <span>项目</span>
          <TSelect
            v-model="filters.projectId"
            clearable
            :options="projectOptions"
            placeholder="全部项目"
            @change="handleProjectFilterChange"
          />
        </label>
        <label class="sprint-filter-field">
          <span>需求</span>
          <TSelect
            v-model="filters.requirementId"
            clearable
            :options="requirementOptions"
            placeholder="全部需求"
          />
        </label>
        <label v-if="canAssignTask" class="sprint-filter-field">
          <span>负责人</span>
          <TInput v-model="filters.assigneeId" clearable placeholder="负责人 ID" />
        </label>
        <div v-else class="sprint-filter-field">
          <span>范围</span>
          <TTag theme="primary" variant="light">仅显示我的任务</TTag>
        </div>
        <label class="sprint-filter-field">
          <span>状态</span>
          <TSelect
            v-model="filters.status"
            clearable
            :options="statusOptions"
            placeholder="全部状态"
          />
        </label>
        <div class="sprint-filter-actions">
          <TButton theme="primary" :loading="loading" @click="queryTasks">查询</TButton>
          <TButton variant="outline" :disabled="loading" @click="resetFilters">重置</TButton>
        </div>
      </div>
    </section>

    <section class="sprint-table-panel">
      <div class="sprint-table-header">
        <h3>任务列表</h3>
        <div class="sprint-table-actions">
          <TButton :loading="loading" @click="loadTasks">刷新</TButton>
        </div>
      </div>

      <TTable
        row-key="id"
        class="sprint-compact-table"
        :columns="columns"
        :data="tasks"
        :loading="loading"
        :pagination="tablePagination"
        size="small"
        hover
        @page-change="handlePageChange"
      >
        <template #projectId="{ row }">
          {{ projectMap[row.projectId]?.name || row.projectId }}
        </template>
        <template #requirementId="{ row }">
          {{ requirementMap[row.requirementId]?.title || row.requirementId }}
        </template>
        <template #status="{ row }">
          <TTag variant="light">{{ statusText[row.status] || row.status }}</TTag>
        </template>
        <template #assigneeId="{ row }">
          {{ row.assigneeId ? userMap[row.assigneeId]?.displayName || row.assigneeId : '未指派' }}
        </template>
        <template #assignedBy="{ row }">
          {{ row.assignedBy ? userMap[row.assignedBy]?.displayName || row.assignedBy : '-' }}
        </template>
        <template #actions="{ row }">
          <TSpace class="sprint-row-actions">
            <TLink theme="primary" @click="openDetail(row)">详情</TLink>
            <TLink v-if="canAssignTask" theme="primary" @click="openAssign(row)">指派</TLink>
          </TSpace>
        </template>
      </TTable>
    </section>

    <TDrawer
      v-model:visible="assignVisible"
      :size="'40%'"
      header="任务指派"
      :confirm-btn="{ content: '保存', loading: assigning }"
      @confirm="assignTask"
    >
      <TForm ref="assignFormRef" :data="assignForm" :rules="assignRules" label-width="80px">
        <TFormItem label="开发人员" name="assigneeId">
          <TSelect
            v-model="assignForm.assigneeId"
            :options="userOptions"
            filterable
            placeholder="选择开发人员"
          />
        </TFormItem>
      </TForm>
    </TDrawer>

  </div>
</template>

<style scoped>
</style>
