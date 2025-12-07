using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityScriptScanner
{
    // Writing *.unity.dump files
    internal static class SceneDumper
    {
        // For all scenes in the project, create a dump for each
        public static void DumpAllScenes(string projectRoot, string outputRoot)
        {
            foreach (string scenePath in Directory.EnumerateFiles(projectRoot, "*.unity", SearchOption.AllDirectories))
            {
                DumpSingleScene(scenePath, outputRoot);
            }
        }

        // Dump a single scene into "Scene.unity.dump"
        private static void DumpSingleScene(string scenePath, string outputRoot)
        {
            string sceneFileName = Path.GetFileName(scenePath);
            string dumpFileName = sceneFileName + ".dump";
            string dumpPath = Path.Combine(outputRoot, dumpFileName);

            string yamlText = File.ReadAllText(scenePath);
            List<GameObjectNode> roots = SceneHierarchyBuilder.BuildHierarchy(yamlText);

            using var writer = new StreamWriter(dumpPath, false, Encoding.UTF8);
            foreach (var root in roots)
                WriteNode(writer, root, 0);
        }

        // Recursive dump of GameObjectNode into the file:
        //  depth 0: "Parent"
        //  depth 1: "--Child"
        //  depth 2: "----ChildNested"
        private static void WriteNode(StreamWriter writer, GameObjectNode node, int depth)
        {
            string prefix = new string('-', depth * 2);
            writer.WriteLine(prefix + node.Name);

            foreach (GameObjectNode child in node.Children)
                WriteNode(writer, child, depth + 1);
        }
    }
}
