# Unity MCP 对接 DeepSeek Harness 部署指南

> 目标：在一台**全新的 Windows 电脑**上，从零部署：
> **Unity Editor + MCP for Unity + uv/uvx + DeepSeek Harness Web GUI**
> 并让 DSH 通过 MCP 直接控制 Unity Editor。
>
> 本文以 Windows 为主；macOS / Linux 的差异见文末表格。
> 当前验证环境：Unity 2022.3.62f3c1、MCP for Unity 10.3.0、DSH Web profile。

---

## 一、最终结构

```text
Unity Editor
  └─ MCP for Unity 插件（Editor 内）
       ├─ Unity Bridge（Editor 内部的 WebSocket 会话）
       └─ Local HTTP MCP server（uvx 启动）
              http://127.0.0.1:8080/mcp
                       ▲
                       │ streamable-http
                       │
DeepSeek Harness ── dsh-mcp-client ──> 模型工具 mcp__unity__*
```

- DSH 只连本地 HTTP，不负责启动 Unity 侧 server；
- Unity 插件负责启动/停止 local HTTP server；
- Unity 必须开着，MCP 工具才可用；
- DSH 的 `mcp-unity` 配置放在 **web profile** 的 `cordis.patch.yml`。

---

## 二、准备清单

| 软件 | 版本/说明 |
| --- | --- |
| Unity | 推荐 2022.3.62f3c1（与当前项目一致）；MCP for Unity 要求 2021.3+ |
| Unity Hub | 安装匹配的 Editor |
| Git | Unity Package Manager 拉 Git URL 需要 |
| Node.js | LTS，带 npm |
| DeepSeek Harness | 当前 `@deepseek-ai/dsh` 版本；含 `dsh-mcp-client` |
| uv / uvx | 最新版，供 Unity 插件启动 MCP server |
| MCP for Unity | 10.3.0（当前验证版本） |
| 防火墙/端口 | 本地 127.0.0.1:8080（可改） |

> DSH 本身不需要额外安装 MCP 插件；`@deepseek-ai/dsh-mcp-client` 是 DSH 自带的包，只需要写配置。

---

## 三、安装 Node.js + DeepSeek Harness

1. 安装 Node.js LTS，确认：

```powershell
node --version
npm --version
```

2. 安装 DSH：

```powershell
npm install -g @deepseek-ai/dsh
dsh --version
```

3. 先启动一次 Web GUI，让 profile 初始化：

```powershell
dsh web
```

默认在 `http://127.0.0.1:3080`。浏览器打开并完成模型登录/配置。
第一次启动会生成：

```text
%USERPROFILE%\.dsh\profiles\web\cordis.patch.yml
```

4. 先关闭这个测试 DSH 进程，或保持运行；后面改配置会自动 live reload。

---

## 四、安装 uv / uvx

Unity 插件的 local HTTP server 由 `uvx` 启动，所以 uv 必须先可用。

Windows：

```powershell
winget install --id=astral-sh.uv -e
```

或：

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
```

验证：

```powershell
uvx --version
Get-Command uvx
```

记下 `uvx.exe` 的实际路径（例如 `D:\Software\uvpython\bin\uvx.exe`），
如果 Unity 插件找不到 uvx，需要在插件的 **Advanced Settings → UVX Path** 里 Browse 选择。

> 第一次 Start Server 时会下载 Python 依赖 `mcpforunityserver`，可能等几十秒；这是正常现象。

---

## 五、安装 Unity + MCP for Unity

### 5.1 已有项目

直接打开项目；`Packages/manifest.json` 里应已有：

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"
```

### 5.2 全新 Unity 项目

1. 用 Unity Hub 创建/打开项目；
2. `Window → Package Manager`；
3. 左上角 `+ → Add package from git URL`；
4. 输入：

```text
https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
```

5. 等待导入和编译完成；
6. 菜单出现 `Window → MCP for Unity`。

