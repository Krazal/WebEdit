@echo off
setlocal
set XDS_BIN=xc
set XDS_PRJ=WebEditU
set PLUGIN=%XDS_PRJ:~0,-1%
if "%1" NEQ "" (
  set "XDS_PRJ=%1"
  set "PLUGIN=%1"
)
if /I "%2"=="C" (
  set XDS_BIN=xm
)
pushd %~dp0
xrc %XDS_PRJ%Ver.rc 2>NUL:
%XDS_BIN% =project %XDS_PRJ%.prj %XDS_OPTS%
echo F | xcopy /DIY %XDS_PRJ%.dll ..\%PLUGIN%.dll
popd
endlocal
exit /B %errorlevel%

@rem ===== old script begins here =====

@setlocal
rem Return non-zero exit code if compilation fails.
xc =p WebEdit.prj
set err=%errorlevel%
xc =p WebEditU.prj
set /a err^|=%errorlevel%

@echo off
mkdir obj 2> NUL
move *.obj obj > NUL
move *.sym obj > NUL
move tmp.lnk obj > NUL
exit /b %err%
