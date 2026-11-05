# 贡献指南 / Contributing

感谢你愿意改进番茄时钟。这个项目刻意保持轻量：**只有 WPF、.NET Framework 和 Windows 自带编译工具**，没有 NuGet 依赖，也没有前端构建链。

## 环境

- Windows 10 / 11
- .NET Framework 4.8（Windows 10/11 通常已自带）
- PowerShell 5.1 或 PowerShell 7
- Git

## 本地构建

```powershell
cd 番茄时钟
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

构建会生成 `番茄时钟.exe` 和配套的 `番茄时钟.exe.config`。这两个文件已被 `.gitignore` 忽略，请不要提交编译产物。

## 本地验收

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\ui-test.ps1
```

该脚本会真实启动窗口，通过 Windows UI Automation 检查开始/暂停、预设、自定义时长、置顶、完成提醒、护眼休息、最小化和退出。运行时窗口会短暂获得焦点并播放完成提示音，建议在交互式桌面会话中执行；不要在无人值守的 CI 中运行它。

## 代码约定

- 使用系统自带 `csc.exe`（C# 5 语法），不要引入 C# 6+ 语法（如字符串插值、`?.`、`nameof`）。
- 不添加第三方 NuGet 包；界面资源通过 `/resource` 嵌入 exe。
- UI 样式集中在 `ui/MainWindow.xaml`，圆环绘制在 `src/ProgressRing.cs`，计时状态机在 `src/MainWindow.cs`。
- 时长上限为 180 分钟；默认专注 25 分钟、护眼休息 5 分钟。
- 保持无边框便笺外观、左上角三枚窗口按钮和圆环计时器的视觉语言。

## 提交 Pull Request

1. Fork 仓库并从 `main` 创建分支。
2. 修改后运行 `build.ps1` 和 `tools/ui-test.ps1`。
3. 在 PR 中说明改动内容、验证方式和截图（如果界面有变化）。
4. 不要提交 `番茄时钟.exe`、`dist/`、`*.zip` 或任何个人设置文件。

## 可以参与的方向

- 高 DPI / 多显示器适配
- 更细腻的圆环动画与主题
- 眼保健操动作提示 / 20-20-20 小贴士
- 可访问性（键盘导航、屏幕阅读器标签）
- 中英文界面切换

提交前请确认没有加入账号、令牌、隐私信息或与项目无关的资源。
