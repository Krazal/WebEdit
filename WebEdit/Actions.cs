using Npp.DotNet.Plugin;
using System;
using System.Collections.Generic;
using WebEdit.IniFiles;

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

    public void ExecuteCommand0() => ExecuteCommand(_commands[0]);
    public void ExecuteCommand1() => ExecuteCommand(_commands[1]);
    public void ExecuteCommand2() => ExecuteCommand(_commands[2]);
    public void ExecuteCommand3() => ExecuteCommand(_commands[3]);
    public void ExecuteCommand4() => ExecuteCommand(_commands[4]);
    public void ExecuteCommand5() => ExecuteCommand(_commands[5]);
    public void ExecuteCommand6() => ExecuteCommand(_commands[6]);
    public void ExecuteCommand7() => ExecuteCommand(_commands[7]);
    public void ExecuteCommand8() => ExecuteCommand(_commands[8]);
    public void ExecuteCommand9() => ExecuteCommand(_commands[9]);
    public void ExecuteCommand10() => ExecuteCommand(_commands[10]);
    public void ExecuteCommand11() => ExecuteCommand(_commands[11]);
    public void ExecuteCommand12() => ExecuteCommand(_commands[12]);
    public void ExecuteCommand13() => ExecuteCommand(_commands[13]);
    public void ExecuteCommand14() => ExecuteCommand(_commands[14]);
    public void ExecuteCommand15() => ExecuteCommand(_commands[15]);
    public void ExecuteCommand16() => ExecuteCommand(_commands[16]);
    public void ExecuteCommand17() => ExecuteCommand(_commands[17]);
    public void ExecuteCommand18() => ExecuteCommand(_commands[18]);
    public void ExecuteCommand19() => ExecuteCommand(_commands[19]);
    public void ExecuteCommand20() => ExecuteCommand(_commands[20]);
    public void ExecuteCommand21() => ExecuteCommand(_commands[21]);
    public void ExecuteCommand22() => ExecuteCommand(_commands[22]);
    public void ExecuteCommand23() => ExecuteCommand(_commands[23]);
    public void ExecuteCommand24() => ExecuteCommand(_commands[24]);
    public void ExecuteCommand25() => ExecuteCommand(_commands[25]);
    public void ExecuteCommand26() => ExecuteCommand(_commands[26]);
    public void ExecuteCommand27() => ExecuteCommand(_commands[27]);
    public void ExecuteCommand28() => ExecuteCommand(_commands[28]);
    public void ExecuteCommand29() => ExecuteCommand(_commands[29]);
    public void ExecuteCommand30() => ExecuteCommand(_commands[30]);
    public void ExecuteCommand31() => ExecuteCommand(_commands[31]);
    public void ExecuteCommand32() => ExecuteCommand(_commands[32]);
    public void ExecuteCommand33() => ExecuteCommand(_commands[33]);
    public void ExecuteCommand34() => ExecuteCommand(_commands[34]);
    public void ExecuteCommand35() => ExecuteCommand(_commands[35]);
    public void ExecuteCommand36() => ExecuteCommand(_commands[36]);
    public void ExecuteCommand37() => ExecuteCommand(_commands[37]);
    public void ExecuteCommand38() => ExecuteCommand(_commands[38]);
    public void ExecuteCommand39() => ExecuteCommand(_commands[39]);
    public void ExecuteCommand40() => ExecuteCommand(_commands[40]);
    public void ExecuteCommand41() => ExecuteCommand(_commands[41]);
    public void ExecuteCommand42() => ExecuteCommand(_commands[42]);
    public void ExecuteCommand43() => ExecuteCommand(_commands[43]);
    public void ExecuteCommand44() => ExecuteCommand(_commands[44]);
    public void ExecuteCommand45() => ExecuteCommand(_commands[45]);
    public void ExecuteCommand46() => ExecuteCommand(_commands[46]);
    public void ExecuteCommand47() => ExecuteCommand(_commands[47]);
    public void ExecuteCommand48() => ExecuteCommand(_commands[48]);
    public void ExecuteCommand49() => ExecuteCommand(_commands[49]);
    public void ExecuteCommand50() => ExecuteCommand(_commands[50]);
  }
}
