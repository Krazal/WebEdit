using Npp.DotNet.Plugin;
using System;
using System.Collections.Generic;

namespace WebEdit {
  class Actions {
    private readonly IDictionary<int, string> _commands;
    public readonly string[] iniKeys;

    public Actions(IniFile ini)
    {
      iniKeys = ini.GetKeys("Commands");
      _commands = new Dictionary<int, string>();
      int i = 0;
      foreach (var key in iniKeys) {
        _commands.Add(i++, ini.Get("Commands", key));
      }
    }

    private void ExecuteCommand(string command)
    {
      IntPtr currentScint = Utils.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint);
      string selectedText = scintillaGateway.GetSelText();
      int positionStar = scintillaGateway.GetSelectionStart();
      int positionEnd = scintillaGateway.GetSelectionEnd();
      string newText = command.Replace("|", selectedText);
      scintillaGateway.ReplaceSel(newText);
      scintillaGateway.SetSelection(positionStar + command.Substring(0, command.IndexOf('|')).Length, positionEnd + command.Substring(0, command.IndexOf('|')).Length);
      //scintillaGateway.SetSelectionEnd(position + command.Substring(0, command.IndexOf('|')).Length);
    }

    public PluginFunc GetCommand(int index)
    {
      return _commands.TryGetValue(index, out string cmdString) ? () => ExecuteCommand(cmdString) : null;
    }
  }
}
