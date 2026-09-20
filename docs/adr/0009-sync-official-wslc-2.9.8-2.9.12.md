# ADR-0009：跟进官方 wslc 2.9.8–2.9.12 能力扩展

- 状态：Accepted

## 背景

官方 WSL 预发布线在 2.9.8 至 2.9.12 之间密集补齐了大量 wslc 能力与 Docker 对齐项，同时 WSL Containers API 仍处于 preview（官方计划 2026 年秋季 GA）。此前项目存在两类问题：

1. **版本检测走错通道**：`WslcInstallService` 使用 `/releases/latest`，该接口只返回最新非预发布版（2.7 稳定线）。含 wslc 的 2.9.x 一直是 pre-release，因此用户永远看不到 2.9.10+ 的真实更新。
2. **一键升级走错通道**：`winget install --id Microsoft.Windows.WSL` 安装稳定版，无法升级到 2.9.x 预发布线。官方推荐路径是 `wsl --update --pre-release`。

此外，官方新增的 `--session` 全局选项、`settings.yaml` 会话字段、网络管理、prune、容器创建参数等均未接入。

## 决策

- **版本检测**：改为拉取 `microsoft/WSL` releases 列表（`per_page=40`），取所有 tag 中版本号最高者，覆盖预发布线。
- **升级通道**：改为 `wsl --update --pre-release`，以管理员权限启动。
- **SDK 包**：`Microsoft.WSL.Containers` 从 2.9.9 升级到 2.9.12。该包仅随 GitHub Release 发布、不在 nuget.org，因此引入 `nuget-packages/` 本地源 + `scripts/fetch-wslc-sdk.ps1` 获取脚本，build/publish/CI 三处统一调用。
- **会话配置真正生效**：
  - 新增 `WslcSettingsFileService` 读写官方 `%LOCALAPPDATA%\wslc\settings.yaml`，覆盖 `cpuCount`、`memorySize`、`maxStorageSize`、`storagePath`、`defaultBindingAddress`、`hostLoopback`、`idleTimeout`、`credentialStore`，写入时保留官方注释与格式。
  - `WslcRunner` 支持 `--session` 全局选项注入。
- **会话名必须校验存在性**：实测 `wslc --session <不存在>` 会让所有操作报 `WSLC_E_SESSION_NOT_FOUND`。因此启动与保存配置时先 `system session list` 校验，不存在则回退到 wslc 默认会话并记录日志，绝不把无效会话名注入命令行。
- **新增能力接入**：网络管理（list/create/rm/connect/disconnect/prune/inspect）、prune（container/image/volume/network）、`container cp`、容器创建 19 项新参数、`inspect -s`、`list --size`、`image ls --all`、`logs` 的 `-n/--timestamps/--details/--since/--until`。

## 后果

- 正面：版本检测与升级路径与官方预发布线一致；会话配置不再只是展示，真正写入官方设置文件；网络与清理能力补齐，容器创建表单覆盖官方主要参数。
- 负面：`settings.yaml` 写入依赖官方注释文本定位键位，若官方调整注释措辞，降级为在对应小节末尾追加键值（仍为合法 YAML，功能不受影响）；prune 结果直接回显 CLI 输出，未做结构化计数，因各资源输出格式不一致。
