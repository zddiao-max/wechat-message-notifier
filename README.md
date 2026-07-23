# WeChat Message Notifier

适用于 Windows 微信 4.x 的本地消息提醒器。当前版本：**3.0.0**。

程序从微信公开的 Windows UI Automation 会话列表读取联系人、消息摘要和未读状态，并仅使用 Windows 系统通知横幅和通知中心提醒。

## 通知规则

- 普通联系人和 `Unknown` 会话默认提醒；可按会话单独设为 `Block`。
- 名称含“群”的会话一律视为群聊；群聊默认提醒，可按会话单独设为 `Block`。
- `groupChatBlockKeywords` 匹配群聊名称：命中任一词的已识别群聊强制不提醒，优先于该群的 `Allow` 规则。
- 公众号、订阅号以及被识别为 `OfficialAccount` 的会话默认不提醒；仅当消息摘要命中公众号允许关键词时提醒。
- 服务号默认不提醒；按会话明确设为 `Allow`，或消息摘要命中服务号允许关键词时提醒。
- `serviceAccountAllowKeywords` 是服务号消息摘要的允许关键词：命中任一关键词时推送。
- `officialAccountAllowKeywords` 是公众号消息摘要的允许关键词：公众号仅在命中任一关键词时推送。
- 不再识别或区分“免打扰群聊”。不会读取截图、图像、像素、坐标或铃铛图标。
- 所有分类与规则的键均为匿名 `ContactHash`，不会写入联系人名称或消息正文。

`sessionKindOverrides` 只用于修正“它是什么类型”；`sessionNotificationOverrides` 只用于决定“这个具体会话是否提醒”。两者互不替代。

```json
{
  "sessionKindOverrides": {
    "a1b2c3d4e5f60708": "ServiceAccount",
    "1020304050607080": "GroupChat"
  },
  "sessionNotificationOverrides": {
    "a1b2c3d4e5f60708": "Allow",
    "1020304050607080": "Block"
  },
  "serviceAccountAllowKeywords": [
    "AA"
  ],
  "officialAccountAllowKeywords": [
    "AA",
    "AB"
  ],
  "groupChatBlockKeywords": [
    "团购",
    "福利"
  }
}
```

配置文件在：

```text
%LOCALAPPDATA%\WeChatMessageNotifier\settings.json
```

配置修改会自动热加载。新规则使用 16 位十六进制 `ContactHash`；为兼容旧配置，已有的 8 位哈希仍可读取。保存使用 UTF-8 无 BOM。旧的 `enable*` 全局类型开关会被忽略，并在下次保存时移除。

设置保存采用同目录临时文件原子替换，并保留上一份有效配置为 `settings.json.bak`；主配置损坏时自动回退到该本地备份。微信未运行或未找到会话列表时，扫描频率自动降至 5 秒；已连接并读取到会话列表后恢复为约 1.5 秒。

## 诊断与隐私

双击托盘图标或右键选择“打开设置…”可编辑服务号、公众号提醒关键词和群聊屏蔽关键词。v3.0 使用独立的 WinUI 3 设置与托盘界面：设置窗口优先使用 Windows 11 Mica、Per-Monitor-V2 DPI 和响应式卡片布局；托盘菜单优先使用 Windows 11 Desktop Acrylic，并可在拉宽后自动切换为一列、两列或三列。前景卡片、文字与输入框保持不透明清晰；不使用整窗透明或 `TransparencyKey`。不支持相应系统材质的 Windows 会自动回退为清晰浅色界面。托盘仍可暂停监控、启用隐私模式、发送测试通知、打开 Windows 通知时长设置，或打开高级原始设置文件与日志。

诊断日志只含 `ContactHash`、检测类型、解析后类型、会话级规则、默认策略、结果和原因；不记录联系人名称或消息正文。

程序完全本地运行：不联网、不调用 AI API、不读取或修改微信数据库、不注入微信进程。

## 构建与测试

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

构建产物：

```text
outputs\WeChatMessageNotifier\WeChatMessageNotifier.exe
outputs\WeChatMessageNotifier\WeChatNotifierLauncher.exe
```

运行 `outputs\WeChatMessageNotifier\install-startup.cmd` 后，Windows 登录时只会启动轻量等待器；它通过 Windows 进程启动事件等待 `Weixin.exe` / `WeChat.exe`，检测到微信实际启动后才启动提醒器并退出。不会在后台定时轮询微信。

初始代码与文档由 OpenAI Codex 于 2026-06-29 在项目所有者指示下编写。
