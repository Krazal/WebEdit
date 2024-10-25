MODULE StrU;

(* ------------------------------------------------------------------------
 * (C) 2009 - 2010 by Alexander Iljin
 * ------------------------------------------------------------------------ *)

IMPORT SYSTEM, Win:=Windows, Str:=StrA;

(** ------------------------------------------------------------------------
  * Basic Unicode string manipulations.
  * ----------------------------------------------------------------------- *)

CONST
   Null* = 0; (* String terminator character. *)

TYPE
   Char* = SYSTEM.CARD16;
   Ptr* = POINTER TO ARRAY OF Char;

PROCEDURE Pos* (ch: Char; VAR str: ARRAY OF Char): LONGINT;
VAR
   res: LONGINT;
BEGIN
   res := Null;
   WHILE str [res] # ch DO
      INC (res);
   END;
   RETURN res
END Pos;

PROCEDURE Length* (VAR str: ARRAY OF Char): LONGINT;
BEGIN
   RETURN Pos (Null, str)
END Length;

PROCEDURE CopyPart* (VAR src, dst: ARRAY OF Char; beg, end, to: LONGINT);
BEGIN
   ASSERT ((0 <= beg) & (beg <= end), 20);
   ASSERT (end <= LEN (src), 21);
   ASSERT (to + (end - beg) <= LEN (dst), 22);
   SYSTEM.MOVE (SYSTEM.ADR (src [beg]), SYSTEM.ADR (dst [to]), (end - beg) * SIZE (Char));
END CopyPart;

PROCEDURE Copy* (VAR from, to: ARRAY OF Char);
BEGIN
   CopyPart (from, to, 0, Length (from) + 1, 0);
END Copy;

PROCEDURE CopyTo* (VAR src: ARRAY OF CHAR; VAR dst: ARRAY OF Char; beg, end, to: LONGINT);
(* Same as CopyPart, but ACP src is copied to the Unicode dst with conversion. *)
VAR res: LONGINT;
BEGIN
   res := to;
   IF beg < end THEN
      res := Win.MultiByteToWideChar (Win.CP_ACP, Win.MULTIBYTE_SET {},
         src [beg], end - beg, dst [to], LEN (dst) - to - 1);
      ASSERT (res # 0, 60);
      INC (res, to);
   END;
   dst [res] := Null
END CopyTo;

PROCEDURE Append* (VAR str: ARRAY OF Char; VAR end: ARRAY OF CHAR);
(* Append end to str, both strings and the result are null-terminated. End is
 * converted to Unicode on the fly. *)
VAR i, c, max: LONGINT;
BEGIN
   i := Length (str);
   c := Str.Length (end);
   max := LEN (str) - i - 1;
   IF c > max THEN
      c := max
   END;
   CopyTo (end, str, 0, SHORT (c), SHORT (i));
   str [i + c] := Null
END Append;

PROCEDURE AppendC* (VAR str: ARRAY OF Char; end: ARRAY OF CHAR);
(* Same as Append, but 'end' parameter is not VAR. *)
BEGIN
   Append (str, end);
END AppendC;

PROCEDURE Assign* (value: ARRAY OF CHAR; VAR str: ARRAY OF Char);
(* Assign the contents of 'value' to 'str'. *)
BEGIN
   str [0] := Null;
   Append (str, value)
END Assign;

END StrU.
