using Npp.DotNet.Plugin;
using System.Text;
using WebEdit.Properties;
using static Npp.DotNet.Plugin.Win32;
using static Npp.DotNet.Plugin.Winforms.WinGDI;
using static Npp.DotNet.Plugin.Winforms.WinUser;
using static System.Diagnostics.FileVersionInfo;
using System.Runtime.InteropServices;

namespace WebEdit {
  partial class Main : IDotNetPlugin {
    /// <summary>See <see href="https://github.com/alex-ilin/WebEdit/blob/7bb4243/Legacy-v2.1/Src/Tags.ob2#L16"/></summary>
    public const int MaxKeyLen = 32;
    internal const string PluginName = "WebEdit";
    private const string MenuCmdPrefix = $"{PluginName} -";
    private const string IniFileName = PluginName + ".ini";
    private const string Version = "2.8";
    private static string MsgBoxCaption = $"{PluginName} {Version}";
    private static string[] currentCommandKeys = null; // Temporary storage of the current [Commands] keys to detect changes on reload
    private static bool currentCommandKeysAlerted = false; // Whether the user has been alerted about changes in [Commands] section
    private const string AboutMsg =
      "This small freeware plugin allows you to wrap the selected text in "
      + "tag pairs and expand abbreviations using a hotkey.\n"
      + "For more information visit https://github.com/npp-dotnet/WebEdit\n"
      + "\n"
      + "Created by Alexander Iljin (Amadeus IT Solutions) using XDS Oberon, "
      + "March 2008 - March 2010.\n"
      + "Ported to C# by Miguel Febres, April 2021.\n"
      + "Ported to .NET 8 by Robert Di Pardo, February 2025.\n"
      + "Currently maintained by Richard Stockinger, September 2025.\n"
      + "Contact e-mail: AlexIljin@users.SourceForge.net";

    static IniFile ini = null;
    static bool isConfigDirty = false;
    internal static string iniDirectory, iniFilePath = null;

