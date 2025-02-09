using Npp.DotNet.Plugin;

namespace WebEdit {
  class Actions {
    private readonly Dictionary<int, string> _commands = [];
    public readonly string[] iniKeys;

    public Actions(IniFile ini)
    {
      iniKeys = ini.GetKeys("Commands");
      for (int i = 0; i < iniKeys.Length; i++)
        _commands.Add(i, ini.Get("Commands", iniKeys[i]));
    }

    internal void ExecuteCommand(string command)
    {
      IntPtr currentScint = Utils.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new(currentScint);
      string selectedText = scintillaGateway.GetSelText();
      long positionStar = scintillaGateway.GetSelectionStart();
      long positionEnd = scintillaGateway.GetSelectionEnd();
      long lineCurrent = scintillaGateway.GetCurrentLineNumber();
      long lineStart = scintillaGateway.PositionFromLine(lineCurrent);
      long indentPos = Math.Min(scintillaGateway.GetLineIndentPosition(lineStart), positionStar);
      Tags parser = new(lineStart, indentPos);
      string newText = Tags.UserDefinedInsertionPoint().Replace(command, selectedText);
      scintillaGateway.ReplaceSel(parser.Unescape(newText));
      if (!Tags.UserDefinedInsertionPoint().IsMatch(command))
        return;
      scintillaGateway.SetSelection(positionStar + command[..command.IndexOf('|')].Length, positionEnd + command[..command.IndexOf('|')].Length);
      //scintillaGateway.SetSelectionEnd(position + command.Substring(0, command.IndexOf('|')).Length);
    }

    public PluginFunc GetCommand(int index)
    {
      return _commands.TryGetValue(index, out string cmdString) ? () => ExecuteCommand(cmdString) : null;
    }
  }
}
