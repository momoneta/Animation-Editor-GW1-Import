# Abre Frosty Editor y elige el perfil del juego en la ventana inicial con UI Automation
# (sin mover el raton). Despues hay que esperar a que cargue el perfil.
# Si Frosty ya esta abierto en la ventana de perfiles, se usa ese en vez de abrir otro.
# Uso: powershell -NoProfile -File abrir_frosty.ps1 [-Frosty <carpeta>] [-Perfil <texto del nombre del juego>] [-Excluir <texto>]
param(
    [string] $Frosty = 'C:\Frosty',
    [string] $Perfil = 'Garden Warfare',
    [string] $Excluir = 'Warfare 2'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$AE = [Windows.Automation.AutomationElement]
$Arbol = [Windows.Automation.TreeScope]
function PorId($id) { New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id) }

$p = Get-Process FrostyEditor -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p) { Write-Output "FrostyEditor ya estaba abierto, pid $($p.Id)" }
else {
    $p = Start-Process -FilePath (Join-Path $Frosty 'FrostyEditor.exe') -WorkingDirectory $Frosty -PassThru
    Write-Output "FrostyEditor iniciado, pid $($p.Id)"
}

$porPid = New-Object Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $p.Id)
$boton = $null
$ventana = $null
for ($i = 0; $i -lt 60 -and -not $boton; $i++) {
    Start-Sleep -Milliseconds 500
    foreach ($v in $AE::RootElement.FindAll($Arbol::Children, $porPid)) {
        $b = $v.FindFirst($Arbol::Descendants, (PorId 'LaunchConfigurationButton'))
        if ($b) { $boton = $b; $ventana = $v; break }
    }
}
if (-not $boton) { throw 'no aparece la ventana inicial de Frosty (lista de perfiles)' }

# Las filas no exponen su texto: se selecciona cada una y se lee el nombre del juego que
# la propia ventana muestra al lado (ProfileNameTextBlock / ProfilePathTextBlock).
$filas = @()
for ($i = 0; $i -lt 20 -and $filas.Count -eq 0; $i++) {
    $filas = @($ventana.FindAll($Arbol::Descendants, (New-Object Windows.Automation.PropertyCondition($AE::IsSelectionItemPatternAvailableProperty, $true))))
    if ($filas.Count -eq 0) { Start-Sleep -Milliseconds 500 }
}
$elegido = $false
foreach ($fila in $filas) {
    $fila.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 400
    $nombre = $ventana.FindFirst($Arbol::Descendants, (PorId 'ProfileNameTextBlock'))
    $ruta = $ventana.FindFirst($Arbol::Descendants, (PorId 'ProfilePathTextBlock'))
    $texto = (@($nombre, $ruta) | Where-Object { $_ } | ForEach-Object { $_.Current.Name }) -join ' | '
    Write-Output "perfil: $texto"
    if ($texto -like "*$Perfil*" -and ($Excluir -eq '' -or $texto -notlike "*$Excluir*")) { $elegido = $true; break }
}
if (-not $elegido) { throw "ningun perfil contiene '$Perfil'" }
$boton.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'perfil seleccionado; Frosty esta cargando'
