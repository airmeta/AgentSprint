<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import {
  deleteSystemUserApi,
  listSystemRolesApi,
  listSystemUsersApi,
  saveSystemUserApi,
  type SystemApi,
} from '#/api';
import SystemPage from '#/views/system/_shared/system-page.vue';
import { getCellRow } from '#/views/system/_shared/table-cell';
import {
  Button as TButton,
  Dialog as TDialog,
  DialogPlugin,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  Link as TLink,
  MessagePlugin,
  Select as TSelect,
  Space as TSpace,
} from 'tdesign-vue-next';
import { requiredRule, validateForm } from '#/views/_shared/form-rules';

const loading = ref(false);
const visible = ref(false);
const formRef = ref<FormInstanceFunctions>();
const users = ref<SystemApi.User[]>([]);
const roles = ref<SystemApi.Role[]>([]);
const roleOptions = ref<{ label: string; value: string }[]>([]);
const form = reactive<Partial<SystemApi.User> & { password?: string }>({
  displayName: '',
  email: '',
  id: undefined,
  password: '',
  phoneNumber: '',
  roleIds: [],
  status: 1,
  username: '',
});
const rules = computed<FormRules<typeof form>>(() => ({
  displayName: requiredRule('请输入显示名称'),
  password: form.id ? [] : requiredRule('请输入密码'),
  username: requiredRule('请输入用户名'),
}));
const filters = reactive({
  keyword: '',
  roleId: '',
  status: undefined as number | undefined,
});
const query = reactive({ ...filters });

const roleNameMap = computed(() => new Map(roles.value.map((role) => [role.id, role.name])));
const columns = [
  { colKey: 'username', title: '用户名', width: 160 },
  { colKey: 'displayName', title: '显示名称', width: 160 },
  { colKey: 'email', title: '邮箱', width: 220 },
  { colKey: 'phoneNumber', title: '手机号', width: 150 },
  {
    colKey: 'roleIds',
    title: '角色',
    cell: (...args: any[]) =>
      asArray(getCellRow(args[0], args[1])?.roleIds).map((id: string) => roleNameMap.value.get(id) || id).join(', ') || '-',
  },
  {
    colKey: 'status',
    title: '状态',
    width: 100,
    cell: (...args: any[]) => (getCellRow(args[0], args[1])?.status === 1 ? '启用' : '停用'),
  },
  { colKey: 'actions', title: '操作', width: 140, cell: 'actions' },
];

const filteredUsers = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();
  return users.value.filter((user) => {
    const matchesKeyword =
      !keyword ||
      user.username.toLowerCase().includes(keyword) ||
      user.displayName.toLowerCase().includes(keyword) ||
      (user.email || '').toLowerCase().includes(keyword) ||
      (user.phoneNumber || '').toLowerCase().includes(keyword);
    const matchesRole = !query.roleId || asArray(user.roleIds).includes(query.roleId);
    const matchesStatus = query.status === undefined || user.status === query.status;
    return matchesKeyword && matchesRole && matchesStatus;
  });
});

function asArray(value: string[] | null | undefined) {
  return Array.isArray(value) ? value : [];
}

function normalizeUser(user: SystemApi.User): SystemApi.User {
  return {
    ...user,
    roleIds: asArray(user.roleIds),
  };
}

function search() {
  Object.assign(query, filters);
}

function reset() {
  Object.assign(filters, { keyword: '', roleId: '', status: undefined });
  search();
}

function open(row?: SystemApi.User) {
  Object.assign(form, {
    displayName: row?.displayName || '',
    email: row?.email || '',
    id: row?.id,
    password: '',
    phoneNumber: row?.phoneNumber || '',
    roleIds: [...asArray(row?.roleIds)],
    status: row?.status ?? 1,
    username: row?.username || '',
  });
  visible.value = true;
}

async function load() {
  loading.value = true;
  try {
    const [userRows, roleRows] = await Promise.all([listSystemUsersApi(), listSystemRolesApi()]);
    users.value = userRows.map(normalizeUser);
    roles.value = roleRows;
    roleOptions.value = roleRows.map((role) => ({
      label: `${role.name} (${role.code})`,
      value: role.id,
    }));
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (!(await validateForm(formRef.value))) return;
  await saveSystemUserApi(form);
  MessagePlugin.success('用户已保存');
  visible.value = false;
  await load();
}

function remove(row: SystemApi.User) {
  DialogPlugin.confirm({
    body: `确认删除用户 ${row.username}？`,
    confirmBtn: '删除',
    header: '删除用户',
    onConfirm: async () => {
      await deleteSystemUserApi(row.id);
      MessagePlugin.success('用户已删除');
      await load();
    },
  });
}

onMounted(load);
</script>

<template>
  <SystemPage
    title="用户管理"
    description="维护维护人员、项目成员和系统账号，角色授权后可参与对应业务操作。"
    :columns="columns"
    :data="filteredUsers"
    :loading="loading"
    @add="open()"
  >
    <template #filters>
      <TInput v-model="filters.keyword" clearable placeholder="用户名 / 显示名称 / 邮箱 / 手机号" class="filter-control" />
      <TSelect v-model="filters.roleId" clearable placeholder="角色" :options="roleOptions" class="filter-control" />
      <TSelect
        v-model="filters.status"
        clearable
        placeholder="状态"
        :options="[
          { label: '启用', value: 1 },
          { label: '停用', value: 0 },
        ]"
        class="filter-control"
      />
      <TSpace>
        <TButton theme="primary" @click="search">查询</TButton>
        <TButton @click="reset">重置</TButton>
      </TSpace>
    </template>
    <template #action>新增用户</template>
    <template #actions="{ row }">
      <TSpace>
        <TLink v-if="row" theme="primary" @click="open(row)">编辑</TLink>
        <TLink v-if="row" theme="danger" @click="remove(row)">删除</TLink>
      </TSpace>
    </template>
  </SystemPage>

  <TDialog v-model:visible="visible" header="用户维护" width="640px" confirm-btn="保存" @confirm="save">
    <TForm ref="formRef" :data="form" :rules="rules" label-width="96px">
      <TFormItem label="用户名" name="username"><TInput v-model="form.username" /></TFormItem>
      <TFormItem label="显示名称" name="displayName"><TInput v-model="form.displayName" /></TFormItem>
      <TFormItem label="密码" name="password"><TInput v-model="form.password" type="password" placeholder="新增必填，编辑留空则不修改" /></TFormItem>
      <TFormItem label="邮箱"><TInput v-model="form.email" /></TFormItem>
      <TFormItem label="手机号"><TInput v-model="form.phoneNumber" /></TFormItem>
      <TFormItem label="状态">
        <TSelect
          v-model="form.status"
          :options="[
            { label: '启用', value: 1 },
            { label: '停用', value: 0 },
          ]"
        />
      </TFormItem>
      <TFormItem label="角色">
        <TSelect v-model="form.roleIds" multiple filterable :options="roleOptions" />
      </TFormItem>
    </TForm>
  </TDialog>
</template>

<style scoped>
.filter-control {
  width: 240px;
}
</style>
