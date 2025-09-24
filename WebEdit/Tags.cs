using System.Text;
using System.Text.RegularExpressions;
using Npp.DotNet.Plugin;

namespace WebEdit
{
  /// <remarks>
  /// Adapted from <see href="https://github.com/alex-ilin/WebEdit/blob/master/Legacy-v2.1/Src/Tags.ob2"/>
  /// </remarks>
  partial class Tags(long tabStartPos, long tabEndPos)
  {
    /// <summary>
    /// Transform string by replacing:
    /// <code>
    /// \r\n with a Windows newline
    /// \r with a carriage return (CR)
    /// \n with a new line string determined by current EOL mode
    /// \t with the tab character
    /// \| with |
    /// \\ with \
    /// </code>
    /// </summary>
    /// <param name="value">Input string from the ini-file's value of the tag.</param>
    /// <returns>Expanded value with escape sequences replaced.</returns>
    /// <remarks>
    /// Adapted from <see href="https://github.com/alex-ilin/WebEdit/blob/7bb4243/Legacy-v2.1/Src/Tags.ob2#L268"/>
    /// </remarks>
    internal string Unescape(string value)
    {
      StringBuilder text = new(value);
      ScintillaGateway sci = new(Utils.GetCurrentScintilla());
      sci.SetTargetRange(_tabStartPos, _tabEndPos);
      string indent = sci.GetTargetText();
      // literal DOS EOL
      text.Replace("\\r\\n", $"\r\n{indent}");
      // literal carriage return
      text.Replace("\\r", $"\r{indent}");
      // document-specific EOL
      text.Replace("\\n", $"{sci.LineDelimiter}{indent}");
      text.Replace("\\t", "\t");
      text.Replace("\\|", "|");
      text.Replace("\\\\", "\\");
      return text.ToString();
    }

    /// <summary>
    /// Find, select and paste into the following escape sequences (in the order shown):
    /// <code>
    /// 1) \f[FileName:Section] with the contents of ':Section', if 'FileName' is a valid INI file;
    ///    or the entire contents of 'FileName', if a section name is missing
    /// 2) \c with SCI_PASTE
    /// 3) \i with SCI_TAB
    /// </code>
    /// </summary>
    /// <param name="startPos">The start position of the text range to search for escape sequences.</param>
    /// <returns>True if at least one instance of '\i' was replaced, otherwise false.</returns>
    /// <remarks>
    /// Adapted from <see href="https://github.com/alex-ilin/WebEdit/blob/7bb4243/Legacy-v2.1/Src/Tags.ob2#L169"/>
    /// </remarks>
    internal bool FindAndReplace(long startPos)
    {
      ScintillaGateway sci = new(Utils.GetCurrentScintilla());
      bool didReplace = false, didIndent = false;
      long rangeEnd = sci.GetSelectionEnd();

      // track the caret position resulting from the FIRST '\i' replacement
      long firstIndentCaret = -1;

      Dictionary<string, Action> replacements = new()
      {
        {"\\f", PasteFileContents},
        {"\\c", sci.Paste},
        {"\\d", PasteDateTime},
        {"\\u", PasteUserName},
        {"\\x", PasteFileName},
        {"\\p", PasteFilePath},
        // must come last as the caret will be restored here
        {"\\i", sci.Tab}
      };

      foreach ((string seq, var replaceFunc) in replacements)
      {
        sci.SetTargetRange(startPos, rangeEnd); // `sci.GetTextLength()` is NOT recommended as `end` position: it may replace unrelevant elements too (in the document)!
        int seqStart = sci.GetTargetText().IndexOf(seq);
        didReplace = didReplace || seqStart > -1;
        didIndent = didReplace && seq == "\\i";

        while (seqStart > -1)
        {
          var tmpTargetText = sci.GetTargetText().Substring(0, seqStart);
          int tmpByteDiff   = sci.CodePage.GetByteCount(tmpTargetText) - tmpTargetText.Length;
          if (tmpByteDiff > 0)
            seqStart += sci.CodePage.GetByteCount(tmpTargetText) - tmpTargetText.Length; // Adjust for multi-byte characters
          long selStart = sci.GetTargetStart() + seqStart;
          sci.SetSelection(selStart, selStart + seq.Length); // sci.CodePage.GetByteCount(seq)
          replaceFunc();
          rangeEnd += (sci.GetSelectionEnd() - selStart) - seq.Length; // sci.CodePage.GetByteCount(seq)

          // capture the caret position produced by the first '\i' replacement
          if (seq == "\\i" && firstIndentCaret == -1)
            firstIndentCaret = sci.GetSelectionEnd();

          sci.SetTargetRange(sci.GetSelectionEnd(), rangeEnd); // ORIG: sci.GetTextLength()
          seqStart = sci.GetTargetText().IndexOf(seq);
        }
      }

      if (didIndent)
      {
        // move caret to the position recorded for the first '\i' replacement;
        // fall back to current selection end if something went wrong.
        sci.SetCurrentPos(firstIndentCaret > -1 ? firstIndentCaret : sci.GetSelectionEnd());
        sci.ClearSelectionToCursor();
      }

      return didIndent;
    }

