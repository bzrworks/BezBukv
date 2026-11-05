#ifndef AppVersion
  #error AppVersion must be supplied by build-installer.ps1
#endif

#ifndef PublishDir
  #error PublishDir must be supplied by build-installer.ps1
#endif

#ifndef SetupIconFile
  #error SetupIconFile must be supplied by build-installer.ps1
#endif

#define AppName "BezBukv"
#define AppPublisher "BZR Works"
#define AppExeName "BezBukv.App.exe"

[Setup]
AppId={{F2E51981-6B3E-4E4A-A6E9-ACBC7C41CE14}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=Local\BezBukv.App.SingleInstance
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile={#SetupIconFile}
OutputDir=Output
OutputBaseFilename=BezBukv-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dynamic
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.DeleteSettingsPrompt=Do you also want to delete BezBukv settings and diagnostics data?
russian.DeleteSettingsPrompt=Удалить также настройки и диагностические данные BezBukv?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupApprovedKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run';
  AutostartValueName = 'BezBukv';

procedure RemoveAutostartValues;
begin
  RegDeleteValue(HKCU, RunKeyPath, AutostartValueName);
  RegDeleteValue(HKCU, StartupApprovedKeyPath, AutostartValueName);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataDirectory: String;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  RemoveAutostartValues;

  if UninstallSilent then
    Exit;

  if MsgBox(ExpandConstant('{cm:DeleteSettingsPrompt}'), mbConfirmation,
    MB_YESNO or MB_DEFBUTTON2) = IDYES then
  begin
    AppDataDirectory := ExpandConstant('{localappdata}\BezBukv');
    if not DelTree(AppDataDirectory, True, True, True) then
      Log(Format('Could not completely remove data directory: %s', [AppDataDirectory]));
  end;
end;
