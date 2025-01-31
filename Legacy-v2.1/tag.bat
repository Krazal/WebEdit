@echo off
set projectPath=svn://agesoft.homeip.net/Amadeus/Npp/WebEdit
set projectName=WebEdit
if "%1" NEQ "" goto start
echo Tag - create a tag of the current branch. The script does NOT set the version number.
echo Usage: "tag <version-to-tag-as>"
echo Example: tag 2.0.2
goto :eof

:start
svn update
::if %errorlevel% NEQ 0 goto :Svnfailed
:: set FileFlags=0, Ver4=0 in the version information
:: - in the ANSI build
sed -e "s@s/$FileFlags/.*/@s/$FileFlags/0/@" -e "s@s/$Ver4/999/@s/$Ver4/0/@" < Src\%projectName%Ver > Src\%projectName%Ver.tmp
del Src\%projectName%Ver
ren Src\%projectName%Ver.tmp %projectName%Ver

:: - in the Unicode build
sed -e "s@s/$FileFlags/.*/@s/$FileFlags/0/@" -e "s@s/$Ver4/999/@s/$Ver4/0/@" < Src\%projectName%UVer > Src\%projectName%UVer.tmp
del Src\%projectName%UVer
ren Src\%projectName%UVer.tmp %projectName%UVer

:: set release date in the Changelog
set ReleaseDate=%DATE%
sed -e "s/$ReleaseDate/%ReleaseDate%/" < Src\Changelog.txt > Src\Changelog.txt.tmp
del Src\Changelog.txt
ren Src\Changelog.txt.tmp Changelog.txt

:: make release notes
cd Src
call MakeReleaseNotes.bat v%1
cd ..

:: make sure the project compiles and passes all tests
make
if %errorlevel% NEQ 0 goto Makefailed
make test
if %errorlevel% NEQ 0 goto Makefailed

:: create distribution package with version number in zip-file name and md5-file
make dist -C Src
if %errorlevel% NEQ 0 goto Makefailed
move Src\%projectName%.zip Src\%projectName%.v%1.zip
sed -e "s/\*%projectName%\.zip/*%projectName%.v%1.zip/" Src\%projectName%.zip.md5 > Src\%projectName%.v%1.zip.md5
del Src\%projectName%.zip.md5

:: create the tag
svn copy . %projectPath%/tags/%1 -m "Tagged %projectName% v%1 release."
if %errorlevel% NEQ 0 goto :Svnfailed
svn revert Src\%projectName%Ver Src\%projectName%UVer
if %errorlevel% NEQ 0 goto :Svnfailed
echo The tag was created successfully.
goto :eof

:Makefailed
echo Make failed - tag was not created
goto :eof

:Svnfailed
echo SVN command failed - see error message above
goto :eof
