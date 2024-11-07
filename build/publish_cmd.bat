echo off

echo ==================
echo Publish gView.Cmd
echo ==================

cd .\..\src\gView.Cmd

echo Windows
dotnet publish -c Release -p:PublishProfile=win64
if errorlevel 1 goto error

echo Linux
dotnet publish -c Release -p:PublishProfile=linux64
if errorlevel 1 goto error

echo ==================
echo Publish Successful
echo ==================

goto end

:error
echo *****************
echo An error occurred
echo *****************

pause

:end

cd .\..\..\build