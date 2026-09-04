#define MyAppName "PC12 Studio TP02"
#define MyAppVersion "0.8"
#define MyAppPublisher "Francisco S. Viana"
#define MyAppExeName "PC12_Studio.exe"

[Setup]
AppId={{D13D2BDD-2747-4C0E-A85B-34E1D0C02F12}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\PC12 Studio TP02
DefaultGroupName=PC12 Studio TP02
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=6.1sp1
OutputDir=output
OutputBaseFilename=PC12-Studio-TP02-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayIcon={app}\PC12_Studio.exe
UninstallDisplayName=PC12 Studio TP02
CloseApplications=yes
RestartApplications=yes
ArchitecturesAllowed=x86 x64

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "..\PC12_v2.1_Windows7_v3_portatil\PC12_Studio.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\PC12_Updater.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\version.txt"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{userdocs}\PC12 Studio TP02\Projetos"
Name: "{userdocs}\PC12 Studio TP02\Dumps"
Name: "{userdocs}\PC12 Studio TP02\Calibration"
Name: "{userdocs}\PC12 Studio TP02\Backups"

[Icons]
Name: "{group}\PC12 Studio TP02"; Filename: "{app}\PC12_Studio.exe"; WorkingDir: "{app}"
Name: "{group}\Verificar atualizações"; Filename: "{app}\PC12_Updater.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\PC12 Studio TP02"; Filename: "{app}\PC12_Studio.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\PC12_Studio.exe"; Description: "Abrir PC12 Studio TP02"; Flags: nowait postinstall skipifsilent
