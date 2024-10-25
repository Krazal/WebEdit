MODULE StrA;

(* ------------------------------------------------------------------------
 * (C) 2009 - 2010 by Alexander Iljin
 * ------------------------------------------------------------------------ *)

IMPORT SYSTEM;

(** ------------------------------------------------------------------------
  * Basic string manipulations.
  * ----------------------------------------------------------------------- *)

CONST
   Null* = 0X; (* String terminator character. *)

TYPE
   Ptr* = POINTER TO ARRAY OF CHAR;
   CaseTable* = ARRAY ORD (MAX (CHAR)) + 1 OF CHAR;
   ListProcessingCallback* = PROCEDURE (VAR str: ARRAY OF CHAR;
      beg, end: LONGINT; VAR tag: LONGINT): BOOLEAN;

VAR
   (* Case conversion tables, e.g. upperCase [ORD ('a')] = 'A', etc. The
    * tables are initialized to ANSI only, i.e. upperCase is equivalent of the
    * CAP standard procedure. *)
   sameCase*, upperCase*, lowerCase*, invertCase*: CaseTable;

PROCEDURE Pos* (ch: CHAR; VAR str: ARRAY OF CHAR): LONGINT;
VAR
   res: LONGINT;
BEGIN
   res := 0;
   WHILE str [res] # ch DO
      INC (res);
   END;
   RETURN res
END Pos;

PROCEDURE Length* (VAR str: ARRAY OF CHAR): LONGINT;
BEGIN
   RETURN Pos (Null, str)
END Length;

PROCEDURE CopyTo* (VAR src, dst: ARRAY OF CHAR; beg, end, to: LONGINT);
(* Copy [beg, end) characters from src to dst [to, to+end-beg], append Null to dst. *)
BEGIN
   WHILE beg < end DO
      dst [to] := src [beg];
      INC (to);
      INC (beg);
   END;
   dst [to] := Null
END CopyTo;

PROCEDURE Append* (VAR str, tail: ARRAY OF CHAR);
VAR
   i, c: LONGINT;
BEGIN
   i := Length (str);
   c := Length (tail);
   SYSTEM.MOVE (SYSTEM.ADR (tail [0]), SYSTEM.ADR (str [i]), c);
   str [i + c] := Null;
END Append;

PROCEDURE AppendC* (VAR str: ARRAY OF CHAR; tail: ARRAY OF CHAR);
BEGIN
   Append (str, tail);
END AppendC;

PROCEDURE New* (VAR value: ARRAY OF CHAR): Ptr;
VAR res: Ptr;
BEGIN
   NEW (res, Length (value) + 1);
   COPY (value, res^);
   RETURN res
END New;

PROCEDURE NewC* (value: ARRAY OF CHAR): Ptr;
BEGIN
   RETURN New (value)
END NewC;

PROCEDURE ChangeCase* (VAR str: ARRAY OF CHAR; VAR table: CaseTable);
(** Change case of all characters in the string upto Null terminator according
  * to the 'table'. The table can be one of (sameCase, upperCase, lowerCase,
  * invertCase) or a custom one. *)
VAR
   i: LONGINT;
BEGIN
   i := 0;
   WHILE str [i] # Null DO
      str [i] := table [ORD (str [i])];
      INC (i);
   END;
END ChangeCase;

PROCEDURE ProcessList* (VAR list: ARRAY OF CHAR; separator: CHAR;
   callback: ListProcessingCallback; VAR tag: LONGINT): BOOLEAN;
(** Process the 'list' by calling 'callback' for every substring between the
  * 'separator' characters. Keep processing while 'callback' returns TRUE.
  * Result is either TRUE, or FALSE if the last 'callback' call returned
  * FALSE. I.e. TRUE means that the full list was processed, and FALSE means
  * premature interruption per the 'callback's request. If the callback is
  * used for searching for a substring, then 'FALSE' means 'found', while
  * 'TRUE' means 'not found', i.e. the result is inverted.
  * When the callback is called the following holds: beg <= end. If beg = end,
  * then an empty substring was found, like the second item in the following
  * list: "first item,,third item" (separator = ',').
  * The trailing separator not is reported as an empty item, i.e. the
  * following list contains only one empty item, not two: ",". An empty string
  * contains zero items. To put an empty item at the end of a list, add an
  * extra separaator.
  * The list must be terminated with Null, i.e. you can't pass a single CHAR
  * variable for the list parameter, unless it's value is Null.
  * 'tag' is just a variable that is passed to the 'callback' unmodified. *)
VAR
   beg, end: LONGINT;
   res: BOOLEAN;
BEGIN
   res := TRUE;
   beg := 0;
   WHILE res & (list [beg] # Null) DO
      end := beg;
      WHILE (list [end] # separator) & (list [end] # Null) DO
         INC (end);
      END;
      res := callback (list, beg, end, tag);
      beg := end;
      IF list [beg] # Null THEN
         INC (beg);
      END;
   END;
   RETURN res
END ProcessList;

PROCEDURE Init ();
VAR i, cap: INTEGER;
BEGIN
   (* same case *)
   i := ORD (MAX (CHAR));
   WHILE i # 0 DO
      sameCase [i] := CHR (i);
      DEC (i);
   END;
   sameCase [0] := CHR (0);
   (* upper, lower and invert cases *)
   upperCase := sameCase;
   lowerCase := sameCase;
   invertCase := sameCase;
   i := ORD ('a');
   WHILE i <= ORD ('z') DO
      cap := ORD (CAP (CHR (i)));
      upperCase [i] := CHR (cap);
      lowerCase [cap] := CHR (i);
      invertCase [i] := CHR (cap);
      invertCase [cap] := CHR (i);
      INC (i);
   END;
END Init;

BEGIN
   Init;
END StrA.
