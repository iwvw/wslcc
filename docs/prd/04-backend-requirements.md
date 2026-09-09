# 后端功能需求（WSLCC.Core / WSLCC.Data）

## 1. 分层结构

```
WSLCC.Core（领域与基础设施服务，不依赖 UI）
├── Models/            不可变 DTO（环境/镜像/容器/进度/设置/审计）
├── Cli/               wslc.exe 进程执行器与错误映射
├── Services/          领域服务（环境/镜像/容器/系统/日志/设置/审计/历史）
└── WslcHost.cs        服务门面（单例）

WSLCC.Data（数据访问层）
├── WslcDatabase.cs    SQLite 连接与 Schema 初始化
└── Repositories/      仓储（设置/审计/拉取历史/容器快照/会话状态）
```

依赖方向：WSLCC.Core → WSLCC.Data；WSLCC.App → WSLCC.Core（不直接依赖 Data）。

## 2. 双通道数据策略

- 主通道：`wslc` 命令行（`wslc.exe` + `--format json`）。理由：CLI 是当前环境唯一稳定可用通道（原生 API 在本机环境报 E_UNEXPECTED），且命令覆盖最全（list/logs/stats/save/load 等）。
- 备通道：Microsoft.WSL.Containers 原生 API（`WslcApiHost` 封装）。环境正常时可选切换，用于进度事件驱动与类型安全调用。
- 运行时选择：环境检测服务报告 `ApiAvailable`；镜像/容器服务以 CLI 为准；`WslcApiHost` 提供 API 能力封装供后续切换。

## 3. 服务清单与职责

| 服务 | 关键操作 | 说明 |
|---|---|---|
| WslcEnvironmentService | GetEnvironmentAsync | `wslc info --format json` + API GetMissingComponents/GetVersion 合并 |
| WslcImageService | List / Pull / Delete | `image ls --format json`、`pull`（流式）、`rmi` |
| WslcContainerService | List / Start / Stop / Kill / Restart / Remove / Run | `container list --format json`、`start/stop/kill/restart/rm`、`run -d --rm --name -p -e -v` |
| WslcLogService | GetLogsAsync | `logs <name>` 输出（等宽文本） |
| WslcInspectService | InspectContainerAsync | `inspect <name> --format json` 返回结构化 JSON 字符串 |
| WslcSystemService | GetInfoAsync / ListSessionsAsync / TerminateSessionsAsync | `info --format json`、`system session list`、`system session terminate` |
| WslcSettingsService | Get/Set 会话配置 | 读写数据库 AppSettings（会话名称/存储路径/CPU/内存） |
| WslcAuditService | Record / Query / Clear | 操作审计：类别、动作、详情、结果、耗时 |
| WslcHistoryService | RecordPull / QueryPulls | 镜像拉取历史落库与查询 |
| WslcSnapshotService | RecordContainer / QueryLatest | 容器状态快照（观察缓存） |
| WslcApiHost | API 能力封装 | Session 单例、PullImageAsync 进度、GetImagesAsync、DeleteImageAsync、TerminateSessionAsync |

## 4. CLI 执行与错误契约

- 所有 CLI 调用统一走 `WslcRunner`：
  - 进程启动：`wslc.exe <args>`，UTF-8 输出重定向，无窗口。
  - 成功：返回 stdout；`--format json` 按"每行一个 JSON 对象"解析（Docker 风格），字段名多候选容错读取。
  - 失败：合并 stdout/stderr，识别 `错误代码： X` / `Error code: X` 提取错误码，抛出 `WslcCliException`（含 WslcErrorCode）。
  - 流式：拉取/运行等长耗时命令逐行回调 `IProgress<string>`。
- 错误可读化：异常消息进入 UI 的 InfoBar；带错误码时展示 `(E_UNEXPECTED)` 等，便于对照 wslc 文档与 GitHub issues。

## 5. 实用功能扩展（官方能力之上）

基于 wslc CLI 的可扩展能力（全部容错处理，命令不可用时给可读错误而非崩溃）：

1. 容器日志：`logs <name>` 实时/历史日志读取。
2. 容器详情：`inspect <name> --format json` 展示结构化详情。
3. 系统会话：`system session list` 与 `terminate`，让用户管理残留 VM 会话（实测残留会话会导致后续操作 E_UNEXPECTED，管理会话是实用修复手段）。
4. 镜像导入导出（save/load 预留命令位）：作为后续功能挂载点。
5. 操作审计与拉取历史：所有写操作经 WslcAuditService 落库。
6. 会话配置持久化：设置页读写数据库，新建容器/会话时作为默认值来源。
7. 容器状态快照：每次列表刷新把状态写入 SQLite，应用离线或快速重启时可展示上次快照。

## 6. 并发与线程

- 服务层无状态，进程级单例（WslcHost.Default）。
- 异步方法全部 await 至 UI 上下文（不主动 ConfigureAwait(false)，由调用方决定）；服务层内部长操作不阻塞 UI。
- WslcRunner 每次调用独立进程，天然串行；数据库写操作使用同一 SQLite 连接加锁（SemaphoreSlim）串行化。
