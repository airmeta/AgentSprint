<script lang="ts" setup>
import type { SprintMvpApi, SprintUserApi } from '#/api/sprint/mvp';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { computed, onActivated, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import { IconifyIcon } from '@vben/icons';
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
import { listDigitalWorkersApi, type AutomationApi } from '#/api/automation/workers';
import { requiredRule, validateForm } from '#/views/_shared/form-rules';
import { withSerialColumn } from '#/views/_shared/table-columns';
import ProjectSecondaryListShell from '#/components/project-secondary-list-shell/project-secondary-list-shell.vue';

import '../_shared/table-layout.css';

defineOptions({ name: 'SprintTasks' });

const assigning = ref(false);
const loading = ref(false);
const assignVisible = ref(false);
const assignFormRef = ref<FormInstanceFunctions>();
const projects = ref<SprintMvpApi.Project[]>([]);
const requirements = ref<SprintMvpApi.Requirement[]>([]);
const tasks = ref<SprintMvpApi.DevelopmentTask[]>([]);
const users = ref<SprintUserApi.UserOption[]>([]);
const digitalWorkers = ref<AutomationApi.DigitalWorker[]>([]);
const currentTask = ref<SprintMvpApi.DevelopmentTask>();
const initialized = ref(false);
const userStore = useUserStore();
const router = useRouter();

const filters = reactive({
  projectId: '',
  relatedUserId: '',
  requirementKeyword: '',
  status: '',
});
const assignForm = reactive({
  assigneeId: '',
  assigneeType: 0 as 0 | 1,
});
const assignRules: FormRules<typeof assignForm> = {
  assigneeId: requiredRule('请选择研发人员', 'change'),
};
const pagination = reactive({
  current: 1,
  pageSize: 30,
});

const selectedProject = computed(() =>
  projects.value.find((project) => project.id === filters.projectId),
);
const requirementMap = computed(() =>
  Object.fromEntries(requirements.value.map((item) => [item.id, item])),
);
const userOptions = computed(() =>
  users.value.map((user) => ({
    label: `${user.displayName} (${user.username})`,
    value: user.id,
  })),
);
const userMap = computed(() => Object.fromEntries(users.value.map((item) => [item.id, item])));
const workerOptions = computed(() =>
  digitalWorkers.value
    .filter((worker) => worker.status === 'active')
    .map((worker) => ({
      label: `${worker.name} (${worker.code})`,
      value: worker.agentUserId,
    })),
);
const assignAssigneeOptions = computed(() =>
  assignForm.assigneeType === 1 ? workerOptions.value : userOptions.value,
);
const canAssignTask = computed(() =>
  userStore.userRoles.some((role) =>
    ['architect', 'pm', 'project_manager', 'super'].includes(role),
  ),
);

const columns = [
  { colKey: 'title', title: '任务标题' },
  { colKey: 'requirementId', title: '需求', width: 200 },
  { colKey: 'status', title: '状态', width: 130 },
  { colKey: 'relatedUser', title: '关联人员', width: 180 },
  { colKey: 'priority', title: '优先级', width: 90 },
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
  pageSizeOptions: [30, 50, 100, 200],
  showJumper: true,
  showPageSize: true,
  size: 'small' as const,
  total: tasks.value.length,
}));

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

function filterTasksByRequirementKeyword(items: SprintMvpApi.DevelopmentTask[]) {
  const keyword = filters.requirementKeyword.trim().toLowerCase();
  if (!keyword) return items;

  return items.filter((task) => {
    const requirement = requirementMap.value[task.requirementId];
    return [requirement?.title, requirement?.id, task.requirementId]
      .filter(Boolean)
      .some((value) => String(value).toLowerCase().includes(keyword));
  });
}

async function loadBase() {
  [projects.value, requirements.value, users.value, digitalWorkers.value] = await Promise.all([
    listProjectsApi(),
    listRequirementsApi(),
    listUserOptionsApi(),
    listDigitalWorkersApi({ status: 'active' }),
  ]);
  filters.projectId ||= projects.value[0]?.id || '';
}

async function loadTasks() {
  loading.value = true;
  try {
    const taskItems = await listDevelopmentTasksApi({
      projectId: filters.projectId || undefined,
      relatedUserId: canAssignTask.value ? filters.relatedUserId || undefined : undefined,
      status: filters.status || undefined,
    });
    tasks.value = filterTasksByRequirementKeyword(taskItems);
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
    projectId: projects.value[0]?.id || '',
    relatedUserId: '',
    requirementKeyword: '',
    status: '',
  });
  await loadTasks();
}

async function handleProjectChange() {
  filters.requirementKeyword = '';
  await loadTasks();
}

