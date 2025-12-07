# Unity Script Scanner

Console tool that analyzes a **Unity project directory** and produces:

- `UnusedScripts.csv` – list of C# scripts that are not used in any scene  
- `*.unity.dump` – plain-text hierarchies of GameObjects for each Unity scene  

The tool parses Unity YAML files directly and uses **Roslyn** to analyze C# source code.  
No Unity API or Unity Editor is required.

---

## 1. Features

1. **Script scanning**
   - Finds all `.cs` files under the Unity project root
   - Reads corresponding `.meta` files
   - Extracts `guid:` values from `.meta` and stores:
     - `Guid`  
     - `RelativePath` (e.g. `Assets/Scripts/MyScript.cs`)

2. **Scene hierarchy dump**
   - Parses `.unity` YAML files
   - Uses `GameObject`, `Transform` and `SceneRoots` blocks
   - Reconstructs the GameObject hierarchy in the exact order used by Unity
   - Writes one dump file per scene:
     - `SampleScene.unity.dump`
     - `SecondScene.unity.dump`
   - Format:
     ```text
     ParentObject
     --Child1
     ----NestedChild
     --Child2
     ```

3. **Unused script detection (Bonus #1)**
   - Uses **Roslyn** (`Microsoft.CodeAnalysis`) to parse all C# files
   - For each script that derives from `MonoBehaviour`, collects names of **serializable fields**:
     - `public` fields (non `static`/`const`)
     - fields with attribute `[SerializeField]`
   - Parses `.unity` YAML to find:
     - direct `m_Script` references (MonoBehaviour component on a GameObject)
     - references stored in serialized fields of other MonoBehaviours
   - Any script whose GUID never appears in scenes (neither as `m_Script`, nor via a live serialized field) is considered **unused** and written to `UnusedScripts.csv`.

---

## 2. Project Structure

```
UnityScriptScanner/
│
├── Analysis/
│   ├── ScriptFieldAnalyzer.cs   # Roslyn: find serializable fields in MonoBehaviour classes
│   └── UsageAnalyzer.cs         # YAML + Roslyn: detect which script GUIDs are actually used
│
├── Core/
│   ├── CsvWriter.cs             # Writes UnusedScripts.csv
│   ├── ScriptInfo.cs            # Record with script GUID + relative path
│   └── ScriptScanner.cs         # Scans .cs files and reads GUIDs from .meta
│
├── Scenes/
│   ├── GameObjectNode.cs        # Simple tree node: Name + Children
│   ├── SceneDumper.cs           # Writes *.unity.dump files
│   └── SceneHierarchyBuilder.cs # Parses Unity YAML to build GameObject hierarchies
│
├── Program.cs                   # Entry point: orchestrates scanning, analysis and dumping
├── UnityScriptScanner.csproj
├── .gitignore
└── README.md
```

## 3. Requirements
- .NET SDK 8.0 (or compatible)
- Windows / Linux / macOS – any OS supported by .NET
- Unity project in YAML text serialization format
(standard for modern Unity versions; the sample test cases follow this format).

### NuGet dependencies

All NuGet dependencies are described in ```UnityScriptScanner.csproj:```

```Microsoft.CodeAnalysis```

```Microsoft.CodeAnalysis.CSharp```

They will be restored automatically by dotnet build or by Visual Studio.

### Optional dependency

For YAML parsing, you can also use existing libraries such as  
[`YamlDotNet`](https://github.com/aaubry/YamlDotNet) (available as a NuGet package).

## 4. Building the project

Option A: Using dotnet CLI

From the project root:

```dotnet restore```

```dotnet build```

After a successful build, the executable is located at:
```bin/Debug/net8.0/UnityScriptScanner.exe```
(or bin/Release/net8.0 if built in Release configuration).

Option B: Using Visual Studio
1. Open UnityScriptScanner.sln.
2. Select configuration Debug or Release.
3. Build → Build Solution.
4. The output .exe will be placed in bin/<Config>/net8.0/.

## 5. Usage

The tool is a console application and expects two arguments:

```UnityScriptScanner.exe <unity_project_path> <output_folder_path>```

- <unity_project_path> – path to the root of the Unity project
(directory that contains Assets/, ProjectSettings/, etc.)
- <output_folder_path> – folder where all output files will be written
(it will be created if it does not exist)

Example (Windows):

```./UnityScriptScanner.exe "C:\Test\Input" "C:\Test\Output"```

After running, the output folder will contain:
- UnusedScripts.csv
- SampleScene.unity.dump
- SecondScene.unity.dump
(and dumps for any other *.unity scenes in the project)
