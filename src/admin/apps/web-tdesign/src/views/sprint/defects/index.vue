<script lang="ts" setup>
import type { SprintMvpApi, SprintTestApi } from '#/api/sprint/mvp';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { computed, onMounted, reactive, ref } from 'vue';

import {
  Button as TButton,
  Descriptions as TDescriptions,
  DescriptionsItem as TDescriptionsItem,
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
  Textarea as TTextarea,
} from 'tdesign-vue-next';

import {
  claimBugApi,
  closeBugApi,
  createBugApi,
  fixBugApi,
  listBugsApi,
  listProjectsApi,
  listRequirementsApi,
  listTestExecutionsApi,
} from '#/api/sprint/mvp';
import { requiredRule, validateForm } from '#/views/_shared/form-rules';
import { formatDateTime } from '#/views/_shared/date-format';
import { withSerialColumn } from '#/views/_shared/table-columns';
import ProjectSecondaryListShell from '#/components/project-secondary-list-shell/project-secondary-list-shell.vue';

import '../_shared/table-layout.css';

defineOptions({ name: 'SprintDefects' });

const creating = ref(false);
const loading = ref(false);
const createVisible = ref(false);
const createFormRef = ref<FormInstanceFunctions>();
const detailVisible = ref(false);
const projects = ref<SprintMvpApi.Project[]>([]);
const requirements = ref<SprintMvpApi.Requirement[]>([]);
const defects = ref<SprintMvpApi.Bug[]>([]);
const currentDefect = ref<SprintMvpApi.Bug>();
const defectExecutions = ref<SprintTestApi.TestExecution[]>([]);

const filters = reactive({
  projectId: '',
  requirementId: '',
});
const form = reactive({
  description: '',
  environment: 'test',
  projectId: '',
  requirementId: '',
  severity: 'major',
  title: '',
});
const defectRules: FormRules<typeof form> = {
  projectId: requiredRule('请选择项目', 'change'),
  requirementId: requiredRule('请选择需求', 'change'),
  title: requiredRule('请输入缺陷标题'),
};
const pagination = reactive({
  current: 1,
  pageSize: 10,
});

const projectOptions = computed(() =>
  projects.value.map((item) => ({ label: `${item.code} · ${item.name}`, value: item.id })),
);
const selectedProject = computed(() =>
  projects.value.find((project) => project.id === filters.projectId),
);
const requirementOptions = computed(() =>
  requirements.value
    .filter((item) => !filters.projectId || item.projectId === filters.projectId)
    .map((item) => ({ label: item.title, value: item.id })),
);
const formRequirementOptions = computed(() =>
  requirements.value
    .filter((item) => !form.projectId || item.projectId === form.projectId)
    .map((item) => ({ label: item.title, value: item.id })),
);
const requirementMap = computed(() =>
  Object.fromEntries(requirements.value.map((item) => [item.id, item])),
);
const projectMap = computed(() => Object.fromEntries(projects.value.map((item) => [item.id, item])));

const columns = [
  { colKey: 'title', title: '缺陷标题' },
  { colKey: 'projectId', title: '项目', width: 160 },
  { colKey: 'requirementId', title: '需求', width: 200 },
  { colKey: 'environment', title: '环境', width: 100 },
  { colKey: 'severity', title: '严重级别', width: 110 },
  { colKey: 'status', title: '状态', width: 130 },
  { colKey: 'createdBy', title: '提交人', width: 130 },
  { colKey: 'developerId', title: '处理人', width: 130 },
  { colKey: 'actions', title: '操作', width: 190 },
];
const executionColumns = [
  { colKey: 'result', title: '结果', width: 100 },
  { colKey: 'testerId', title: '执行人', width: 130 },
  { colKey: 'actualResult', title: '实际结果' },
  { colKey: 'evidence', title: '证据', width: 180 },
  { colKey: 'executedAt', title: '执行时间', width: 190 },
];

