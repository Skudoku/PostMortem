using System;
using System.Collections.Generic;
using System.IO;

namespace DeathRecap
{
    // Remembers who has killed you recently, so the recap can say "that's the third time" instead
    // of repeating generic advice. Persisted next to the plugin so it survives a restart.
    // Read during BuildContext and written afterwards, so the current death never counts itself.
    internal static class DeathHistory
    {
        private const int MaxEntries = 30;
        private static readonly List<string> Killers = new List<string>();
        private static string FilePath => Path.Combine(Plugin.PluginDir, "DeathHistory.txt");

        internal static void Load()
        {
            Killers.Clear();
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string name = line.Trim();
                    if (name.Length > 0)
                    {
                        Killers.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not read death history: " + ex.Message);
            }
        }

        internal static int TimesKilledBy(string attackerName)
        {
            if (string.IsNullOrEmpty(attackerName))
            {
                return 0;
            }

            int count = 0;
            foreach (string killer in Killers)
            {
                if (string.Equals(killer, attackerName, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
            return count;
        }

        internal static bool LastKillerWas(string attackerName)
        {
            return !string.IsNullOrEmpty(attackerName)
                && Killers.Count > 0
                && string.Equals(Killers[Killers.Count - 1], attackerName, StringComparison.OrdinalIgnoreCase);
        }

        internal static void Record(DeathContext ctx)
        {
            // Environmental deaths have no attacker - store a placeholder so "last killer" stays
            // accurate rather than silently skipping the death.
            string name = string.IsNullOrEmpty(ctx.AttackerName) ? "-" : ctx.AttackerName;

            Killers.Add(name);
            while (Killers.Count > MaxEntries)
            {
                Killers.RemoveAt(0);
            }

            try
            {
                File.WriteAllLines(FilePath, Killers);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not write death history: " + ex.Message);
            }
        }
    }
}
