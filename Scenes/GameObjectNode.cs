using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityScriptScanner
{
    // Node of the GameObject hierarchy (just name + children)
    internal class GameObjectNode
    {
        public string Name { get; set; } = "";
        public List<GameObjectNode> Children { get; } = new();
    }
}
