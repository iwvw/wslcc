# ADR-0004：本地存储使用 SQLite（Microsoft.Data.Sqlite）

- 状态：Accepted

## 背景

应用需要持久化：用户设置（会话名称/存储路径/配额）、操作审计日志、镜像拉取历史、容器状态快照。候选：SQLite、JSON 文件、EF Core + SQLite。

## 决策

- 数据库文件：`%LOCALAPPDATA%\WSLCC\wslcc.db`。
- 驱动：Microsoft.Data.Sqlite（官方 ADO.NET 提供程序），仓储模式封装，Schema 幂等初始化，WAL 模式，写操作库级串行化。

理由：
- 本地单机、低并发、小数据量，SQLite 零部署最合适。
- 相比裸 JSON 文件：支持查询、聚合（计数）、事务、后续扩展。
- 相比 EF Core：避免 WinUI/AOT 场景下的源生成器与迁移负担，依赖更轻。
- 与分层架构配合：WSLCC.Data 独立项目，单向依赖（App → Core → Data）。

## 后果

- 正面：可靠持久化、可查询、易备份（单文件）、跨工具可读。
- 负面：需自行维护 Schema 与仓储代码（规模小，可接受）。
