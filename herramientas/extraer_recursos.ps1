# Saca de un ensamblado WPF su bloque <Nombre>.g.resources (BAML e imagenes) tal cual,
# para volver a incrustarlo al recompilar con csc (-resource:...).
# Uso: powershell -NoProfile -File extraer_recursos.ps1 -Dll <ensamblado.dll> -Salida <carpeta>
param(
    [Parameter(Mandatory)] [string] $Dll,
    [Parameter(Mandatory)] [string] $Salida
)
$ErrorActionPreference = 'Stop'
$Dll = (Resolve-Path -LiteralPath $Dll).Path
New-Item -ItemType Directory -Force $Salida | Out-Null
# Se carga desde bytes para no dejar el archivo bloqueado.
$asm = [Reflection.Assembly]::ReflectionOnlyLoad([IO.File]::ReadAllBytes($Dll))
foreach ($nombre in $asm.GetManifestResourceNames()) {
    $s = $asm.GetManifestResourceStream($nombre)
    $destino = Join-Path $Salida $nombre
    $f = [IO.File]::Create($destino)
    $s.CopyTo($f)
    $f.Dispose()
    Write-Output ("{0}: {1} bytes" -f $nombre, (Get-Item $destino).Length)
    if ($nombre -like '*.g.resources') {
        $s.Position = 0
        $r = New-Object System.Resources.ResourceReader($s)
        foreach ($e in $r) { Write-Output ("   " + $e.Key) }
        $r.Dispose()
    }
    $s.Dispose()
}
