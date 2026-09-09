; OpenLadder Studio installer template.
; A versão é injetada a partir de version.txt por scripts/PrepareInstaller.ps1.
#define MyAppName "OpenLadder Studio"
#define MyAppVersion "@OPENLADDER_VERSION@"
#define MyAppPublisher "Francisco S. Viana"
#define MyAppExeName "OpenLadderStudio.exe"

[Setup]
AppId={{D13D2BDD-2747-4C0E-A85B-34E1D0C02F12}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\OpenLadder Studio
DefaultGroupName=OpenLadder Studio
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
; Windows 7 RTM (6.1) também é aceito. O .NET Framework 4 usado pelo Studio
; possui redistribuível oficial compatível com Windows 7 e é tratado abaixo.
MinVersion=6.1
OutputDir=output
OutputBaseFilename=OpenLadder-Studio-Setup
SetupIconFile=..\src\OpenLadderStudio.Desktop\OpenLadderStudio.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayIcon={app}\OpenLadderStudio.ico
UninstallDisplayName=OpenLadder Studio
CloseApplications=yes
RestartApplications=yes
ArchitecturesAllowed=x86 x64

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
; Pré-requisito incorporado ao setup. Só é extraído/executado quando não existe .NET 4.x.
Source: "prerequisites\dotNetFx40_Full_x86_x64.exe"; Flags: dontcopy
Source: "..\src\OpenLadderStudio.Desktop\OpenLadderStudio.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\OpenLadderUpdater.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\OpenLadderDeviceManager.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\OpenLadderModbus.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\OpenLadderMemoryMap.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\OpenLadderTP02PgLab.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\OpenLadderTP02Capture.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\OpenLadderSimulator.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\TP02-PG-Tests.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\OpenLadderStudio.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\OpenLadderStudio.Desktop\version.txt"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{app}\OpenLadderTP02PgLink.exe"
Type: files; Name: "{group}\Link PG TP02 - diagnóstico.lnk"
Type: files; Name: "{group}\Gerenciar controladores.lnk"
Type: files; Name: "{group}\Monitor Modbus.lnk"
Type: files; Name: "{group}\Mapa de memória.lnk"
Type: files; Name: "{group}\Simulação de processo.lnk"
Type: files; Name: "{group}\Laboratório PG TP02.lnk"
Type: files; Name: "{group}\Captura serial PC12\TP02.lnk"
Type: files; Name: "{group}\Captura serial PC12/TP02.lnk"
Type: files; Name: "{group}\Verificar atualizações.lnk"

[Dirs]
Name: "{userdocs}\OpenLadder Studio\Projetos"
Name: "{userdocs}\OpenLadder Studio\Dumps"
Name: "{userdocs}\OpenLadder Studio\Calibration"
Name: "{userdocs}\OpenLadder Studio\Backups"

[Icons]
Name: "{group}\OpenLadder Studio"; Filename: "{app}\OpenLadderStudio.exe"; WorkingDir: "{app}"; IconFilename: "{app}\OpenLadderStudio.ico"
Name: "{autodesktop}\OpenLadder Studio"; Filename: "{app}\OpenLadderStudio.exe"; WorkingDir: "{app}"; IconFilename: "{app}\OpenLadderStudio.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\OpenLadderStudio.exe"; Flags: nowait; Check: ShouldAutoReopenOpenLadder
Filename: "{app}\OpenLadderStudio.exe"; Description: "Abrir OpenLadder Studio"; Flags: nowait postinstall skipifsilent; Check: WasOpenLadderClosedBeforeInstall

[Code]
var
  OpenLadderWasRunning: Boolean;

function DotNet4KeyInstalled(RootKey: Integer; const SubKey: String): Boolean;
var
  InstallValue: Cardinal;
begin
  Result := RegQueryDWordValue(RootKey, SubKey, 'Install', InstallValue) and (InstallValue = 1);
end;

function IsDotNet4Installed(): Boolean;
begin
  Result :=
    DotNet4KeyInstalled(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full') or
    DotNet4KeyInstalled(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client');

  if (not Result) and IsWin64 then
    Result :=
      DotNet4KeyInstalled(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full') or
      DotNet4KeyInstalled(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  DotNetSetup: String;
begin
  Result := '';
  if IsDotNet4Installed() then
    exit;

  WizardForm.StatusLabel.Caption := 'Preparando o Microsoft .NET Framework 4...';
  ExtractTemporaryFile('dotNetFx40_Full_x86_x64.exe');
  DotNetSetup := ExpandConstant('{tmp}\dotNetFx40_Full_x86_x64.exe');

  if not FileExists(DotNetSetup) then
  begin
    Result := 'O pré-requisito Microsoft .NET Framework 4 não foi encontrado dentro do instalador.';
    exit;
  end;

  if not ShellExec('runas', DotNetSetup, '/q /norestart', '', SW_SHOW,
    ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Não foi possível iniciar a instalação do Microsoft .NET Framework 4. Autorize a elevação de administrador e tente novamente.';
    exit;
  end;

  if ResultCode = 3010 then
    NeedsRestart := True
  else if ResultCode = 1641 then
    NeedsRestart := True
  else if ResultCode <> 0 then
  begin
    Result := 'A instalação do Microsoft .NET Framework 4 falhou. Código: ' + IntToStr(ResultCode) + '.';
    exit;
  end;

  if (not IsDotNet4Installed()) and (not NeedsRestart) then
    Result := 'O Microsoft .NET Framework 4 não foi detectado após a instalação. Reinicie o Windows e execute este instalador novamente.';
end;

function UpdateResumeRequested(): Boolean;
begin
  Result := FileExists(ExpandConstant('{localappdata}\OpenLadder Studio\resume-after-update.flag'));
end;

procedure CloseRunningOpenLadderForUpdate();
var
  ResultCode: Integer;
begin
  if UpdateResumeRequested() then
  begin
    { O atualizador já solicitou o fechamento normal. Este é apenas o fallback. }
    Sleep(1000);
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM OpenLadderStudio.exe /T', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode);
    Sleep(300);
  end;
end;

function InitializeSetup(): Boolean;
begin
  OpenLadderWasRunning := UpdateResumeRequested() or (FindWindowByWindowName('{#MyAppName}') <> 0);
  CloseRunningOpenLadderForUpdate();
  Result := True;
end;

function ShouldAutoReopenOpenLadder(): Boolean;
begin
  Result := OpenLadderWasRunning or UpdateResumeRequested();
end;

function WasOpenLadderClosedBeforeInstall(): Boolean;
begin
  Result := (not OpenLadderWasRunning) and (not UpdateResumeRequested());
end;