> 当前验证版本是 **10.3.0**。如果要可复现，建议把 `#main` 换成你验证过的 tag/commit；
> 当前机器缓存对应的 commit 是 `81542a7e86`。
>
> 换版本后，Unity 侧 server 版本会跟着变；如果协议有变化，DSH 的 URL 可能仍兼容，但要以插件窗口的 HTTP URL 为准。

---

## 六、配置 Unity 侧 MCP

打开：

```text
Window → MCP for Unity
```

### 6.1 Server 区域

| 设置 | 值 |
| --- | --- |
| Transport | **HTTP Local** |
| HTTP URL | `http://127.0.0.1:8080`（默认；base URL 不带 `/mcp`） |
| API Key | 留空（本地） |
| Local Server | 先点击 **Start Server** |
| 连接按钮 | 再点击 **Start**（按钮在连接状态右侧） |

等待：

- Local Server 显示运行；
- 连接状态显示 **Connected**；
- Console 出现 `Server ready on http://127.0.0.1:8080` 和 `Session connected`。

> 如果 8080 被占用，把 HTTP URL 改成例如 `http://127.0.0.1:8081`，
> Start Server / Start 后，同步改 DSH 配置里的 URL。

### 6.2 Advanced Settings

打开 `Advanced Settings`：

- **Auto-Start Server on Editor Load** → **ON**
- **UVX Path** → 如果自动检测不到，Browse 到 `uvx.exe`
- 不建议开启 **Allow LAN Bind**（保持本机 127.0.0.1）

开启 Auto-Start 后，下次打开 Unity 会自动启动 server + 连接 bridge。

### 6.3 Tools 区域

打开 `Tools`：

- **Project-Scoped Tools** → **OFF**

原因：

- 开启时 Unity 只向客户端暴露一个 `execute_custom_tool` 路由器；
- 关闭后 DSH 才能看到原生 36 个工具，例如：
  `mcp__unity__manage_scene`、`mcp__unity__manage_gameobject`、
  `mcp__unity__read_console`、`mcp__unity__run_tests`。

切换这个开关后，需要 **Stop Server / Start Server** 再重新连接，新的工具列表才生效。

### 6.4 MCP Client Configuration

**不需要配置**。

DeepSeek Harness 不在插件的客户端下拉列表里；DSH 是外部 MCP 客户端，
我们直接在 DSH 的 profile 里写配置即可。不要为了 DSH 去点 Cursor/Claude 的 Auto Configure。

---

## 七、配置 DSH 的 MCP 客户端

先明确一个限制：

> **DSH 目前不会自动读取项目根目录的 `.dsh/` 作为 profile 配置。**
> `<project>/.dsh` 这个目录本身没有特殊含义，除非你通过 `--patch` 显式加载，
> 或者把 `DSH_HOME` 整个指到它。
>
> （`AGENTS.md` 是项目级自动加载的；但 MCP/profile 配置不是。）

有三种放法，推荐 **7.1 项目级 `--patch`**。

### 7.1 项目级（推荐）：`.dsh/unity-mcp.yml` + `--patch`

只把 **Unity MCP 这一段**放到项目里，DSH 的模型、账号、会话仍使用全局 `~/.dsh`。

项目根目录创建：

```text
<project>/.dsh/unity-mcp.yml
```

内容：

```yaml
- insert:
    - id: mcp-unity
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: unity
        transport: streamable-http
        url: 'http://127.0.0.1:8080/mcp'
        toolCallTimeoutMs: 180000
        failOnStartupError: false
        reconnect:
          enabled: true
          initialDelayMs: 1000
          maxDelayMs: 30000
          maxAttempts: 10
```

从**项目根目录**启动 DSH，并显式加载这个 patch：

```powershell
cd E:\Projects\SurvivorArenaU
dsh web --patch .dsh/unity-mcp.yml
```

> `--patch` 是 DSH 启动参数，放在 profile 名 `web` 后面；可重复使用多个 patch 文件。
> 我已在本机用 `dsh web --patch .dsh/unity-mcp.yml --dump-config` 验证过这种写法。

验证合成后的配置：

```powershell
dsh --profile web --patch .dsh/unity-mcp.yml --dump-config |
  Select-String mcp-unity
```

**重要**：

