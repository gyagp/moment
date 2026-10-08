# 时刻 · Shike

Windows 原生截图与录屏工具，使用 **C# / WinUI 3 / Windows App SDK** 构建。

[下载最新版](https://github.com/gyagp/shike/releases/latest) · [开发说明](docs/architecture.md) · [验证与限制](docs/validation.md)

## 当前功能

| 功能 | 状态 |
| --- | --- |
| 矩形截图 | 在截图工具栏选择矩形，拖动框选，可跨显示器 |
| 窗口截图 | 悬停高亮、单击选择窗口，捕获窗口本身，不混入遮挡它的其他窗口 |
| 全屏截图 | 一次捕获所有显示器组成的完整桌面 |
| 任意形状截图 | 按住鼠标描绘轮廓，区域外透明，保存 PNG |
| 滚动截图 | **实验功能**：框选内容后自动向下滚动、拼接，你只需停止；到达底部也会自动保存 |
| 屏幕录制 | 录制所选显示器，H.264 / MP4，30 fps，支持暂停、继续、停止 |
| 音频 | 可选系统声音和麦克风；麦克风默认关闭 |
| 全局快捷键 | 截图、长截图、录屏、结束捕获；冲突会在主界面提示 |
| 本地存储 | 图片保存在系统“图片”目录下的 `Shike`，视频保存在系统“视频”目录下的 `Shike` |

时刻不上传截图、录屏或音频。当前没有联网服务、账号或自动更新功能。

## 运行

支持 **Windows 10 2004（build 19041）及以上、Windows 11，x64**。

1. 在 [Releases](https://github.com/gyagp/shike/releases) 下载 `Shike-<版本>-win-x64.zip`。
2. 解压整个目录，运行 `Shike.exe`。请勿只移动 exe 文件。
3. 发布包包含 .NET、Windows App SDK 和 Visual C++ 运行库，无需单独安装这些运行时。

当前版本未做代码签名。Windows N / KN 版本需要系统的 Media Feature Pack 才能使用录屏和原生窗口捕获。当前不提供 ARM64 原生包或安装程序。

## 使用

| 操作 | 快捷键 |
| --- | --- |
| 新建截图，打开模式工具栏 | `Ctrl + Shift + S` |
| 矩形 / 窗口 / 全屏 / 任意形状 | 截图工具栏中按 `1` / `2` / `3` / `4` |
| 滚动截图 | `Ctrl + Shift + L` |
| 开始 / 结束录屏 | `Ctrl + Shift + R` |
| 结束当前长截图或录屏并保存 | `Ctrl + Shift + F` |
| 取消框选 | `Esc` |

**截图**：点击“新建截图”或使用快捷键，顶部工具栏提供与 Windows 截图工具相似的四种模式。矩形与任意形状以进入截图时的画面为准；全屏包含所有显示器；窗口模式在点击后捕获所选窗口的当前画面。`Esc` 取消。结果保存为 PNG，可预览、复制图片或打开所在目录。“长截图 / 录屏显示器”不限制普通截图的范围，只决定其工具栏初始所在屏幕。

**滚动截图**：只框选同一个窗口内的滚动内容（至少 80 × 160 像素），排除浏览器工具栏、固定标题和悬浮控件。时刻会聚焦目标窗口，将鼠标放入区域中并自动滚动、等待画面稳定、拼接。无需你操作滚轮，点击浮动条的“结束并保存”或按 `Ctrl + Shift + F` 即可停止。到达底部或连续三次无法继续滚动时自动保存；焦点切换、鼠标离开区域、窗口移动、画面不稳定、匹配失败或大小达到上限时，停止滚动并保存已成功捕获的部分，同时提示原因。上限为 30,000 像素高及 256 MiB 原始拼接数据。请检查保存结果；固定元素、动画、重复纹理、跳页式滚动及更高权限窗口仍可能无法捕获。

**录屏**：先确认显示器和音频选项，点击开始。浮动条可以停止；从任务栏回到主窗口可以暂停、继续。浮动条通过 Windows 捕获排除 API 隐藏。视频完成编码后才会从 `.partial.mp4` 重命名为 `.mp4`。异常退出可能留下未完成的视频。

## 开发

需要 Windows、.NET 10 SDK。Visual Studio 可选；打包时需要 Visual Studio C++ Build Tools 的 x64 可再发行运行库文件。

```powershell
dotnet restore src/Shike.App/Shike.App.csproj
dotnet build src/Shike.App/Shike.App.csproj -c Release -p:Platform=x64
dotnet run --project src/Shike.App -c Release -p:Platform=x64
dotnet run --project tests/Shike.Core.Tests -c Release
```

打包：

```powershell
pwsh ./scripts/package.ps1 -Version 0.1.1
```

输出 ZIP 与 SHA-256 文件位于 `artifacts/`。推送 `v*` 标签会运行 GitHub Actions，执行核心测试、构建并上传 GitHub Release 附件。后续按 `v0.1.1`、`v0.1.2` 等小版本发布，实验功能单独标注。

## 后续方向

- 截图标注、撤销和导出选项。
- 区域 / 窗口录屏、倒计时、更多帧率与编码设置。
- 自动滚动的兼容性、固定页头处理、更丰富的拼接样例。
- 托盘、设置持久化、快捷键自定义和历史记录。
- 安装包、代码签名、ARM64 原生构建。

## 参考与许可

功能方向参考 [Snow Apps / Snow Shot](https://github.com/mg-chao/snow-apps)。本项目独立实现，没有复制 Snow Shot 的 GPL 源代码或资源。

时刻源代码使用 [MIT License](LICENSE)。录屏使用 [ScreenRecorderLib](https://github.com/sskodje/ScreenRecorderLib)；依赖及随包组件保留原许可，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
