# Vuelca el IL de un metodo de un ensamblado .NET con Mono.Cecil (el que trae Visual Studio).
# Uso: powershell -NoProfile -File il_metodo.ps1 -Dll <ensamblado> -Tipo <Espacio.Tipo> -Metodo <nombre> [-Max 80]
param(
    [Parameter(Mandatory)] [string] $Dll,
    [Parameter(Mandatory)] [string] $Tipo,
    [Parameter(Mandatory)] [string] $Metodo,
    [int] $Max = 80
)
$ErrorActionPreference = 'Stop'
$cecil = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\Microsoft\AppCAT\Mono.Cecil.dll'
[void][Reflection.Assembly]::LoadFrom($cecil)
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $Dll).Path)
$t = $asm.MainModule.GetType($Tipo)
if (-not $t) { throw "no existe el tipo $Tipo" }
foreach ($m in $t.Methods | Where-Object { $_.Name -eq $Metodo }) {
    Write-Output ("== " + $m.FullName)
    $i = 0
    foreach ($ins in $m.Body.Instructions) {
        Write-Output $ins.ToString()
        $i++
        if ($i -ge $Max) { break }
    }
}
$asm.Dispose()
