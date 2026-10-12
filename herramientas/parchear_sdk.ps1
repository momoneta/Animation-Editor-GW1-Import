# Corrige FrostySdk.dll del fork 1.0.7 para PvZ Garden Warfare 1.
#
# DbWriter.Write pone la cabecera de los .toc que escribe Frosty (layout.toc y los de cada
# superbundle). Para BF4, GW2, DAI, NFS... usa el tipo 0x03CED100; el fork olvido GW1
# (DataVersion 20140225) y le pone 0x01CED100, el tipo firmado: el juego comprueba la firma, no
# la encuentra y se cierra a los dos segundos de arrancar. La 1.0.6.3 si incluye GW1.
#
# Uso: powershell -NoProfile -File parchear_sdk.ps1 -Entrada <FrostySdk.dll> -Salida <dll nuevo>
param(
    [Parameter(Mandatory)] [string] $Entrada,
    [Parameter(Mandatory)] [string] $Salida
)
$ErrorActionPreference = 'Stop'
$GW1 = 20140225
$CABECERA_FIRMADA = 30331136      # 0x01CED100
$CABECERA_GW = 63885568           # 0x03CED100
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
$tipo = $asm.MainModule.GetType('FrostySdk.IO.DbWriter')
$metodo = $tipo.Methods | Where-Object { $_.Name -eq 'Write' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.Name -eq 'DbObject' }
if (-not $metodo) { throw 'no encuentro DbWriter.Write(DbObject)' }

$cuerpo = $metodo.Body
[Mono.Cecil.Rocks.MethodBodyRocks]::SimplifyMacros($cuerpo)
$il = $cuerpo.GetILProcessor()
$Op = [Mono.Cecil.Cil.OpCodes]
$ins = @($cuerpo.Instructions)

if ($ins | Where-Object { $_.OpCode -eq $Op::Ldc_I4 -and $_.Operand -eq $GW1 }) {
    Write-Output 'GW1 ya esta en la lista de DbWriter: no hay nada que cambiar.'
    $asm.Dispose()
    exit 0
}
# ... DataVersion == X -> beq <tipo 03> ...; ldc 0x01CED100; br fin; <tipo 03>: ldc 0x03CED100
$firmada = $ins | Where-Object { $_.OpCode -eq $Op::Ldc_I4 -and $_.Operand -eq $CABECERA_FIRMADA }
$gw = $ins | Where-Object { $_.OpCode -eq $Op::Ldc_I4 -and $_.Operand -eq $CABECERA_GW }
$version = $ins | Where-Object { $_.OpCode -eq $Op::Call -and $_.Operand.Name -eq 'get_DataVersion' } | Select-Object -First 1
if (@($firmada).Count -ne 1 -or @($gw).Count -ne 1 -or -not $version) { throw 'DbWriter.Write no tiene la forma esperada; no se escribe nada' }

$il.InsertBefore($firmada, $il.Create($Op::Call, $version.Operand))
$il.InsertBefore($firmada, $il.Create($Op::Ldc_I4, [int]$GW1))
$il.InsertBefore($firmada, $il.Create($Op::Beq, $gw))
[Mono.Cecil.Rocks.MethodBodyRocks]::OptimizeMacros($cuerpo)
$asm.Write($Salida)
$asm.Dispose()
Write-Output "DbWriter.Write: GW1 escribe ahora la cabecera 0x03CED100. Escrito $Salida ($((Get-Item $Salida).Length) bytes)"
