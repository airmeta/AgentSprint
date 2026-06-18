# AgentSprint 1.0.0.0 部署文档

本文档用于部署 AgentSprint 平台。当前部署形态为 Docker Compose，包含 Admin、API、MCP、Worker 四个 AgentSprint 业务容器，并依赖 MySQL 与 Redis。

## AI 执行约束

如果由 AI Agent 或自动化工具按本文档执行部署，必须遵守以下约束：

- 只操作 AgentSprint 相关目录：`/opt/agentsprint-deploy` 与 `/opt/math-for-codex/primary/worker-1`。
- 不要执行 `docker system prune -a`、全局删除容器、全局删除镜像或全局删除 Docker 卷。
- 不要删除非 AgentSprint 容器。目标服务器可能混部了其它系统。
- 不要打印 `.env`、Token、API Key、数据库密码等敏感值；只允许检查键是否存在。
- 部署前必须备份远端 `/opt/agentsprint-deploy/docker` 目录。
- 部署前必须确认当前 Git 改动符合预期，不要覆盖或回滚用户未要求处理的改动。
- 部署失败时先查看 `docker compose ps`、`docker logs` 和 HTTP 健康检查结果，不要直接重装服务器或清理全局环境。
- 回滚只允许回滚到 `/opt/agentsprint-deploy/docker.bak.*` 目录，并且回滚后必须重新执行健康检查。

## 变量替换清单

执行前必须确认或替换以下配置：

| 配置项 | 文档默认值或占位符 | 说明 |
| --- | --- | --- |
| 服务器 IP | `192.168.100.100` | 示例目标服务器 IP，请根据实际环境调整 |
| SSH 用户 | `<SSH_USER>` | 具备 Docker 操作权限的远端用户 |
| SSH 私钥路径 | `<SSH_KEY_PATH>` | 本地登录目标服务器使用的私钥路径 |
| 本地仓库目录 | `D:/AgentSprint` | 示例仓库目录，请根据实际目录调整 |
| MySQL 地址 | `192.168.100.100:3306` | API 使用的数据库地址 |
| Redis 地址 | `192.168.100.100:6379` | API 使用的 Redis 地址 |
| 数据库密码 | `AGENTSPRINT_DB_PASSWORD` | 写入远端 `/opt/agentsprint-deploy/docker/.env` |
| Agent Token | `AGENTSPRINT_AGENT_TOKEN` | Worker 访问平台使用 |
| 模型网关 Key | `OPENAI_API_KEY` | Codex Worker 登录和执行使用 |

## 0. 服务器信息

部署前先确定目标服务器信息。本文使用 `192.168.100.100` 作为示例服务器 IP；实际部署时请按目标环境替换。

| 配置项 | 占位符 | 示例说明 |
| --- | --- | --- |
| 服务器 IP 或域名 | `192.168.100.100` | 示例目标服务器地址 |
| SSH 用户 | `<SSH_USER>` | 通常为 `root` 或具备 Docker 权限的用户 |
| SSH 私钥路径 | `<SSH_KEY_PATH>` | 本地用于登录服务器的私钥路径 |
| 本地仓库根目录 | `D:/AgentSprint` | 示例仓库目录，请根据自己的实际目录调整 |
| MySQL 地址 | `192.168.100.100` | 示例 MySQL 地址 |
| Redis 地址 | `192.168.100.100` | 示例 Redis 地址 |

本文默认 MySQL 和 Redis 与 AgentSprint 部署在同一台服务器，因此示例地址都使用 `192.168.100.100`；如果数据库或 Redis 独立部署，请填写对应服务地址。

## 1. 部署拓扑

### AgentSprint 业务服务

| 服务 | Compose service | 容器名 | 镜像 | 端口 | 作用 |
| --- | --- | --- | --- | --- | --- |
| Admin 前端 | `admin` | `agentsprint-admin` | `agentsprint-admin:latest` | `5999:80` | 管理后台页面 |
| API 后端 | `api` | `agentsprint-api` | `agentsprint-api:latest` | `5000:5000`, `25520:25520` | 主业务 API 与 Akka 平台节点 |
| MCP 服务 | `mcp` | `agentsprint-mcp` | `agentsprint-mcp:latest` | `5010:5010` | Codex 连接平台的 MCP 服务 |
| Codex Worker | `math-codex-worker-1` | `math-codex-worker-1` | `agentsprint-worker:latest` | 无外部端口 | 受控端，轮询平台任务并启动 Codex |

### 基础依赖

| 服务 | 容器名 | 镜像 | 端口 | 持久化 |
| --- | --- | --- | --- | --- |
| MySQL | `mysql_server` | `mysql:latest` | `3306:3306` | `/opt/servers/mysql/data -> /var/lib/mysql` |
| Redis | `redis` | `redis` | `6379:6379` | `/opt/servers/redis/redis.conf -> /usr/local/etc/redis/redis.conf` |

