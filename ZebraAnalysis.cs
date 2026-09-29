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
  Zebra Analysis
  --------------
  Author: Rajeev Pulari
  Version: 2026.04.28
  Native zebra surface-analysis display: shades the geometry with a striped
  environment map to reveal curvature and continuity. Display only - no
  Grasshopper outputs - and it suppresses all wires/edges for clarity.
*/
public class Script_Instance : GH_ScriptInstance
{
    // --- Viewport override state ---
    private Mesh previewMesh;
    private DisplayMaterial zebraMaterial;
    private BoundingBox bbox;

    private void RunScript(
		List<GeometryBase> Geometry,
		bool Horizontal,
		int StripeCount)
    {
        // 1. Metadata + pin tooltips (once)
        if (this.Component != null && this.Component.Name != "Zebra Analysis")
        {
            this.Component.Name = "Zebra Analysis";
            this.Component.NickName = "Zebra";
            this.Component.Message = "Zebra Map";
            this.Component.Description = "Shades Breps/Meshes with a striped zebra environment map to check surface curvature and continuity. Display only.";

            SetTip(this.Component.Params.Input, 0, "Geometry",
                "Breps or Meshes to analyze.");
            SetTip(this.Component.Params.Input, 1, "Horizontal",
                "True = horizontal stripes, False = vertical stripes.");
            SetTip(this.Component.Params.Input, 2, "StripeCount",
                "Number of stripe pairs. More stripes = finer analysis. 0 or less uses 20.");
        }

        // 2. State reset (prevents viewport ghosting)
        previewMesh = new Mesh();
        zebraMaterial = null;
        bbox = BoundingBox.Empty;

        // 3. Input validation (warnings, not errors)
        if (Geometry == null || Geometry.Count == 0)
        {
            this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Geometry list is empty. Please connect Breps or Meshes.");
            return;
        }

        if (StripeCount <= 0) StripeCount = 20;

        // 4. Extract high-quality render mesh for optical analysis
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

        // 5. Weld & smooth normals
        previewMesh.Weld(Math.PI);
        previewMesh.Normals.ComputeNormals();
        previewMesh.Compact();
        bbox = previewMesh.GetBoundingBox(true);

        // 6. Generate the zebra environment texture
        // Unique file name per component so two Zebra components don't
        // overwrite each other's texture (the original shared one fixed path,
        // "GH_ZebraMap.jpg", for every instance).
        string tag = (this.Component != null) ? this.Component.InstanceGuid.ToString("N") : "default";
        string tempPath = Path.Combine(Path.GetTempPath(), "GH_ZebraMap_" + tag + ".jpg");

        using (Bitmap bmp = new Bitmap(1024, 1024))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                int stripeWidth = Math.Max(1, 1024 / (StripeCount * 2));

                for (int i = 0; i < 1024; i += stripeWidth * 2)
                {
                    if (Horizontal) g.FillRectangle(Brushes.Black, 0, i, 1024, stripeWidth);
                    else            g.FillRectangle(Brushes.Black, i, 0, stripeWidth, 1024);
                }
            }
            bmp.Save(tempPath, System.Drawing.Imaging.ImageFormat.Jpeg);
        }

        // 7. Build the display material
        zebraMaterial = new DisplayMaterial();
        zebraMaterial.Diffuse = Color.White;
        zebraMaterial.SetEnvironmentTexture(tempPath, true);
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
        if (previewMesh != null && previewMesh.Vertices.Count > 0 && zebraMaterial != null)
            args.Display.DrawMeshShaded(previewMesh, zebraMaterial);
    }

    public override void DrawViewportWires(IGH_PreviewArgs args)
    {
        // Intentionally blank: no wires/edges drawn, for a clean zebra read.
    }

    private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].Name = name;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}
