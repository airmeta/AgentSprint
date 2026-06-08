<script lang="ts" setup>
import type { SprintMvpApi, SprintUserApi } from '#/api/sprint/mvp';

import { CircleAlert } from '@vben/icons';

import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

import {
  Button as TButton,
  Dialog as TDialog,
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Link as TLink,
  MessagePlugin,
  Select as TSelect,
  Space as TSpace,
  Table as TTable,
  Tag as TTag,
  Textarea as TTextarea,
  Tooltip as TTooltip,
} from 'tdesign-vue-next';

import { listAgentTokensApi, type SystemApi } from '#/api';
import {
  completeDevelopmentTaskApi,
  getDevelopmentTaskPromptApi,
  listMyDevelopmentTasksApi,
  listProjectsApi,
  listRequirementsApi,
  listUserOptionsApi,
} from '#/api/sprint/mvp';

import '../_shared/table-layout.css';
import {
  buildAgentsprintMcpSetupPrompt,
  buildAgentsprintMcpSetupPromptWithToken,
} from '../_shared/mcpSetupPrompt';

const loading = ref(false);
const router = useRouter();
const promptVisible = ref(false);
const mcpCopyVisible = ref(false);
const promptResult = ref<SprintMvpApi.TaskPrompt>();
const mcpSetupContent = ref('');
const agentTokens = ref<SystemApi.AgentToken[]>([]);
const selectedAgentTokenId = ref('');
const projects = ref<SprintMvpApi.Project[]>([]);
const requirements = ref<SprintMvpApi.Requirement[]>([]);
const tasks = ref<SprintMvpApi.DevelopmentTask[]>([]);
const users = ref<SprintUserApi.UserOption[]>([]);
const currentTask = ref<SprintMvpApi.DevelopmentTask>();
const filters = reactive({
  projectId: '',
  requirementId: '',
  status: '',
});
const pagination = reactive({
  current: 1,
  pageSize: 10,
});

const projectMap = computed(() => Object.fromEntries(projects.value.map((item) => [item.id, item])));
const requirementMap = computed(() =>
  Object.fromEntries(requirements.value.map((item) => [item.id, item])),
);
const userMap = computed(() => Object.fromEntries(users.value.map((item) => [item.id, item])));
const validAgentTokens = computed(() =>
  agentTokens.value.filter(
    (token) =>
      token.status === 1 && !token.revokedAt && new Date(token.expiresAt).getTime() > Date.now(),
  ),
);
const agentTokenOptions = computed(() =>
  validAgentTokens.value.map((token) => ({
    label: `${token.name} (${token.maskedToken})`,
    value: token.id,
  })),
);
const projectOptions = computed(() =>
  projects.value.map((item) => ({ label: `${item.code} · ${item.name}`, value: item.id })),
);
const requirementOptions = computed(() =>
  requirements.value
    .filter((item) => !filters.projectId || item.projectId === filters.projectId)
    .map((item) => ({ label: item.title, value: item.id })),
);

const columns = [
  { colKey: 'title', title: '任务标题' },
  { colKey: 'projectId', title: '项目', width: 160 },
  { colKey: 'requirementId', title: '需求', width: 200 },
  { colKey: 'status', title: '状态', width: 120 },
  { colKey: 'priority', title: '优先级', width: 90 },
  { colKey: 'assignedBy', title: '指派人', width: 140 },
  { colKey: 'updateTime', title: '更新时间', width: 180 },
  { colKey: 'actions', title: '操作', width: 220 },
];

const statusText: Record<string, string> = {
  assigned: '已指派',
  completed: '已完成',
  in_progress: '推进中',
  pending_assign: '待指派',
};
const statusOptions = [
  { label: '已指派', value: 'assigned' },
  { label: '推进中', value: 'in_progress' },
  { label: '已完成', value: 'completed' },
];
const filteredTasks = computed(() =>
  tasks.value.filter(
    (task) =>
      (!filters.projectId || task.projectId === filters.projectId) &&
      (!filters.requirementId || task.requirementId === filters.requirementId) &&
      (!filters.status || task.status === filters.status),
  ),
);
const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  pageSizeOptions: [10, 20, 50],
  showJumper: true,
  showPageSize: true,
  size: 'small' as const,
  total: filteredTasks.value.length,
}));
const displayedMcpSetupPrompt = computed(() =>
  buildAgentsprintMcpSetupPrompt(promptResult.value?.mcpSetupPrompt.content),
);

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

async function loadTasks() {
  loading.value = true;
  try {
    [projects.value, requirements.value, users.value, tasks.value, agentTokens.value] = await Promise.all([
      listProjectsApi(),
      listRequirementsApi(),
      listUserOptionsApi(),
      listMyDevelopmentTasksApi(),
      listAgentTokensApi(),
    ]);
    pagination.current = 1;
  } finally {
    loading.value = false;
  }
}

function handleProjectFilterChange() {
  filters.requirementId = '';
  pagination.current = 1;
}

function handleFilterChange() {
  pagination.current = 1;
}

function queryTasks() {
  pagination.current = 1;
}

