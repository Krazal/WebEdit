MODULE Tags;

(* ---------------------------------------------------------------------------
 * (C) 2010 - 2011 by Alexander Iljin
 * --------------------------------------------------------------------------- *)

IMPORT
   Ini:=IniFiles, Sci:=Scintilla, Settings, Str:=StrA, StrU, Win:=Windows;

(* ---------------------------------------------------------------------------
 * This module deals with tag replacements. It was made as a substitute for
 * the very good, but unsupported QuickText plugin.
 * --------------------------------------------------------------------------- *)

CONST
   MaxKeyLen*     = 32; (* When changing, update the MaxKeyMsg message. *)
   MaxKeyMsg      = 'Maximum tag length is 32 characters.';
   KeyNotFoundMsg = 'Undefined tag: ';
   NoKeyMsg       = 'No tag here.';
   NoTagsMsg      = 'No tags defined.';

TYPE
   KeyStr* = ARRAY MaxKeyLen + 1 OF CHAR;
   Tag = POINTER TO TagDesc;
   TagDesc = RECORD
      key: KeyStr;
      value: Str.Ptr;
      next: Tag;
   END;

VAR
   root: Tag;

PROCEDURE ValidKeyChar (char: CHAR): BOOLEAN;
(* Return TRUE if 'char' is valid in a tag key. *)
BEGIN
   CASE char OF
   | 'a'..'z', 'A'..'Z', '0'..'9':
      RETURN TRUE
   ELSE
      RETURN FALSE
   END;
END ValidKeyChar;

PROCEDURE ValidKey (VAR key: KeyStr): BOOLEAN;
(* Return TRUE if 'key' consists of valid tag keys only. *)
VAR
   i: LONGINT;
