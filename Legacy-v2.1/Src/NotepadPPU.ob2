MODULE NotepadPPU;

(* --------------------------------------------------------------------------------
 * (C) 2008 - 2010 by Alexander Iljin
 * -------------------------------------------------------------------------------- *)

IMPORT SYSTEM,Win:=Windows,Sci:=Scintilla,StrU;

(** --------------------------------------------------------------------------------
  * This module provides plugin interface for the Unicode version of Notepad++.
  * -------------------------------------------------------------------------------- *)

CONST
   (* Default max number of menu items to be exposed by a plugin. *)
   DefNumMenuItems* = 16;
   MenuItemNameLength* = 64;

   (* Notepad++ notification codes *)
   NPPN_FIRST          = 1000;
   NPPN_READY          = NPPN_FIRST + 1;
   NPPN_TBMODIFICATION = NPPN_FIRST + 2;

   (* Notepad++ command codes *)
   NPPMSG = Win.WM_USER + 1000;
   NPPM_GETCURRENTSCINTILLA = NPPMSG + 4;
   NPPM_ADDTOOLBARICON      = NPPMSG + 41;
   NPPM_GETPLUGINCONFIGDIR  = NPPMSG + 46;
   NPPM_GETNPPVERSION       = NPPMSG + 50;
   (* Added in v7.6: https://github.com/notepad-plus-plus/notepad-plus-plus/commit/a87e89e *)
   NPPM_GETPLUGINHOMEPATH   = NPPMSG + 97;
   (* Added in v8.0: https://community.notepad-plus-plus.org/post/68808 *)
   NPPM_ADDTOOLBARICON_FORDARKMODE = NPPMSG + 101;

   SCINTILLA_USER           = Win.WM_USER + 2000;
   NPPM_DOOPEN              = SCINTILLA_USER + 8;

   RUNCOMMAND_USER          = Win.WM_USER + 3000;
   NPPM_GETFULLCURRENTPATH  = RUNCOMMAND_USER + 1;
   NPPM_GETCURRENTDIRECTORY = RUNCOMMAND_USER + 2;
   NPPM_GETFILENAME         = RUNCOMMAND_USER + 3;
   NPPM_GETNAMEPART         = RUNCOMMAND_USER + 4;
   NPPM_GETEXTPART          = RUNCOMMAND_USER + 5;
   NPPM_GETCURRENTWORD      = RUNCOMMAND_USER + 6;
   NPPM_GETNPPDIRECTORY     = RUNCOMMAND_USER + 7;

TYPE
   Char* = StrU.Char;
   Handle = Win.HWND;
   Shortcut* = POINTER TO ShortcutDesc;
   ShortcutDesc = RECORD
      ctrl* : BOOLEAN;
      alt*  : BOOLEAN;
      shift*: BOOLEAN;
      key*  : CHAR;
   END;

   Function* = PROCEDURE ['C'];
   MenuItem = RECORD
      itemName: ARRAY MenuItemNameLength OF Char;
      pFunc   : Function;
      cmdID   : LONGINT;
      initChk : LONGINT;
      shortcut: Shortcut;
   END;

   SCNotification = RECORD
      nmhdr: Win.NMHDR;
      (* other fields are not used in this plugin *)
   END;

   (* Used with NPPM_ADDTOOLBARICON_FORDARKMODE *)
   ToolbarIconsDarkMode = RECORD
      bmp: Win.HBITMAP;
      ico: Win.HICON;
      icodm: Win.HICON;
   END;

   (* Used with NPPM_ADDTOOLBARICON *)
   ToolbarIcons = RECORD
      bmp: Win.HBITMAP;
      ico: Win.HICON;
   END;

VAR
   handle-: Handle;
   scintillaMain-: Sci.Handle;
   scintillaSecond-: Sci.Handle;
   PluginName*: ARRAY MenuItemNameLength OF Char;
   MI: POINTER TO ARRAY OF MenuItem;
   menuItemInfo: POINTER TO ARRAY OF RECORD
      tbi: ToolbarIcons;
      tbdm: ToolbarIconsDarkMode;
      enabled: BOOLEAN;
   END;
   lastItem: INTEGER;
   menuLocked: BOOLEAN; (* this internal flag is set to FALSE when you can no
   * longer add new items to the menu (you can still modify existing ones). *)
   toolbarLocked: BOOLEAN; (* the same flag for the toolbar modification *)
   onSetInfo*: PROCEDURE; (** onSetInfo is called as soon as the main handle
   * values are supplied by Notepad++. Before version 5.0 the setInfo function
   * was called after getFuncsArray, which made it impossible to load the menu
   * contents from an ini-file, because the plugin did not have the handle to
   * call GetPluginConfigDir. From version 5.0 on the setInfo is called right
   * after the plugin is loaded into memory, before any other functions, so
   * you may use it the perform the early initialization and prepare the menu
   * contents for the subsequent call to getFuncsArray. Note, however, that if
   * you want to make your plugin compatible with pre-5.0 versions, you should
   * be prepared to return something meaningful to getFuncsArray in any case.
   *)
   onReady*: PROCEDURE; (** onReady is called when Notepad++ has finished its
   * startup sequence, loaded all plugins, etc. This is when you should
   * perform your initialization, unless you have special requirements. *)

PROCEDURE GetCurrentScintilla* (): Sci.Handle;
(** Return handle of the currently active Scintilla view or NIL on error. *)
VAR res: LONGINT;
BEGIN
   res := 0;
   Win.SendMessage (handle, NPPM_GETCURRENTSCINTILLA, 0, SYSTEM.ADR (res));
   IF res = 0 THEN
      RETURN scintillaMain
   ELSE
      RETURN scintillaSecond
   END;
END GetCurrentScintilla;

PROCEDURE GetPluginConfigDir* (VAR dir: ARRAY OF Char);
BEGIN
   dir [0] := 0;
   Win.SendMessage (handle, NPPM_GETPLUGINCONFIGDIR, LEN (dir), SYSTEM.ADR (dir))
END GetPluginConfigDir;

PROCEDURE GetPluginsHomeDir* (VAR dir: ARRAY OF Char);
BEGIN
   dir [0] := 0;
   Win.SendMessage (handle, NPPM_GETPLUGINHOMEPATH, LEN (dir), SYSTEM.ADR (dir))
END GetPluginsHomeDir;

PROCEDURE GetFullCurrentPath* (VAR res: ARRAY OF Char);
BEGIN Win.SendMessage (handle, NPPM_GETFULLCURRENTPATH, 0, SYSTEM.ADR (res))
END GetFullCurrentPath;

PROCEDURE GetCurrentDir* (VAR res: ARRAY OF Char);
BEGIN Win.SendMessage (handle, NPPM_GETCURRENTDIRECTORY, 0, SYSTEM.ADR (res))
END GetCurrentDir;

PROCEDURE GetFileName* (VAR res: ARRAY OF Char);
BEGIN Win.SendMessage (handle, NPPM_GETFILENAME, 0, SYSTEM.ADR (res))
END GetFileName;

PROCEDURE GetNamePart* (VAR res: ARRAY OF Char);
BEGIN Win.SendMessage (handle, NPPM_GETNAMEPART, 0, SYSTEM.ADR (res))
END GetNamePart;

PROCEDURE GetExtPath* (VAR res: ARRAY OF Char);
BEGIN Win.SendMessage (handle, NPPM_GETEXTPART, 0, SYSTEM.ADR (res))
END GetExtPath;

PROCEDURE GetCurrentWord* (VAR res: ARRAY OF Char);
BEGIN Win.SendMessage (handle, NPPM_GETCURRENTWORD, 0, SYSTEM.ADR (res))
END GetCurrentWord;

PROCEDURE GetNppDir* (VAR res: ARRAY OF Char);
BEGIN Win.SendMessage (handle, NPPM_GETNPPDIRECTORY, 0, SYSTEM.ADR (res))
END GetNppDir;

PROCEDURE OpenFile* (fname: ARRAY OF Char): BOOLEAN;
BEGIN RETURN Win.SendMessage (handle, NPPM_DOOPEN, 0, SYSTEM.ADR (fname)) # 0
END OpenFile;

PROCEDURE AddMenuItem* (VAR name: ARRAY OF Char; func: Function; initChk: BOOLEAN; shortcut: Shortcut);
(** Add menu item with text name, calling function func. If initChk, the menu item should
  * have a check mark initially. You can't add new items after onReady event was signaled. *)
BEGIN
   ASSERT (~menuLocked, 20);
   StrU.Copy (name, MI [lastItem].itemName);
   MI [lastItem].pFunc := func;
   MI [lastItem].cmdID := 0;
   MI [lastItem].initChk := ORD (initChk);
   MI [lastItem].shortcut := shortcut;
   INC (lastItem)
END AddMenuItem;

PROCEDURE SetNumMenuItems* (num: INTEGER);
(** If you plugin wants to export more than DefNumMenuItems menu items (including
  * separators), you should set the desired number using this procedure. Do it before
  * your first call to AddMenuItem or AddMenuSeparator. *)
BEGIN
   ASSERT (lastItem = 0, 20); (* assert AddMenuItem was not used yet *)
   ASSERT (~menuLocked, 21);
   NEW (MI, num);
   NEW (menuItemInfo, num);
   REPEAT
      DEC (num);
      menuItemInfo [num].enabled := TRUE
   UNTIL num = 0;
END SetNumMenuItems;

PROCEDURE GetMenu* (): Win.HMENU;
BEGIN RETURN Win.GetMenu (handle)
END GetMenu;

PROCEDURE SetMenuItemName* (index: INTEGER; VAR name: ARRAY OF Char);
(** Set text for the menu item number index. If the name string is too long,
  * it gets truncated. *)
VAR
   hMenu: Win.HMENU;
   i, c: SHORTINT;
BEGIN
   ASSERT ((0 <= index) & (index < lastItem), 20);
   IF menuLocked THEN
      hMenu := GetMenu ();
      Win.ModifyMenuW (hMenu, MI [index].cmdID, SYSTEM.VAL (SYSTEM.CARD32, Win.MF_BYCOMMAND + Win.MF_STRING),
         MI [index].cmdID, SYSTEM.VAL (Win.PCWSTR, SYSTEM.ADR (name))
      )
   ELSE
      i := 0;
      c := MenuItemNameLength - 1;
      WHILE (i < c) & (name [i] # 0) DO
         MI [index].itemName [i] := name [i];
         INC (i)
      END;
      MI [index].itemName [i] := 0
   END
END SetMenuItemName;

PROCEDURE EnableMenuItem* (index: INTEGER; enable: BOOLEAN);
(** Set the "enabled" status of the index'th menu item. *)
VAR hMenu: Win.HMENU;
BEGIN
   ASSERT ((0 <= index) & (index < lastItem), 20);
   IF menuLocked THEN
      hMenu := GetMenu ();
      IF enable THEN
         Win.EnableMenuItem (hMenu, MI [index].cmdID, Win.MF_BYCOMMAND + Win.MF_ENABLED)
      ELSE
         Win.EnableMenuItem (hMenu, MI [index].cmdID, Win.MF_BYCOMMAND + Win.MF_GRAYED + Win.MF_DISABLED)
      END;
   ELSE
      menuItemInfo [index].enabled := enable
   END
END EnableMenuItem;

PROCEDURE CheckMenuItem* (index: INTEGER; check: BOOLEAN);
(** Set the "checked" status of the index'th menu item. *)
VAR hMenu: Win.HMENU;
BEGIN
   ASSERT ((0 <= index) & (index < lastItem), 20);
   IF menuLocked THEN
      hMenu := GetMenu ();
      IF check THEN
         Win.CheckMenuItem (hMenu, MI [index].cmdID, Win.MF_BYCOMMAND + Win.MF_CHECKED);
      ELSE
         Win.CheckMenuItem (hMenu, MI [index].cmdID, Win.MF_BYCOMMAND + Win.MF_UNCHECKED);
      END;
   ELSE
      MI [index].initChk := ORD (check);
   END
END CheckMenuItem;

PROCEDURE MenuItemChecked* (index: INTEGER): BOOLEAN;
(** Return the "checked" status of the index'th menu item. *)
VAR
   hMenu: Win.HMENU;
   info: Win.MENUITEMINFO;
BEGIN
   ASSERT ((0 <= index) & (index < lastItem), 20);
   IF menuLocked THEN
      hMenu := GetMenu ();
      info.cbSize := SIZE (info);
      info.fMask := Win.MIIM_STATE;
      RETURN Win.GetMenuItemInfo (hMenu, MI [index].cmdID, FALSE, info)
         & (Win.MFS_CHECKED * info.fState # Win.MF_SET {})
   ELSE
      RETURN MI [index].initChk # 0
   END
END MenuItemChecked;

PROCEDURE MenuItemToToolbar* (index: INTEGER; bmp: Win.HBITMAP; ico: Win.HICON; icodm: Win.HICON);
(** Show the index'th menu item on toolbar using either a bitmap or an icon.
  * You can put an item on the toolbar at any time, but you can't remove it. *)
BEGIN
   ASSERT (~toolbarLocked, 20);
   ASSERT ((0 <= index) & (index < LEN (menuItemInfo^)), 21);
   IF icodm = NIL THEN
     icodm := ico
   END;
   menuItemInfo [index].tbdm.bmp := bmp;
   menuItemInfo [index].tbdm.ico := ico;
   menuItemInfo [index].tbdm.icodm := icodm;
   menuItemInfo [index].tbi.bmp := bmp;
   menuItemInfo [index].tbi.ico := ico
END MenuItemToToolbar;

PROCEDURE ['C'] SeparatorDummyFunc ();
BEGIN
END SeparatorDummyFunc;

PROCEDURE AddMenuSeparator* ();
(** Add a dummy function to create separator menu item afterwards. *)
VAR empty: ARRAY 1 OF Char;
BEGIN
   ASSERT (~menuLocked, 20);
   empty [0] := 0;
   AddMenuItem (empty, SeparatorDummyFunc, FALSE, NIL)
END AddMenuSeparator;

PROCEDURE InitMenu;
(* Initialize the menu after it was created in Notepad++:
 * - turn all menu items added with AddMenuSeparator into separators;
 * - disable all menu items disabled using EnableMenuItems (i, FALSE);
 * - set all "checked" properties according to 'initChk';
 * - put items on the toolbar. *)
VAR
   i: INTEGER;
   hMenu: Win.HMENU;
   cmdID: LONGINT; (* a copy of 'MI [i].cmdID' in the loop for optimization *)
BEGIN
   ASSERT (menuLocked, 20);
   hMenu := GetMenu ();
   i := 0;
   WHILE i < lastItem DO
      cmdID := MI [i].cmdID;
      IF MI [i].pFunc = SeparatorDummyFunc THEN
         Win.ModifyMenu(hMenu, cmdID, SYSTEM.VAL (SYSTEM.CARD32, Win.MF_BYCOMMAND + Win.MF_SEPARATOR),
            cmdID, NIL
         )
      END;
      IF MI [i].initChk # 0 THEN
         (* This is a workaround for a bug in certain old Notepad++ versions
            such as v4.8.5: it ignored the 'initChk' value and all menu items
            were unchecked on startup. *)
         Win.CheckMenuItem (hMenu, cmdID, Win.MF_BYCOMMAND + Win.MF_CHECKED);
      END;
      IF ~menuItemInfo [i].enabled THEN
         Win.EnableMenuItem (hMenu, cmdID, Win.MF_BYCOMMAND + Win.MF_GRAYED + Win.MF_DISABLED)
      END;
      INC (i)
   END;
   menuItemInfo := NIL (* not used anymore - release memory *)
END InitMenu;

PROCEDURE InitToolbar ();
VAR i: INTEGER;
    versionWords: LONGINT;
BEGIN
   ASSERT (toolbarLocked, 20);
   i := 0;
   WHILE i < lastItem DO
      IF (menuItemInfo [i].tbi.bmp # NIL) OR (menuItemInfo [i].tbi.ico # NIL) THEN
         versionWords := Win.SendMessage (handle, NPPM_GETNPPVERSION, 0, 0);
         (* the dark mode API requires a least one ICO, otherwise nothing will display *)
         IF (Win.HIWORD (versionWords) >= 8) & (menuItemInfo [i].tbi.ico # NIL) THEN
           Win.SendMessage (handle, NPPM_ADDTOOLBARICON_FORDARKMODE, MI [i].cmdID, SYSTEM.ADR (menuItemInfo [i].tbdm))
         ELSE
           Win.SendMessage (handle, NPPM_ADDTOOLBARICON, MI [i].cmdID, SYSTEM.ADR (menuItemInfo [i].tbi))
         END;
      END;
      INC (i)
   END
END InitToolbar;

(* --- Notepad++ required plugin functions --- *)

<* DLLEXPORT+ *>

PROCEDURE ['C'] setInfo* (npp, sciMain, sciSecond: Win.HWND);
(** Notepad++ gives the main window handles, we'll need them later. *)
BEGIN
   handle := npp;
   Sci.Init (scintillaMain, sciMain, TRUE);
   Sci.Init (scintillaSecond, sciSecond, TRUE);
   IF onSetInfo # NIL THEN
      onSetInfo ()
   END
END setInfo;

PROCEDURE ['C'] getName* (): Win.PWSTR;
(** Return the name of this plugin *)
BEGIN
   RETURN SYSTEM.VAL (Win.PWSTR, SYSTEM.ADR (PluginName));
END getName;

PROCEDURE ['C'] beNotified* (VAR note: SCNotification);
(** Receive various notifications from Notepad++. *)
BEGIN
   IF note.nmhdr.hwndFrom = handle THEN
      CASE note.nmhdr.code OF
      | NPPN_READY:
         (* the startup of Notepad++ is complete, you may perform additional
          * plugin initialization here *)
         InitMenu;
         IF onReady # NIL THEN
            onReady
         END
      | NPPN_TBMODIFICATION:
         toolbarLocked := TRUE;
         InitToolbar
      ELSE
      END
   END
END beNotified;

<* PUSH *><* +WOFF301 (* Disable "unused parameters" warning *) *>
PROCEDURE ['C'] messageProc* (msg: Win.UINT; wParam: Win.WPARAM; lParam: Win.LPARAM): Win.LRESULT;
BEGIN
   RETURN 0
END messageProc;
<* POP *>

PROCEDURE ['C'] getFuncsArray* (VAR nFuncs: LONGINT): LONGINT;
(** Notepad++ requests the list of exported functions. *)
BEGIN
   menuLocked := TRUE; (* no more new items *)
   nFuncs := lastItem;
   RETURN SYSTEM.ADR (MI^)
END getFuncsArray;

PROCEDURE ['C'] isUnicode* (): BOOLEAN;
(** Notepad++ asks if the plugin is ANSI or Unicode. *)
BEGIN
   RETURN TRUE
END isUnicode;

<* DLLEXPORT- *>

BEGIN lastItem := 0; menuLocked := FALSE; toolbarLocked := FALSE; SetNumMenuItems (DefNumMenuItems)
END NotepadPPU.
