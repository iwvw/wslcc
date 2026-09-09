# 数据库设计（WSLCC.Data / SQLite）

## 1. 选型依据

- 本地单机桌面应用，数据量小（配置/审计/历史），并发低。
- SQLite 零部署、单文件、跨平台读取方便，满足生产可维护性。
- 驱动：Microsoft.Data.Sqlite（官方 ADO.NET 提供程序），避免 EF Core 在 WinUI 场景的 AOT/迁移负担。

## 2. 数据库位置与连接

- 文件：`%LOCALAPPDATA%\WSLCC\wslcc.db`。
- 连接：每次操作短连接（`SqliteConnection` 打开-执行-关闭），库级写入用 SemaphoreSlim 串行化，开启 WAL 模式提升并发读。
- 初始化：首次打开执行 Schema（幂等 CREATE TABLE IF NOT EXISTS）。

## 3. Schema 设计

### 3.1 app_settings（应用设置）

| 列 | 类型 | 说明 |
|---|---|---|
| key | TEXT PK | 设置键 |
| value | TEXT NOT NULL | 设置值（JSON 或纯文本） |
| updated_at | TEXT NOT NULL | 更新时间（ISO8601） |

约定键：`session.name`、`session.storagePath`、`session.cpuCount`、`session.memoryMb`、`ui.defaultImage`。

### 3.2 audit_log（操作审计）

| 列 | 类型 | 说明 |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | 主键 |
| ts | TEXT NOT NULL | 发生时间 ISO8601 |
| category | TEXT NOT NULL | 类别（image/container/session/settings/system） |
| action | TEXT NOT NULL | 动作（pull/run/start/stop/restart/remove/delete/...） |
| detail | TEXT | 详情（对象名、镜像引用等） |
| result | TEXT NOT NULL | 结果（success/failure） |
| message | TEXT | 失败时的错误消息 |
| duration_ms | INTEGER | 耗时毫秒 |

### 3.3 pull_history（镜像拉取历史）

| 列 | 类型 | 说明 |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | 主键 |
| image_ref | TEXT NOT NULL | 镜像引用 |
| status | TEXT NOT NULL | 结果（success/failure） |
| error | TEXT | 失败原因 |
| started_at | TEXT NOT NULL | 开始时间 |
| finished_at | TEXT | 结束时间 |

### 3.4 container_snapshot（容器状态快照）

| 列 | 类型 | 说明 |
|---|---|---|
| id | INTEGER PK AUTOINCREMENT | 主键 |
| name | TEXT NOT NULL | 容器名称 |
| image | TEXT | 镜像 |
| state | TEXT | 状态（running/exited/...） |
| status | TEXT | 状态描述 |
| ports | TEXT | 端口映射文本 |
| observed_at | TEXT NOT NULL | 观察时间 |

索引：`(name, observed_at)`。

### 3.5 session_state（会话状态）

| 列 | 类型 | 说明 |
|---|---|---|
| name | TEXT PK | 会话名称 |
| storage_path | TEXT NOT NULL | 存储路径 |
| cpu_count | INTEGER | CPU 配额 |
| memory_mb | INTEGER | 内存配额 |
| created_at | TEXT | 首次创建时间 |
| last_started_at | TEXT | 最近启动时间 |
| last_terminated_at | TEXT | 最近终止时间 |

## 4. 仓储接口

| 仓储 | 接口要点 |
|---|---|
| SettingsRepository | Get(key)/Set(key,value)/GetAll() |
| AuditLogRepository | Add(entry)/Query(limit, category?)/Clear()/Count() |
| PullHistoryRepository | Add(entry)/Query(limit)/Count() |
| ContainerSnapshotRepository | Add(entry)/QueryLatest(limit)/ClearOlderThan(days) |
| SessionStateRepository | Upsert(state)/Get(name)/GetAll() |

## 5. 数据流

- 读路径：页面 Loaded → ViewModel → 服务（CLI/API）→ 返回 DTO → 绑定列表；审计/历史/设置直接读数据库。
- 写路径：用户操作 → 服务执行 → 成功/失败 → WslcAuditService.Record → UI 刷新。
- 快照路径：容器列表刷新成功 → WslcSnapshotService.RecordContainer 批量落库（保留最近 N 天）。
