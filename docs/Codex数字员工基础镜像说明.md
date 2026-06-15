# Codex 数字员工基础镜像说明

## 定位

`deploy/docker/Dockerfile.codex-worker-base` 用于构建每个数字员工容器的基础运行环境。

镜像包含：

- .NET SDK 10.0
- Debian 系统工具
- git
- curl
- Node.js
- npm
- Codex CLI
- jq、ripgrep、openssh-client 等常用诊断工具

默认目录：

| 目录 | 用途 |
| --- | --- |
| `/codex-home` | 独立 Codex 配置目录，对应 `CODEX_HOME` |
| `/workspaces` | 数字员工工作区根目录 |
| `/runs` | Codex 执行日志、结果和临时运行记录 |

## 构建

```bash
docker build -f deploy/docker/Dockerfile.codex-worker-base -t agentsprint-codex-worker-base:latest deploy/docker
```

## 验证

```bash
docker run --rm agentsprint-codex-worker-base:latest
```

容器会输出 .NET、git、Node.js、npm 和 Codex CLI 的版本信息。

## 使用建议

每个数字员工容器都应挂载独立卷：

```yaml
volumes:
  - worker-1-codex:/codex-home
  - worker-1-workspaces:/workspaces
  - worker-1-runs:/runs
```

不要在多个数字员工容器之间共享 `/codex-home`、`/workspaces` 或 Agent Token。
