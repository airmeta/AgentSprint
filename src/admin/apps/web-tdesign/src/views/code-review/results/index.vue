<script lang="ts" setup>
import type { PrimaryTableCol } from 'tdesign-vue-next';

import { computed, reactive, ref } from 'vue';

import {
  getCodeAuditReleaseReportApi,
  getCodeAuditTaskApi,
  listCodeAuditResultsApi,
  type CodeReviewApi,
} from '#/api/code-review';
import AdminListPage from '#/components/admin-list-page/admin-list-page.vue';
import RowAction from '#/views/system/_shared/row-action.vue';
import {
  Descriptions as TDescriptions,
  DescriptionsItem as TDescriptionsItem,
  Drawer as TDrawer,
  Input as TInput,
  Select as TSelect,
  Space as TSpace,
  TabPanel as TTabPanel,
  Tabs as TTabs,
  Tag as TTag,
} from 'tdesign-vue-next';

defineOptions({ name: 'CodeReviewResults' });

type CodeReviewResultRow = {
  auditTaskId: string;
  branch?: string;
  id: string;
  projectId: string;
  resultLevel: CodeReviewApi.TaskStatus;
  riskCount: number;
  taskName: string;
  workerId: string;
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
const detailVisible = ref(false);
const detailLoading = ref(false);
const detail = ref<CodeReviewApi.CodeAuditTaskDetail>();
const releaseReport = ref<CodeReviewApi.ReleaseReport>();
const activeDetailTab = ref('issues');

const levelOptions = [
  { label: '通过', value: 'passed' },
  { label: '需修改', value: 'needs_changes' },
  { label: '阻断', value: 'blocked' },
  { label: '失败', value: 'failed' },
  { label: '已取消', value: 'cancelled' },
  { label: '待审计', value: 'pending' },
  { label: '审计中', value: 'running' },
];
const columns: PrimaryTableCol[] = [
  { colKey: 'taskName', title: '审计任务', minWidth: 220 },
  { colKey: 'projectId', title: '项目', minWidth: 160 },
  { colKey: 'branch', title: '分支', width: 140 },
  { colKey: 'resultLevel', title: '结果等级', cell: 'resultLevel', width: 120 },
  { colKey: 'riskCount', title: '风险数', width: 100 },
  { colKey: 'workerId', title: 'Worker', minWidth: 180 },
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

    return [row.taskName, row.projectId, row.workerId, row.branch]
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
  if (level === 'running') return 'primary';
  if (level === 'pending' || level === 'needs_changes') return 'warning';
  if (level === 'blocked' || level === 'failed') return 'danger';
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

async function loadRows() {
  const data = await listCodeAuditResultsApi({
    keyword: query.keyword,
    status: query.resultLevel,
  });
  rows.splice(
    0,
    rows.length,
    ...data.map((item: CodeReviewApi.CodeAuditResultListItem) => ({
      auditTaskId: item.task.id,
      branch: item.result?.branch || item.task.branch,
      id: item.result?.id || item.task.id,
      projectId: item.task.projectId,
      resultLevel: item.task.status,
      riskCount:
        parseJsonArray(item.result?.issuesJson).length +
        parseJsonArray(item.result?.annotationIssuesJson).length,
      taskName: item.task.targetId
        ? `${item.task.auditTargetType} / ${item.task.targetId}`
        : item.task.id,
      workerId: item.task.workerId,
    })),
  );
}

function parseJsonArray(value?: string) {
  if (!value) {
    return [];
  }

  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

const issueItems = computed(() => parseJsonArray(detail.value?.result?.issuesJson));
const annotationIssueItems = computed(() => parseJsonArray(detail.value?.result?.annotationIssuesJson));
const manualCheckItems = computed(() => parseJsonArray(detail.value?.result?.manualCheckItemsJson));
const changedFiles = computed(() => parseJsonArray(detail.value?.result?.changedFilesJson));

async function openDetail(row: CodeReviewResultRow) {
  detailVisible.value = true;
  detailLoading.value = true;
  activeDetailTab.value = 'issues';
  releaseReport.value = undefined;
  try {
    const [taskDetail, report] = await Promise.all([
      getCodeAuditTaskApi(row.auditTaskId),
      getCodeAuditReleaseReportApi(row.auditTaskId),
    ]);
    detail.value = taskDetail;
    releaseReport.value = report;
    row.riskCount = issueItems.value.length + annotationIssueItems.value.length;
  } finally {
    detailLoading.value = false;
  }
}

loadRows();
</script>

<template>
  <AdminListPage
    title="代码审计结果"
    description="查看代码审查输出结果、风险分级、整改建议、版本边界和审计追踪。"
    table-title="代码审计结果列表"
    :addable="false"
    :columns="columns"
    :data="filteredRows"
    :pagination="tablePagination"
    @page-change="handlePageChange"
    @refresh="loadRows"
    @reset="resetFilters"
    @search="() => { applyFilters(); loadRows(); }"
  >
    <template #filters>
      <label class="filter-field">
        <span>结果信息</span>
        <TInput v-model="filters.keyword" clearable placeholder="任务 / 项目 / Worker" />
      </label>
      <label class="filter-field">
        <span>等级</span>
        <TSelect v-model="filters.resultLevel" clearable placeholder="全部等级" :options="levelOptions" />
      </label>
    </template>
    <template #resultLevel="{ row }">
      <TTag :theme="levelTheme(row.resultLevel)" variant="light">{{ levelText(row.resultLevel) }}</TTag>
    </template>
    <template #actions="{ row }">
      <TSpace>
        <RowAction icon="lucide:eye" label="详情" @click="openDetail(row)" />
      </TSpace>
    </template>
  </AdminListPage>

  <TDrawer v-model:visible="detailVisible" size="920px" header="代码审计结果详情" :footer="false">
    <div v-if="detailLoading" class="detail-empty">加载中...</div>
    <div v-else-if="detail" class="detail-panel">
      <TDescriptions bordered :column="2">
        <TDescriptionsItem label="审计任务">{{ detail.task.id }}</TDescriptionsItem>
        <TDescriptionsItem label="结论">
          <TTag :theme="levelTheme(detail.task.status)" variant="light">{{ levelText(detail.task.status) }}</TTag>
        </TDescriptionsItem>
        <TDescriptionsItem label="项目">{{ detail.task.projectId }}</TDescriptionsItem>
        <TDescriptionsItem label="仓库">{{ detail.task.gitRepositoryId }}</TDescriptionsItem>
        <TDescriptionsItem label="分支">{{ detail.result?.branch || detail.task.branch }}</TDescriptionsItem>
        <TDescriptionsItem label="Git Commit">{{ detail.result?.gitCommitId || detail.task.headCommitId || '-' }}</TDescriptionsItem>
        <TDescriptionsItem label="Base Commit">{{ detail.task.baseCommitId || '-' }}</TDescriptionsItem>
        <TDescriptionsItem label="Head Commit">{{ detail.task.headCommitId || '-' }}</TDescriptionsItem>
        <TDescriptionsItem label="当前分支 Head">{{ detail.task.currentBranchHeadCommitId || '-' }}</TDescriptionsItem>
        <TDescriptionsItem label="运行记录">{{ detail.result?.workerRunId || '-' }}</TDescriptionsItem>
      </TDescriptions>

      <div v-if="detail.task.workspaceDirtyReason" class="detail-warning">
        {{ detail.task.workspaceDirtyReason }}
      </div>

      <TTabs v-model="activeDetailTab" theme="card">
        <TTabPanel value="issues" label="问题列表">
          <div v-if="issueItems.length === 0" class="detail-empty">未记录明确问题。</div>
          <div v-for="(item, index) in issueItems" :key="index" class="issue-item">
            <div class="issue-title">{{ item.problem || item.message || item.title || item }}</div>
            <div class="issue-meta">
              <TTag variant="light">{{ item.severity || item.level || '未分级' }}</TTag>
              <span>{{ item.location || item.path || item.file || '-' }}</span>
            </div>
            <div v-if="item.direction || item.fix || item.recommendation" class="issue-direction">
              {{ item.direction || item.fix || item.recommendation }}
            </div>
          </div>
        </TTabPanel>
        <TTabPanel value="annotations" label="注释检查">
          <div v-if="annotationIssueItems.length === 0" class="detail-empty">注释检查未记录明确问题。</div>
          <div v-for="(item, index) in annotationIssueItems" :key="index" class="issue-item">
            <div class="issue-title">{{ item.problem || item.message || item.title || item }}</div>
            <div class="issue-meta">{{ item.location || item.path || item.file || '-' }}</div>
          </div>
        </TTabPanel>
        <TTabPanel value="manual" label="人工确认项">
          <div v-if="manualCheckItems.length === 0" class="detail-empty">暂无人工确认项。</div>
          <ul v-else class="manual-list">
            <li v-for="(item, index) in manualCheckItems" :key="index">{{ item }}</li>
          </ul>
        </TTabPanel>
        <TTabPanel value="release" label="发布报告">
          <div v-if="!releaseReport" class="detail-empty">暂无发布报告。</div>
          <div v-else class="report-panel">
            <div class="report-summary">
              <TTag :theme="releaseReport.canRelease ? 'success' : 'danger'" variant="light">
                {{ releaseReport.canRelease ? '可发布' : '不可发布' }}
              </TTag>
              <span>变更文件 {{ releaseReport.changedFileCount }}</span>
              <span>问题 {{ releaseReport.issueCount }}</span>
              <span>阻断 {{ releaseReport.blockingIssueCount }}</span>
              <span>人工确认 {{ releaseReport.manualCheckCount }}</span>
            </div>
            <div class="report-block">
              <h4>阻断摘要</h4>
              <div v-if="releaseReport.blockingSummaries.length === 0" class="detail-empty">无阻断摘要。</div>
              <ul v-else>
                <li v-for="(item, index) in releaseReport.blockingSummaries" :key="index">{{ item }}</li>
              </ul>
            </div>
            <div class="report-block">
              <h4>人工确认</h4>
              <div v-if="releaseReport.manualCheckItems.length === 0" class="detail-empty">无人工确认项。</div>
              <ul v-else>
                <li v-for="(item, index) in releaseReport.manualCheckItems" :key="index">{{ item }}</li>
              </ul>
            </div>
          </div>
        </TTabPanel>
        <TTabPanel value="files" label="Changed files">
          <div v-if="changedFiles.length === 0" class="detail-empty">暂无 changed files 记录。</div>
          <div v-for="(item, index) in changedFiles" :key="index" class="file-row">
            <TTag variant="light">{{ item.status || 'changed' }}</TTag>
            <span>{{ item.path || item.filePath || item }}</span>
          </div>
        </TTabPanel>
        <TTabPanel value="raw" label="原始输出">
          <pre class="raw-output">{{ detail.result?.rawResult || '暂无原始输出。' }}</pre>
        </TTabPanel>
      </TTabs>
    </div>
  </TDrawer>
</template>

<style scoped>
.filter-field {
  display: grid;
  grid-template-columns: auto minmax(180px, 260px);
  gap: 8px;
  align-items: center;
}

.detail-panel {
  display: grid;
  gap: 16px;
}

.detail-warning {
  padding: 10px 12px;
  color: var(--td-error-color-7);
  background: var(--td-error-color-1);
  border: 1px solid var(--td-error-color-3);
  border-radius: 6px;
}

.detail-empty {
  padding: 18px 0;
  color: var(--td-text-color-secondary);
}

.issue-item {
  display: grid;
  gap: 8px;
  padding: 12px 0;
  border-bottom: 1px solid var(--td-border-level-1-color);
}

.issue-title {
  font-weight: 600;
  color: var(--td-text-color-primary);
}

.issue-meta,
.file-row {
  display: flex;
  gap: 8px;
  align-items: center;
  color: var(--td-text-color-secondary);
}

.report-panel {
  display: grid;
  gap: 14px;
}

.report-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: center;
}

.report-block h4 {
  margin: 0 0 8px;
  font-size: 14px;
}

.file-row {
  padding: 8px 0;
  border-bottom: 1px solid var(--td-border-level-1-color);
}

.issue-direction {
  color: var(--td-text-color-secondary);
}

.manual-list {
  padding-left: 18px;
}

.raw-output {
  max-height: 420px;
  padding: 12px;
  overflow: auto;
  white-space: pre-wrap;
  background: var(--td-bg-color-container-hover);
  border-radius: 6px;
}
</style>
