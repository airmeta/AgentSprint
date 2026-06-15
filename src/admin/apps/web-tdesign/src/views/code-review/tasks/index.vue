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

defineOptions({ name: 'CodeReviewTasks' });

type CodeReviewTaskRow = {
  assignee?: string;
  branch?: string;
  id: string;
  projectName: string;
  status: 'cancelled' | 'completed' | 'draft' | 'failed' | 'running' | 'waiting';
  taskName: string;
};

const defaultFilters = {
  keyword: '',
  status: undefined as CodeReviewTaskRow['status'] | undefined,
};
const filters = reactive({ ...defaultFilters });
const query = reactive({ ...defaultFilters });
const pagination = reactive({
  current: 1,
  pageSize: 30,
  pageSizeOptions: [30, 50, 100, 200],
});
const rows = reactive<CodeReviewTaskRow[]>([]);

const statusOptions = [
  { label: '草稿', value: 'draft' },
  { label: '待审计', value: 'waiting' },
  { label: '审计中', value: 'running' },
  { label: '已完成', value: 'completed' },
  { label: '失败', value: 'failed' },
  { label: '已取消', value: 'cancelled' },
];
const columns: PrimaryTableCol[] = [
  { colKey: 'taskName', title: '任务名称', minWidth: 180 },
  { colKey: 'projectName', title: '项目', minWidth: 160 },
  { colKey: 'branch', title: '分支', minWidth: 150 },
  { colKey: 'assignee', title: '负责人', width: 130 },
  { colKey: 'status', title: '状态', cell: 'status', width: 110 },
  { colKey: 'actions', title: '操作', cell: 'actions', width: 130 },
];

const filteredRows = computed(() => {
  const keyword = query.keyword.trim().toLowerCase();
  return rows.filter((row) => {
    if (query.status && row.status !== query.status) {
      return false;
    }

    if (!keyword) {
      return true;
    }

    return [row.taskName, row.projectName, row.branch, row.assignee]
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

function statusTheme(status?: CodeReviewTaskRow['status']) {
  if (status === 'completed') return 'success';
  if (status === 'running') return 'primary';
  if (status === 'waiting' || status === 'draft') return 'warning';
  if (status === 'failed') return 'danger';
  return 'default';
}

function statusText(status?: CodeReviewTaskRow['status']) {
  return statusOptions.find((item) => item.value === status)?.label || '-';
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
  MessagePlugin.info('代码审计任务接口接入后即可维护任务');
}
</script>

<template>
  <AdminListPage
    title="代码审计任务"
    description="维护 AI 代码审查任务，后续可按项目、分支、负责人和执行状态跟踪审计进度。"
    table-title="代码审计任务列表"
    add-button-text="新增任务"
    :columns="columns"
    :data="filteredRows"
    :pagination="tablePagination"
    @add="showPendingFeature"
    @page-change="handlePageChange"
    @refresh="applyFilters"
    @reset="resetFilters"
    @search="applyFilters"
  >
    <template #filters>
      <label class="filter-field">
        <span>任务信息</span>
        <TInput v-model="filters.keyword" clearable placeholder="任务 / 项目 / 分支 / 负责人" />
      </label>
      <label class="filter-field">
        <span>状态</span>
        <TSelect v-model="filters.status" clearable placeholder="全部状态" :options="statusOptions" />
      </label>
    </template>
    <template #status="{ row }">
      <TTag :theme="statusTheme(row.status)" variant="light">{{ statusText(row.status) }}</TTag>
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
