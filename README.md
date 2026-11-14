# AntigravityZh

给 Google **Antigravity**（反重力）桌面版加一层中文界面。不是官方语言包，也不修改 Antigravity 的安装文件。

> 非官方项目，与 Google 无关。Antigravity 更新后界面文案可能变化，欢迎提 PR 补词条。

## 它解决什么问题

Antigravity 目前只有英文界面，而且它的前端（`main.js`）是编译进 `language_server.exe` 的，没有可替换的语言文件，也没有官方语言包机制。这个工具的做法是：

1. 读取 Antigravity 写在 `%APPDATA%\Antigravity\DevToolsActivePort` 里的本机调试端口；
2. 通过 Chrome DevTools Protocol 连上界面窗口；
3. 注入一段翻译脚本，把界面上的英文文案替换成 `dict.json` 里的中文；
4. 常驻后台，Antigravity 重启或新开窗口后自动重连。

对话正文、代码块、终端输出、输入框内容不会被翻译（脚本里有跳过规则）。

## 安装

要求：Windows 10/11 x64，已安装 Antigravity。

1. 从 [Releases](../../releases) 下载 `AntigravityZh-win-x64.zip` 并解压；
2. 双击 `安装.cmd`；
3. 安装脚本会把程序复制到 `%LOCALAPPDATA%\AntigravityZh`，加入开机自启，并启动 Antigravity。

之后正常使用 Antigravity 即可：窗口加载后一两秒内界面会变成中文。开机自启使用当前用户的启动文件夹（`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\AntigravityZh.vbs`），不需要管理员权限。

卸载：双击 `卸载.cmd`（或运行 `AntigravityZh.exe uninstall`），会移除自启、停止后台助手并删除程序文件。

## 命令行

```text
AntigravityZh.exe                 前台运行汉化助手
AntigravityZh.exe --background    后台运行(无窗口)
AntigravityZh.exe install         安装并加入开机自启,同时启动 Antigravity
AntigravityZh.exe install --no-launch
                                  安装但不自动启动 Antigravity
AntigravityZh.exe uninstall       移除自启并停止
AntigravityZh.exe status          查看端口、连接数、词典条数、自启状态
AntigravityZh.exe launch          启动 Antigravity
```

## 补翻译 / 自定义

安装后所有可编辑文件都在 `%LOCALAPPDATA%\AntigravityZh`：

| 文件 | 用途 |
| --- | --- |
| `dict.json` | 英中词典。加 `"English": "中文"` 即可，保存后自动热重载 |
| `inject.js` | 页面内翻译脚本。动态文案（带数字、路径、时间）在这里加正则 |
| `missing.json` | 自动收集界面上还没翻译到的英文，用来补词条 |
| `helper.log` | 运行日志 |
| `status.json` | 当前端口、已连接窗口数、词典条数 |

`missing.json` 里会混入项目名、文件名、模型名、插件 ID 等**不该翻译**的内容，补词条时跳过即可。

## 已知限制

- Antigravity 的桌面端调试端口目前是默认开启的（它自己的 language server 也在用）。如果未来 Google 关掉这个端口，本工具需要改方案。
- 词典覆盖的是高频界面文案，长尾文案会保持英文，可以随时补。
- 原生系统菜单（如果显示）可能仍是英文；界面内的菜单、设置、弹窗都已覆盖。
- exe 未签名，首次运行时 Windows SmartScreen 可能提示"未知发布者"。代码全部开源，可自行编译。
- 后台助手会读取本机 `127.0.0.1` 上的 Antigravity 调试端口，不联网、不收集数据、不写 Antigravity 安装目录。

## 从源码构建

需要 .NET 10 SDK（Windows）：

```powershell
dotnet publish src/AntigravityZh/AntigravityZh.csproj -c Release -r win-x64 -p:PublishAot=true -o dist
```

产物是单个 `dist/AntigravityZh.exe`（约 5 MB），不需要目标机器安装 .NET。

## English

**AntigravityZh** is an unofficial Chinese localization layer for Google Antigravity on Windows.

It reads Antigravity's local DevTools port from `%APPDATA%\Antigravity\DevToolsActivePort`, attaches over CDP, and injects a small translation script into the UI. It never modifies Antigravity's installation files. The helper stays in the background and reconnects after app restarts.

Install: download the release zip, run `安装.cmd` (install), or run `AntigravityZh.exe install`. Uninstall with `卸载.cmd` / `AntigravityZh.exe uninstall`. The dictionary lives at `%LOCALAPPDATA%\AntigravityZh\dict.json` and hot-reloads on save.

Requires Windows 10/11 x64. Not affiliated with Google. MIT licensed.

## License

MIT
