import { viteBundler } from '@vuepress/bundler-vite';
import { defaultTheme } from '@vuepress/theme-default';
import { defineUserConfig } from 'vuepress';
import { fileURLToPath } from 'node:url';

export default defineUserConfig({
  lang: 'zh-CN',
  title: 'AgentSprint 文档',
  description: 'AgentSprint 协同开发平台初始化、MCP 接入与任务推进指南',
  pagePatterns: ['**/*.md', '!docs_site/**'],

  bundler: viteBundler({
    viteOptions: {
      resolve: {
        alias: [
          {
            find: /^vue$/,
            replacement: fileURLToPath(new URL('../node_modules/vue/dist/vue.runtime.esm-bundler.js', import.meta.url)),
          },
          {
            find: /^vue\/server-renderer$/,
            replacement: fileURLToPath(new URL('../node_modules/@vue/server-renderer/dist/server-renderer.esm-bundler.js', import.meta.url)),
          },
        ],
      },
    },
  }),

  theme: defaultTheme({
    logo: null,
    repo: '',
    docsDir: 'docs',
    lastUpdated: true,
    contributors: false,
    navbar: [
      {
        text: '初始化教程',
        link: '/getting-started/初始化教程.md',
      },
      {
        text: 'MCP 接入',
        link: '/integrations/mcp/AgentSprint-MCP接入说明.md',
      },
      {
        text: '工具清单',
        link: '/integrations/mcp/AgentSprint-MCP工具清单.md',
      },
    ],
    sidebar: {
      '/': [
        {
          text: '开始使用',
          children: [
            '/getting-started/初始化教程.md',
          ],
        },
        {
          text: 'MCP 集成',
          children: [
            '/integrations/mcp/AgentSprint-MCP接入说明.md',
            '/integrations/mcp/AgentSprint-MCP工具清单.md',
          ],
        },
        {
          text: '平台配置',
          children: [
            '/platform-admin/运行环境与提示词管理说明.md',
            '/platform-admin/Skill配置管理说明.md',
            '/platform-admin/Git管理说明.md',
            '/platform-admin/系统字典管理说明.md',
            '/platform-admin/业务数据清空说明.md',
          ],
        },
        {
          text: '数字员工',
          children: [
            '/workers/codex-worker/数字员工受控端探针服务说明.md',
            '/workers/codex-worker/Codex数字员工基础镜像说明.md',
            '/workers/codex-worker/Codex数字员工迁移部署教程.md',
          ],
        },
        {
          text: '烛照',
          children: [
            '/works/烛照/design/敏捷需求管理平台-Codex适配方案.md',
            '/works/烛照/design/项目材料与提案管理方案.md',
            '/works/烛照/design/优化建议功能实现方案.md',
            '/works/烛照/design/权限管理模型后续演进方案.md',
            '/works/烛照/requirements/项目材料与提案管理需求细节.md',
            '/works/烛照/requirements/数字员工受控端研发需求细节.md',
            '/works/烛照/plan/项目材料提案转需求排程.md',
            '/works/烛照/plan/数字员工受控端开发排程.md',
          ],
        },
        {
          text: '瑶光',
          children: [
            '/works/瑶光/design/瑶光远程测试平台.md',
          ],
        },
      ],
    },
  }),
});
