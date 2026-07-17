# Changelog

## 2.6.0 - 2026-07-17

- Removed the legacy custom-popup, animation, acrylic/glass, DPI-popup, and system-panel-avoidance source files, their settings, and their self-tests. Windows system notifications are the only supported reminder implementation.
- Replaced global six-type switches with session-level notification rules: groups, direct contacts, and unknown sessions default to notify and can be set to `Block`.
- Removed the retired mute-specific group type and all mute-label classification. The monitor does not inspect screenshots, icons, pixels, coordinates, or crossed-bell geometry.
- Added `sessionNotificationOverrides` alongside `sessionKindOverrides`, and expanded newly generated anonymous contact hashes to 16 hexadecimal characters.
- Added summary-keyword allowlists: matching service-account and official-account summaries can notify; service accounts remain blocked when neither their session nor a keyword allows them.
- Added group-name block keywords that override a per-session group allow rule.

## 2.5.1 - 2026-07-15

- Removed the legacy custom-popup, animation, glass, and system-panel-avoidance paths from the running application. Windows system notifications are now the only delivery path.
- The tray menu no longer exposes custom-popup display, motion, or visual-effect controls.
- Clear a session's aggregated notification count and matching Windows Notification Center history only after WeChat UI Automation confirms that the intended conversation was selected. The next message starts again at one.
- Reduced tray-menu stalls by calculating the WeChat page kind once per polling pass.

## 2.5.0 - 2026-07-10

- 默认提醒方式改为 Windows 系统通知横幅：新微信消息直接使用系统 toast 弹出并进入 Windows 通知中心，通知样式、动画、圆角、阴影和毛玻璃交由 Windows 11 处理。
- 保留自定义 WinForms 弹窗作为可选模式，但默认不再创建 `PopupHostForm` / `PopupCardControl`，也不再运行自定义弹窗动画和遮挡避让 Timer。
- 托盘新增“提醒方式”菜单，可在“Windows 系统通知”和“自定义弹窗”之间切换；动画和外观菜单在系统通知模式下灰显并注明仅影响自定义弹窗。
- Windows 系统通知模式下，同一会话使用稳定 tag/group 聚合，连续消息更新同一条联系人通知并显示累计新消息数量。
- 点击 Windows 系统通知时通过本地协议携带会话标识，唤醒已有提醒器实例并尽量打开微信目标会话，避免重复启动多个提醒器。
- 托盘“发送测试通知”改为发送 Windows 系统通知横幅；新增“打开 Windows 通知时长设置”，便于调整系统全局横幅停留时间。
- 保留自己发送消息过滤、selected 前台会话保护、未读数优先判断、提醒类型过滤、隐私模式、去重冷却、日志脱敏和本地只读设计。

- 使用单一 `AnimationClock` 统一驱动卡片进入、退出、同联系人更新反馈、卡片重排和遮挡避让动画；动画稳定后自动停止 60 FPS 定时器。
- 卡片进入时短距离滑入、淡入并轻微缩放，退出动画完成后才移除；同联系人更新复用原卡片并使用轻微淡入和 scale pulse。
- 为遮挡避让增加连续采样确认，减少系统通知或托盘面板边界变化造成的上下抖动。
- 将遮挡检测与宿主窗口移动彻底分离：检测到遮挡立即更新目标，连续 3 次未检测到才允许回落；动画保留速度并限制单帧位移，消除托盘展开时的阶梯式跳动。
- 修复新增卡片时短暂以最终外观加入可见宿主、以及宿主增高先于旧卡片位置补偿而导致的进入动画缺失和重排瞬移；卡片现在先应用 Entering 状态，宿主保持底边一次性扩展，旧卡片通过 Moving 状态平滑上移。
- 重新调校卡片动画体感：进入偏移增至 32px 并使用更柔和的独立弹簧；128px 重排限制为每帧最多 14px；透明度改为单调 ease-out 并将缓存精度从 21 级提升到 65 级。
- 将同联系人更新反馈收敛为 1.015 倍轻微 pulse 和 0.94 起始透明度，避免整张卡片明显闪烁。
- 增加 PopupManager 级无窗口流程测试，覆盖首次 Show、第二会话重排、同会话更新、动画 Timer 启停和 Standard/Reduced/Off 行为。
- 托盘新增“动画效果”菜单，支持标准动画、减弱动画和关闭动画，并将 `motionMode` 保存到 `settings.json`。
- 自绘卡片缓存字体、透明度矩阵和静态位图，动画帧只更新位置、透明度与缩放状态，不执行 UIA 扫描、窗口枚举或日志写入。

