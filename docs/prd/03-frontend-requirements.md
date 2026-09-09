# 前端功能需求（WinUI 3）

## 1. 设计规范

- 遵循 Fluent Design / Windows 11 设计语言：Mica 背景、System 资源样式（Title/Subtitle/BodyStrong/CaptionTextBlockStyle）、圆角卡片（Border + CardBackgroundFillColorDefaultBrush）、InfoBar、ProgressRing/ProgressBar、ContentDialog、NavigationView。
- 绝对禁止自定义绘制：不使用 Path/Canvas/Polyline 等任何手绘渲染组件，不使用自绘控件模板；视觉效果一律由标准控件与内置主题资源构成。
- 全部交互控件使用标准 WinUI 控件：NavigationView、Frame、ListView、TextBox、CheckBox、Button、InfoBar、ProgressBar、ContentDialog、TextBlock。
- 遵循 MVVM：页面 DataContext 绑定 ViewModel，命令用 RelayCommand/AsyncRelayCommand，数据变更用 ObservableObject/[ObservableProperty]（WinUI 场景用 partial property 形式以保证 CsWinRT AOT 兼容）。
- 页面在 Loaded 时加载数据，异步操作通过 ObservableProperty 的状态（IsLoading/IsPulling/IsRunning）驱动控件反馈。

## 2. 导航结构

| 导航项 | 页面 | 图标 | 说明 |
|---|---|---|---|
| 仪表盘 | HomePage | E80F (Home) | 环境信息、数据通道、会话列表 |
| 容器 | ContainersPage | E7F4 | 容器列表、新建、启停删 |
| 镜像 | ImagesPage | EB9F | 镜像列表、拉取、删除 |
| 日志 | LogsPage | E7C3 (Page/Script) | 选中容器的实时日志 |
| 活动 | ActivityPage | E823 (History) | 审计日志与拉取历史 |
| 关于 | AboutPage | EA3A | 版本与说明 |
| 设置（内置 Settings 入口，IsSettingsSelected） | SettingsPage | E713 | 会话配置、数据库信息 |

## 3. 页面明细

### 3.1 HomePage（仪表盘）

- 标题栏：页面标题 + 刷新按钮 + 加载指示。
- 环境信息卡片：wslc 版本、Linux 内核、Windows 版本、会话管理器版本、活动会话数、缺失组件、原生 API 可用状态、设置文件路径。
- 会话列表卡片：列出 `wslc info` 返回的活动会话（名称 + ID）。
- 错误 InfoBar：环境读取失败时展示可读错误。

### 3.2 ContainersPage（容器）

- 顶部：标题、刷新、新建（ContentDialog）。
- 已选容器操作栏：名称 + 启动/停止/重启/删除按钮（命令的 CanExecute 随所选容器状态联动）。
- 容器列表（ListView）：状态图标（running=播放、stopped=方块）、名称、镜像、Status、端口。
- 新建对话框（全部标准控件）：名称、镜像、端口映射（逗号分隔 host:container）、环境变量（逗号分隔 KEY=VALUE）、自动删除 CheckBox、后台运行 CheckBox。
- 行尾/选中动作后自动刷新列表；操作异常写入审计并展示 InfoBar。

### 3.3 ImagesPage（镜像）

- 顶部：标题、刷新、删除所选。
- 拉取输入行：TextBox + 拉取按钮，占位提示"镜像名称，如 nginx:latest"。
- 拉取区：ProgressBar（不确定态）+ 状态文本（实时输出 wslc 拉取行）。
- 镜像列表（ListView）：名称、ID（截断）、大小、创建时间。
- 删除成功后同步移除列表项并刷新。

### 3.4 LogsPage（日志）

- 顶部：标题 + 容器下拉选择（ComboBox） + 刷新。
- 日志内容：只读 TextBox（TextWrapping 关闭、VerticalScrollBarVisibility=Auto、等宽字体，IsReadOnly=true、IsReadOnly 为标准控件属性），展示 `wslc logs` 输出。
- 失败时 InfoBar 提示（容器未运行/日志不可用时给出可读错误）。

### 3.5 ActivityPage（活动）

- 顶部：标题 + 刷新。
- 审计日志列表（ListView）：时间、分类、动作、结果（成功/失败）、耗时。
- 拉取历史列表（ListView）：镜像引用、总量、状态、开始/结束时间。
- 两个列表来自本地 SQLite，展示 `WslcAuditService` / `WslcHistoryService` 数据。

### 3.6 SettingsPage（设置）

- 会话配置卡片：会话名称、存储路径、CPU 配额、内存配额（表单值来自数据库 AppSettings，保存时写库并提示成功）。
- 数据通道说明卡片：展示原生 API 可用状态与当前通道。
- 数据库信息卡片：数据库文件路径、审计记录数、拉取历史数。
- 清空审计日志按钮（带确认 ContentDialog）。

## 4. 视觉约束

- 颜色、圆角、间距全部来自系统主题资源，不在页面中硬编码颜色值。
- 图标一律 FontIcon（Segoe Fluent Icons，使用标准代码点），不使用图片或自定义矢量。