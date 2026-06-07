<script lang="ts" setup>
import { computed, reactive, useSlots, watch } from 'vue';

import {
  Button as TButton,
  EnhancedTable as TEnhancedTable,
  Pagination as TPagination,
  Space as TSpace,
  Table as TTable,
  Tag as TTag,
} from 'tdesign-vue-next';

const props = withDefaults(defineProps<{
  addable?: boolean;
  columns: any[];
  data: any[];
  description?: string;
  expandedTreeNodes?: Array<number | string>;
  loading?: boolean;
  tableKey?: number | string;
  title: string;
  tree?: Record<string, any>;
}>(), {
  addable: true,
});

const emit = defineEmits<{
  add: [];
  expandedTreeNodesChange: [nodes: Array<number | string>];
}>();

const slots = useSlots();
const tableSlots = computed(() =>
  Object.keys(slots).filter((name) => !['action', 'filters', 'toolbar'].includes(name)),
);
const safeData = computed(() => (Array.isArray(props.data) ? props.data.filter(Boolean) : []));
const activeCount = computed(() => safeData.value.filter((item) => item?.status !== 0).length);
const pagination = reactive({
  current: 1,
  pageSize: 10,
  pageSizeOptions: [10, 20, 50],
  total: 0,
});

const pageData = computed(() => {
  const start = (pagination.current - 1) * pagination.pageSize;
  return safeData.value.slice(start, start + pagination.pageSize);
});
const showAddButton = computed(() => props.addable);

watch(
  () => safeData.value.length,
  (total) => {
    pagination.total = total;
    const maxPage = Math.max(1, Math.ceil(total / pagination.pageSize));
    if (pagination.current > maxPage) {
      pagination.current = maxPage;
    }
  },
  { immediate: true },
);

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

function normalizeSlotProps(slotProps: any) {
  return {
    ...slotProps,
    row: slotProps?.row ?? slotProps?.data?.row,
  };
}
</script>

<template>
  <div class="system-page">
    <header class="page-header">
      <div>
        <h2>{{ title }}</h2>
        <p>{{ description || '维护当前项目的 RBAC 主体、菜单授权和通用组织关系。' }}</p>
      </div>
      <div class="page-metrics">
        <TTag theme="primary" variant="light">总数 {{ safeData.length }}</TTag>
        <TTag theme="success" variant="light">启用 {{ activeCount }}</TTag>
      </div>
    </header>

    <section v-if="$slots.filters" class="query-form">
      <slot name="filters"></slot>
    </section>

    <section class="table-panel">
      <div class="table-toolbar">
        <div>
          <h3>{{ title }}列表</h3>
          <span>共 {{ safeData.length }} 条记录</span>
        </div>
        <TSpace>
          <slot name="toolbar"></slot>
          <TButton v-if="showAddButton" theme="primary" @click="emit('add')">
            <slot name="action">新增</slot>
          </TButton>
          <slot v-else name="action"></slot>
        </TSpace>
      </div>

      <TEnhancedTable
        v-if="tree"
        :key="tableKey"
        row-key="id"
        :columns="columns"
        :data="pageData"
        :expanded-tree-nodes="expandedTreeNodes"
        :loading="loading"
        :tree="tree"
        bordered
        hover
        @expanded-tree-nodes-change="emit('expandedTreeNodesChange', $event)"
      >
        <template v-for="name in tableSlots" :key="name" #[name]="slotProps">
          <slot :name="name" v-bind="normalizeSlotProps(slotProps)"></slot>
        </template>
      </TEnhancedTable>

      <TTable v-else row-key="id" :columns="columns" :data="pageData" :loading="loading" bordered hover>
        <template v-for="name in tableSlots" :key="name" #[name]="slotProps">
          <slot :name="name" v-bind="normalizeSlotProps(slotProps)"></slot>
        </template>
      </TTable>

      <div class="pagination-bar">
        <TPagination
          v-model="pagination.current"
          v-model:page-size="pagination.pageSize"
          :page-size-options="pagination.pageSizeOptions"
          :total="pagination.total"
          show-jumper
          show-page-size
          @change="handlePageChange"
        />
      </div>
    </section>
  </div>
</template>

<style scoped>
.system-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
}

.page-header,
.query-form,
.table-panel {
  background: var(--td-bg-color-container);
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 18px 20px;
}

.page-header h2,
.table-toolbar h3 {
  margin: 0;
  color: var(--td-text-color-primary);
}

.page-header h2 {
  font-size: 20px;
}

.page-header p {
  margin: 6px 0 0;
  color: var(--td-text-color-secondary);
}

.page-metrics,
.table-toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
}

.query-form {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  padding: 14px 20px;
}

.table-panel {
  padding: 16px 20px;
}

.table-toolbar {
  justify-content: space-between;
  margin-bottom: 14px;
}

.table-toolbar h3 {
  font-size: 16px;
}

.table-toolbar span {
  display: inline-block;
  margin-top: 4px;
  color: var(--td-text-color-secondary);
}

.pagination-bar {
  display: flex;
  justify-content: flex-end;
  padding-top: 14px;
}

@media (max-width: 768px) {
  .page-header,
  .table-toolbar {
    flex-direction: column;
    align-items: stretch;
  }

  .pagination-bar {
    justify-content: flex-start;
    overflow-x: auto;
  }
}
</style>
