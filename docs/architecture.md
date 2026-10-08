# 时刻：技术决策

## 产品约束

- 仅 Windows，软件名为「时刻」，英文标识为 Shike。
- 原生 WinUI 3 界面，不使用浏览器或 WebView 实现界面。
- 仓库与二进制发布位置：`gyagp/shike`。
- 首个迭代以可验证的截图、实验性长截图、显示器录屏为范围。

## 分层

`src/Shike.App` 是 .NET 10、WinUI 3 的 unpackaged 桌面应用。主窗口负责捕获状态、错误提示、预览、全局快捷键和生命周期。单次只允许一个捕获会话，录屏结束后等待编码完成再恢复空闲状态；关闭时也会等待保存。

`Services/NativeMethods.cs` 集中封装 Win32：按显示器获取物理坐标，通过 GDI BitBlt / DIB 读取 BGRA 像素，通过 RegisterHotKey 注册快捷键。`Views/SelectionWindow.cs` 使用捕获时的静态屏幕图作框选背景，将逻辑坐标映射回物理像素。目前每次只在一个显示器上框选，不支持跨屏框选。

`ImageStore` 通过 Windows BitmapEncoder 编码 PNG，通过 Windows 剪贴板复制图片。`RecordingSession` 封装 ScreenRecorderLib，以原生图形捕获 / Media Foundation 编码 H.264 MP4。是否启用系统回放音频与麦克风由界面明确选择。完成和失败事件映射为 Task，UI 通过 DispatcherQueue 接收状态。

`src/Shike.Core` 不依赖 Windows，包含像素帧和滚动匹配算法。算法比较相邻视口中不同向下偏移的重叠区域，接受误差小且不歧义的匹配，将新增底部条带追加到结果中。失败时保留最后一次成功帧，供用户回退重试。无可靠特征、固定标题、动画或跳页不在首版保证范围内。

`tests/Shike.Core.Tests` 是无需测试框架依赖的断言程序，覆盖真实像素重建、无重叠/向上移动拒绝、重复帧和尺寸限制。App 的显式 `--smoke-test` 入口验证 Windows 图形和媒体集成，不在正常启动时运行。

## 发布

以 x64、自包含、多文件 ZIP 发布。多文件形式适配 WinUI PRI 资源和混合模式原生录屏依赖，避免单文件解包与加载路径问题。`scripts/package.ps1` 捆绑 .NET、Windows App SDK、Visual C++ CRT 和许可证，生成 ZIP 与 SHA-256。CI 对普通提交做构建与核心测试，标签触发 Release。

ARM64、安装程序、代码签名和自动更新留待后续版本。不要在未验证架构依赖、捕获和编码的情况下仅修改 RID 来宣称支持。
