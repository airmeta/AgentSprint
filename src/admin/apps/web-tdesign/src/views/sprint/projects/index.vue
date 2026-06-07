<script lang="ts" setup>
import type { SprintMvpApi, SprintUserApi } from '#/api/sprint/mvp';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { computed, onMounted, reactive, ref } from 'vue';

import {
  Button as TButton,
  Card as TCard,
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  Link as TLink,
  MessagePlugin,
  Select as TSelect,
  Tag as TTag,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

import {
  createProjectApi,
  listProjectsApi,
  listUserOptionsApi,
  updateProjectApi,
} from '#/api/sprint/mvp';
import {
  requiredArrayRule,
  requiredHttpUrlRule,
  requiredRule,
  validateForm,
} from '#/views/_shared/form-rules';

const loading = ref(false);
const drawerVisible = ref(false);
const drawerMode = ref<'detail' | 'edit' | 'stats'>('detail');
const createVisible = ref(false);
const createFormRef = ref<FormInstanceFunctions>();
const editFormRef = ref<FormInstanceFunctions>();
const createCodeSeed = ref('');
const selectedProject = ref<SprintMvpApi.Project>();
const projects = ref<SprintMvpApi.Project[]>([]);
const users = ref<SprintUserApi.UserOption[]>([]);

const form = reactive({
  architectId: '',
  backendTechStack: '',
  code: '',
  description: '',
  developerIds: [] as string[],
  frontendTechStack: '',
  name: '',
  productManagerIds: [] as string[],
  projectManagerId: '',
  repositoryUrl: '',
  testerIds: [] as string[],
  testEnvironmentUrl: '',
});
const editForm = reactive({
  architectId: '',
  backendTechStack: '',
  description: '',
  developerIds: [] as string[],
  frontendTechStack: '',
  name: '',
  productManagerIds: [] as string[],
  projectManagerId: '',
  repositoryUrl: '',
  testerIds: [] as string[],
  testEnvironmentUrl: '',
});
const projectRules: FormRules<typeof form> = {
  architectId: requiredRule('请选择架构师', 'change'),
  backendTechStack: requiredRule('请输入后端技术栈'),
  developerIds: requiredArrayRule('请选择至少一名开发人员'),
  frontendTechStack: requiredRule('请输入前端技术栈'),
  name: requiredRule('请输入项目名称'),
  productManagerIds: requiredArrayRule('请选择至少一名产品经理'),
  projectManagerId: requiredRule('请选择项目经理', 'change'),
  repositoryUrl: requiredHttpUrlRule('请输入 http 或 https 仓库地址'),
  testerIds: requiredArrayRule('请选择至少一名测试人员'),
};
const activeProjects = computed(
  () => projects.value.filter((item) => item.status === 'active').length,
);
const userOptions = computed(() =>
  users.value.map((user) => ({ label: `${user.displayName} (${user.username})`, value: user.id })),
);
const userMap = computed(() => Object.fromEntries(users.value.map((item) => [item.id, item])));
const generatedProjectCode = computed(() => generateProjectCode(form.name));

function resetCreateForm() {
  Object.assign(form, {
    architectId: '',
    backendTechStack: '',
    code: '',
    description: '',
    developerIds: [],
    frontendTechStack: '',
    name: '',
    productManagerIds: [],
    projectManagerId: '',
    repositoryUrl: '',
    testerIds: [],
    testEnvironmentUrl: '',
  });
}

function openCreate() {
  resetCreateForm();
  createCodeSeed.value = Date.now().toString(36).toUpperCase();
  createVisible.value = true;
}

function openDrawer(project: SprintMvpApi.Project, mode: typeof drawerMode.value) {
  selectedProject.value = project;
  drawerMode.value = mode;
  if (mode === 'edit') {
    Object.assign(editForm, {
      architectId: project.architectId || '',
      backendTechStack: project.backendTechStack || '',
      description: project.description || '',
      developerIds: [...(project.developerIds || [])],
      frontendTechStack: project.frontendTechStack || '',
      name: project.name,
      productManagerIds: [...(project.productManagerIds || [])],
      projectManagerId: project.projectManagerId || '',
      repositoryUrl: project.repositoryUrl || '',
      testerIds: [...(project.testerIds || [])],
      testEnvironmentUrl: project.testEnvironmentUrl || '',
    });
  }
  drawerVisible.value = true;
}

async function loadProjects() {
  loading.value = true;
  try {
    [projects.value, users.value] = await Promise.all([listProjectsApi(), listUserOptionsApi()]);
  } finally {
    loading.value = false;
  }
}

function resolveUserName(userId?: string) {
  return userId ? userMap.value[userId]?.displayName || userId : '未指定';
}

function resolveUserNames(userIds?: string[]) {
  return userIds && userIds.length > 0 ? userIds.map((id) => resolveUserName(id)).join('、') : '未指定';
}

function generateProjectCode(name: string) {
  const normalized = name
    .trim()
    .toUpperCase()
    .replaceAll(/[^0-9A-Z]+/g, '-')
    .replaceAll(/^-+|-+$/g, '');
  return `${normalized || 'PROJECT'}-${createCodeSeed.value || Date.now().toString(36).toUpperCase()}`;
}

function normalizeProjectPayload(source: typeof form | typeof editForm) {
  const repositoryUrl = source.repositoryUrl.trim();
  if (!repositoryUrl || !/^https?:\/\//i.test(repositoryUrl)) {
    MessagePlugin.warning('仓库地址必须是 http 或 https 地址');
    return;
  }

  if (
    !source.name.trim() ||
    !source.frontendTechStack.trim() ||
    !source.backendTechStack.trim() ||
    !source.projectManagerId ||
    source.productManagerIds.length === 0 ||
    source.developerIds.length === 0 ||
    source.testerIds.length === 0 ||
    !source.architectId
  ) {
    MessagePlugin.warning('项目名称、技术栈和项目团队均为必填');
    return;
  }

  return {
    architectId: source.architectId,
    backendTechStack: source.backendTechStack.trim(),
    description: source.description.trim() || undefined,
    developerIds: [...source.developerIds],
    frontendTechStack: source.frontendTechStack.trim(),
    name: source.name.trim(),
    productManagerIds: [...source.productManagerIds],
    projectManagerId: source.projectManagerId,
    repositoryUrl,
    testerIds: [...source.testerIds],
    testEnvironmentUrl: source.testEnvironmentUrl.trim() || undefined,
  };
}

async function createProject() {
  if (!(await validateForm(createFormRef.value))) return;
  const payload = normalizeProjectPayload(form);
  if (!payload) return;

  await createProjectApi({
    ...payload,
    code: generatedProjectCode.value,
  });
  MessagePlugin.success('项目已创建');
  resetCreateForm();
  createVisible.value = false;
  await loadProjects();
}

async function saveProject() {
  if (!selectedProject.value) return;
  if (!(await validateForm(editFormRef.value))) return;
  const payload = normalizeProjectPayload(editForm);
  if (!payload) return;

  selectedProject.value = await updateProjectApi(selectedProject.value.id, payload);
  MessagePlugin.success('项目已保存');
  drawerVisible.value = false;
  await loadProjects();
}

onMounted(loadProjects);
</script>

<template>
  <div class="project-page">
    <section class="toolbar">
      <div>
        <h2>项目管理</h2>
        <p>按项目聚合需求、任务、测试和缺陷。</p>
      </div>
      <div class="toolbar-stats">
        <div>
          <span>项目数</span>
          <strong>{{ projects.length }}</strong>
        </div>
        <div>
          <span>推进中</span>
          <strong>{{ activeProjects }}</strong>
        </div>
        <TButton theme="primary" @click="openCreate">新增项目</TButton>
      </div>
    </section>

    <section class="project-grid" :class="{ loading }">
      <div v-if="!loading && projects.length === 0" class="project-empty">
        <h3>暂无项目数据</h3>
        <p>先新建项目，再维护端、功能模块、需求和任务闭环。</p>
        <TButton theme="primary" @click="openCreate">新建项目</TButton>
      </div>
      <TCard
        v-for="project in projects"
        :key="project.id"
        class="project-card"
        :bordered="true"
      >
        <div class="card-main">
          <div>
            <span class="code">{{ project.code }}</span>
            <h3>{{ project.name }}</h3>
          </div>
          <TTag theme="success" variant="light">{{ project.status }}</TTag>
        </div>
        <p class="repo">{{ project.repositoryUrl || '未配置仓库' }}</p>
        <div class="card-actions">
          <TLink theme="primary" @click="openDrawer(project, 'edit')">编辑</TLink>
          <TLink theme="primary" @click="openDrawer(project, 'detail')">详情</TLink>
          <TLink theme="primary" @click="openDrawer(project, 'stats')">统计</TLink>
        </div>
      </TCard>
    </section>

    <TDrawer
      v-model:visible="drawerVisible"
      :footer="drawerMode === 'edit'"
      :size="'60%'"
      :header="selectedProject?.name || '项目详情'"
      confirm-btn="保存"
      @confirm="saveProject"
    >
      <div v-if="selectedProject" class="drawer-content">
        <template v-if="drawerMode === 'stats'">
          <div class="stats-grid">
            <div class="stat-box">
              <span>项目状态</span>
              <strong>{{ selectedProject.status }}</strong>
            </div>
            <div class="stat-box">
              <span>创建人</span>
              <strong>{{ selectedProject.createdBy }}</strong>
            </div>
            <div class="stat-box">
              <span>创建时间</span>
              <strong>{{ selectedProject.createTime }}</strong>
            </div>
          </div>
        </template>
        <template v-else-if="drawerMode === 'edit'">
          <TForm ref="editFormRef" :data="editForm" :rules="projectRules" label-width="100px">
            <TFormItem label="项目编码">
              <TInput :value="selectedProject.code" disabled />
            </TFormItem>
            <TFormItem label="项目名称" name="name">
              <TInput v-model="editForm.name" />
            </TFormItem>
            <TFormItem label="详细信息">
              <TTextarea v-model="editForm.description" :autosize="{ minRows: 3, maxRows: 6 }" />
            </TFormItem>
            <TFormItem label="仓库地址" name="repositoryUrl">
              <TInput v-model="editForm.repositoryUrl" placeholder="https://github.com/org/repo.git" />
            </TFormItem>
            <TFormItem label="测试环境">
              <TInput v-model="editForm.testEnvironmentUrl" />
            </TFormItem>
            <TFormItem label="前端技术栈" name="frontendTechStack">
              <TInput v-model="editForm.frontendTechStack" placeholder="Vue 3 / Vite / TDesign" />
            </TFormItem>
            <TFormItem label="后端技术栈" name="backendTechStack">
              <TInput v-model="editForm.backendTechStack" placeholder=".NET 10 / EF Core / MySQL" />
            </TFormItem>
            <TFormItem label="项目经理" name="projectManagerId">
              <TSelect v-model="editForm.projectManagerId" filterable :options="userOptions" />
            </TFormItem>
            <TFormItem label="产品经理" name="productManagerIds">
              <TSelect v-model="editForm.productManagerIds" multiple filterable :options="userOptions" />
            </TFormItem>
            <TFormItem label="开发人员" name="developerIds">
              <TSelect v-model="editForm.developerIds" multiple filterable :options="userOptions" />
            </TFormItem>
            <TFormItem label="测试人员" name="testerIds">
              <TSelect v-model="editForm.testerIds" multiple filterable :options="userOptions" />
            </TFormItem>
            <TFormItem label="架构师" name="architectId">
              <TSelect v-model="editForm.architectId" filterable :options="userOptions" />
            </TFormItem>
          </TForm>
        </template>
        <template v-else>
          <dl>
            <dt>项目编码</dt>
            <dd>{{ selectedProject.code }}</dd>
            <dt>详细信息</dt>
            <dd>{{ selectedProject.description || '未填写' }}</dd>
            <dt>仓库地址</dt>
            <dd>{{ selectedProject.repositoryUrl || '未配置' }}</dd>
            <dt>测试环境</dt>
            <dd>{{ selectedProject.testEnvironmentUrl || '未配置' }}</dd>
            <dt>前端技术栈</dt>
            <dd>{{ selectedProject.frontendTechStack || '未填写' }}</dd>
            <dt>后端技术栈</dt>
            <dd>{{ selectedProject.backendTechStack || '未填写' }}</dd>
            <dt>项目经理</dt>
            <dd>{{ resolveUserName(selectedProject.projectManagerId) }}</dd>
            <dt>产品经理</dt>
            <dd>{{ resolveUserNames(selectedProject.productManagerIds) }}</dd>
            <dt>开发人员</dt>
            <dd>{{ resolveUserNames(selectedProject.developerIds) }}</dd>
            <dt>测试人员</dt>
            <dd>{{ resolveUserNames(selectedProject.testerIds) }}</dd>
            <dt>架构师</dt>
            <dd>{{ resolveUserName(selectedProject.architectId) }}</dd>
            <dt>状态</dt>
            <dd>{{ selectedProject.status }}</dd>
          </dl>
        </template>
      </div>
    </TDrawer>

    <TDrawer
      v-model:visible="createVisible"
      :size="'60%'"
      header="新增项目"
      confirm-btn="保存"
      @confirm="createProject"
    >
      <TForm ref="createFormRef" :data="form" :rules="projectRules" label-width="100px">
        <TFormItem label="项目编码">
          <TInput :value="generatedProjectCode" disabled />
        </TFormItem>
        <TFormItem label="项目名称" name="name">
          <TInput v-model="form.name" placeholder="AgentSprint" />
        </TFormItem>
        <TFormItem label="详细信息">
          <TTextarea
            v-model="form.description"
            :autosize="{ minRows: 3, maxRows: 6 }"
            placeholder="项目背景、交付范围和关键约束"
          />
        </TFormItem>
        <TFormItem label="仓库地址" name="repositoryUrl">
          <TInput v-model="form.repositoryUrl" placeholder="https://github.com/org/repo.git" />
        </TFormItem>
        <TFormItem label="测试环境">
          <TInput v-model="form.testEnvironmentUrl" placeholder="http://localhost:5999" />
        </TFormItem>
        <TFormItem label="前端技术栈" name="frontendTechStack">
          <TInput v-model="form.frontendTechStack" placeholder="Vue 3 / Vite / TDesign" />
        </TFormItem>
        <TFormItem label="后端技术栈" name="backendTechStack">
          <TInput v-model="form.backendTechStack" placeholder=".NET 10 / EF Core / MySQL" />
        </TFormItem>
        <TFormItem label="项目经理" name="projectManagerId">
          <TSelect v-model="form.projectManagerId" filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="产品经理" name="productManagerIds">
          <TSelect v-model="form.productManagerIds" multiple filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="开发人员" name="developerIds">
          <TSelect v-model="form.developerIds" multiple filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="测试人员" name="testerIds">
          <TSelect v-model="form.testerIds" multiple filterable :options="userOptions" />
        </TFormItem>
        <TFormItem label="架构师" name="architectId">
          <TSelect v-model="form.architectId" filterable :options="userOptions" />
        </TFormItem>
      </TForm>
    </TDrawer>
  </div>
</template>

<style scoped>
.project-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
}

