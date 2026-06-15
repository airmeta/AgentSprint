<script lang="ts" setup>
import type { AutomationApi, SystemApi } from '#/api';
import type { SprintUserApi } from '#/api/sprint/mvp';
import type { FormInstanceFunctions, FormRules, PrimaryTableCol } from 'tdesign-vue-next';

import { computed, onMounted, reactive, ref } from 'vue';

import {
  createDigitalWorkerApi,
  createWorkerCommandApi,
  getDigitalWorkerDetailApi,
  listAgentTokensApi,
  listAiPlatformsApi,
  listDictionaryItemsApi,
  listDictionaryTypesApi,
  listDigitalWorkersApi,
  setDigitalWorkerStatusApi,
  updateDigitalWorkerApi,
} from '#/api';
import { listUserOptionsApi } from '#/api/sprint/mvp';
import AdminListPage from '#/components/admin-list-page/admin-list-page.vue';
import { formatDateTime } from '#/views/_shared/date-format';
import { requiredRule, validateForm } from '#/views/_shared/form-rules';
import { confirmAndClose } from '#/views/_shared/dialog-confirm';
import RowAction from '#/views/system/_shared/row-action.vue';
import {
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  MessagePlugin,
  Select as TSelect,
  Space as TSpace,
  Switch as TSwitch,
  Tag as TTag,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

defineOptions({ name: 'AutomationDigitalWorkers' });

const loading = ref(false);
const saving = ref(false);
const detailLoading = ref(false);
const visible = ref(false);
const detailVisible = ref(false);
const formRef = ref<FormInstanceFunctions>();
const workers = ref<AutomationApi.DigitalWorker[]>([]);
const users = ref<SprintUserApi.UserOption[]>([]);
const tokens = ref<SystemApi.AgentToken[]>([]);
const aiPlatforms = ref<SystemApi.AiPlatform[]>([]);
const employeeTypeItems = ref<SystemApi.DictionaryItem[]>([]);
const detail = ref<AutomationApi.DigitalWorkerDetail>();

const filters = reactive({
  keyword: '',
  status: '',
  workerType: '',
});
const query = reactive({ ...filters });
const pagination = reactive({
  current: 1,
  pageSize: 30,
  pageSizeOptions: [30, 50, 100, 200],
});
const form = reactive<AutomationApi.SaveDigitalWorkerRequest & { aiPlatformCode?: string; id?: string; code?: string }>({
  agentTokenId: '',
  agentUserId: '',
  aiPlatformCode: 'openai',
  code: '',
  description: '',
  employeeType: 'development',
  heartbeatTimeoutSeconds: 90,
  idleMaxIntervalSeconds: 180,
  codexHome: '/codex-home',
  codexModel: 'gpt-5.4',
  codexProvider: 'openai',
  maxConcurrentRuns: 1,
  maxRunMinutes: 60,
  name: '',
  openAiBaseUrl: '',
  pollIntervalSeconds: 15,
  runSmokeOnStartup: false,
  runsRoot: '/runs',
  sandboxMode: 'workspace-write',
  smokePrompt: 'hello',
  status: 'active',
  workspaceRoot: '/workspaces',
  workerType: 'codex',
});

const rules: FormRules<typeof form> = {
  aiPlatformCode: requiredRule('请选择AI平台', 'change'),
  agentTokenId: requiredRule('请选择 Agent Token', 'change'),
  employeeType: requiredRule('请选择员工类型', 'change'),
  name: requiredRule('请输入员工名称'),
};

const statusOptions = [
  { label: '启用', value: 'active' },
  { label: '停用', value: 'disabled' },
  { label: '维护中', value: 'maintenance' },
];
const driverTypeOptions = [{ label: 'Codex', value: 'codex' }];
const maxConcurrentOptions = Array.from({ length: 10 }, (_, index) => {
  const value = index + 1;
  return { label: `${value}`, value };
});
const heartbeatTimeoutOptions = [30, 60, 90, 120].map((value) => ({ label: `${value} 秒`, value }));
const sandboxModeOptions = [
  { label: 'workspace-write', value: 'workspace-write' },
  { label: 'read-only', value: 'read-only' },
  { label: 'danger-full-access', value: 'danger-full-access' },
];
const fallbackEmployeeTypeItems: SystemApi.DictionaryItem[] = [
  { code: 'operations', dictionaryTypeId: '', id: 'operations', name: '运维', sort: 10, status: 1 },
  { code: 'development', dictionaryTypeId: '', id: 'development', name: '研发', sort: 20, status: 1 },
  { code: 'audit', dictionaryTypeId: '', id: 'audit', name: '审计', sort: 30, status: 1 },
  { code: 'test', dictionaryTypeId: '', id: 'test', name: '测试', sort: 40, status: 1 },
  { code: 'product', dictionaryTypeId: '', id: 'product', name: '产品', sort: 50, status: 1 },
];
const commandOptions = [
  { label: '烟测', value: 'smoke' },
  { label: '重载配置', value: 'reload_config' },
  { label: '当前任务后停止', value: 'stop_after_current' },
  { label: '取消当前运行', value: 'cancel_current_run' },
];
const columns: PrimaryTableCol[] = [
  { colKey: 'code', title: '编码', width: 140 },
  { colKey: 'name', title: '名称', width: 170 },
  { colKey: 'employeeType', title: '员工类型', cell: 'employeeType', width: 120 },
  { colKey: 'workerType', title: '驱动类型', cell: 'workerType', width: 120 },
  { colKey: 'agentUserId', title: '平台账号', cell: 'agentUserId', width: 150 },
  { colKey: 'runtime', title: '运行策略', cell: 'runtime', width: 150 },
  { colKey: 'status', title: '状态', cell: 'status', width: 100 },
  { colKey: 'updateTime', title: '更新时间', cell: 'updateTime', width: 170 },
  { colKey: 'actions', title: '操作', cell: 'actions', width: 260 },
];

const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  pageSizeOptions: pagination.pageSizeOptions,
  total: workers.value.length,
}));
const activeEmployeeTypeItems = computed(() =>
  (employeeTypeItems.value.length ? employeeTypeItems.value : fallbackEmployeeTypeItems).filter(
    (item) => item.status === 1,
  ),
);
const employeeTypeOptions = computed(() =>
  activeEmployeeTypeItems.value.map((item) => ({ label: item.name, value: item.code })),
);
const tokenOptions = computed(() =>
  tokens.value
    .filter((item) => item.status === 1 && !item.revokedAt)
    .map((item) => ({ label: `${item.name} - ${item.maskedToken}`, value: item.id })),
);
const activeAiPlatforms = computed(() => aiPlatforms.value.filter((item) => item.status === 1));
const aiPlatformOptions = computed(() =>
  activeAiPlatforms.value.map((item) => ({
    label: `${item.name} / ${item.model}`,
    value: item.code,
  })),
);
const userMap = computed(() => Object.fromEntries(users.value.map((item) => [item.id, item])));
const detailCommands = computed(() => detail.value?.pendingCommands || []);

