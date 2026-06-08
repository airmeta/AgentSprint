<script lang="ts" setup>
import type { SprintMvpApi } from '#/api/sprint/mvp';

import { computed, onMounted, reactive, ref } from 'vue';

import {
  Button as TButton,
  Drawer as TDrawer,
  Form as TForm,
  FormItem as TFormItem,
  Input as TInput,
  Link as TLink,
  MessagePlugin,
  Space as TSpace,
  Table as TTable,
  Tag as TTag,
  Textarea as TTextarea,
} from 'tdesign-vue-next';

import {
  approveRequirementReviewApi,
  listMyPendingReviewsApi,
  rejectRequirementReviewApi,
} from '#/api/sprint/mvp';

import '../_shared/table-layout.css';

const approving = ref(false);
const loading = ref(false);
const rejecting = ref(false);
const previewVisible = ref(false);
const reviewVisible = ref(false);
const current = ref<SprintMvpApi.RequirementReviewItem>();
const items = ref<SprintMvpApi.RequirementReviewItem[]>([]);
const filters = reactive({
  keyword: '',
  projectId: '',
  status: '',
});
const reviewForm = reactive({
  comment: '',
});
const pagination = reactive({
  current: 1,
  pageSize: 10,
});
const reviewStatusText: Record<string, string> = {
  approved: '已通过',
  pending: '待我评审',
  rejected: '已驳回',
};
const reviewStatusTheme: Record<string, 'danger' | 'primary' | 'success' | 'warning'> = {
  approved: 'success',
  pending: 'warning',
  rejected: 'danger',
};

const columns = [
  { colKey: 'requirement.title', title: '需求名' },
  { colKey: 'project.name', title: '项目名', width: 180 },
  { colKey: 'requirement.createdBy', title: '产品经理', width: 140 },
  { colKey: 'requirement.stakeholders', title: '干系人', width: 180 },
  { colKey: 'requirement.submittedAt', title: '提交时间', width: 180 },
  { colKey: 'currentStatus', title: '当前评审状态', width: 130 },
  { colKey: 'actions', title: '操作', width: 140 },
];
const statusOptions = [
  { label: '待我评审', value: 'pending' },
  { label: '已通过', value: 'approved' },
  { label: '已驳回', value: 'rejected' },
];
const projectOptions = computed(() =>
  Array.from(
    new Map(
      items.value.map((item) => [
        item.project.id,
        {
          label: item.project.name,
          value: item.project.id,
        },
      ]),
    ).values(),
  ),
);
const filteredItems = computed(() => {
  const keyword = filters.keyword.trim().toLowerCase();
  return items.value.filter((item) => {
    const status = resolveCurrentReviewStatus(item);
    return (
      (!filters.projectId || item.project.id === filters.projectId) &&
      (!filters.status || status === filters.status) &&
      (!keyword ||
        item.requirement.title.toLowerCase().includes(keyword) ||
        item.project.name.toLowerCase().includes(keyword) ||
        (item.requirement.stakeholders || '').toLowerCase().includes(keyword))
    );
  });
});
const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  pageSizeOptions: [10, 20, 50],
  showJumper: true,
  showPageSize: true,
  size: 'small' as const,
  total: filteredItems.value.length,
}));

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

function handleFilterChange() {
  pagination.current = 1;
}

function queryReviews() {
  pagination.current = 1;
}

function resetFilters() {
  Object.assign(filters, {
    keyword: '',
    projectId: '',
    status: '',
  });
  pagination.current = 1;
}

async function loadReviews() {
  loading.value = true;
  try {
    items.value = await listMyPendingReviewsApi();
    pagination.current = 1;
  } finally {
    loading.value = false;
  }
}

function openPreview(item: SprintMvpApi.RequirementReviewItem) {
  current.value = item;
  previewVisible.value = true;
}

function openReview(item: SprintMvpApi.RequirementReviewItem) {
  current.value = item;
  reviewForm.comment = '';
  reviewVisible.value = true;
}

function resolveCurrentReviewStatus(item: SprintMvpApi.RequirementReviewItem) {
  return item.reviews.find((review: SprintMvpApi.RequirementReview) => review.status === 'pending')
    ?.status || 'pending';
}