如果目标服务器还混部了其它系统容器，它们不属于 AgentSprint 1.0.0.0 的核心部署对象。日常部署 AgentSprint 时不要清理全局 Docker 容器、镜像或卷。

## 2. 服务器要求

- 服务器：`192.168.100.100`
- SSH 用户：`<SSH_USER>`
- 系统架构：Linux x86_64
- Docker：已验证 `29.1.3`
- Docker Compose：已验证 `2.40.3`
- 部署根目录：`/opt/agentsprint-deploy`
- Compose 目录：`/opt/agentsprint-deploy/docker`
- 部署包路径：`/opt/agentsprint-deploy/agentsprint-docker-deploy.tgz`

需要确认以下端口未被其它业务占用：

| 端口 | 用途 |
| --- | --- |
| `5999` | Admin 前端访问端口 |
| `5000` | API 访问端口 |
| `5010` | MCP 访问端口 |
| `25520` | API Akka 节点端口 |
| `3306` | MySQL |
| `6379` | Redis |

## 3. 目录结构

AgentSprint 部署目录：

```text
/opt/agentsprint-deploy/
  agentsprint-docker-deploy.tgz
  deploy.sh
  docker/
    .env
    docker-compose.yml
    Dockerfile.api
    Dockerfile.admin
    Dockerfile.mcp
    Dockerfile.worker
    Dockerfile.codex-worker-base
    nginx.conf
    fastgithub-worker-entrypoint.sh
    fastgithub_linux-x64.zip
    artifacts/
      api/
      admin/
      mcp/
      worker/
```

Worker 数据目录：

```text
/opt/math-for-codex/primary/worker-1/
  codex-home/
  workspaces/
  runs/
```

Worker 容器挂载关系：

```text
/opt/math-for-codex/primary/worker-1/workspaces -> /workspaces
/opt/math-for-codex/primary/worker-1/codex-home -> /codex-home
/opt/math-for-codex/primary/worker-1/runs -> /runs
```

## 4. 配置文件

本目录提供两类配置样例：

- `api_config/`：API 服务配置、数据库与 Redis 环境变量、Compose 片段。
- `mcp_config/`：MCP 服务配置、Worker/Codex 接入配置、Compose 片段。

远端 `/opt/agentsprint-deploy/docker/.env` 至少需要包含：

```bash
AGENTSPRINT_DB_PASSWORD=替换为数据库密码
AGENTSPRINT_AGENT_TOKEN=替换为平台签发的AgentToken
OPENAI_API_KEY=替换为模型网关Key
```

敏感值不要提交到仓库。仓库内只保留 `.example` 或占位符配置。

## 5. 基础依赖准备

### MySQL

当前 API 使用连接字符串：

```text
server=192.168.100.100;port=3306;database=agentsprint;user=root;password=${AGENTSPRINT_DB_PASSWORD};Allow User Variables=True;UseAffectedRows=False;CharSet=utf8mb4;
```

首次部署前需要确保 MySQL 已启动，并存在 `agentsprint` 数据库。当前 API 配置 `Database__AutoInitialize=true`，应用启动时会自动初始化表结构和种子数据。

### Redis

默认连接：

```text
192.168.100.100:6379
```

如果 Redis 设置了用户名或密码，在 `.env` 中补充：

```bash
AGENTSPRINT_REDIS_CONNECTION_STRING=192.168.100.100:6379
AGENTSPRINT_REDIS_USERNAME=
AGENTSPRINT_REDIS_PASSWORD=
```

## 6. 本地构建部署包

在仓库根目录执行：

```powershell
cd D:/AgentSprint
git status --short
```

后端验证：

```powershell
dotnet test .\src\api\AgentSprint.Tests\AgentSprint.Tests.csproj --no-restore
```

前端验证与构建：

```powershell
cd .\src\admin
corepack pnpm -F @vben/web-tdesign run typecheck
corepack pnpm -F @vben/web-tdesign run build
cd ..\..
```

生成部署产物：

```powershell
Remove-Item -Recurse -Force .\deploy\docker\artifacts -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force .\deploy\docker\artifacts\api | Out-Null
New-Item -ItemType Directory -Force .\deploy\docker\artifacts\mcp | Out-Null
New-Item -ItemType Directory -Force .\deploy\docker\artifacts\admin | Out-Null
New-Item -ItemType Directory -Force .\deploy\docker\artifacts\worker | Out-Null

dotnet publish .\src\api\AgentSprint.Entry\AgentSprint.Entry.csproj -c Release -o .\deploy\docker\artifacts\api --no-restore
dotnet publish .\src\api\AgentSprint.Mcp\AgentSprint.Mcp.csproj -c Release -o .\deploy\docker\artifacts\mcp --no-restore
dotnet publish .\src\api\AgentSprint.Worker\AgentSprint.Worker.csproj -c Release -o .\deploy\docker\artifacts\worker --no-restore
Copy-Item -Recurse -Force .\src\admin\apps\web-tdesign\dist\* .\deploy\docker\artifacts\admin\
```

