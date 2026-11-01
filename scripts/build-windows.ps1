# Builds every Windows deliverable into output\windows
#   BLACKSITE.exe              self-contained single-file executable
#   BLACKSITE-Setup.exe        per-user installer (Inno Setup)
#   BLACKSITE-Portable.zip     portable package (settings stored next to the exe)
#   framework-dependent\       small build that needs the .NET 10 Desktop Runtime
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
$out = Join-Path $root 'output\windows'
$proj = Join-Path $root 'src\Blacksite.Desktop\Blacksite.Desktop.csproj'
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $out | Out-Null

Write-Host '== self-contained single file'
& $dotnet publish $proj -c $Configuration -r win-x64 --self-contained true -o "$out\selfcontained" `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false
if ($LASTEXITCODE) { throw 'self-contained publish failed' }
Copy-Item "$out\selfcontained\BLACKSITE.exe" "$out\BLACKSITE.exe"

Write-Host '== framework-dependent'
& $dotnet publish $proj -c $Configuration -r win-x64 --self-contained false -o "$out\framework-dependent" -p:DebugType=none -p:DebugSymbols=false
if ($LASTEXITCODE) { throw 'framework-dependent publish failed' }
Compress-Archive -Path "$out\framework-dependent\*" -DestinationPath "$out\BLACKSITE-FrameworkDependent.zip" -Force

Write-Host '== portable zip'
$port = Join-Path $out 'portable'
New-Item -ItemType Directory -Force $port | Out-Null
Copy-Item "$out\BLACKSITE.exe" $port
Set-Content "$port\portable.flag" 'Presence of this file makes BLACKSITE store settings in .\Data next to the executable.'
Copy-Item (Join-Path $root 'README.md') $port
Compress-Archive -Path "$port\*" -DestinationPath "$out\BLACKSITE-Portable.zip" -Force
Remove-Item $port -Recurse -Force

Write-Host '== installer'
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe') | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) { & $iscc (Join-Path $root 'installer\BLACKSITE.iss') | Select-Object -Last 4; if ($LASTEXITCODE) { throw 'installer failed' } }
else { Write-Warning 'Inno Setup not found - installer skipped' }
Get-ChildItem $out -File | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,1)}}
