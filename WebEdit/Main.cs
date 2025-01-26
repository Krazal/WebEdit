using Npp.DotNet.Plugin;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using WebEdit.Properties;
using static Npp.DotNet.Plugin.Win32;
using static Npp.DotNet.Plugin.Winforms.WinGDI;
using static Npp.DotNet.Plugin.Winforms.WinUser;

namespace WebEdit {
  partial class Main : DotNetPlugin {
    internal const string PluginName = "WebEdit";
    private const string MenuCmdPrefix = $"{PluginName} -";
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
      if (notification.Header.HwndFrom == PluginData.NppData.NppHandle)
      {
        uint code = notification.Header.Code;
        switch ((NppMsg)code)
        {
          case NppMsg.NPPN_TBMODIFICATION:
            PluginData.FuncItems.RefreshItems();
            AddToolbarIcons();
            break;
          case NppMsg.NPPN_SHUTDOWN:
            PluginCleanUp();
            break;
        }
      }
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
        var methodInfo = actions.GetCommand(i++);
        if (methodInfo == null)
          break;

        Utils.SetCommand(
          $"{MenuCmdPrefix} {key}",
          methodInfo);
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
#pragma warning disable CS0618 // Dark mode unaware icons are deprecated since Npp v8.0
    internal static void AddToolbarIcons()
    {
      ToolbarIcon _tbIcons = default;
      ToolbarIconDarkMode tbIcons = default;
      bool hasDarkMode = NppUtils.NppVersionAtLeast8;
      var ini = new IniFile(iniFilePath);
      var actions = new Actions(ini);
      var icons = ini.GetKeys("Toolbar");
      for (int i = 0; i < actions.iniKeys.Length && i < icons.Length; ++i)
      {
        try
        {
          if (actions.GetCommand(i) == null)
            continue;
          MenuItemToToolbar(ini.Get("Toolbar", icons[i]).Replace("\0", ""), ref tbIcons);
          // The dark mode API requires at least one ICO, or else nothing will display
          if (hasDarkMode && tbIcons.HToolbarIcon != NULL)
            NppUtils.Notepad.AddToolbarIcon(i, tbIcons);
          else
          {
            _tbIcons.HToolbarBmp = tbIcons.HToolbarBmp;
            _tbIcons.HToolbarIcon = tbIcons.HToolbarIcon;
            NppUtils.Notepad.AddToolbarIcon(i, _tbIcons);
          }
        }
        catch
        {
          // Ignore any errors like missing or corrupt bitmap files, or
          // incorrect command index values.
        }
      }
    }
#pragma warning restore CS0618

    internal static void PluginCleanUp()
    {
      // This method is called when the plugin is notified about Npp shutdown.
      PluginData.PluginNamePtr = NULL;
      PluginData.FuncItems.Dispose();
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
        string value = ini.Get("Tags", selectedText);
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

    /// <summary>
    /// Parse a delimited string of 1-3 icon file names, load the icon files
    /// and assign their handles to the given <paramref name="tbIcons"/> instance.
    /// </summary>
    private static void MenuItemToToolbar(string iniValueString, ref ToolbarIconDarkMode tbIcons)
    {
      string[] icons = iniValueString.Split(IniFile.ValueStringDelimiter, StringSplitOptions.RemoveEmptyEntries);
      for (int i = 0; i < icons.Length; ++i)
      {
        string iconFileName = icons[i]?.Trim().ToLowerInvariant();
        string iconExt = Path.GetExtension(iconFileName)?.ToLowerInvariant();
        if (iconExt == ".bmp")
          LoadToolbarIcon(LoadImageType.IMAGE_BITMAP, GetIconPath(iconFileName), out tbIcons.HToolbarBmp);
        else if (iconExt == ".ico")
        {
          if (i == 1)
          {
            LoadToolbarIcon(LoadImageType.IMAGE_ICON, GetIconPath(iconFileName), out tbIcons.HToolbarIcon);
            if (icons.Length < 3)
              tbIcons.HToolbarIconDarkMode = tbIcons.HToolbarIcon;
          }
          if (i == 2)
            LoadToolbarIcon(LoadImageType.IMAGE_ICON, GetIconPath(iconFileName), out tbIcons.HToolbarIconDarkMode);
        }
      }
    }

    /// <summary>
    /// Load a bitmap or icon from the given file name and return the handle.
    /// </summary>
    private static void LoadToolbarIcon(LoadImageType imgType, string iconFile, out IntPtr hImg)
    {
      var loadFlags = LoadImageFlag.LR_LOADFROMFILE;
      switch (imgType)
      {
        case LoadImageType.IMAGE_BITMAP:
          (int bmpX, int bmpY) = GetLogicalPixels(16, 16);
          hImg = LoadImage(NULL, iconFile, imgType, bmpX, bmpY, loadFlags | LoadImageFlag.LR_LOADMAP3DCOLORS);
          break;
        case LoadImageType.IMAGE_ICON:
          (int icoX, int icoY) = GetLogicalPixels(32, 32);
          hImg = LoadImage(NULL, iconFile, imgType, icoX, icoY, loadFlags | LoadImageFlag.LR_LOADTRANSPARENT);
          break;
        default:
          hImg = NULL;
          break;
      }
    }

    /// <summary>
    /// Return the absolute path to an icon file. The user's config directory
    /// is tried first; then the plugin's installation folder.
    /// </summary>
    private static string GetIconPath(string icon)
    {
      string path = Path.Combine(NppUtils.Notepad.GetPluginConfigPath(), PluginName, icon);
      if (!File.Exists(path))
        path = Path.Combine(NppUtils.Notepad.GetPluginsHomePath(), PluginName, "Config", icon);
      return path;
    }
  }
}
