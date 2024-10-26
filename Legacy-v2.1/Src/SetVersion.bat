@echo off
where sed 2>NUL:
if %errorlevel% NEQ 0 ( goto :EOF )
setlocal
set MAJ=2
set MIN=1
set REV=0
set BUILD=0
IF "%1" NEQ "" ( set "MAJ=%1" ) else ( goto :EOF )
IF "%2" NEQ "" ( set "MIN=%2" ) else ( goto :EOF )
IF "%3" NEQ "" ( set "REV=%3" ) else ( goto :EOF )
IF "%4" NEQ "" ( set "BUILD=%4" )
FOR /F "tokens=* USEBACKQ" %%F IN (`git ls-files -- "Src/*Ver.rc" "Src/*Ver.ob2"`) DO (
  sed -i -e "s/\([^0-9]*\)\(\([0-9]\([.,]\)\)\{1,4\}[0-9]\)\(.*\)/\1%MAJ%\4%MIN%\4%REV%\4%BUILD%\5/g" %%F
  unix2dos %%F
)
endlocal
:EOF
exit /B %errorlevel%

@rem ===== old script begins here =====

if "%1" == "" (
  echo Usage: %~nx0 Ver1 Ver2 [Ver3]
  echo Example: %~nx0 2 0
) else (
  if "%2" == "" (
    echo Error: Ver2 is missing. Run without parameters for help.
  ) else (
    if "%3" == "" (
      sed -e "1 s/ v[.0-9]* / v%1.%2 /" < WebEdit.txt > WebEdit.tmp
      sed -e "3 s/Version .*,/Version %1.%2,/" < Changelog.txt > Changelog.tmp
      sed -e "s@s/$Ver1/.*/@s/$Ver1/%1/@" -e "s@s/$Ver2/.*/@s/$Ver2/%2/@" -e "s@s/$Ver3/.*/@s/$Ver3/0/@" < WebEditVer > WebEditVer.tmp
      sed -e "s@s/$Ver1/.*/@s/$Ver1/%1/@" -e "s@s/$Ver2/.*/@s/$Ver2/%2/@" -e "s@s/$Ver3/.*/@s/$Ver3/0/@" < WebEditUVer > WebEditUVer.tmp
    ) else (
      sed -e "1 s/ v[.0-9]* / v%1.%2.%3 /" < WebEdit.txt > WebEdit.tmp
      sed -e "3 s/Version .*,/Version %1.%2.%3,/" < Changelog.txt > Changelog.tmp
      sed -e "s@s/$Ver1/.*/@s/$Ver1/%1/@" -e "s@s/$Ver2/.*/@s/$Ver2/%2/@" -e "s@s/$Ver3/.*/@s/$Ver3/%3/@" < WebEditVer > WebEditVer.tmp
      sed -e "s@s/$Ver1/.*/@s/$Ver1/%1/@" -e "s@s/$Ver2/.*/@s/$Ver2/%2/@" -e "s@s/$Ver3/.*/@s/$Ver3/%3/@" < WebEditUVer > WebEditUVer.tmp
    )
    del WebEdit.txt Changelog.txt WebEditVer WebEditUVer
    ren WebEdit.tmp WebEdit.txt
    ren Changelog.tmp Changelog.txt
    ren WebEditVer.tmp WebEditVer
    ren WebEditUVer.tmp WebEditUVer
  )
)
