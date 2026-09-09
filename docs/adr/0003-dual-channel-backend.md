# ADR-0003：后端双通道——wslc CLI 为主、原生 API 为备

- 状态：Accepted

## 背景

wslc 提供两条数据通路：命令行（wslc.exe，Docker 风格语法 + `--format json`）与原生 API（Microsoft.WSL.Containers NuGet，C# 投影）。本机实测（2026-09）API 的 Session 对象 Start 成功但 GetImages 等操作持续抛 COMException E_UNEXPECTED（0x8000FFFF），而 CLI 的 info/image ls 正常、container/volume 相关命令偶发 E_UNEXPECTED。属于 wslc 预览版 + 特定环境（VPN/网络干扰，见 microsoft/WSL#41082）的不稳定。

## 决策

- **主通道：wslc CLI**。所有界面数据读取与写操作默认走 `wslc.exe` 子进程 + JSON 解析。
- **备通道：原生 API**。`WslcApiHost` 封装完整 API 能力（环境检测、会话、镜像、进度事件），供环境正常时启用与后续演进。
- 环境检测服务报告 `ApiAvailable`，仪表盘向用户透明展示当前通道。

理由：
- CLI 是官方稳定的程序化接口，语法与 Docker 一致，`--format json` 适合 GUI 消费，且覆盖 list/logs/stats 等 API 缺失能力。
- 原生 API 处于 preview 且已知不稳定，作为主通道会拖垮可用性。
- 双通道保留未来切换空间，符合预览期"适配层隔离"风险应对。

## 后果

- 正面：应用在 wslc 可用的现实环境下可靠工作；API 稳定后可平滑切换。
- 负面：CLI 输出解析依赖字段名（已用多候选容错缓解）；子进程调用有少量开销（对桌面交互可忽略）。
