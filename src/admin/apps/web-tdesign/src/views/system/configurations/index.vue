<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import {
  deleteSystemConfigurationApi,
  listSystemConfigurationsApi,
  saveSystemConfigurationApi,
  type SystemApi,
} from '#/api';
import SystemPage from '#/views/system/_shared/system-page.vue';
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
const visible = ref(false);
const formRef = ref<FormInstanceFunctions>();
const rows = ref<SystemApi.Configuration[]>([]);
const form = reactive<Partial<SystemApi.Configuration>>({
  description: '',
  id: undefined,
  key: 'Mcp:Endpoint',
  status: 1,
  value: '',
});
const rules: FormRules<typeof form> = {
  key: requiredRule('请输入配置键'),
  value: requiredRule('请输入配置值'),
};
const filters = reactive({
  keyword: '',
  status: undefined as number | undefined,
});
const query = reactive({ ...filters });

const columns = [
  { colKey: 'key', title: '配置键', width: 220 },
  { colKey: 'value', title: '配置值', ellipsis: true },
  { colKey: 'description', title: '说明', ellipsis: true },
  { colKey: 'status', title: '状态', width: 100 },
  { colKey: 'actions', title: '操作', width: 140 },
];

const filteredRows = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();
  return rows.value.filter((item) => {
    const matchKeyword =
      !keyword ||
      item.key.toLowerCase().includes(keyword) ||
      item.value.toLowerCase().includes(keyword) ||
      (item.description || '').toLowerCase().includes(keyword);
    const matchStatus = query.status === undefined || item.status === query.status;
    return matchKeyword && matchStatus;
  });
});

function resetForm() {
  Object.assign(form, {
    description: 'Streamable HTTP MCP 服务地址，用于任务提示词生成。',
    id: undefined,
    key: 'Mcp:Endpoint',
    status: 1,
    value: 'http://192.168.80.101:5010/mcp',
  });
}

function applyFilters() {
  Object.assign(query, filters);
}

function resetFilters() {
  Object.assign(filters, { keyword: '', status: undefined });
  applyFilters();
}

async function loadRows() {
  loading.value = true;
  try {
    rows.value = await listSystemConfigurationsApi();
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  resetForm();
  visible.value = true;
}

function openEdit(row: SystemApi.Configuration) {
  Object.assign(form, row);
  visible.value = true;
}

async function save() {
  if (!(await validateForm(formRef.value))) return;
  if (!form.key?.trim() || !form.value?.trim()) {
    MessagePlugin.warning('配置键和配置值不能为空');
    return;
  }

  await saveSystemConfigurationApi(form);
  MessagePlugin.success('配置已保存');
  visible.value = false;
  await loadRows();
}

function remove(row: SystemApi.Configuration) {
  DialogPlugin.confirm({
    body: `确认删除配置 ${row.key}？删除后业务会回退默认值。`,
    confirmBtn: '删除',
    header: '删除配置',
    onConfirm: async () => {
      await deleteSystemConfigurationApi(row.id);
      MessagePlugin.success('配置已删除');
      await loadRows();
    },
  });
}

onMounted(async () => {
  resetForm();
  await loadRows();
});
</script>

<template>
  <SystemPage
    title="系统配置"
    description="维护运行时动态配置，当前用于控制任务提示词中的 Streamable HTTP MCP 服务地址。"
    :columns="columns"
    :data="filteredRows"
    :loading="loading"
    @add="openCreate"
  >
    <template #filters>
      <TInput v-model="filters.keyword" clearable placeholder="配置键 / 配置值 / 说明" class="filter-control" />
      <TSelect
        v-model="filters.status"
        clearable
        placeholder="状态"
        class="filter-control"
        :options="[
          { label: '启用', value: 1 },
          { label: '停用', value: 0 },
        ]"
      />
      <TSpace>
        <TButton theme="primary" @click="applyFilters">查询</TButton>
        <TButton @click="resetFilters">重置</TButton>
      </TSpace>
    </template>

    <template #action>新增配置</template>

    <template #actions="{ row }">
      <TSpace>
        <TLink theme="primary" @click="openEdit(row)">编辑</TLink>
        <TLink theme="danger" @click="remove(row)">删除</TLink>
      </TSpace>
    </template>
  </SystemPage>

  <TDialog v-model:visible="visible" header="系统配置" width="620" :confirm-on-enter="true" @confirm="save">
    <TForm ref="formRef" :data="form" :rules="rules" label-width="96px">
      <TFormItem label="配置键" name="key">
        <TInput v-model="form.key" placeholder="Mcp:Endpoint" />
      </TFormItem>
      <TFormItem label="配置值" name="value">
        <TInput v-model="form.value" placeholder="http://192.168.80.101:5010/mcp" />
      </TFormItem>
      <TFormItem label="说明">
        <TTextarea v-model="form.description" placeholder="配置用途说明" />
      </TFormItem>
      <TFormItem label="状态">
        <TSelect
          v-model="form.status"
          :options="[
            { label: '启用', value: 1 },
            { label: '停用', value: 0 },
          ]"
        />
      </TFormItem>
    </TForm>
  </TDialog>
</template>

<style scoped>
.filter-control {
  width: 220px;
}
</style>
