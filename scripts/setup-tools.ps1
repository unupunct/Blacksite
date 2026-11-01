$ErrorActionPreference='Stop'
$t='D:\MyAIProjects\blacksite\.tools'
Set-Location $t
$ProgressPreference='SilentlyContinue'
# private .NET SDK
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
./dotnet-install.ps1 -Version 10.0.403 -InstallDir "$t\dotnet"
# JDK 17
Invoke-WebRequest https://aka.ms/download-jdk/microsoft-jdk-17-windows-x64.zip -OutFile jdk.zip
Expand-Archive jdk.zip -DestinationPath "$t\jdk-tmp" -Force
Move-Item (Get-ChildItem "$t\jdk-tmp")[0].FullName "$t\jdk" -Force
Remove-Item jdk.zip,"$t\jdk-tmp" -Recurse -Force
# Android cmdline tools
Invoke-WebRequest https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip -OutFile clt.zip
Expand-Archive clt.zip -DestinationPath "$t\clt-tmp" -Force
New-Item -ItemType Directory -Force "$t\android-sdk\cmdline-tools" | Out-Null
Move-Item "$t\clt-tmp\cmdline-tools" "$t\android-sdk\cmdline-tools\latest" -Force
Remove-Item clt.zip,"$t\clt-tmp" -Recurse -Force
$env:JAVA_HOME="$t\jdk"; $env:PATH="$t\jdk\bin;$env:PATH"
$sm="$t\android-sdk\cmdline-tools\latest\bin\sdkmanager.bat"
1..30 | % { 'y' } | & $sm --sdk_root="$t\android-sdk" --licenses | Out-Null
& $sm --sdk_root="$t\android-sdk" "platform-tools" "platforms;android-35" "build-tools;35.0.0"
"TOOLS DONE"