.toolbar {
  display: flex;
  gap: 16px;
  align-items: center;
  flex-wrap: wrap;
  justify-content: space-between;
  padding: 16px 20px;
  background: var(--td-bg-color-container);
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.toolbar h2 {
  margin: 0;
  font-size: 20px;
}

.toolbar p {
  margin: 6px 0 0;
  color: var(--td-text-color-secondary);
}

.toolbar-stats {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 24px;
}

.toolbar-stats span {
  display: block;
  color: var(--td-text-color-secondary);
  font-size: 12px;
}

.toolbar-stats strong {
  display: block;
  margin-top: 4px;
  font-size: 22px;
}

.project-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 300px));
  gap: 16px;
}

.project-empty {
  grid-column: 1 / -1;
  display: grid;
  gap: 10px;
  justify-items: start;
  padding: 28px;
  background: var(--td-bg-color-container);
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.project-empty h3,
.project-empty p {
  margin: 0;
}

.project-empty p {
  color: var(--td-text-color-secondary);
}

.project-card {
  width: 100%;
  max-width: 300px;
  height: 150px;
}

.project-card :deep(.t-loading__parent) {
  height: 100%;
}

.project-card :deep(.t-card__body) {
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 14px 16px 12px;
}

.card-main,
.card-actions {
  display: flex;
  gap: 12px;
}

.card-main {
  align-items: flex-start;
  justify-content: space-between;
  min-height: 46px;
}

.card-actions {
  align-items: center;
  justify-content: flex-start;
  padding-top: 10px;
  margin-top: auto;
  line-height: 1.6;
  border-top: 1px solid var(--td-component-border);
}

.card-actions :deep(.t-link) {
  min-height: 22px;
}

.code {
  display: block;
  max-width: 210px;
  overflow: hidden;
  color: var(--td-text-color-secondary);
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

h3 {
  margin: 4px 0 0;
  overflow: hidden;
  font-size: 16px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.repo {
  height: 36px;
  margin: 8px 0 10px;
  overflow: hidden;
  color: var(--td-text-color-secondary);
  font-size: 12px;
  line-height: 18px;
  word-break: break-all;
}

.drawer-content dl {
  display: grid;
  grid-template-columns: 100px minmax(0, 1fr);
  gap: 12px;
}

.drawer-content dt {
  color: var(--td-text-color-secondary);
}

.drawer-content dd {
  margin: 0;
  word-break: break-all;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}

.stat-box {
  padding: 14px;
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.stat-box span {
  display: block;
  margin-bottom: 8px;
  color: var(--td-text-color-secondary);
}

.stat-box strong {
  word-break: break-all;
}

.hint {
  color: var(--td-text-color-secondary);
}

@media (max-width: 720px) {
  .project-grid {
    grid-template-columns: 1fr;
  }

  .project-card {
    max-width: none;
  }

  .stats-grid {
    grid-template-columns: 1fr;
  }
}
</style>
