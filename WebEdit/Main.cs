using Npp.DotNet.Plugin;
using System.Runtime.InteropServices;
using System.Text;
using WebEdit.Properties;
using static Npp.DotNet.Plugin.Win32;
using static Npp.DotNet.Plugin.Winforms.WinGDI;
using static Npp.DotNet.Plugin.Winforms.WinUser;
using static System.Diagnostics.FileVersionInfo;

namespace WebEdit {
  partial class Main : IDotNetPlugin {
    /// <summary>See <see href="https://github.com/alex-ilin/WebEdit/blob/7bb4243/Legacy-v2.1/Src/Tags.ob2#L16"/></summary>
    public const int MaxKeyLen = 32;
    internal const string PluginName = "WebEdit";
    private const string MenuCmdPrefix = $"{PluginName} -";
    private const string IniFileName = PluginName + ".ini";
    private const string Version = "2.8";
    private static string[] currentIniKeys = null; // Temporary storage of the current [Commands] keys to detect changes on reload
    private static string[] currentToolbarKeys = null; // Temporary storage of the current [Toolbar] keys to detect changes on reload
    private static bool currentKeysChangeAlerted = false; // Whether the user has been alerted about changes in [Commands] and/or [Toolbar] section
    private static string MsgBoxCaption = $"{PluginName} {Version}";
    private static bool pluginACOpened = false;
    private static string pluginACSpecialText = "";
    private static Dictionary<string, string[]> pluginACIcons = new();
    private const string AboutMsg =
      "This small freeware plugin allows you to wrap the selected text in "
      + "tag pairs and expand abbreviations using a hotkey.\n"
      + "For more information visit https://github.com/Krazal/WebEdit\n"
      + "\n"
      + "Created by Alexander Iljin (Amadeus IT Solutions) using XDS Oberon, "
      + "March 2008 - March 2010.\n"
      + "Ported to C# by Miguel Febres, April 2021.\n"
      + "Ported to .NET 8 by Robert Di Pardo, February 2025.\n"
      + "Maintained by Richard Stockinger since September 2025.\n"
      + "Contact e-mail: AlexIljin@users.SourceForge.net";

    static IniFile ini = null;
    static bool isConfigDirty = false;
    internal static string iniDirectory, iniFilePath = null;