    /// <summary>
    /// Try to read from the file named in the tag <c>\\f[FileName:Section]</c>.
    /// If a section name is given, treat the file as an ini-file and paste the
    /// contents of <c>:Section</c> into the document, otherwise paste the entire file.
    /// </summary>
    /// <remarks>
    /// Adapted from <see href="https://github.com/alex-ilin/WebEdit/blob/7bb4243/Legacy-v2.1/Src/Tags.ob2#L223"/>
    /// </remarks>
    private void PasteFileContents()
    {
      ScintillaGateway sci = new(Utils.GetCurrentScintilla());
      long tagStart = sci.GetSelectionEnd();

      if ('[' != sci.GetCharAt(tagStart))
        return;

      long selectionEnd = tagStart;
      while (']' != sci.GetCharAt(selectionEnd) && selectionEnd++ < sci.GetLineEndPosition(tagStart)) ;
      sci.SetTargetRange(tagStart, selectionEnd);
      string[] value = sci.GetTargetText()[1..].Split(':', StringSplitOptions.TrimEntries);
      string fileName = value?.First();
      string section = (value?.Length > 1) ? value?.Skip(1)?.First() : string.Empty;

      if (string.IsNullOrEmpty(fileName))
        fileName = $"{Main.PluginName}.ini";

      string filePath = Path.Combine(Main.iniDirectory, fileName);
      if (!File.Exists(filePath) ||
          /* do not paste the contents of our own INI file */
          (string.IsNullOrEmpty(section) && string.Compare(filePath, Main.iniFilePath, StringComparison.InvariantCultureIgnoreCase) == 0))
        return;

      StringBuilder buffer = new();
      if (!string.IsNullOrEmpty(section))
      {
        var ini = new IniFile(filePath);
        foreach (var key in ini.GetKeys(section))
          buffer.Append($"{Unescape(ini.Get(section, key))}{sci.LineDelimiter}");
      }
      else
      {
        foreach (var line in File.ReadAllLines(filePath))
          buffer.Append($"{Unescape(line)}{sci.LineDelimiter}");
      }

      string replacementText = buffer.ToString().TrimEnd();
      if (!string.IsNullOrWhiteSpace(replacementText))
      {
        sci.SetSelection(sci.GetSelectionStart(), selectionEnd + sci.CodePage.GetByteCount("]"));
        sci.ReplaceSel(replacementText);
      }
    }

    /// <summary>
    /// Try to replace a date string in format <c>\\d:"date_time_string"</c>.
    /// </summary>
    /// <remarks>
    /// For custom date/time format strings see: <see href="https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings"/>
    /// </remarks>
    private void PasteDateTime()
    {
      ScintillaGateway sci = new(Utils.GetCurrentScintilla());
      long startPos = sci.GetSelectionStart();
      sci.SetTargetRange(startPos, sci.GetTextLength());
      string text = sci.GetTargetText();
      var matches = UserDefinedDateRegex().Matches(text);
      int offset = 0;
      foreach (Match match in matches.Cast<Match>())
      {
        if (!match.Success) continue;
        string format = match.Groups[1].Value;
        string formattedDate;
        try
        {
            formattedDate = DateTime.Now.ToString(format);
        }
        catch
        {
            formattedDate = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"); // fallback
        }
        int matchStart = match.Index + offset;
        int matchEnd = matchStart + match.Length;
        sci.SetSelection(sci.GetTargetStart() + matchStart, sci.GetTargetStart() + matchEnd);
        sci.ReplaceSel(formattedDate);
        offset += formattedDate.Length - match.Length;
      }

    }

    /// <summary>
    /// Paste the short Windows username for the '\u' tag.
    /// </summary>
    private void PasteUserName()
    {
      ScintillaGateway sci = new(Utils.GetCurrentScintilla());
      string user = Environment.UserName ?? string.Empty;
      sci.ReplaceSel(user);
    }

    /// <summary>
    /// Paste the current file name for the '\x' tag e.g. 'example.txt'
    /// </summary>
    private void PasteFileName()
    {
      ScintillaGateway sci = new(Utils.GetCurrentScintilla());
      string fileName = Path.GetFileName(PluginData.Notepad.GetCurrentFilePath()); // `PluginData.Notepad.GetCurrentFile()` or similar function (for `NppMsg.NPPM_GETFILENAME`) is not available...
      sci.ReplaceSel(fileName);
    }

    /// <summary>
    /// Paste the current file name for the '\p' tag e.g. 'C:\path\to\example.txt'
    /// </summary>
    private void PasteFilePath()
    {
      ScintillaGateway sci = new(Utils.GetCurrentScintilla());
      string fileName = PluginData.Notepad.GetCurrentFilePath();
      sci.ReplaceSel(fileName);
    }

    /// <summary>
    /// Matches a single, unescaped '|'
    /// </summary>
    [GeneratedRegex(@"(?<!\\)\|")]
    internal static partial Regex UserDefinedInsertionPoint();

    /// <summary>
    /// Matches an unescaped `\d:` followed by a quoted date,
    /// e.g. <c>\d:"yyyy-MM-dd"</c> (2025-12-31), <c>\d:'F'</c> (Wednesday, December 31, 2025 12:30:59) etc.
    /// Captures the date string (format specifier).
    /// </summary>
    /// <remarks>
    /// For custom date/time format strings see: <see href="https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings"/>
    /// </remarks>
    [GeneratedRegex(@"\\d:[""']([^""']+)[""']")]
    private static partial Regex UserDefinedDateRegex();

    private readonly long _tabStartPos = tabStartPos;
    private readonly long _tabEndPos = tabEndPos;
  }
}
