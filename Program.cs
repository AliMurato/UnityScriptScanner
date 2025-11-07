using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityProjectAnalyzerBonus
{
    internal class Program
    {
        // Entry point of the console application.
        // Expects 2 arguments:
        //  0: path to the root of the Unity project
        //  1: path to the folder where results will be written
        static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: tool.exe <unity_project_path> <output_folder_path>");
                return 1; // error: invalid arguments
            }

            string projectRoot = Path.GetFullPath(args[0]);
            string outputRoot = Path.GetFullPath(args[1]);

            if (!Directory.Exists(projectRoot))
            {
                Console.Error.WriteLine($"Project folder does not exist: {projectRoot}");
                return 1; // error: project folder does not exist
            }

            // Create the output folder (if it does not exist)
            Directory.CreateDirectory(outputRoot);

            // 1) Collect all scripts: .cs + their GUIDs from .meta
            List<ScriptInfo> scripts = ScriptScanner.FindScripts(projectRoot);

            // 2) Roslyn: for each GUID collect names of serializable fields
            Dictionary<string, HashSet<string>> scriptFieldsByGuid = ScriptFieldAnalyzer.AnalyzeScripts(projectRoot);

            // 3) YAML + Roslyn: which script GUIDs are actually used in scenes
            HashSet<string> usedGuids = UsageAnalyzer.FindUsedGuidsWithRoslyn(projectRoot, scriptFieldsByGuid);

            // 4) Write UnusedScripts.csv based on all scripts and the usedGuids set
            CsvWriter.WriteUnusedScripts(outputRoot, scripts, usedGuids);

            // 5) Scene dumps *.unity.dump (GameObject hierarchy)
            SceneDumper.DumpAllScenes(projectRoot, outputRoot);

            return 0; // successful exit
        }
    }
}
