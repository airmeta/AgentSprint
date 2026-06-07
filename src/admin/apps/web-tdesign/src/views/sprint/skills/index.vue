<script lang="ts" setup>
import type { SprintMvpApi } from '#/api/sprint/mvp';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import { computed, onMounted, reactive, ref } from 'vue';

import {
  Button as TButton,
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  Link as TLink,
  MessagePlugin,
  Select as TSelect,
  Space as TSpace,
  Table as TTable,
  Tag as TTag,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

import {
  createSkillApi,
  listSkillsApi,
  updateSkillApi,
} from '#/api/sprint/mvp';
import { requiredRule, validateForm } from '#/views/_shared/form-rules';
import '../_shared/table-layout.css';

const loading = ref(false);
const visible = ref(false);
const formRef = ref<FormInstanceFunctions>();
const rows = ref<SprintMvpApi.Skill[]>([]);
const selected = ref<SprintMvpApi.Skill>();

const filters = reactive({
  keyword: '',
  status: '',
});
const form = reactive({
  code: '',
  content: '',
  description: '',
  name: '',
  status: 'active',
});

const rules: FormRules<typeof form> = {
  code: requiredRule('请输入 Skill 编码'),
  content: requiredRule('请输入 Skill 内容'),
  name: requiredRule('请输入 Skill 名称'),
};

const columns = [
  { colKey: 'code', title: '编码', width: 160 },
  { colKey: 'name', title: '名称', width: 180 },
  { colKey: 'description', ellipsis: true, title: '说明' },
  { colKey: 'status', title: '状态', width: 100 },
  { colKey: 'actions', title: '操作', width: 150 },
];

const filteredRows = computed(() => {
  const keyword = filters.keyword.trim().toLowerCase();
  return rows.value.filter(
    (row) =>
      (!keyword ||
        row.code.toLowerCase().includes(keyword) ||
        row.name.toLowerCase().includes(keyword) ||
        row.content.toLowerCase().includes(keyword) ||
        (row.description || '').toLowerCase().includes(keyword)) &&
      (!filters.status || row.status === filters.status),
  );
});

function resetForm() {
  selected.value = undefined;
  Object.assign(form, {
    code: '',
    content: '',
    description: '',
    name: '',
    status: 'active',
  });
}

function openCreate() {
  resetForm();
  visible.value = true;
}

function openEdit(row: SprintMvpApi.Skill) {
  selected.value = row;
  Object.assign(form, {
    code: row.code,
    content: row.content,
    description: row.description || '',
    name: row.name,
    status: row.status,
  });
  visible.value = true;
}

async function loadRows() {
  loading.value = true;
  try {
    rows.value = await listSkillsApi();
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (!(await validateForm(formRef.value))) return;
  if (!form.code.trim() || !form.name.trim() || !form.content.trim()) {
    MessagePlugin.warning('Skill 编码、名称和内容不能为空');
    return;
  }

  if (selected.value) {
    await updateSkillApi(selected.value.id, {
      content: form.content.trim(),
      description: form.description.trim() || undefined,
      name: form.name.trim(),
      status: form.status,
    });
    MessagePlugin.success('Skill 已保存');
  } else {
    await createSkillApi({
      code: form.code.trim(),
      content: form.content.trim(),
      description: form.description.trim() || undefined,
      name: form.name.trim(),
    });
    MessagePlugin.success('Skill 已创建');
  }

  visible.value = false;
  await loadRows();
}

async function switchStatus(row: SprintMvpApi.Skill) {
  await updateSkillApi(row.id, {
    content: row.content,
    description: row.description,
    name: row.name,
    status: row.status === 'active' ? 'disabled' : 'active',
  });
  MessagePlugin.success(row.status === 'active' ? 'Skill 已停用' : 'Skill 已启用');
  await loadRows();
}

onMounted(loadRows);
</script>

<template>
  <div class="skills-page sprint-list-page">
    <section class="sprint-page-title">
      <h2>Skill配置</h2>
      <p>维护项目构建和新建需求时可选择的 Skill 指令内容。</p>
    </section>

    <section class="sprint-filter-panel">
      <div class="sprint-filter-grid">
        <label class="sprint-filter-field">
          <span>关键字</span>
          <TInput v-model="filters.keyword" clearable placeholder="编码 / 名称 / 内容" />
        </label>
        <label class="sprint-filter-field">
          <span>状态</span>
          <TSelect
            v-model="filters.status"
            clearable
            :options="[
              { label: '启用', value: 'active' },
              { label: '停用', value: 'disabled' },
            ]"
          />
        </label>
        <div class="sprint-filter-actions">
          <TButton theme="primary" @click="openCreate">新增Skill</TButton>
          <TButton variant="outline" @click="loadRows">刷新</TButton>
        </div>
      </div>
    </section>

    <section class="sprint-table-panel">
      <div class="sprint-table-header">
        <h3>Skill列表</h3>
        <div class="sprint-table-actions">
          <TTag variant="light">{{ filteredRows.length }} 项</TTag>
        </div>
      </div>

      <TTable
        row-key="id"
        class="sprint-compact-table"
        :columns="columns"
        :data="filteredRows"
        :loading="loading"
        size="small"
        hover
      >
        <template #status="{ row }">
          <TTag :theme="row.status === 'active' ? 'success' : 'default'" variant="light">
            {{ row.status === 'active' ? '启用' : '停用' }}
          </TTag>
        </template>
        <template #actions="{ row }">
          <TSpace class="sprint-row-actions">
            <TLink theme="primary" @click="openEdit(row)">编辑</TLink>
            <TLink :theme="row.status === 'active' ? 'danger' : 'primary'" @click="switchStatus(row)">
              {{ row.status === 'active' ? '停用' : '启用' }}
            </TLink>
          </TSpace>
        </template>
      </TTable>
    </section>

    <TDrawer
      v-model:visible="visible"
      :header="selected ? '编辑Skill' : '新增Skill'"
      :size="'56%'"
      confirm-btn="保存"
      @confirm="save"
    >
      <TForm ref="formRef" :data="form" :rules="rules" label-width="96px">
        <TFormItem label="编码" name="code">
          <TInput v-model="form.code" :disabled="!!selected" placeholder="AIR-CLOUD" />
        </TFormItem>
        <TFormItem label="名称" name="name">
          <TInput v-model="form.name" placeholder="Air.Cloud 交付规范" />
        </TFormItem>
        <TFormItem label="说明">
          <TInput v-model="form.description" placeholder="适用场景和约束摘要" />
        </TFormItem>
        <TFormItem v-if="selected" label="状态">
          <TSelect
            v-model="form.status"
            :options="[
              { label: '启用', value: 'active' },
              { label: '停用', value: 'disabled' },
            ]"
          />
        </TFormItem>
        <TFormItem label="内容" name="content">
          <TTextarea
            v-model="form.content"
            :autosize="{ minRows: 12, maxRows: 20 }"
            placeholder="填写 Skill 的使用规则、输入输出约束和验收要求"
          />
        </TFormItem>
      </TForm>
    </TDrawer>
  </div>
</template>
