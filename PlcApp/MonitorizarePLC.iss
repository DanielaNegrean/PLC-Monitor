; ============================================================================
;  Script Inno Setup pentru aplicatia Monitorizare PLC
;  Deschide-l cu Inno Setup Compiler si apasa F9 (sau Build > Compile).
;  Rezultatul: un singur fisier MonitorizarePLC-Setup.exe
;
;  Caile sunt relative la locul in care se afla acest script (SourcePath),
;  ca proiectul sa se compileze la fel pe orice calculator.
; ============================================================================

#define MyAppName "Monitorizare PLC"
#define MyAppVersion "1.0"
#define MyAppPublisher "Daniela Negrean"
#define MyAppExeName "PlcApp.exe"

; Folderul produs de "dotnet publish", relativ la acest script.
; Daca scriptul nu sta in radacina repository-ului, ajusteaza prefixul.
#define MySourceDir SourcePath + "PlcApp\PlcApp\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish"

; Unde sa fie pus fisierul de instalare rezultat (ignorat de git).
#define MyOutputDir SourcePath + "Installer"

[Setup]
; AppId identifica aplicatia la dezinstalare si la actualizari.
; NU il schimbati intre versiuni, altfel instalarile se dubleaza.
AppId={{7B3E2F41-5C9A-4D82-9E16-2A4F8D1C7B03}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\MonitorizarePLC
DefaultGroupName={#MyAppName}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

OutputDir={#MyOutputDir}
OutputBaseFilename=MonitorizarePLC-Setup

; Instalare pentru utilizatorul curent, fara cerere de drepturi de administrator.
; Daca vreti instalare pentru toti utilizatorii, puneti "admin".
PrivilegesRequired=lowest

; Aplicatia este pe 64 de biti.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Copiaza tot continutul folderului publish, inclusiv subfolderele.
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
