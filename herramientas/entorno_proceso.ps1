# Lee variables de entorno de otro proceso (del mismo usuario) desde su PEB, solo lectura.
# Sirve para saber si la EA app o el juego tienen GAME_DATA_DIR, que es de donde GW1 saca la
# carpeta de datos cuando no recibe -dataPath.
# Uso: powershell -NoProfile -File entorno_proceso.ps1 -Nombre EADesktop [-Variable GAME_DATA_DIR]
param(
    [Parameter(Mandatory)] [string] $Nombre,
    [string] $Variable = 'GAME_DATA_DIR'
)
$ErrorActionPreference = 'Stop'
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class EntornoProceso {
    [StructLayout(LayoutKind.Sequential)]
    struct PBI { public IntPtr Reserved1; public IntPtr PebBaseAddress; public IntPtr Reserved2a; public IntPtr Reserved2b; public IntPtr UniqueProcessId; public IntPtr Reserved3; }
    [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr h, int c, ref PBI i, int l, out int r);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(int a, bool i, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr a, byte[] b, IntPtr n, out IntPtr r);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    static byte[] Leer(IntPtr h, long dir, int n) {
        byte[] b = new byte[n]; IntPtr r;
        if (!ReadProcessMemory(h, new IntPtr(dir), b, new IntPtr(n), out r)) throw new Exception("ReadProcessMemory " + Marshal.GetLastWin32Error());
        return b;
    }
    public static string Leer(int pid) {
        IntPtr h = OpenProcess(0x0410, false, pid);   // QUERY_INFORMATION | VM_READ
        if (h == IntPtr.Zero) throw new Exception("OpenProcess " + Marshal.GetLastWin32Error());
        try {
            PBI pbi = new PBI(); int ret;
            if (NtQueryInformationProcess(h, 0, ref pbi, Marshal.SizeOf(typeof(PBI)), out ret) != 0) throw new Exception("NtQueryInformationProcess");
            long parametros = BitConverter.ToInt64(Leer(h, pbi.PebBaseAddress.ToInt64() + 0x20, 8), 0);
            long entorno = BitConverter.ToInt64(Leer(h, parametros + 0x80, 8), 0);
            long tam = BitConverter.ToInt64(Leer(h, parametros + 0x3F0, 8), 0);
            if (tam <= 0 || tam > 0x200000) tam = 0x10000;
            return Encoding.Unicode.GetString(Leer(h, entorno, (int)tam));
        } finally { CloseHandle(h); }
    }
}
"@
foreach ($p in Get-Process -Name $Nombre -ErrorAction SilentlyContinue) {
    try {
        $bloque = [EntornoProceso]::Leer($p.Id)
        $vars = $bloque.Split([char]0) | Where-Object { $_ -like '*=*' }
        $hallada = $vars | Where-Object { $_ -like "$Variable=*" }
        if ($hallada) { Write-Output ("{0} (pid {1}, iniciado {2:HH:mm:ss}): {3}" -f $p.ProcessName, $p.Id, $p.StartTime, $hallada) }
        else { Write-Output ("{0} (pid {1}, iniciado {2:HH:mm:ss}): {3} NO esta definida ({4} variables)" -f $p.ProcessName, $p.Id, $p.StartTime, $Variable, $vars.Count) }
    } catch {
        Write-Output ("{0} (pid {1}): no se puede leer: {2}" -f $p.ProcessName, $p.Id, $_.Exception.Message)
    }
}
