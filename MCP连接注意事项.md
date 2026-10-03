# Unity MCP 连接注意事项（mcp-for-unity v10.1.0）

> 本文档整理自 箱庭侵蚀（WJGamejam）项目的实战经验，适用于把 MCP 接到任意 Unity 项目。

## 1. 架构：三部分

```
AI 客户端 (DSH/Claude 等)
   ↓ MCP 协议 (streamable HTTP)
Python MCP 服务器 (mcp-for-unity, 跑在 127.0.0.1:8081)
   ↓ WebSocket /hub/plugin
Unity 编辑器插件 (com.coplaydev.unity-mcp, 挂在你打开的项目上)
```

## 2. 安装（Unity 端，UPM git 引用）

在目标项目的 `Packages/manifest.json` 加：

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"
```

服务器本体是 Python 包（`mcpforunityserver`，用 `uv` 安装），由 Unity 插件自动拉起，**无需手动启动**。
服务器进程信息写在 `Library/MCPForUnity/RunState/mcp_http_8081.pid`，日志在 `Library/MCPForUnity/Logs/server-launch-8081.log`。

## 3. 客户端连接配置（DSH 示例）

```yaml
- id: mcp-unity
  name: '@deepseek-ai/dsh-mcp-client'
  config:
    serverName: unity
    transport: streamable-http
    url: http://127.0.0.1:8081/mcp
    reconnect:
      enabled: true
      initialDelayMs: 500
      maxDelayMs: 30000
      maxAttempts: 24
```

## 4. ⚠️ 核心注意事项（血泪经验）

| 坑 | 现象 | 解决 |
|---|---|---|
| **端口 8081 被占用** | console 报 `通常每个套接字地址只允许使用一次` | 关掉占用进程或改端口；Unity 插件默认 8081 |
| **Unity 编译/域重载后工具失效** | AI 端工具变成不可用 | 服务器随 Unity 重启，客户端 reconnect 预算会耗尽 → 工具被注销，**需要重连** |
| **重连预算耗尽** | 工具消失，普通重试无效 | DSH 里**编辑一次配置文件**（如 `cordis.patch.yml` 的 `maxAttempts` 改个数字）触发 HMR 重载，重新建立会话。注意：这个坑**只发生在 AI 客户端侧**；Unity 插件的 WS 客户端初始重连计划耗尽后会**每 30s 无限重试**，永不放弃 |
| **服务器进程死掉（Unity 还开着）** | 8081 无监听；服务器日志无报错、戛然而止（常见于休眠/重启后） | **不用动 Unity**：手动 uvx 重拉服务器（命令见 §6，在项目根目录执行），约 30s 内插件自动回连，日志重新出现 `Plugin registered` |
| **会话不跨项目** | 换了 Unity 项目连接失败 | 服务器是 **project-scoped** 的，每个项目有自己的 pidfile/实例 token；工具只作用于当前打开的 Unity 项目 |
| **插件未加载** | `Plugin registered` 日志没有 | 确认 Unity 编辑器开着对应项目，且 MCPForUnity 菜单里插件服务已启用 |

## 5. 验证是否连上

- 服务器日志出现 `Plugin registered: <项目名> (<实例ID>)` + `Registered N tools for session ...` = Unity 端正常
- 客户端能调 `mcp__unity__*` 工具 = 链路通
- 参考项目（WJGamejam）当前启用 35 个工具，分 10 组：`core / ui / animation / asset_gen / docs / probuilder / profiling / scripting_ext / testing / vfx`

## 6. 给"另一个聊天"的速查

- 服务器 URL：`http://127.0.0.1:8081/mcp`（streamable HTTP）
- 工具命名：`mcp__unity__<工具名>`
- Unity 项目必须**打开着**，否则服务器在但插件 session 断（`disconnected (1005)`）
- 改配置触发重连是 DSH 特有的恢复手段；其他客户端重启客户端进程即可

## 7. 手动重拉服务器（Unity 开着、服务器死了时）

在**项目根目录**执行（版本号对齐插件版本，插件版本看 `Library/PackageCache/com.coplaydev.unity-mcp@*/package.json`，本项目当前是 10.1.2）：

```bash
uvx --from mcpforunityserver==10.1.2 mcp-for-unity --transport http --http-url http://127.0.0.1:8081 --project-scoped-tools
```

执行后不用碰 Unity——插件每 30s 无限重试，约半分钟内自动回连。这是插件 `ServerCommandBuilder` 生成的同款命令，等价于 Unity 界面里点 Start Server（区别只是进程托管方：想让插件托管进程就去窗口里点一次 Start）。

## 8. ZCode 客户端配置（streamable HTTP）

工作区级（推荐，服务器本身就是 project-scoped 的）：`<项目根>/.zcode/config.json`

```json
{
  "mcp": {
    "servers": {
      "unity": {
        "type": "http",
        "url": "http://127.0.0.1:8081/mcp",
        "timeoutMs": 60000
      }
    }
  }
}
```

字段从严：`type: "http"` + `url`，多写未知字段整个服务器会被丢弃。写完**重启会话**（新开对话即可），所有作用域的 MCP 服务器都会自动连接。
