using System.Collections.Generic;
using System.IO;

namespace DeathRecap
{
    // Loose per-language "key=value" text files in a Lang/ folder next to the DLL, one file
    // per language named exactly as Valheim's own Localization.GetSelectedLanguage() returns
    // it (e.g. "English.txt", "German.txt"). Falls back to English for any missing key or
    // missing language file, so a partial translation never breaks anything.
    internal static class Loc
    {
        private const string DefaultLanguage = "English";
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();

        internal static int LoadedLanguageCount => Tables.Count;

        internal static void LoadAll(string langDir)
        {
            Tables.Clear();
            if (!Directory.Exists(langDir))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(langDir, "*.txt"))
            {
                string language = Path.GetFileNameWithoutExtension(file);
                Dictionary<string, string> table = new Dictionary<string, string>();

                foreach (string rawLine in File.ReadAllLines(file))
                {
                    string line = rawLine.TrimEnd('\r');
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }

                    table[line.Substring(0, eq).Trim()] = line.Substring(eq + 1);
                }

                Tables[language] = table;
            }
        }

        internal static string Tr(string key, params object[] args)
        {
            string language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : DefaultLanguage;
            string raw = Lookup(language, key) ?? Lookup(DefaultLanguage, key) ?? key;
            return args != null && args.Length > 0 ? string.Format(raw, args) : raw;
        }

        private static string Lookup(string language, string key)
        {
            if (language != null && Tables.TryGetValue(language, out Dictionary<string, string> table) && table.TryGetValue(key, out string value))
            {
                return value;
            }
            return null;
        }
    }
}
