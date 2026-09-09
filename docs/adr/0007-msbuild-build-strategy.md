# ADR-0007：构建使用 VS MSBuild（amd64）规避 XamlCompiler 中文环境 bug

- 状态：Accepted

## 背景

`dotnet build` 在本机（中文 Windows + .NET 10 SDK + Windows App SDK 2.4.0/winui 2.3.6）构建 WinUI 3 失败：`XamlCompiler.exe`（net472 子进程，`UseXamlCompilerExecutable=true`）在 Pass2 处理 XamlTypeInfo 时抛 WMC9999（NullReference，"未将对象引用设置到对象的实例"），伴随 WMC1509（No LocalAssembly）。该问题与微官方 issue microsoft/microsoft-ui-xaml#11157 同族：非英文系统下 XamlCompiler 资源名不匹配导致内部错误；VS 的完整 MSBuild 使用自有 XAML 编译器宿主不受影响。尝试 `UseXamlCompilerExecutable=false`（in-proc）会触发 MSB4027（MetadataLoadContext 已释放），不可用。

## 决策

- 构建命令固定使用 VS 完整 MSBuild（64 位）：
  `"C:\Program Files\Microsoft Visual Studio\<ver>\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" src/WSLCC.App/WSLCC.App.csproj -p:Platform=x64`
- 必须使用 amd64 MSBuild：32 位 MSBuild 会把项目的 RuntimeIdentifier 推断为 win-x86，与 -p:Platform=x64 冲突（NETSDK1032）。
- 将构建命令记录到仓库构建文档，CI/脚本统一引用（不依赖 `dotnet build` 作为主构建通道）；`dotnet build` 仅用于 Core 类库等非 XAML 项目。

## 后果

- 正面：构建稳定通过，0 警告；CI 可脚本化。
- 负面：构建依赖 VS 安装（环境类库）；等待上游修复后可回切 `dotnet build`。