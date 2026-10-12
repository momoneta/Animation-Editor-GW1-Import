@echo off
rem Recompila AssetBankPlugin.dll (Animation Editor con importacion para PvZ GW1/GW2) con el csc
rem de Visual Studio contra los DLL del Frosty 1.0.7 de Desktop\Final.
rem   bin\AssetBankPlugin.dll   el plugin
rem   bin\prueba.exe            banco de pruebas sin Frosty (ver pruebas\Prueba.cs)
rem   bin\extraer.exe           saca recursos del juego sin abrir Frosty (lanzarlo desde la carpeta de Frosty)
cd /d "%~dp0"
if not exist bin mkdir bin
set CSC="C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
%CSC% @compilar.rsp
if errorlevel 1 exit /b 1
%CSC% @pruebas\compilar.rsp
if errorlevel 1 exit /b 1
%CSC% @pruebas\extraer.rsp
