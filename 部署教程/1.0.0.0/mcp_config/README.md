# MCP 与 Worker 配置说明

本目录保存 AgentSprint MCP 服务和 Codex Worker 的部署配置样例。真实部署时，Token、账号、密码、API Key 写入远端 `/opt/agentsprint-deploy/docker/.env` 或平台配置，不要提交到仓库。

## 文件说明

| 文件 | 用途 |
| --- | --- |
| `.env.example` | MCP 与 Worker 依赖的环境变量样例 |
| `docker-compose.mcp-worker.example.yml` | MCP 和 Worker Compose 片段 |
| `codex-config.example.toml` | Worker 容器内 `/codex-home/config.toml` 样例 |
| `worker-runtime.example.json` | Worker 运行配置参考 |

## 关键点

- MCP 容器通过 `AGENTSPRINT_API_BASE_URL=http://api:5000` 访问 API。
- Worker 容器通过 `AgentSprint__ApiBaseUrl=http://api:5000` 访问 API。
- Worker 挂载 `/workspaces`、`/codex-home`、`/runs` 三个目录。
- Codex 登录态保存在 `/codex-home`，多个 Worker 不应共享。
