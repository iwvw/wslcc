# ADR-0011：跟进官方 wslc 2.9.13 能力（digests / all-tags / follow-link / events）

- 状态：Accepted

## 背景

官方 WSL 预发布线于 2026-09-25 发布 2.9.13（仍为 pre-release），相对项目此前同步的 2.9.12 新增了一批 wslc 能力与 Docker 对齐项：

1. `image list --digests`：镜像列表新增 DIGEST 列。仅当显式传 `--digests` 时 JSON 输出才返回真实摘要，否则为 `<none>`（与 docker 的 `needDigest` 门控一致）。
2. `pull --all-tags` / `-a`：拉取仓库全部标签。已带 tag 或 digest 的引用会被拒绝（`tag can't be used with --all-tags/-a`）。
3. `container cp --follow-link` / `-L`：复制时跟随符号链接；不带 `-L` 时保持原行为（复制链接本身）。
4. `events`（别名 `system events`）：新增事件流命令，支持 `--since`、`--until` 与可重复 `--filter`（键：`type`、`container`、`image`、`network`、`event`）。可过滤容器与网络生命周期事件（create/start/stop/destroy/connect/disconnect 等）。
5. 列表 JSON 的 `CreatedSince` 字段改为区域无关英文渲染（此前受本地化影响）。

此外 CLI 引入命令作用域的全局选项机制（为 compose 铺路），`--session` 等全局选项需出现在声明它的命令之后。

## 决策

- **SDK 包**：`Microsoft.WSL.Containers` 从 2.9.12 升级到 2.9.13，`scripts/fetch-wslc-sdk.ps1` 默认版本同步。
- **版本门控**：新增 `WslcCapabilities`（`IWslcCapabilities`），读取 `wslc --version` 解析版本号并缓存，按最低版本判定能力（`ImageDigests`/`PullAllTags`/`CopyFollowLink`/`Events` 均要求 ≥ 2.9.13）。原因：这些 flag 在 2.9.12 上会直接报「选项名称未被识别」，必须按版本降级，避免对旧版用户注入无效参数。能力查询结果在会话/环境变化时可 `Invalidate()`。
- **镜像**：`ImageItem` 增加 `CreatedSince`、`Digest`；列表支持 `--digests`（门控），显示优先用 `CreatedSince` 并在有摘要时展示摘要。拉取支持 `--all-tags`（门控）。
- **容器复制**：`CopyAsync` 增加 `followLink` 参数，门控后追加 `--follow-link`。
- **事件**：新增 `WslcEventService`（`IWslcEventService`）——`QueryAsync` 拉取历史事件并结构化为 `WslcEventEntry`（时间戳、类型、动作、目标、属性），`StreamAsync` 借助 `RunStreamingAsync` 实时流式上报。新增事件页（导航「事件」），支持 since/until/类型/容器筛选、历史查询与实时监听切换。

## 后果

- 正面：镜像摘要与全标签拉取、符号链接复制、容器/网络生命周期事件均可在 GUI 中使用；对 2.9.12 及更早版本优雅降级（控件禁用并提示升级）。
- 负面：新增能力全部依赖 2.9.13+，能力门控基于 `wslc --version` 版本号，若官方在某版本调整 flag 语义需同步更新最低版本表。事件解析按官方 E2E 测试断言的 `<时间戳> <类型> <动作> <ID> (k=v, ...)` 文本格式实现，官方调整措辞时解析会退化（条目不显示而非报错）。

## 实测记录（2026-09-27）

在 2.9.13 上逐项实测新增能力，全部符合官方契约：

- `image list --digests --format json` 返回真实 `sha256:` 摘要；不加 `--digests` 时 `Digest` 为 `<none>`。
- `pull <带 tag 引用> --all-tags` 被拒绝（`tag 不能与 --all-tags/-a 一起使用`，`E_INVALIDARG`）。
- `container cp -L` 跟随符号链接得到目标内容（13 字节），不加 `-L` 得到链接本身（0 字节）。
- `events` 历史查询与 `--filter type=network|container` 均正确；实时流可被取消。

### 两处必须注意的坑（已在实现中规避）

1. **`events` 不带 `--until` 会永久流式输出、不退出**。`QueryAsync`（历史查询）必须注入 `--until`，否则调用永久阻塞。且 `until` 与事件落在同一整秒时会漏掉该事件（官方 E2E 亦用 `+1`），故查询用 `当前秒 + 1` 作为上界。
2. **2.9.13 启用 RedirectionGuard 后拒绝穿越重解析点**。若 `%LOCALAPPDATA%\wslc`（或 `storagePath`）是 junction/符号链接，所有 wslc 操作报 `0x800701c0`（无法遍历该路径，因为它包含不受信任的装入点）。规避方式：在 `settings.yaml` 显式将 `storagePath` 指向真实目录（该键本应用设置页已支持写入）。普通用户默认路径非重解析点时不受影响。