function resetForm(row?: AutomationApi.DigitalWorker) {
  Object.assign(form, {
    agentTokenId: row?.agentTokenId || '',
    agentUserId: row?.agentUserId || '',
    aiPlatformCode: resolveAiPlatformCode(row),
    code: row?.code || '',
    description: row?.description || '',
    employeeType: row?.employeeType || 'development',
    heartbeatTimeoutSeconds: row?.heartbeatTimeoutSeconds || 90,
    idleMaxIntervalSeconds: row?.idleMaxIntervalSeconds || 180,
    codexHome: row?.codexHome || '/codex-home',
    codexModel: row?.codexModel || 'gpt-5.4',
    codexProvider: row?.codexProvider || 'openai',
    id: row?.id,
    maxConcurrentRuns: row?.maxConcurrentRuns || 1,
    maxRunMinutes: row?.maxRunMinutes || 60,
    name: row?.name || '',
    openAiBaseUrl: row?.openAiBaseUrl || '',
    pollIntervalSeconds: row?.pollIntervalSeconds || 15,
    runSmokeOnStartup: row?.runSmokeOnStartup || false,
    runsRoot: row?.runsRoot || '/runs',
    sandboxMode: row?.sandboxMode || 'workspace-write',
    smokePrompt: row?.smokePrompt || 'hello',
    status: row?.status || 'active',
    workspaceRoot: row?.workspaceRoot || '/workspaces',
    workerType: row?.workerType || 'codex',
  });
}

