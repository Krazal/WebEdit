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
      int positionStar = scintillaGateway.GetSelectionStart();
      int positionEnd = scintillaGateway.GetSelectionEnd();
      string newText = command.Replace("|", selectedText);
      scintillaGateway.ReplaceSel(newText);
      if (command.IndexOf('|') < 0)
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
