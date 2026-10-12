# Mira o pulsa controles de la ventana de Frosty Editor con UI Automation (sin mover el raton).
#   frosty_ui.ps1 ventanas                 lista las ventanas del proceso y sus botones/textos
#   frosty_ui.ps1 pulsar <AutomationId o nombre>   pulsa un boton (p. ej. LaunchButton)
param(
    [Parameter(Mandatory)] [string] $Orden,
    [string] $Control = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$AE = [Windows.Automation.AutomationElement]
$proc = Get-Process FrostyEditor -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { throw 'Frosty Editor no esta abierto' }
$raiz = $AE::RootElement
$porPid = New-Object Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
$ventanas = $raiz.FindAll([Windows.Automation.TreeScope]::Children, $porPid)

if ($Orden -eq 'ventanas') {
    foreach ($v in $ventanas) {
        Write-Output ("VENTANA '{0}' [{1}]" -f $v.Current.Name, $v.Current.ClassName)
        $tipos = @([Windows.Automation.ControlType]::Button, [Windows.Automation.ControlType]::Text)
        foreach ($t in $tipos) {
            $c = New-Object Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $t)
            $n = 0
            foreach ($e in $v.FindAll([Windows.Automation.TreeScope]::Descendants, $c)) {
                if ($e.Current.Name -or $e.Current.AutomationId) {
                    Write-Output ("   {0,-7} id='{1}' nombre='{2}'" -f $t.ProgrammaticName.Replace('ControlType.', ''), $e.Current.AutomationId, $e.Current.Name)
                    $n++
                    if ($n -ge 60) { break }
                }
            }
        }
    }
    exit 0
}
if ($Orden -eq 'pulsar') {
    foreach ($v in $ventanas) {
        $porId = New-Object Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $Control)
        $porNombre = New-Object Windows.Automation.PropertyCondition($AE::NameProperty, $Control)
        $e = $v.FindFirst([Windows.Automation.TreeScope]::Descendants, (New-Object Windows.Automation.OrCondition($porId, $porNombre)))
        if ($e) {
            $e.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
            Write-Output ("pulsado '{0}' en '{1}'" -f $Control, $v.Current.Name)
            exit 0
        }
    }
    throw "no encuentro el control '$Control'"
}
throw "orden desconocida: $Orden"
