# Local Release build of AWB + WikiFunctions without Visual Studio.
# Uses the .NET Framework MSBuild with the Roslyn compiler from NuGet.
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$msbuild = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
$nuget = Join-Path $repo 'build\nuget.exe'
$roslyn = Join-Path $repo 'packages\Microsoft.Net.Compilers.Toolset.4.14.0\tasks\net472'

if (-not (Test-Path $nuget)) {
    New-Item -ItemType Directory -Force (Split-Path $nuget) | Out-Null
    Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/v6.11.1/nuget.exe' -OutFile $nuget
}
foreach ($project in 'AWB', 'WikiFunctions') {
    & $nuget restore "$repo\$project\packages.config" -PackagesDirectory "$repo\packages" -NonInteractive | Out-Null
}
if (-not (Test-Path $roslyn)) {
    & $nuget install Microsoft.Net.Compilers.Toolset -Version 4.14.0 -OutputDirectory "$repo\packages" -NonInteractive | Out-Null
}
if (-not (Test-Path "$repo\build\ref\Microsoft.mshtml.dll")) {
    & (Join-Path $PSScriptRoot 'make-mshtml-ref.ps1')
}

& $msbuild "$repo\AWB\AutoWikiBrowser.csproj" /t:Build "/p:Configuration=$Configuration" /p:Platform=AnyCPU `
    "/p:CscToolPath=$roslyn" /p:CscToolExe=csc.exe "/p:ReferencePath=$repo\build\ref" /m /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

# The generated mshtml reference is compile-only; never leave it next to the exe.
Remove-Item -ErrorAction SilentlyContinue "$repo\AWB\bin\$Configuration\Microsoft.mshtml.dll"
