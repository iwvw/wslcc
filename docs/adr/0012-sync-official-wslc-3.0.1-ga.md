# ADR-0012：跟进官方 WSL 3.0.1（wslc 正式 GA）

- 状态：Accepted

## 背景

官方于 2026-09-29 发布 **WSL 3.0.1**，WSL 容器（wslc）与 WSL Containers API 正式 GA，不再是预览特性。官方公告移除了所有 preview 标注（microsoft/WSL#41704），安装路径改为文档化的 `wsl --update`。截至本 ADR，仓库内此前所有 `2.9.x` 版本均为 pre-release 标记。

GA 版本相对此前同步的 2.9.13 的主要变化：

1. **分发与升级**：`wsl --update` 即安装/升级到正式版；`wslc.exe` 新增内置别名 `container.exe`（同一命令集）。
2. **新命令**：`wslc system info`（即 `info` 的别名，输出客户端与服务端版本 + 活动会话列表）、`wslc container restart`（支持 `-s/--signal`、`-t/--timeout`）、`wslc settings reset`。
3. **容器创建/运行新增参数**：`--mount`、`--env-file`、`--domainname`、`--network-alias`、`--ip`、`-P/--publish-all`、`--no-healthcheck`、`--dns-option`、`--dns-search`、`--cidfile`、`-i/--interactive`、`-t/--tty`。经核对，其中多数在 2.9.13 的 `ArgumentDefinitions.h` 中已定义，只是此前未接入管理器。
4. **网络创建**：`-o/--opt` 驱动选项、`-l/--label`（管理器在 v1.2.0 已接入）。
5. **企业治理**：Intune 增加「允许 WSL 容器」「镜像仓库允许列表」控制；Defender for Endpoint 插件扩展到容器活动。
6. **性能**：从 Linux 访问 Windows 文件最高 2 倍提升；新增 `consomme` 网络模式。

## 决策

- **SDK 包**：`Microsoft.WSL.Containers` 从 2.9.13 升级到 3.0.1，获取脚本默认版本同步。
- **升级通道**：`WslcInstallService.RunInstallOrUpgradeAsync` 从 `wsl --update --pre-release` 改为 `wsl --update`（正式通道），并在提示文案中说明将更新正式版 wslc 容器组件。
- **文案口径统一**：README、设置页提示、事件页不可用提示、`WslcRunner` 未找到可执行文件提示等，全部由「预览版 / pre-release 线 / `wsl --update --pre-release`」改为 GA 表述「正式版 / `wsl --update`」。历史 ADR（0009、0011）保留其记录时的原貌，不做改写；ADR-0003 的复核小节更新为 GA 结论。
- **能力接入**：容器创建表单与 `ContainerCreateOptions` 补入 `--mount`、`--env-file`、`--domainname`、`--network-alias`、`-P/--publish-all`、`--no-healthcheck`，与既有高级选项保持同一表单风格（Expander + TextBox/CheckBox）。
- **版本检测**：`WslcInstallService.GetStatusAsync` 仍取 releases 列表所有 tag 中的最高版本，天然覆盖 GA 线（3.0.1 > 2.9.x），无需改动。
- **能力门控**：`WslcCapabilities` 的最低版本表保持 2.9.13（现存 GA 3.0.1 满足），既有 gating 无需调整。

## 后果

- 正面：与官方 GA 线一致的分发与升级路径；容器创建覆盖官方主要参数；文案不再误导用户使用预览通道。
- 负面：企业治理项（Intune/MDE、镜像仓库允许列表、`consomme` 网络模式）属于平台/管理侧能力，桌面管理器不直接介入，仅在需要时透传 `network create -o`；`container.exe` 别名与 `wslc.exe` 等价，管理器继续使用 `wslc.exe` 以保持兼容。
- 遗留：官方已规划 `wslc compose`（下一步重点）。管理器当前的 Compose 为自研编排（YAML 解析 + 逐容器 `wslc run`）。待官方 `wslc compose` 落地后，另立 ADR 评估是否切换到原生命令。
