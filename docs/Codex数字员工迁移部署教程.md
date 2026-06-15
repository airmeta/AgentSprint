# Codex 数字员工迁移部署教程

## 包内容

当前已在 `192.168.100.164` 生成迁移包：

- `/opt/math-for-codex/primary/export/agentsprint-codex-worker-base.tar.gz`
- `/opt/math-for-codex/primary/export/math-codex-worker-1-container.tar.gz`
- `/opt/math-for-codex/primary/export/math-for-codex-deploy-template.tgz`

推荐迁移方式是使用镜像包 `agentsprint-codex-worker-base.tar.gz` 加部署模板 `math-for-codex-deploy-template.tgz`。容器快照 `math-codex-worker-1-container.tar.gz` 仅用于留档，不建议作为主要迁移方式，因为 `docker export` 不包含 bind-mounted 的 `/codex-home`、`/workspaces`、`/runs` 外部目录。

## 目标服务器前置条件

目标服务器需要：

- Linux x86_64。
- Docker 20+，推荐 Docker 26+。
- Docker Compose v2。
- 能访问模型网关 `https://devai.yunsee.cn`。
- 如果启用 AgentSprint MCP，需要能访问对应 MCP endpoint。

检查命令：

```bash
docker --version
docker compose version
curl -I https://devai.yunsee.cn
```

## 1. 上传迁移包

从源服务器上传到目标服务器：

```bash
scp /opt/math-for-codex/primary/export/agentsprint-codex-worker-base.tar.gz root@<target-host>:/opt/math-for-codex/
scp /opt/math-for-codex/primary/export/math-for-codex-deploy-template.tgz root@<target-host>:/opt/math-for-codex/
```

## 2. 导入镜像

在目标服务器执行：

```bash
mkdir -p /opt/math-for-codex/primary
cd /opt/math-for-codex
gunzip -c agentsprint-codex-worker-base.tar.gz | docker load
tar -xzf math-for-codex-deploy-template.tgz -C /opt/math-for-codex/primary
```

## 3. 准备运行目录

```bash
cd /opt/math-for-codex/primary
cp deploy-template/docker-compose.yml ./
cp deploy-template/.env.example ./.env
mkdir -p worker-1/codex-home worker-1/workspaces worker-1/runs
cp deploy-template/worker-1/codex-home/config.toml worker-1/codex-home/config.toml
chmod 600 .env worker-1/codex-home/config.toml
```

最终结构：

```text
/opt/math-for-codex/primary/
  docker-compose.yml
  .env
  worker-1/
    codex-home/config.toml
    workspaces/
    runs/
```

## 4. 配置密钥和网关

编辑 `.env`：

```bash
vi /opt/math-for-codex/primary/.env
```

填写：

```bash
OPENAI_API_KEY=你的模型网关Key
AGENTSPRINT_AGENT_TOKEN=你的协同平台AgentToken
```

编辑 `worker-1/codex-home/config.toml`：

```toml
model = "gpt-5.4"
approval_policy = "never"
sandbox_mode = "workspace-write"
openai_base_url = "https://devai.yunsee.cn"

[mcp_servers.agentsprint]
url = "http://<agentsprint-mcp-host>:5010/mcp"
bearer_token_env_var = "AGENTSPRINT_AGENT_TOKEN"
required = false
tool_timeout_sec = 120
```

如果目标环境暂时没有 MCP，保留 `required = false`，Codex 仍可进行模型调用验证。

## 5. 启动数字员工

```bash
cd /opt/math-for-codex/primary
docker compose up -d
docker compose ps
```

## 6. 登录 Codex

首次启动后执行：

```bash
docker exec math-codex-worker-1 bash -lc 'printenv OPENAI_API_KEY | codex login --with-api-key && codex login status'
```

登录态会写入 `worker-1/codex-home/auth.json`。迁移到新服务器时建议重新登录，不要复用旧服务器的 `auth.json`。

## 7. 验证响应

```bash
docker exec math-codex-worker-1 bash -lc 'cd /workspaces && codex exec --skip-git-repo-check --output-last-message /runs/hello-final.md "你好"'
docker exec math-codex-worker-1 cat /runs/hello-final.md
```

预期输出：

```text
你好。
```

## 8. 多数字员工扩展

每个数字员工必须独立目录、独立容器、独立 Token：

```text
worker-1/codex-home
worker-1/workspaces
worker-1/runs
worker-2/codex-home
worker-2/workspaces
worker-2/runs
```

复制 compose service 时需要修改：

- `container_name`
- 挂载目录
- `.env` 或 token 变量来源

不要让多个数字员工共享 `/codex-home` 或 `/workspaces`。

## 常见问题

Codex 提示 MCP 连接失败：检查 `config.toml` 中 `mcp_servers.agentsprint.url` 是否可访问，或暂时保持 `required = false`。

Codex 提示未登录：重新执行 `codex login --with-api-key`。

Codex 提示 bubblewrap：当前镜像可以使用 Codex bundled bubblewrap；正式环境建议在 Dockerfile 中补 `bubblewrap` 包。
