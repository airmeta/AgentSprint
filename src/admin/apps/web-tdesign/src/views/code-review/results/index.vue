<script lang="ts" setup>
import type { PrimaryTableCol } from 'tdesign-vue-next';

import { computed, reactive } from 'vue';

import AdminListPage from '#/components/admin-list-page/admin-list-page.vue';
import RowAction from '#/views/system/_shared/row-action.vue';
import {
  Input as TInput,
  MessagePlugin,
  Select as TSelect,
  Space as TSpace,
  Tag as TTag,
} from 'tdesign-vue-next';

defineOptions({ name: 'CodeReviewResults' });

type CodeReviewResultRow = {
  id: string;
  projectName: string;
  resultLevel: 'critical' | 'high' | 'low' | 'medium' | 'passed';
  reviewer?: string;
  riskCount: number;
  taskName: string;
};

const defaultFilters = {
  keyword: '',
  resultLevel: undefined as CodeReviewResultRow['resultLevel'] | undefined,
};
const filters = reactive({ ...defaultFilters });
const query = reactive({ ...defaultFilters });
const pagination = reactive({
  current: 1,
  pageSize: 30,
  pageSizeOptions: [30, 50, 100, 200],
});
const rows = reactive<CodeReviewResultRow[]>([]);

const levelOptions = [
  { label: '通过', value: 'passed' },
  { label: '低风险', value: 'low' },
  { label: '中风险', value: 'medium' },
  { label: '高风险', value: 'high' },
  { label: '严重', value: 'critical' },
];
const columns: PrimaryTableCol[] = [
  { colKey: 'taskName', title: '任务名称', minWidth: 180 },
  { colKey: 'projectName', title: '项目', minWidth: 160 },
  { colKey: 'resultLevel', title: '结果等级', cell: 'resultLevel', width: 120 },
  { colKey: 'riskCount', title: '风险数', width: 100 },
  { colKey: 'reviewer', title: '审查人', width: 130 },
  { colKey: 'actions', title: '操作', cell: 'actions', width: 130 },
];

const filteredRows = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();
  return rows.filter((row) => {
    if (query.resultLevel && row.resultLevel !== query.resultLevel) {
      return false;
    }

    if (!keyword) {
      return true;
    }

    return [row.taskName, row.projectName, row.reviewer]
      .filter(Boolean)
      .join('\n')
      .toLowerCase()
      .includes(keyword);
  });
});
const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  pageSizeOptions: pagination.pageSizeOptions,
  total: filteredRows.value.length,
}));

function levelTheme(level?: CodeReviewResultRow['resultLevel']) {
  if (level === 'passed') return 'success';
  if (level === 'low') return 'primary';
  if (level === 'medium') return 'warning';
  if (level === 'high' || level === 'critical') return 'danger';
  return 'default';
}

function levelText(level?: CodeReviewResultRow['resultLevel']) {
  return levelOptions.find((item) => item.value === level)?.label || '-';
}

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

function applyFilters() {
  Object.assign(query, filters);
  pagination.current = 1;
}

function resetFilters() {
  Object.assign(filters, defaultFilters);
  applyFilters();
}

function showPendingFeature() {
  MessagePlugin.info('代码审计结果接口接入后即可查看详情');
}
</script>

<template>
  <AdminListPage
    title="代码审计结果"
    description="查看代码审查输出结果，后续可承载风险分级、整改建议、证据和审计追踪。"
    table-title="代码审计结果列表"
    :addable="false"
    :columns="columns"
    :data="filteredRows"
    :pagination="tablePagination"
    @page-change="handlePageChange"
    @refresh="applyFilters"
    @reset="resetFilters"
    @search="applyFilters"
  >
    <template #filters>
      <label class="filter-field">
        <span>结果信息</span>
        <TInput v-model="filters.keyword" clearable placeholder="任务 / 项目 / 审查人" />
      </label>
      <label class="filter-field">
        <span>等级</span>
        <TSelect v-model="filters.resultLevel" clearable placeholder="全部等级" :options="levelOptions" />
      </label>
    </template>
    <template #resultLevel="{ row }">
      <TTag :theme="levelTheme(row.resultLevel)" variant="light">{{ levelText(row.resultLevel) }}</TTag>
    </template>
    <template #actions>
      <TSpace>
        <RowAction icon="lucide:eye" label="详情" @click="showPendingFeature" />
      </TSpace>
    </template>
  </AdminListPage>
</template>

<style scoped>
.filter-field {
  display: grid;
  grid-template-columns: auto minmax(180px, 260px);
  gap: 8px;
  align-items: center;
}
</style>