    public void OnBeNotified(ScNotification notification)
    {
      if (notification.Header.HwndFrom == PluginData.NppData.NppHandle)
      {
        uint code = notification.Header.Code;
        switch ((NppMsg)code)
        {
          case NppMsg.NPPN_READY:
          case NppMsg.NPPN_BUFFERACTIVATED:
          case NppMsg.NPPN_NATIVELANGCHANGED:
            SetMenuItemNames();
            break;
          case NppMsg.NPPN_FILESAVED:
            if (isConfigDirty &&
                  (string.Compare(iniFilePath, PluginData.Notepad.GetCurrentFilePath(), StringComparison.InvariantCultureIgnoreCase) == 0))
            {
              LoadConfig();
              isConfigDirty = false;
            }
            SetMenuItemNames(true);
            break;
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
    public void OnSetInfo()
    {
      var npp = new NotepadPPGateway();
      iniDirectory = Path.Combine(npp.GetConfigDirectory(), PluginName);
      _ = Directory.CreateDirectory(iniDirectory);
      iniFilePath = Path.Combine(iniDirectory, IniFileName);
      try
      {
        MsgBoxCaption =
          MsgBoxCaption.Replace(Version,
            GetVersionInfo(Path.Combine(npp.GetPluginsHomePath(), PluginName, $"{PluginName}.dll")).ProductVersion);
      }
      catch { }
      LoadConfig();
      SetMenuItemNames();
    }

    public NativeBool OnMessageProc(uint msg, UIntPtr wParam, IntPtr lParam) => TRUE;

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
      else
        isConfigDirty = true;
    }

    /// <summary>
    /// Load the settings from the ini-file. This is done on startup and when
    /// requested by the user via the Load Config menu. The iniFilePath member
    /// must be initialized prior to calling this method.
    /// </summary>
    internal static void LoadConfig()
    {
      if (!File.Exists(iniFilePath))
        using (var fs = File.Create(iniFilePath)) {
          byte[] info = new UTF8Encoding(true).GetBytes(Resources.WebEditIni);
          fs.Write(info, 0, info.Length);
        }
      // Reload the ini-file contents and update the menu and tag
      // replacement data.
      ini = new IniFile(iniFilePath);
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
      bool hasDarkMode = PluginData.Notepad.GetNppVersion() switch { (int maj, _, _) => maj >= 8 };
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
            PluginData.Notepad.AddToolbarIcon(i, tbIcons);
          else
          {
            _tbIcons.HToolbarBmp = tbIcons.HToolbarBmp;
            _tbIcons.HToolbarIcon = tbIcons.HToolbarIcon;
            PluginData.Notepad.AddToolbarIcon(i, _tbIcons);
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
    }

    /// <summary>
    /// Replace the tag at the caret with an expansion defined in the [Tags]
    /// ini-file section.
    /// </summary>
    internal static void ReplaceTag()
    {
      IntPtr currentScint = Utils.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint);
      long lineCurrent = scintillaGateway.GetCurrentLineNumber();
      long lineStart = scintillaGateway.PositionFromLine(lineCurrent);
      long lineStartNext = scintillaGateway.PositionFromLine(lineCurrent + 1);
      string selectedText = scintillaGateway.GetSelText();

      if (string.IsNullOrEmpty(selectedText)) {
        string tag = PluginData.Notepad.GetCurrentWord();
        scintillaGateway.SetTargetRange(lineStart, lineStartNext);
        string lineText = scintillaGateway.GetTargetText();

        if (string.IsNullOrEmpty(lineText))
          return;

        // Find the last occurrence of the tag in the current line before the caret
        // NOTE: Scintilla positions are byte offsets for the document encoding,
        // while string.IndexOf works on characters. Convert between bytes and chars
        // using the document encoding to make the search Unicode-aware.
        long tagStartCharPos = -1;

        // Byte position of the caret relative to the line start
        long caretBytePosInLine = scintillaGateway.GetCurrentPos() - lineStart;

        // Get the encoded bytes of the line once
        byte[] lineBytes = scintillaGateway.CodePage.GetBytes(lineText);

        // Clamp caret byte index to available bytes
        int caretByteIndex = (int)Math.Min(Math.Max(0, caretBytePosInLine), lineBytes.Length);

        // Convert the caret byte offset to a character index
        int caretCharPos = scintillaGateway.CodePage.GetCharCount(lineBytes, 0, caretByteIndex);

        int searchPos = 0;
        while (searchPos < lineText.Length)
        {
          int foundPos = lineText.IndexOf(tag, searchPos, StringComparison.Ordinal);
          if (foundPos == -1 || foundPos + tag.Length > caretCharPos)
            break;
          tagStartCharPos = foundPos;
          searchPos = foundPos + 1;
        }

        if (tagStartCharPos < 0)
          return;

        // Convert the character position of the found tag into a byte position
        int tagStartByteOffset = scintillaGateway.CodePage.GetByteCount(lineText.AsSpan(0, (int)tagStartCharPos));
        long selStart = lineStart + tagStartByteOffset;

        long tagLengthBytes = scintillaGateway.CodePage.GetByteCount(tag);
        long selEnd = selStart + tagLengthBytes;
        scintillaGateway.SetSelection(selStart, selEnd);
        selectedText = scintillaGateway.GetSelText();
      }

      long position = scintillaGateway.GetSelectionEnd();
      try {
        scintillaGateway.BeginUndoAction();
        if (string.IsNullOrEmpty(selectedText?.Trim())) {
          position = scintillaGateway.GetCurrentPos();
          scintillaGateway.ClearSelectionToCursor();
          scintillaGateway.CallTipShow(position, "No tag here.");
          return;
        }
        else if (selectedText.Length > MaxKeyLen) {
          scintillaGateway.CallTipShow(position, $"Maximum tag length is {MaxKeyLen} characters.");
          return;
        }

        LoadConfig();
        string value = ini.Get("Tags", selectedText);

        if (string.IsNullOrEmpty(value.Trim('\0'))) {
          
          // Try to find a similar tag
          int shortestDistance = -1;
          var similarTags = ini.GetKeys("Tags");
          var closestTag = string.Empty;
          for (int i = 0; i < similarTags.Length; ++i)
          {
            int tmpDistance = calculateLevenshtein(selectedText, similarTags[i]);
            if (tmpDistance < selectedText.Length && (tmpDistance < shortestDistance || shortestDistance < 0))
            {
              shortestDistance = tmpDistance;
              closestTag = similarTags[i];
            }
          }

          // Show calltip with the closest tag found, or an undefined tag message
          unsafe
          {
            if (shortestDistance >= 0) {
              // encode selection according to the document in case it's in ASCII mode
              fixed (byte* pText = scintillaGateway.CodePage.GetBytes($"Did you mean: \"{closestTag}\"?\0"))
                SendMessage(currentScint, SciMsg.SCI_CALLTIPSHOW, (UIntPtr)position, (IntPtr)pText);
            } else {
              // encode selection according to the document in case it's in ASCII mode
              fixed (byte* pText = scintillaGateway.CodePage.GetBytes($"Undefined tag: \"{selectedText}\"\0"))
                SendMessage(currentScint, SciMsg.SCI_CALLTIPSHOW, (UIntPtr)position, (IntPtr)pText);
            }
          }
          return;
        }

        long selStart = scintillaGateway.GetSelectionStart();
        long indentPos = Math.Min(lineStart + (scintillaGateway.GetUseTabs() ? (scintillaGateway.GetLineIndentation(lineCurrent) / scintillaGateway.GetTabWidth()) : scintillaGateway.GetLineIndentation(lineCurrent)), selStart);
        Tags parser = new(lineStart, indentPos);
        // scintillaGateway.ReplaceSel(Tags.UserDefinedInsertionPoint().Replace(value, string.Empty));
        bool isUDInsPoint = Tags.UserDefinedInsertionPoint().IsMatch(value);
        if (isUDInsPoint) {
          // Replace only the first occurrence of the user-defined insertion point with a never-occurring sequence
          value = Tags.UserDefinedInsertionPoint().Replace(value, "\n\r", 1); // Replace only the first match
        }
        value = parser.Unescape(value);
        scintillaGateway.ReplaceSel(value);

        if (parser.FindAndReplace(selStart) && !isUDInsPoint)
          return;

        // Move the caret to the user-defined insertion point if present
        if (isUDInsPoint)
        {
          // Find the literal placeholder sequence we inserted ("\n\r") and move the caret there,
          // then remove the placeholder characters.
          scintillaGateway.SetTargetRange(selStart, scintillaGateway.GetTextLength());
          string tail = scintillaGateway.GetTargetText();
          int foundIndex = tail.IndexOf("\n\r", StringComparison.Ordinal);
          if (foundIndex >= 0)
          {
            // Compute byte offset of the found placeholder relative to document start
            long bytesBefore = scintillaGateway.CodePage.GetByteCount(tail.AsSpan(0, foundIndex));
            long placeholderDocPos = selStart + bytesBefore;
            long placeholderByteLen = scintillaGateway.CodePage.GetByteCount("\n\r");

            // Remove the placeholder
            scintillaGateway.SetSelection(placeholderDocPos, placeholderDocPos + placeholderByteLen);
            scintillaGateway.ReplaceSel(string.Empty);

            // Place the caret where the placeholder used to be
            scintillaGateway.SetCurrentPos(placeholderDocPos);
            scintillaGateway.ClearSelectionToCursor();
          }
          else
          {
            // Fallback: if we can't find the placeholder, restore caret to insertion end
            scintillaGateway.SetCurrentPos(position);
            scintillaGateway.ClearSelectionToCursor();
          }
        }

        // Hide calltip/autocompletion if visible
        if (scintillaGateway.CallTipActive()) // For the sake of completeness...
          SendMessage(currentScint, (uint)SciMsg.SCI_CALLTIPCANCEL);
        if (scintillaGateway.AutoCActive()) // && !scintillaGateway.AutoCGetCurrent() <-- Does it make sense to check this?
          SendMessage(currentScint, (uint)SciMsg.SCI_AUTOCCANCEL);

      } catch (Exception ex) {
        scintillaGateway.CallTipShow(position, ex.Message);
      } finally {
        scintillaGateway.EndUndoAction();
      }
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
      string path = Path.Combine(PluginData.Notepad.GetConfigDirectory(), PluginName, icon);
      if (!File.Exists(path))
        path = Path.Combine(PluginData.Notepad.GetPluginsHomePath(), PluginName, "Config", icon);
      return path;
    }

    /// <summary>
    /// (Re-)add menu items
    /// </summary>
    /// <remarks>
    /// Adapted from <see href="https://github.com/alex-ilin/WebEdit/blob/7bb4243/Legacy-v2.1/Src/NotepadPPU.ob2#L184"/>
    /// </remarks>
    private static unsafe void SetMenuItemNames(bool isReload = false)
    {
      var actions = new Actions(ini);

      // Remove previously added plugin menu items so we can re-add them (NOT WORKING AS EXPECTED)
      // if (isReload)
      {
                
        // Alert if the [Commands] section has changed (TEMPORARY, may be removed in future versions)
        if (currentCommandKeys != null && !currentCommandKeys.SequenceEqual(actions.iniKeys))
        {
          if (!currentCommandKeysAlerted)
          {
            MsgBoxDialog(
            PluginData.NppData.NppHandle,
            "The [Commands] configuration has changed. Please restart Notepad++ for all changes to take effect",
            MsgBoxCaption,
            (uint)(MsgBox.ICONWARNING | MsgBox.OK));
          }
          currentCommandKeysAlerted = true;
          return;
        }
        currentCommandKeys = actions.iniKeys;

        /*
        // Remove all previously added menu items (NOT WORKING AS EXPECTED)
        try
        {
          IntPtr hMenu = SendMessage(PluginData.NppData.NppHandle, (uint)NppMsg.NPPM_GETMENUHANDLE, (uint)NppMsg.NPPPLUGINMENU);
          if (hMenu != IntPtr.Zero)
          {
            // Iterate the registered function items and delete them by command id.
            // Iterate backwards to avoid any issues with indices when removing.
            for (int idx = PluginData.FuncItems.Items.Count - 1; idx >= 0; --idx)
            {
              try
              {
                int cmdId = PluginData.FuncItems.Items[idx].CmdID;
                if (PluginData.FuncItems.Items[idx].PFunc != null && PluginData.FuncItems.Items[idx].ItemName != "-")
                  _ = NativeMethods.DeleteMenu(hMenu, (uint)cmdId, MF_BYCOMMAND); // Delete the menu item by command identifier
                else
                  _ = NativeMethods.DeleteMenu(hMenu, (uint)idx, MF_BYPOSITION); // Delete the menu item separator by position
              }
              catch { }
            }
          }
        } catch { }

        // Clear the registered function items so Utils.SetCommand can add them again
        try { PluginData.FuncItems.Items.Clear(); } catch { }
        */
      }

      // Add menu items for each command in the [Commands] section of the ini-file
      bool foundItem = false;
      int i = 0;
      foreach (string key in actions.iniKeys)
      {
        var methodInfo = actions.GetCommand(i++);
        if (methodInfo == null)
        break;

        Utils.SetCommand(
        $"{MenuCmdPrefix} {key}",
        () =>
            {
            var cmds = new Actions(ini);
            cmds.ExecuteCommand(ini.Get("Commands", key));
        });
        foundItem = true;
      }

      // Add other menu items (Replace Tag, Edit Config, Load Config and About)
      if (foundItem)
      {
        Utils.MakeSeparator(); // Separator if "Commands" were found
      }
      Utils.SetCommand(
        "Replace Tag", ReplaceTag,
        new ShortcutKey(FALSE, TRUE, FALSE, 13));
      Utils.MakeSeparator();
      Utils.SetCommand("Edit Config", EditConfig);
      Utils.SetCommand("Load Config", LoadConfig);
      Utils.SetCommand("About...", About);
            
      /* // Refresh the menu items if this is a reload request (NOT WORKING AS EXPECTED, SEE ABOVE)
      if (isReload)
      {
        PluginData.FuncItems.RefreshItems();
        _ = NativeMethods.DrawMenuBar(PluginData.NppData.NppHandle);
      }
      // */




      /* DEPRECATED (may cause non-expected behavior)
      // PluginData.FuncItems.Items.Clear();
      IntPtr hMenu = SendMessage(PluginData.NppData.NppHandle, (uint)NppMsg.NPPM_GETMENUHANDLE, (uint)NppMsg.NPPPLUGINMENU);
      for (int i = 0; i < actions.iniKeys.Length && i < PluginData.FuncItems.Items.Count; ++i)
      {
        try
        {
          var itemName = actions.iniKeys[i];
          var itemID = PluginData.FuncItems.Items[i].CmdID;
          fixed (char* lpNewItem = itemName)
          {
            ModifyMenu(hMenu, itemID, MF_BYCOMMAND | MF_STRING, (UIntPtr)itemID, (IntPtr)lpNewItem);
          }
        }
        catch { }
      }
      */
    }


    /// <summary>
    ///   Calculate the difference between 2 strings using the Levenshtein distance algorithm
    /// </summary>
    /// <param name="source1">First string</param>
    /// <param name="source2">Second string</param>
    /// <remarks>
    /// Adapted from <see href="https://gist.github.com/Davidblkx/e12ab0bb2aff7fd8072632b396538560"/>
    /// </remarks>
    public static int calculateLevenshtein(string source1, string source2) //O(n*m)
    {
      var source1Length = source1.Length;
      var source2Length = source2.Length;

      var matrix = new int[source1Length + 1, source2Length + 1];

      // First calculation, if one entry is empty return full length
      if (source1Length == 0)
        return source2Length;

      if (source2Length == 0)
        return source1Length;

      // Initialization of matrix with row size source1Length and columns size source2Length
      for (var i = 0; i <= source1Length; matrix[i, 0] = i++) { }
      for (var j = 0; j <= source2Length; matrix[0, j] = j++) { }

      // Calculate rows and collumns distances
      for (var i = 1; i <= source1Length; i++)
      {
        for (var j = 1; j <= source2Length; j++)
        {
          var cost = (source2[j - 1] == source1[i - 1]) ? 0 : 1;

          matrix[i, j] = Math.Min(
            Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
              matrix[i - 1, j - 1] + cost);
        }
      }

      // Return result
      return matrix[source1Length, source2Length];
    }

    // P/Invoke helpers not present in Win32 wrapper
    private static class NativeMethods
    {
      [DllImport("user32", SetLastError = true)]
      public static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

      [DllImport("user32")]
      public static extern bool DrawMenuBar(IntPtr hWnd);
    }
  }
}