    public void OnBeNotified(ScNotification notification)
    {
      uint code = notification.Header.Code;

      // Handle Notepad++ notifications
      if (notification.Header.HwndFrom == PluginData.NppData.NppHandle)
      {
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

      // Handle Scintilla notifications (auto-completion)
      // if (notification.Header.HwndFrom == Utils.GetCurrentScintilla()) 
      else
      {
        switch ((SciMsg)code)
        {

          // User cancelled autocompletion (e.g. add tag)
          case SciMsg.SCN_AUTOCCANCELLED:
            pluginACOpened = false;
            break;

          // Autocompletion selected; check if the user selected a special autocompletion entry (e.g. add/find tag)
          case SciMsg.SCN_AUTOCSELECTION:
            if (pluginACOpened)
            {
              var scintillaGateway = new ScintillaGateway(Utils.GetCurrentScintilla());

              // Get the autocompletion text in the correct encoding
              IntPtr textPointer;
              textPointer = notification.TextPointer;
              string acSelectedText = (scintillaGateway.CodePage == Encoding.UTF8)
                ? Marshal.PtrToStringUTF8(textPointer)
                : Marshal.PtrToStringAnsi(textPointer);

              // Check if the user selected the "find/add tag" entry
              if (pluginACSpecialText == acSelectedText)
              {
                IntPtr currentScint = Utils.GetCurrentScintilla();
                SendMessage(currentScint, (uint)SciMsg.SCI_AUTOCCANCEL); // Close the AC list; do not modify the current tag
                EditConfigAddOrFindTag(); // User selected the "find/add tag" entry; open the ini-file to add the new tag
              }
              else if (!string.IsNullOrEmpty(ini.Get("Tags", acSelectedText).Trim('\0')))
              {
                scintillaGateway.ReplaceSel(""); // Remove the original text before inserting the tag related expansion
              }
              else // This is not a plugin related autocompletion (any more)...
              {
                pluginACOpened = false;
              }
            }
            break;

          // Autocompletion was performed; handle tag insertion if necessary
          case SciMsg.SCN_AUTOCCOMPLETED:
            if (pluginACOpened)
            {
              HandleTag();
              pluginACOpened = false;
            }
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
    /// Open the ini-file in Notepad++ for editing and prepare to add a new tag in [Tags] section
    /// </summary>
    internal static void EditConfigAddOrFindTag()
    {      
      IntPtr currentScint = Utils.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint);
      string tagToAddOrFind = scintillaGateway.GetSelText();
      EditConfig();
      if (isConfigDirty)
      {
        if (string.IsNullOrEmpty(tagToAddOrFind) || tagToAddOrFind.Length > MaxKeyLen)
          return;

        // Get the entire document text
        scintillaGateway.SetTargetRange(0, scintillaGateway.GetTextLength());
        string docText = scintillaGateway.GetTargetText();

        // Find the [Tags] section
        int tagsSectionStart = docText.IndexOf("[Tags]", StringComparison.InvariantCultureIgnoreCase);
        if (tagsSectionStart >= 0)
        {

          // Get the line ending style from Scintilla (shortened variable name)
          string tmpEOL = scintillaGateway.LineDelimiter; // "\r\n", "\r" or "\n"

          // Try to find the tag in the ini-file
          string existingValue = ini.Get("Tags", tagToAddOrFind);
          if (!string.IsNullOrEmpty(existingValue.Trim('\0')))
          {

            // Scroll to the existing tag
            int tagPos = docText.IndexOf($"{tmpEOL}{tagToAddOrFind}=", tagsSectionStart + 6);
            if (tagPos >= 0)
            {
              // Convert the character position to byte position for Scintilla
              int bytePos = scintillaGateway.CodePage.GetByteCount(docText.AsSpan(0, tagPos));
              int caretPos = bytePos + tmpEOL.Length + scintillaGateway.CodePage.GetByteCount(tagToAddOrFind) + 1;
              scintillaGateway.SetSelection(caretPos, caretPos);
              _ = MsgBoxDialog(// Allow time for file (view) loading ^^'
                PluginData.NppData.NppHandle,
                $"Tag found: \"{tagToAddOrFind}\"",
                MsgBoxCaption,
                (uint)(MsgBox.ICONINFORMATION | MsgBox.OK));
              scintillaGateway.EnsureVisible(scintillaGateway.LineFromPosition(caretPos));
              scintillaGateway.ScrollCaret();
              return;
            }
          }

          // No existing tag found - prepare to add a new one (Confirm history drop if REDO is possible)
          bool needAlert = true;
          if (scintillaGateway.CanRedo())
          {
            DlgResult redoDlgResult = MsgBoxDialog(
              PluginData.NppData.NppHandle,
              $"You may lose your \"Redo\" history if you insert the following tag: \"{tagToAddOrFind}\"\n\nContinue?",
              MsgBoxCaption,
              (uint)(MsgBox.ICONWARNING | MsgBox.OKCANCEL));
            if (redoDlgResult != DlgResult.OK)
              return;
            needAlert = false;
          }

          // Find the start of the next section or end of file
          int nextSectionStart = docText.IndexOf($"{tmpEOL}[", tagsSectionStart + 6);
          if (nextSectionStart < 0) nextSectionStart = docText.Length;

          // Find the last non-empty line in the [Tags] section
          int lastTagLineEnd = -1;
          int searchPos = tagsSectionStart;
          
          while (searchPos < nextSectionStart)
          {
            int lineEnd = docText.IndexOf(tmpEOL, searchPos);
            if (lineEnd < 0) break;

            // Reached the next section
            if (lineEnd == nextSectionStart)
            {
              lastTagLineEnd = lineEnd;
              break;
            }

            // Check if the line is non-empty and not a comment or section header
            string line = docText[searchPos..lineEnd].Trim();
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(";") && !line.StartsWith("#") && !string.Equals(line, "[Tags]", StringComparison.InvariantCultureIgnoreCase))
            {
              lastTagLineEnd = lineEnd;
            }

            // Reached the penultimate (last but one) line of the file
            int lastLineEnd = docText.LastIndexOf(tmpEOL);
            if (lineEnd == lastLineEnd && !string.IsNullOrWhiteSpace(docText[lineEnd..nextSectionStart]))
            {
              lastTagLineEnd = (docText[lineEnd..nextSectionStart] == tmpEOL)
                ? lineEnd + tmpEOL.Length // the last line is empty
                : nextSectionStart; // the last line is non-empty
              break;
            }

            searchPos = lineEnd + tmpEOL.Length;
          }

          // Convert the character position to byte position for Scintilla
          int insertPos;
          if (lastTagLineEnd >= 0)
          {
            var textBeforeInsert = docText[..lastTagLineEnd];
            insertPos = scintillaGateway.CodePage.GetByteCount(textBeforeInsert);
          }
          else
          {
            // No existing tags - insert after [Tags] line
            var textBeforeInsert = docText[..(tagsSectionStart + 6)];
            insertPos = scintillaGateway.CodePage.GetByteCount(textBeforeInsert);
          }

          // Insert the new tag and place the caret after the '=' of the new tag
          scintillaGateway.SetSelection(insertPos, insertPos);
          scintillaGateway.ReplaceSel($"{tmpEOL}{tagToAddOrFind}=");
          long selEnd = scintillaGateway.GetSelectionEnd();
          scintillaGateway.SetSelection(selEnd, selEnd);

          // Allow time for file (view) loading ^^'
          if (needAlert)
            _ = MsgBoxDialog(
              PluginData.NppData.NppHandle,
              $"Tag added and ready for editing: \"{tagToAddOrFind}\"",
              MsgBoxCaption,
              (uint)(MsgBox.ICONINFORMATION | MsgBox.OK));
          scintillaGateway.EnsureVisible(scintillaGateway.LineFromPosition(selEnd));
          scintillaGateway.ScrollCaret();
        }
      }
    }

    /// <summary>
    /// Open the ini-file in Notepad++ for editing
    /// </summary>
    internal static void EditConfig()
    {
      if (new NotepadPPGateway().OpenFile(iniFilePath))
      {
        isConfigDirty = true;
      }
      else
      {
        _ = MsgBoxDialog(
          PluginData.NppData.NppHandle,
          "Failed to open the configuration file for editing:\n" + iniFilePath,
          MsgBoxCaption,
          (uint)(MsgBox.ICONWARNING | MsgBox.OK));
      }
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
    /// ini-file section. If multiple selections exist, handles each one starting
    /// from the last selection.
    /// </summary>
    internal static void ReplaceTag()
    {
      var cmds = new Actions(ini);
      cmds.HelpCommands(() => HandleTag(), () => HandleTag(false, true));
    }

    /// <summary>
    /// Recommend tags similar to the one at the caret using autocompletion.
    /// </summary>
    internal static void RecommendTags()
    {
      IntPtr currentScint = Utils.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint);
      if (scintillaGateway.GetSelections() <= 1) // "There is always at least one selection", but just in case...
      {
        // Single (or no) selection - handle normally 
        HandleTag(true);
        return;
      }

      // Multiple selections - notify the user that this is not supported via tooltip
      scintillaGateway.CallTipShow(scintillaGateway.GetCurrentPos(), "Tag recommendation is not supported in multi-selection mode");
    }


    /// <summary>
    /// Handle tag replacement or recommendation
    /// </summary>
    private static void HandleTag(bool alwaysRecommendTags = false, bool isMultiSelection = false)
    {
      IntPtr currentScint = Utils.GetCurrentScintilla();
      ScintillaGateway scintillaGateway = new ScintillaGateway(currentScint);
      long lineCurrent = scintillaGateway.GetCurrentLineNumber();
      long lineStart = scintillaGateway.PositionFromLine(lineCurrent);
      long lineStartNext = scintillaGateway.PositionFromLine(lineCurrent + 1);
      string selectedText = scintillaGateway.GetSelText();

      // Hide calltip/autocompletion if visible
      if (scintillaGateway.CallTipActive()) // For the sake of completeness...
        SendMessage(currentScint, (uint)SciMsg.SCI_CALLTIPCANCEL);
      if (scintillaGateway.AutoCActive())
        SendMessage(currentScint, (uint)SciMsg.SCI_AUTOCCANCEL);

      if (string.IsNullOrEmpty(selectedText))
      {
        string tag = PluginData.Notepad.GetCurrentWord();
        scintillaGateway.SetTargetRange(lineStart, lineStartNext);
        string lineText = scintillaGateway.GetTargetText();
        long tmpPosition = scintillaGateway.GetCurrentPos();

        if (string.IsNullOrEmpty(lineText))
        {
          if (!isMultiSelection)
            scintillaGateway.CallTipShow(tmpPosition, "Empty line");
          return;
        }

        // Find the last occurrence of the tag in the current line before the caret
        // NOTE: Scintilla positions are byte offsets for the document encoding,
        // while string.IndexOf works on characters. Convert between bytes and chars
        // using the document encoding to make the search Unicode-aware.
        long tagStartCharPos = -1;

        // Byte position of the caret relative to the line start
        long caretBytePosInLine = tmpPosition - lineStart;

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
        {
          if (!isMultiSelection)
            scintillaGateway.CallTipShow(tmpPosition, "No tag found");
          return;
        }

        // Convert the character position of the found tag into a byte position
        int tagStartByteOffset = scintillaGateway.CodePage.GetByteCount(lineText.AsSpan(0, (int)tagStartCharPos));
        long selStart = lineStart + tagStartByteOffset;

        long tagLengthBytes = scintillaGateway.CodePage.GetByteCount(tag);
        long selEnd = selStart + tagLengthBytes;
        scintillaGateway.SetSelection(selStart, selEnd);
        selectedText = scintillaGateway.GetSelText();
      }

      long position = scintillaGateway.GetSelectionEnd();
      try
      {
        if (!isMultiSelection)
          scintillaGateway.BeginUndoAction();
        if (string.IsNullOrEmpty(selectedText?.Trim()))
        {
          position = scintillaGateway.GetCurrentPos();
          scintillaGateway.ClearSelectionToCursor();
          if (!isMultiSelection)
            scintillaGateway.CallTipShow(position, "No tag here");
          return;
        }
        else if (selectedText.Length > MaxKeyLen)
        {
          if (!isMultiSelection)
            scintillaGateway.CallTipShow(position, $"Maximum tag length is {MaxKeyLen} characters");
          return;
        }

        LoadConfig();
        string value = ini.Get("Tags", selectedText);

        // Show AC with similar tags (and/or add tag) if no exact match found or if forced
        if (!isMultiSelection && (string.IsNullOrEmpty(value.Trim('\0')) || alwaysRecommendTags))
        {

          // Try to find similar tags (even case-insensitive) using Levenshtein distance
          int shortestDistance = -1;
          List<string> similarTags = [];
          var iniTags = ini.GetKeys("Tags");
          var closestTag = string.Empty;

          // Collect similar tags
          for (int i = 0; i < iniTags.Length; ++i)
          {

            // Exact match found (see: RecommendTags)
            if (selectedText == iniTags[i])
            {
              closestTag = iniTags[i];
              shortestDistance = 0; // Mark as the closest match
              similarTags.Add(iniTags[i]);
              continue;
            }

            // More than half of the tag must match the selected text (to avoid a list that's too long)
            int tmpDistance = calculateLevenshtein(selectedText, iniTags[i], true);
            if (tmpDistance <= Math.Max(selectedText.Length, iniTags[i].Length) / 2) // Remove `/ 2` for more results. In this case `<` should be used to find at least one matching character
            {
              if (tmpDistance < shortestDistance || shortestDistance < 0)
              {
                shortestDistance = tmpDistance;
                closestTag = iniTags[i];
              }
              similarTags.Add(iniTags[i]);
            }
          }

          // Show autocompletion e.g. if similar tags were found
          int  origACSeparator     = scintillaGateway.AutoCGetSeparator();
          int  origACTypeSeparator = scintillaGateway.AutoCGetTypeSeparator();
          bool origACIgnoreCase    = scintillaGateway.AutoCGetIgnoreCase();
          bool origACAutoHide      = scintillaGateway.AutoCGetAutoHide();
          scintillaGateway.AutoCSetSeparator(10);      // New line / Line Feed (\n) -- see below
          scintillaGateway.AutoCSetTypeSeparator(13);  // Carriage Return (\r) -- see `else` block below
          scintillaGateway.AutoCSetIgnoreCase(false);  // Necessary for `AutoCSelect()` below!
          scintillaGateway.AutoCSetAutoHide(false);    // Keep AC open until user cancels or selects an entry (fix bug [?] when the first entry is selected, and AC closes immediately)
          // scintillaGateway.ClearRegisteredImages(); // Not recommended; this may affect Notepad++'s AC images too!
          pluginACOpened = true;
          if (shortestDistance >= 0)
          {
            bool isExactMatch = (selectedText == closestTag);

            // XPM image data for the search/add icons -- [Find...]/[Add...] entry/entries (see below)
            bool hasDarkMode = PluginData.Notepad.GetNppVersion() switch { (int maj, _, _) => maj >= 8 }
              && SendMessage(PluginData.NppData.NppHandle, (uint)NppMsg.NPPM_ISDARKMODEENABLED) != IntPtr.Zero;

            // Set icon name, aka. `pluginACIcons` key
            string iconName = isExactMatch
              ? GetIconPath(hasDarkMode ? "search-dm" : "search")
              : GetIconPath(hasDarkMode ? "add-dm" : "add");

            // Check if the `pluginACIcons[iconName]` exists + add if necessary (micro-optimization, but why not) (:
            if (!pluginACIcons.ContainsKey(iconName))
            {

              // Prepare XPM image data
              string[] icon2Reg = {
                "12 12 2 1", // Columns, Rows, Number of colors, Chars per pixel
                "  c None",  // Transparent background where space character is used
                hasDarkMode ? ". c #C8C8C8" : ". c #151515",    // Dark/Light mode friendly foreground color where dot character is used
                isExactMatch ? "   .....    " : "     ..     ", // Pixels: search or add icon
                isExactMatch ? " .........  " : "     ..     ",
                isExactMatch ? "...     ... " : "     ..     ",
                isExactMatch ? "..       .. " : "     ..     ",
                isExactMatch ? "..       .. " : "     ..     ",
                isExactMatch ? "..       .. " : "............",
                isExactMatch ? "...     ... " : "............",
                isExactMatch ? " .........  " : "     ..     ",
                isExactMatch ? "   .......  " : "     ..     ",
                isExactMatch ? "         .. " : "     ..     ",
                isExactMatch ? "          .." : "     ..     ",
                isExactMatch ? "           ." : "     ..     "
              };
              pluginACIcons[iconName] = icon2Reg;
            }

            // Register XPM image data with Scintilla
            unsafe
            {

              // Convert to (unmanaged) ANSI strings
              IntPtr[] xpmData = new IntPtr[pluginACIcons[iconName].Length];
              try
              {
                for (int i = 0; i < pluginACIcons[iconName].Length; i++)
                {
                  xpmData[i] = Marshal.StringToHGlobalAnsi(pluginACIcons[iconName][i]);
                }

                // The Scintilla API expects a pointer to an array of pointers to ANSI strings (char*[])
                fixed (IntPtr* pXpmData = xpmData)
                  SendMessage(currentScint, (uint)SciMsg.SCI_REGISTERIMAGE, 5001, (IntPtr)pXpmData); // Image ID 5001 -- try to avoid conflicts
              }
              finally
              {
                for (int i = 0; i < xpmData.Length; i++)
                {
                  if (xpmData[i] != IntPtr.Zero)
                    Marshal.FreeHGlobal(xpmData[i]);
                }
              }
            }

            // Order + join the similar tags by alphabetical order
            similarTags.Sort(StringComparer.InvariantCultureIgnoreCase);
            string similarTagList = "\n" + string.Join("\n", similarTags.Distinct()) + "\n";

            // Add find/add option to the AC list
            pluginACSpecialText = isExactMatch
              ? $"[Find \"{selectedText}\" in WebEdit.ini]"
              : $"[Add \"{selectedText}\" to WebEdit.ini]";
            string similarTagReplaceText = $"{closestTag}\n{pluginACSpecialText}\r5001"; // Closest tag, then find/add + the appropriate icon (ID 5001)

            // Replace the closest tag entry with the find/add instruction at the top
            similarTagList = similarTagList.Replace($"\n{closestTag}\n", $"\n{similarTagReplaceText}\n").Trim('\n');

            /* NOT WORKED :( -- Added `AutoCSetAutoHide(false)` instead
            // If the closest tag is the first entry, the Scintilla AC list won't open properly (even if we do NOT call `AutoCSelect()` below [?])
            // So add a leading newline (empty entry) to workaround this issue
            if (similarTags[0] == closestTag)
              similarTagList = "\n" + similarTagList;
            // */

            // encode tag list according to the document in case it's in ASCII mode
            unsafe
            {
              fixed (byte* pText = scintillaGateway.CodePage.GetBytes(similarTagList + "\0"))
                SendMessage(currentScint, SciMsg.SCI_AUTOCSHOW, 0, (IntPtr)pText); // (UIntPtr)lengthEntered (0): show AC all items
            }

            // Select the closest tag in the AC list
            // Attention! If the tag (closestTag) isn't found (or it is in the first place inn spec. cases?), the AC list will NOT be displayed!
            scintillaGateway.AutoCSelect(closestTag);
          }
          else
          {
            pluginACSpecialText = $"Tag not found. Add \"{selectedText}\" to WebEdit.ini?";
            unsafe
            {
              fixed (byte* pText = scintillaGateway.CodePage.GetBytes(pluginACSpecialText + "\0"))
                SendMessage(currentScint, SciMsg.SCI_AUTOCSHOW, 0, (IntPtr)pText);
            }
          }

          // Reset AC settings + return
          scintillaGateway.AutoCSetSeparator(origACSeparator);
          scintillaGateway.AutoCSetTypeSeparator(origACTypeSeparator);
          scintillaGateway.AutoCSetIgnoreCase(origACIgnoreCase);
          scintillaGateway.AutoCSetAutoHide(origACAutoHide);
          return;
        }
        else if (isMultiSelection && string.IsNullOrEmpty(value.Trim('\0')))
        {
          // In multi-selection mode, silently skip tags with no defined expansion
          return;
        }

        long selStart = scintillaGateway.GetSelectionStart();
        long indentPos = Math.Min(lineStart + (scintillaGateway.GetUseTabs() ? (scintillaGateway.GetLineIndentation(lineCurrent) / scintillaGateway.GetTabWidth()) : scintillaGateway.GetLineIndentation(lineCurrent)), selStart);
        Tags parser = new(lineStart, indentPos);
        bool isUDInsPoint = Tags.UserDefinedInsertionPoint().IsMatch(value);
        if (isUDInsPoint)
        {
          // Replace the user-defined insertion point with a never-occurring sequence
          value = Tags.UserDefinedInsertionPoint().Replace(value, "\n\r", 1); // Replace only the FIRST match
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

      }
      catch (Exception ex)
      {
        scintillaGateway.CallTipShow(position, ex.Message);
      }
      finally
      {
        if (!isMultiSelection)
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

      // Alert if the [Commands] or [Toolbar] section has changed
      if (currentIniKeys != null && (!currentIniKeys.SequenceEqual(actions.iniKeys) || !currentToolbarKeys.SequenceEqual(actions.toolbarKeys)))
      {
        if (!currentKeysChangeAlerted)
        {
          string tmpSection = (currentIniKeys.SequenceEqual(actions.iniKeys))
            ? "[Toolbar]"
            : (currentToolbarKeys.SequenceEqual(actions.toolbarKeys))
              ? "[Commands]"
              : "[Commands] and [Toolbar]";
          MsgBoxDialog(
          PluginData.NppData.NppHandle,
          $"The {tmpSection} configuration has changed.\n\nPlease restart Notepad++ for all changes to take effect",
          MsgBoxCaption,
          (uint)(MsgBox.ICONWARNING | MsgBox.OK));
        }
        currentKeysChangeAlerted = true;
        return;
      }
      currentIniKeys = actions.iniKeys;
      currentToolbarKeys = actions.toolbarKeys;

      // Add menu items for each command in the [Commands] section
      bool foundItem = false;
      int cmdIndex = 0;
      foreach (string key in actions.iniKeys)
      {
        var methodInfo = actions.GetCommand(cmdIndex++);
        if (methodInfo == null)
          break;

        Utils.SetCommand(
          $"{MenuCmdPrefix} {key}",
          () =>
          {
            var cmds = new Actions(ini);

            // Alert if the [Commands] or [Toolbar] section has changed
            if (currentIniKeys != null && (!currentIniKeys.SequenceEqual(cmds.iniKeys) || !currentToolbarKeys.SequenceEqual(cmds.toolbarKeys)))
            {
              string tmpSection = (currentIniKeys.SequenceEqual(cmds.iniKeys))
                ? "[Toolbar]"
                : (currentToolbarKeys.SequenceEqual(cmds.toolbarKeys))
                  ? "[Commands]"
                  : "[Commands] and [Toolbar]";
              MsgBoxDialog(
              PluginData.NppData.NppHandle,
              $"The {tmpSection} section and thus the menu/toolbar configuration has changed.\n\nPlease restart Notepad++ for all changes to take effect",
              MsgBoxCaption,
              (uint)(MsgBox.ICONWARNING | MsgBox.OK));
              return;
            }

            // Execute the command
            cmds.ExecuteCommand(ini.Get("Commands", key));
          });
        foundItem = true;
      }

      // Add standard menu items
      if (foundItem)
      {
        Utils.MakeSeparator();
      }

      Utils.SetCommand( // Try to replace tag at caret/selection
        "&Replace Tag",
        ReplaceTag,
        new ShortcutKey(FALSE, TRUE, FALSE, 13));
      Utils.SetCommand( // Always show AC with recommended tags
        "R&ecommend Tags",
        RecommendTags,
        new ShortcutKey(FALSE, TRUE, TRUE, 13));
      Utils.MakeSeparator();
      Utils.SetCommand("E&dit Config", EditConfig);
      Utils.SetCommand("&Load Config", LoadConfig);
      Utils.SetCommand("&About...", About);


      /* DEPRECATED (may cause unexpected behavior)
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
    /// <param name="caseSensitive">Case sensitive calculation?</param>
    /// <remarks>
    /// Adapted from <see href="https://gist.github.com/Davidblkx/e12ab0bb2aff7fd8072632b396538560"/>
    /// </remarks>
    private static int calculateLevenshtein(string source1, string source2, bool caseSensitive = false)
    {
      if (caseSensitive)
      {
        source1 = source1.ToLowerInvariant();
        source2 = source2.ToLowerInvariant();
      }
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
  }
}
