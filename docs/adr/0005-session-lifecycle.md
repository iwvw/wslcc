# ADR-0005：会话生命周期采用进程级单例 + 显式启停

- 状态：Accepted

## 背景

实测发现同名 Session 在同一时刻只能有一个活跃实例：第二次 `new Session(同名).Start()` 抛 COMException（0x800700B7 文件已存在）。而 Terminate 之后同名 Session 可重新 Start（跨应用重启复用成立）。原生 API 没有会话枚举/复用接口，若设计不当会造成会话冲突或资源泄漏。

## 决策

- `WslcApiHost` 维护进程级单例 Session（SemaphoreSlim 串行化），只创建一次。
- 提供 `EnsureSessionAsync`（幂等启动）与 `TerminateSessionAsync`（显式终止释放 VM）。
- 应用退出不自动 Terminate（保留容器与 VM 状态），由用户通过界面/`wslc system session terminate` 管理；仪表盘展示活动会话便于人工清理。
- 会话配置（名称/存储路径/CPU/内存）来自数据库 AppSettings，持久化。

## 后果

- 正面：规避同名会话冲突；容器跨重启保留；用户对 VM 资源占用可见可控。
- 负面：会话长期驻留会占用内存，需依赖 idleTimeout 与人工管理。
