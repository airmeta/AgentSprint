<script lang="ts" setup>
import type { SprintMvpApi } from '#/api/sprint/mvp';
import type { SystemApi } from '#/api';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import {
  deleteRuntimeEnvironmentApi,
  deleteRuntimeEnvironmentContainerApi,
  listRuntimeEnvironmentContainersApi,
  listRuntimeEnvironmentsApi,
  saveRuntimeEnvironmentApi,
  saveRuntimeEnvironmentContainerApi,
} from '#/api';
import {
  listFeatureModulesApi,
  listProjectEndpointsApi,
  listProjectsApi,
} from '#/api/sprint/mvp';
import {
  optionalHttpUrlRule,
  optionalNumberRule,
  requiredRule,
  validateForm,
} from '#/views/_shared/form-rules';
import SystemPage from '#/views/system/_shared/system-page.vue';
import { getCellRow } from '#/views/system/_shared/table-cell';
import {
  Button as TButton,
  Dialog as TDialog,
  DialogPlugin,
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  Link as TLink,
  MessagePlugin,
  Select as TSelect,
  Space as TSpace,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

const loading = ref(false);
const containerLoading = ref(false);
const containerSaving = ref(false);
const environmentSaving = ref(false);
const environmentVisible = ref(false);
const containerVisible = ref(false);
const environmentFormRef = ref<FormInstanceFunctions>();
const containerFormRef = ref<FormInstanceFunctions>();
const environments = ref<SystemApi.RuntimeEnvironment[]>([]);
const containers = ref<SystemApi.RuntimeEnvironmentContainer[]>([]);
const projects = ref<SprintMvpApi.Project[]>([]);
const endpoints = ref<SprintMvpApi.ProjectEndpoint[]>([]);
const modules = ref<SprintMvpApi.FeatureModule[]>([]);
const selectedEnvironmentId = ref('');

const filters = reactive({
  endpointId: '',
  moduleId: '',
  projectId: '',
});
const environmentForm = reactive<Partial<SystemApi.RuntimeEnvironment>>({
  apiBaseUrl: '',
  code: '',
  composeFilePath: '',
  deployRoot: '',
  description: '',
  dockerDirectory: '',
  endpointId: '',
  environmentType: 'test',
  frontendProxyApiUrl: '',
  frontendUrl: '',
  id: undefined,
  localPackagePaths: '',
  mcpEndpoint: '',
  moduleId: '',
  name: '',
  projectId: '',
  remotePackagePath: '',
  sort: 10,
  status: 1,
});
const containerForm = reactive<Partial<SystemApi.RuntimeEnvironmentContainer>>({
  containerPort: 80,
  description: '',
  hostPort: 5999,
  id: undefined,
  name: '',
  protocol: 'tcp',
  runtimeEnvironmentId: '',
  sort: 10,
  status: 1,
});

const environmentRules: FormRules<typeof environmentForm> = {
  apiBaseUrl: optionalHttpUrlRule('API 地址必须以 http:// 或 https:// 开头'),
  code: requiredRule('请输入环境编码'),
  environmentType: requiredRule('请选择环境类型', 'change'),
  frontendProxyApiUrl: optionalHttpUrlRule('前端代理 API 必须以 http:// 或 https:// 开头'),
  frontendUrl: optionalHttpUrlRule('前端地址必须以 http:// 或 https:// 开头'),
  mcpEndpoint: optionalHttpUrlRule('MCP 地址必须以 http:// 或 https:// 开头'),
  name: requiredRule('请输入环境名称'),
  sort: optionalNumberRule('排序必须是数字'),
};
const containerRules: FormRules<typeof containerForm> = {
  containerPort: optionalNumberRule('容器端口必须是数字'),
  hostPort: optionalNumberRule('宿主端口必须是数字'),
  name: requiredRule('请输入容器名称'),
  runtimeEnvironmentId: requiredRule('请选择运行环境', 'change'),
  sort: optionalNumberRule('排序必须是数字'),
};

const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
];
const environmentTypeOptions = [
  { label: '测试环境', value: 'test' },
  { label: '预发环境', value: 'staging' },
  { label: '生产环境', value: 'production' },
];
const protocolOptions = [
  { label: 'TCP', value: 'tcp' },
  { label: 'UDP', value: 'udp' },
];
const environmentColumns = [
  { colKey: 'code', title: '环境编码', width: 130 },
  { colKey: 'name', title: '环境名称', width: 150 },
  { colKey: 'scope', title: '归属范围', cell: 'scope', width: 220 },
  { colKey: 'frontendUrl', title: '前端地址', width: 230 },
  { colKey: 'apiBaseUrl', title: 'API 地址', width: 210 },
  { colKey: 'mcpEndpoint', title: 'MCP 地址', width: 230 },
  { colKey: 'status', title: '状态', width: 80, cell: (...args: any[]) => (getCellRow(args[0], args[1])?.status === 1 ? '启用' : '停用') },
  { colKey: 'actions', title: '操作', width: 180, cell: 'actions' },
];
const containerColumns = [
  { colKey: 'name', title: '容器', width: 180 },
  { colKey: 'ports', title: '端口映射', cell: 'ports', width: 160 },
  { colKey: 'protocol', title: '协议', width: 80 },
  { colKey: 'description', title: '说明', cell: (...args: any[]) => getCellRow(args[0], args[1])?.description || '-' },
  { colKey: 'status', title: '状态', width: 80, cell: (...args: any[]) => (getCellRow(args[0], args[1])?.status === 1 ? '启用' : '停用') },
  { colKey: 'actions', title: '操作', width: 140, cell: 'actions' },
];