async function approve() {
  if (approving.value) return;
  if (!current.value) return;
  approving.value = true;
  try {
    await approveRequirementReviewApi(current.value.requirement.id, {
      comment: reviewForm.comment,
    });
    MessagePlugin.success('评审已通过');
    reviewVisible.value = false;
    await loadReviews();
  } finally {
    approving.value = false;
  }
}

async function reject() {
  if (rejecting.value) return;
  if (!current.value) return;
  rejecting.value = true;
  try {
    await rejectRequirementReviewApi(current.value.requirement.id, {
      comment: reviewForm.comment,
    });
    MessagePlugin.success('评审已驳回');
    reviewVisible.value = false;
    await loadReviews();
  } finally {
    rejecting.value = false;
  }
}

onMounted(loadReviews);
</script>

<template>
  <div class="reviews-page sprint-list-page">
    <section class="sprint-page-title">
      <h2>需求评审</h2>
      <p>仅展示待我评审的需求。</p>
    </section>

    <section class="sprint-filter-panel">
      <div class="sprint-filter-grid">
        <label class="sprint-filter-field">
          <span>项目</span>
          <TSelect
            v-model="filters.projectId"
            clearable
            :options="projectOptions"
            empty="暂无项目"
            placeholder="全部项目"
            @change="handleFilterChange"
          />
        </label>
        <label class="sprint-filter-field">
          <span>状态</span>
          <TSelect
            v-model="filters.status"
            clearable
            :options="statusOptions"
            placeholder="全部状态"
            @change="handleFilterChange"
          />
        </label>
        <label class="sprint-filter-field">
          <span>需求信息</span>
          <TInput
            v-model="filters.keyword"
            clearable
            placeholder="需求、项目、干系人"
            @change="handleFilterChange"
          />
        </label>
        <div class="sprint-filter-actions">
          <TButton theme="primary" :disabled="loading" @click="queryReviews">查询</TButton>
          <TButton variant="outline" :disabled="loading" @click="resetFilters">重置</TButton>
        </div>
      </div>
    </section>

    <section class="sprint-table-panel">
      <div class="sprint-table-header">
        <h3>评审列表</h3>
        <div class="sprint-table-actions">
          <TButton shape="circle" variant="outline" title="刷新" :loading="loading" @click="loadReviews">↻</TButton>
        </div>
      </div>

      <TTable
        row-key="requirement.id"
        class="sprint-compact-table"
        :columns="columns"
        :data="filteredItems"
        :loading="loading"
        :pagination="tablePagination"
        size="small"
        hover
        @page-change="handlePageChange"
      >
        <template #requirement.title="{ row }">
          {{ row.requirement.title }}
        </template>
        <template #project.name="{ row }">
          {{ row.project.name }}
        </template>
        <template #requirement.createdBy="{ row }">
          {{ row.requirement.createdBy }}
        </template>
        <template #requirement.stakeholders="{ row }">
          {{ row.requirement.stakeholders || '未填写' }}
        </template>
        <template #requirement.submittedAt="{ row }">
          {{ row.requirement.submittedAt || '-' }}
        </template>
        <template #currentStatus="{ row }">
          <TTag variant="light">
            {{ reviewStatusText[resolveCurrentReviewStatus(row)] }}
          </TTag>
        </template>
        <template #actions="{ row }">
          <TSpace class="sprint-row-actions">
            <TLink theme="primary" @click="openReview(row)">评审</TLink>
            <TLink theme="primary" @click="openPreview(row)">预览</TLink>
          </TSpace>
        </template>
      </TTable>
    </section>

    <TDrawer
      v-model:visible="previewVisible"
      :footer="false"
      :size="'60%'"
      :header="current?.requirement.title || '需求预览'"
    >
      <section v-if="current" class="review-preview">
        <div class="review-preview__header">
          <TTag :theme="reviewStatusTheme[resolveCurrentReviewStatus(current)]" variant="light">
            {{ reviewStatusText[resolveCurrentReviewStatus(current)] }}
          </TTag>
          <h3>{{ current.requirement.title }}</h3>
        </div>
        <dl>
          <dt>项目</dt>
          <dd>{{ current.project.name }}</dd>
          <dt>产品经理</dt>
          <dd>{{ current.requirement.createdBy }}</dd>
          <dt>干系人</dt>
          <dd>{{ current.requirement.stakeholders || '未填写' }}</dd>
          <dt>提交时间</dt>
          <dd>{{ current.requirement.submittedAt || '-' }}</dd>
        </dl>
        <h4>需求内容</h4>
        <p>{{ current.requirement.description || '暂无需求内容' }}</p>
        <h4>评审进度</h4>
        <div class="review-list">
          <div v-for="review in current.reviews" :key="review.id" class="review-list__item">
            <TTag :theme="reviewStatusTheme[review.status]" variant="light">
              {{ reviewStatusText[review.status] || review.status }}
            </TTag>
            <strong>{{ review.reviewerId }}</strong>
            <span>{{ review.reviewedAt || review.createTime }}</span>
            <p>{{ review.comment || '暂无意见' }}</p>
          </div>
        </div>
      </section>
    </TDrawer>

    <TDrawer v-model:visible="reviewVisible" :footer="false" :size="'60%'" header="需求评审">
      <section v-if="current" class="review-preview compact">
        <div class="review-preview__header">
          <TTag :theme="reviewStatusTheme[resolveCurrentReviewStatus(current)]" variant="light">
            {{ reviewStatusText[resolveCurrentReviewStatus(current)] }}
          </TTag>
          <h3>{{ current.requirement.title }}</h3>
        </div>
        <dl>
          <dt>项目</dt>
          <dd>{{ current.project.name }}</dd>
          <dt>产品经理</dt>
          <dd>{{ current.requirement.createdBy }}</dd>
          <dt>干系人</dt>
          <dd>{{ current.requirement.stakeholders || '未填写' }}</dd>
          <dt>提交时间</dt>
          <dd>{{ current.requirement.submittedAt || '-' }}</dd>
        </dl>
        <h4>需求内容</h4>
        <p>{{ current.requirement.description || '暂无需求内容' }}</p>
        <h4>评审进度</h4>
        <div class="review-list">
          <div v-for="review in current.reviews" :key="review.id" class="review-list__item">
            <TTag :theme="reviewStatusTheme[review.status]" variant="light">
              {{ reviewStatusText[review.status] || review.status }}
            </TTag>
            <strong>{{ review.reviewerId }}</strong>
            <span>{{ review.reviewedAt || review.createTime }}</span>
            <p>{{ review.comment || '暂无意见' }}</p>
          </div>
        </div>
      </section>
      <TForm :data="reviewForm" label-width="80px">
        <TFormItem label="意见">
          <TTextarea v-model="reviewForm.comment" placeholder="填写评审意见" />
        </TFormItem>
      </TForm>
      <div class="dialog-actions">
        <TButton theme="danger" @click="reject">驳回</TButton>
        <TButton theme="primary" @click="approve">通过</TButton>
      </div>
    </TDrawer>
  </div>
</template>

<style scoped>
.review-preview p {
  white-space: pre-wrap;
}

.dialog-actions {
  display: flex;
  gap: 10px;
}

.review-preview__header {
  display: grid;
  gap: 10px;
}

.review-preview h3,
.review-preview h4 {
  margin: 0;
}

.review-preview h4 {
  margin-top: 18px;
}

.review-preview dl {
  display: grid;
  grid-template-columns: 90px minmax(0, 1fr);
  gap: 10px;
  margin: 16px 0 0;
}

.review-preview dt {
  color: var(--td-text-color-secondary);
}

.review-preview dd {
  margin: 0;
}

.review-preview.compact {
  max-height: 420px;
  padding-right: 4px;
  margin-bottom: 16px;
  overflow: auto;
}

.review-list {
  display: grid;
  gap: 10px;
  margin-top: 10px;
}

.review-list__item {
  display: grid;
  grid-template-columns: auto 110px minmax(150px, 1fr);
  gap: 8px 10px;
  align-items: center;
  padding: 10px 0;
  border-top: 1px solid var(--td-component-border);
}

.review-list__item p {
  grid-column: 1 / -1;
  margin: 0;
  color: var(--td-text-color-secondary);
}

.dialog-actions {
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