function resolveAiPlatformCode(row?: AutomationApi.DigitalWorker) {
  if (!row) {
    return activeAiPlatforms.value[0]?.code || 'openai';
  }

  return activeAiPlatforms.value.find((item) =>
    item.provider === row.codexProvider &&
    item.model === row.codexModel &&
    (item.openAiBaseUrl || '') === (row.openAiBaseUrl || ''),
  )?.code || activeAiPlatforms.value[0]?.code || 'openai';
}

function getSelectedAiPlatform() {
  return activeAiPlatforms.value.find((item) => item.code === form.aiPlatformCode);
}

function resolveUserName(userId?: string) {
  const user = userId ? userMap.value[userId] : undefined;
  return user ? `${user.displayName || user.username}` : userId || '-';
}

function statusTheme(status?: string) {
  if (status === 'active') return 'success';
  if (status === 'maintenance') return 'warning';
  if (status === 'disabled') return 'danger';
  return 'default';
}

function statusText(status?: string) {
  return statusOptions.find((item) => item.value === status)?.label || status || '-';
}

function resolveEmployeeTypeName(type?: string) {
  return activeEmployeeTypeItems.value.find((item) => item.code === type)?.name || type || '-';
}

function resolveDriverTypeName(type?: string) {
  return driverTypeOptions.find((item) => item.value === type)?.label || type || '-';
}

function sessionStatusText(status?: string) {
  const labels: Record<string, string> = {
    auth_required: '待认证',
    busy: '运行中',
    error: '异常',
    expired: '已过期',
    idle: '空闲',
    offline: '离线',
    starting: '启动中',
  };
  return status ? labels[status] || status : '-';
}

function commandText(commandType?: string) {
  return commandOptions.find((item) => item.value === commandType)?.label || commandType || '-';
}

function openCreate() {
  resetForm();
  visible.value = true;
}

function openEdit(row: AutomationApi.DigitalWorker) {
  resetForm(row);
  visible.value = true;
}

async function openDetail(row: AutomationApi.DigitalWorker) {
  detailVisible.value = true;
  detailLoading.value = true;
  try {
    detail.value = await getDigitalWorkerDetailApi(row.id);
  } finally {
    detailLoading.value = false;
  }
}

function buildPayload() {
  const selectedToken = tokens.value.find((item) => item.id === form.agentTokenId);
  const selectedAiPlatform = getSelectedAiPlatform();
  return {
    agentTokenId: form.agentTokenId,
    agentUserId: selectedToken?.ownerUserId || form.agentUserId,
    code: form.id ? form.code?.trim() : undefined,
    description: form.description?.trim() || undefined,
    employeeType: form.employeeType,
    endpointIds: [],
    heartbeatTimeoutSeconds: Number(form.heartbeatTimeoutSeconds || 90),
    idleMaxIntervalSeconds: Number(form.idleMaxIntervalSeconds || 180),
    codexHome: form.codexHome?.trim() || '/codex-home',
    codexModel: selectedAiPlatform?.model || form.codexModel?.trim() || 'gpt-5.4',
    codexProvider: selectedAiPlatform?.provider || form.codexProvider?.trim() || 'openai',
    maxConcurrentRuns: Number(form.maxConcurrentRuns || 1),
    maxRunMinutes: Number(form.maxRunMinutes || 60),
    name: form.name.trim(),
    openAiBaseUrl: selectedAiPlatform?.openAiBaseUrl || form.openAiBaseUrl?.trim() || undefined,
    pollIntervalSeconds: Number(form.pollIntervalSeconds || 15),
    projectIds: [],
    runSmokeOnStartup: Boolean(form.runSmokeOnStartup),
    runsRoot: form.runsRoot?.trim() || '/runs',
    sandboxMode: form.sandboxMode || 'workspace-write',
    status: form.status,
    smokePrompt: form.smokePrompt?.trim() || undefined,
    workspaceRoot: form.workspaceRoot?.trim() || '/workspaces',
    workerType: form.workerType || 'codex',
  };
}

async function save() {
  if (saving.value) return;
  if (!(await validateForm(formRef.value))) return;

  saving.value = true;
  try {
    const payload = buildPayload();
    if (form.id) {
      await updateDigitalWorkerApi(form.id, payload);
    } else {
      await createDigitalWorkerApi(payload);
    }
    MessagePlugin.success('数字员工已保存');
    visible.value = false;
    await load();
  } finally {
    saving.value = false;
  }
}

