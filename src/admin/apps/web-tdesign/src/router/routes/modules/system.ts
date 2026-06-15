import type { RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    meta: {
      icon: 'lucide:settings',
      order: 90,
      title: '系统管理',
    },
    name: 'System',
    path: '/system',
    redirect: '/system/users',
    children: [
      {
        name: 'SystemUsers',
        path: '/system/users',
        component: () => import('#/views/system/users/index.vue'),
        meta: {
          icon: 'lucide:users',
          keepAlive: false,
          title: '用户管理',
        },
      },
      {
        name: 'SystemRoles',
        path: '/system/roles',
        component: () => import('#/views/system/roles/index.vue'),
        meta: {
          icon: 'lucide:shield-check',
          keepAlive: false,
          title: '角色管理',
        },
      },
      {
        name: 'SystemRoleAuthorize',
        path: '/system/roles/authorize/:id',
        component: () => import('#/views/system/roles/authorize.vue'),
        meta: {
          activePath: '/system/roles',
          hideInMenu: true,
          title: '角色授权',
        },
      },
      {
        name: 'SystemMenus',
        path: '/system/menus',
        component: () => import('#/views/system/menus/index.vue'),
        meta: {
          icon: 'lucide:menu',
          keepAlive: false,
          title: '菜单管理',
        },
      },
      {
        name: 'SystemDictionaries',
        path: '/system/dictionaries',
        component: () => import('#/views/system/dictionaries/index.vue'),
        meta: {
          icon: 'lucide:book-open-text',
          keepAlive: false,
          title: '字典管理',
        },
      },
      {
        name: 'SystemConfigurations',
        path: '/system/configurations',
        component: () => import('#/views/system/configurations/index.vue'),
        meta: {
          icon: 'lucide:sliders-horizontal',
          keepAlive: false,
          title: '系统配置',
        },
      },
      {
        name: 'SystemDepartments',
        path: '/system/departments',
        component: () => import('#/views/system/departments/index.vue'),
        meta: {
          icon: 'lucide:network',
          keepAlive: false,
          title: '部门管理',
        },
      },
      {
        name: 'SystemAssignments',
        path: '/system/assignments',
        component: () => import('#/views/system/assignments/index.vue'),
        meta: {
          icon: 'lucide:briefcase-business',
          keepAlive: false,
          title: '岗位管理',
        },
      },
    ],
  },
  {
    meta: {
      icon: 'lucide:server-cog',
      order: 91,
      title: '运维管理',
    },
    name: 'OperationManagement',
    path: '/operations',
    redirect: '/operations/scripts',
    children: [
      {
        name: 'OperationScripts',
        path: '/operations/scripts',
        component: () => import('#/views/operations/scripts/index.vue'),
        meta: {
          icon: 'lucide:file-terminal',
          keepAlive: false,
          title: '脚本管理',
        },
      },
      {
        name: 'OperationEnvironments',
        path: '/operations/environments',
        component: () => import('#/views/system/runtime-environments/index.vue'),
        meta: {
          icon: 'lucide:server-cog',
          keepAlive: false,
          title: '环境配置',
        },
      },
    ],
  },
  {
    meta: {
      icon: 'lucide:sliders-horizontal',
      order: 92,
      title: '全局配置',
    },
    name: 'GlobalConfig',
    path: '/global-config',
    redirect: '/global-config/ai-platforms',
    children: [
      {
        name: 'GlobalConfigAiPlatforms',
        path: '/global-config/ai-platforms',
        component: () => import('#/views/system/ai-platforms/index.vue'),
        meta: {
          icon: 'lucide:cpu',
          keepAlive: false,
          title: 'AI平台维护',
        },
      },
      {
        name: 'GlobalConfigPromptTemplates',
        path: '/global-config/prompt-templates',
        component: () => import('#/views/system/prompt-templates/index.vue'),
        meta: {
          icon: 'lucide:message-square-code',
          keepAlive: false,
          title: '提示词设置',
        },
      },
      {
        name: 'GlobalConfigSkills',
        path: '/global-config/skills',
        component: () => import('#/views/sprint/skills/index.vue'),
        meta: {
          icon: 'lucide:brain-circuit',
          keepAlive: false,
          title: 'Skill配置',
        },
      },
    ],
  },
  {
    meta: {
      icon: 'lucide:bot',
      order: 93,
      title: '自动化管理',
    },
    name: 'AutomationManagement',
    path: '/automation',
    redirect: '/automation/digital-workers',
    children: [
      {
        name: 'AutomationDigitalWorkers',
        path: '/automation/digital-workers',
        component: () => import('#/views/automation/digital-workers/index.vue'),
        meta: {
          icon: 'lucide:bot',
          title: '数字员工管理',
        },
      },
      {
        name: 'AutomationMcpSessions',
        path: '/automation/mcp-sessions',
        component: () => import('#/views/automation/mcp-sessions/index.vue'),
        meta: {
          icon: 'lucide:monitor-dot',
          title: 'MCP会话管理',
        },
      },
    ],
  },
];

export default routes;
