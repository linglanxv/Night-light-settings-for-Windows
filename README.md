# NightTint 🌙

Windows 10 / 11 托盘常驻的**夜间模式（色温）调节**小工具 —— 类似 Twinkle Tray 的交互，但调的是暖色强度，并且**每台显示器记住你最后一次的设置**：重启、锁屏、休眠唤醒、拔插显示器之后自动恢复。

![screenshot](docs/screenshot.png)

## 功能

- **0~100 强度滑块**（和 Windows 自带夜间模式同一个逻辑）：0 = 关闭，100 = 最暖；滑块右侧同时显示等效色温（如 `45% · 4430K`）。
- **每台显示器独立滑块**，也可用菜单对全部显示器一键设强度（0% / 25% / 50% / 75% / 100%）。
- **记忆与恢复**：按显示器记录最后一次的强度，开机 / 重新插上显示器 / 解锁 / 休眠唤醒后自动恢复；后台每 2 秒校验一次 gamma，被系统重置也会自动写回。
- **托盘常驻**：左键图标弹出面板，右键打开菜单；图标颜色实时反映状态（白 = 关闭，越橙越暖）。
- **独立设置面板**：`开机自动启动`、`托盘滚轮调节`、`全局快捷键`、`启动时恢复上次强度` 全部做成开关，点击即切换（滑块面板右上角齿轮图标或右键菜单「设置…」打开）。
- **托盘滚轮调节**：鼠标停在托盘图标上滚动滚轮，±5% 直接变暖/变冷。
- **全局快捷键**：`Ctrl+Alt+Shift+↑ / ↓`（避开被 Intel 核显占用的 Ctrl+Alt+↑/↓）；`Ctrl+Alt+B` 弹出/收起面板。
- **轻量**：.NET 8 原生 WinForms，单文件约 220KB，无 Electron，启动不到 1 秒。
- 退出程序时自动把屏幕恢复成中性白，不会把暖色留在系统里。

## 实现原理

色温走 GDI **Gamma Ramp**（`SetDeviceGammaRamp`，f.lux / Redshift 同款方案）：

1. 强度 0~100 → 开尔文色温（0 = 6500K 原生白点，100 = 1900K 极暖）；
2. 开尔文 → RGB 系数（黑体辐射近似，Tanner Helland 算法），归一化到最亮通道避免整体变暗；
3. 每台显示器一个 GDI DC（`\\.\DISPLAY1` …），写入 3×256 gamma ramp；
4. 后台线程每 2 秒读回当前 ramp 校验，发现被重置（锁屏、驱动重置、分辨率切换）立即写回 —— 这是"记住并保持"的关键。

不需要管理员权限，不需要 DDC/CI，笔记本内屏和外接显示器都适用。

## 使用

| 操作 | 说明 |
| --- | --- |
| 左键托盘图标 | 弹出 / 收起面板 |
| 右键托盘图标 | 菜单（预设强度、开机自启、退出等） |
| 拖动滑块 | 实时调节，松手即记忆 |
| **双击滑块行** | 关闭夜间模式（0%） |
| 面板右上角 ⚙ / 右键菜单「设置…」 | 打开设置面板（开机自启动、滚轮、快捷键、启动恢复等开关） |
| 托盘上滚轮 | ±5% 快速微调 |
| `Ctrl+Alt+Shift+↑/↓` | 全局调整强度 |
| `Ctrl+Alt+B` | 全局弹出面板 |

配置文件：`%LOCALAPPDATA%\NightTint\settings.json`（每台显示器的强度也记在这里）。

## 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```bash
# 调试运行
dotnet run --project src/NightTint -c Release

# 发布单文件（约 220KB）
dotnet publish src/NightTint -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish

# 直接弹出面板 / 设置面板启动（可做成快捷方式）
NightTint.exe --show
NightTint.exe --settings
```

> 发布产物依赖已安装的 .NET 8 Desktop Runtime；想要完全绿色版把 `--self-contained false` 改成 `true`（体积约 150MB）。

### 没有 SDK 时的用户级安装（不需要管理员）

```powershell
Invoke-RestMethod https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
./dotnet-install.ps1 -Channel 8.0 -InstallDir "$env:USERPROFILE\.dotnet8"
$env:PATH = "$env:USERPROFILE\.dotnet8;$env:PATH"
```

## GitHub Actions 自动构建

仓库里带了 [.github/workflows/build.yml](.github/workflows/build.yml)：

- 每次推送到 `main` 自动构建，产物在 Actions 的 Artifacts 下载；
- 推送 `v*` 标签（如 `v1.0.0`）自动创建 Release 并附上 `NightTint.exe`。

## 已知限制

- **和系统自带夜间模式互斥**：两者都改 gamma 会互相覆盖，请关闭 Windows 夜间模式后使用 NightTint。
- **HDR 模式无效**：显示器开启 HDR 时 GDI gamma 不生效，请关闭 HDR 或改用显示器 OSD。
- 使用 ICC 颜色配置文件校色过的屏幕，gamma 可能被校准约束，效果会打折。
- 个别老旧驱动对 ramp 有量化误差，程序已做 ±2% 容错。

## Roadmap

- [ ] 按时间自动切换（日落变暖 / 日出恢复）
- [ ] 托盘图标右键快速预设条
- [ ] 多显示器配置导出 / 导入
- [ ] 可自定义快捷键与步长

## License

MIT
