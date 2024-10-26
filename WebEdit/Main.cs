using Npp.DotNet.Plugin;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using WebEdit;
using WebEdit.IniFiles;
using WebEdit.Properties;
using static Npp.DotNet.Plugin.Win32;

namespace WebEdit {
  partial class Main : DotNetPlugin {
    internal const string PluginName = "WebEdit";
    private const string IniFileName = PluginName + ".ini";
    private const string Version = "2.1";
    private const string MsgBoxCaption = PluginName + " " + Version;
    private const string AboutMsg =
      "This small freeware plugin allows you to wrap the selected text in "
      + "tag pairs and expand abbreviations using a hotkey.\n"
      + "For more information refer to " + PluginName + ".txt.\n"
      + "\n"
      + "Created by Alexander Iljin (Amadeus IT Solutions) using XDS Oberon, "
      + "March 2008 - March 2010.\n"
      + "Ported to C# by Miguel Febres, April 2021.\n"
      + "Contact e-mail: AlexIljin@users.SourceForge.net";

    static string iniDirectory, iniFilePath = null;

    public override void OnBeNotified(ScNotification notification)
    {
    }

    /// <summary>
    /// Load the ini-file and initialize the menu items. The toolbar will be
    /// initialized later and will use the commands used in the menu added here
    /// to get the command identifiers for the toolbar buttons.
    /// </summary>
    public override void OnSetInfo()
    {
      int i = 0;
      var npp = new NotepadPPGateway();
      iniDirectory = Path.Combine(npp.GetPluginConfigPath(), PluginName);
      _ = Directory.CreateDirectory(iniDirectory);
      iniFilePath = Path.Combine(iniDirectory, IniFileName);
      LoadConfig();
      // TODO: move the menu initialization to the LoadConfig method.
      var ini = new IniFile(iniFilePath);
      var actions = new Actions(ini);
      foreach (string key in actions.iniKeys) {
        var methodInfo = typeof(Actions).GetMethod("ExecuteCommand" + i++);
        if (methodInfo == null)
          break;

        Utils.SetCommand(
          key,
          (PluginFunc) Delegate.CreateDelegate(
            typeof(PluginFunc), actions, methodInfo.Name));
      }
      Utils.SetCommand(
        "Replace Tag", ReplaceTag,
        new ShortcutKey(FALSE, TRUE, FALSE, 13));
      Utils.MakeSeparator();
      Utils.SetCommand("Edit Config", EditConfig);
      Utils.SetCommand("Load Config", LoadConfig);
      Utils.SetCommand("About...", About);
    }

    /// <summary>
    /// Edit the plugin ini-file in Notepad++.
    /// </summary>
    internal static void EditConfig()
    {
      if (!new NotepadPPGateway().OpenFile(iniFilePath))
        _ = MsgBoxDialog(
          PluginData.NppData.NppHandle,
          "Failed to open the configuration file for editing:\n" + iniFilePath,
          MsgBoxCaption,
          (uint)(MsgBox.ICONWARNING | MsgBox.OK));
    }

    /// <summary>
    /// Load the settings from the ini-file. This is done on startup and when
    /// requested by the user via the Load Config menu. The iniFilePath member
    /// must be initialized prior to calling this method.
    /// </summary>
    internal static unsafe void LoadConfig()
    {
      if (!File.Exists(iniFilePath))
        using (var fs = File.Create(iniFilePath)) {
          byte[] info = new UTF8Encoding(true).GetBytes(Resources.WebEditIni);
          fs.Write(info, 0, info.Length);
        }
      // TODO: load the ini-file contents and update the menu and tag
      // replacement data.
    }

    /// <summary>
    /// Show the About message with copyright and version information.
    /// </summary>
    internal static void About()
      => _ = MsgBoxDialog(
            PluginData.NppData.NppHandle,
            AboutMsg,
            MsgBoxCaption,
            (uint)(MsgBox.ICONINFORMATION | MsgBox.OK));

    /// <summary>
    /// Add the toolbar icons for the menu items that have the configured
    /// bitmap files in the iniDirectory folder.
    /// </summary>
    internal static void AddToolbarIcons()
    {
      var npp = new NotepadPPGateway();
      var ini = new IniFile(iniFilePath);
      foreach (string key in ini.GetKeys("Toolbar"))
        try {
          npp.AddToolbarIcon(
            Convert.ToInt32(key) - 1,
            new Bitmap(
              Path.Combine(
                iniDirectory,
                ini.Get("Toolbar", key).Replace("\0", ""))));
        } catch {
          // Ignore any errors like missing or corrupt bitmap files, or
          // incorrect command index values.
        }
    }

    internal static void PluginCleanUp()
    {
      // This method is called when the plugin is notified about Npp shutdown.
    }

    /// <summary>
    /// Replace the tag at the caret with an expansion defined in the [Tags]
    /// ini-file section.
    /// </summary>
    internal static void ReplaceTag()
    {
      IntPtr currentScint = Utils.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint);
      int position = scintillaGateway.GetSelectionEnd();

      string selectedText = scintillaGateway.GetSelText();
      if (string.IsNullOrEmpty(selectedText)) {
        // TODO: remove this hardcoded 10 crap. Remove selection manipulation:
        // user will not be happy to see any such side-effects.
        scintillaGateway.SetSelection(position > 10 ? (position - 10) : (position - position), position);
        selectedText = scintillaGateway.GetSelText();
        var reges = Regex.Matches(scintillaGateway.GetSelText(), @"(\w+)");
        if (reges.Count > 0) {
          selectedText = reges.Cast<Match>().Select(m => m.Value).LastOrDefault();
          scintillaGateway.SetSelection(position - selectedText.Length, position);
          selectedText = scintillaGateway.GetSelText();
        }
      }
      try {
        if (string.IsNullOrEmpty(selectedText)) {
          throw new Exception("No tag here.");
        }
        byte[] buffer = new byte[1048];
        var ini = new IniFile(iniFilePath);
        string value = ini.Get("Tags", selectedText, 1048);
        if (string.IsNullOrEmpty(value.Trim('\0'))) {
          throw new Exception("No tag here.");
        }
        value = TransformTags(value);
        scintillaGateway.ReplaceSel(value.Replace("|", null));
        scintillaGateway.SetSelectionEnd(position + value.Substring(0, value.IndexOf('|')).Length - selectedText.Length);
      } catch (Exception ex) {
        scintillaGateway.CallTipShow(position, ex.Message);
      }
    }

    /// <summary>
    /// Transform string by replacing:
    /// \c with the system Clipboard contents,
    /// \i with a single indentation level,
    /// \n with a new line,
    /// \t with tab character,
    /// \| with |,
    /// \\ with \.
    /// The order of replacements is important.
    /// </summary>
    /// <param name="value">Input string from the ini-file's value of the tag.</param>
    /// <returns>Expanded value with escape sequences replaced.</returns>
    private static string TransformTags(string value)
    {
      // TODO: add more commands: \\, \t.
      // TODO: does indentation work? I don't see insertions before \n.
      value = value.Replace("\\n", "\n");
      if (value.Contains("\\c")) {
        // TODO: what the heck is this? It's supposed to insert text from the
        // system Clipboard.
        value = value.Replace("\\c", "ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint)");
      }
      value = value.Replace("\\i", "  ");
      return value;
    }
  }
}
