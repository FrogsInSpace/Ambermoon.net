; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "Ambermoon.net"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Robert Schneckenhaus"
#define MyAppURL "https://www.pyrdacor.net"
#define MyAppExeName "Ambermoon.net.exe"
#define MyResourcePath "."
#define MyDistPath ".\dist"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{1A11EB73-9B24-49FD-B6F6-7D4F3019716D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
InfoBeforeFile={#MyResourcePath}\Package\readme.txt
InfoAfterFile={#MyResourcePath}\Thanks.txt
; Uncomment the following line to run in non administrative install mode (install for current user only.)
;PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#MyResourcePath}
OutputBaseFilename=AmbermoonInstall
SetupIconFile=.\Ambermoon.net\Resources\app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
WindowResizable=no
WizardResizable=no
WizardSizePercent=135,120

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[LangOptions]
DialogFontName=Courier New
DialogFontSize=8
TitleFontName=Courier New
TitleFontSize=11
CopyrightFontName=Courier New
CopyrightFontSize=8

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyDistPath}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyResourcePath}\Package\AmbermoonMap.pdf"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyResourcePath}\Package\AmbermoonRuneTable.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyResourcePath}\Package\Walkthrough.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyResourcePath}\Package\readme.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyResourcePath}\Package\AmbermoonManualEnglish.pdf"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyResourcePath}\Package\changelog.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyDistPath}\api-ms-win-core-winrt-l1-1-0.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyDistPath}\AmbermoonPatcher.exe"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

