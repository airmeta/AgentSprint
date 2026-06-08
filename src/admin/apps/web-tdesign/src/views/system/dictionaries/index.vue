<script lang="ts" setup>
import { computed, onMounted, reactive, ref, watch } from 'vue';
import type { FormInstanceFunctions, FormRules } from 'tdesign-vue-next';

import {
  deleteDictionaryItemApi,
  deleteDictionaryTypeApi,
  listDictionaryItemsApi,
  listDictionaryTypesApi,
  saveDictionaryItemApi,
  saveDictionaryTypeApi,
  type SystemApi,
} from '#/api';
import { optionalNumberRule, requiredRule, validateForm } from '#/views/_shared/form-rules';
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

const itemLoading = ref(false);
const itemSaving = ref(false);
const loading = ref(false);
const typeSaving = ref(false);
const itemVisible = ref(false);
const typeVisible = ref(false);
const itemFormRef = ref<FormInstanceFunctions>();
const typeFormRef = ref<FormInstanceFunctions>();
const dictionaryItems = ref<SystemApi.DictionaryItem[]>([]);
const dictionaryTypes = ref<SystemApi.DictionaryType[]>([]);
const selectedTypeId = ref('');

const filters = reactive({
  keyword: '',
  status: undefined as number | undefined,
});
const query = reactive({
  keyword: '',
  status: undefined as number | undefined,
});
const itemForm = reactive<Partial<SystemApi.DictionaryItem>>({
  code: '',
  description: '',
  dictionaryTypeId: '',
  id: undefined,
  name: '',
  sort: 0,
  status: 1,
});
const typeForm = reactive<Partial<SystemApi.DictionaryType>>({
  code: '',
  description: '',
  id: undefined,
  name: '',
  sort: 0,
  status: 1,
});

const itemRules: FormRules<typeof itemForm> = {
  code: requiredRule('请输入字典项编码'),
  dictionaryTypeId: requiredRule('请选择字典类型'),
  name: requiredRule('请输入字典项名称'),
  sort: optionalNumberRule('排序必须是数字'),
};
const typeRules: FormRules<typeof typeForm> = {
  code: requiredRule('请输入字典编码'),
  name: requiredRule('请输入字典名称'),
  sort: optionalNumberRule('排序必须是数字'),
};

const statusOptions = [
  { label: '启用', value: 1 },
  { label: '停用', value: 0 },
];
const itemColumns = [
  { colKey: 'code', title: '字典项编码', width: 180 },
  { colKey: 'name', title: '字典项名称', width: 160 },
  { colKey: 'description', title: '说明', cell: (...args: any[]) => getCellRow(args[0], args[1])?.description || '-' },
  { colKey: 'sort', title: '排序', width: 80 },
  { colKey: 'status', title: '状态', width: 90, cell: (...args: any[]) => (getCellRow(args[0], args[1])?.status === 1 ? '启用' : '停用') },
  { colKey: 'actions', title: '操作', width: 150, cell: 'actions' },
];
const typeColumns = [
  { colKey: 'code', title: '字典编码', width: 180 },
  { colKey: 'name', title: '字典名称', width: 160 },
  { colKey: 'sort', title: '排序', width: 80 },
  { colKey: 'status', title: '状态', width: 90, cell: (...args: any[]) => (getCellRow(args[0], args[1])?.status === 1 ? '启用' : '停用') },
  { colKey: 'actions', title: '操作', width: 170, cell: 'actions' },
];

const selectedType = computed(() => dictionaryTypes.value.find((item) => item.id === selectedTypeId.value));
const typeOptions = computed(() =>
  dictionaryTypes.value.map((item) => ({
    label: `${item.name} (${item.code})`,
    value: item.id,
  })),
);
const filteredItems = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();
  return dictionaryItems.value.filter((item) => {
    const matchesKeyword =
      !keyword ||
      item.code.toLowerCase().includes(keyword) ||
      item.name.toLowerCase().includes(keyword) ||
      (item.description || '').toLowerCase().includes(keyword);
    const matchesStatus = query.status === undefined || item.status === query.status;
    return matchesKeyword && matchesStatus;
  });
});

