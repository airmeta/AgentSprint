<script lang="ts" setup>
import type { SprintMvpApi, SprintUserApi } from '#/api/sprint/mvp';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import {
  Button as TButton,
  Card as TCard,
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  Link as TLink,
  MessagePlugin,
  Pagination as TPagination,
  Select as TSelect,
  Space as TSpace,
  Table as TTable,
  Tag as TTag,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

import {
  createFeatureModuleApi,
  createProjectEndpointApi,
  listFeatureModulesApi,
  listProjectEndpointsApi,
  listProjectsApi,
  listSkillsApi,
  listUserOptionsApi,
  updateFeatureModuleApi,
  updateProjectEndpointApi,
} from '#/api/sprint/mvp';
import {
  optionalNumberRule,
  requiredRule,
  validateForm,
} from '#/views/_shared/form-rules';

import '../_shared/table-layout.css';

type EndpointPager = {
  current: number;
  pageSize: number;
};

const loading = ref(false);
const endpointVisible = ref(false);
const endpointMode = ref<'create' | 'edit'>('create');
const endpointFormRef = ref<FormInstanceFunctions>();
const moduleVisible = ref(false);
const moduleMode = ref<'create' | 'edit'>('create');
const moduleFormRef = ref<FormInstanceFunctions>();
const selectedProjectId = ref('');
const projects = ref<SprintMvpApi.Project[]>([]);
const endpoints = ref<SprintMvpApi.ProjectEndpoint[]>([]);
const modules = ref<SprintMvpApi.FeatureModule[]>([]);
const skills = ref<SprintMvpApi.Skill[]>([]);
const users = ref<SprintUserApi.UserOption[]>([]);
const endpointPagers = reactive<Record<string, EndpointPager>>({});

const endpointForm = reactive({
  code: '',
  developerIds: [] as string[],
  id: '',
  name: '',
  ownerId: '',
  projectId: '',
  skillIds: [] as string[],
  sort: '',
  status: 'active',
  testerIds: [] as string[],
  type: 'web',
});
const moduleForm = reactive({
  code: '',
  description: '',
  developerIds: [] as string[],
  endpointId: '',
  id: '',
  name: '',
  ownerId: '',
  projectId: '',
  sort: '',
  status: 'active',
  testerIds: [] as string[],
});
const endpointRules = computed<FormRules<typeof endpointForm>>(() => ({
  code: endpointMode.value === 'create' ? requiredRule('请输入端编码') : [],
  name: requiredRule('请输入端名称'),
  sort: optionalNumberRule('排序必须是数字'),
  type: requiredRule('请选择端类型', 'change'),
}));
const moduleRules = computed<FormRules<typeof moduleForm>>(() => ({
  code: moduleMode.value === 'create' ? requiredRule('请输入模块编码') : [],
  endpointId: requiredRule('请选择所属端', 'change'),
  name: requiredRule('请输入模块名称'),
  sort: optionalNumberRule('排序必须是数字'),
}));

const endpointTypeOptions = [
  { label: 'iOS', value: 'ios' },
  { label: '安卓', value: 'android' },
  { label: '桌面端', value: 'desktop' },
  { label: 'Web网站', value: 'web' },
  { label: '管理后台', value: 'admin' },
  { label: '其他端', value: 'other' },
];
const statusOptions = [
  { label: '启用', value: 'active' },
  { label: '停用', value: 'disabled' },
];
const moduleColumns = [
  { colKey: 'name', title: '模块名称', width: 180 },
  { colKey: 'code', title: '编码', width: 120 },
  { colKey: 'ownerId', title: '负责人', width: 140 },
  { colKey: 'developerIds', title: '开发人员' },
  { colKey: 'testerIds', title: '测试人员' },
  { colKey: 'status', title: '状态', width: 90 },
  { colKey: 'actions', title: '操作', width: 100 },
];

const selectedProject = computed(() =>
  projects.value.find((project) => project.id === selectedProjectId.value),
);
const selectedEndpoints = computed(() =>
  endpoints.value
    .filter((endpoint) => endpoint.projectId === selectedProjectId.value)
    .sort((left, right) => left.sort - right.sort || left.createTime.localeCompare(right.createTime)),
);
const selectedModules = computed(() =>
  modules.value.filter((module) => module.projectId === selectedProjectId.value),
);
const userOptions = computed(() =>
  users.value.map((user) => ({
    label: `${user.displayName} (${user.username})`,
    value: user.id,
  })),
);
const userMap = computed(() => Object.fromEntries(users.value.map((item) => [item.id, item])));
const skillOptions = computed(() =>
  skills.value.map((skill) => ({ label: `${skill.code} - ${skill.name}`, value: skill.id })),
);
const skillMap = computed(() => Object.fromEntries(skills.value.map((item) => [item.id, item])));

function resolveUserName(userId?: string) {
  return userId ? userMap.value[userId]?.displayName || userId : '未指定';
}

function resolveUserNames(userIds?: string[]) {
  return userIds && userIds.length > 0 ? userIds.map((id) => resolveUserName(id)).join('、') : '未指定';
}

function endpointTypeLabel(type: string) {
  return endpointTypeOptions.find((item) => item.value === type)?.label || type;
}

function resolveSkillNames(skillIds?: string[]) {
  return skillIds && skillIds.length > 0
    ? skillIds.map((id) => skillMap.value[id]?.name || id).join(' / ')
    : '未选择';
}

function projectEndpointCount(projectId: string) {
  return endpoints.value.filter((endpoint) => endpoint.projectId === projectId).length;
}

function projectModuleCount(projectId: string) {
  return modules.value.filter((module) => module.projectId === projectId).length;
}

function endpointModules(endpointId: string) {
  return selectedModules.value
    .filter((module) => module.endpointId === endpointId)
    .sort((left, right) => left.sort - right.sort || left.createTime.localeCompare(right.createTime));
}

function endpointPageModules(endpointId: string) {
  const pager = ensurePager(endpointId);
  const source = endpointModules(endpointId);
  const start = (pager.current - 1) * pager.pageSize;
  return source.slice(start, start + pager.pageSize);
}

function ensurePager(endpointId: string) {
  endpointPagers[endpointId] ||= { current: 1, pageSize: 5 };
  return endpointPagers[endpointId];
}

function handleEndpointPageChange(endpointId: string, pageInfo: { current: number; pageSize: number }) {
  const pager = ensurePager(endpointId);
  pager.current = pageInfo.current;
  pager.pageSize = pageInfo.pageSize;
}

function resetEndpointForm(projectId = selectedProjectId.value) {
  Object.assign(endpointForm, {
    code: '',
    developerIds: [],
    id: '',
    name: '',
    ownerId: '',
    projectId,
    skillIds: [],
    sort: '',
    status: 'active',
    testerIds: [],
    type: 'web',
  });
}

function openEndpointCreate() {
  if (!selectedProjectId.value) {
    MessagePlugin.warning('请先选择项目');
    return;
  }

  resetEndpointForm();
  endpointMode.value = 'create';
  endpointVisible.value = true;
}

function openEndpointEdit(endpoint: SprintMvpApi.ProjectEndpoint) {
  Object.assign(endpointForm, {
    code: endpoint.code,
    developerIds: [...(endpoint.developerIds || [])],
    id: endpoint.id,
    name: endpoint.name,
    ownerId: endpoint.ownerId || '',
    projectId: endpoint.projectId,
    skillIds: [...(endpoint.skillIds || [])],
    sort: String(endpoint.sort ?? ''),
    status: endpoint.status,
    testerIds: [...(endpoint.testerIds || [])],
    type: endpoint.type,
  });
  endpointMode.value = 'edit';
  endpointVisible.value = true;
}

async function saveEndpoint() {
  if (!(await validateForm(endpointFormRef.value))) return;
  if (!endpointForm.projectId || !endpointForm.name.trim() || !endpointForm.type) {
    MessagePlugin.warning('端名称和端类型必填');
    return;
  }

  const payload = {
    developerIds: [...endpointForm.developerIds],
    name: endpointForm.name.trim(),
    ownerId: endpointForm.ownerId || undefined,
    skillIds: [...endpointForm.skillIds],
    sort: endpointForm.sort ? Number(endpointForm.sort) : undefined,
    status: endpointForm.status || undefined,
    testerIds: [...endpointForm.testerIds],
    type: endpointForm.type,
  };
  if (endpointMode.value === 'create') {
    if (!endpointForm.code.trim()) {
      MessagePlugin.warning('端编码必填');
      return;
    }

    await createProjectEndpointApi({
      ...payload,
      code: endpointForm.code.trim().toUpperCase(),
      projectId: endpointForm.projectId,
    });
  } else {
    await updateProjectEndpointApi(endpointForm.id, payload);
  }

  MessagePlugin.success('端配置已保存');
  endpointVisible.value = false;
  await loadData();
}

function resetModuleForm(projectId = selectedProjectId.value, endpointId = '') {
  Object.assign(moduleForm, {
    code: '',
    description: '',
    developerIds: [],
    endpointId,
    id: '',
    name: '',
    ownerId: '',
    projectId,
    sort: '',
    status: 'active',
    testerIds: [],
  });
}

function openModuleCreate(endpointId: string) {
  resetModuleForm(selectedProjectId.value, endpointId);
  moduleMode.value = 'create';
  moduleVisible.value = true;
}

function openModuleEdit(module: SprintMvpApi.FeatureModule) {
  Object.assign(moduleForm, {
    code: module.code,
    description: module.description || '',
    developerIds: [...(module.developerIds || [])],
    endpointId: module.endpointId,
    id: module.id,
    name: module.name,
    ownerId: module.ownerId || '',
    projectId: module.projectId,
    sort: String(module.sort ?? ''),
    status: module.status,
    testerIds: [...(module.testerIds || [])],
  });
  moduleMode.value = 'edit';
  moduleVisible.value = true;
}

async function saveModule() {
  if (!(await validateForm(moduleFormRef.value))) return;
  if (!moduleForm.projectId || !moduleForm.endpointId || !moduleForm.name.trim()) {
    MessagePlugin.warning('模块名称和所属端必填');
    return;
  }

  const payload = {
    description: moduleForm.description.trim() || undefined,
    developerIds: [...moduleForm.developerIds],
    name: moduleForm.name.trim(),
    ownerId: moduleForm.ownerId || undefined,
    sort: moduleForm.sort ? Number(moduleForm.sort) : undefined,
    status: moduleForm.status || undefined,
    testerIds: [...moduleForm.testerIds],
  };
  if (moduleMode.value === 'create') {
    if (!moduleForm.code.trim()) {
      MessagePlugin.warning('模块编码必填');
      return;
    }

    await createFeatureModuleApi({
      ...payload,
      code: moduleForm.code.trim().toUpperCase(),
      endpointId: moduleForm.endpointId,
      projectId: moduleForm.projectId,
    });
  } else {
    await updateFeatureModuleApi(moduleForm.id, payload);
  }

  MessagePlugin.success('模块配置已保存');
  moduleVisible.value = false;
  ensurePager(moduleForm.endpointId).current = 1;
  await loadData();
}

async function loadData() {
  loading.value = true;
  try {
    [projects.value, endpoints.value, modules.value, users.value, skills.value] = await Promise.all([
      listProjectsApi(),
      listProjectEndpointsApi(),
      listFeatureModulesApi(),
      listUserOptionsApi(),
      listSkillsApi(true),
    ]);
    if (!selectedProjectId.value && projects.value.length > 0) {
      selectedProjectId.value = projects.value[0]!.id;
    }
  } finally {
    loading.value = false;
  }
}

watch(selectedProjectId, () => {
  for (const endpoint of selectedEndpoints.value) {
    ensurePager(endpoint.id).current = 1;
  }
});

onMounted(loadData);
</script>

<template>
  <div class="multi-endpoint-page sprint-list-page">
    <section class="sprint-page-title">
      <h2>多端管理</h2>
      <p>按项目维护端和端下功能模块，配置负责人、开发人员和测试人员。</p>
    </section>

    <section class="multi-layout">
      <aside class="project-tree-panel">
        <div class="panel-title">
          <h3>项目列表</h3>
          <TButton size="small" variant="outline" @click="loadData">刷新</TButton>
        </div>
        <div v-if="projects.length === 0 && !loading" class="empty-state">暂无项目数据</div>
        <div v-else class="project-card-list">
          <button
            v-for="project in projects"
            :key="project.id"
            class="project-card"
            :class="{ active: project.id === selectedProjectId }"
            type="button"
            @click="selectedProjectId = project.id"
          >
            <span class="project-card-head">
              <strong>{{ project.name }}</strong>
              <TTag size="small" variant="light">{{ project.status }}</TTag>
            </span>
            <span class="project-card-code">{{ project.code }}</span>
            <span class="project-card-desc">
              {{ project.description || '暂无项目说明' }}
            </span>
            <span class="project-card-meta">
              <span>端 {{ projectEndpointCount(project.id) }}</span>
              <span>模块 {{ projectModuleCount(project.id) }}</span>
              <span>经理 {{ resolveUserName(project.projectManagerId) }}</span>
            </span>
            <span class="project-card-stack">
              {{ project.frontendTechStack || '前端未配置' }} / {{ project.backendTechStack || '后端未配置' }}
            </span>
          </button>
        </div>
      </aside>

      <main class="endpoint-workspace">
        <div class="workspace-head">
          <div>
            <h3>{{ selectedProject?.name || '请选择项目' }}</h3>
            <p>{{ selectedProject?.code || '-' }}</p>
          </div>
          <TButton theme="primary" :disabled="!selectedProjectId" @click="openEndpointCreate">
            新增端
          </TButton>
        </div>

        <div v-if="!selectedProjectId" class="empty-state large">请选择左侧项目</div>
        <div v-else-if="selectedEndpoints.length === 0 && !loading" class="empty-state large">
          当前项目暂无端配置
        </div>

        <TCard
          v-for="endpoint in selectedEndpoints"
          :key="endpoint.id"
          class="endpoint-card"
          :bordered="true"
        >
          <template #title>
            <div class="endpoint-title">
              <div>
                <strong>{{ endpoint.name }}</strong>
                <span>{{ endpoint.code }} · {{ endpointTypeLabel(endpoint.type) }}</span>
              </div>
              <TTag variant="light">{{ endpoint.status }}</TTag>
            </div>
          </template>
          <template #actions>
            <TSpace>
              <TLink theme="primary" @click="openEndpointEdit(endpoint)">编辑端</TLink>
              <TLink theme="primary" @click="openModuleCreate(endpoint.id)">新增模块</TLink>
            </TSpace>
          </template>

          <dl class="endpoint-people">
            <div>
              <dt>负责人</dt>
              <dd>{{ resolveUserName(endpoint.ownerId) }}</dd>
            </div>
            <div>
              <dt>开发</dt>
              <dd>{{ resolveUserNames(endpoint.developerIds) }}</dd>
            </div>
            <div>
              <dt>测试</dt>
              <dd>{{ resolveUserNames(endpoint.testerIds) }}</dd>
            </div>
            <div>
              <dt>Skill</dt>
              <dd>{{ resolveSkillNames(endpoint.skillIds) }}</dd>
            </div>
          </dl>

          <div class="module-table-head">
            <h4>模块管理</h4>
          </div>
          <TTable
            row-key="id"
            class="sprint-compact-table"
            :columns="moduleColumns"
            :data="endpointPageModules(endpoint.id)"
            :loading="loading"
            size="small"
            hover
          >
            <template #ownerId="{ row }">
              {{ resolveUserName(row.ownerId) }}
            </template>
            <template #developerIds="{ row }">
              {{ resolveUserNames(row.developerIds) }}
            </template>
            <template #testerIds="{ row }">
              {{ resolveUserNames(row.testerIds) }}
            </template>
            <template #status="{ row }">
              <TTag variant="light">{{ row.status }}</TTag>
            </template>
            <template #actions="{ row }">
              <TLink theme="primary" @click="openModuleEdit(row)">编辑</TLink>
            </template>
          </TTable>
          <div class="module-pagination">
            <TPagination
              v-model="ensurePager(endpoint.id).current"
              v-model:page-size="ensurePager(endpoint.id).pageSize"
              :page-size-options="[5, 10, 20]"
              :total="endpointModules(endpoint.id).length"
              size="small"
              show-jumper
              show-page-size
              @change="handleEndpointPageChange(endpoint.id, $event)"
            />
          </div>
        </TCard>
      </main>
    </section>

    <TDrawer
      v-model:visible="endpointVisible"
      :size="'520px'"
      :header="endpointMode === 'create' ? '新增端' : '编辑端'"
      confirm-btn="保存"
      @confirm="saveEndpoint"
    >
      <TForm ref="endpointFormRef" :data="endpointForm" :rules="endpointRules" label-width="90px">
        <TFormItem label="端编码" name="code">
          <TInput v-model="endpointForm.code" :disabled="endpointMode === 'edit'" placeholder="WEB" />
        </TFormItem>
        <TFormItem label="端名称" name="name">
          <TInput v-model="endpointForm.name" placeholder="Web网站" />
        </TFormItem>
        <TFormItem label="端类型" name="type">
          <TSelect v-model="endpointForm.type" :options="endpointTypeOptions" />
        </TFormItem>
        <TFormItem label="负责人">
          <TSelect v-model="endpointForm.ownerId" clearable filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="开发人员">
          <TSelect v-model="endpointForm.developerIds" multiple filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="测试人员">
          <TSelect v-model="endpointForm.testerIds" multiple filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="Skill">
          <TSelect v-model="endpointForm.skillIds" multiple filterable :options="skillOptions" />
        </TFormItem>
        <TFormItem label="状态">
          <TSelect v-model="endpointForm.status" :options="statusOptions" />
        </TFormItem>
        <TFormItem label="排序" name="sort">
          <TInput v-model="endpointForm.sort" placeholder="100" />
        </TFormItem>
      </TForm>
    </TDrawer>

    <TDrawer
      v-model:visible="moduleVisible"
      :size="'520px'"
      :header="moduleMode === 'create' ? '新增模块' : '编辑模块'"
      confirm-btn="保存"
      @confirm="saveModule"
    >
      <TForm ref="moduleFormRef" :data="moduleForm" :rules="moduleRules" label-width="90px">
        <TFormItem label="所属端" name="endpointId">
          <TSelect
            v-model="moduleForm.endpointId"
            :disabled="moduleMode === 'edit'"
            :options="selectedEndpoints.map((item) => ({ label: item.name, value: item.id }))"
          />
        </TFormItem>
        <TFormItem label="模块编码" name="code">
          <TInput v-model="moduleForm.code" :disabled="moduleMode === 'edit'" placeholder="ORDER" />
        </TFormItem>
        <TFormItem label="模块名称" name="name">
          <TInput v-model="moduleForm.name" placeholder="订单管理" />
        </TFormItem>
        <TFormItem label="模块说明">
          <TTextarea v-model="moduleForm.description" :autosize="{ minRows: 3, maxRows: 5 }" />
        </TFormItem>
        <TFormItem label="负责人">
          <TSelect v-model="moduleForm.ownerId" clearable filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="开发人员">
          <TSelect v-model="moduleForm.developerIds" multiple filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="测试人员">
          <TSelect v-model="moduleForm.testerIds" multiple filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="状态">
          <TSelect v-model="moduleForm.status" :options="statusOptions" />
        </TFormItem>
        <TFormItem label="排序" name="sort">
          <TInput v-model="moduleForm.sort" placeholder="100" />
        </TFormItem>
      </TForm>
    </TDrawer>
  </div>
</template>

<style scoped>
.multi-layout {
  display: grid;
  grid-template-columns: 280px minmax(0, 1fr);
  gap: 12px;
  min-height: 620px;
}

.project-tree-panel,
.endpoint-workspace {
  background: var(--td-bg-color-container);
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.project-tree-panel {
  padding: 12px;
}

.panel-title,
.workspace-head,
.module-table-head,
.endpoint-title {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.panel-title {
  padding-bottom: 10px;
  margin-bottom: 10px;
  border-bottom: 1px solid var(--td-component-border);
}

.panel-title h3,
.workspace-head h3,
.module-table-head h4 {
  margin: 0;
  font-size: 16px;
}

.project-card-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.project-card {
  display: flex;
  width: 100%;
  flex-direction: column;
  gap: 8px;
  padding: 12px;
  color: var(--td-text-color-primary);
  text-align: left;
  background: var(--td-bg-color-container);
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
  cursor: pointer;
  transition:
    border-color 0.2s ease,
    box-shadow 0.2s ease,
    background-color 0.2s ease;
}

.project-card:hover,
.project-card.active {
  background: var(--td-brand-color-light);
  border-color: var(--td-brand-color);
  box-shadow: 0 2px 8px rgb(0 0 0 / 6%);
}

.project-card-head,
.project-card-meta {
  display: flex;
  gap: 8px;
  align-items: center;
  justify-content: space-between;
}

.project-card-head strong {
  min-width: 0;
  overflow: hidden;
  font-size: 14px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-card-code,
.project-card-desc,
.project-card-stack {
  color: var(--td-text-color-secondary);
  font-size: 12px;
  line-height: 1.5;
}

.project-card-desc {
  display: -webkit-box;
  min-height: 36px;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.project-card-meta {
  flex-wrap: wrap;
  justify-content: flex-start;
  color: var(--td-text-color-placeholder);
  font-size: 12px;
}

.project-card-stack {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.endpoint-workspace {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 12px;
}

.workspace-head {
  padding: 4px 4px 12px;
  border-bottom: 1px solid var(--td-component-border);
}

.workspace-head p {
  margin: 4px 0 0;
  color: var(--td-text-color-secondary);
}

.empty-state {
  padding: 14px;
  color: var(--td-text-color-secondary);
  background: var(--td-bg-color-container-hover);
  border-radius: 6px;
}

.empty-state.large {
  display: grid;
  min-height: 180px;
  place-items: center;
}

.endpoint-card :deep(.t-card__header) {
  min-height: 48px;
  padding: 12px 16px;
}

.endpoint-card :deep(.t-card__body) {
  padding: 12px 16px 14px;
}

.endpoint-title strong {
  display: block;
}

.endpoint-title span {
  display: block;
  margin-top: 4px;
  color: var(--td-text-color-secondary);
  font-size: 12px;
}

.endpoint-people {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  padding-bottom: 12px;
  margin: 0 0 12px;
  font-size: 12px;
  border-bottom: 1px solid var(--td-component-border);
}

.endpoint-people div {
  min-width: 0;
}

.endpoint-people dt {
  margin-bottom: 4px;
  color: var(--td-text-color-secondary);
}

.endpoint-people dd {
  margin: 0;
  word-break: break-all;
}

.module-table-head {
  padding: 0 0 10px;
}

.module-pagination {
  display: flex;
  justify-content: flex-end;
  padding-top: 10px;
  border-top: 1px solid var(--td-component-border);
}

@media (max-width: 900px) {
  .multi-layout {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .endpoint-people {
    grid-template-columns: 1fr;
  }
}
</style>
