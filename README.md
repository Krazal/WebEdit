```
  █████   ███   █████          █████     ██████████     █████  ███   █████
 ░░███   ░███  ░░███          ░░███     ░░███░░░░░█    ░░███  ░░░   ░░███
  ░███   ░███   ░███   ██████  ░███████  ░███  █ ░   ███████  ████  ███████
  ░███   ░███   ░███  ███░░███ ░███░░███ ░██████    ███░░███ ░░███ ░░░███░
  ░░███  █████  ███  ░███████  ░███ ░███ ░███░░█   ░███ ░███  ░███   ░███
   ░░░█████░█████░   ░███░░░   ░███ ░███ ░███ ░   █░███ ░███  ░███   ░███ ███
     ░░███ ░░███     ░░██████  ████████  ██████████░░████████ █████  ░░█████
      ░░░   ░░░       ░░░░░░  ░░░░░░░░  ░░░░░░░░░░  ░░░░░░░░ ░░░░░    ░░░░░
```

# WebEdit v2.9

A lightweight but powerful Notepad++ plugin for **expanding abbreviations** and **wrapping selections** with customizable tags.

## Features

### Using Commands/Toolbars and Tags
![Commands/Toolbars and Tags](https://github.com/user-attachments/assets/6779b9ec-0d3c-4c00-ae69-40a67d2c48ac)
- Tag pair wrapping around selected text
- Quick tag insertion using customizable abbreviations

### Multi-Selection Support
![Multi-select Tag Operations](https://github.com/user-attachments/assets/9897fa26-8238-4826-b6d5-66e24578be9c)
- Bulk wrapping of multiple selections
- Apply tags to multiple selections simultaneously

### Suggest Similar Tags or Add New Tags
![Suggest Similar Tags, Add New Tag](https://github.com/user-attachments/assets/89cdf24a-a404-47ea-9448-959225654160)
- Suggest similar tags if a tag is not found
- Add new tags easily and dynamically
- Just save the `WebEdit.ini` file and the new tag is available

### Tag Customization
![Finding and Modifying Tags](https://github.com/user-attachments/assets/a70674bb-e1cc-4b9a-a932-10e2be280fa5)
- Search and modify existing tags
- Flexible tag configuration

## Dynamic Insertions

WebEdit supports several dynamic insertion sequences:

| Sequence | Description | Example |
|----------|-------------|---------|
| [`\r`,] `\n`, `\t` | [Carriage return,] New line, Tab | `lines=line1\nline2` → line1<br>line2 |
| `\i` | Indentation (spaces or tabs) | `if=if ()\n\i# code` → if ()<br>&nbsp;&nbsp;&nbsp;&nbsp;# code |
| `\c` | Clipboard content | `clip=Clipboard: \c` → Clipboard: Hello World! |
| `\|` | Caret position | `func=function() {\|}` → function() {‸} |
| `\u` | Current Windows username | `user=Author: \u` → Author: JohnDoe |
| `\x` | Current filename | `file=\x` → document.html |
| `\p` | Full file path | `path=\p` → C:\Projects\document.html |
| `\d:"format"` | Formatted date/time | `datetime=\d:"yyyy-MM-dd"` → 2025-01-01 |

*For more details, escape sequences and examples (tags), see the `[Tags]` section of the [WebEdit/Resources/WebEdit.ini](https://github.com/Krazal/WebEdit/blob/master/WebEdit/Resources/WebEdit.ini#L52) file*

### Date Format Examples
```ini
created=Date: \d:"yyyy-MM-dd HH:mm:ss" # Date: 2025-01-01 14:30:00
modified=\d:"MMM d, yyyy"              # Jan 1, 2025
now=\d:"dddd dd MMMM, hh:mm tt"        # Wednesday 01 January, 02:30 PM
```

*For more format specifiers and examples, see: [Custom date and time format strings](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings#table)*

## Recent Updates (v2.8+)
- ✨ Full 64-bit Notepad++ support
- 🌐 Enhanced Unicode handling
- 📝 Improved caret positioning
- 💾 Backwards compatibility with existing configurations

## Installation

1. Install via Notepad++ Plugin Manager, or
2. Download from the [releases page](https://github.com/Krazal/WebEdit/releases)

## Credits

- Original creation: Alexander Iljin (Amadeus IT Solutions), 2008-2010
- C# Port: Miguel Febres, 2021
- .NET 8 Update: Robert Di Pardo, 2025
- Current Maintainer: Richard Stockinger

## License

This plugin is available as freeware. For more information, visit the [GitHub repository](https://github.com/Krazal/WebEdit).

---
*Note: For legacy documentation (v2.1), please refer to [Legacy-v2.1/ReadMe.txt](https://github.com/Krazal/WebEdit/blob/master/Legacy-v2.1/ReadMe.txt)*
