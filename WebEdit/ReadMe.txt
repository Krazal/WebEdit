WebEdit v2.8 - current (64-bit compatible) — New features overview

Note: This file contains two sections. The first section documents the current WebEdit v2.8 enhancements (added to the modern, 64-bit-capable plugin). The second section contains the original, unchanged legacy documentation for WebEdit v2.1 (32‑bit). The legacy section is preserved verbatim for historical/reference purposes.

v2.8 — New features (examples)
- 64-bit Notepad++ support and compatibility updates for modern Notepad++ builds.
- New tag escape sequences for dynamic insertion:
  - \u
    - Replaced with the current Windows user name (the user account running Notepad++).
    - Example tag line in `WebEdit.ini`:
      user=Author: \u|
  - \d:"format"
    - Replaced with the local date/time. An optional .NET-style date/time format string may be supplied in quotes.
    - Examples:
      - `datetime=Created: \d:"yyyy-MM-dd HH:mm:ss"` → inserts date/time in 24h ISO-like format.
- Small usability improvements:
  - Better handling of Unicode and long replacements.
  - Improved behavior for caret placement after tag replacement e.g. when multiple pipe characters are present.
  - Backwards-compatible with existing `WebEdit.ini` files.

Notes on using the new tags
- Place the tag definitions in the `[Tags]` section of your `WebEdit.ini` file the same way as before. The new escape sequences behave the same as existing escapes (they are expanded inside Replacement text).
- Examples to add to `WebEdit.ini`:
  - `user=Author: \u|`
  - `now=Date/Time: \d:"yyyy-MM-dd HH:mm:ss"`
  - `árv=Árvíztűrő tükörfúrógép` (Hungarian pangram with accented characters)
- If you rely on the legacy 32-bit plugin, see the legacy documentation below. The legacy plugin (v2.1) may be unchanged and is documented verbatim in the section that follows.

CREDITS
-------
This small freeware plugin allows you to wrap the selected text in tag pairs and expand abbreviations using a hotkey.
For more information visit https://github.com/Krazal/WebEdit

Created by Alexander Iljin (Amadeus IT Solutions) using XDS Oberon, March 2008 - March 2010.
Ported to C# by Miguel Febres, April 2021.
Ported to .NET 8 by Robert Di Pardo, February 2025.
Currently maintained by Richard Stockinger, September 2025.

----------------------------------------------------------------------
Legacy documentation (unchanged) — WebEdit v2.1 - freeware open-source plugin for 32-bit Notepad++ ANSI/Unicode.

WebEdit v2.1 - freeware open-source plugin for 32-bit Notepad++ ANSI/Unicode.

INTRODUCTION

This plugin allows you to define up to 30 commands. Each command will surround
the currently selected text with the Left and Right text defined for the
command. If there is no selection, the Left text is inserted to the left of
current caret position, and the Right text is inserted to the right. In any
case the selection and relative cursor position are preserved. Clipboard is
not used. Both text insertions can be undone/redone as a single action.

Also you can define any number of Tags to be replaced by the "Replace Tag"
with some Replacement text. This functionality is very similar to the
QuickText's "Replace Tag". I really liked and used QuickText extensively, but
unfortunately it is not very well supported lately, which was the reason for
adding the command to WebEdit.

The idea is that you can type a short abbreviation, e.g. "p", then press a
hotkey and have it replaced with a (possibly multiline) text of your choice.
Alt+Enter is the default shortcut. If all you need is a QuickText replacement,
then you can put a semicolon (";") before the [Commands] and [Toolbar] sections
in the sample WebEdit.ini, and then edit the [Tags] section to your liking.

INSTALLATION AND SETUP

Only the 32-bit Notepad++ is supported. There is no 64-bit version of WebEdit.
Before Notepad++ v6.0 there were two versions: ANSI and Unicode. Starting with
v6.0 and newer there is only the Unicode build of Notepad++. The installation
instructions below were written for Notepad++ before v7.6. Since v7.6 it comes
with a new plugin loading mechanism, so you should ignore the parts about the
manual installation and the "Plugin Manager" plugin in the following text.

