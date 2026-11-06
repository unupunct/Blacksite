# BLACKSITE one-line installer for Windows 10/11 (x64). Installs for the current user, no admin rights needed.
#
#   irm https://raw.githubusercontent.com/unupunct/Blacksite/main/install-windows.ps1 | iex
#
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repo = 'unupunct/Blacksite'
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('blacksite-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $setup = Join-Path $tmp 'BLACKSITE-Setup.exe'
    Write-Host '==> Downloading BLACKSITE-Setup.exe'
    Invoke-WebRequest "https://github.com/$repo/releases/latest/download/BLACKSITE-Setup.exe" -OutFile $setup -UseBasicParsing
    Write-Host '==> Installing'
    Start-Process $setup -ArgumentList '/VERYSILENT', '/NORESTART', '/SUPPRESSMSGBOXES' -Wait
    Write-Host '==> Done. Start BLACKSITE from the Start menu.'
}
finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
