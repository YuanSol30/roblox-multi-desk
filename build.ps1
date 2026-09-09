$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /out:"$PSScriptRoot\Roblox Multi Desk.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll "$PSScriptRoot\Program.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
