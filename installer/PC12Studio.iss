#define MyAppName "OpenLadder Studio"
#define MyAppVersion "0.16"
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
MinVersion=6.1sp1
OutputDir=output
OutputBaseFilename=OpenLadder-Studio-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayIcon={app}\OpenLadderStudio.exe
UninstallDisplayName=OpenLadder Studio
CloseApplications=yes
RestartApplications=yes
ArchitecturesAllowed=x86 x64

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "..\PC12_v2.1_Windows7_v3_portatil\OpenLadderStudio.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\OpenLadderUpdater.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\OpenLadderDeviceManager.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\OpenLadderModbus.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\OpenLadderMemoryMap.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\version.txt"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{userdocs}\OpenLadder Studio\Projetos"
Name: "{userdocs}\OpenLadder Studio\Dumps"
Name: "{userdocs}\OpenLadder Studio\Calibration"
Name: "{userdocs}\OpenLadder Studio\Backups"

[Icons]
Name: "{group}\OpenLadder Studio"; Filename: "{app}\OpenLadderStudio.exe"; WorkingDir: "{app}"
Name: "{group}\Gerenciar controladores"; Filename: "{app}\OpenLadderDeviceManager.exe"; WorkingDir: "{app}"
Name: "{group}\Monitor Modbus"; Filename: "{app}\OpenLadderModbus.exe"; WorkingDir: "{app}"
Name: "{group}\Mapa de memória"; Filename: "{app}\OpenLadderMemoryMap.exe"; WorkingDir: "{app}"
Name: "{group}\Verificar atualizações"; Filename: "{app}\OpenLadderUpdater.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\OpenLadder Studio"; Filename: "{app}\OpenLadderStudio.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\OpenLadderStudio.exe"; Description: "Abrir OpenLadder Studio"; Flags: nowait postinstall skipifsilent
