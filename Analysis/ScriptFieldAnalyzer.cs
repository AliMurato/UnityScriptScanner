using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityProjectAnalyzerBonus
{
    // C# code analysis using Roslyn: find serializable fields in MonoBehaviour classes
    internal static class ScriptFieldAnalyzer
    {
        // Result:
        //   key     = script GUID
        //   value   = set of serializable field names for this script
        public static Dictionary<string, HashSet<string>> AnalyzeScripts(string projectRoot)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            // Iterate through all .cs files in the project
            foreach (var csPath in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
            {
                string metaPath = csPath + ".meta";
                if (!File.Exists(metaPath))
                    continue;

                string? guid = ReadGuidFromMeta(metaPath);
                if (guid == null)
                    continue;

                // Collect serializable field names from the source code
                var fields = GetSerializedFieldNames(csPath);
                result[guid] = fields;
            }

            return result;
        }

        // Read GUID from .meta (same logic as in ScriptScanner)
        private static string? ReadGuidFromMeta(string metaPath)
        {
            foreach (var line in File.ReadLines(metaPath))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("guid:"))
                    return trimmed.Split(':', 2)[1].Trim();
            }
            return null;
        }

        // Extracts names of serializable fields from a C# file.
        // Serializable fields are considered to be:
        //  - public fields (non static/const)
        //  - or fields with the [SerializeField] attribute
        private static HashSet<string> GetSerializedFieldNames(string csPath)
        {
            var text = File.ReadAllText(csPath);

            // Parse the source into a Roslyn syntax tree
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();

            var result = new HashSet<string>();

            // Find class declarations
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                bool isMono = false;

                // Check if the class inherits from MonoBehaviour
                if (classDecl.BaseList != null)
                {
                    foreach (var b in classDecl.BaseList.Types)
                    {
                        if (b.Type.ToString() == "MonoBehaviour")
                        {
                            isMono = true;
                            break;
                        }
                    }
                }

                if (!isMono)
                    continue; // only MonoBehaviour classes are interesting

                // Iterate over fields in this class
                foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
                {
                    bool isPublic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
                    bool isStatic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) ||
                                    field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));

                    // Check for the [SerializeField] attribute
                    bool hasSerializeField = field.AttributeLists
                        .SelectMany(a => a.Attributes)
                        .Any(a => a.Name.ToString().Contains("SerializeField"));

                    // Serializable fields:
                    //  - public OR marked with [SerializeField]
                    //  - and not static/const
                    if ((!isPublic && !hasSerializeField) || isStatic)
                        continue;

                    // One FieldDeclaration can have multiple variables:
                    // e.g. public int a, b;
                    foreach (var v in field.Declaration.Variables)
                    {
                        result.Add(v.Identifier.Text);
                    }
                }
            }

            return result;
        }
    }
}