function resetFilters() {
  Object.assign(filters, {
    projectId: '',
    requirementId: '',
    status: '',
  });
  pagination.current = 1;
}

function goTaskHall() {
  router.push('/sprint/tasks');
}

async function advanceTask(task: SprintMvpApi.DevelopmentTask) {
  const result = await getDevelopmentTaskPromptApi(task.id);
  promptResult.value = result;
  currentTask.value = task;
  promptVisible.value = true;
  await loadTasks();
}

function openDetail(task: SprintMvpApi.DevelopmentTask) {
  router.push(`/sprint/tasks/detail/${task.id}`);
}

async function completeTask(task: SprintMvpApi.DevelopmentTask) {
  await completeDevelopmentTaskApi(task.id);
  MessagePlugin.success('任务已完成；同一需求下全部任务完成后需求进入待测试');
  await loadTasks();
}

function fallbackCopyText(content: string) {
  const textarea = document.createElement('textarea');
  textarea.value = content;
  textarea.style.position = 'fixed';
  textarea.style.left = '-9999px';
  textarea.style.top = '0';
  textarea.setAttribute('readonly', 'readonly');
  document.body.append(textarea);
  textarea.select();
  textarea.setSelectionRange(0, textarea.value.length);
  try {
    return document.execCommand('copy');
  } finally {
    textarea.remove();
  }
}

async function copyPrompt(content: string) {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(content);
    } else if (!fallbackCopyText(content)) {
      throw new Error('Clipboard fallback failed');
    }
    MessagePlugin.success('写入剪切板成功');
    return true;
  } catch {
    if (fallbackCopyText(content)) {
      MessagePlugin.success('写入剪切板成功');
      return true;
    }
    MessagePlugin.error('写入剪切板失败，请手动复制文本框内容');
    return false;
  }
}

async function openMcpCopy() {
  if (!currentTask.value) return;
  selectedAgentTokenId.value = validAgentTokens.value[0]?.id || '';
  await refreshMcpSetupPrompt();
  mcpCopyVisible.value = true;
}

function selectedAgentToken() {
  return validAgentTokens.value.find((token) => token.id === selectedAgentTokenId.value);
}

async function refreshMcpSetupPrompt() {
  if (!currentTask.value || !selectedAgentTokenId.value) {
    mcpSetupContent.value = '';
    return;
  }

  const result = await getDevelopmentTaskPromptApi(currentTask.value.id);
  promptResult.value = result;
  const token = selectedAgentToken();
  const bearerToken = `Bearer ${token?.token || token?.maskedToken || ''}`;
  mcpSetupContent.value = buildAgentsprintMcpSetupPromptWithToken(
    result.mcpSetupPrompt.content,
    bearerToken,
  );
}

async function handleAgentTokenChange() {
  await refreshMcpSetupPrompt();
}

async function copyMcpSetupPrompt() {
  if (!selectedAgentTokenId.value) {
    MessagePlugin.warning('请选择一个有效令牌');
    return;
  }
  if (!mcpSetupContent.value) {
    await refreshMcpSetupPrompt();
  }
  const copied = await copyPrompt(mcpSetupContent.value);
  if (copied) {
    mcpCopyVisible.value = false;
  }
}

onMounted(loadTasks);
</script>

