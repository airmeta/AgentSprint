const DEFAULT_AGENTSPRINT_MCP_ENDPOINT = 'http://192.168.80.101:5010/mcp';
const TOKEN_PLACEHOLDER = 'Bearer <这里填令牌>';

function escapeTomlString(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
}

function resolveMcpEndpoint(content?: string) {
  const endpoint = content?.match(/^\s*url\s*=\s*"([^"]+)"/m)?.[1]?.trim();
  return endpoint || DEFAULT_AGENTSPRINT_MCP_ENDPOINT;
}

export function buildAgentsprintMcpSetupPrompt(content?: string) {
  const mcpEndpoint = escapeTomlString(resolveMcpEndpoint(content));

  return `你现在位于 AgentSprint 项目工作区，请只完成 Codex 的 agentsprint MCP 接入配置，不修改项目代码。

目标：
将 agentsprint 远程 HTTP MCP 配置到 Codex，使后续任务可以通过 MCP 自动拉取任务上下文并推进。

请按下面流程执行：
1. 检查当前项目工作区是否为 AgentSprint 项目。
2. 检查 \`~/.codex/config.toml\` 中是否已有 \`[mcp_servers.agentsprint]\`。
3. 如果不存在则新增；如果已存在则更新为下面配置。
4. 保留现有其他 MCP 配置，不要覆盖 \`node_repl\` 等已有配置。
5. 默认只配置 MCP endpoint 和 Authorization，不要默认写入 \`X-AgentSprint-Api-Base-Url\`。
6. 只有在用户明确提供“远程 MCP 服务可访问的 AgentSprint API 地址”时，才写入 \`X-AgentSprint-Api-Base-Url\`。
7. 不要把 \`http://localhost:5000\` 固定写入 \`X-AgentSprint-Api-Base-Url\`。因为这里的 localhost 对远程 MCP 服务来说通常表示 MCP 服务所在机器，不一定是当前 Codex 开发机。
8. Codex HTTP MCP 请求头必须使用 \`http_headers\` 字段，不要使用 \`[mcp_servers.agentsprint.headers]\` 子表。
9. 配置完成后，验证 Codex 是否能识别 agentsprint MCP；如果需要新对话或重启 Codex 才能生效，请明确告诉我。

需要写入的 Codex TOML 配置为：

\`\`\`toml
[mcp_servers.agentsprint]
url = "${mcpEndpoint}"
http_headers = { Authorization = "${TOKEN_PLACEHOLDER}" }
\`\`\`

可选覆盖配置：
仅当用户明确提供远程 MCP 服务可访问的 AgentSprint API 地址时，才追加到 \`http_headers\`：

\`\`\`toml
http_headers = {
  Authorization = "${TOKEN_PLACEHOLDER}",
  "X-AgentSprint-Api-Base-Url" = "<远程 MCP 服务可访问的 AgentSprint API 地址>"
}
\`\`\`

如果当前 Codex 版本不支持 HTTP MCP 的 \`http_headers\` 字段，请不要继续猜测配置方式，直接说明阻塞点。`;
}

export function buildAgentsprintMcpSetupPromptWithToken(content: string, bearerToken: string) {
  return buildAgentsprintMcpSetupPrompt(content)
    .replaceAll(TOKEN_PLACEHOLDER, bearerToken)
    .replace('Bearer <AgentSprint Agent Token>', bearerToken);
}
