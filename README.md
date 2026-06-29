# WeChat Message Notifier

一个适用于 Windows 微信 4.x 的本地消息提醒器。

微信处于后台时，程序从微信公开的 Windows UI Automation 会话列表中读取联系人、消息摘要和时间，并在当前屏幕右下角显示持久提醒。提醒不会自动消失；点击提醒打开微信后才会关闭。

> 初始代码与文档由 **OpenAI Codex** 于 2026-06-29 根据项目所有者的需求编写，并在实际 Windows 微信 4.1.11 环境中完成测试。

## 功能

- 完全本地运行，不联网、不调用 AI API、Token 消耗为零。
- 不读取或修改微信数据库，不注入微信进程。
- 微信在后台收到新消息时显示联系人和消息摘要。
- 微信处于前台时默认抑制重复提醒。
- 提醒持久显示，点击后打开微信并关闭提醒。
- 同一联系人连续发消息时更新已有提醒。
- QQ 或系统通知遮挡时，微信提醒自动上浮；遮挡消失后回落。
- 打开微信时保留原窗口状态，最大化窗口不会被还原为普通窗口。
- 支持隐私模式，只显示联系人，不显示消息内容。
- 支持托盘暂停、测试通知和查看日志。
- 支持当前用户登录 Windows 后自动启动。

## 隐私设计

- 消息正文和联系人不会写入日志。
- 日志只记录时间、程序状态、字段长度和错误类型。
- 程序不会发送微信消息。
- 程序不会访问网络。

默认日志位置：

```text
%LOCALAPPDATA%\WeChatMessageNotifier\app.log
```

## 系统要求

- Windows 10 或 Windows 11。
- Windows 微信 4.x。
- .NET Framework 4.x（现代 Windows 通常已内置）。

当前已验证环境：

- Windows 11
- 微信 4.1.11.24

## 构建

项目有意不依赖 NuGet 包，可直接使用 Windows 自带的 .NET Framework C# 编译器。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

生成文件：

```text
outputs\WeChatMessageNotifier\WeChatMessageNotifier.exe
```

## 测试

运行解析、去重和过滤测试：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

对当前运行的微信执行只读集成检查：

```powershell
.\work\tests\WeChatMessageNotifier.Tests.exe --integration-test
```

集成测试只输出识别到的会话数量，不输出联系人或消息内容。

## 使用

1. 保持微信已登录。
2. 运行 `WeChatMessageNotifier.exe`。
3. 第一次扫描只建立基线，不会把已有消息全部弹出。
4. 右键托盘图标可暂停监控、启用隐私模式、测试通知或退出。
5. 点击消息提醒可打开微信。

命令行参数：

```text
--privacy            启动时只显示联系人。
--notify-foreground  微信在前台时也显示提醒。
--self-test          运行内置测试并退出。
--integration-test   只读检查微信会话列表并退出。
```

## 工作原理

1. 枚举 `Weixin.exe` 的顶层 Qt 窗口。
2. 通过 Windows UI Automation 找到包含多行项目的会话列表。
3. 从每个列表项解析联系人、摘要和时间。
4. 使用内容签名建立初始基线并过滤重复变化。
5. 使用不抢焦点的置顶 WinForms 窗口显示提醒。
6. 检测右下角的其他置顶窗口，动态调整提醒位置。

## 已知限制

- 只能识别微信当前已加载到会话列表中的项目。
- 微信大版本更新若改变辅助功能结构，解析规则可能需要更新。
- 微信未运行、未登录或完全退出时无法监控。
- 图片、语音等消息只显示微信会话列表提供的摘要。
- 相同显示名称的联系人可能被视作同一个会话。

## 项目结构

```text
src/WeChatMessageNotifier/       主程序
src/WeChatNotifier.Probe/        脱敏结构探针
build.ps1                        构建脚本
test.ps1                         测试脚本
build-probe.ps1                  探针构建脚本
outputs/                         本地构建产物
```

## 署名

本项目初始实现由 **OpenAI Codex** 编写，项目所有者负责提出需求、授权本机测试并确认交互行为。

## 许可证

本仓库目前没有附加开源许可证。除非项目所有者后续添加许可证，否则默认保留全部权利，不代表允许复制、修改或再分发。
