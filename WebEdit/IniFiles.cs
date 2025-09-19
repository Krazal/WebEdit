using System.Text.RegularExpressions;

namespace WebEdit
{
  partial class IniFile(string fileName)
  {
    public static readonly char ValueStringDelimiter = ';';

    private readonly char _keyValueSeparator = '=';

    [GeneratedRegex(@"^[;#]\s?")]
    private static partial Regex IniFileComment();

    // private const int maxValueLength = 256;
    // private const int maxKeysBuffer = 1024;

    private readonly string _fileName = fileName;

    private (string, string) ExtractKeyAndValue(string line)
    {
      var kv =
        Regex.Split(line ?? string.Empty, $@"(?i)(^[\p{{L}}\p{{N}} _\-\&]{{1,{Main.MaxKeyLen}}}){_keyValueSeparator}") // For Unicode support: `a-z0-9` » `\p{{L}}\p{{N}}`
        .Where(s => s.Trim() != string.Empty);
      return (kv?.Count() > 1) ? (kv.First(), string.Join("", kv.Skip(1)).Trim()) : ("", "");
    }

    /// <summary>
    /// Return a UTF8 string value of the given section's key.
    /// </summary>
    /// <param name="section">The [section] name that contains the key in the ini-file.</param>
    /// <param name="key">The key name in the ini-file, whose value is to be retrieved.</param>
    /// <returns>A UTF8 string value of the given section's key.</returns>
    public string Get(string section, string key = null)
    {
      if (!File.Exists(_fileName))
        return string.Empty;

      bool inSection = false;
      List<string> sectionKeys = [];

      foreach (var line in File.ReadAllLines(_fileName))
      {
        if (inSection && !IniFileComment().IsMatch(line))
        {
          if (key == null)
            sectionKeys.Add(ExtractKeyAndValue(line).Item1);
          else if (line.StartsWith($"{key}{_keyValueSeparator}", StringComparison.InvariantCulture))
            return ExtractKeyAndValue(line).Item2;
        }

        inSection = inSection && !line.StartsWith('[') || line.StartsWith($"[{section}]", StringComparison.InvariantCultureIgnoreCase);
      }

      return string.Join("\0", sectionKeys);
    }

    public string[] GetKeys(string section)
    {
      var keys = Get(section).Trim('\0');
      return string.IsNullOrEmpty(keys) ? Array.Empty<string>() : keys.Split('\0');
    }
  }
}
