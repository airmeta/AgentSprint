<script lang="ts" setup>
import type { SprintGitApi } from '#/api/sprint/git';
import type { FormInstanceFunctions, FormRules, PrimaryTableCol } from 'tdesign-vue-next';

import { IconifyIcon } from '@vben/icons';
import { computed, h, onMounted, reactive, ref } from 'vue';

import {
  Button as TButton,
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
  Tooltip as TTooltip,
} from 'tdesign-vue-next';

import {
  createGitAccountApi,
  listGitAccountsApi,
  updateGitAccountApi,
} from '#/api/sprint/git';
import { formatDateTime } from '#/views/_shared/date-format';
import { requiredRule, validateForm } from '#/views/_shared/form-rules';

defineOptions({ name: 'SprintGitAccounts' });

const loading = ref(false);
const saving = ref(false);
const drawerVisible = ref(false);
const editingId = ref('');
const formRef = ref<FormInstanceFunctions>();
const accounts = ref<SprintGitApi.GitAccount[]>([]);
const query = reactive({
  keyword: '',
  status: '',
});
const pagination = reactive({
  current: 1,
  pageSize: 10,
});
const form = reactive<SprintGitApi.SaveGitAccountRequest>({
  accessToken: '',
  code: '',
  commitAuthorEmail: '',
  commitAuthorName: '',
  description: '',
  name: '',
  status: 'active',
  username: '',
});
const rules: FormRules<typeof form> = {
  code: requiredRule('请输入账户编码'),
  name: requiredRule('请输入账户名称'),
  username: requiredRule('请输入Git用户名'),
};
const statusOptions = [
  { label: '启用', value: 'active' },
  { label: '停用', value: 'disabled' },
];
const accessTokenTips = [
  '访问令牌是 HTTPS Git 认证中的 password/secret 字段，具体填什么取决于 Git 平台。',
  'GitHub 填 Personal Access Token，不是账号密码。',
  'GitLab 建议填 Personal Access Token、Project Access Token 或 Deploy Token 的 token 值，不建议填账号密码。',
  '自建 GitLab、Gitea、Gogs、Coding、Azure DevOps 等平台如果仍允许 HTTPS basic password，理论上可以填账号密码；现代平台通常推荐或强制用 token。',
  'Deploy Token 需要用户名填平台生成的 deploy token username，访问令牌填 deploy token password/token。',
  '用于推送时，令牌必须具备写仓库权限，例如 GitLab 的 read_repository 和 write_repository。',
];

function renderAccessTokenTitle() {
  return h('span', { class: 'field-label-with-tip' }, [
    '访问令牌',
    h(
      TTooltip,
      { placement: 'top', theme: 'light' },
      {
        content: () =>
          h(
            'div',
            { class: 'access-token-tip' },
            accessTokenTips.map((item) => h('p', { key: item }, item)),
          ),
        default: () =>
          h(IconifyIcon, {
            class: 'access-token-warning-icon',
            icon: 'lucide:circle-alert',
          }),
      },
    ),
  ]);
}

const columns: PrimaryTableCol[] = [
  { colKey: 'serial-number', title: '序号', width: 70 },
  { colKey: 'code', title: '账户编码', width: 150, ellipsis: true },
  { colKey: 'name', title: '账户名称', minWidth: 160, ellipsis: true },
  { colKey: 'username', title: 'Git用户名', minWidth: 160, ellipsis: true },
  { colKey: 'accessToken', title: renderAccessTokenTitle, width: 150, cell: 'accessToken' },
  { colKey: 'commitAuthorName', title: '提交作者', minWidth: 160, ellipsis: true },
  { colKey: 'commitAuthorEmail', title: '提交邮箱', minWidth: 200, ellipsis: true },
  { colKey: 'status', title: '状态', width: 100, cell: 'status' },
  { colKey: 'createTime', title: '创建时间', width: 180, cell: 'createTime' },
  { colKey: 'operation', title: '操作', width: 110, fixed: 'right', cell: 'operation' },
];
const pageData = computed(() => {
  const start = (pagination.current - 1) * pagination.pageSize;
  return accounts.value.slice(start, start + pagination.pageSize);
});

function resetForm(row?: SprintGitApi.GitAccount) {
  editingId.value = row?.id || '';
  Object.assign(form, {
    accessToken: row?.accessToken || '',
    code: row?.code || '',
    commitAuthorEmail: row?.commitAuthorEmail || '',
    commitAuthorName: row?.commitAuthorName || '',
    description: row?.description || '',
    name: row?.name || '',
    status: row?.status || 'active',
    username: row?.username || '',
  });
}

function openCreate() {
  resetForm();
  drawerVisible.value = true;
}

function openEdit(row: SprintGitApi.GitAccount) {
  resetForm(row);
  drawerVisible.value = true;
}

async function loadAccounts() {
  loading.value = true;
  try {
    accounts.value = await listGitAccountsApi({
      keyword: query.keyword || undefined,
      status: query.status || undefined,
    });
    pagination.current = 1;
  } finally {
    loading.value = false;
  }
}

function resetQuery() {
  query.keyword = '';
  query.status = '';
  void loadAccounts();
}

