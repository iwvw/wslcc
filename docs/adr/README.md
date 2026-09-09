# 架构决策记录（ADR）索引

本目录记录 WSLCC（WSLC 容器管理器）的关键架构决策。每条 ADR 采用固定格式：状态、背景、决策、后果。

| ADR | 主题 | 状态 |
|---|---|---|
| 0001 | 使用 WinUI 3 + Windows App SDK 作为 UI 技术栈 | Accepted |
| 0002 | 使用 CommunityToolkit.Mvvm 实现 MVVM（partial property） | Accepted |
| 0003 | 后端双通道：wslc CLI 为主、原生 API 为备 | Accepted |
| 0004 | 本地存储使用 SQLite（Microsoft.Data.Sqlite） | Accepted |
| 0005 | 会话生命周期采用进程级单例 + 显式启停 | Accepted |
| 0006 | CLI JSON 输出解析采用多候选字段容错策略 | Accepted |
| 0007 | 构建使用 VS MSBuild（amd64）规避 XamlCompiler 中文环境 bug | Accepted |
| 0008 | 部署采用 WindowsAppSDKSelfContained + WindowsPackageType=None | Accepted |
