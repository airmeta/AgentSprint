# API 配置说明

本目录保存 AgentSprint API 服务部署配置样例。真实部署时，敏感值写入远端 `/opt/agentsprint-deploy/docker/.env`，不要提交到仓库。

## 文件说明

| 文件 | 用途 |
| --- | --- |
| `.env.example` | API 依赖的环境变量样例 |
| `appsettings.Production.example.json` | API 生产配置参考 |
| `docker-compose.api.example.yml` | API Compose 片段 |

## 必需配置

- `AGENTSPRINT_DB_PASSWORD`：MySQL root 密码。
- `AGENTSPRINT_REDIS_CONNECTION_STRING`：Redis 地址，默认 `192.168.100.100:6379`。
- `AGENTSPRINT_REDIS_USERNAME`：Redis 用户名，没有可留空。
- `AGENTSPRINT_REDIS_PASSWORD`：Redis 密码，没有可留空。

API 容器使用 `Database__AutoInitialize=true`，启动时会自动初始化 `agentsprint` 数据库结构。