const statusText: Record<string, string> = {
  closed: '已关闭',
  fixed_ready_regression: '已修复待回归',
  fixing: '修复中',
  open: '未修复',
};
const severityOptions = [
  { label: '严重', value: 'critical' },
  { label: '主要', value: 'major' },
  { label: '次要', value: 'minor' },
  { label: '轻微', value: 'trivial' },
];
const severityText: Record<string, string> = {
  critical: '严重',
  major: '主要',
  minor: '次要',
  trivial: '轻微',
};
const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  pageSizeOptions: [10, 20, 50],
  showJumper: true,
  showPageSize: true,
  size: 'small' as const,
  total: defects.value.length,
}));

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

function openCreate() {
  const projectId = filters.projectId || projects.value[0]?.id || '';
  Object.assign(form, {
    description: '',
    environment: 'test',
    projectId,
    requirementId: '',
    severity: 'major',
    title: '',
  });
  form.requirementId =
    filters.requirementId &&
    requirements.value.some(
      (item) => item.id === filters.requirementId && item.projectId === form.projectId,
    )
      ? filters.requirementId
      : formRequirementOptions.value[0]?.value || '';
  createVisible.value = true;
}

function handleFormProjectChange() {
  form.requirementId = formRequirementOptions.value[0]?.value || '';
}

async function openDetail(defect: SprintMvpApi.Bug) {
  currentDefect.value = defect;
  const executions = defect.testPlanId ? await listTestExecutionsApi(defect.testPlanId) : [];
  defectExecutions.value = executions.filter(
    (execution) => execution.bugId === defect.id || execution.createdBugId === defect.id,
  );
  detailVisible.value = true;
}

async function loadBase() {
  projects.value = await listProjectsApi();
  requirements.value = await listRequirementsApi();
  filters.projectId ||= projects.value[0]?.id || '';
}

async function loadDefects() {
  loading.value = true;
  try {
    defects.value = await listBugsApi(
      filters.projectId || undefined,
      filters.requirementId || undefined,
    );
    pagination.current = 1;
  } finally {
    loading.value = false;
  }
}

async function handleProjectChange() {
  filters.requirementId = '';
  await loadDefects();
}

async function queryDefects() {
  await loadDefects();
}

async function resetFilters() {
  Object.assign(filters, {
    projectId: projects.value[0]?.id || '',
    requirementId: '',
  });
  await loadDefects();
}

async function createDefect() {
  if (creating.value) return;
  if (!(await validateForm(createFormRef.value))) return;
  if (!form.projectId || !form.requirementId || !form.title.trim()) {
    MessagePlugin.warning('项目、需求和缺陷标题不能为空');
    return;
  }

  creating.value = true;
  try {
    await createBugApi({
      description: form.description,
      environment: form.environment,
      projectId: form.projectId,
      requirementId: form.requirementId,
      severity: form.severity,
      title: form.title,
    });
    MessagePlugin.success('缺陷已提交');
    Object.assign(form, {
      description: '',
      environment: 'test',
      projectId: '',
      requirementId: '',
      severity: 'major',
      title: '',
    });
    createVisible.value = false;
    await loadDefects();
  } finally {
    creating.value = false;
  }
}

async function claimDefect(defect: SprintMvpApi.Bug) {
  await claimBugApi(defect.id, { ownerDevice: 'admin-web' });
  MessagePlugin.success('缺陷已领取，进入修复中');
  await loadDefects();
}

async function fixDefect(defect: SprintMvpApi.Bug) {
  await fixBugApi(defect.id);
  MessagePlugin.success('缺陷已标记为待回归');
  await loadDefects();
}

async function closeDefect(defect: SprintMvpApi.Bug) {
  await closeBugApi(defect.id);
  MessagePlugin.success('缺陷已关闭');
  await loadDefects();
}

onMounted(async () => {
  await loadBase();
  await loadDefects();
});
</script>

