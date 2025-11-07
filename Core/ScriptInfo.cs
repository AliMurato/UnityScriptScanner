using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityProjectAnalyzerBonus
{
    // Small record to store script information:
    // Guid (GUID from .meta), RelativePath (relative path like Assets/....)
    internal record ScriptInfo(string Guid, string RelativePath);
}
