<script lang="ts" setup>
import type { SprintMvpApi } from '#/api/sprint/mvp';

import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';

import {
  Button as TButton,
  Descriptions as TDescriptions,
  DescriptionsItem as TDescriptionsItem,
  Empty as TEmpty,
  Table as TTable,
  Tag as TTag,
} from 'tdesign-vue-next';

import {
  listBugsApi,
  listDevelopmentTasksApi,
  listProjectsApi,
  listRequirementsApi,
} from '#/api/sprint/mvp';

const route = useRoute();
const loading = ref(false);
const project = ref<SprintMvpApi.Project>();
const requirements = ref<SprintMvpApi.Requirement[]>([]);
const tasks = ref<SprintMvpApi.DevelopmentTask[]>([]);
const bugs = ref<SprintMvpApi.Bug[]>([]);

const requirementColumns = [
  { colKey: 'title', title: '需求' },
  { colKey: 'status', title: '状态', width: 130 },
  { colKey: 'health', title: '健康', width: 100 },
  { colKey: 'createdBy', title: '产品经理', width: 140 },
];
const taskColumns = [
  { colKey: 'title', title: '任务' },
  { colKey: 'status', title: '状态', width: 130 },
  { colKey: 'assigneeId', title: '负责人', width: 140 },
];
const bugColumns = [
  { colKey: 'title', title: '缺陷' },
  { colKey: 'status', title: '状态', width: 130 },
  { colKey: 'createdBy', title: '提交人', width: 140 },
];

const summary = computed(() => ({
  bugCount: bugs.value.length,
  openBugCount: bugs.value.filter((item) => item.status !== 'closed').length,
  requirementCount: requirements.value.length,
  taskCount: tasks.value.length,
}));

async function loadDetail() {
  loading.value = true;
  try {
    const projectId = String(route.params.id || '');
    const projects = await listProjectsApi();
    project.value = projects.find((item) => item.id === projectId);
    if (!project.value) return;

    [requirements.value, tasks.value, bugs.value] = await Promise.all([
      listRequirementsApi(projectId),
      listDevelopmentTasksApi({ projectId }),
      listBugsApi(projectId),
    ]);
  } finally {
    loading.value = false;
  }
}

onMounted(loadDetail);
</script>

<template>
  <div class="detail-page">
    <section class="header">
      <div>
        <h2>{{ project?.name || '项目详情' }}</h2>
        <p>{{ project?.code || '未找到项目' }}</p>
      </div>
      <TButton @click="loadDetail">刷新</TButton>
    </section>

    <TEmpty v-if="!loading && !project" description="项目不存在或已被删除" />

    <template v-else-if="project">
      <section class="panel">
        <TDescriptions bordered :column="2">
          <TDescriptionsItem label="项目编码">{{ project.code }}</TDescriptionsItem>
          <TDescriptionsItem label="状态">
            <TTag theme="success" variant="light">{{ project.status }}</TTag>
          </TDescriptionsItem>
          <TDescriptionsItem label="仓库地址">{{ project.repositoryUrl || '未配置' }}</TDescriptionsItem>
          <TDescriptionsItem label="测试环境">
            {{ project.testEnvironmentUrl || '未配置' }}
          </TDescriptionsItem>
          <TDescriptionsItem label="创建人">{{ project.createdBy }}</TDescriptionsItem>
          <TDescriptionsItem label="创建时间">{{ project.createTime }}</TDescriptionsItem>
        </TDescriptions>
      </section>

      <section class="stats">
        <div>
          <span>需求</span>
          <strong>{{ summary.requirementCount }}</strong>
        </div>
        <div>
          <span>任务</span>
          <strong>{{ summary.taskCount }}</strong>
        </div>
        <div>
          <span>缺陷</span>
          <strong>{{ summary.bugCount }}</strong>
        </div>
        <div>
          <span>未关闭缺陷</span>
          <strong>{{ summary.openBugCount }}</strong>
        </div>
      </section>

      <section class="panel">
        <h3>需求</h3>
        <TTable row-key="id" :columns="requirementColumns" :data="requirements" hover />
      </section>

      <section class="panel">
        <h3>任务</h3>
        <TTable row-key="id" :columns="taskColumns" :data="tasks" hover>
          <template #assigneeId="{ row }">
            {{ row.assigneeId || '未指派' }}
          </template>
        </TTable>
      </section>

      <section class="panel">
        <h3>缺陷</h3>
        <TTable row-key="id" :columns="bugColumns" :data="bugs" hover />
      </section>
    </template>
  </div>
</template>

<style scoped>
.detail-page {
  padding: 16px;
}

.header,
.panel,
.stats {
  margin-bottom: 16px;
  padding: 16px 20px;
  background: var(--td-bg-color-container);
  border: 1px solid var(--td-component-border);
  border-radius: 6px;
}

.header {
  display: flex;
  gap: 16px;
  align-items: center;
  flex-wrap: wrap;
  justify-content: space-between;
}

.header h2,
.panel h3 {
  margin: 0;
}

.header p {
  margin: 6px 0 0;
  color: var(--td-text-color-secondary);
}

.panel h3 {
  margin-bottom: 12px;
}

.stats {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
}

.stats span {
  display: block;
  color: var(--td-text-color-secondary);
}

.stats strong {
  display: block;
  margin-top: 6px;
  font-size: 24px;
}

@media (max-width: 960px) {
  .stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 560px) {
  .stats {
    grid-template-columns: 1fr;
  }
}
</style>