打包：

```powershell
if (Test-Path .\agentsprint-docker-deploy.tgz) {
  Move-Item -Force .\agentsprint-docker-deploy.tgz .\agentsprint-docker-deploy.tgz.prev
}

tar -czf .\agentsprint-docker-deploy.tgz -C .\deploy docker
Get-Item .\agentsprint-docker-deploy.tgz | Select-Object FullName,Length,LastWriteTime
```

## 7. 上传与部署

上传部署包：

```powershell
scp -i <SSH_KEY_PATH> .\agentsprint-docker-deploy.tgz <SSH_USER>@192.168.100.100:/opt/agentsprint-deploy/agentsprint-docker-deploy.tgz
```

执行远端部署：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'bash /opt/agentsprint-deploy/deploy.sh'
```

如果目标服务器还没有 `/opt/agentsprint-deploy/deploy.sh`：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'set -e; cd /opt/agentsprint-deploy; tar -xzf agentsprint-docker-deploy.tgz docker/deploy.sh; cp docker/deploy.sh deploy.sh; chmod 755 deploy.sh docker/deploy.sh; bash /opt/agentsprint-deploy/deploy.sh'
```

部署脚本会自动：

- 备份当前 `/opt/agentsprint-deploy/docker` 到 `docker.bak.<yyyyMMddHHmmss>`。
- 解压新的 `docker` 目录。
- 保留并恢复远端 `.env`。
- 执行 `docker compose config --quiet`。
- 执行 `docker compose build --no-cache`。
- 执行 `docker compose up -d`。
- 做基础 HTTP 健康检查。

## 8. 验证

查看 Compose 服务：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'cd /opt/agentsprint-deploy/docker && docker compose ps'
```

查看 AgentSprint 容器：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'docker container ls -a | grep -E "agentsprint|math-codex-worker"'
```

HTTP 验证：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'curl -I --max-time 10 http://127.0.0.1:5999'
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'curl -i --max-time 10 http://127.0.0.1:5000'
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'curl -i --max-time 10 http://127.0.0.1:5010/mcp'
```

期望结果：

- Admin 返回 `200 OK`。
- API 根路径可返回 `404 Not Found`，只要 Kestrel 有响应即可。
- MCP `/mcp` 返回 `200`，响应中包含 `agentsprint-mcp`。

日志检查：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'docker logs --tail 120 agentsprint-api'
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'docker logs --tail 80 agentsprint-mcp'
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'docker logs --tail 80 agentsprint-admin'
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'docker logs --tail 120 math-codex-worker-1'
```

## 9. Worker 与 Codex 登录

Worker 首次启动后需要确认 Codex 登录态。可在容器内使用 API key 登录：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'docker exec math-codex-worker-1 bash -lc "printenv OPENAI_API_KEY | codex login --with-api-key && codex login status"'
```

验证 Codex 响应：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'docker exec math-codex-worker-1 bash -lc "cd /workspaces && codex exec --skip-git-repo-check --output-last-message /runs/hello-final.md \"你好\" && cat /runs/hello-final.md"'
```

不要让多个 Worker 共享同一个 `/codex-home` 或 `/workspaces`。

## 10. 回滚

查看备份：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'ls -dt /opt/agentsprint-deploy/docker.bak.* | head'
```

回滚到指定备份：

```powershell
ssh -i <SSH_KEY_PATH> <SSH_USER>@192.168.100.100 'set -e; cd /opt/agentsprint-deploy; mv docker docker.failed.$(date +%Y%m%d%H%M%S); cp -a docker.bak.替换为备份时间戳 docker; cd docker; docker compose build --no-cache; docker compose up -d'
```

回滚后重新执行第 8 节验证。

## 11. 注意事项

- PowerShell 中远端 Linux 命令建议使用单引号包裹，避免 `$(date ...)` 被本地提前解析。
- 不要把真实 `.env`、Token、API Key、数据库密码提交到仓库。
- 如果目标服务器是混部服务器，不要执行 `docker system prune -a`、全局删除容器或全局删除卷。
- 前端只更新时，需要先清理远端 `docker/artifacts/admin`，避免旧 hash 文件残留。
- API 刚启动时偶尔会短暂连接重置，等待数秒后重试健康检查。
- Worker 如需支持 Java 项目，应基于 `agentsprint-codex-worker-base:latest` 扩展安装 JDK 与 Maven/Gradle，再构建 Java Worker 基础镜像。
