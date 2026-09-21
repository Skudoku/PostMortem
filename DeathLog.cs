using System;
using System.IO;

namespace DeathRecap
{
    internal static class DeathLog
    {
        internal static void Append(string recap)
        {
            if (!Plugin.WriteDeathLog.Value)
            {
                return;
            }

            try
            {
                string path = Path.Combine(Plugin.PluginDir, "DeathLog.txt");
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {recap.Replace("\n", " | ")}";
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not write death log: " + ex.Message);
            }
        }
    }
}
