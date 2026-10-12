# Corrige FrostyModSupport.dll del fork 1.0.7 para PvZ Garden Warfare 1.
#
# El fork olvido GW1 (ProfileVersion.PlantsVsZombiesGardenWarfare = 20140225) en las cuatro listas de
# FrostyModExecutor.Run que tratan los juegos con parche en Update\Patch\Data (BF4, GW2, DAI, NFS...).
# Sin eso escribe el mod en ModData\<pack>\Patch, y el juego, que busca en
# <dataPath>\Update\Patch\Data, arranca sin mods. La 1.0.6.3 si incluye GW1 en las cuatro.
#
# Uso: powershell -NoProfile -File parchear_fms.ps1 -Entrada <FrostyModSupport.dll> -Salida <dll nuevo>
param(
    [Parameter(Mandatory)] [string] $Entrada,
    [Parameter(Mandatory)] [string] $Salida
)
$ErrorActionPreference = 'Stop'
$GW1 = 20140225
$GW2 = 20150223
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\Microsoft\AppCAT'
[void][Reflection.Assembly]::LoadFrom("$vs\Mono.Cecil.dll")
[void][Reflection.Assembly]::LoadFrom("$vs\Mono.Cecil.Rocks.dll")

$Entrada = (Resolve-Path -LiteralPath $Entrada).Path
$resolvedor = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolvedor.AddSearchDirectory((Split-Path $Entrada))
$param = New-Object Mono.Cecil.ReaderParameters
$param.AssemblyResolver = $resolvedor
$param.InMemory = $true
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Entrada, $param)
$tipo = $asm.MainModule.GetType('Frosty.ModSupport.FrostyModExecutor')
$run = $tipo.Methods | Where-Object { $_.Name -eq 'Run' }
if (-not $run) { throw 'no encuentro FrostyModExecutor.Run' }

$cuerpo = $run.Body
[Mono.Cecil.Rocks.MethodBodyRocks]::SimplifyMacros($cuerpo)
$il = $cuerpo.GetILProcessor()
$Op = [Mono.Cecil.Cil.OpCodes]
$cambiadas = 0
$yaEstaba = 0

# Cada lista es: ldc.i4 N; newarr ProfileVersion; dup; ldtoken <datos>; call InitializeArray; call IsLoaded
foreach ($llamada in @($cuerpo.Instructions)) {
    if ($llamada.OpCode -ne $Op::Call -or $llamada.Operand.Name -ne 'IsLoaded') { continue }
    $init = $llamada.Previous
    if ($init.OpCode -ne $Op::Call -or $init.Operand.Name -ne 'InitializeArray') { continue }
    $token = $init.Previous
    $dup = $token.Previous
    $newarr = $dup.Previous
    $cuenta = $newarr.Previous
    if ($token.OpCode -ne $Op::Ldtoken -or $dup.OpCode -ne $Op::Dup -or $newarr.OpCode -ne $Op::Newarr -or $cuenta.OpCode -ne $Op::Ldc_I4) { continue }
    $datos = $token.Operand.Resolve().InitialValue
    $valores = @(for ($i = 0; $i -lt $datos.Length; $i += 4) { [BitConverter]::ToInt32($datos, $i) })
    if ($valores -notcontains $GW2) { continue }
    if ($valores -contains $GW1) { $yaEstaba++; continue }
    $valores += $GW1
    # Se sustituye el bloque de datos por una matriz escrita elemento a elemento.
    $cuenta.Operand = [int]$valores.Count
    $il.Remove($token)
    $il.Remove($init)
    $ancla = $dup
    $il.Remove($dup)
    $ancla = $newarr
    for ($i = 0; $i -lt $valores.Count; $i++) {
        foreach ($ins in @(
                $il.Create($Op::Dup),
                $il.Create($Op::Ldc_I4, [int]$i),
                $il.Create($Op::Ldc_I4, [int]$valores[$i]),
                $il.Create($Op::Stelem_I4))) {
            $il.InsertAfter($ancla, $ins)
            $ancla = $ins
        }
    }
    $cambiadas++
    Write-Output ("lista ampliada: " + ($valores -join ', '))
}
[Mono.Cecil.Rocks.MethodBodyRocks]::OptimizeMacros($cuerpo)

if ($yaEstaba -gt 0 -and $cambiadas -eq 0) {
    Write-Output "GW1 ya estaba en las $yaEstaba listas: no hay nada que cambiar."
    $asm.Dispose()
    exit 0
}
if ($cambiadas -ne 4) { throw "esperaba ampliar 4 listas y han sido $cambiadas; no se escribe nada" }
$asm.Write($Salida)
$asm.Dispose()
Write-Output "escrito $Salida ($((Get-Item $Salida).Length) bytes)"
