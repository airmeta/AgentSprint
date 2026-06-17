<script lang="ts" setup>
import { computed, onMounted } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { MessagePlugin, Select as TSelect } from 'tdesign-vue-next';

import { useProjectContextStore } from '#/store/project-context';

defineOptions({ name: 'HeaderProjectSelect' });

const projectContext = useProjectContextStore();

const projectOptions = computed(() =>
  projectContext.projects.map((project) => ({
    content: `${project.name} (${project.code})`,
    label: project.name,
    value: project.id,
  })),
);

async function loadProjects(force = false) {
  try {
    await projectContext.loadProjects(force);
  } catch {
    MessagePlugin.error('项目列表加载失败');
  }
}

onMounted(() => loadProjects());
</script>

<template>
  <div class="header-project-select">
    <IconifyIcon class="header-project-select__icon" icon="lucide:folder-kanban" />
    <TSelect
      v-model="projectContext.selectedProjectId"
      :borderless="true"
      filterable
      :loading="projectContext.loading"
      :options="projectOptions"
      placeholder="选择项目"
      size="small"
      @change="(value) => projectContext.selectProject(String(value || ''))"
      @focus="loadProjects()"
    />
  </div>
</template>

<style scoped>
.header-project-select {
  display: flex;
  width: min(30vw, 260px);
  min-width: 180px;
  height: 32px;
  align-items: center;
  gap: 4px;
  padding: 0 8px;
  margin-right: 12px;
  background: hsl(var(--accent));
  border-radius: 16px;
}

.header-project-select__icon {
  flex: 0 0 auto;
  width: 16px;
  height: 16px;
  color: hsl(var(--muted-foreground));
}

.header-project-select :deep(.t-select) {
  min-width: 0;
  flex: 1;
}

.header-project-select :deep(.t-input) {
  background: transparent;
}

.header-project-select :deep(.t-input__inner) {
  font-size: 12px;
}

@media (max-width: 768px) {
  .header-project-select {
    width: 172px;
    min-width: 0;
    margin-right: 4px;
  }
}
</style>