function openAssign(task: SprintMvpApi.DevelopmentTask) {
  currentTask.value = task;
  assignForm.assigneeId = task.assigneeId || '';
  assignForm.assigneeType = task.assigneeType === 1 ? 1 : 0;
  assignVisible.value = true;
}

function handleAssignTypeChange() {
  assignForm.assigneeId = '';
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
      assigneeType: assignForm.assigneeType,
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
  <ProjectSecondaryListShell
    v-model:selected-project-id="filters.projectId"
    class="tasks-page"
    :loading="loading"
    :projects="projects"
    @project-change="handleProjectChange"
    @refresh="loadTasks"
  >
    <template #header>
      <section class="sprint-page-title">
      <h2>任务大厅</h2>
      <p>统一管理需求拆解后的任务，并指派给具体研发人员。</p>
      </section>
    </template>

    <template #workspace-header>
      <div class="workspace-head">
        <div>
          <h3>{{ selectedProject?.name || '请选择项目' }}</h3>
          <p>{{ selectedProject?.code || '-' }}</p>
        </div>
      </div>
    </template>

    <section class="sprint-filter-panel">
      <div class="sprint-filter-grid">
        <label class="sprint-filter-field">
          <span>需求</span>
          <TInput
            v-model="filters.requirementKeyword"
            clearable
            placeholder="输入需求标题或ID"
            @enter="queryTasks"
          />
        </label>
        <label v-if="canAssignTask" class="sprint-filter-field">
          <span>关联人员</span>
          <TSelect
            v-model="filters.relatedUserId"
            clearable
            filterable
            :options="userOptions"
            placeholder="负责人 / 指派人"
          />
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
          <TButton theme="primary" :loading="loading" @click="queryTasks">
            <template #icon>
              <IconifyIcon icon="lucide:search" />
            </template>
            查询
          </TButton>
          <TButton theme="default" :disabled="loading" @click="resetFilters">
            <template #icon>
              <IconifyIcon icon="lucide:rotate-ccw" />
            </template>
            重置
          </TButton>
        </div>
      </div>
    </section>

    <section class="sprint-table-panel">
      <div class="sprint-table-header">
        <h3>任务列表</h3>
        <div class="sprint-table-actions">
          <TButton shape="circle" variant="outline" title="刷新" :loading="loading" @click="loadTasks">
            <IconifyIcon icon="lucide:refresh-cw" />
          </TButton>
        </div>
      </div>

      <TTable
        row-key="id"
        class="sprint-compact-table"
        :columns="withSerialColumn(columns, { offset: () => (pagination.current - 1) * pagination.pageSize })"
        :data="tasks"
        :loading="loading"
        :pagination="tablePagination"
        size="small"
        hover
        stripe
        @page-change="handlePageChange"
      >
        <template #requirementId="{ row }">
          {{ requirementMap[row.requirementId]?.title || row.requirementId }}
        </template>
        <template #status="{ row }">
          <TTag variant="light">{{ statusText[row.status] || row.status }}</TTag>
        </template>
        <template #relatedUser="{ row }">
          <div class="task-related-users">
            <span>负责人：{{ row.assigneeId ? userMap[row.assigneeId]?.displayName || row.assigneeId : '未指派' }}</span>
            <span>指派人：{{ row.assignedBy ? userMap[row.assignedBy]?.displayName || row.assignedBy : '-' }}</span>
          </div>
        </template>
        <template #actions="{ row }">
          <TSpace class="sprint-row-actions">
            <TLink theme="primary" @click="openDetail(row)">
              <IconifyIcon icon="lucide:eye" />
              <span>详情</span>
            </TLink>
            <TLink v-if="canAssignTask" theme="primary" @click="openAssign(row)">
              <IconifyIcon icon="lucide:user-plus" />
              <span>指派</span>
            </TLink>
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
        <TFormItem label="指派类型">
          <TSelect
            v-model="assignForm.assigneeType"
            :options="[
              { label: '员工', value: 0 },
              { label: '数字员工', value: 1 },
            ]"
            @change="handleAssignTypeChange"
          />
        </TFormItem>
        <TFormItem :label="assignForm.assigneeType === 1 ? '数字员工' : '研发人员'" name="assigneeId">
          <TSelect
            v-model="assignForm.assigneeId"
            :options="assignAssigneeOptions"
            filterable
            :placeholder="assignForm.assigneeType === 1 ? '选择数字员工' : '选择研发人员'"
          />
        </TFormItem>
      </TForm>
    </TDrawer>
  </ProjectSecondaryListShell>
</template>

<style scoped>
.task-related-users {
  display: grid;
  gap: 2px;
  color: var(--td-text-color-secondary);
  font-size: 12px;
  line-height: 18px;
}
</style>