<template>
  <div class="my-tasks-page sprint-list-page">
    <section class="sprint-page-title">
      <h2>我的任务</h2>
      <p>当前账号被指派的需求拆解任务。</p>
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
            @change="handleFilterChange"
          />
        </label>
        <label class="sprint-filter-field">
          <span>状态</span>
          <TSelect
            v-model="filters.status"
            clearable
            :options="statusOptions"
            placeholder="全部状态"
            @change="handleFilterChange"
          />
        </label>
        <div class="sprint-filter-actions">
          <TButton theme="primary" :disabled="loading" @click="queryTasks">查询</TButton>
          <TButton variant="outline" :disabled="loading" @click="resetFilters">重置</TButton>
        </div>
      </div>
    </section>

    <section class="sprint-table-panel">
      <div class="sprint-table-header">
        <h3>任务列表</h3>
        <div class="sprint-table-actions">
          <TButton theme="primary" @click="goTaskHall">接取任务</TButton>
          <TButton shape="circle" variant="outline" title="刷新" :loading="loading" @click="loadTasks">↻</TButton>
        </div>
      </div>

      <TTable
        row-key="id"
        class="sprint-compact-table"
        :columns="columns"
        :data="filteredTasks"
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
        <template #assignedBy="{ row }">
          {{ row.assignedBy ? userMap[row.assignedBy]?.displayName || row.assignedBy : '-' }}
        </template>
        <template #updateTime="{ row }">
          {{ row.updateTime || row.createTime }}
        </template>
        <template #actions="{ row }">
          <TSpace class="sprint-row-actions">
            <TLink theme="primary" @click="openDetail(row)">详情</TLink>
            <TLink theme="primary" @click="advanceTask(row)">任务推进</TLink>
            <TLink v-if="row.status !== 'completed'" theme="primary" @click="completeTask(row)">
              完成
            </TLink>
          </TSpace>
        </template>
      </TTable>
    </section>

    <TDrawer v-model:visible="promptVisible" :size="'60%'" header="任务推进" :footer="false">
      <section v-if="promptResult" class="prompt-drawer">
        <header class="prompt-summary">
          <div>
            <h3>{{ currentTask?.title || '任务推进' }}</h3>
            <p>
              {{ projectMap[currentTask?.projectId || '']?.name || currentTask?.projectId }}
              · 任务 ID：{{ promptResult.taskId }}
            </p>
          </div>
        </header>

        <section class="prompt-section">
          <div class="prompt-section-header">
            <div>
              <h4>{{ promptResult.mcpSetupPrompt.title }}</h4>
            </div>
            <TButton theme="primary" @click="openMcpCopy">
              初次接入配置
            </TButton>
          </div>
          <p class="prompt-usage">
            <strong>首次接入</strong>或本地 Codex 未发现
            <strong>AgentSprint MCP</strong> 时复制执行；默认只配置 MCP endpoint 和
            <strong>Authorization</strong>，不要默认写入
            <strong>X-AgentSprint-Api-Base-Url</strong>。
          </p>
          <div class="prompt-content">
            {{ displayedMcpSetupPrompt }}
          </div>
        </section>

        <section class="prompt-section">
          <div class="prompt-section-header">
            <div>
              <div class="prompt-title-line">
                <h4>{{ promptResult.taskExecutionPrompt.title }}</h4>
                <TTooltip placement="top" theme="light">
                  <template #content>
                    <ul class="prompt-note-list">
                      <li v-for="item in promptResult.taskExecutionPrompt.notes" :key="item">
                        {{ item }}
                      </li>
                    </ul>
                  </template>
                  <CircleAlert class="prompt-note-icon" />
                </TTooltip>
              </div>
            </div>
            <TButton theme="primary" @click="copyPrompt(promptResult.taskExecutionPrompt.content)">
              任务推进提示词
            </TButton>
          </div>
          <p class="prompt-usage">
            <strong>日常推进</strong>只复制此段，Codex 会按
            <strong>任务 ID</strong> 通过 <strong>MCP</strong> 拉取任务、需求和 Skill 上下文，并按
            <strong>next_work</strong> 继续接取后续缺陷或任务。
          </p>
          <div class="prompt-content">
            {{ promptResult.taskExecutionPrompt.content }}
          </div>
        </section>
      </section>
    </TDrawer>

    <TDialog
      v-model:visible="mcpCopyVisible"
      header="初次接入配置"
      width="620px"
      confirm-btn="写入剪切板"
      @confirm="copyMcpSetupPrompt"
    >
      <section class="mcp-copy-dialog">
        <p>
          选择一个有效令牌后会重新拉取初次接入提示词，并只替换 Authorization
          令牌；默认不写入 X-AgentSprint-Api-Base-Url。只有用户明确提供远程 MCP
          服务可访问的 AgentSprint API 地址时，才追加该可选覆盖配置，不要固定写入
          http://localhost:5000。
        </p>
        <TForm label-width="112px">
          <TFormItem label="令牌记录">
            <TSelect
              v-model="selectedAgentTokenId"
              filterable
              :options="agentTokenOptions"
              placeholder="请选择令牌"
              @change="handleAgentTokenChange"
            />
          </TFormItem>
        </TForm>
        <TTextarea :model-value="mcpSetupContent" readonly autosize />
      </section>
    </TDialog>

  </div>
</template>

<style scoped>
.prompt-drawer {
  display: grid;
  gap: 16px;
}

.prompt-summary,
.prompt-section-header {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  justify-content: space-between;
}

.prompt-summary {
  padding-bottom: 12px;
  border-bottom: 1px solid var(--td-component-border);
}

.prompt-summary h3,
.prompt-section h4 {
  margin: 0;
}

.prompt-summary p {
  margin: 6px 0 0;
  color: var(--td-text-color-secondary);
}

.prompt-section {
  display: grid;
  gap: 12px;
  padding: 16px;
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.prompt-title-line {
  display: flex;
  gap: 6px;
  align-items: center;
}

.prompt-note-icon {
  width: 16px;
  height: 16px;
  color: var(--td-warning-color);
  cursor: help;
}

.prompt-note-list {
  max-width: 320px;
  padding-left: 18px;
  margin: 0;
  line-height: 1.7;
}

.prompt-note-list li + li {
  margin-top: 4px;
}

.prompt-usage {
  margin: 0;
  color: var(--td-text-color-secondary);
  line-height: 1.7;
}

.prompt-content {
  max-height: 360px;
  min-height: 220px;
  padding: 12px 14px;
  overflow: auto;
  font-size: 13px;
  font-family: ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', monospace;
  line-height: 1.7;
  white-space: pre-wrap;
  word-break: break-word;
  background: var(--td-bg-color-secondarycontainer);
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.mcp-copy-dialog {
  display: grid;
  gap: 12px;
}

.mcp-copy-dialog p {
  margin: 0;
  color: var(--td-text-color-secondary);
  line-height: 1.7;
}
</style>
