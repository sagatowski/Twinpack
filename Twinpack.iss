; Script generated by the Inno Script Studio Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

; #define MyAppName "Twinpack"
; #define MyAppVersion "0.1.0.0"
; #define MyConfiguration "Debug"

#define MyAppPublisher "Zeugwerk GmbH"
#define MyAppURL "http://www.zeugwerk.at/"
#define MyAppExeName "Twinpack.exe"
#define TcXaeShellExtensionsFolder "C:\Program Files (x86)\Beckhoff\TcXaeShell\Common7\IDE\Extensions\"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{326905EF-5BAA-50D5-9F26-B205B58C91EF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
CreateAppDir=no
OutputBaseFilename={#MyAppName} {#MyAppVersion}
Compression=lzma
SolidCompression=yes
VersionInfoCompany=Zeugwerk GmbH
VersionInfoProductName=Zeugwerk Creator
CloseApplications=force
RestartApplications=True
SetupIconFile=Zeugwerk.ico
WizardSmallImageFile=Zeugwerk.bmp
LicenseFile=LICENSE
InfoBeforeFile=DISCLAIMER

[Files]
Source: "TwinpackVsix\bin\{#MyConfiguration}\Package\*"; DestDir: "{#TcXaeShellExtensionsFolder}Zeugwerk\Twinpack"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallVsixInTcXaeShell;
Source: "TwinpackVsix\bin\{#MyConfiguration}\TwinpackVsix.vsix"; DestDir: "{tmp}"; Flags: deleteafterinstall;
Source: "vswhere.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall;

[Dirs]
Name: "C:\Program Files (x86)\Beckhoff\TcXaeShell\Common7\IDE\Extensions\Zeugwerk\Twinpack"

[InstallDelete]
Type: filesandordirs; Name: "{#TcXaeShellExtensionsFolder}Zeugwerk\Twinpack\*"

[Code]
function VsWhereValue(ParameterName: string; OutputData: string): TStringList;
var
  Lines: TStringList;
  Line: string;
  i: integer;
  begin
    Result := TStringList.Create;
    Lines := TStringList.Create;
    try
      Lines.Text := OutputData;
      for i := 0 TO Lines.Count - 1 do
      begin
        Line := Lines[i];
        if Pos(ParameterName + ':', Line) > 0 then
        begin
          Result.Add(Trim(Copy(Line, Pos(ParameterName + ':', Line) + Length(ParameterName) + 2, MaxInt)));
        end;
      end;
    finally
      Lines.Free;
  end;
end;

procedure OpenPrivacyPolicy(Sender : TObject);
var
  ErrorCode : Integer;
begin
  ShellExec('open', 'https://github.com/Zeugwerk/Twinpack/blob/main/PRIVACY_POLICY.md', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

// Exec with output stored in result.
// ResultString will only be altered if True is returned.
function ExecWithResult(Filename, Params, WorkingDir: String; ShowCmd: Integer; Wait: TExecWait; var ResultCode: Integer; var ResultString: String): Boolean;
var
  TempFilename: String;
  Command: String;
  ResultStringAnsi: AnsiString;
begin
  TempFilename := ExpandConstant('{tmp}\~execwithresult.txt');
  Command := Format('"%s" /S /C ""%s" %s > "%s""', [ExpandConstant('{cmd}'), Filename, Params, TempFilename]);
  Result := Exec(ExpandConstant('{cmd}'), Command, WorkingDir, ShowCmd, Wait, ResultCode);
  if not Result then
    Exit;
  LoadStringFromFile(TempFilename, ResultStringAnsi); 
  ResultString := ResultStringAnsi;
  DeleteFile(TempFilename);
  // Remove new-line at the end
  if (Length(ResultString) >= 2) and (ResultString[Length(ResultString) - 1] = #13) and (ResultString[Length(ResultString)] = #10) then
    Delete(ResultString, Length(ResultString) - 1, 2);
end;

var
  UserPage : TInputQueryWizardPage;
  UserPagePolicyLabel : TLabel;
  VisualStudioOptionsPage: TInputOptionWizardPage;
  UserPageRegisterButton: TNewButton;

  OutputFile: string;
  OutputData: AnsiString;
  InstallationPaths: TStringList;
  DisplayNames: TStringList;
  ErrorCode: integer;
  i : integer; 
  VsWhereOutput : string; 

function ValidateEmail(strEmail : String) : boolean;
var
    strTemp  : String;
    nSpace   : Integer;
    nAt      : Integer;
    nDot     : Integer;
begin
    strEmail := Trim(strEmail);
    nSpace := Pos(' ', strEmail);
    nAt := Pos('@', strEmail);
    strTemp := Copy(strEmail, nAt + 1, Length(strEmail) - nAt + 1);
    nDot := Pos('.', strTemp) + nAt;
    Result := ((nSpace = 0) and (1 < nAt) and (nAt + 1 < nDot) and (nDot < Length(strEmail)));
end;

procedure RegisterEnable(Sender: TObject);
begin
   UserPageRegisterButton.Enabled := ValidateEmail(UserPage.Edits[0].Text);
end;

function IntToHex(Value: Integer): string;
begin
  Result := Format('%.2x', [Value]);
end;

function UrlEncode(data: AnsiString): AnsiString;
var
  i : Integer;
begin
  Result := '';
  for i := 1 to Length(data) do begin
    if ((Ord(data[i]) < 65) or (Ord(data[i]) > 90)) and ((Ord(data[i]) < 97) or (Ord(data[i]) > 122)) then begin
      Result := Result + '%' + IntToHex(Ord(data[i]));
    end else
      Result := Result + data[i];
  end;
end;

procedure Register(Sender: TObject);
var
  WinHttpReq: Variant;
begin
  WinHttpReq := CreateOleObject('WinHttp.WinHttpRequest.5.1');
  WinHttpReq.Open('GET', 'https://operations.zeugwerk.dev/api.php?method=zkregister&usermail='+UrlEncode(UserPage.Edits[0].Text), False);
  WinHttpReq.Send('');
  if WinHttpReq.Status <> 200 then
  begin
      MsgBox('Could not connect to Login server. Please check your internet connection!', mbError, MB_OK);
  end
    else
  begin
    if Pos('HTTP/1.1 200', Trim(WinHttpReq.ResponseText)) > 0 then
    begin
      WizardForm.NextButton.OnClick(nil);
    end
      else
    begin
      MsgBox('This email adress is already reserved or used. You might already be registered with this email address.', mbError, MB_OK);
    end;
  end;
end;

procedure InitializeWizard;
begin
  ExtractTemporaryFile('vswhere.exe');
  ExecWithResult(ExpandConstant('{tmp}\\vswhere.exe'), '-all -products * -version [15.0,17.0)', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode, VsWhereOutput);
  
  { Create the pages }
  
  { UserPage }
  UserPage := CreateInputQueryPage(wpWelcome,
    'Welcome to the Twinpack Installer', 'Twinpack is a package manage tool to faciliate sharing of TwinCAT libraries within the community.',
    'Register a Twinpack account (only needed if you want to publish packages to the Twinpack Server)');
  UserPage.Add('Email:', False);
  UserPage.Edits[0].OnChange := @RegisterEnable;
  
  UserPageRegisterButton := TNewButton.Create(UserPage);
  UserPageRegisterButton.Left := UserPage.Edits[0].Left;
  UserPageRegisterButton.Top := UserPage.Edits[0].Top + UserPage.Edits[0].Height + ScaleY(5);
  UserPageRegisterButton.Parent := UserPage.Edits[0].Parent;
  UserPageRegisterButton.ParentFont := True;
  UserPageRegisterButton.Caption := 'Register';
  UserPageRegisterButton.OnClick  := @Register;  
  UserPageRegisterButton.Enabled := False;    

  UserPagePolicyLabel := TLabel.Create(UserPage);
  UserPagePolicyLabel.Left := UserPageRegisterButton.Left + UserPageRegisterButton.Width + ScaleX(5);
  UserPagePolicyLabel.Top := UserPageRegisterButton.Top + ScaleY(5);
  UserPagePolicyLabel.Parent := UserPage.Edits[0].Parent;
  UserPagePolicyLabel.Caption := 'Privacy policy';
  UserPagePolicyLabel.OnClick := @OpenPrivacyPolicy;
  UserPagePolicyLabel.Font.Style := UserPagePolicyLabel.Font.Style + [fsUnderline];
  UserPagePolicyLabel.Font.Color := clBlue;
  UserPagePolicyLabel.Cursor := crHand; 

  { VisualStudioOptionsPage } 
	VisualStudioOptionsPage := CreateInputOptionPage(UserPage.ID,
	  'Install options', 'Twinpack is compatible with multiple IDEs',
	  'Please choose the Visual Studio versions that Twinpack is installed for.',
	  False, False);

	// Add items
  VisualStudioOptionsPage.Add('TcXAEShell');
  if(DirExists('C:\Program Files (x86)\Beckhoff\TcXaeShell')) then
    VisualStudioOptionsPage.CheckListBox.Checked[0] := true
  else
    VisualStudioOptionsPage.CheckListBox.ItemEnabled[0] := false;

  DisplayNames := VsWhereValue('displayName', VsWhereOutput);
  InstallationPaths := VsWhereValue('installationPath', VsWhereOutput); 

  for i:= 0 to DisplayNames.Count-1 do
  begin
    if(FileExists(InstallationPaths[i] + '\Common7\IDE\VSIXInstaller.exe')) then
      VisualStudioOptionsPage.Add(DisplayNames[i]);
  end;
end;

const
  SHCONTCH_NOPROGRESSBOX = 4;
  SHCONTCH_RESPONDYESTOALL = 16;

function InstallVsixInTcXaeShell(): Boolean;
begin
  Result := VisualStudioOptionsPage.CheckListBox.Checked[0] = True;
end;

procedure CurStepChanged (CurStep: TSetupStep);
var
  WorkingDir:   String;
  ReturnCode:   Integer;
  i:            Integer;
begin  
  if (ssInstall = CurStep) then
  begin
    ExtractTemporaryFile('TwinpackVsix.vsix');

    for i := 0 to DisplayNames.Count-1 do
    begin
      if(VisualStudioOptionsPage.CheckListBox.Checked[i+1]) then
        ShellExec('', InstallationPaths[i] + '\Common7\IDE\VSIXInstaller.exe', ExpandConstant('{tmp}\TwinpackVsix.vsix'), '', SW_HIDE, ewWaitUntilTerminated, ReturnCode)
    end;
  end;

end;

function InstallVsixThroughVsixInstaller(): Boolean;
begin
  Result := False;
  if(VisualStudioOptionsPage.CheckListBox.Checked[1] = True) then
  begin
    Result := True;
  end  
end;
