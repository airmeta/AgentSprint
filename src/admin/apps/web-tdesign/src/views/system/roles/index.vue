<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import {
  deleteSystemRoleApi,
  listSystemMenusApi,
  listSystemPermissionsApi,
  listSystemRolesApi,
  saveSystemRoleApi,
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
  Textarea as TTextarea,
} from 'tdesign-vue-next';
import { requiredRule, validateForm } from '#/views/_shared/form-rules';

const loading = ref(false);
const saving = ref(false);
const visible = ref(false);
const formRef = ref<FormInstanceFunctions>();
const roles = ref<SystemApi.Role[]>([]);
const menuOptions = ref<{ label: string; value: string }[]>([]);
const permissionOptions = ref<{ label: string; value: string }[]>([]);
const form = reactive<Partial<SystemApi.Role>>({
  code: '',
  description: '',
  id: undefined,
  menuIds: [],
  name: '',
  permissionIds: [],
  status: 1,
});
const rules: FormRules<typeof form> = {
  code: requiredRule('请输入角色编码'),
  name: requiredRule('请输入角色名称'),
};
const filters = reactive({
  grantState: undefined as number | undefined,
  keyword: '',
  status: undefined as number | undefined,
});
const query = reactive({ ...filters });
const columns = [
  { colKey: 'code', title: '角色编码', width: 160 },
  { colKey: 'name', title: '角色名称', width: 160 },
  { colKey: 'description', title: '说明' },
  { colKey: 'menuIds', title: '菜单数', width: 100, cell: (...args: any[]) => asArray(getCellRow(args[0], args[1])?.menuIds).length },
  { colKey: 'permissionIds', title: '按钮权限数', width: 120, cell: (...args: any[]) => asArray(getCellRow(args[0], args[1])?.permissionIds).length },
  {
    colKey: 'status',
    title: '状态',
    width: 100,
    cell: (...args: any[]) => (getCellRow(args[0], args[1])?.status === 1 ? '启用' : '停用'),
  },
  { colKey: 'actions', title: '操作', width: 160, cell: 'actions' },
];

const filteredRoles = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();
  return roles.value.filter((role) => {
    const hasGrant = asArray(role.menuIds).length > 0 || asArray(role.permissionIds).length > 0;
    const matchesKeyword =
      !keyword ||
      role.code.toLowerCase().includes(keyword) ||
      role.name.toLowerCase().includes(keyword) ||
      (role.description || '').toLowerCase().includes(keyword);
    const matchesStatus = query.status === undefined || role.status === query.status;
    const matchesGrant = query.grantState === undefined || (query.grantState === 1 ? hasGrant : !hasGrant);
    return matchesKeyword && matchesStatus && matchesGrant;
  });
});

function asArray<T>(value: T[] | null | undefined) {
  return Array.isArray(value) ? value : [];
}

function normalizeRole(role: SystemApi.Role): SystemApi.Role {
  return {
    ...role,
    menuIds: asArray(role.menuIds),
    permissionIds: asArray(role.permissionIds),
  };
}

function search() {
  Object.assign(query, filters);
}

function reset() {
  Object.assign(filters, { grantState: undefined, keyword: '', status: undefined });
  search();
}

function open(row?: SystemApi.Role) {
  Object.assign(form, {
    code: row?.code || '',
    description: row?.description || '',
    id: row?.id,
    menuIds: [...asArray(row?.menuIds)],
    name: row?.name || '',
    permissionIds: [...asArray(row?.permissionIds)],
    status: row?.status ?? 1,
  });
  visible.value = true;
}

async function load() {
  loading.value = true;
  try {
    const [roleRows, menus, permissions] = await Promise.all([
      listSystemRolesApi(),
      listSystemMenusApi(),
      listSystemPermissionsApi(),
    ]);
    roles.value = roleRows.map(normalizeRole);
    menuOptions.value = menus.map((menu) => ({
      label: `${menu.name} (${menu.path})`,
      value: menu.id,
    }));
    const menuNameMap = new Map(menus.map((menu) => [menu.id, `${menu.name} (${menu.path})`]));
    permissionOptions.value = permissions.map((permission) => ({
      label: `${permission.name} (${permission.code}) - ${
        permission.menuId ? menuNameMap.get(permission.menuId) || permission.menuId : '未绑定菜单'
      }`,
      value: permission.id,
    }));
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (saving.value) return;
  if (!(await validateForm(formRef.value))) return;
  saving.value = true;
  try {
    await saveSystemRoleApi(form);
    MessagePlugin.success('角色已保存');
    visible.value = false;
    await load();
  } finally {
    saving.value = false;
  }
}

function remove(row: SystemApi.Role) {
  DialogPlugin.confirm({
    body: `确认删除角色 ${row.code}？`,
    confirmBtn: '删除',
    header: '删除角色',
    onConfirm: async () => {
      await deleteSystemRoleApi(row.id);
      MessagePlugin.success('角色已删除');
      await load();
    },
  });
}

onMounted(load);
</script>

<template>
  <SystemPage
    title="角色管理"
    description="维护角色基础信息，并为角色分配菜单和菜单下的按钮权限。"
    :columns="columns"
    :data="filteredRoles"
    :loading="loading"
    @add="open()"
  >
    <template #filters>
      <TInput v-model="filters.keyword" clearable placeholder="角色编码 / 名称 / 说明" class="filter-control" />
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
      <TSelect
        v-model="filters.grantState"
        clearable
        placeholder="授权状态"
        :options="[
          { label: '已授权', value: 1 },
          { label: '未授权', value: 0 },
        ]"
        class="filter-control"
      />
      <TSpace>
        <TButton theme="primary" :disabled="loading" @click="search">查询</TButton>
        <TButton @click="reset">重置</TButton>
      </TSpace>
    </template>
    <template #action>新增角色</template>
    <template #actions="{ row }">
      <TSpace>
        <TLink v-if="row" theme="primary" @click="open(row)">编辑授权</TLink>
        <TLink v-if="row" theme="danger" @click="remove(row)">删除</TLink>
      </TSpace>
    </template>
  </SystemPage>

  <TDialog v-model:visible="visible" header="角色维护" width="720px" :confirm-btn="{ content: '保存', loading: saving }" @confirm="save">
    <TForm ref="formRef" :data="form" :rules="rules" label-width="96px">
      <TFormItem label="角色编码" name="code"><TInput v-model="form.code" /></TFormItem>
      <TFormItem label="角色名称" name="name"><TInput v-model="form.name" /></TFormItem>
      <TFormItem label="说明"><TTextarea v-model="form.description" /></TFormItem>
      <TFormItem label="状态">
        <TSelect
          v-model="form.status"
          :options="[
            { label: '启用', value: 1 },
            { label: '停用', value: 0 },
          ]"
        />
      </TFormItem>
      <TFormItem label="菜单授权"><TSelect v-model="form.menuIds" multiple filterable :options="menuOptions" /></TFormItem>
      <TFormItem label="按钮权限"><TSelect v-model="form.permissionIds" multiple filterable :options="permissionOptions" /></TFormItem>
    </TForm>
  </TDialog>
</template>

<style scoped>
.filter-control {
  width: 240px;
}
</style>
