using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace PregnantLordsExpanded.Diagnostics
{
    internal static class DiagnosticLog
    {
        private const ulong ModdingFilter = 17592186044416UL;
        private const string Prefix = "[PregnantLordsExpanded] ";
        private static readonly HashSet<string> WarnedKeys = new HashSet<string>(StringComparer.Ordinal);

        public static void Info(string message)
        {
            Debug.Print(Prefix + message, 0, Debug.DebugColor.White, ModdingFilter);
        }

        public static void WarnOnce(string key, string message)
        {
            if (string.IsNullOrEmpty(key))
            {
                key = message ?? string.Empty;
            }

            lock (WarnedKeys)
            {
                if (!WarnedKeys.Add(key))
                {
                    return;
                }
            }

            Debug.Print(Prefix + message, 0, Debug.DebugColor.Yellow, ModdingFilter);
        }
    }
}

