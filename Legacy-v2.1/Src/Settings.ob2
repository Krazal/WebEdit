MODULE Settings;

(* ---------------------------------------------------------------------------
 * (C) 2008 - 2011 by Alexander Iljin
 * --------------------------------------------------------------------------- *)

IMPORT
   Npp:=NotepadPPU, StrU, Win:=Windows;

CONST
   PluginName* = 'WebEdit';
   IniFileName* = PluginName + '.ini';

VAR
   configDir-: ARRAY Win.MAX_PATH OF StrU.Char;
   configDirLen-: INTEGER;

PROCEDURE GetIniFileName* (VAR res: ARRAY OF StrU.Char);
BEGIN
   StrU.Copy (configDir, res);
   StrU.AppendC (res, IniFileName);
END GetIniFileName;

PROCEDURE Init*;
(* This procedure must be called when Notepad++ handle is assigned. *)
BEGIN
   ASSERT (Npp.handle # NIL, 20);
   Npp.GetPluginConfigDir (configDir);
   StrU.AppendC (configDir, '\');
   configDirLen := SHORT (StrU.Length (configDir));
END Init;

BEGIN
   configDir[0] := 0;
   configDirLen := 0;
END Settings.