const projectOptions = computed(() =>
  projects.value.map((item) => ({ label: `${item.name} (${item.code})`, value: item.id })),
);
const endpointOptions = computed(() =>
  endpoints.value
    .filter((item) => !filters.projectId || item.projectId === filters.projectId)
    .map((item) => ({ label: `${item.name} (${item.code})`, value: item.id })),
);
const moduleOptions = computed(() =>
  modules.value
    .filter((item) => !filters.projectId || item.projectId === filters.projectId)
    .filter((item) => !filters.endpointId || item.endpointId === filters.endpointId)
    .map((item) => ({ label: `${item.name} (${item.code})`, value: item.id })),
);
const formEndpointOptions = computed(() =>
  endpoints.value
    .filter((item) => !environmentForm.projectId || item.projectId === environmentForm.projectId)
    .map((item) => ({ label: `${item.name} (${item.code})`, value: item.id })),
);
const formModuleOptions = computed(() =>
  modules.value
    .filter((item) => !environmentForm.projectId || item.projectId === environmentForm.projectId)
    .filter((item) => !environmentForm.endpointId || item.endpointId === environmentForm.endpointId)
    .map((item) => ({ label: `${item.name} (${item.code})`, value: item.id })),
);
const projectMap = computed(() => Object.fromEntries(projects.value.map((item) => [item.id, item])));
const endpointMap = computed(() => Object.fromEntries(endpoints.value.map((item) => [item.id, item])));
const moduleMap = computed(() => Object.fromEntries(modules.value.map((item) => [item.id, item])));
const selectedEnvironment = computed(() =>
  environments.value.find((item) => item.id === selectedEnvironmentId.value),
);

watch(selectedEnvironmentId, async (id) => {
  if (!id) {
    containers.value = [];
    return;
  }

  await loadContainers(id);
});

function resolveScope(row: SystemApi.RuntimeEnvironment) {
  const scope = [
    row.projectId ? projectMap.value[row.projectId]?.name || row.projectId : '全局',
    row.endpointId ? endpointMap.value[row.endpointId]?.name || row.endpointId : '',
    row.moduleId ? moduleMap.value[row.moduleId]?.name || row.moduleId : '',
  ].filter(Boolean);
  return scope.join(' / ');
}