function setStatus(row: AutomationApi.DigitalWorker, status: AutomationApi.WorkerStatus) {
  confirmAndClose({
    body: `确认将 ${row.name} 设置为 ${statusText(status)}？`,
    confirmBtn: '确认',
    header: '调整员工状态',
    onConfirm: async () => {
      await setDigitalWorkerStatusApi(row.id, status);
      MessagePlugin.success('状态已更新');
      await load();
    },
  });
}

function sendCommand(row: AutomationApi.DigitalWorker, commandType: AutomationApi.WorkerCommandType) {
  confirmAndClose({
    body: `确认向 ${row.name} 下发 ${commandText(commandType)} 命令？命令会在下一次心跳时被受控端领取。`,
    confirmBtn: '下发',
    header: '下发 Worker 命令',
    onConfirm: async () => {
      await createWorkerCommandApi({ commandType, workerId: row.id });
      MessagePlugin.success('命令已下发');
      await openDetail(row);
    },
  });
}

async function applyFilters() {
  Object.assign(query, filters);
  pagination.current = 1;
  await load();
}

async function resetFilters() {
  Object.assign(filters, { keyword: '', status: '', workerType: '' });
  await applyFilters();
}

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

async function loadReferences() {
  const [userRows, tokenRows, aiPlatformRows, dictionaryTypeRows] = await Promise.allSettled([
    listUserOptionsApi(),
    listAgentTokensApi(),
    listAiPlatformsApi({ status: 1 }),
    listDictionaryTypesApi(),
  ]);
  users.value = userRows.status === 'fulfilled' ? userRows.value : [];
  tokens.value = tokenRows.status === 'fulfilled' ? tokenRows.value : [];
  aiPlatforms.value = aiPlatformRows.status === 'fulfilled' ? aiPlatformRows.value : [];
  const dictionaryTypes = dictionaryTypeRows.status === 'fulfilled' ? dictionaryTypeRows.value : [];
  const employeeType = dictionaryTypes.find((item) => item.code === 'digital_worker_employee_type');
  employeeTypeItems.value = employeeType ? await listDictionaryItemsApi(employeeType.id) : fallbackEmployeeTypeItems;
}

async function load() {
  loading.value = true;
  try {
    workers.value = await listDigitalWorkersApi(query);
  } finally {
    loading.value = false;
  }
}

onMounted(async () => {
  await Promise.all([loadReferences(), load()]);
});
</script>

