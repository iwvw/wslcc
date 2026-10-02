# ADR-0014：识别并提示 wslc 数据目录为重解析点导致的 0x800701c0

- 状态：Accepted

## 背景

部分用户把 `%LOCALAPPDATA%`（或其下目录）迁移到其它盘，`%LOCALAPPDATA%\wslc` 因此变成指向别处的 junction。wslc 2.9.13+ 启用 RedirectionGuard，处于 enforce 时会拒绝遍历不受信任的装入点，读取 `settings.yaml` 或会话存储即失败：

```
无法遍历该路径，因为它包含不受信任的装入点。
错误代码：0x800701c0
```

最小复现确认：RedirectionGuard 为 enforce 时，读取 junction 路径下的文件必然抛 `IOException`（0x800701c0）；audit/off 不报。`wslc.exe` 自身导入 `SetProcessMitigationPolicy`，会走该路径。子进程会继承父进程的 RedirectionGuard，因此应用侧无法通过放宽子进程策略规避（实测 `CreateProcess` 的 mitigation 属性无法放宽父进程已 enforce 的策略）。

## 决策

不做规避式修复，改为**启动自检 + 明确指引**：

- 新增 `WslcPathDiagnostics.DetectUntrustedReparsePoints`，检测 `%LOCALAPPDATA%\wslc` 与默认存储路径是否为重解析点（junction/符号链接）。
- 首页指引：当错误含 `800701c0`/不受信任的装入点，或自检命中重解析点，追加一条针对性指引。
- 容器页：新增路径策略警告 InfoBar，列出命中的路径。
- 错误文案：`0x800701c0` 单独识别为友好说明，指引把存储路径改为真实目录，或把 `%LOCALAPPDATA%\wslc` 恢复为普通目录。
- 顺带修复 stats 轮询错误刷屏：相同错误只记录一次。

## 后果

- 正面：用户能直接看懂问题与解决办法，不再被丢到 WSL 官方 issue 列表；不改变用户既有目录布局。
- 负面：不自动消除错误，需用户按指引调整路径。
- 已知限制：`CreateProcess` 的 mitigation 属性无法放宽父进程已 enforce 的 RedirectionGuard，故无法在应用内自动规避。
