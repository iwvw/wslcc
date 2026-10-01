# ADR-0013：发行版提供合并版（自包含）与分离版（框架依赖）两种模式

- 状态：Accepted

## 背景

ADR-0008 决定采用 `WindowsAppSDKSelfContained=true` + `WindowsPackageType=None`，产物为自包含单目录，开箱即用。代价是体积偏大（约 100 MB），其中大部分是随包分发的 .NET 与 Windows App SDK 运行时。

部分用户机器已安装对应运行时，希望提供体积更小的分发形态；同时保留开箱即用的默认形态，照顾未装运行时的用户。

## 决策

同一版本同时构建两种模式，产物以文件名区分：

| 模式 | 说明 | zip | 安装器 |
|---|---|---|---|
| 合并版（自包含） | 内置 .NET 运行时，开箱即用 | `WSLCC-win-x64.zip` | `WSLCC-Setup.exe` |
| 分离版（框架依赖） | 不含运行时，需系统已装 .NET 10 桌面运行时与 Windows App Runtime 2.4 | `WSLCC-win-x64-framework.zip` | `WSLCC-Setup-framework.exe` |

- 合并版沿用原文件名（无后缀），保持既有下载链接与自更新可用。
- 分离版统一加 `-framework` 后缀。
- csproj 新增开关 `WSLCCFrameworkDependent`（默认 `false`）。为 `true` 时 `WindowsAppSDKSelfContained=false`，并定义编译常量 `WSLCC_FRAMEWORK_DEPENDENT`。
- 自更新按编译期常量选择对应安装包（`WSLCC-Setup.exe` / `WSLCC-Setup-framework.exe`），即按当前安装模式下载同模式产物。
- 分离版安装器在安装前检测 .NET 10 桌面运行时与 Windows App Runtime 2.4，缺失时提示并打开下载页。
- `publish.ps1` 新增 `-Framework` 开关；CI 同时构建两种模式并上传四个产物。

## 后果

- 正面：用户可按体积与运行时的取舍选择；默认仍是开箱即用的合并版。
- 负面：CI 构建与上传时间翻倍；需维护两套产物的命名与自更新映射。
- 分离版依赖系统运行时，缺失时会启动失败，故由安装器前置检测并引导。
