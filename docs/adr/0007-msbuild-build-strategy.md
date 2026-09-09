# ADR-0007：构建使用 dotnet CLI 规避中文环境 XamlCompiler bug

- 状态：Accepted

## 背景

`dotnet build` 在本机（中文 Windows + .NET 10 SDK + Windows App SDK 2.4.0/winui 2.3.6）构建 WinUI 3 时，`XamlCompiler.exe`（net472 子进程，`UseXamlCompilerExecutable=true`）在 Pass2 处理 XamlTypeInfo 时会抛 WMC9999（NullReference，"未将对象引用设置到对象的实例"），伴随 WMC1509（No LocalAssembly）。该问题与微官方 issue microsoft/microsoft-ui-xaml#11157 同族：非英文系统下 XamlCompiler 资源名不匹配导致内部错误。尝试 `UseXamlCompilerExecutable=false`（in-proc）会触发 MSB4027（MetadataLoadContext 已释放），不可用。

但该问题仅在中文系统本机出现。GitHub Actions 的 windows-latest runner 为英文系统，`dotnet build` 不会触发 WMC9999，可正常完成 XAML Pass2 编译。当前构建不再依赖 VS 完整 MSBuild，统一使用 dotnet CLI。

## 决策

- 构建命令固定使用 dotnet CLI：
  `dotnet build src/WSLCC.App/WSLCC.App.csproj -c <Configuration> -p:Platform=<Platform> -p:RuntimeIdentifier=win-<Platform>`
- 主构建通道为 `build.ps1` 与 CI workflow（`.github/workflows/build.yml`），二者均使用 `dotnet build` / `dotnet publish`。
- CI 使用 windows-latest（英文系统）+ dotnet 10 SDK，规避中文系统下 XamlCompiler 的 WMC9999 问题。

## 后果

- 正面：构建不依赖 Visual Studio 安装；英文 runner 上无中文 XAML 编译问题，CI 稳定通过；本机仅需 .NET 10 SDK。
- 负面：中文系统本机直接 `dotnet build` WinUI 3 可能触发 WMC9999，若出现需在英文环境构建或等待上游修复。
