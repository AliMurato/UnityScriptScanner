using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityScriptScanner
{
    // Build a scene GameObject hierarchy from Unity YAML
    internal static class SceneHierarchyBuilder
    {
        // Information about a Transform:
        //  Id           — fileID of the Transform itself
        //  GameObjectId — fileID of the attached GameObject
        //  ChildrenIds  — list of child Transform fileIDs, in the same order as m_Children
        private class TransformInfo
        {
            public long Id;
            public long GameObjectId;
            public List<long> ChildrenIds { get; } = new();
        }

        // Builds a list of root GameObjectNode objects from the scene YAML text
        public static List<GameObjectNode> BuildHierarchy(string yamlText)
        {
            var goNames = new Dictionary<long, string>();        // GameObject id - name
            var goBlocks = new Dictionary<long, string>();        // GameObject id - block text
            var trBlocks = new Dictionary<long, string>();        // Transform id - block text
            var transforms = new Dictionary<long, TransformInfo>(); // Transform id - info
            string? sceneRootsBlock = null;                         // SceneRoots block (if present)

            // 1) Split YAML into blocks by headers "--- !u!X &id"
            using (var reader = new StringReader(yamlText))
            {
                string? line;
                StringBuilder? currentBlock = null;
                int? currentType = null;
                long? currentId = null;

                while ((line = reader.ReadLine()) != null)
                {
                    var header = Regex.Match(line, @"^---\s*!u!(\d+)\s*&(\d+)");
                    if (header.Success)
                    {
                        // Save the previous block if there was one
                        if (currentBlock != null && currentId != null && currentType != null)
                        {
                            string text = currentBlock.ToString();
                            if (currentType == 1)
                                goBlocks[currentId.Value] = text;
                            else if (currentType == 4)
                                trBlocks[currentId.Value] = text;
                            else if (currentType == 1660057539)
                                sceneRootsBlock = text;
                        }

                        // Start a new block
                        currentType = int.Parse(header.Groups[1].Value);
                        currentId = long.Parse(header.Groups[2].Value);
                        currentBlock = new StringBuilder();
                        currentBlock.AppendLine(line);
                    }
                    else if (currentBlock != null)
                    {
                        // Continue the current block
                        currentBlock.AppendLine(line);
                    }
                }

                // Save the last block
                if (currentBlock != null && currentId != null && currentType != null)
                {
                    string text = currentBlock.ToString();
                    if (currentType == 1)
                        goBlocks[currentId.Value] = text;
                    else if (currentType == 4)
                        trBlocks[currentId.Value] = text;
                    else if (currentType == 1660057539)
                        sceneRootsBlock = text;
                }
            }

            // 2) Extract GameObject names (m_Name) from GameObject blocks
            foreach (var kv in goBlocks)
            {
                long goId = kv.Key;
                string block = kv.Value;

                using var r = new StringReader(block);
                string? line;
                while ((line = r.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("m_Name:"))
                    {
                        string name = trimmed.Substring("m_Name:".Length).Trim();
                        goNames[goId] = name;
                        break;
                    }
                }
            }

            // 3) From Transform blocks collect:
            //    - which GameObject they are attached to
            //    - list of children (m_Children)
            foreach (var kv in trBlocks)
            {
                long trId = kv.Key;
                string block = kv.Value;

                var info = new TransformInfo { Id = trId };

                using var r = new StringReader(block);
                string? line;
                bool inChildren = false;

                while ((line = r.ReadLine()) != null)
                {
                    string trimmed = line.Trim();

                    if (trimmed.StartsWith("m_GameObject:"))
                    {
                        // m_GameObject: {fileID: 136406834}
                        var m = Regex.Match(trimmed, @"fileID:\s*(-?\d+)");
                        if (m.Success)
                        {
                            info.GameObjectId = long.Parse(m.Groups[1].Value);
                        }
                    }
                    else if (trimmed.StartsWith("m_Children:"))
                    {
                        // Following lines will be "- {fileID: ...}"
                        inChildren = true;
                    }
                    else if (inChildren)
                    {
                        if (trimmed.StartsWith("- "))
                        {
                            // Line like "- {fileID: 2118425386}"
                            var m = Regex.Match(trimmed, @"fileID:\s*(-?\d+)");
                            if (m.Success)
                            {
                                long childTr = long.Parse(m.Groups[1].Value);
                                info.ChildrenIds.Add(childTr);
                            }
                        }
                        else
                        {
                            // We left the m_Children section
                            inChildren = false;
                        }
                    }
                }

                transforms[trId] = info;
            }

            // 4) Read SceneRoots.m_Roots, if present (order of roots in the scene).
            var rootTransformIds = new List<long>();

            if (sceneRootsBlock != null)
            {
                using var r = new StringReader(sceneRootsBlock);
                string? line;
                bool inRoots = false;

                while ((line = r.ReadLine()) != null)
                {
                    var trimmed = line.Trim();

                    if (trimmed.StartsWith("m_Roots:"))
                    {
                        inRoots = true;
                        continue;
                    }

                    if (inRoots)
                    {
                        if (!trimmed.StartsWith("-"))
                            break;

                        var m = Regex.Match(trimmed, @"fileID:\s*(-?\d+)");
                        if (m.Success)
                        {
                            long trId = long.Parse(m.Groups[1].Value);
                            rootTransformIds.Add(trId);
                        }
                    }
                }
            }

            // 5) Create GameObjectNode instances for all GameObjects
            var goNodes = new Dictionary<long, GameObjectNode>();
            foreach (var kv in goNames)
                goNodes[kv.Key] = new GameObjectNode { Name = kv.Value };

            var roots = new List<GameObjectNode>();
            var attached = new HashSet<long>(); // which GameObjects were already added to the tree (duplicate protection)

            // 6) Build the tree: add roots in the same order as they are listed in m_Roots
            foreach (long trId in rootTransformIds)
            {
                if (!transforms.TryGetValue(trId, out TransformInfo? tr))
                    continue;

                long goId = tr.GameObjectId;
                if (!goNodes.TryGetValue(goId, out GameObjectNode? node))
                    continue;

                if (!attached.Add(goId))
                    continue;

                roots.Add(node);
                AttachChildren(tr, node, transforms, goNodes, attached);
            }

            return roots;
        }

        // Recursive attachment of children to the tree
        private static void AttachChildren(
            TransformInfo parentTr,
            GameObjectNode parentNode,
            Dictionary<long, TransformInfo> transforms,
            Dictionary<long, GameObjectNode> goNodes,
            HashSet<long> attached)
        {
            // Children order is the same as in m_Children in YAML
            foreach (long childTrId in parentTr.ChildrenIds)
            {
                if (!transforms.TryGetValue(childTrId, out TransformInfo? childTr))
                    continue;

                long childGoId = childTr.GameObjectId;
                if (!goNodes.TryGetValue(childGoId, out GameObjectNode? childNode))
                    continue;

                if (!attached.Add(childGoId))
                    continue;

                parentNode.Children.Add(childNode);
                AttachChildren(childTr, childNode, transforms, goNodes, attached);
            }
        }
    }
}
