# Builds output\android\BLACKSITE-debug.apk and BLACKSITE-release.apk
# Uses the private toolchain in .tools (JDK 17, Android SDK, .NET SDK with the android workload).
# The release APK is signed with a local keystore created on first run (keystore\blacksite.keystore, NOT committed).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$t = Join-Path $root '.tools'
$env:DOTNET_ROOT = "$t\dotnet"; $env:JAVA_HOME = "$t\jdk"; $env:ANDROID_HOME = "$t\android-sdk"; $env:ANDROID_SDK_ROOT = "$t\android-sdk"
$env:PATH = "$t\dotnet;$t\jdk\bin;$env:PATH"; $env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$dotnet = "$t\dotnet\dotnet.exe"
$proj = Join-Path $root 'src\Blacksite.Android\Blacksite.Android.csproj'
$out = Join-Path $root 'output\android'
New-Item -ItemType Directory -Force $out | Out-Null
$common = @("-p:AndroidSdkDirectory=$t\android-sdk", "-p:JavaSdkDirectory=$t\jdk")

Write-Host '== debug APK'
& $dotnet build $proj -c Debug @common -v q
if ($LASTEXITCODE) { throw 'debug build failed' }
$dbg = Get-ChildItem "$root\src\Blacksite.Android\bin\Debug" -Recurse -Filter '*-Signed.apk' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Copy-Item $dbg.FullName "$out\BLACKSITE-debug.apk" -Force

Write-Host '== release APK'
$ksDir = Join-Path $root 'keystore'; $ks = "$ksDir\blacksite.keystore"; $pwFile = "$ksDir\password.txt"
if (-not (Test-Path $ks)) {
    New-Item -ItemType Directory -Force $ksDir | Out-Null
    $pw = -join ((48..57) + (97..122) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
    Set-Content $pwFile $pw
    & "$t\jdk\bin\keytool.exe" -genkeypair -v -keystore $ks -alias blacksite -keyalg RSA -keysize 2048 -validity 10000 -storepass $pw -keypass $pw -dname 'CN=BLACKSITE, O=BLACKSITE, C=US' | Out-Null
}
$pw = (Get-Content $pwFile -Raw).Trim()
& $dotnet publish $proj -c Release @common -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=$ks -p:AndroidSigningKeyAlias=blacksite `
    -p:AndroidSigningKeyPass=$pw -p:AndroidSigningStorePass=$pw -v q
if ($LASTEXITCODE) { throw 'release build failed' }
$rel = Get-ChildItem "$root\src\Blacksite.Android\bin\Release" -Recurse -Filter '*-Signed.apk' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Copy-Item $rel.FullName "$out\BLACKSITE-release.apk" -Force
Get-ChildItem $out -File | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,1)}}
