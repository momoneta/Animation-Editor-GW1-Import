@echo off
rem Instala el plugin y los dos DLL corregidos en una carpeta de Frosty Editor 1.0.7 (fork).
rem Uso: instalar.cmd "C:\ruta\a\Frosty"            (Frosty tiene que estar cerrado)
rem      instalar.cmd "C:\ruta\a\Frosty" restaurar   vuelve a dejar los originales
if "%~1"=="" (echo Uso: instalar.cmd "carpeta de Frosty" [restaurar] & exit /b 1)
set F=%~1
if not exist "%F%\FrostyEditor.exe" (echo No encuentro FrostyEditor.exe en %F% & exit /b 1)
cd /d "%~dp0"
if /i "%~2"=="restaurar" (
    for %%A in (FrostyModSupport.dll FrostySdk.dll Plugins\AssetBankPlugin.dll) do if exist "%F%\%%A.original" copy /y "%F%\%%A.original" "%F%\%%A" >nul
    echo Restaurados los originales.
    exit /b 0
)
for %%A in (FrostyModSupport.dll FrostySdk.dll Plugins\AssetBankPlugin.dll) do (
    if not exist "%F%\%%A.original" copy /y "%F%\%%A" "%F%\%%A.original" >nul
    copy /y "instalar\%%A" "%F%\%%A" >nul || (echo No se pudo copiar %%A: cierra Frosty. & exit /b 1)
)
echo Instalado. Los originales quedan como *.original junto a cada archivo.
