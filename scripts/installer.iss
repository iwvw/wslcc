#define MyAppName "WSLCC"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif
#ifndef OutputSuffix
  #define OutputSuffix ""
#endif
#ifndef FrameworkDependent
  #define FrameworkDependent "false"
#endif
#define MyAppExeName "WSLCC.exe"

[Setup]
AppId={{F0A1A3E2-5C6B-4D7E-9F10-ABCDEF012345}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher=WSLCC
DefaultDirName={localappdata}\Programs\WSLCC
DefaultGroupName=WSLCC
PrivilegesRequired=lowest
UsePreviousAppDir=yes
UsePreviousGroup=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
OutputDir={#OutputDir}
OutputBaseFilename=WSLCC-Setup-{#AppVersion}{#OutputSuffix}
SetupIconFile={#SourceDir}\Assets\AppIcon.ico
VersionInfoVersion={#AppVersion}

[Languages]
Name: "chinesesimplified"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} 卸载"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即运行 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
#if FrameworkDependent == "true"
const
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/10.0';
  WindowsAppRuntimeUrl = 'https://aka.ms/windowsappsdk/2.4/latest/windowsappruntimeinstall-x64.exe';

function IsDotNet10DesktopInstalled: Boolean;
var
  FindRec: TFindRec;
  BaseDir: String;
  Found: Boolean;
begin
  Result := False;
  BaseDir := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(BaseDir) then
    Exit;
  Found := FindFirst(BaseDir + '\10.*', FindRec);
  if Found then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          Result := True;
          Break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function IsWindowsAppRuntimeInstalled: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(HKCU, 'Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages', Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Pos('Microsoft.WindowsAppRuntime.', Names[I]) = 1 then
      begin
        Result := True;
        Break;
      end;
    end;
  end;
end;

function InitializeSetup: Boolean;
var
  Missing: String;
  ErrorCode: Integer;
begin
  Result := True;
  Missing := '';
  if not IsDotNet10DesktopInstalled then
    Missing := Missing + '  - .NET 10 桌面运行时' + #13#10;
  if not IsWindowsAppRuntimeInstalled then
    Missing := Missing + '  - Windows App Runtime 2.4' + #13#10;

  if Missing = '' then
    Exit;

  if MsgBox('这是框架依赖（分离版），检测到以下运行时未安装：' + #13#10#13#10 + Missing
            + #13#10 + '缺少运行时应用将无法启动。是否现在打开下载页面？' + #13#10
            + '选择「否」将忽略并继续安装。',
            mbConfirmation, MB_YESNO) = IDYES then
  begin
    if not IsDotNet10DesktopInstalled then
      ShellExec('open', DotNetDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    if not IsWindowsAppRuntimeInstalled then
      ShellExec('open', WindowsAppRuntimeUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;
#endif
