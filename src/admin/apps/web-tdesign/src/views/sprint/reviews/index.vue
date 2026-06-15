<script lang="ts" setup>
import type { SprintMvpApi } from '#/api/sprint/mvp';

import { IconifyIcon } from '@vben/icons';
import { computed, onActivated, onMounted, reactive, ref } from 'vue';

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
  approveRequirementReviewApi,
  listProjectsApi,
  listMyPendingReviewsApi,
  rejectRequirementReviewApi,
} from '#/api/sprint/mvp';
import { formatDateTime } from '#/views/_shared/date-format';
import { withSerialColumn } from '#/views/_shared/table-columns';

import ProjectSecondaryListShell from '#/components/project-secondary-list-shell/project-secondary-list-shell.vue';
import MarkdownEditor from '../_shared/markdown-editor.vue';
import '../_shared/table-layout.css';

defineOptions({ name: 'SprintRequirementReviews' });

const approving = ref(false);
const loading = ref(false);
const rejecting = ref(false);
const previewVisible = ref(false);
const reviewVisible = ref(false);
const current = ref<SprintMvpApi.RequirementReviewItem>();
const items = ref<SprintMvpApi.RequirementReviewItem[]>([]);
const projects = ref<SprintMvpApi.Project[]>([]);
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
  pageSize: 30,
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
const selectedProject = computed(() =>
  projects.value.find((project) => project.id === filters.projectId),
);

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
const tablePagination = computed(() => ({
  current: pagination.current,
  pageSize: pagination.pageSize,
  pageSizeOptions: [30, 50, 100, 200],
  showJumper: true,
  showPageSize: true,
  size: 'small' as const,
  total: items.value.length,
}));

function handlePageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
}

async function handleFilterChange() {
  pagination.current = 1;
  await loadReviews();
}

async function handleProjectChange() {
  pagination.current = 1;
  await loadReviews();
}

async function queryReviews() {
  pagination.current = 1;
  await loadReviews();
}

async function resetFilters() {
  Object.assign(filters, {
    keyword: '',
    projectId: projects.value[0]?.id || '',
    status: '',
  });
  pagination.current = 1;
  await loadReviews();
}

async function loadProjects() {
  projects.value = await listProjectsApi();
  if (filters.projectId && projects.value.some((project) => project.id === filters.projectId)) return;
  filters.projectId = projects.value[0]?.id || '';
}

async function loadReviews(options: { refreshProjects?: boolean } = {}) {
  loading.value = true;
  try {
    if (options.refreshProjects || projects.value.length === 0) {
      await loadProjects();
    }
    items.value = await listMyPendingReviewsApi({
      keyword: filters.keyword || undefined,
      projectId: filters.projectId || undefined,
      status: filters.status || undefined,
    });
    pagination.current = 1;
  } finally {
    loading.value = false;
  }
}

