<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { deleteDepartmentApi, listDepartmentsApi, saveDepartmentApi, type SystemApi } from '#/api';
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
import { optionalNumberRule, requiredRule, validateForm } from '#/views/_shared/form-rules';

const loading = ref(false);
const visible = ref(false);
const formRef = ref<FormInstanceFunctions>();
const departments = ref<SystemApi.Department[]>([]);
const form = reactive<Partial<SystemApi.Department>>({
  code: '',
  id: undefined,
  name: '',
  parentId: '',
  sort: 0,
  status: 1,
});
const rules: FormRules<typeof form> = {
  code: requiredRule('请输入部门编码'),
  name: requiredRule('请输入部门名称'),
  sort: optionalNumberRule('排序必须是数字'),
};
const filters = reactive({
  keyword: '',
  status: undefined as number | undefined,
});
const query = reactive({
  keyword: '',
  status: undefined as number | undefined,
});
const columns = [
  { colKey: 'code', title: '部门编码' },
  { colKey: 'name', title: '部门名称' },
  { colKey: 'parentId', title: '父级部门 ID', cell: (...args: any[]) => getCellRow(args[0], args[1])?.parentId || '-' },
  { colKey: 'sort', title: '排序' },
  { colKey: 'status', title: '状态', cell: (...args: any[]) => (getCellRow(args[0], args[1])?.status === 1 ? '启用' : '停用') },
  { colKey: 'actions', title: '操作', cell: 'actions' },
];

const filteredDepartments = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();
  return departments.value.filter((department) => {
    const matchesKeyword =
      !keyword ||
      department.code.toLowerCase().includes(keyword) ||
      department.name.toLowerCase().includes(keyword) ||
      (department.parentId || '').toLowerCase().includes(keyword);
    const matchesStatus = query.status === undefined || department.status === query.status;
    return matchesKeyword && matchesStatus;
  });
});

function open(row?: SystemApi.Department) {
  Object.assign(form, {
    code: row?.code || '',
    id: row?.id,
    name: row?.name || '',
    parentId: row?.parentId || '',
    sort: row?.sort ?? 0,
    status: row?.status ?? 1,
  });
  visible.value = true;
}

async function load() {
  loading.value = true;
  try {
    departments.value = await listDepartmentsApi();
  } finally {
    loading.value = false;
  }
}

function search() {
  Object.assign(query, filters);
}

function reset() {
  Object.assign(filters, { keyword: '', status: undefined });
  search();
}

async function save() {
  if (!(await validateForm(formRef.value))) return;
  await saveDepartmentApi(form);
  MessagePlugin.success('部门已保存');
  visible.value = false;
  await load();
}

function remove(row: SystemApi.Department) {
  DialogPlugin.confirm({
    body: `确认删除部门 ${row.code}？`,
    confirmBtn: '删除',
    header: '删除部门',
    onConfirm: async () => {
      await deleteDepartmentApi(row.id);
      MessagePlugin.success('部门已删除');
      await load();
    },
  });
}

onMounted(load);
</script>

<template>
  <div>
    <SystemPage title="部门管理" :columns="columns" :data="filteredDepartments" :loading="loading" @add="open()">
      <template #filters>
        <TInput v-model="filters.keyword" clearable placeholder="部门编码 / 名称 / 父级 ID" class="filter-control" />
        <TSelect
          v-model="filters.status"
          clearable
          placeholder="状态"
          :options="[{ label: '启用', value: 1 }, { label: '停用', value: 0 }]"
          class="filter-control"
        />
        <TSpace>
          <TButton theme="primary" @click="search">查询</TButton>
          <TButton @click="reset">重置</TButton>
        </TSpace>
      </template>
      <template #action>新增部门</template>
      <template #actions="{ row }">
        <TSpace>
          <TLink theme="primary" @click="open(row)">编辑</TLink>
          <TLink theme="danger" @click="remove(row)">删除</TLink>
        </TSpace>
      </template>
    </SystemPage>
    <TDialog v-model:visible="visible" header="部门维护" width="560px" confirm-btn="保存" @confirm="save">
      <TForm ref="formRef" :data="form" :rules="rules" label-width="110px">
        <TFormItem label="父级部门 ID"><TInput v-model="form.parentId" /></TFormItem>
        <TFormItem label="部门编码" name="code"><TInput v-model="form.code" /></TFormItem>
        <TFormItem label="部门名称" name="name"><TInput v-model="form.name" /></TFormItem>
        <TFormItem label="排序" name="sort"><TInput v-model="form.sort" type="number" /></TFormItem>
        <TFormItem label="状态"><TSelect v-model="form.status" :options="[{ label: '启用', value: 1 }, { label: '停用', value: 0 }]" /></TFormItem>
      </TForm>
    </TDialog>
  </div>
</template>

<style scoped>
.filter-control {
  width: 240px;
}
</style>
