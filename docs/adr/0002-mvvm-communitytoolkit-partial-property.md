# ADR-0002：使用 CommunityToolkit.Mvvm 实现 MVVM（partial property）

- 状态：Accepted

## 背景

WinUI 3 页面需要将状态与命令绑定到视图。手工实现 INotifyPropertyChanged/ICommand 冗长且易错；需遵循微软推荐的 MVVM 工具包，并保证 CsWinRT AOT 兼容。

## 决策

使用 CommunityToolkit.Mvvm 8.4，且所有 `[ObservableProperty]` 采用 **partial property** 形式（`public partial string X { get; set; }`），而非字段形式。

理由：
- MVVMTK0045 警告明确提示：字段式 [ObservableProperty] 在 WinUI 3（CsWinRT）场景生成代码不兼容 AOT，partial property 允许 CsWinRT 源生成器正确产出 WinRT 封送代码。
- 命令使用 RelayCommand/AsyncRelayCommand，异步命令天然支持 CanExecute 联动与 await。

## 后果

- 正面：零编译器警告、类型安全、样板代码大幅减少。
- 负面：partial property 要求 C# 13+（net10.0 满足）。
