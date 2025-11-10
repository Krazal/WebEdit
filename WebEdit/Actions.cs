using Npp.DotNet.Plugin;

namespace WebEdit {
  class Actions {
    private readonly Dictionary<int, string> _commands = [];
    public readonly string[] iniKeys;
    public readonly string[] toolbarKeys;

    public Actions(IniFile ini)
    {
      iniKeys = ini.GetKeys("Commands");
      toolbarKeys = ini.GetKeys("Toolbar");
      for (int i = 0; i < iniKeys.Length; i++)
        _commands.Add(i, ini.Get("Commands", iniKeys[i]));
    }

    /// <summary>
    /// Execute a menu/toolbar command: replace the selected tag (command) with its expansion
    /// </summary>
    /// <param name="command">Command string from the ini-file</param>
    internal void ExecuteCommand(string command)
    {
      Action<bool> replaceCommand = useHistory =>
      {

        // We use a simpler approach here compared to Main.HandleTag
        IntPtr currentScint = Utils.GetCurrentScintilla();
        ScintillaGateway scintillaGateway = new(currentScint);
        if (useHistory)
          scintillaGateway.BeginUndoAction();
        string selectedText = scintillaGateway.GetSelText();
        long positionStar = scintillaGateway.GetSelectionStart();
        long positionEnd = scintillaGateway.GetSelectionEnd();
        long lineCurrent = scintillaGateway.GetCurrentLineNumber();
        long lineStart = scintillaGateway.PositionFromLine(lineCurrent);
        long indentPos = Math.Min(scintillaGateway.GetLineIndentPosition(lineStart), positionStar);
        Tags parser = new(lineStart, indentPos);
        string newText = Tags.UserDefinedInsertionPoint().Replace(command, selectedText);
        scintillaGateway.ReplaceSel(parser.Unescape(newText));
        if (Tags.UserDefinedInsertionPoint().IsMatch(command))
          scintillaGateway.SetSelection(positionStar + command[..command.IndexOf('|')].Length, positionEnd + command[..command.IndexOf('|')].Length);
        if (useHistory)
          scintillaGateway.EndUndoAction();
      };
      HelpCommands(() => replaceCommand(true), () => replaceCommand(false));
    }

    /// <summary>
    /// Helper method to handle multiple selections for commands (tag replacements)
    /// </summary>
    /// <param name="singleCommand">The command (action) to execute for single selection</param>
    /// <param name="multiCommands">The command (action) to execute for multiple selections (e.g. avoid to open AutoComplete multiple times). Optional; if empty, `singleCommand()` will be used instead.</param>
    internal void HelpCommands(Action singleCommand, Action multiCommands = null)
    {
      IntPtr currentScint = Utils.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new(currentScint);

      // Get number of active selections
      int selections = scintillaGateway.GetSelections();
      if (selections <= 1) // "There is always at least one selection", but just in case...
      {
        // Single (or no) selection - handle normally 
        singleCommand();
        return;
      }

      // Start undo action for multiple selections
      scintillaGateway.BeginUndoAction();

      // Store + order all selection ranges
      var selectionRanges = new (long start, long end)[selections];
      for (int i = 0; i < selections; i++)
      {
        selectionRanges[i] = (
          scintillaGateway.GetSelectionNStart(i),
          scintillaGateway.GetSelectionNEnd(i)
        );
      }
      Array.Sort(selectionRanges, (a, b) => a.start.CompareTo(b.start)); // Ascending order for easier understanding

      // Process selections (selectionRanges) in reverse order to maintain selection positions
      var cursorPositions = selectionRanges;
      for (int i = selectionRanges.Length - 1; i >= 0; i--)
      {

        // Restore the original selection range
        scintillaGateway.SetSelection(selectionRanges[i].start, selectionRanges[i].end);
        long textLenDiff = scintillaGateway.GetTextLength(); // G2K: It has no role in the first iteration, but the minor overhead is negligible

        // Execute the command for the current selection
        if (multiCommands != null)
          multiCommands();
        else
          singleCommand();

        // Update the previous (and current) cursor positions after tag expansion
        textLenDiff = scintillaGateway.GetTextLength() - textLenDiff; // See above
        for (int j = cursorPositions.Length - 1; j > i; j--)
        {
          cursorPositions[j].start += textLenDiff;
          cursorPositions[j].end += textLenDiff;
        }
        cursorPositions[i].start = scintillaGateway.GetSelectionStart();
        cursorPositions[i].end   = scintillaGateway.GetSelectionEnd();
      }

      // Restore all cursor positions by creating multiple selections
      scintillaGateway.ClearSelections();
      for (int i = 0; i < cursorPositions.Length; i++)
      {
        if (i == 0)
          scintillaGateway.SetSelection(cursorPositions[i].start, cursorPositions[i].end);
        else
          scintillaGateway.AddSelection(cursorPositions[i].start, cursorPositions[i].end);
      }

      // End undo action for multiple selections
      scintillaGateway.EndUndoAction();
    }

    /// <summary>
    /// Get the command (tag) for the given index from the [Commands] section in the ini-file
    /// </summary>
    /// <param name="index">Tag index</param>
    /// <returns>PluginFunc if the command (tag) exists, null otherwise</returns>
    public PluginFunc GetCommand(int index)
    {
        return _commands.TryGetValue(index, out string cmdString) && !string.IsNullOrWhiteSpace(cmdString) ? () => ExecuteCommand(cmdString) : null;
    }
  }
}