- 如果全局 `%USERPROFILE%\.dsh\profiles\web\cordis.patch.yml` 里已经有 `mcp-unity`，先删掉那一段；
- 否则同一个 `id: mcp-unity` 会被重复插入，启动时报重复/覆盖错误；
- 当前正在运行的 `dsh web` 不会因为创建 `.dsh` 文件而自动读取它；
  必须**停止旧进程，从项目根目录用 `--patch` 重新启动**；
- `--patch` 是启动期 overlay，修改 `.dsh/unity-mcp.yml` 后建议重启 DSH；
- 这个文件没有密钥，可以提交到 Git；如果只放这个文件，`.dsh/` 不必整体忽略。

可以加一个项目启动脚本 `start-dsh.ps1`：

```powershell
Set-Location $PSScriptRoot
dsh web --patch .dsh/unity-mcp.yml
```

这样以后从项目根目录运行脚本，就一定带上项目级 MCP 配置。

### 7.2 全局配置（当前机器使用的方式）

编辑：

```text
%USERPROFILE%\.dsh\profiles\web\cordis.patch.yml
```

追加同一段 `- insert:` 配置：

```yaml
# Unity Editor MCP bridge (CoplayDev "MCP for Unity" 10.x)
- insert:
    - id: mcp-unity
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: unity
        transport: streamable-http
        url: 'http://127.0.0.1:8080/mcp'
        toolCallTimeoutMs: 180000
        failOnStartupError: false
        reconnect:
          enabled: true
          initialDelayMs: 1000
          maxDelayMs: 30000
          maxAttempts: 10
```

注意：

- 必须是 `- insert:` **顶层 patch 项**；
- 直接写 `- id: mcp-unity` 会被 DSH 报 `entry "mcp-unity" not found`；
- URL 要带 `/mcp`，base URL 是 8080；
- 如果改了 Unity 端口，这里同步改；
- 文件是 YAML，缩进必须一致；
- 修改前备份：

```powershell
Copy-Item "$env:USERPROFILE\.dsh\profiles\web\cordis.patch.yml" `
          "$env:USERPROFILE\.dsh\profiles\web\cordis.patch.yml.bak"
```

保存后：

- Web profile 默认 `patchReload: live`，会自动重载；
- 如果没生效，重启 `dsh web`；
- 建议**新开一个 DSH 会话**，确保拿到新工具列表。

### 7.3 完整项目级 DSH Home（可选，隔离最彻底）

如果你希望模型设置、账号、会话、MCP 全部按项目隔离，可以把整个
`DSH_HOME` 指到项目内：

```powershell
cd E:\Projects\SurvivorArenaU
$env:DSH_HOME = (Join-Path (Get-Location) '.dsh')
dsh web
```

此时目录结构是：

```text
<project>/.dsh/
  profiles/web/cordis.patch.yml   ← 在这里写 MCP 配置
  sessions/
  cache/
  credentials.yaml                ← 不要提交
