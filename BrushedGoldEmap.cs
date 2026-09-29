using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using Rhino;
using Rhino.Geometry;
using Rhino.Display;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

/*
  Brushed Gold Emap
  -----------------
  Author: Rajeev Pulari
  Version: 2026.04.28
  Display-only surface analysis: shades geometry with Rhino 8's native
  "brushed_gold.jpg" environment map to read reflections/curvature.
*/
public class Script_Instance : GH_ScriptInstance
{
    // --- Viewport override state ---
    private Mesh previewMesh;
    private DisplayMaterial goldEmapMaterial;
    private BoundingBox bbox;

    private void RunScript(List<GeometryBase> Geometry)
    {
        // 1. Metadata + pin tooltip (once)
        if (this.Component != null && this.Component.Name != "Brushed Gold Emap")
        {
            this.Component.Name = "Brushed Gold Emap";
            this.Component.NickName = "GoldEmap";
            this.Component.Message = "Brushed Gold";
            this.Component.Description = "Shades Breps/Meshes with Rhino's brushed-gold environment map to read reflections and curvature. Display only.";

            SetTip(this.Component.Params.Input, 0, "Geometry",
                "Breps or Meshes to shade with the brushed-gold reflection map.");
        }

        // 2. State reset
        previewMesh = new Mesh();
        goldEmapMaterial = null;
        bbox = BoundingBox.Empty;

        // 3. Input validation (warnings, not errors)
        if (Geometry == null || Geometry.Count == 0)
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Geometry list is empty. Please connect Breps or Meshes.");
            return;
        }

        // 4. Resolve the native environment map path
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string emapPath = Path.Combine(appDataPath, @"McNeel\Rhinoceros\8.0\Localization\en-US\Environment Maps\brushed_gold.jpg");

        // Fallback: the "en-US" folder only exists on English installs. Search
        // every localization folder for the file so non-English Rhino works too.
        if (!File.Exists(emapPath))
        {
            string locRoot = Path.Combine(appDataPath, @"McNeel\Rhinoceros\8.0\Localization");
            if (Directory.Exists(locRoot))
            {
                try
                {
                    string[] hits = Directory.GetFiles(locRoot, "brushed_gold.jpg", SearchOption.AllDirectories);
                    if (hits.Length > 0) emapPath = hits[0];
                }
                catch { /* ignore search errors, handled by the check below */ }
            }
        }

        if (!File.Exists(emapPath))
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Native brushed_gold.jpg not found in the Rhino 8 AppData Environment Maps folder.");
            return;
        }

        // 5. Extract high-quality render mesh
        MeshingParameters mp = new MeshingParameters();
        mp.GridAngle = 0.05;
        mp.RelativeTolerance = 0.001;
        mp.MinimumEdgeLength = 0.001;

        foreach (GeometryBase geo in Geometry)
        {
            if (geo == null) continue;

            if (geo is Brep)
            {
                Mesh[] meshes = Mesh.CreateFromBrep(geo as Brep, mp);
                if (meshes != null)
                    foreach (Mesh m in meshes) previewMesh.Append(m);
            }
            else if (geo is Mesh)
            {
                previewMesh.Append(((Mesh)geo).DuplicateMesh());
            }
            else
            {
                this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Unsupported geometry type bypassed. Only Breps and Meshes are processed.");
            }
        }

        if (previewMesh.Vertices.Count == 0) return;

        // 6. Weld & smooth normals
        previewMesh.Weld(Math.PI);
        previewMesh.Normals.ComputeNormals();
        previewMesh.Compact();
        bbox = previewMesh.GetBoundingBox(true);

        // 7. Build the display material from the native map
        goldEmapMaterial = new DisplayMaterial();
        goldEmapMaterial.Diffuse = Color.White;
        goldEmapMaterial.SetEnvironmentTexture(emapPath, true);
    }

    // =========================================================
    // Override the native Grasshopper preview
    // =========================================================

    public override BoundingBox ClippingBox
    {
        get { return bbox; }
    }

    public override void DrawViewportMeshes(IGH_PreviewArgs args)
    {
        if (previewMesh != null && previewMesh.Vertices.Count > 0 && goldEmapMaterial != null)
            args.Display.DrawMeshShaded(previewMesh, goldEmapMaterial);
    }

    public override void DrawViewportWires(IGH_PreviewArgs args)
    {
        // Intentionally blank: suppress wireframes for a clean reflection read.
    }

    private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].Name = name;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}
