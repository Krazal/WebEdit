@echo off
echo Removing whitespace at EOLs...
for /d %%d in (*) do (
  pushd %%d
  for /r %%f in (*.cs,*.resx,*.targets,*.config) do (
    echo %%f
    sed -i -r -e "s/[\t\r ]+$//" -e "s/$/\r/" "%%f"
  )
  popd
)
