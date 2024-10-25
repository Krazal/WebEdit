MODULE IniFiles;

(* ---------------------------------------------------------------------------
 * (C) 2008 - 2011 by Alexander Iljin
 * --------------------------------------------------------------------------- *)

IMPORT
   SYSTEM, Str:=StrA, StrU, Win:=Windows;

(* This module provides line-based read-only buffered access to files. Special
 * support of ini-files: section headers are detected, lines starting with
 * the semicolon character and empty lines are skipped. Any character below
 * Space (except for Tab) is considered EOL, therefore any EOL style is
 * supported. *)

TYPE
   File* = RECORD
      buff: ARRAY 1024 OF CHAR;
      line*: ARRAY 2049 OF CHAR;
      buffPos, buffLen: INTEGER;
      hFile: Win.HANDLE;
      eof-, section-: BOOLEAN;
   END;

PROCEDURE Init* (VAR f: File);
BEGIN
   f.line := '';
   f.buffPos := 0;
   f.buffLen := 0;
   f.hFile := NIL;
   f.eof := FALSE;
   f.section := FALSE;
END Init;

PROCEDURE Open* (VAR f: File; VAR fname: ARRAY OF StrU.Char);
BEGIN
   Init (f);
   f.hFile := Win.CreateFileW (fname, Win.FILE_READ_DATA, Win.FILE_SHARE_READ,
      NIL, Win.OPEN_EXISTING, Win.FILE_ATTRIBUTE_NORMAL, NIL)
END Open;

PROCEDURE IsOpen* (VAR f: File): BOOLEAN;
BEGIN
   RETURN f.hFile # Win.INVALID_HANDLE_VALUE
END IsOpen;

PROCEDURE Close* (VAR f: File);
BEGIN
   Win.CloseHandle (f.hFile);
   f.hFile := NIL
END Close;

PROCEDURE ReadLine* (VAR f: File): BOOLEAN;
(* Read until not empty line is read, return TRUE on success. If a new section
 * header is found, return FALSE, line will contain the section string
 * "[section name]", f.section = TRUE, otherwise f.section = FALSE. *)
CONST
   Tab = 09X;
   CommentChar = ';';
VAR
   i: INTEGER;
   ch: CHAR;
   eol: BOOLEAN;

   PROCEDURE ReadChar;
   (* Read ch from hFile, set eof = TRUE on error. *)
   VAR read: Win.DWORD;
   BEGIN
      IF f.buffPos >= f.buffLen THEN;
         f.buffPos := 0;
         read := 0;
         f.eof := ~Win.ReadFile (f.hFile, SYSTEM.VAL (Win.PVOID, SYSTEM.ADR (f.buff)), LEN (f.buff), read, NIL)
            OR (read = 0) OR (read > LEN (f.buff));
         f.buffLen := SHORT (read)
      END;
      IF ~f.eof THEN
         ch := f.buff [f.buffPos];
         INC (f.buffPos)
      END
   END ReadChar;

BEGIN (* ReadLine *)
   f.section := FALSE;
   REPEAT
      i := 0;
      ch := 0X;
      eol := FALSE;
      ReadChar;
      WHILE ~f.eof & ~eol & (i < LEN (f.line) - 1) DO
         IF (ch < ' ') & (ch # Tab) THEN
            eol := TRUE
         ELSE
            f.line [i] := ch;
            INC (i);
            ReadChar
         END
      END;
      IF (i = LEN (f.line) - 1) & ~eol & ~f.eof THEN (* line too long *)
         i := 0;
         f.eof := TRUE
      ELSIF i > 0 THEN
         IF f.line [0] = CommentChar THEN (* comment *)
            i := 0
         ELSIF (f.line [0] = '[') & (f.line [i - 1] = ']') THEN (* new section *)
            f.section := TRUE
         END
      END
   UNTIL f.eof OR f.section OR (i > 0);
   f.line [i] := Str.Null;
   RETURN (i > 0) & ~f.section
END ReadLine;

PROCEDURE ReadSection* (VAR file: File): BOOLEAN;
(* Read next line and keep reading until a new section is found. Return TRUE
 * on success, FALSE on EOF. *)
BEGIN
   WHILE ReadLine (file) DO
   END;
   RETURN file.section
END ReadSection;

PROCEDURE FindSection* (VAR file: File; VAR section: ARRAY OF CHAR): BOOLEAN;
(* Read next line and keep reading until the section is found. Return TRUE if
 * found, FALSE otherwise. *)

   PROCEDURE MatchSection (): BOOLEAN;
   (* file.line is known to contain a Null-terminated section name in brackets,
    * return TRUE if it matches the 'section' string. *)
   VAR
      i: INTEGER;
   BEGIN
      i := 0;
      WHILE (section [i] # Str.Null) & (section [i] = file.line [i + 1]) DO
         INC (i);
      END;
      RETURN (section [i] = Str.Null)
         & (file.line [i + 1] = ']')
         & (file.line [i + 2] = Str.Null)
   END MatchSection;

BEGIN (* FindSection *)
   WHILE ReadSection (file) & ~MatchSection () DO
   END;
   RETURN ~file.eof
END FindSection;

END IniFiles.