function openEnvironment(row?: SystemApi.RuntimeEnvironment) {
  Object.assign(environmentForm, {
    apiBaseUrl: row?.apiBaseUrl || '',
    code: row?.code || '',
    composeFilePath: row?.composeFilePath || '',
    deployRoot: row?.deployRoot || '',
    description: row?.description || '',
    dockerDirectory: row?.dockerDirectory || '',
    endpointId: row?.endpointId || '',
    environmentType: row?.environmentType || 'test',
    frontendProxyApiUrl: row?.frontendProxyApiUrl || '',
    frontendUrl: row?.frontendUrl || '',
    id: row?.id,
    localPackagePaths: row?.localPackagePaths || '',
    mcpEndpoint: row?.mcpEndpoint || '',
    moduleId: row?.moduleId || '',
    name: row?.name || '',
    projectId: row?.projectId || '',
    remotePackagePath: row?.remotePackagePath || '',
    sort: row?.sort ?? 10,
    status: row?.status ?? 1,
  });
  environmentVisible.value = true;
}

function openContainer(row?: SystemApi.RuntimeEnvironmentContainer) {
  if (!selectedEnvironmentId.value) {
    MessagePlugin.warning('请先选择运行环境');
    return;
  }

  Object.assign(containerForm, {
    containerPort: row?.containerPort ?? 80,
    description: row?.description || '',
    hostPort: row?.hostPort ?? 5999,
    id: row?.id,
    name: row?.name || '',
    protocol: row?.protocol || 'tcp',
    runtimeEnvironmentId: row?.runtimeEnvironmentId || selectedEnvironmentId.value,
    sort: row?.sort ?? 10,
    status: row?.status ?? 1,
  });
  containerVisible.value = true;
}

async function loadReferences() {
  [projects.value, endpoints.value, modules.value] = await Promise.all([
    listProjectsApi(),
    listProjectEndpointsApi(),
    listFeatureModulesApi(),
  ]);
}

async function loadEnvironments() {
  loading.value = true;
  try {
    environments.value = await listRuntimeEnvironmentsApi({
      endpointId: filters.endpointId || undefined,
      moduleId: filters.moduleId || undefined,
      projectId: filters.projectId || undefined,
    });
    if (!selectedEnvironmentId.value || !environments.value.some((item) => item.id === selectedEnvironmentId.value)) {
      selectedEnvironmentId.value = environments.value[0]?.id || '';
    }
  } finally {
    loading.value = false;
  }
}

async function loadContainers(runtimeEnvironmentId = selectedEnvironmentId.value) {
  if (!runtimeEnvironmentId) return;
  containerLoading.value = true;
  try {
    containers.value = await listRuntimeEnvironmentContainersApi(runtimeEnvironmentId);
  } finally {
    containerLoading.value = false;
  }
}

async function search() {
  await loadEnvironments();
}

async function reset() {
  Object.assign(filters, { endpointId: '', moduleId: '', projectId: '' });
  await loadEnvironments();
}

function selectEnvironment(row: SystemApi.RuntimeEnvironment) {
  selectedEnvironmentId.value = row.id;
}

async function saveEnvironment() {
  if (environmentSaving.value) return;
  if (!(await validateForm(environmentFormRef.value))) return;
  environmentSaving.value = true;
  try {
    await saveRuntimeEnvironmentApi({
      ...environmentForm,
      endpointId: environmentForm.endpointId || undefined,
      moduleId: environmentForm.moduleId || undefined,
      projectId: environmentForm.projectId || undefined,
      sort: Number(environmentForm.sort || 0),
    });
    MessagePlugin.success('运行环境已保存');
    environmentVisible.value = false;
    await loadEnvironments();
  } finally {
    environmentSaving.value = false;
  }
}

async function saveContainer() {
  if (containerSaving.value) return;
  if (!(await validateForm(containerFormRef.value))) return;
  containerSaving.value = true;
  try {
    await saveRuntimeEnvironmentContainerApi({
      ...containerForm,
      containerPort: Number(containerForm.containerPort),
      hostPort: Number(containerForm.hostPort),
      sort: Number(containerForm.sort || 0),
    });
    MessagePlugin.success('容器映射已保存');
    containerVisible.value = false;
    await loadContainers();
  } finally {
    containerSaving.value = false;
  }
}

