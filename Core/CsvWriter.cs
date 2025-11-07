using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityProjectAnalyzerBonus.Program;

namespace UnityProjectAnalyzerBonus
{
    // Writing a CSV file with unused scripts
    internal static class CsvWriter
    {
        public static void WriteUnusedScripts(
            string outputRoot,
            List<ScriptInfo> scripts,
            HashSet<string> usedGuids)
        {
            string csvPath = Path.Combine(outputRoot, "UnusedScripts.csv");

            // Filter all scripts by condition "GUID not present in usedGuids"
            //  + sort: first by path depth (fewer slashes -> higher),
            //          then alphabetically by path
            var unused = scripts
                .Where(s => !usedGuids.Contains(s.Guid))
                .OrderBy(s => s.RelativePath.Count(c => c == '/'))   // less nested first
                .ThenBy(s => s.RelativePath)                         // then by name
                .ToList();

            using var writer = new StreamWriter(csvPath, false, Encoding.UTF8);

            // CSV header
            writer.WriteLine("Relative Path,GUID");

            // Lines like "Assets/Scripts/UnusedScript.cs,011111111..."
            foreach (var script in unused)
            {
                writer.WriteLine($"{script.RelativePath},{script.Guid}");
            }
        }
    }
}
