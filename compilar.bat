@echo off
chcp 65001 >nul
title Compilar ColorTags
echo ======================================
echo       COMPILADOR DO COLORTAGS
echo ======================================
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERRO: .NET 8 SDK nao encontrado.
  echo Instale em: https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
cd /d "%~dp0ColorTags"
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo Falha ao compilar.
  pause
  exit /b 1
)
echo.
echo Executavel criado em:
echo %~dp0ColorTags\bin\Release\net8.0-windows\win-x64\publish\ColorTags.exe
pause