function removeEnvironment(row: SystemApi.RuntimeEnvironment) {
  DialogPlugin.confirm({
    body: `确认删除运行环境 ${row.code}？该环境下的容器映射也会被删除。`,
    confirmBtn: '删除',
    header: '删除运行环境',
    onConfirm: async () => {
      await deleteRuntimeEnvironmentApi(row.id);
      MessagePlugin.success('运行环境已删除');
      await loadEnvironments();
    },
  });
}

function removeContainer(row: SystemApi.RuntimeEnvironmentContainer) {
  DialogPlugin.confirm({
    body: `确认删除容器映射 ${row.name}？`,
    confirmBtn: '删除',
    header: '删除容器映射',
    onConfirm: async () => {
      await deleteRuntimeEnvironmentContainerApi(row.id);
      MessagePlugin.success('容器映射已删除');
      await loadContainers();
    },
  });
}

onMounted(async () => {
  await loadReferences();
  await loadEnvironments();
});
</script>

<template>
  <div class="runtime-page">
    <SystemPage
      title="运行环境"
      description="按项目、端和模块维护测试地址、部署路径、发布包与 Compose 信息。"
      :columns="environmentColumns"
      :data="environments"
      :loading="loading"
      @add="openEnvironment()"
    >
      <template #filters>
        <TSelect v-model="filters.projectId" clearable filterable placeholder="项目" :options="projectOptions" class="filter-control" />
        <TSelect v-model="filters.endpointId" clearable filterable placeholder="端" :options="endpointOptions" class="filter-control" />
        <TSelect v-model="filters.moduleId" clearable filterable placeholder="模块" :options="moduleOptions" class="filter-control" />
        <TSpace>
          <TButton theme="primary" :disabled="loading" @click="search">查询</TButton>
          <TButton @click="reset">重置</TButton>
        </TSpace>
      </template>
      <template #action>新增环境</template>
      <template #scope="{ row }">{{ resolveScope(row) }}</template>
      <template #actions="{ row }">
        <TSpace>
          <TLink theme="primary" @click="selectEnvironment(row)">容器</TLink>
          <TLink theme="primary" @click="openEnvironment(row)">编辑</TLink>
          <TLink theme="danger" @click="removeEnvironment(row)">删除</TLink>
        </TSpace>
      </template>
    </SystemPage>

    <SystemPage
      title="容器映射"
      :description="selectedEnvironment ? `${selectedEnvironment.name} (${selectedEnvironment.code})` : '请选择运行环境'"
      :columns="containerColumns"
      :data="containers"
      :loading="containerLoading"
      :addable="Boolean(selectedEnvironmentId)"
      @add="openContainer()"
    >
      <template #action>新增容器</template>
      <template #ports="{ row }">{{ row.hostPort }} -> {{ row.containerPort }}</template>
      <template #actions="{ row }">
        <TSpace>
          <TLink theme="primary" @click="openContainer(row)">编辑</TLink>
          <TLink theme="danger" @click="removeContainer(row)">删除</TLink>
        </TSpace>
      </template>
    </SystemPage>

    <TDrawer v-model:visible="environmentVisible" size="720px" header="运行环境维护" :confirm-btn="{ content: '保存', loading: environmentSaving }" @confirm="saveEnvironment">
      <TForm ref="environmentFormRef" :data="environmentForm" :rules="environmentRules" label-width="116px">
        <TFormItem label="环境编码" name="code"><TInput v-model="environmentForm.code" placeholder="test" /></TFormItem>
        <TFormItem label="环境名称" name="name"><TInput v-model="environmentForm.name" placeholder="测试环境" /></TFormItem>
        <TFormItem label="环境类型" name="environmentType"><TSelect v-model="environmentForm.environmentType" :options="environmentTypeOptions" /></TFormItem>
        <TFormItem label="所属项目"><TSelect v-model="environmentForm.projectId" clearable filterable :options="projectOptions" /></TFormItem>
        <TFormItem label="所属端"><TSelect v-model="environmentForm.endpointId" clearable filterable :options="formEndpointOptions" /></TFormItem>
        <TFormItem label="所属模块"><TSelect v-model="environmentForm.moduleId" clearable filterable :options="formModuleOptions" /></TFormItem>
        <TFormItem label="前端地址" name="frontendUrl"><TInput v-model="environmentForm.frontendUrl" placeholder="http://192.168.80.101:5999" /></TFormItem>
        <TFormItem label="API 地址" name="apiBaseUrl"><TInput v-model="environmentForm.apiBaseUrl" placeholder="http://192.168.80.101:5000" /></TFormItem>
        <TFormItem label="前端代理 API" name="frontendProxyApiUrl"><TInput v-model="environmentForm.frontendProxyApiUrl" placeholder="http://192.168.80.101:5999/api" /></TFormItem>
        <TFormItem label="MCP 地址" name="mcpEndpoint"><TInput v-model="environmentForm.mcpEndpoint" placeholder="http://192.168.80.101:5010/mcp" /></TFormItem>
        <TFormItem label="部署根目录"><TInput v-model="environmentForm.deployRoot" placeholder="/opt/agentsprint-deploy" /></TFormItem>
        <TFormItem label="Docker 目录"><TInput v-model="environmentForm.dockerDirectory" placeholder="/opt/agentsprint-deploy/docker" /></TFormItem>
        <TFormItem label="远端发布包"><TInput v-model="environmentForm.remotePackagePath" placeholder="/opt/agentsprint-deploy/agentsprint-docker-deploy.tgz" /></TFormItem>
        <TFormItem label="Compose 文件"><TInput v-model="environmentForm.composeFilePath" placeholder="/opt/agentsprint-deploy/docker/docker-compose.yml" /></TFormItem>
        <TFormItem label="本地发布包"><TTextarea v-model="environmentForm.localPackagePaths" :autosize="{ minRows: 3, maxRows: 6 }" /></TFormItem>
        <TFormItem label="说明"><TTextarea v-model="environmentForm.description" :autosize="{ minRows: 3, maxRows: 5 }" /></TFormItem>
        <TFormItem label="排序" name="sort"><TInput v-model="environmentForm.sort" type="number" /></TFormItem>
        <TFormItem label="状态"><TSelect v-model="environmentForm.status" :options="statusOptions" /></TFormItem>
      </TForm>
    </TDrawer>

    <TDialog v-model:visible="containerVisible" header="容器映射维护" width="560px" :confirm-btn="{ content: '保存', loading: containerSaving }" @confirm="saveContainer">
      <TForm ref="containerFormRef" :data="containerForm" :rules="containerRules" label-width="104px">
        <TFormItem label="运行环境" name="runtimeEnvironmentId">
          <TSelect
            v-model="containerForm.runtimeEnvironmentId"
            :options="environments.map((item) => ({ label: `${item.name} (${item.code})`, value: item.id }))"
          />
        </TFormItem>
        <TFormItem label="容器名称" name="name"><TInput v-model="containerForm.name" placeholder="agentsprint-admin" /></TFormItem>
        <TFormItem label="宿主端口" name="hostPort"><TInput v-model="containerForm.hostPort" type="number" /></TFormItem>
        <TFormItem label="容器端口" name="containerPort"><TInput v-model="containerForm.containerPort" type="number" /></TFormItem>
        <TFormItem label="协议"><TSelect v-model="containerForm.protocol" :options="protocolOptions" /></TFormItem>
        <TFormItem label="说明"><TTextarea v-model="containerForm.description" /></TFormItem>
        <TFormItem label="排序" name="sort"><TInput v-model="containerForm.sort" type="number" /></TFormItem>
        <TFormItem label="状态"><TSelect v-model="containerForm.status" :options="statusOptions" /></TFormItem>
      </TForm>
    </TDialog>
  </div>
</template>

<style scoped>
.runtime-page {
  display: grid;
  grid-template-columns: minmax(680px, 1.35fr) minmax(420px, 0.85fr);
  gap: 16px;
}

.filter-control {
  width: 210px;
}

@media (max-width: 1280px) {
  .runtime-page {
    grid-template-columns: 1fr;
  }
}
</style>