async function saveAccount() {
  if (saving.value) return;
  if (!(await validateForm(formRef.value))) return;
  saving.value = true;
  try {
    const payload = {
      ...form,
      accessToken: form.accessToken?.trim() || undefined,
      commitAuthorEmail: form.commitAuthorEmail?.trim() || undefined,
      commitAuthorName: form.commitAuthorName?.trim() || undefined,
      description: form.description?.trim() || undefined,
    };
    if (editingId.value) {
      await updateGitAccountApi(editingId.value, payload);
    } else {
      await createGitAccountApi(payload);
    }
    MessagePlugin.success('Git账户已保存');
    drawerVisible.value = false;
    await loadAccounts();
  } finally {
    saving.value = false;
  }
}

onMounted(loadAccounts);
</script>

<template>
  <div class="git-page">
    <section class="page-header">
      <div>
        <h2>Git账户管理</h2>
        <p>维护仓库访问使用的Git用户名和访问令牌。</p>
      </div>
    </section>

    <section class="query-panel">
      <TForm :data="query" layout="inline">
        <TFormItem label="关键字">
          <TInput v-model="query.keyword" clearable placeholder="编码 / 名称 / 用户名" />
        </TFormItem>
        <TFormItem label="状态">
          <TSelect v-model="query.status" clearable :options="statusOptions" />
        </TFormItem>
        <TFormItem>
          <TButton theme="primary" @click="loadAccounts">查询</TButton>
          <TButton variant="outline" @click="resetQuery">重置</TButton>
        </TFormItem>
      </TForm>
    </section>

    <section class="table-panel">
      <div class="table-toolbar">
        <h3>Git账户列表</h3>
        <div>
          <TButton theme="primary" @click="openCreate">
            <template #icon><IconifyIcon icon="lucide:plus" /></template>
            新增
          </TButton>
          <TButton variant="outline" :loading="loading" @click="loadAccounts">
            <template #icon><IconifyIcon icon="lucide:refresh-cw" /></template>
          </TButton>
        </div>
      </div>
      <TTable
        row-key="id"
        :columns="columns"
        :data="pageData"
        :loading="loading"
        hover
      >
        <template #accessToken="{ row }">
          <TTag :theme="row.accessToken ? 'warning' : 'default'" variant="light">
            {{ row.accessToken ? '已配置' : '未配置' }}
          </TTag>
        </template>
        <template #status="{ row }">
          <TTag :theme="row.status === 'active' ? 'success' : 'default'" variant="light">
            {{ row.status === 'active' ? '启用' : '停用' }}
          </TTag>
        </template>
        <template #createTime="{ row }">
          {{ formatDateTime(row.createTime) }}
        </template>
        <template #operation="{ row }">
          <TLink theme="primary" @click="openEdit(row)">
            <IconifyIcon icon="lucide:pencil" />
            编辑
          </TLink>
        </template>
      </TTable>
      <div class="pagination-bar">
        <span>共计 {{ accounts.length }} 条数据</span>
        <TPagination
          v-model="pagination.current"
          v-model:page-size="pagination.pageSize"
          :total="accounts.length"
          show-jumper
        />
      </div>
    </section>

    <TDrawer
      v-model:visible="drawerVisible"
      :confirm-btn="{ content: '保存', loading: saving }"
      :header="editingId ? '编辑Git账户' : '新增Git账户'"
      size="520px"
      @confirm="saveAccount"
    >
      <TForm ref="formRef" :data="form" :rules="rules" label-width="100px">
        <TFormItem label="账户编码" name="code">
          <TInput v-model="form.code" :disabled="!!editingId" placeholder="GITHUB_MAIN" />
        </TFormItem>
        <TFormItem label="账户名称" name="name">
          <TInput v-model="form.name" placeholder="GitHub主账号" />
        </TFormItem>
        <TFormItem label="用户名" name="username">
          <TInput v-model="form.username" placeholder="git username" />
        </TFormItem>
        <TFormItem>
          <template #label>
            <span class="field-label-with-tip">
              访问令牌
              <TTooltip placement="top" theme="light">
                <template #content>
                  <div class="access-token-tip">
                    <p v-for="item in accessTokenTips" :key="item">{{ item }}</p>
                  </div>
                </template>
                <IconifyIcon class="access-token-warning-icon" icon="lucide:circle-alert" />
              </TTooltip>
            </span>
          </template>
          <TInput v-model="form.accessToken" type="password" placeholder="密码或Personal Access Token" />
        </TFormItem>
        <TFormItem label="提交作者">
          <TInput v-model="form.commitAuthorName" placeholder="AgentSprint Bot" />
        </TFormItem>
        <TFormItem label="提交邮箱">
          <TInput v-model="form.commitAuthorEmail" placeholder="agentsprint-bot@example.com" />
        </TFormItem>
        <TFormItem label="状态">
          <TSelect v-model="form.status" :options="statusOptions" />
        </TFormItem>
        <TFormItem label="说明">
          <TTextarea v-model="form.description" :autosize="{ minRows: 3, maxRows: 5 }" />
        </TFormItem>
      </TForm>
    </TDrawer>
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
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.table-toolbar {
  margin-bottom: 12px;
}

.table-toolbar > div {
  display: flex;
  gap: 8px;
}

.pagination-bar {
  margin-top: 12px;
  color: var(--td-text-color-secondary);
}

.field-label-with-tip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.access-token-warning-icon {
  color: var(--td-warning-color);
  cursor: help;
  font-size: 16px;
}

.access-token-tip {
  max-width: 420px;
  line-height: 1.6;
}

.access-token-tip p {
  margin: 0;
}

.access-token-tip p + p {
  margin-top: 6px;
}
</style>
