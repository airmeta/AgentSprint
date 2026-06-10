import { viteBundler } from '@vuepress/bundler-vite';
import { defaultTheme } from '@vuepress/theme-default';
import { defineUserConfig } from 'vuepress';

export default defineUserConfig({
  lang: 'zh-CN',
  title: 'AgentSprint 文档',
  description: 'AgentSprint 协同开发平台初始化、MCP 接入与任务推进指南',

  bundler: viteBundler(),

  theme: defaultTheme({
    logo: null,
    repo: '',
    docsDir: 'docs',
    lastUpdated: true,
    contributors: false,
    navbar: [
      {
        text: '初始化教程',
        link: '/初始化教程.md',
      },
      {
        text: 'MCP 接入',
        link: '/AgentSprint-MCP接入说明.md',
      },
      {
        text: '工具清单',
        link: '/AgentSprint-MCP工具清单.md',
      },
    ],
    sidebar: {
      '/': [
        {
          text: '开始使用',
          children: [
            '/初始化教程.md',
            '/AgentSprint-MCP接入说明.md',
            '/AgentSprint-MCP工具清单.md',
          ],
        },
        {
          text: '平台配置',
          children: [
            '/运行环境与提示词管理说明.md',
            '/Skill配置管理说明.md',
            '/系统字典管理说明.md',
            '/业务数据清空说明.md',
            '/权限管理模型后续演进方案.md',
          ],
        },
        {
          text: '方案文档',
          children: [
            '/敏捷需求管理平台-Codex适配方案.md',
            '/优化建议功能实现方案.md',
          ],
        },
      ],
    },
  }),
});