```

- 这和 7.1 的 `.dsh/unity-mcp.yml` 是**两种不同用法**，不要混用；
- 首次启动会初始化一套新的 profile，模型/账号要重新配置；
- 适合“一个项目一套独立 DSH 状态”；
- `.gitignore` 至少忽略 `.dsh/sessions/`、`.dsh/cache/`、`.dsh/credentials.yaml`；
- 如果只想隔离 Unity MCP，不想重新登录/配置模型，用 7.1 更轻。

### 7.4 多项目注意

- 每个项目可以有自己的 `.dsh/unity-mcp.yml`；
- 但 Unity MCP 的本地 HTTP server 默认只有一个 8080 端口；
  同时开多个 Unity 项目时，需要给不同项目改端口，DSH 侧对应改 `url`；
- 一个 DSH Web 进程通常对应一个项目配置；多项目建议分别启动，或手动切换 patch。

---

## 八、验证

### Unity 侧

```text
Window → MCP for Unity
Server: Connected
```

Windows 检查端口：

```powershell
Get-NetTCPConnection -LocalPort 8080 -State Listen
```

### DSH 侧

在新会话里让 DSH 读取：

```text
mcpforunity://instances
```

应看到类似：

```json
{
  "success": true,
  "transport": "http",
  "instances": [
    { "name": "YourProject", "unity_version": "2022.3.62f3c1" }
  ]
}
```

再让它调用：

```text
read_console
```

能返回 Unity Console 就说明打通了。此时工具名应类似：

```text
mcp__unity__read_console
mcp__unity__manage_scene
mcp__unity__manage_gameobject
mcp__unity__run_tests
```

---

## 九、日常启动顺序

推荐顺序：

1. 打开 Unity 项目，等待编译完成；
2. 确认 `Window → MCP for Unity` 显示 **Connected**；
3. 启动 `dsh web`；
4. 在 DSH 新会话里使用 Unity 工具。

如果 DSH 先启动：

- DSH 配置里 `failOnStartupError: false`，不会因为 Unity 没开而崩；
- Unity 启动 server 后，如果 DSH 没自动恢复，新开一个 DSH 会话/重载配置即可。

开发时 Unity 可以**最小化/失焦**，不需要保持焦点；但不要关闭。

---

## 十、常见问题

| 现象 | 处理 |
| --- | --- |
| DSH 只看到 `execute_custom_tool` | Unity Tools 里 **Project-Scoped Tools** 还开着；关掉 → Stop/Start Server → 新会话 |
| `Session not found` | Unity 的 MCP server 重启过，DSH HTTP 会话过期；新开 DSH 会话，或重载 DSH 配置 |
| `Connection refused` / 工具全失败 | Unity 没开，或 Local Server 没启动；Window → MCP for Unity → Start Server + Start |
| `uvx not found` | 安装 uv；或 Advanced Settings → UVX Path 选择 uvx.exe |
| Start Server 很久没反应 | 首次 uvx 下载依赖；看 Unity Console；检查网络/uvx 路径 |
| 8080 被占用 | Unity 改 HTTP URL 端口；DSH 配置同步改；重启 server |
| 编译/域重载后工具断 | 等编译完成；必要时新开 DSH 会话 |
| DSH 配置报 `entry "mcp-unity" not found` | 忘了用 `- insert:` 包裹；按第七节改 |
| Unity 开着但跑 `-batchmode` 构建失败 | 同一项目不能同时被 Editor 和 batchmode 打开；用 MCP 的 `manage_build`/`run_tests`，或关闭 Editor 再批处理 |
| 想开 LAN/远程 | 不要开启 Allow LAN Bind；本地 127.0.0.1 足够，避免暴露 8080 |

---

## 十一、版本与安全注意事项

- **版本固定**：正式部署建议固定 Unity 插件 tag/commit；否则 `#main` 更新后接口/端口行为可能变化。
- **端口**：默认 8080；只绑定 127.0.0.1。
- **安全**：本地 HTTP 没有额外认证；任何本机进程都能连。不要开 LAN Bind，不要把 8080 暴露到公网。
- **权限**：DSH 当前可用 `danger-full-access`；Unity MCP 工具能直接改场景/预制体/资产。操作前提交 Git，重要场景先备份。
- **MCP 不是万能的**：批量代码修改、重构、测试仍建议走文件工具 + Git + 可复现的 Editor 脚本；MCP 更适合实时查看/操作 Unity。

---

## 十二、macOS / Linux 差异

| 项 | Windows | macOS / Linux |
| --- | --- | --- |
| DSH profile | `%USERPROFILE%\.dsh\profiles\web\cordis.patch.yml` | `~/.dsh/profiles/web/cordis.patch.yml` |
| uv 安装 | winget / install.ps1 | `brew install uv` 或 `curl -LsSf https://astral.sh/uv/install.sh \| sh` |
| Unity 路径 | Unity Hub 安装目录 | `/Applications/Unity/Hub/Editor/...` 等 |
| 端口检查 | `Get-NetTCPConnection -LocalPort 8080` | `lsof -i :8080` |
| YAML 路径 | 反斜杠注意引号 | 正斜杠 |

其他步骤（Unity 插件、HTTP transport、DSH YAML）完全一致。