The easiest way to install WebEdit is via the Plugin Manager plugin. Keep in
mind, though, that if you upgrade an existing installation of WebEdit, the
WebEdit.ini file will be replaced with the sample version. Don't worry: your
customized file is safely renamed WebEdit.ini.bak or WebEdit.ini.bak2, etc.
All you need to do is go to the Config folder and manually rename it back. As
usual, you can also use the "Edit Config" command to restore the file contents.

To install this plugin manually you have to copy two files:
- file 1: either WebEdit.dll (if you use Unicode version of Notepad++)
  or WebEdit-ansi.dll (for the ANSI version) to Notepad++\plugins\ folder;
- file 2: WebEdit.ini to Notepad++\plugins\Config\ folder.
Both folders are to be found either in "Program Files" or in "Application Data"
of your current user, depending on whether you installed Notepad++ in the
Portable mode or not. (Hint: to determine whether Notepad++ is installed in
Portable mode, check for the existence of "doLocalConf.xml" file in the
Notepad++ folder. If the file is present, Portable mode is in effect,
otherwise it is not.) If the WebEdit.dll file is not installed, you will not
see the "WebEdit" menu under the Plugins menu. If the WebEdit.ini file is not
installed, you will see that the Plugins - WebEdit menu consists entirely of
disabled items.
The Config folder of the distribution package contains a sample WebEdit.ini
and a sample set of bitmaps for the toolbar buttons.

The WebEdit plugin allows you to create commands to wrap the selected text in
tags. The commands are to be defined in the "[Commands]" section of the
WebEdit.ini file. Ini-file format:
- a character with code below 32 (space) terminates a line (i.e. end-of-line
  sequence does not matter), the only exception is the tab character;
- line length must not exceed 2046 characters (if a longer line is encountered,
  file processing is aborted at that point);
- any line starting with a semicolon character (;) is considered a comment and
  ignored;
- the file may contain any number of any sections. A section is a line of the
  following format: "[" <section name> "]". Only the sections with supported
  names are processed in order of appearance, all others are ignored.

You can edit the ini-file in Notepad++ by selecting "Edit Config" command from
Plugins - WebEdit menu. Edit the file, save it, and update the plugin
configuration by selecting "Load Config" command from the Plugins - WebEdit
menu (you don't have to restart Notepad++, but note that toolbar is only
updated on startup).

The "[Commands]" section format:
- if you want a line to contain a non-printable character, you must escape it
  with a backslash; the following escape sequences are supported:
  - \t = 09 - tab character;
  - \n = 10 - line feed (LF) character;
  - \r = 13 - carriage return (CR) character;
  - \\ = "\" - double backslash is replaced with a single backslash. This is
    required if you don't want "\t", "\n", etc. to be escaped: just write
    "\\t" and you will get "\t";
  - any other characters after the backslash are not escaped, e.g. "\a" = "\a";
- the minimum valid line is "=|" any line not containing "=" and "|" after it
  is ignored;
- line syntax: [menu item] "=" [Left text] "|" [Right text], for example:
  "Paragraph=<p>|</p>";
- syntax explanation:
  - [menu item] is the text displayed in the plugin menu ("Paragraph"));
  - [Left text] is the text inserted to the left of current selection ("<p>");
  - [Right text] is the text inserted to the right of current selection ("</p>");
  - all of the above strings are optional;
  - limitations: you can't use "=" in the menu text, and you can't use "|" in
    the Left text.
At most 30 commands may be defined in the "[Commands]" section, all superfluous
commands are ignored. The defined commands are listed in the plugin menu in the
same order they appear in the file. Unused menu slots appear as disabled and
show text "WebEdit Slot XX", where XX is the number of the slot. The first slot
number is "01".

