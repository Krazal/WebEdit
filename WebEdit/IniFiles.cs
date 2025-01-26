using System.Text.RegularExpressions;

namespace WebEdit
{
  class IniFile(string fileName)
  {
    public static readonly char ValueStringDelimiter = ';';

    private readonly char _keyValueSeparator = '=';
    private readonly Regex _iniFileComment = new(@"^[;#]\s?");

    // private const int maxValueLength = 256;
    // private const int maxKeysBuffer = 1024;

    private readonly string _fileName = fileName;

    private (string, string) ExtractKeyAndValue(string line)
    {
      var kv = line?.Split([_keyValueSeparator], StringSplitOptions.RemoveEmptyEntries);
      return (kv?.Length > 1) ? (kv.First(), string.Join("", kv.Skip(1)).Trim()) : ("", "");
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
        if (inSection && !_iniFileComment.IsMatch(line))
        {
          if (key == null)
            sectionKeys.Add(ExtractKeyAndValue(line).Item1);
          else if (line.Contains(key))
            return ExtractKeyAndValue(line).Item2;
        }

        inSection = inSection && !line.StartsWith('[') || line.StartsWith($"[{section}]");
      }

      return string.Join("\0", sectionKeys);
    }

    public string[] GetKeys(string section)
      => Get(section).Trim('\0').Split('\0');
  }
}
