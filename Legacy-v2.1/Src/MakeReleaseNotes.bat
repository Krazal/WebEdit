sed -e "1 c CHANGELOG" < Changelog.txt > Changelog.tmp
sed -e "$a\ " -e "$r Changelog.tmp" < WebEdit.txt > ReleaseNotes.%1.txt
del Changelog.tmp