<template>
  <AdminListPage
    title="数字员工管理"
    description="维护通过 AgentSprint.Worker 注册和执行的数字员工，管理平台账号、员工类型、驱动类型和运行策略。"
    table-title="数字员工列表"
    add-button-text="新增数字员工"
    :columns="columns"
    :data="workers"
    :loading="loading"
    :pagination="tablePagination"
    @add="openCreate"
    @page-change="handlePageChange"
    @refresh="load"
    @reset="resetFilters"
    @search="applyFilters"
  >
    <template #filters>
      <label class="filter-field">
        <span>员工信息</span>
        <TInput v-model="filters.keyword" clearable placeholder="编码 / 名称 / 描述" />
      </label>
      <label class="filter-field">
        <span>驱动类型</span>
        <TSelect v-model="filters.workerType" clearable placeholder="全部驱动" :options="driverTypeOptions" />
      </label>
      <label class="filter-field">
	    <span>状态</span>
	    <TSelect v-model="filters.status" clearable placeholder="全部状态" :options="statusOptions" />
      </label>
    </template>

    <template #employeeType="{ row }">{{ resolveEmployeeTypeName(row.employeeType) }}</template>
    <template #workerType="{ row }">{{ resolveDriverTypeName(row.workerType) }}</template>
    <template #agentUserId="{ row }">{{ resolveUserName(row.agentUserId) }}</template>
    <template #runtime="{ row }">
      {{ row.maxConcurrentRuns }} 并发 / {{ row.heartbeatTimeoutSeconds }} 秒心跳
    </template>
    <template #status="{ row }">
      <TTag :theme="statusTheme(row.status)" variant="light">{{ statusText(row.status) }}</TTag>
    </template>
    <template #updateTime="{ row }">
      {{ formatDateTime(row.updateTime || row.createTime) }}
    </template>
    <template #actions="{ row }">
      <TSpace>
        <RowAction icon="lucide:eye" label="详情" @click="openDetail(row)" />
        <RowAction label="编辑" @click="openEdit(row)" />
	        <RowAction icon="lucide:rotate-cw" label="烟测" @click="sendCommand(row, 'smoke')" />
        <RowAction
          v-if="row.status === 'active'"
          icon="lucide:pause-circle"
          label="维护"
          theme="warning"
          @click="setStatus(row, 'maintenance')"
        />
        <RowAction
          v-else
          icon="lucide:play-circle"
          label="启用"
          theme="success"
          @click="setStatus(row, 'active')"
        />
      </TSpace>
    </template>
  </AdminListPage>

  <TDrawer
    v-model:visible="visible"
    size="780px"
    :header="form.id ? '编辑数字员工' : '新增数字员工'"
    :confirm-btn="{ content: '保存', loading: saving }"
    @confirm="save"
  >
    <TForm ref="formRef" class="worker-form" :data="form" :rules="rules" label-width="120px">
      <div class="form-grid form-grid--single">
        <TFormItem label="员工名称" name="name">
          <TInput v-model="form.name" placeholder="Codex worker" />
        </TFormItem>
      </div>
      <div v-if="form.id" class="form-grid">
        <TFormItem v-if="form.id" label="员工编码">
          <TInput v-model="form.code" disabled />
        </TFormItem>
      </div>
      <div class="form-grid form-grid--single">
        <TFormItem label="Agent Token" name="agentTokenId">
          <TSelect v-model="form.agentTokenId" filterable placeholder="请选择 Agent Token" :options="tokenOptions" />
        </TFormItem>
      </div>
      <div class="form-grid">
        <TFormItem label="员工类型" name="employeeType">
          <TSelect v-model="form.employeeType" :options="employeeTypeOptions" />
        </TFormItem>
        <TFormItem label="驱动类型">
          <TSelect v-model="form.workerType" :options="driverTypeOptions" />
        </TFormItem>
      </div>
      <div class="form-grid">
        <TFormItem label="最大并发" name="maxConcurrentRuns">
          <TSelect v-model="form.maxConcurrentRuns" :options="maxConcurrentOptions" />
        </TFormItem>
        <TFormItem label="心跳超时" name="heartbeatTimeoutSeconds">
          <TSelect v-model="form.heartbeatTimeoutSeconds" :options="heartbeatTimeoutOptions" />
        </TFormItem>
      </div>
      <div class="form-grid">
        <TFormItem label="轮询间隔">
          <TInput v-model="form.pollIntervalSeconds" type="number" placeholder="15" />
        </TFormItem>
        <TFormItem label="最大空闲间隔">
          <TInput v-model="form.idleMaxIntervalSeconds" type="number" placeholder="180" />
        </TFormItem>
      </div>
      <div class="form-grid">
        <TFormItem label="最大运行分钟">
          <TInput v-model="form.maxRunMinutes" type="number" placeholder="60" />
        </TFormItem>
        <TFormItem label="沙箱模式">
          <TSelect v-model="form.sandboxMode" :options="sandboxModeOptions" />
        </TFormItem>
      </div>
      <div class="form-grid">
        <TFormItem label="工作区根目录">
          <TInput v-model="form.workspaceRoot" placeholder="/workspaces" />
        </TFormItem>
        <TFormItem label="运行日志目录">
          <TInput v-model="form.runsRoot" placeholder="/runs" />
        </TFormItem>
      </div>
      <div class="form-grid">
        <TFormItem label="Codex Home">
          <TInput v-model="form.codexHome" placeholder="/codex-home" />
        </TFormItem>
      </div>
      <div class="form-grid form-grid--single">
        <TFormItem label="AI平台" name="aiPlatformCode">
          <TSelect
            v-model="form.aiPlatformCode"
            filterable
            placeholder="请选择AI平台"
            :options="aiPlatformOptions"
          />
        </TFormItem>
      </div>
      <div v-if="false" class="form-grid">
        <TFormItem label="Provider">
          <TInput v-model="form.codexProvider" placeholder="openai" />
        </TFormItem>
        <TFormItem label="模型">
          <TInput v-if="false" v-model="form.codexModel" placeholder="gpt-5.4" />
        </TFormItem>
      </div>
      <div class="form-grid form-grid--single">
        <TFormItem v-if="false" label="OpenAI Base URL">
          <TInput v-model="form.openAiBaseUrl" placeholder="https://api.openai.com/v1" />
        </TFormItem>
      </div>
      <div class="form-grid">
        <TFormItem label="启动烟测">
          <TSwitch v-model="form.runSmokeOnStartup" />
        </TFormItem>
        <TFormItem label="烟测提示词">
          <TInput v-model="form.smokePrompt" placeholder="你好" />
        </TFormItem>
      </div>
      <div class="form-grid">
        <TFormItem label="状态">
          <TSelect v-model="form.status" :options="statusOptions" />
        </TFormItem>
      </div>
      <TFormItem label="说明">
        <TTextarea v-model="form.description" :autosize="{ minRows: 3, maxRows: 5 }" placeholder="记录部署位置、用途或接单边界" />
      </TFormItem>
    </TForm>
  </TDrawer>

  <TDrawer v-model:visible="detailVisible" size="860px" header="数字员工详情" :footer="false">
    <div v-if="detail" class="detail-panel">
      <section class="detail-section">
        <h3>{{ detail.worker.name }} ({{ detail.worker.code }})</h3>
        <div class="detail-grid">
          <span>平台账号: {{ resolveUserName(detail.worker.agentUserId) }}</span>
          <span>员工类型: {{ resolveEmployeeTypeName(detail.worker.employeeType) }}</span>
          <span>驱动类型: {{ resolveDriverTypeName(detail.worker.workerType) }}</span>
          <span>状态: {{ statusText(detail.worker.status) }}</span>
          <span>配置版本: {{ detail.worker.configVersion }}</span>
          <span>模型: {{ detail.worker.codexModel }}</span>
          <span>创建人: {{ detail.worker.createdBy }}</span>
          <span>创建时间: {{ formatDateTime(detail.worker.createTime) }}</span>
        </div>
      </section>

      <section class="detail-section">
        <h3>最近会话</h3>
        <div v-if="detail.latestSession" class="detail-grid">
          <span>实例: {{ detail.latestSession.instanceId }}</span>
          <span>状态: {{ sessionStatusText(detail.latestSession.status) }}</span>
          <span>主机: {{ detail.latestSession.hostName || '-' }}</span>
          <span>最后心跳: {{ formatDateTime(detail.latestSession.lastHeartbeatAt) }}</span>
          <span>工作区: {{ detail.latestSession.workspaceRoot || '-' }}</span>
        </div>
        <TTag v-else variant="light">暂无会话</TTag>
      </section>

      <section class="detail-section">
        <h3>待领取命令</h3>
        <div v-if="detailCommands.length" class="command-list">
          <div v-for="command in detailCommands" :key="command.id" class="command-item">
            <strong>{{ commandText(command.commandType) }}</strong>
            <span>{{ command.status }} / {{ formatDateTime(command.createTime) }}</span>
          </div>
        </div>
        <TTag v-else variant="light">暂无待领取命令</TTag>
      </section>
    </div>
  </TDrawer>
</template>

<style scoped>
.filter-field {
  display: grid;
  grid-template-columns: auto minmax(180px, 260px);
  gap: 8px;
  align-items: center;
  color: var(--td-text-color-secondary);
}

.form-grid,
.detail-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.form-grid--single {
  grid-template-columns: 1fr;
}

.worker-form {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.worker-form :deep(.t-form__item) {
  margin-bottom: 0;
}

.detail-panel {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.detail-section {
  padding: 12px;
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.detail-section h3 {
  margin: 0 0 10px;
  font-size: 15px;
}

.command-list {
  display: grid;
  gap: 8px;
}

.command-item {
  display: flex;
  justify-content: space-between;
  padding: 8px 10px;
  background: var(--td-bg-color-container-hover);
  border-radius: 4px;
}

@media (max-width: 760px) {
  .filter-field,
  .form-grid,
  .detail-grid {
    grid-template-columns: 1fr;
    width: 100%;
  }
}
</style>