The "[Toolbar]" section determines which of the command slots will be placed on
the toolbar. Syntax: <slot number> "=" <file name>
Example: 1=Paragraph.bmp
Slot numbers are 1..30. The bitmap file should be placed in the plugins/Config
folder and should contain a bitmap image suitable for the toolbar.

To display custom toolbar buttons Notepad++ should be configured to use "Small
standard icons" (it is by default). To find this option go to the "Settings - 
Preferences..." menu, and in the "Global" tab look for the "Tool bar" group.

The "[Tags]" section format:
- if you want a line to contain a non-printable character, you must escape it
  with a backslash; the following escape sequences are supported:
  - \c - replaced with the current clipboard contents;
  - \f[FileName:Section] - insert contents of an ini-file section. The file
    name should be relative to the Config folder. If "FileName" part is
    missing, then "WebEdit.ini" is assumed. If ":Section" part is missing,
    then the entire file is inserted;
  - \i - add one level of indentation (same as pressing the Tab key);
  - \n - new line, replaced with the current document's EOL sequence;
  - \t - tab character;
  - \| = "|" - use it if you want to insert the "pipe" character;
  - \\ = "\" - double backslash is replaced with a single backslash. This is
    required if you need a backslash to appear in the Replacement text;
  - any other escaped character will be simply removed from the string;
- line syntax: <Tag> "=" <Replacement>, for example: "p=PROCEDURE | ;\n";
- syntax explanation:
  - <Tag> is the text in the document to be replaced;
  - <Replacement> is the text to be inserted instead of the Tag;
  - limitations: Tag can only contain character a..z, A..Z and 0..9. The Tag
    can be 1 to 32 characters long. Total length of a line is subject to the
    global ini-file limitation of 2046 characters. This means that the
    Replacement text can contain 2013..2044 characters, depending on the
    length of the corresponding Tag text. If a line is too long, it will be
    truncated. If a line does not adhere to the rest of the restrictions, it
    will be skipped;
  - since '\' character is not allowed in the Tag, only the Replacement text
    can contain escape sequences and other special characters;
  - the pipe character "|" plays a special role in the Replacement text: it
    marks the position of the caret after tag replacement. All pipe characters
    will be removed from the Replacement text, unless prefixed with a '\', but
    only one of them (the last one) will mark the caret position.

You can assign shortcuts to the plugin menu items via Settings - Shortcut
Mapper - Plugin commands. If you are running Notepad++ 5.0 and later, you will
see the actual command names, i.e. instead of the dummy text "WebEdit Slot XX"
you will see "WebEdit - Command name", where the "Command name" is the text as
displayed in the plugin menu. Due to insufficient support of the dynamic menus
by Notepad++ the text in the Shortcut Mapper will only be updated on restart,
even though you can change the command at run-time using the "Load Config"
command.

Hint: if you want to set a menu accelerator, use the ampersand character (&) in
the menu item text. For example: "&Paragraph tag=<p>|</p>". The character after
the ampersand ("P" in this case) will be underlined in the plugin menu, which
makes it possible to simply type "p" on the keyboard instead of selecting the
menu item with mouse or cursor keys. The accelerator, of course, will only work
when the plugin menu is displayed (press "Alt+p, w" to display the "Plugins -
WebEdit" menu).

PS: If you feel that the limit of 30 functions maximum is too constricting, you
can painlessly increase the number up to 99, but you will have to recompile the
plugin. See instructions in the WebEdit.ob2 source file.

AUTHOR

The WebEdit plugin was created by Alexander Iljin (Amadeus IT Solutions) on
March 2008 - March 2010.
Project page with contact information:
https://www.notion.so/abb42c4224f245a9a678f983c30d258c
Support me on Patreon: https://www.patreon.com/alexilin
Or on Flattr: https://flattr.com/@alex.ilin
