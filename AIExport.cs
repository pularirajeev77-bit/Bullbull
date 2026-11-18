#region Visual Meta Data
// Component Name : AI Export (Tree)
// Nickname       : AI Export
// Version        : 1.4.1
// Date           : 2026-11-18
// Category       : Export / Illustrator
// Target Platform: Rhino 8 (C# Script Editor / Roslyn engine), Grasshopper 1
// Description    : Exports a geometry tree to Adobe Illustrator (.ai), ONE FILE PER BRANCH, at 1:1 in model
//                  units, preserving Rhino object/layer colors unless overridden.
// Inputs:
//   Geometries   (Tree)  Geometry and annotations. Each branch becomes one .ai file.
//   Colors       (List)  Optional colors per object inside a branch (overrides Rhino colors).
//   FilePath     (Item)  Output folder. If a file path is given, its folder is used.
//   FileNames    (List)  One name per branch, in branch order; ".ai" is added automatically.
//                        Missing names -> first name (or "Export") + branch path, e.g. Plan_0_1.ai.
//   Export       (Item)  Button: the export runs once when the button is RELEASED.
//   MoveToOrigin (Item)  True moves each file's bounding-box lower-left corner to world 0,0.
// Outputs:
//   Success      One true/false per branch.
//   Log          Summary plus one line per branch.
// Color priority : 1. 'Colors' input -> 2. Rhino object color -> 3. Rhino layer color -> 4. Black
// Changelog:
//   1.4.1  Pin tooltips on every input/output (set by name). The active view is restored after the
//          export (it used to stay on Top). The temporary bake + delete of all branches is grouped into
//          one undo record instead of one step per object. Author: Rajeev Pulari.
//   1.4.0  The component now sets its own Name, NickName, Description and Message (version tag under it).
//   1.3.0  Fixed the button freezing (needing a second click): the export no longer runs inside the
//          Grasshopper solution while the button is held. Press arms it, release queues it, and it runs
//          on Rhino's Idle event once the solution is finished; the component then refreshes its outputs.
//   1.2.0  Geometries as a tree + FileNames as a list: one file per branch, names paired in order.
//   1.1.0  Added FileNames input (extension added automatically, invalid characters replaced).
//          Fixed AI export options (PreserveUnits=Yes) so 0,0 and 1:1 scale are kept.
//   1.0.0  First version.
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
    // Component display info (canvas label, tooltip and the message under the component)
    private const string ComponentName = "AI Export (Tree)";
    private const string ComponentNickName = "AI Export";
    private const string ComponentDescription = "Exports a geometry tree to Adobe Illustrator (.ai), one file per branch, at 1:1 in model units, keeping Rhino object/layer colors unless overridden.";
    private const string ComponentMessage = "v1.4.1";

    // Button state between solutions: pressed -> armed, released -> queued for Idle
    private bool _armed;
    private bool _queued;
    private object _lastSuccess = false;
    private string _lastLog;

    // Inputs captured at release, used by the Idle export
    private DataTree<GeometryBase> _qGeometries;
    private List<Color> _qColors;
    private string _qFilePath;
    private List<object> _qFileNames;
    private bool _qMoveToOrigin;
    private Dictionary<string, List<Guid>> _qReferenceIds;

    private void RunScript(
		DataTree<GeometryBase> Geometries,
		List<Color> Colors,
		string FilePath,
		List<object> FileNames,
		bool Export,
		bool MoveToOrigin,
		ref object Success,
		ref object Log)
    {
        ApplyComponentInfo();

        Success = _lastSuccess;

        if (_queued)
        {
            Log = "Exporting...";
            return;
        }

        // Button held down: only arm. Never export here, or the button cannot see its release.
        if (Export)
        {
            _armed = true;
            Log = "Release the button to export.";
            return;
        }

        // Button released: capture the inputs and run the export once Grasshopper is idle
        if (_armed)
        {
            _armed = false;
            _qGeometries = Geometries;
            _qColors = Colors;
            _qFilePath = FilePath;
            _qFileNames = FileNames;
            _qMoveToOrigin = MoveToOrigin;
            // Grasshopper passes copies of referenced geometry, so read source Rhino object IDs from the input goo now
            _qReferenceIds = GetInputReferenceIds("Geometries");
            _queued = true;
            RhinoApp.Idle += OnIdleExport;
            Log = "Exporting...";
            return;
        }

        Log = _lastLog ?? "Ready. Press the button to export.";
    }

    // Sets the component's Name, NickName, Description and Message; only writes what changed
    private void ApplyComponentInfo()
    {
        if (Component == null) return;

        if (Component.Name != ComponentName) Component.Name = ComponentName;
        if (Component.NickName != ComponentNickName) Component.NickName = ComponentNickName;
        if (Component.Description != ComponentDescription) Component.Description = ComponentDescription;
        Component.Message = ComponentMessage;
        SetPinTips();
    }

    // ---- pin tooltips (set once, matched by the pin Name = script variable) ----
    private bool _tipsSet;
    private void SetPinTips()
    {
        if (_tipsSet) return;
        _tipsSet = true;
        TipPin(Component.Params.Input, "Geometries", "Geometry and annotations (curves, Breps, meshes, hatches, text, dimensions...). Tree: each branch becomes one .ai file.");
        TipPin(Component.Params.Input, "Colors", "Optional colors per object inside a branch (item i -> color i, the last color repeats). Overrides the Rhino object/layer colors.");
        TipPin(Component.Params.Input, "FilePath", "Output folder. A file path is accepted and its folder used. Empty = folder of the saved Rhino file.");
        TipPin(Component.Params.Input, "FileNames", "One file name per branch, in branch order. '.ai' is added. Missing names = first name (or Export) + branch path, e.g. Plan_0_1.ai.");
        TipPin(Component.Params.Input, "Export", "Button: the export runs once when the button is RELEASED.");
        TipPin(Component.Params.Input, "MoveToOrigin", "True = each file's bounding-box lower-left corner is moved to world 0,0. False = world coordinates kept.");
        TipPin(Component.Params.Output, "Success", "One true/false per branch: was its .ai file written?");
        TipPin(Component.Params.Output, "Log", "Summary plus one line per branch (objects, color source, units, file name) or the reason it failed.");
    }

    // Match pins by Name, fall back to NickName. Only NickName and Description are changed.
    private void TipPin(IList<IGH_Param> ps, string name, string tip)
    {
        if (ps == null) return;
        IGH_Param hit = ps.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                     ?? ps.FirstOrDefault(p => string.Equals(p.NickName, name, StringComparison.OrdinalIgnoreCase));
        if (hit == null) return;
        hit.NickName = name;
        hit.Description = tip;
    }

    private void OnIdleExport(object sender, EventArgs e)
    {
        RhinoApp.Idle -= OnIdleExport;
        _queued = false;

        try
        {
            object success;
            string log;
            DoExport(_qGeometries, _qColors, _qFilePath, _qFileNames, _qMoveToOrigin, _qReferenceIds, out success, out log);
            _lastSuccess = success;
            _lastLog = log;
        }
        catch (Exception ex)
        {
            _lastSuccess = false;
            _lastLog = $"Export Error: {ex.Message}";
        }
        finally
        {
            _qGeometries = null;
            _qColors = null;
            _qFileNames = null;
            _qReferenceIds = null;
        }

        // Show Success and Log
        if (Component != null && Component.OnPingDocument() != null)
        {
            Component.ExpireSolution(true);
        }
    }

    private void DoExport(
        DataTree<GeometryBase> Geometries,
        List<Color> Colors,
        string FilePath,
        List<object> FileNames,
        bool MoveToOrigin,
        Dictionary<string, List<Guid>> referenceIds,
        out object Success,
        out string Log)
    {
        Success = false;

        if (Geometries == null || Geometries.DataCount == 0)
        {
            Log = "No geometry provided.";
            return;
        }

        RhinoDoc doc = RhinoDoc.ActiveDoc;
        if (doc == null)
        {
            Log = "Active Rhino document not found.";
            return;
        }

        string folder;
        string folderError;
        if (!ResolveFolder(doc, FilePath, out folder, out folderError))
        {
            Log = folderError;
            return;
        }

        List<string> names = (FileNames ?? new List<object>())
            .Select(n => n == null ? "" : Convert.ToString(n))
            .ToList();

        // Switch to Top View for orthographic 2D AI export (the previous view is restored at the end)
        Rhino.Display.RhinoView previousView = doc.Views.ActiveView;
        Rhino.Display.RhinoView topView = doc.Views.Find("Top", false);
        if (topView != null)
        {
            doc.Views.ActiveView = topView;
        }


        if (referenceIds == null) referenceIds = new Dictionary<string, List<Guid>>();

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var successList = new List<bool>();
        var log = new StringBuilder();
        int okCount = 0;

        if (names.Count > 0 && names.Count != Geometries.BranchCount)
        {
            log.AppendLine($"Note: {names.Count} name(s) for {Geometries.BranchCount} branch(es).");
        }

        // The temporary bake + delete of all branches is grouped into one undo record
        uint undo = doc.BeginUndoRecord("AI Export (temporary objects)");
        try
        {
            for (int b = 0; b < Geometries.BranchCount; b++)
            {
                GH_Path path = Geometries.Paths[b];
                List<GeometryBase> branch = Geometries.Branch(b);

                string fullPath = BuildFilePath(folder, names, b, Geometries.BranchCount, path, usedNames);
                List<Guid> branchRefs;
                referenceIds.TryGetValue(path.ToString(), out branchRefs);

                string message;
                bool ok = ExportBranch(doc, branch, Colors, branchRefs, fullPath, MoveToOrigin, out message);

                successList.Add(ok);
                if (ok) okCount++;
                log.AppendLine($"{path} -> {message}");
            }
        }
        finally
        {
            if (undo != 0) doc.EndUndoRecord(undo);
            if (previousView != null && previousView != topView)
            {
                try { doc.Views.ActiveView = previousView; } catch { }
            }
        }

        doc.Views.Redraw();

        Success = successList;
        Log = $"Exported {okCount}/{Geometries.BranchCount} file(s) to: {folder}{System.Environment.NewLine}{log}";
    }

    // Bakes one branch, exports it to its own .ai file, then removes the baked objects
    private bool ExportBranch(
        RhinoDoc doc,
        List<GeometryBase> geoms,
        List<Color> colors,
        List<Guid> refIds,
        string fullPath,
        bool moveToOrigin,
        out string message)
    {
        message = "";

        BoundingBox bbox = BoundingBox.Empty;
        int validCount = 0;
        if (geoms != null)
        {
            foreach (var g in geoms)
            {
                if (g != null && g.IsValid)
                {
                    bbox.Union(g.GetBoundingBox(true));
                    validCount++;
                }
            }
        }

        if (validCount == 0 || !bbox.IsValid)
        {
            message = "Skipped: no valid geometry in branch.";
            return false;
        }

        // Translation to World Origin (0,0,0), per file
        Transform moveXform = Transform.Identity;
        if (moveToOrigin)
        {
            Point3d sourceRef = new Point3d(bbox.Min.X, bbox.Min.Y, 0.0);
            moveXform = Transform.Translation(Point3d.Origin - sourceRef);
        }

        bool useRefs = refIds != null && refIds.Count == geoms.Count;
        bool useColors = colors != null && colors.Count > 0;

        doc.Objects.UnselectAll();
        var bakedIds = new List<Guid>();
        int colorsFromRhino = 0;

        try
        {
            for (int i = 0; i < geoms.Count; i++)
            {
                var geom = geoms[i];
                if (geom == null || !geom.IsValid) continue;

                // Determine final color
                Color resolvedColor;
                if (useColors)
                {
                    // Priority 1: User explicitly specified colors in the input
                    resolvedColor = colors[Math.Min(i, colors.Count - 1)];
                }
                else
                {
                    // Priority 2: Query the active Rhino doc for the source object or layer color
                    bool found;
                    resolvedColor = ResolveRhinoColor(doc, geom, useRefs ? refIds[i] : Guid.Empty, out found);
                    if (found) colorsFromRhino++;
                }

                ObjectAttributes attr = new ObjectAttributes
                {
                    ObjectColor = resolvedColor,
                    ColorSource = ObjectColorSource.ColorFromObject,
                    PlotColor = resolvedColor,
                    PlotColorSource = ObjectPlotColorSource.PlotColorFromObject
                };

                GeometryBase geomCopy = geom.Duplicate();
                if (moveToOrigin)
                {
                    geomCopy.Transform(moveXform);
                }

                Guid id = AddToDocument(doc, geomCopy, attr);
                if (id != Guid.Empty)
                {
                    doc.Objects.Select(id, true);
                    bakedIds.Add(id);
                }
            }

            if (bakedIds.Count == 0)
            {
                message = "Failed to add objects to document.";
                return false;
            }

            // Overwrite any previous export with the same name
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            // Run scripted AI export preserving scale (1:1) and world coordinates.
            // PreserveUnits=Yes is the real AI-export option: without it Rhino maps the on-screen viewport
            // to the page, so positions depend on zoom/pan and 0,0 is lost.
            string cmd = $"-_Export \"{fullPath}\" _PreserveUnits=_Yes _AIScale=1 _RhinoScale=1 _ViewportBoundary=_No _Enter";
            RhinoApp.RunScript(cmd, false);

            if (!File.Exists(fullPath))
            {
                message = "Rhino export command failed to write the file.";
                return false;
            }

            string colorInfo = useColors
                ? "input colors"
                : $"Rhino colors {colorsFromRhino}/{bakedIds.Count}";
            message = $"OK: {bakedIds.Count} object(s), {colorInfo}, {doc.ModelUnitSystem} 1:1 -> {Path.GetFileName(fullPath)}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Export Error: {ex.Message}";
            return false;
        }
        finally
        {
            // Immediate cleanup
            foreach (Guid id in bakedIds)
            {
                doc.Objects.Delete(id, true);
            }
        }
    }

    private Guid AddToDocument(RhinoDoc doc, GeometryBase geom, ObjectAttributes attr)
    {
        switch (geom)
        {
            case TextEntity textEntity:
                return doc.Objects.AddText(textEntity, attr);
            case Leader leader:
                return doc.Objects.AddLeader(leader, attr);
            case Curve crv:
                return doc.Objects.AddCurve(crv, attr);
            case Brep brep:
                return doc.Objects.AddBrep(brep, attr);
            case Mesh mesh:
                return doc.Objects.AddMesh(mesh, attr);
            case SubD subD:
                return doc.Objects.AddSubD(subD, attr);
            case Rhino.Geometry.Point pt:
                return doc.Objects.AddPoint(pt.Location, attr);
            case Hatch hatch:
                return doc.Objects.AddHatch(hatch, attr);
            default:
                // Covers all Dimensions and Centermarks cleanly
                return doc.Objects.Add(geom, attr);
        }
    }

    // FilePath is the folder; a file path is accepted and its folder used. Empty -> folder of the saved Rhino file.
    private bool ResolveFolder(RhinoDoc doc, string filePath, out string folder, out string error)
    {
        error = null;
        folder = (filePath ?? "").Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = !string.IsNullOrEmpty(doc.Path) ? Path.GetDirectoryName(doc.Path) : null;
            if (string.IsNullOrWhiteSpace(folder))
            {
                error = "FilePath (folder) is empty and the Rhino file is not saved yet.";
                return false;
            }
        }
        else if (Path.HasExtension(folder))
        {
            folder = Path.GetDirectoryName(folder);
        }

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            error = $"Cannot access or create folder: {ex.Message}";
            return false;
        }
        return true;
    }

    // File naming:
    //  - a name for this branch index -> that name
    //  - otherwise first name (or "Export") + branch path suffix, e.g. Plan_0_2.ai (single branch -> name as-is)
    //  - ".ai" always added; invalid characters replaced; duplicates get _1, _2...
    private string BuildFilePath(string folder, List<string> names, int branchIndex, int branchCount, GH_Path path, HashSet<string> used)
    {
        string baseName;

        if (branchIndex < names.Count && !string.IsNullOrWhiteSpace(names[branchIndex]))
        {
            baseName = StripAiExtension(names[branchIndex].Trim().Trim('"'));
        }
        else
        {
            string root = (names.Count > 0 && !string.IsNullOrWhiteSpace(names[0])) ? StripAiExtension(names[0].Trim().Trim('"')) : "Export";
            baseName = branchCount == 1 ? root : $"{root}_{string.Join("_", path.Indices)}";
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(c, '_');
        }
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Export";

        // Avoid two branches writing the same file
        string candidate = baseName;
        int k = 1;
        while (used.Contains(candidate))
        {
            candidate = $"{baseName}_{k++}";
        }
        used.Add(candidate);

        return Path.Combine(folder, candidate + ".ai");
    }

    private string StripAiExtension(string name)
    {
        return name.EndsWith(".ai", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 3) : name;
    }

    // Reads the Rhino object reference IDs carried by the input parameter's goo, keyed by branch path
    private Dictionary<string, List<Guid>> GetInputReferenceIds(string inputName)
    {
        var map = new Dictionary<string, List<Guid>>();
        try
        {
            if (Component == null) return map;

            IGH_Param param = Component.Params.Input.FirstOrDefault(p => p.NickName == inputName || p.Name == inputName);
            if (param == null) return map;

            IGH_Structure data = param.VolatileData;
            if (data == null) return map;

            foreach (GH_Path p in data.Paths)
            {
                var ids = new List<Guid>();
                System.Collections.IList branch = data.get_Branch(p);
                if (branch != null)
                {
                    foreach (object item in branch)
                    {
                        ids.Add(ExtractReferenceId(item));
                    }
                }
                map[p.ToString()] = ids;
            }
        }
        catch
        {
            map.Clear();
        }
        return map;
    }

    private Guid ExtractReferenceId(object item)
    {
        if (item is IGH_GeometricGoo geoGoo && geoGoo.IsReferencedGeometry)
        {
            return geoGoo.ReferenceID;
        }

        if (item is GH_ObjectWrapper wrapper && wrapper.Value is IGH_GeometricGoo innerGoo && innerGoo.IsReferencedGeometry)
        {
            return innerGoo.ReferenceID;
        }

        return Guid.Empty;
    }

    private Color ResolveRhinoColor(RhinoDoc doc, GeometryBase geom, Guid referenceId, out bool found)
    {
        found = false;

        // 1. Reference ID from the Grasshopper input (most reliable)
        if (referenceId != Guid.Empty)
        {
            RhinoObject obj = doc.Objects.FindId(referenceId);
            if (obj != null)
            {
                found = true;
                return GetEffectiveColor(doc, obj);
            }
        }

        // 2. Explicit GUID stored on the geometry
        if (geom.UserDictionary.ContainsKey("RhinoObjectGuid"))
        {
            if (Guid.TryParse(geom.UserDictionary.GetString("RhinoObjectGuid"), out Guid objId))
            {
                RhinoObject obj = doc.Objects.FindId(objId);
                if (obj != null)
                {
                    found = true;
                    return GetEffectiveColor(doc, obj);
                }
            }
        }

        // 3. Geometric match against document objects (for un-referenced copies of Rhino geometry)
        BoundingBox geomBox = geom.GetBoundingBox(true);
        double tol = Math.Max(doc.ModelAbsoluteTolerance, 1e-6);

        var settings = new ObjectEnumeratorSettings
        {
            NormalObjects = true,
            LockedObjects = true,
            HiddenObjects = false,
            DeletedObjects = false,
            IncludeLights = false,
            IncludeGrips = false
        };

        foreach (RhinoObject obj in doc.Objects.GetObjectList(settings))
        {
            GeometryBase docGeom = obj.Geometry;
            if (docGeom == null || docGeom.ObjectType != geom.ObjectType) continue;

            BoundingBox docBox = docGeom.GetBoundingBox(true);
            if (docBox.Min.DistanceTo(geomBox.Min) > tol || docBox.Max.DistanceTo(geomBox.Max) > tol) continue;

            if (object.ReferenceEquals(docGeom, geom) || GeometryBase.GeometryEquals(docGeom, geom))
            {
                found = true;
                return GetEffectiveColor(doc, obj);
            }
        }

        // Default fallback if generated entirely parametrically without source
        return Color.Black;
    }

    private Color GetEffectiveColor(RhinoDoc doc, RhinoObject obj)
    {
        // DrawColor resolves object, layer, material and parent (block) color sources
        Color c = obj.Attributes.DrawColor(doc);
        return c.IsEmpty ? Color.Black : c;
    }
}