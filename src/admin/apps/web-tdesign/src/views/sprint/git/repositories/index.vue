<script lang="ts" setup>
import type { SprintGitApi } from '#/api/sprint/git';
import type { FormInstanceFunctions, FormRules, PrimaryTableCol } from 'tdesign-vue-next';

import { IconifyIcon } from '@vben/icons';
import { computed, onMounted, reactive, ref } from 'vue';

import {
  Button as TButton,
  Dialog as TDialog,
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  Link as TLink,
  MessagePlugin,
  Pagination as TPagination,
  Select as TSelect,
  Table as TTable,
  Tag as TTag,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

import {
  createGitBranchApi,
  createGitRepositoryApi,
  deleteGitBranchApi,
  listGitAccountsApi,
  listGitRepositoriesApi,
  readGitPushRecordsApi,
  updateGitRepositoryApi,
} from '#/api/sprint/git';
import { formatDateTime } from '#/views/_shared/date-format';
import { requiredHttpUrlRule, requiredRule, validateForm } from '#/views/_shared/form-rules';

defineOptions({ name: 'SprintGitRepositories' });

const loading = ref(false);
const saving = ref(false);
const branchSaving = ref(false);
const recordsLoading = ref(false);
const drawerVisible = ref(false);
const branchDrawerVisible = ref(false);
const recordsVisible = ref(false);
const editingId = ref('');
const branchMode = ref<'create' | 'delete'>('create');
const selectedRepository = ref<SprintGitApi.GitRepository>();
const formRef = ref<FormInstanceFunctions>();
const branchFormRef = ref<FormInstanceFunctions>();
const repositories = ref<SprintGitApi.GitRepository[]>([]);
const accounts = ref<SprintGitApi.GitAccount[]>([]);
const records = ref<SprintGitApi.BranchOperation[]>([]);
const query = reactive({
  gitAccountId: '',
  keyword: '',
  status: '',
});
const pagination = reactive({
  current: 1,
  pageSize: 10,
});
const form = reactive<SprintGitApi.SaveGitRepositoryRequest>({
  code: '',
  defaultBranch: 'main',
  description: '',
  gitAccountId: '',
  localPath: '',
  name: '',
  repositoryUrl: '',
  status: 'active',
});
const branchForm = reactive({
  backupBranch: '',
  branchName: '',
  sourceBranch: '',
});
const recordQuery = reactive({
  branch: '',
});
const rules: FormRules<typeof form> = {
  code: requiredRule('请输入仓库编码'),
  name: requiredRule('请输入仓库名称'),
  repositoryUrl: requiredHttpUrlRule('请输入http或https仓库地址'),
};
const branchRules: FormRules<typeof branchForm> = {
  branchName: requiredRule('请输入分支名称'),
};
const statusOptions = [
  { label: '启用', value: 'active' },
  { label: '停用', value: 'disabled' },
];
const accountOptions = computed(() =>
  accounts.value
    .filter((item) => item.status === 'active')
    .map((item) => ({ label: `${item.name} (${item.username})`, value: item.id })),
);
const accountMap = computed(() => Object.fromEntries(accounts.value.map((item) => [item.id, item])));
const columns: PrimaryTableCol[] = [
  { colKey: 'serial-number', title: '序号', width: 70 },
  { colKey: 'code', title: '仓库编码', width: 140, ellipsis: true },
  { colKey: 'name', title: '仓库名称', minWidth: 150, ellipsis: true },
  { colKey: 'repositoryUrl', title: '仓库地址', minWidth: 260, ellipsis: true },
  { colKey: 'defaultBranch', title: '默认分支', width: 120, ellipsis: true },
  { colKey: 'gitAccountId', title: 'Git账户', width: 160, cell: 'gitAccountId' },
  { colKey: 'status', title: '状态', width: 90, cell: 'status' },
  { colKey: 'operation', title: '操作', width: 330, fixed: 'right', cell: 'operation' },
];
const recordColumns: PrimaryTableCol[] = [
  { colKey: 'serial-number', title: '序号', width: 70 },
  { colKey: 'branchName', title: '分支', width: 140, ellipsis: true },
  { colKey: 'commitHash', title: '提交', width: 120, ellipsis: true },
  { colKey: 'commitMessage', title: '提交说明', minWidth: 220, ellipsis: true },
  { colKey: 'pushedAt', title: '推送时间', width: 180, cell: 'pushedAt' },
  { colKey: 'status', title: '状态', width: 90, cell: 'status' },
];
const pageData = computed(() => {
  const start = (pagination.current - 1) * pagination.pageSize;
  return repositories.value.slice(start, start + pagination.pageSize);
});

function resolveAccountName(id?: string) {
  return id ? accountMap.value[id]?.name || id : '未绑定';
}

function resetForm(row?: SprintGitApi.GitRepository) {
  editingId.value = row?.id || '';
  Object.assign(form, {
    code: row?.code || '',
    defaultBranch: row?.defaultBranch || 'main',
    description: row?.description || '',
    gitAccountId: row?.gitAccountId || '',
    localPath: row?.localPath || '',
    name: row?.name || '',
    repositoryUrl: row?.repositoryUrl || '',
    status: row?.status || 'active',
  });
}

function openCreate() {
  resetForm();
  drawerVisible.value = true;
}

function openEdit(row: SprintGitApi.GitRepository) {
  resetForm(row);
  drawerVisible.value = true;
}

function openBranch(row: SprintGitApi.GitRepository, mode: typeof branchMode.value) {
  selectedRepository.value = row;
  branchMode.value = mode;
  Object.assign(branchForm, {
    backupBranch: '',
    branchName: '',
    sourceBranch: row.defaultBranch || 'main',
  });
  branchDrawerVisible.value = true;
}

async function openRecords(row: SprintGitApi.GitRepository) {
  selectedRepository.value = row;
  records.value = [];
  recordQuery.branch = row.defaultBranch || '';
  recordsVisible.value = true;
  await loadRecords();
}

async function loadRepositories() {
  loading.value = true;
  try {
    [repositories.value, accounts.value] = await Promise.all([
      listGitRepositoriesApi({
        gitAccountId: query.gitAccountId || undefined,
        keyword: query.keyword || undefined,
        status: query.status || undefined,
      }),
      listGitAccountsApi(),
    ]);
    pagination.current = 1;
  } finally {
    loading.value = false;
  }
}

function resetQuery() {
  query.gitAccountId = '';
  query.keyword = '';
  query.status = '';
  void loadRepositories();
}

async function saveRepository() {
  if (saving.value) return;
  if (!(await validateForm(formRef.value))) return;
  saving.value = true;
  try {
    const payload = {
      ...form,
      defaultBranch: form.defaultBranch?.trim() || undefined,
      description: form.description?.trim() || undefined,
      gitAccountId: form.gitAccountId || undefined,
      localPath: form.localPath?.trim() || undefined,
    };
    if (editingId.value) {
      await updateGitRepositoryApi(editingId.value, payload);
    } else {
      await createGitRepositoryApi(payload);
    }
    MessagePlugin.success('Git仓库已保存');
    drawerVisible.value = false;
    await loadRepositories();
  } finally {
    saving.value = false;
  }
}

async function submitBranchOperation() {
  if (!selectedRepository.value || branchSaving.value) return;
  if (!(await validateForm(branchFormRef.value))) return;
  branchSaving.value = true;
  try {
    const payload = {
      backupBranch: branchForm.backupBranch.trim() || undefined,
      branchName: branchForm.branchName.trim(),
      sourceBranch: branchForm.sourceBranch.trim() || undefined,
    };
    const result =
      branchMode.value === 'create'
        ? await createGitBranchApi(selectedRepository.value.id, {
            branchName: payload.branchName,
            sourceBranch: payload.sourceBranch,
          })
        : await deleteGitBranchApi(selectedRepository.value.id, {
            backupBranch: payload.backupBranch,
            branchName: payload.branchName,
          });
    if (result.status === 'success') {
      MessagePlugin.success(branchMode.value === 'create' ? '分支已新增' : '分支已备份并删除');
      branchDrawerVisible.value = false;
    } else {
      MessagePlugin.error(result.message || 'Git分支操作失败');
    }
  } finally {
    branchSaving.value = false;
  }
}

async function loadRecords() {
  if (!selectedRepository.value || recordsLoading.value) return;
  recordsLoading.value = true;
  try {
    records.value = await readGitPushRecordsApi(
      selectedRepository.value.id,
      recordQuery.branch.trim() || undefined,
    );
  } finally {
    recordsLoading.value = false;
  }
}

onMounted(loadRepositories);
</script>

<template>
  <div class="git-page">
    <section class="page-header">
      <div>
        <h2>Git仓库管理</h2>
        <p>维护可选仓库数据源，并执行新增分支、备份删除分支和读取推送记录。</p>
      </div>
    </section>

    <section class="query-panel">
      <TForm :data="query" layout="inline">
        <TFormItem label="关键字">
          <TInput v-model="query.keyword" clearable placeholder="编码 / 名称 / 地址" />
        </TFormItem>
        <TFormItem label="Git账户">
          <TSelect v-model="query.gitAccountId" clearable filterable :options="accountOptions" />
        </TFormItem>
        <TFormItem label="状态">
          <TSelect v-model="query.status" clearable :options="statusOptions" />
        </TFormItem>
        <TFormItem>
          <TButton theme="primary" @click="loadRepositories">查询</TButton>
          <TButton variant="outline" @click="resetQuery">重置</TButton>
        </TFormItem>
      </TForm>
    </section>

    <section class="table-panel">
      <div class="table-toolbar">
        <h3>Git仓库列表</h3>
        <div>
          <TButton theme="primary" @click="openCreate">
            <template #icon><IconifyIcon icon="lucide:plus" /></template>
            新增
          </TButton>
          <TButton variant="outline" :loading="loading" @click="loadRepositories">
            <template #icon><IconifyIcon icon="lucide:refresh-cw" /></template>
          </TButton>
        </div>
      </div>
      <TTable row-key="id" :columns="columns" :data="pageData" :loading="loading" hover>
        <template #gitAccountId="{ row }">
          {{ resolveAccountName(row.gitAccountId) }}
        </template>
        <template #status="{ row }">
          <TTag :theme="row.status === 'active' ? 'success' : 'default'" variant="light">
            {{ row.status === 'active' ? '启用' : '停用' }}
          </TTag>
        </template>
        <template #operation="{ row }">
          <div class="row-actions">
            <TLink theme="primary" @click="openEdit(row)">
              <IconifyIcon icon="lucide:pencil" />
              编辑
            </TLink>
            <TLink theme="primary" @click="openBranch(row, 'create')">
              <IconifyIcon icon="lucide:git-branch-plus" />
              新增分支
            </TLink>
            <TLink theme="danger" @click="openBranch(row, 'delete')">
              <IconifyIcon icon="lucide:trash-2" />
              删除分支
            </TLink>
            <TLink theme="primary" @click="openRecords(row)">
              <IconifyIcon icon="lucide:list-tree" />
              推送记录
            </TLink>
          </div>
        </template>
      </TTable>
      <div class="pagination-bar">
        <span>共计 {{ repositories.length }} 条数据</span>
        <TPagination
          v-model="pagination.current"
          v-model:page-size="pagination.pageSize"
          :total="repositories.length"
          show-jumper
        />
      </div>
    </section>

    <TDrawer
      v-model:visible="drawerVisible"
      :confirm-btn="{ content: '保存', loading: saving }"
      :header="editingId ? '编辑Git仓库' : '新增Git仓库'"
      size="620px"
      @confirm="saveRepository"
    >
      <TForm ref="formRef" :data="form" :rules="rules" label-width="100px">
        <TFormItem label="仓库编码" name="code">
          <TInput v-model="form.code" :disabled="!!editingId" placeholder="AGENTSPRINT" />
        </TFormItem>
        <TFormItem label="仓库名称" name="name">
          <TInput v-model="form.name" placeholder="AgentSprint主仓库" />
        </TFormItem>
        <TFormItem label="仓库地址" name="repositoryUrl">
          <TInput v-model="form.repositoryUrl" placeholder="https://github.com/org/repo.git" />
        </TFormItem>
        <TFormItem label="默认分支">
          <TInput v-model="form.defaultBranch" placeholder="main" />
        </TFormItem>
        <TFormItem label="Git账户">
          <TSelect v-model="form.gitAccountId" clearable filterable :options="accountOptions" />
        </TFormItem>
        <TFormItem label="本地路径">
          <TInput v-model="form.localPath" placeholder="可选，本地工作副本路径" />
        </TFormItem>
        <TFormItem label="状态">
          <TSelect v-model="form.status" :options="statusOptions" />
        </TFormItem>
        <TFormItem label="说明">
          <TTextarea v-model="form.description" :autosize="{ minRows: 3, maxRows: 5 }" />
        </TFormItem>
      </TForm>
    </TDrawer>

    <TDrawer
      v-model:visible="branchDrawerVisible"
      :confirm-btn="{ content: branchMode === 'create' ? '新增分支' : '备份并删除', loading: branchSaving }"
      :header="branchMode === 'create' ? '新增分支' : '删除分支'"
      size="520px"
      @confirm="submitBranchOperation"
    >
      <TForm ref="branchFormRef" :data="branchForm" :rules="branchRules" label-width="110px">
        <TFormItem label="目标仓库">
          <TInput :value="selectedRepository?.name" disabled />
        </TFormItem>
        <TFormItem label="分支名称" name="branchName">
          <TInput v-model="branchForm.branchName" placeholder="feature/demo" />
        </TFormItem>
        <TFormItem v-if="branchMode === 'create'" label="来源分支">
          <TInput v-model="branchForm.sourceBranch" placeholder="main" />
        </TFormItem>
        <TFormItem v-else label="备份分支">
          <TInput v-model="branchForm.backupBranch" placeholder="留空时自动生成 backup/分支-时间" />
        </TFormItem>
      </TForm>
    </TDrawer>

    <TDialog v-model:visible="recordsVisible" width="860px" header="分支推送记录" :footer="false">
      <div class="records-query">
        <TInput v-model="recordQuery.branch" clearable placeholder="分支名称，留空读取全部远端分支" />
        <TButton theme="primary" :loading="recordsLoading" @click="loadRecords">读取</TButton>
      </div>
      <TTable
        row-key="id"
        :columns="recordColumns"
        :data="records"
        :loading="recordsLoading"
        hover
      >
        <template #pushedAt="{ row }">
          {{ row.pushedAt ? formatDateTime(row.pushedAt) : '-' }}
        </template>
        <template #status="{ row }">
          <TTag :theme="row.status === 'success' ? 'success' : 'danger'" variant="light">
            {{ row.status === 'success' ? '成功' : '失败' }}
          </TTag>
        </template>
      </TTable>
    </TDialog>
  </div>
</template>

<style scoped>
.git-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
}

.page-header,
.query-panel,
.table-panel {
  padding: 16px 20px;
  background: var(--td-bg-color-container);
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.page-header h2,
.page-header p,
.table-toolbar h3 {
  margin: 0;
}

.page-header p {
  margin-top: 6px;
  color: var(--td-text-color-secondary);
}

.table-toolbar,
.pagination-bar,
.records-query {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.table-toolbar {
  margin-bottom: 12px;
}

.table-toolbar > div,
.row-actions {
  display: flex;
  gap: 8px;
}

.row-actions {
  flex-wrap: wrap;
}

.pagination-bar {
  margin-top: 12px;
  color: var(--td-text-color-secondary);
}

.records-query {
  margin-bottom: 12px;
}
</style>
