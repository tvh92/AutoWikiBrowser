# Generates a compile-only Microsoft.mshtml reference assembly from Windows' mshtml.tlb,
# for machines without Visual Studio's primary interop assembly.
# The output is delay-signed with the same identity as the real PIA
# (Microsoft.mshtml, 7.0.3300.0, b03f5f7f11d50a3a), so AWB binds to the real PIA at
# runtime when it exists. Never ship the generated file; it cannot load by itself.
#
# Usage: build with  /p:ReferencePath=<repo>\build\ref
param([string]$OutDir = (Join-Path $PSScriptRoot '..\build\ref'))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null
$out = Join-Path (Resolve-Path $OutDir) 'Microsoft.mshtml.dll'

Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Runtime.InteropServices;
public class MshtmlSink : ITypeLibImporterNotifySink {
    public void ReportEvent(ImporterEventKind k, int c, string m) { }
    public Assembly ResolveRef(object typeLib) {
        throw new InvalidOperationException("Unexpected typelib reference: " + Marshal.GetTypeLibName((System.Runtime.InteropServices.ComTypes.ITypeLib)typeLib));
    }
}
public static class TlbLoader {
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void LoadTypeLibEx(string file, int regKind, [MarshalAs(UnmanagedType.Interface)] out object typeLib);
}
'@

$typeLib = $null
[TlbLoader]::LoadTypeLibEx("$env:SystemRoot\System32\mshtml.tlb", 2, [ref]$typeLib)
$publicKey = [System.Drawing.Point].Assembly.GetName().GetPublicKey()   # b03f5f7f11d50a3a
$conv = New-Object System.Runtime.InteropServices.TypeLibConverter
$asm = $conv.ConvertTypeLibToAssembly($typeLib, $out, 0, (New-Object MshtmlSink),
    $publicKey, $null, 'mshtml', (New-Object Version 7, 0, 3300, 0))
$asm.Save('Microsoft.mshtml.dll')
Write-Output "Wrote $out"
Write-Output ([Reflection.AssemblyName]::GetAssemblyName($out).FullName)