- 将多个独立 TOPMOST 弹窗重构为一个非激活置顶宿主窗口和多个联系人卡片，消除窗口层级竞争与闪烁。
- 使用 UI Automation 的稳定会话标识聚合同一联系人提醒，后续消息更新最新摘要、累计“新消息”数量并重置 2 分钟计时。
- 将 60 FPS 动画定时器改为仅在位置实际变化时运行；遮挡检测保持 250 毫秒低频运行，最后一张卡片关闭后全部停止。
- 隐私模式下聚合卡片只显示“收到 N 条新消息”，并在切换隐私模式时立即隐藏已有卡片正文。
- 扩展本人发送摘要识别，支持“我/You”冒号前缀、常见附件和语音/视频通话摘要；不再无证据强过滤“你:”。
- 未读数在相邻扫描均可用时，只有未读数增加才产生通知；本人发送导致的摘要变化只更新基线。
- 从 UIA `SelectionItemPattern` 读取当前 selected 会话；微信前台时，selected 会话没有未读增加的纯摘要变化不再提醒。
- 为普通联系人、公众号、服务号、普通群聊、免打扰群聊和未知类型增加独立托盘开关，默认全部开启。
- 类型开关保存到 `%LOCALAPPDATA%\WeChatMessageNotifier\settings.json`；损坏时安全回退默认值。
- 修复无类型证据的会话被默认归为普通联系人、导致公众号和免打扰群开关失效的问题；现在无证据时归为 `Unknown`。
- 增加一次性分类诊断日志，记录会话哈希、类型、静音/selected/未读及标签布尔值，不记录联系人或正文。
- `settings.json` 支持按会话哈希配置 `sessionKindOverrides`，文件修改后自动热加载。

## 2.0.0 - 2026-07-02

- 修复多个置顶提醒在 60 FPS 动画期间反复争夺窗口层级而导致第二条提醒闪烁的问题。
- 增加自定义弹窗与 Windows 通知中心双通道：正常时静默保存历史，自定义弹窗失败时显示系统横幅。
- 支持点击通知中心记录打开微信；自定义弹窗关闭后系统历史继续保留。
- 修复点击通知中心记录只删除消息、没有打开微信的问题，改用本地协议和命名事件激活现有进程。
- 通知中心历史清空后重置消息标签序号，避免新消息数量沿用清空前的累计值。
- 点击自定义消息弹窗后，在打开微信的同时通过 UI Automation 选择对应联系人会话。
- 服务号等同时暴露 Invoke/Selection 动作的会话优先使用 Selection，修复只打开微信但不切换聊天框。
- 服务号聚合会话按“最新一条在前”解析摘要，修复顶部新消息变化但末尾旧摘要不变时漏弹窗。
- 微信 4.x 报告会话已选择但未刷新聊天内容时，向匹配的可见会话行补发一次窗口内点击；不会移动鼠标。
- 微信位于前台时默认继续显示新消息，避免点击一条通知后漏掉其他会话的消息。

## 1.9.0 - 2026-07-01

- 将消息提醒的显示时长调整为 2 分钟，到时自动关闭。
- 同一联系人出现新消息时，重新开始 2 分钟倒计时。
- 日志达到 1 MB 时自动轮转，最多保留 3 份历史日志。
- 修复时间文本、未读数量变化及短暂空扫描导致的重复提醒。
- 参考同类 UI Automation 项目，复用现有解析与去重流程，优先使用微信会话 AutomationId 和 `[N条]` 未读标记判断新消息。
- 增加 5 分钟同联系人同摘要去重冷却，修复未读计数回跳导致弹窗关闭后再次提醒。
- 收窄遮挡检测范围，仅对右下角短通知上浮，忽略剪贴板历史和大型透明宿主窗口。
- 提醒右上角增加“×”，支持只关闭当前提醒而不打开微信。
- 托盘展开面板加入遮挡白名单，面板出现时提醒自动上浮。
- 上浮与回落改为约 60 FPS 的非线性阻尼弹簧动画，同时保持遮挡扫描低频运行。
- 增加 Windows 11 圆角、轻透明、阴影及瞬态亚克力背景，旧系统自动回退。
- 忽略 DWM 已隐藏的托盘宿主窗口，使提醒在托盘面板关闭后使用同一弹簧动画平滑下落。

## 1.0.0 - 2026-06-29

- 初始版本，由 OpenAI Codex 根据项目所有者需求编写。
- 支持读取 Windows 微信 4.x 会话列表。
- 支持联系人、摘要和时间解析。
- 支持消息去重、前台抑制与隐私模式。
- 支持持久提醒，点击后打开微信。
- 支持其他置顶通知遮挡时自动上浮。
- 修复打开微信后丢失最大化状态的问题。
- 添加逻辑测试、真实微信只读集成测试和源码备份流程。
