<script lang="ts" setup>
import type { SprintMvpApi } from '#/api/sprint/mvp';

import { computed } from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  Button as TButton,
  Tag as TTag,
  Tooltip as TTooltip,
} from 'tdesign-vue-next';

defineOptions({ name: 'ProjectSecondaryListShell' });

const props = withDefaults(
  defineProps<{
    emptyText?: string;
    loading?: boolean;
    projects: SprintMvpApi.Project[];
    refreshText?: string;
    selectedProjectId: string;
  }>(),
  {
    emptyText: '暂无项目数据',
    loading: false,
    refreshText: '刷新',
  },
);

const emit = defineEmits<{
  'project-change': [project: SprintMvpApi.Project];
  refresh: [];
  'update:selectedProjectId': [projectId: string];
}>();

const selectedProject = computed(() =>
  props.projects.find((project) => project.id === props.selectedProjectId),
);

function hiddenTech(project: SprintMvpApi.Project) {
  return projectTech(project)
    .slice(3)
    .map((item) => `${item.scope}：${item.name}`);
}

function projectStatusTheme(status: string) {
  return status === 'active' ? 'success' : 'default';
}

function selectProject(project: SprintMvpApi.Project) {
  if (project.id === props.selectedProjectId) return;
  emit('update:selectedProjectId', project.id);
  emit('project-change', project);
}

function splitTechStack(value?: string) {
  return (value || '')
    .split(/[\/,，、;；\n\r]+/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function projectTech(project: SprintMvpApi.Project) {
  return [
    ...splitTechStack(project.frontendTechStack).map((name) => ({ name, scope: '前端' })),
    ...splitTechStack(project.backendTechStack).map((name) => ({ name, scope: '后端' })),
  ];
}

function visibleTech(project: SprintMvpApi.Project) {
  return projectTech(project).slice(0, 3);
}
</script>

<template>
  <div class="project-secondary-list-shell sprint-list-page">
    <slot name="header" />

    <section class="multi-layout">
      <aside class="project-tree-panel">
        <div class="panel-title">
          <h3>项目列表</h3>
          <TButton
            shape="circle"
            size="small"
            variant="outline"
            :loading="loading"
            :title="refreshText"
            @click="emit('refresh')"
          >
            <IconifyIcon icon="lucide:refresh-cw" />
          </TButton>
        </div>
        <div v-if="projects.length === 0 && !loading" class="empty-state">{{ emptyText }}</div>
        <div v-else class="project-card-list">
          <button
            v-for="project in projects"
            :key="project.id"
            class="project-card"
            :class="{ active: project.id === selectedProjectId }"
            type="button"
            @click="selectProject(project)"
          >
            <span class="project-card-head">
              <strong>{{ project.name }}</strong>
              <TTag :theme="projectStatusTheme(project.status)" size="small" variant="light">
                {{ project.status }}
              </TTag>
            </span>
            <span class="project-card-code">{{ project.code }}</span>
            <span class="project-card-desc">
              {{ project.description || '暂无项目说明' }}
            </span>
            <span v-if="$slots['project-meta']" class="project-card-meta">
              <slot name="project-meta" :project="project" />
            </span>
            <span class="project-card-stack">
              <slot name="project-stack" :project="project">
                <span class="project-tech-tags">
                  <TTag
                    v-for="tech in visibleTech(project)"
                    :key="`${project.id}-${tech.scope}-${tech.name}`"
                    theme="primary"
                    size="small"
                    :title="`${tech.scope}：${tech.name}`"
                    variant="light"
                  >
                    <span class="project-tech-tag-text" :title="`${tech.scope}：${tech.name}`">
                      {{ tech.name }}
                    </span>
                  </TTag>
                  <TTag
                    v-if="visibleTech(project).length === 0"
                    theme="primary"
                    size="small"
                    title="未配置技术栈"
                    variant="light"
                  >
                    未配置技术栈
                  </TTag>
                  <TTooltip v-if="hiddenTech(project).length > 0" placement="top" theme="light">
                    <template #content>
                      <div class="project-tech-tooltip">
                        <div v-for="tech in hiddenTech(project)" :key="tech">{{ tech }}</div>
                      </div>
                    </template>
                    <TTag class="project-tech-warning" theme="warning" size="small" variant="light">
                      <IconifyIcon icon="lucide:circle-alert" />
                    </TTag>
                  </TTooltip>
                </span>
              </slot>
            </span>
          </button>
        </div>
      </aside>

      <main class="sprint-project-workspace">
        <slot name="workspace-header" :project="selectedProject" :project-id="selectedProjectId">
          <div class="workspace-head">
            <div>
              <h3>{{ selectedProject?.name || '请选择项目' }}</h3>
              <p>{{ selectedProject?.code || '-' }}</p>
            </div>
          </div>
        </slot>

        <slot :project="selectedProject" :project-id="selectedProjectId" />
      </main>
    </section>
  </div>
</template>
