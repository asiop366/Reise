# build.ps1 - compile Reise and stage its dependencies next to the exe.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wv2  = Join-Path $root 'lib'

if (-not (Test-Path (Join-Path $wv2 'Microsoft.Web.WebView2.Core.dll'))) {
    Write-Host "[!] lib\ is missing the WebView2 assemblies"
    Write-Host "    download Microsoft.Web.WebView2 from nuget.org and copy:"
    Write-Host "      lib\Microsoft.Web.WebView2.Core.dll"
    Write-Host "      lib\Microsoft.Web.WebView2.WinForms.dll"
    Write-Host "      lib\WebView2Loader.dll"
    exit 1
}

& $csc /nologo /target:winexe /platform:x64 /optimize+ `
    "/win32icon:$root\assets\logo.ico" `
    "/out:$root\Reise.exe" `
    "/r:System.dll" "/r:System.Core.dll" "/r:System.Windows.Forms.dll" "/r:System.Drawing.dll" `
    "/r:$wv2\Microsoft.Web.WebView2.Core.dll" "/r:$wv2\Microsoft.Web.WebView2.WinForms.dll" `
    "$root\src\Reise.cs"

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
foreach ($dll in 'Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll') {
    Copy-Item (Join-Path $wv2 $dll) $root -Force
}
Write-Host "[+] built $root\Reise.exe"
