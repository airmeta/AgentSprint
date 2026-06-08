<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { deleteAssignmentApi, listAssignmentsApi, saveAssignmentApi, type SystemApi } from '#/api';
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
const saving = ref(false);
const visible = ref(false);
const formRef = ref<FormInstanceFunctions>();
const assignments = ref<SystemApi.CodeName[]>([]);
const form = reactive<Partial<SystemApi.CodeName>>({
  code: '',
  description: '',
  id: undefined,
  name: '',
  status: 1,
});
const rules: FormRules<typeof form> = {
  code: requiredRule('请输入岗位编码'),
  name: requiredRule('请输入岗位名称'),
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
  { colKey: 'code', title: '岗位编码' },
  { colKey: 'name', title: '岗位名称' },
  { colKey: 'description', title: '说明', cell: (...args: any[]) => getCellRow(args[0], args[1])?.description || '-' },
  { colKey: 'status', title: '状态', cell: (...args: any[]) => (getCellRow(args[0], args[1])?.status === 1 ? '启用' : '停用') },
  { colKey: 'actions', title: '操作', cell: 'actions' },
];

const filteredAssignments = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();
  return assignments.value.filter((assignment) => {
    const matchesKeyword =
      !keyword ||
      assignment.code.toLowerCase().includes(keyword) ||
      assignment.name.toLowerCase().includes(keyword) ||
      (assignment.description || '').toLowerCase().includes(keyword);
    const matchesStatus = query.status === undefined || assignment.status === query.status;
    return matchesKeyword && matchesStatus;
  });
});

function open(row?: SystemApi.CodeName) {
  Object.assign(form, {
    code: row?.code || '',
    description: row?.description || '',
    id: row?.id,
    name: row?.name || '',
    status: row?.status ?? 1,
  });
  visible.value = true;
}

async function load() {
  loading.value = true;
  try {
    assignments.value = await listAssignmentsApi();
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
  if (saving.value) return;
  if (!(await validateForm(formRef.value))) return;
  saving.value = true;
  try {
    await saveAssignmentApi(form);
    MessagePlugin.success('岗位已保存');
    visible.value = false;
    await load();
  } finally {
    saving.value = false;
  }
}

function remove(row: SystemApi.CodeName) {
  DialogPlugin.confirm({
    body: `确认删除岗位 ${row.code}？`,
    confirmBtn: '删除',
    header: '删除岗位',
    onConfirm: async () => {
      await deleteAssignmentApi(row.id);
      MessagePlugin.success('岗位已删除');
      await load();
    },
  });
}

onMounted(load);
</script>

<template>
  <div>
    <SystemPage title="岗位管理" :columns="columns" :data="filteredAssignments" :loading="loading" @add="open()">
      <template #filters>
        <TInput v-model="filters.keyword" clearable placeholder="岗位编码 / 名称 / 说明" class="filter-control" />
        <TSelect
          v-model="filters.status"
          clearable
          placeholder="状态"
          :options="[{ label: '启用', value: 1 }, { label: '停用', value: 0 }]"
          class="filter-control"
        />
        <TSpace>
          <TButton theme="primary" :disabled="loading" @click="search">查询</TButton>
          <TButton @click="reset">重置</TButton>
        </TSpace>
      </template>
      <template #action>新增岗位</template>
      <template #actions="{ row }">
        <TSpace>
          <TLink theme="primary" @click="open(row)">编辑</TLink>
          <TLink theme="danger" @click="remove(row)">删除</TLink>
        </TSpace>
      </template>
    </SystemPage>
    <TDialog v-model:visible="visible" header="岗位维护" width="560px" :confirm-btn="{ content: '保存', loading: saving }" @confirm="save">
      <TForm ref="formRef" :data="form" :rules="rules" label-width="96px">
        <TFormItem label="岗位编码" name="code"><TInput v-model="form.code" /></TFormItem>
        <TFormItem label="岗位名称" name="name"><TInput v-model="form.name" /></TFormItem>
        <TFormItem label="说明"><TInput v-model="form.description" /></TFormItem>
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
