#define MyAppName "PC12 Studio TP02"
#define MyAppVersion "0.7"
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
ChangesAssociations=no
ArchitecturesAllowed=x86 x64

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "..\PC12_v2.1_Windows7_v3_portatil\PC12_Studio.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\PC12_Moderno.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\PC12_Ladder.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\TP02_Bridge_Lab.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\TP02_RBP_Reader.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\TP02_Machine_Decoder.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\TP02_Opcode_Calibration.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\TP02_Calibration_Campaign.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\TP02_Auto_Decoder.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\TP02_IL_to_Ladder.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\PC12_Updater.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PC12_v2.1_Windows7_v3_portatil\version.txt"; DestDir: "{app}"; Flags: ignoreversion

; PC12 legado preservado para contingência
Source: "..\PC12_v2.1_Windows7_v3_portatil\pc12.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\PC12_v2.1_Windows7_v3_portatil\*.DLL"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\PC12_v2.1_Windows7_v3_portatil\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\PC12_v2.1_Windows7_v3_portatil\*.HLP"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\PC12_v2.1_Windows7_v3_portatil\*.hlp"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\PC12_v2.1_Windows7_v3_portatil\*.CNT"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\PC12_v2.1_Windows7_v3_portatil\*.cnt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\PC12_v2.1_Windows7_v3_portatil\lastfile.cpu"; DestDir: "{app}"; Flags: onlyifdoesntexist skipifsourcedoesntexist
Source: "..\PC12_v2.1_Windows7_v3_portatil\lastfile.dir"; DestDir: "{app}"; Flags: onlyifdoesntexist skipifsourcedoesntexist

[Dirs]
Name: "{userdocs}\PC12 Studio TP02\Projetos"
Name: "{userdocs}\PC12 Studio TP02\Dumps"
Name: "{userdocs}\PC12 Studio TP02\Calibration"
Name: "{userdocs}\PC12 Studio TP02\Backups"

[Icons]
Name: "{group}\PC12 Studio TP02"; Filename: "{app}\PC12_Studio.exe"; WorkingDir: "{app}"
Name: "{group}\Verificar atualizações"; Filename: "{app}\PC12_Updater.exe"; WorkingDir: "{app}"
Name: "{group}\PC12 original"; Filename: "{app}\pc12.exe"; WorkingDir: "{app}"; Check: FileExists(ExpandConstant('{app}\pc12.exe'))
Name: "{autodesktop}\PC12 Studio TP02"; Filename: "{app}\PC12_Studio.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\PC12_Studio.exe"; Description: "Abrir PC12 Studio TP02"; Flags: nowait postinstall skipifsilent

[Code]
function FileExistsAtInstall(Path: String): Boolean;
begin
  Result := FileExists(Path);
end;