watch(selectedTypeId, async (value) => {
  if (!value) {
    dictionaryItems.value = [];
    return;
  }

  await loadItems(value);
});

function openItem(row?: SystemApi.DictionaryItem) {
  if (!selectedTypeId.value) {
    MessagePlugin.warning('请先选择字典类型');
    return;
  }

  Object.assign(itemForm, {
    code: row?.code || '',
    description: row?.description || '',
    dictionaryTypeId: row?.dictionaryTypeId || selectedTypeId.value,
    id: row?.id,
    name: row?.name || '',
    sort: row?.sort ?? 0,
    status: row?.status ?? 1,
  });
  itemVisible.value = true;
}

function openType(row?: SystemApi.DictionaryType) {
  Object.assign(typeForm, {
    code: row?.code || '',
    description: row?.description || '',
    id: row?.id,
    name: row?.name || '',
    sort: row?.sort ?? 0,
    status: row?.status ?? 1,
  });
  typeVisible.value = true;
}

async function loadItems(dictionaryTypeId = selectedTypeId.value) {
  if (!dictionaryTypeId) return;
  itemLoading.value = true;
  try {
    dictionaryItems.value = await listDictionaryItemsApi(dictionaryTypeId);
  } finally {
    itemLoading.value = false;
  }
}

async function loadTypes() {
  loading.value = true;
  try {
    dictionaryTypes.value = await listDictionaryTypesApi();
    if (!selectedTypeId.value || !dictionaryTypes.value.some((item) => item.id === selectedTypeId.value)) {
      selectedTypeId.value = dictionaryTypes.value[0]?.id || '';
    }
  } finally {
    loading.value = false;
  }
}

function reset() {
  Object.assign(filters, { keyword: '', status: undefined });
  search();
}

function search() {
  Object.assign(query, filters);
}

function selectType(row: SystemApi.DictionaryType) {
  selectedTypeId.value = row.id;
}

async function saveItem() {
  if (itemSaving.value) return;
  if (!(await validateForm(itemFormRef.value))) return;
  itemSaving.value = true;
  try {
    await saveDictionaryItemApi(itemForm);
    MessagePlugin.success('字典项已保存');
    itemVisible.value = false;
    await loadItems();
  } finally {
    itemSaving.value = false;
  }
}

async function saveType() {
  if (typeSaving.value) return;
  if (!(await validateForm(typeFormRef.value))) return;
  typeSaving.value = true;
  try {
    await saveDictionaryTypeApi(typeForm);
    MessagePlugin.success('字典类型已保存');
    typeVisible.value = false;
    await loadTypes();
  } finally {
    typeSaving.value = false;
  }
}

function removeItem(row: SystemApi.DictionaryItem) {
  DialogPlugin.confirm({
    body: `确认删除字典项 ${row.code}？`,
    confirmBtn: '删除',
    header: '删除字典项',
    onConfirm: async () => {
      await deleteDictionaryItemApi(row.id);
      MessagePlugin.success('字典项已删除');
      await loadItems();
    },
  });
}

function removeType(row: SystemApi.DictionaryType) {
  DialogPlugin.confirm({
    body: `确认删除字典类型 ${row.code}？该类型下的字典项也会被删除。`,
    confirmBtn: '删除',
    header: '删除字典类型',
    onConfirm: async () => {
      await deleteDictionaryTypeApi(row.id);
      MessagePlugin.success('字典类型已删除');
      await loadTypes();
    },
  });
}

onMounted(loadTypes);
</script>

