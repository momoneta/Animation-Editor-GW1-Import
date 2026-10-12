# Descompila ensamblados .NET a proyectos C# con el ICSharpCode.Decompiler que trae Visual Studio.
# Uso: powershell -NoProfile -File descompilar.ps1 -Salida <carpeta> -Buscar <dir1>,<dir2> -Dll <a.dll>,<b.dll>
param(
    [Parameter(Mandatory)] [string] $Salida,
    [Parameter(Mandatory)] [string[]] $Dll,
    [string[]] $Buscar = @()
)
$ErrorActionPreference = 'Stop'
# Con -File las listas llegan como una sola cadena separada por comas.
$Dll = $Dll | ForEach-Object { $_ -split ',' }
$Buscar = $Buscar | ForEach-Object { $_ -split ',' }
$vs = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE'
$dirs = @(
    "$vs\CommonExtensions\Microsoft\VBCSharp\LanguageServices",
    "$vs\PrivateAssemblies",
    "$vs\PublicAssemblies",
    $vs
)

# El resolvedor va en C#: el descompilador trabaja en hilos sin runspace de PowerShell.
Add-Type @"
using System; using System.IO; using System.Reflection;
public static class Resolvedor {
    public static string[] Dirs;
    public static void Instalar() {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            string n = new AssemblyName(e.Name).Name + ".dll";
            foreach (string d in Dirs) {
                string p = Path.Combine(d, n);
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            return null;
        };
    }
}
"@
[Resolvedor]::Dirs = $dirs
[Resolvedor]::Instalar()
[void][Reflection.Assembly]::LoadFrom("$($dirs[0])\ICSharpCode.Decompiler.dll")

foreach ($ruta in $Dll) {
    $ruta = (Resolve-Path -LiteralPath $ruta).Path
    $nombre = [IO.Path]::GetFileNameWithoutExtension($ruta)
    $destino = Join-Path $Salida $nombre
    New-Item -ItemType Directory -Force $destino | Out-Null

    $modulo = New-Object ICSharpCode.Decompiler.Metadata.PEFile($ruta)
    $marco = [ICSharpCode.Decompiler.Metadata.DotNetCorePathFinderExtensions]::DetectTargetFrameworkId($modulo)
    $res = New-Object ICSharpCode.Decompiler.Metadata.UniversalAssemblyResolver($ruta, $false, $marco)
    foreach ($b in $Buscar) { $res.AddSearchDirectory($b) }

    $dec = New-Object ICSharpCode.Decompiler.CSharp.ProjectDecompiler.WholeProjectDecompiler($res)
    $dec.Settings.ThrowOnAssemblyResolveErrors = $false
    [void]$dec.DecompileProject($modulo, $destino, [Threading.CancellationToken]::None)
    $n = (Get-ChildItem $destino -Recurse -Filter *.cs).Count
    Write-Output "$nombre ($marco): $n archivos .cs -> $destino"
}