async function refreshReviews() {
  await loadReviews({ refreshProjects: true });
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

onMounted(refreshReviews);
onActivated(loadReviews);
</script>

<template>
  <ProjectSecondaryListShell
    v-model:selected-project-id="filters.projectId"
    class="reviews-page"
    :loading="loading"
    :projects="projects"
    @project-change="handleProjectChange"
    @refresh="refreshReviews"
  >
    <template #header>
      <section class="sprint-page-title">
        <h2>需求评审</h2>
        <p>按项目查看待评审需求，完成评审意见与审批流转。</p>
      </section>
    </template>

    <template #workspace-header>
      <div class="workspace-head">
        <div>
          <h3>{{ selectedProject?.name || '请选择项目' }}</h3>
          <p>{{ selectedProject?.code || '-' }}</p>
        </div>
      </div>
    </template>

    <section class="sprint-filter-panel">
      <div class="sprint-filter-grid">
        <div class="sprint-filter-field">
          <span>状态</span>
          <TSelect
            v-model="filters.status"
            clearable
            :options="statusOptions"
            placeholder="全部状态"
            @change="handleFilterChange"
          />
        </div>
        <div class="sprint-filter-field">
          <span>需求信息</span>
          <TInput
            v-model="filters.keyword"
            clearable
            placeholder="需求、项目、干系人"
            @change="handleFilterChange"
          />
        </div>
        <div class="sprint-filter-actions">
          <TButton theme="primary" :loading="loading" @click="queryReviews">
            <template #icon>
              <IconifyIcon icon="lucide:search" />
            </template>
            查询
          </TButton>
          <TButton theme="default" :disabled="loading" @click="resetFilters">
            <template #icon>
              <IconifyIcon icon="lucide:rotate-ccw" />
            </template>
            重置
          </TButton>
        </div>
      </div>
    </section>

    <section class="sprint-table-panel">
      <div class="sprint-table-header">
        <h3>评审列表</h3>
        <div class="sprint-table-actions">
          <TButton shape="circle" variant="outline" title="刷新" :loading="loading" @click="loadReviews()">
            <IconifyIcon icon="lucide:refresh-cw" />
          </TButton>
        </div>
      </div>

      <TTable
        row-key="requirement.id"
        class="sprint-compact-table"
        :columns="withSerialColumn(columns, { offset: () => (pagination.current - 1) * pagination.pageSize })"
        :data="items"
        :loading="loading"
        :pagination="tablePagination"
        size="small"
        hover
        stripe
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
          {{ formatDateTime(row.requirement.submittedAt) }}
        </template>
        <template #currentStatus="{ row }">
          <TTag variant="light">
            {{ reviewStatusText[resolveCurrentReviewStatus(row)] }}
          </TTag>
        </template>
        <template #actions="{ row }">
          <TSpace class="sprint-row-actions">
            <TLink theme="primary" @click="openReview(row)">
              <IconifyIcon icon="lucide:clipboard-check" />
              评审
            </TLink>
            <TLink theme="primary" @click="openPreview(row)">
              <IconifyIcon icon="lucide:eye" />
              预览
            </TLink>
          </TSpace>
        </template>
      </TTable>
    </section>

    <TDrawer
      v-model:visible="previewVisible"
      :footer="false"
      :size="'60%'"
      :header="current?.requirement.title || '需求预览'"
      drawer-class-name="review-drawer"
    >
      <section v-if="current" class="review-preview">
        <div class="review-preview__body">
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
            <dd>{{ formatDateTime(current.requirement.submittedAt) }}</dd>
          </dl>
          <div class="review-markdown-area">
            <h4>需求内容</h4>
            <MarkdownEditor
              class="review-markdown-preview"
              :model-value="current.requirement.description || '暂无需求内容'"
              height="100%"
              preview
              preview-only
              read-only
              placeholder="暂无需求内容"
            />
          </div>
        </div>
        <section class="review-progress">
          <h4>评审进度</h4>
          <div class="review-list">
            <div v-for="review in current.reviews" :key="review.id" class="review-list__item">
              <TTag :theme="reviewStatusTheme[review.status]" variant="light">
                {{ reviewStatusText[review.status] || review.status }}
              </TTag>
              <strong>{{ review.reviewerId }}</strong>
              <span>{{ formatDateTime(review.reviewedAt || review.createTime) }}</span>
              <p>{{ review.comment || '暂无意见' }}</p>
            </div>
          </div>
        </section>
      </section>
    </TDrawer>

    <TDrawer
      v-model:visible="reviewVisible"
      :footer="false"
      :size="'60%'"
      header="需求评审"
      drawer-class-name="review-drawer"
    >
      <section v-if="current" class="review-preview compact">
        <div class="review-preview__body">
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
            <dd>{{ formatDateTime(current.requirement.submittedAt) }}</dd>
          </dl>
          <div class="review-markdown-area">
            <h4>需求内容</h4>
            <MarkdownEditor
              class="review-markdown-preview"
              :model-value="current.requirement.description || '暂无需求内容'"
              height="100%"
              preview
              preview-only
              read-only
              placeholder="暂无需求内容"
            />
          </div>
          <TForm :data="reviewForm" class="review-decision-form" label-width="80px">
            <TFormItem label="意见">
              <TTextarea v-model="reviewForm.comment" placeholder="填写评审意见" />
            </TFormItem>
          </TForm>
          <div class="dialog-actions">
            <TButton theme="danger" @click="reject">
              <template #icon>
                <IconifyIcon icon="lucide:x" />
              </template>
              驳回
            </TButton>
            <TButton theme="primary" @click="approve">
              <template #icon>
                <IconifyIcon icon="lucide:check" />
              </template>
              通过
            </TButton>
          </div>
        </div>
        <section class="review-progress">
          <h4>评审进度</h4>
          <div class="review-list">
            <div v-for="review in current.reviews" :key="review.id" class="review-list__item">
              <TTag :theme="reviewStatusTheme[review.status]" variant="light">
                {{ reviewStatusText[review.status] || review.status }}
              </TTag>
              <strong>{{ review.reviewerId }}</strong>
              <span>{{ formatDateTime(review.reviewedAt || review.createTime) }}</span>
              <p>{{ review.comment || '暂无意见' }}</p>
            </div>
          </div>
        </section>
      </section>
    </TDrawer>
  </ProjectSecondaryListShell>
</template>

<style scoped>
.review-preview {
  display: flex;
  height: calc(100vh - 96px);
  min-height: 0;
  flex-direction: column;
}

.dialog-actions {
  display: flex;
  gap: 10px;
}

.review-preview__header {
  display: grid;
  gap: 10px;
}

.review-preview__body {
  display: flex;
  height: calc(100% - 200px);
  min-height: 0;
  flex-direction: column;
  overflow: auto;
  padding-right: 4px;
}

.review-preview h3,
.review-preview h4 {
  margin: 0;
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

.review-markdown-area {
  display: flex;
  min-height: 280px;
  flex: 1;
  flex-direction: column;
  gap: 10px;
  margin-top: 18px;
}

.review-markdown-preview {
  min-height: 0;
  flex: 1;
}

.review-decision-form {
  margin-top: 16px;
}

.review-progress {
  height: 200px;
  min-height: 0;
  flex: 0 0 200px;
  padding-top: 12px;
  overflow: auto;
  border-top: 1px solid var(--td-component-border);
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
  white-space: pre-wrap;
}

.dialog-actions {
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
