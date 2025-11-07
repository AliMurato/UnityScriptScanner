using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityProjectAnalyzerBonus.Program;

namespace UnityProjectAnalyzerBonus
{
    // Scan all .cs files and their GUIDs
    internal static class ScriptScanner
    {
        public static List<ScriptInfo> FindScripts(string projectRoot)
        {
            var result = new List<ScriptInfo>();

            // Iterate over all .cs files in the project
            foreach (string csPath in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
            {
                // For each .cs, look for a .meta with the same name
                string metaPath = csPath + ".meta";
                if (!File.Exists(metaPath))
                    continue; // if there is no meta, treat it as not a Unity script

                // Read guid from .meta
                string? guid = ReadGuidFromMeta(metaPath);
                if (guid == null)
                    continue;

                // Relative path like Assets/...
                string relPath = Path.GetRelativePath(projectRoot, csPath).Replace('\\', '/');

                result.Add(new ScriptInfo(guid, relPath));
            }

            return result;
        }

        // Reads a "guid: ..." line from a .meta file
        private static string? ReadGuidFromMeta(string metaPath)
        {
            foreach (string line in File.ReadLines(metaPath))
            {
                string trimmed = line.TrimStart();
                if (!trimmed.StartsWith("guid:", StringComparison.Ordinal))
                    continue;

                string[] parts = trimmed.Split(':', 2);
                if (parts.Length == 2)
                    return parts[1].Trim();
            }

            return null;
        }
    }
}
