<script lang="ts" setup>
import type { SystemApi } from '#/api';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { computed, onMounted, reactive, ref } from 'vue';

import { listPromptTemplatesApi, savePromptTemplateApi } from '#/api';
import { optionalNumberRule, requiredRule, validateForm } from '#/views/_shared/form-rules';
import SystemPage from '#/views/system/_shared/system-page.vue';
import { getCellRow } from '#/views/system/_shared/table-cell';
import {
  Button as TButton,
  Dialog as TDialog,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  Link as TLink,
  MessagePlugin,
  Select as TSelect,
  Space as TSpace,
  Tag as TTag,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

const fixedPromptTemplates = [
  {
    code: 'mcp_setup',
    description: 'Codex agentsprint MCP 接入配置提示词。',
    name: 'MCP 接入提示词',
    sort: 10,
  },
  {
    code: 'task_execution',
    description: 'Codex 任务推进提示词。',
    name: '任务推进提示词',
    sort: 20,
  },
] as const;
const fixedPromptCodes = new Set<string>(fixedPromptTemplates.map((item) => item.code));

const loading = ref(false);
const saving = ref(false);
const visible = ref(false);
const formRef = ref<FormInstanceFunctions>();
const templates = ref<SystemApi.PromptTemplate[]>([]);
const form = reactive<Partial<SystemApi.PromptTemplate>>({
  agentEnvironment: 'codex',
  code: '',
  content: '',
  description: '',
  id: undefined,
  name: '',
  sort: 10,
  status: 1,
});
const filters = reactive({
  keyword: '',
  status: undefined as number | undefined,
});
const query = reactive({
  keyword: '',
  status: undefined as number | undefined,
});

const rules: FormRules<typeof form> = {
  content: requiredRule('请输入提示词内容'),
  sort: optionalNumberRule('排序必须是数字'),
};
const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
];
const columns = [
  { colKey: 'code', title: '模板编码', width: 180 },
  { colKey: 'name', title: '模板名称', width: 180 },
  { colKey: 'description', title: '说明', cell: (...args: any[]) => getCellRow(args[0], args[1])?.description || '-' },
  { colKey: 'sort', title: '排序', width: 80 },
  { colKey: 'status', title: '状态', width: 90, cell: (...args: any[]) => (getCellRow(args[0], args[1])?.status === 1 ? '启用' : '停用') },
  { colKey: 'actions', title: '操作', width: 100, cell: 'actions' },
];

const fixedTemplates = computed<SystemApi.PromptTemplate[]>(() =>
  fixedPromptTemplates.map((definition) => {
    const saved = templates.value.find((item) => item.code === definition.code);
    return {
      agentEnvironment: 'codex',
      code: definition.code,
      content: saved?.content || '',
      description: saved?.description || definition.description,
      id: saved?.id || '',
      name: saved?.name || definition.name,
      sort: saved?.sort ?? definition.sort,
      status: saved?.status ?? 1,
    };
  }),
);
const filteredTemplates = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();
  return fixedTemplates.value.filter((item) => {
    const matchesKeyword =
      !keyword ||
      item.code.toLowerCase().includes(keyword) ||
      item.name.toLowerCase().includes(keyword) ||
      item.content.toLowerCase().includes(keyword) ||
      (item.description || '').toLowerCase().includes(keyword);
    const matchesStatus = query.status === undefined || item.status === query.status;
    return matchesKeyword && matchesStatus;
  });
});

function openTemplate(row: SystemApi.PromptTemplate) {
  const definition = fixedPromptTemplates.find((item) => item.code === row.code);
  if (!definition) {
    MessagePlugin.warning('只能维护固定提示词模板');
    return;
  }

  Object.assign(form, {
    agentEnvironment: 'codex',
    code: definition.code,
    content: row.content || '',
    description: row.description || definition.description,
    id: row.id || undefined,
    name: row.name || definition.name,
    sort: row.sort ?? definition.sort,
    status: row.status ?? 1,
  });
  visible.value = true;
}

async function loadTemplates() {
  loading.value = true;
  try {
    const data = await listPromptTemplatesApi('codex');
    templates.value = data.filter((item) => fixedPromptCodes.has(item.code));
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

async function saveTemplate() {
  if (saving.value) return;
  if (!(await validateForm(formRef.value))) return;
  const definition = fixedPromptTemplates.find((item) => item.code === form.code);
  if (!definition) {
    MessagePlugin.warning('只能维护固定提示词模板');
    return;
  }

  saving.value = true;
  try {
    await savePromptTemplateApi({
      ...form,
      agentEnvironment: 'codex',
      code: definition.code,
      name: definition.name,
      sort: Number(form.sort || definition.sort),
    });
    MessagePlugin.success('提示词模板已保存');
    visible.value = false;
    await loadTemplates();
  } finally {
    saving.value = false;
  }
}

onMounted(loadTemplates);
</script>

<template>
  <div class="prompt-page">
    <SystemPage
      title="提示词设置"
      description="Codex 当前固定维护 MCP 接入提示词和任务推进提示词。"
      :columns="columns"
      :data="filteredTemplates"
      :loading="loading"
      :addable="false"
    >
      <template #filters>
        <TInput v-model="filters.keyword" clearable placeholder="模板编码 / 名称 / 内容" class="filter-control" />
        <TSelect v-model="filters.status" clearable placeholder="状态" :options="statusOptions" class="filter-control small" />
        <TSpace>
          <TButton theme="primary" :disabled="loading" @click="search">查询</TButton>
          <TButton @click="reset">重置</TButton>
        </TSpace>
      </template>
      <template #toolbar>
        <TTag theme="primary" variant="light">固定 2 个模板</TTag>
      </template>
      <template #action></template>
      <template #actions="{ row }">
        <TLink theme="primary" @click="openTemplate(row)">编辑</TLink>
      </template>
    </SystemPage>

    <TDialog v-model:visible="visible" header="提示词模板维护" width="760px" :confirm-btn="{ content: '保存', loading: saving }" @confirm="saveTemplate">
      <TForm ref="formRef" :data="form" :rules="rules" label-width="104px">
        <TFormItem label="Agent 环境"><TInput value="Codex" disabled /></TFormItem>
        <TFormItem label="模板编码"><TInput v-model="form.code" disabled /></TFormItem>
        <TFormItem label="模板名称"><TInput v-model="form.name" disabled /></TFormItem>
        <TFormItem label="提示词内容" name="content">
          <TTextarea v-model="form.content" :autosize="{ minRows: 10, maxRows: 18 }" />
        </TFormItem>
        <TFormItem label="说明"><TTextarea v-model="form.description" :autosize="{ minRows: 2, maxRows: 4 }" /></TFormItem>
        <TFormItem label="排序" name="sort"><TInput v-model="form.sort" type="number" /></TFormItem>
        <TFormItem label="状态"><TSelect v-model="form.status" :options="statusOptions" /></TFormItem>
      </TForm>
    </TDialog>
  </div>
</template>

<style scoped>
.prompt-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.filter-control {
  width: 260px;
}

.filter-control.small {
  width: 160px;
}
</style>