BEGIN
   i := 0;
   WHILE (i <= MaxKeyLen) & (key [i] # Str.Null) & ValidKeyChar (key [i]) DO
      INC (i);
   END;
   RETURN (i <= MaxKeyLen) & (key [i] = Str.Null)
END ValidKey;

PROCEDURE Find (VAR key: KeyStr): Tag;
(* Find Tag with the given key, return NIL if not found. *)
VAR
   res: Tag;
BEGIN
   res := root;
   WHILE (res # NIL) & (res.key # key) DO
      res := res.next;
   END;
   RETURN res
END Find;

PROCEDURE Do* (sci: Sci.Handle);
(** Either replace the current tag or jump to the next hotspot. *)
TYPE
   ScintillaCommand = PROCEDURE (sci: Sci.Handle);
VAR
   pos: LONGINT;
   key: KeyStr;
   tag: Tag;
   msg: ARRAY LEN (KeyNotFoundMsg) + MaxKeyLen OF CHAR; (* Error message *)
   posLeft, posRight, line: LONGINT;
   indentBeg, indentEnd: LONGINT;
   fname: StrU.Ptr; section: Str.Ptr;

   PROCEDURE ShowMsg (msg: ARRAY OF CHAR);
   BEGIN
      Sci.CallTipShow (sci, pos, msg);
   END ShowMsg;

   PROCEDURE GetKey (): BOOLEAN;
   (* Copy key from sci [pos]. On error show message and return false. *)
   VAR
      stop: LONGINT;
      res: BOOLEAN;
   BEGIN
      posLeft := pos;
      stop := pos - MaxKeyLen - 1;
      IF stop < 0 THEN
         stop := 0;
      END;
      WHILE (posLeft > stop) & ValidKeyChar (Sci.GetCharAt (sci, posLeft - 1)) DO
         DEC (posLeft);
      END;
      posRight := pos;
      stop := Sci.GetTextLength (sci);
      IF stop > pos + MaxKeyLen + 1 THEN
         stop := pos + MaxKeyLen + 1;
      END;
      WHILE (posRight < stop) & ValidKeyChar (Sci.GetCharAt (sci, posRight)) DO
         INC (posRight);
      END;
      res := FALSE;
      IF posRight - posLeft <= 0 THEN
         ShowMsg (NoKeyMsg);
      ELSIF posRight - posLeft <= MaxKeyLen THEN
         stop := Sci.GetTextRange (sci, posLeft, posRight, key);
         res := stop = posRight - posLeft;
         IF res THEN
            key [stop] := Str.Null;
         END;
      ELSE
         ShowMsg (MaxKeyMsg);
      END;
      RETURN res
   END GetKey;

   PROCEDURE PasteValue (pastePos: LONGINT; VAR value: ARRAY OF CHAR;
      indentBeg, indentEnd: LONGINT);
   CONST
      MaxIndent = 80;
      TabChar = 09X;
   VAR
      indent: ARRAY MaxIndent + 1 OF CHAR; (* Indentation at line start. *)
      indentLen: LONGINT; (* Really required length. Can be > MaxIndent. *)
      i, c: LONGINT;
      pos: LONGINT; (* Current text insertion position. *)
      caretPos: LONGINT; (* Caret position after the operation. *)

      PROCEDURE PasteChar (VAR to: LONGINT; char: CHAR);
      VAR
         str: ARRAY 2 OF CHAR;
      BEGIN
         str [0] := char;
         str [1] := Str.Null;
         Sci.InsertText (sci, to, str);
         INC (to);
      END PasteChar;

      PROCEDURE PasteIndent (VAR to: LONGINT);
      (* Paste indentation characters. 'to' is increased by the pasted amount. *)
      VAR from, endPos: LONGINT;
      BEGIN
         IF indentLen < MaxIndent THEN
            Sci.InsertText (sci, to, indent);
            INC (to, indentLen);
         ELSE (* pump sci [indentBeg..indentEnd] to 'to' using 'indent' as buffer *)
            endPos := to + indentLen;
            from := indentBeg;
            WHILE ~(indentEnd - from < LEN (indent) - 1) DO
               indent [Sci.GetTextRange (sci, from, from + MaxIndent, indent)] := Str.Null;
               Sci.InsertText (sci, to, indent);
               INC (from, MaxIndent);
               INC (to, MaxIndent);
            END;
            indent [Sci.GetTextRange (sci, from, indentEnd, indent)] := Str.Null;
            Sci.InsertText (sci, to, indent);
            INC (to, indentEnd - from);
            ASSERT (to = endPos, 60);
         END;
      END PasteIndent;

      PROCEDURE PasteByCommand (VAR to: LONGINT; cmd: ScintillaCommand);
      (* Paste something using 'cmd'. 'to' is increased by the pasted amount. *)
      VAR prevLen: LONGINT;
      BEGIN
         Sci.SetCurrentPos (sci, to);
         Sci.SetAnchor (sci, to);
         prevLen := Sci.GetTextLength (sci);
         cmd (sci);
         INC (to, Sci.GetTextLength (sci) - prevLen);
      END PasteByCommand;

      PROCEDURE ReadFileNameAndSection (VAR str: ARRAY OF CHAR; VAR index: LONGINT; VAR outFileName: StrU.Ptr; outSection: Str.Ptr): BOOLEAN;
      (* Read "[" <outFileName> [":" <outSection>] "]" from str [index]...
       * Return TRUE on success. Both out-variables can be NIL on success. *)
      CONST
         StartChar = '[';
         Separator = ':';
         EndChar = ']';
      VAR
         res: BOOLEAN;
         i: LONGINT;
      BEGIN
         res := FALSE;
         outFileName := NIL;
         outSection := NIL;
         IF str [index] = StartChar THEN
            INC (index);
            i := index; (* read outFileName *)
            WHILE (str [index] # Str.Null) & (str [index] # Separator) & (str [index] # EndChar) DO
               INC (index);
            END;
            IF (str [index] # Str.Null) & (index > i) THEN
               NEW (outFileName, index - i + 1);
               StrU.CopyTo (str, outFileName^, i, index, 0);
            END;
            IF str [index] = Separator THEN
               INC (index);
               i := index; (* read outSection *)
               WHILE (str [index] # Str.Null) & (str [index] # EndChar) DO
                  INC (index);
               END;
               IF (str [index] # Str.Null) & (index > i) THEN
                  NEW (outSection, index - i + 1);
                  Str.CopyTo (str, outSection^, i, index, 0);
               END;
            END;
            res := str [index] # Str.Null;
            IF str [index] = EndChar THEN
               INC (index);
            END;
         END;
         RETURN res
      END ReadFileNameAndSection;

      PROCEDURE PasteFileContents (VAR fname: StrU.Ptr; section: Str.Ptr);
      (* Try to read 'fname' file. If 'section' # NIL, then treat the file as
       * an ini-file and paste the contents of the 'section' section to the
       * Scintilla text, otherwise paste the entire file. *)
      VAR
         file: Ini.File;

         PROCEDURE PasteFileLine (VAR pos: LONGINT);
         BEGIN
            Sci.InsertText (sci, pos, file.line);
            INC (pos, Str.Length (file.line));
         END PasteFileLine;

      BEGIN
         IF fname = NIL THEN
            NEW (fname, Win.MAX_PATH);
            Settings.GetIniFileName (fname^);
         END;
         Ini.Open (file, fname^);
         IF Ini.IsOpen (file) THEN
            IF (section = NIL) OR Ini.FindSection (file, section^) THEN
               (* Processing is terminated either by reaching the end of the
                * current section, or EOF if section = NIL. *)
               IF Ini.ReadLine (file) OR (section = NIL) & ~file.eof THEN
                  PasteFileLine (pos);
               END;
               WHILE Ini.ReadLine (file) OR (section = NIL) & ~file.eof DO
                  INC (pos, Sci.InsertEol (sci, pos));
                  PasteIndent (pos);
                  PasteFileLine (pos);
               END;
            END;
            Ini.Close (file);
         END;
         (* Test code: paste contents of 'fname' and 'section'
         IF fname # NIL THEN
            Sci.InsertText (sci, pos, fname^);
            INC (pos, Str.Length (fname^));
         END;
         IF section # NIL THEN
            Sci.InsertText (sci, pos, section^);
            INC (pos, Str.Length (section^));
         END; *)
      END PasteFileContents;

   BEGIN (* PasteValue *)
      caretPos := -1;
      indentLen := indentEnd - indentBeg;
      IF indentLen < MaxIndent THEN (* init indent once and for all *)
         indent [Sci.GetTextRange (sci, indentBeg, indentEnd, indent)] := Str.Null;
      END;
      i := 0;
      c := 0; (* value [c] is the next character to be pasted to 'sci' *)
      pos := pastePos;
      WHILE value [i] # Str.Null DO
         IF value [i] = '\' THEN (* paste value [c..i - 1] to 'sci' *)
            Sci.InsertBuff (sci, pos, value, c, i);
            INC (pos, i - c);
            INC (i);
            (* handle escaped characters *)
            IF value [i] = 'n' THEN         (* eol *)
               INC (pos, Sci.InsertEol (sci, pos));
               PasteIndent (pos);
            ELSIF value [i] = 'i' THEN      (* indentation *)
               PasteByCommand (pos, Sci.Tab);
            ELSIF value [i] = '\' THEN      (* backslash *)
               PasteChar (pos, '\');
            ELSIF value [i] = 't' THEN      (* tab *)
               PasteChar (pos, TabChar);
            ELSIF value [i] = '|' THEN      (* pipe *)
               PasteChar (pos, '|');
            ELSIF value [i] = 'c' THEN      (* clipboard *)
               PasteByCommand (pos, Sci.Paste);
            ELSIF value [i] = 'f' THEN      (* file contents *)
               INC (i); (* step to the next character *)
               IF ReadFileNameAndSection (value, i, fname, section) THEN
                  PasteFileContents (fname, section);
               END;
               DEC (i); (* step back to re-parse the last character *)
            ELSIF value [i] = Str.Null THEN (* null? yes, it can happen *)
               DEC (i); (* step back to terminate the outer loop *)
            END;
            c := i + 1;
         ELSIF value [i] = '|' THEN
            Sci.InsertBuff (sci, pos, value, c, i);
            INC (pos, i - c);
            caretPos := pos;
            c := i + 1;
         END;
         INC (i);
      END;
      Sci.InsertBuff (sci, pos, value, c, i); (* c = i is no problem *)
      INC (pos, i - c);
      IF caretPos = -1 THEN
         caretPos := pos;
      END;
      Sci.GotoPos (sci, caretPos);
      (* move the caret about the target position to avoid invalid caret
       * placement on subsequent moves with up and down keys (wrong column) *)
      Sci.CharLeft (sci);
      Sci.CharRight (sci);
      IF caretPos = 0 THEN
         Sci.CharLeft (sci);
      END;
   END PasteValue;

BEGIN (* Do *)
   pos := Sci.GetCurrentPos (sci);
   IF root = NIL THEN
      ShowMsg (NoTagsMsg);
   ELSIF GetKey () THEN
      tag := Find (key);
      IF tag # NIL THEN
         Sci.BeginUndoAction (sci);
         line := Sci.LineFromPosition (sci, posLeft);
         indentBeg := Sci.PositionFromLine (sci, line);
         indentEnd := Sci.GetLineIndentPosition (sci, line);
         msg := ''; (* msg is just a temp variable here *)
         Sci.SetSel (sci, posLeft, posRight);
         Sci.ReplaceSel (sci, msg);
         PasteValue (posLeft, tag.value^, indentBeg, indentEnd);
         Sci.EndUndoAction (sci);
      ELSE
         msg := KeyNotFoundMsg;
         Str.Append (msg, key);
         ShowMsg (msg);
      END;
   END;
END Do;

PROCEDURE Clear*;
(** Forget all tags. *)
BEGIN
   root := NIL;
END Clear;

PROCEDURE Add* (VAR key: KeyStr; value: Str.Ptr);
(** Add key-value pair to the global tag list. *)
VAR
   tag: Tag;
BEGIN
   ASSERT (value # NIL, 21);
   IF ValidKey (key) THEN (* otherwise it won't be recognized in text *)
      NEW (tag);
      tag.key := key;
      tag.value := value;
      tag.next := root;
      root := tag;
   END;
END Add;

BEGIN
   Clear;
END Tags.
