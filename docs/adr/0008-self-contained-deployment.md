# ADR-0008：部署采用 WindowsAppSDKSelfContained + WindowsPackageType=None

- 状态：Accepted

## 背景

模板默认按 MSIX packaged 运行。本机未安装 Windows App Runtime，直接运行 exe 时 DeploymentManager 自动初始化失败：`DeploymentInitializeOptions..ctor()` 抛 COMException 0x80040154（没有注册类），应用崩溃。

## 决策

- csproj 设置：
  - `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`：Windows App SDK 运行时随应用分发，不依赖系统安装。
  - `<WindowsPackageType>None</WindowsPackageType>`：声明为非打包（unpackaged）应用，跳过 MSIX 部署与 Runtime 自动安装流程。
- 运行产物：`bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\WSLCC.App.exe` 可直接启动。
- 保留 PublishProfiles（win-x64/arm64/x86）供后续 MSIX 打包发布。

## 后果

- 正面：一键运行、绿色分发、CI 验证简单。
- 负面：self-contained 使输出目录体积增大；unpackaged 模式部分系统集成（如深色模式通知）依赖 WindowsAppSDK 自管理。