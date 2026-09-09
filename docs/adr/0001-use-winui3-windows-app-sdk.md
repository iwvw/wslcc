# ADR-0001：使用 WinUI 3 + Windows App SDK 作为 UI 技术栈

- 状态：Accepted

## 背景

需要为 WSLC 开发原生 Windows 桌面控制程序。候选方案：WinUI 3、WPF、Tauri/Electron。要求遵循微软设计规范、使用标准控件、不手绘组件、与 wslc 原生能力深度集成。

## 决策

采用 WinUI 3（Windows App SDK 2.4.0）+ .NET 10。

理由：
- 微软当前主推的 Windows 原生 UI 框架，遵循 Fluent Design，Mica/圆角/主题资源开箱即用。
- 标准控件体系（NavigationView/ListView/InfoBar/ContentDialog 等）天然满足"绝不手绘组件"的约束。
- 与 Microsoft.WSL.Containers（WinRT API）同为微软生态，类型互操作顺畅。
- 支持自包含部署，规避运行时安装门槛。

## 后果

- 正面：视觉与交互符合 Windows 11 规范，开发效率高，官方持续维护。
- 负面：仅支持 Windows 平台（符合产品定位）；XAML 编译器在非英文系统 + dotnet build 存在已知 bug（见 ADR-0007），需用 VS MSBuild 构建。
