using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UnityProjectAnalyzerBonus
{
    // YAML scene analysis: find which script GUIDs are actually used
    //          (via m_Script and via live serialized fields)
    internal static class UsageAnalyzer
    {
        public static HashSet<string> FindUsedGuidsWithRoslyn(
            string projectRoot,
            Dictionary<string, HashSet<string>> scriptFieldsByGuid)
        {
            var usedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Iterate through all .unity files in the project
            foreach (var scenePath in Directory.EnumerateFiles(projectRoot, "*.unity", SearchOption.AllDirectories))
            {
                AnalyzeScene(scenePath, scriptFieldsByGuid, usedGuids);
            }

            return usedGuids;
        }

        // Analyze a single scene
        private static void AnalyzeScene(
            string scenePath,
            Dictionary<string, HashSet<string>> scriptFieldsByGuid,
            HashSet<string> usedGuids)
        {
            string[] lines = File.ReadAllLines(scenePath);

            // 1) First pass. Build a map: MonoBehaviour fileID -> script GUID
            //                         (via m_Script field inside the MonoBehaviour block)
            var monoFileIdToGuid = new Dictionary<long, string>();
            long? currentFileId = null;
            bool inMono = false;

            foreach (var raw in lines)
            {
                // MonoBehaviour block header: --- !u!114 &136406839
                var header = Regex.Match(raw, @"^---\s*!u!114\s*&(\d+)");
                if (header.Success)
                {
                    currentFileId = long.Parse(header.Groups[1].Value);
                    inMono = true;
                    continue;
                }

                // New block of another type started, so we exit MonoBehaviour
                if (raw.StartsWith("--- !u!") && !header.Success)
                {
                    inMono = false;
                    currentFileId = null;
                }

                if (!inMono)
                    continue;

                string line = raw.Trim();
                if (line.StartsWith("m_Script:"))
                {
                    // Line like: m_Script: {fileID: 11500000, guid: fd9b..., type: 3}
                    var m = Regex.Match(line, @"guid:\s*([0-9a-fA-F]{32})");
                    if (m.Success && currentFileId.HasValue)
                    {
                        string guid = m.Groups[1].Value;

                        // Remember which MonoBehaviour (fileID) is bound to which script (GUID)
                        monoFileIdToGuid[currentFileId.Value] = guid;

                        // The fact that this component exists on an object already makes the script "used"
                        usedGuids.Add(guid);
                    }
                }
            }

            // 2) Second pass. Now look at valid serialized fields inside MonoBehaviour.
            // We only care about fields that store references to other MonoBehaviours (by fileID).
            currentFileId = null;
            inMono = false;
            string? currentScriptGuid = null;
            HashSet<string>? validFields = null;

            foreach (var raw in lines)
            {
                var header = Regex.Match(raw, @"^---\s*!u!114\s*&(\d+)");
                if (header.Success)
                {
                    // Start of a MonoBehaviour block
                    currentFileId = long.Parse(header.Groups[1].Value);
                    inMono = true;

                    // Script GUID for the current MonoBehaviour (if there is one)
                    currentScriptGuid = monoFileIdToGuid.TryGetValue(currentFileId.Value, out var guid)
                        ? guid
                        : null;

                    // Set of serialized field names for this script (from Roslyn)
                    validFields = null;
                    if (currentScriptGuid != null && scriptFieldsByGuid.TryGetValue(currentScriptGuid, out var fields))
                    {
                        validFields = fields;
                    }

                    continue;
                }

                // Another block started - exit MonoBehaviour
                if (raw.StartsWith("--- !u!") && !header.Success)
                {
                    inMono = false;
                    currentFileId = null;
                    currentScriptGuid = null;
                    validFields = null;
                    continue;
                }

                // Either not in MonoBehaviour now, or we don't know its fields - skip
                if (!inMono || currentScriptGuid == null || validFields == null)
                    continue;

                string line = raw.Trim();

                // Potential field line: "FieldName: value"
                var fm = Regex.Match(line, @"^([A-Za-z_][A-Za-z0-9_]*)\s*:\s*(.*)$");
                if (!fm.Success)
                    continue;

                string fieldName = fm.Groups[1].Value;

                // If there is no such field in C# (by Roslyn), treat it as old "garbage" in YAML and ignore it.
                if (!validFields.Contains(fieldName))
                    continue;

                // Field exists in C#. Now check whether it references another component.
                string valuePart = fm.Groups[2].Value;

                // Look for a fragment like {fileID: 123456} inside the value
                var refMatch = Regex.Match(valuePart, @"fileID:\s*(-?\d+)");
                if (!refMatch.Success)
                    continue;

                long targetFileId = long.Parse(refMatch.Groups[1].Value);

                // If this is a reference to another MonoBehaviour we know the GUID for, mark that GUID as "used" too.
                if (monoFileIdToGuid.TryGetValue(targetFileId, out var targetGuid))
                {
                    usedGuids.Add(targetGuid);
                }
            }
        }
    }
}