<template>
  <ProjectSecondaryListShell
    v-model:selected-project-id="filters.projectId"
    class="defects-page"
    :loading="loading"
    :projects="projects"
    @project-change="handleProjectChange"
    @refresh="loadDefects"
  >
    <template #header><section class="sprint-page-title">
      <h2>缺陷管理</h2>
      <p>缺陷必须绑定项目和具体需求，需求列表健康状态会随缺陷变化。</p>
    </section></template><template #workspace-header><div class="workspace-head"><div><h3>{{ selectedProject?.name || '请选择项目' }}</h3><p>{{ selectedProject?.code || '-' }}</p></div><TButton theme="primary" :disabled="!filters.projectId" @click="openCreate"><template #icon><IconifyIcon icon="lucide:bug" /></template>提交缺陷</TButton></div></template>

    <section class="sprint-filter-panel">
      <div class="sprint-filter-grid">
        <label class="sprint-filter-field">
          <span>需求</span>
          <TSelect
            v-model="filters.requirementId"
            clearable
            :options="requirementOptions"
            placeholder="全部需求"
          />
        </label>
        <div class="sprint-filter-actions">
          <TButton theme="primary" :loading="loading" @click="queryDefects">
            <template #icon>
              <IconifyIcon icon="lucide:search" />
            </template>
            查询
          </TButton>
          <TButton :disabled="loading" @click="resetFilters">
            <template #icon>
              <IconifyIcon icon="lucide:refresh-cw" />
            </template>
            重置
          </TButton>
        </div>
      </div>
    </section>

    <section class="sprint-table-panel">
      <div class="sprint-table-header">
        <h3>缺陷列表</h3>
        <div class="sprint-table-actions">
          <TButton shape="circle" variant="outline" title="刷新" :loading="loading" @click="loadDefects">
            <IconifyIcon icon="lucide:refresh-cw" />
          </TButton>
          <TButton theme="primary" @click="openCreate">
            <template #icon>
              <IconifyIcon icon="lucide:bug" />
            </template>
            提交缺陷
          </TButton>
        </div>
      </div>

      <TTable
        row-key="id"
        class="sprint-compact-table"
        :columns="withSerialColumn(columns, { offset: () => (pagination.current - 1) * pagination.pageSize })"
        :data="defects"
        :loading="loading"
        :pagination="tablePagination"
        size="small"
        hover
        stripe
        @page-change="handlePageChange"
      >
        <template #projectId="{ row }">
          {{ projectMap[row.projectId]?.name || row.projectId }}
        </template>
        <template #requirementId="{ row }">
          {{ requirementMap[row.requirementId]?.title || row.requirementId }}
        </template>
        <template #status="{ row }">
          <TTag theme="warning" variant="light">{{ statusText[row.status] || row.status }}</TTag>
        </template>
        <template #severity="{ row }">
          <TTag variant="light">{{ severityText[row.severity] || row.severity }}</TTag>
        </template>
        <template #developerId="{ row }">
          {{ row.developerId || '未指派' }}
        </template>
        <template #actions="{ row }">
          <TSpace class="sprint-row-actions">
            <TLink theme="primary" @click="openDetail(row)">
              <IconifyIcon icon="lucide:eye" />
              <span>详情</span>
            </TLink>
            <TLink v-if="row.status === 'open'" theme="primary" @click="claimDefect(row)">
              <IconifyIcon icon="lucide:handshake" />
              <span>领取</span>
            </TLink>
            <TLink v-if="row.status === 'fixing'" theme="primary" @click="fixDefect(row)">
              <IconifyIcon icon="lucide:wrench" />
              <span>修复</span>
            </TLink>
            <TLink
              v-if="row.status === 'fixed_ready_regression'"
              theme="primary"
              @click="closeDefect(row)"
            >
              <IconifyIcon icon="lucide:check" />
              <span>关闭</span>
            </TLink>
          </TSpace>
        </template>
      </TTable>
    </section>

    <TDrawer
      v-model:visible="createVisible"
      :size="'60%'"
      header="提交缺陷"
      :confirm-btn="{ content: '保存', loading: creating }"
      @confirm="createDefect"
    >
      <TForm ref="createFormRef" :data="form" :rules="defectRules" label-width="80px">
        <TFormItem label="项目" name="projectId">
          <TSelect
            v-model="form.projectId"
            :options="projectOptions"
            @change="handleFormProjectChange"
          />
        </TFormItem>
        <TFormItem label="需求" name="requirementId">
          <TSelect v-model="form.requirementId" :options="formRequirementOptions" />
        </TFormItem>
        <TFormItem label="标题" name="title">
          <TInput v-model="form.title" placeholder="输入缺陷标题" />
        </TFormItem>
        <TFormItem label="环境">
          <TInput v-model="form.environment" />
        </TFormItem>
        <TFormItem label="严重级别">
          <TSelect v-model="form.severity" :options="severityOptions" />
        </TFormItem>
        <TFormItem label="描述">
          <TTextarea v-model="form.description" placeholder="描述复现步骤、实际结果和期望结果" />
        </TFormItem>
      </TForm>
    </TDrawer>

    <TDrawer v-model:visible="detailVisible" :size="'60%'" header="缺陷详情" :footer="false">
      <section v-if="currentDefect" class="detail-content">
        <TDescriptions bordered :column="2">
          <TDescriptionsItem label="缺陷标题">{{ currentDefect.title }}</TDescriptionsItem>
          <TDescriptionsItem label="状态">
            <TTag theme="warning" variant="light">
              {{ statusText[currentDefect.status] || currentDefect.status }}
            </TTag>
          </TDescriptionsItem>
          <TDescriptionsItem label="严重级别">
            <TTag variant="light">
              {{ severityText[currentDefect.severity] || currentDefect.severity }}
            </TTag>
          </TDescriptionsItem>
          <TDescriptionsItem label="环境">{{ currentDefect.environment }}</TDescriptionsItem>
          <TDescriptionsItem label="项目">
            {{ projectMap[currentDefect.projectId]?.name || currentDefect.projectId }}
          </TDescriptionsItem>
          <TDescriptionsItem label="需求">
            {{ requirementMap[currentDefect.requirementId]?.title || currentDefect.requirementId }}
          </TDescriptionsItem>
          <TDescriptionsItem label="提交人">{{ currentDefect.createdBy }}</TDescriptionsItem>
          <TDescriptionsItem label="处理人">{{ currentDefect.developerId || '未指派' }}</TDescriptionsItem>
          <TDescriptionsItem label="测试计划">
            {{ currentDefect.testPlanId || '未绑定' }}
          </TDescriptionsItem>
          <TDescriptionsItem label="测试执行">
            {{ currentDefect.testExecutionId || '未绑定' }}
          </TDescriptionsItem>
          <TDescriptionsItem label="修复时间">{{ formatDateTime(currentDefect.fixedAt) }}</TDescriptionsItem>
          <TDescriptionsItem label="创建时间">{{ formatDateTime(currentDefect.createTime) }}</TDescriptionsItem>
        </TDescriptions>
        <section class="detail-section">
          <h3>缺陷描述</h3>
          <article>{{ currentDefect.description || '暂无缺陷描述' }}</article>
        </section>
        <section class="detail-section">
          <h3>测试执行记录</h3>
          <TTable
            row-key="id"
            class="sprint-compact-table"
            :columns="withSerialColumn(executionColumns)"
            :data="defectExecutions"
            hover
            stripe
          >
            <template #executedAt="{ row }">
              {{ formatDateTime(row.executedAt) }}
            </template>
          </TTable>
        </section>
      </section>
    </TDrawer>
  </ProjectSecondaryListShell>
</template>

<style scoped>
.detail-content {
  display: grid;
  gap: 16px;
}

.detail-section {
  padding: 16px;
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.detail-section h3 {
  margin: 0 0 12px;
}

.detail-section article {
  min-height: 120px;
  white-space: pre-wrap;
}
</style>

