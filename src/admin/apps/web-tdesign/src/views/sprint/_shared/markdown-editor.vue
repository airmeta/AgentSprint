<script lang="ts" setup>
import { MdEditor } from 'md-editor-v3';

import 'md-editor-v3/lib/style.css';

withDefaults(
  defineProps<{
    height?: number;
    placeholder?: string;
  }>(),
  {
    height: 360,
    placeholder: '使用 Markdown 编写内容。',
  },
);

const modelValue = defineModel<string>({ default: '' });
</script>

<template>
  <MdEditor
    v-model="modelValue"
    class="sprint-markdown-editor"
    language="zh-CN"
    :placeholder="placeholder"
    :preview-theme="'default'"
    :style="{ height: `${height}px` }"
    :toolbars-exclude="['github', 'save', 'catalog']"
  />
</template>

<style scoped>
.sprint-markdown-editor {
  border-color: var(--td-component-border);
  border-radius: 6px;
}

.sprint-markdown-editor :deep(.md-editor-toolbar-wrapper) {
  border-bottom-color: var(--td-component-border);
}
</style>
