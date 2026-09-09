# 验收标准

## 1. 构建验收

- AC-01：使用 VS MSBuild（amd64）执行 `MSBuild.exe WSLCC.App.csproj -p:Platform=x64` 构建成功，0 错误 0 警告。
- AC-02：应用以 WindowsAppSDKSelfContained + WindowsPackageType=None 构建，可在未安装 Windows App Runtime 的机器上直接运行 exe。

## 2. 运行验收

- AC-03：应用启动后主窗口正常显示（Mica 背景、NavigationView 导航、仪表盘为默认页），进程保持运行不崩溃。
- AC-04：仪表盘展示 wslc 版本、内核版本、Windows 版本、缺失组件、活动会话、API 可用状态。
- AC-05：镜像页可读取本机镜像列表（`wslc image ls` 可用时），拉取输入框与按钮可用，拉取失败给出可读错误。
- AC-06：容器页可读取容器列表（wslc 可用时），新建对话框（名称/镜像/端口/环境变量/自动删除/后台运行）可打开，启停删按钮状态随选中项联动。
- AC-07：日志页可选择容器查看 `wslc logs` 输出（容器不存在/不可用时给出可读错误）。
- AC-08：活动页展示审计日志与拉取历史，数据来自 SQLite。

## 3. 持久化验收

- AC-09：在设置页修改会话名称/存储路径/配额并保存，重启应用后值保留。
- AC-10：执行任一操作（拉取/新建/启停/删除）后，审计日志出现对应记录（类别/动作/结果/耗时）。
- AC-11：拉取操作完成后，拉取历史出现对应记录（镜像引用/状态/时间）。
- AC-12：`%LOCALAPPDATA%\WSLCC\wslcc.db` 存在且可被读取。

## 4. 健壮性验收

- AC-13：在 wslc 命令报 E_UNEXPECTED 的环境下，应用不崩溃，相关操作显示可读错误。
- AC-14：无 Docker Hub 直连（TLS 被阻断）时，拉取失败仅影响该操作，其他页面正常。
- AC-15：全局异常写入 `%LOCALAPPDATA%\WSLCC\app-crash.log`。

## 5. 设计验收

- AC-16：界面全部由标准 WinUI 控件构成，无 Path/Canvas 等自绘组件。
- AC-17：所有 ViewModel 使用 partial property 形式的 [ObservableProperty]（MVVMTK0045 清零）。
- AC-18：数据访问集中在 WSLCC.Data，服务集中在 WSLCC.Core，App 不直接触达数据库。