<template>
  <div class="dictionary-page">
    <section class="type-panel">
      <SystemPage
        title="字典类型"
        description="维护可复用的业务枚举分类。"
        :columns="typeColumns"
        :data="dictionaryTypes"
        :loading="loading"
        @add="openType()"
      >
        <template #action>新增类型</template>
        <template #actions="{ row }">
          <TSpace>
            <TLink theme="primary" @click="selectType(row)">选择</TLink>
            <TLink theme="primary" @click="openType(row)">编辑</TLink>
            <TLink theme="danger" @click="removeType(row)">删除</TLink>
          </TSpace>
        </template>
      </SystemPage>
    </section>

    <section class="item-panel">
      <SystemPage
        title="字典项"
        :description="selectedType ? `当前类型：${selectedType.name} (${selectedType.code})` : '请先创建或选择字典类型。'"
        :columns="itemColumns"
        :data="filteredItems"
        :loading="itemLoading"
        :addable="Boolean(selectedTypeId)"
        @add="openItem()"
      >
        <template #filters>
          <TInput v-model="filters.keyword" clearable placeholder="字典项编码 / 名称 / 说明" class="filter-control" />
          <TSelect v-model="filters.status" clearable placeholder="状态" :options="statusOptions" class="filter-control" />
          <TSpace>
            <TButton theme="primary" :disabled="itemLoading" @click="search">查询</TButton>
            <TButton @click="reset">重置</TButton>
          </TSpace>
        </template>
        <template #action>新增字典项</template>
        <template #actions="{ row }">
          <TSpace>
            <TLink theme="primary" @click="openItem(row)">编辑</TLink>
            <TLink theme="danger" @click="removeItem(row)">删除</TLink>
          </TSpace>
        </template>
      </SystemPage>
    </section>

    <TDialog v-model:visible="typeVisible" header="字典类型维护" width="560px" :confirm-btn="{ content: '保存', loading: typeSaving }" @confirm="saveType">
      <TForm ref="typeFormRef" :data="typeForm" :rules="typeRules" label-width="96px">
        <TFormItem label="字典编码" name="code"><TInput v-model="typeForm.code" placeholder="requirement_priority" /></TFormItem>
        <TFormItem label="字典名称" name="name"><TInput v-model="typeForm.name" placeholder="需求优先级" /></TFormItem>
        <TFormItem label="说明"><TTextarea v-model="typeForm.description" /></TFormItem>
        <TFormItem label="排序" name="sort"><TInput v-model="typeForm.sort" type="number" /></TFormItem>
        <TFormItem label="状态"><TSelect v-model="typeForm.status" :options="statusOptions" /></TFormItem>
      </TForm>
    </TDialog>

    <TDialog v-model:visible="itemVisible" header="字典项维护" width="560px" :confirm-btn="{ content: '保存', loading: itemSaving }" @confirm="saveItem">
      <TForm ref="itemFormRef" :data="itemForm" :rules="itemRules" label-width="96px">
        <TFormItem label="字典类型" name="dictionaryTypeId"><TSelect v-model="itemForm.dictionaryTypeId" :options="typeOptions" /></TFormItem>
        <TFormItem label="字典项编码" name="code"><TInput v-model="itemForm.code" placeholder="high" /></TFormItem>
        <TFormItem label="字典项名称" name="name"><TInput v-model="itemForm.name" placeholder="高" /></TFormItem>
        <TFormItem label="说明"><TTextarea v-model="itemForm.description" /></TFormItem>
        <TFormItem label="排序" name="sort"><TInput v-model="itemForm.sort" type="number" /></TFormItem>
        <TFormItem label="状态"><TSelect v-model="itemForm.status" :options="statusOptions" /></TFormItem>
      </TForm>
    </TDialog>
  </div>
</template>

<style scoped>
.dictionary-page {
  display: grid;
  grid-template-columns: minmax(420px, 0.9fr) minmax(560px, 1.2fr);
  gap: 16px;
}

.filter-control {
  width: 220px;
}

@media (max-width: 1200px) {
  .dictionary-page {
    grid-template-columns: 1fr;
  }
}
</style>
