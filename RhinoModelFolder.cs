/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2026.04.05
  Component: RhinoModelFolder

  Description:
    Retrieves:
      • Rh_path - the folder that contains the saved Rhino model (.3dm)
      • Gh_path - the full path of the saved Grasshopper file (.gh / .ghx)
    Works only when the files have been saved.
*/

using System;
using System.IO;
using Rhino;
using Grasshopper;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(bool Get, ref object Rh_path, ref object Gh_path)
    {
        // 1. Set Component Metadata + pin tooltips (once)
        if (this.Component != null && this.Component.Message != "Model Folder")
        {
            this.Component.Message = "Model Folder";
            this.Component.NickName = "ModFold";
            this.Component.Name = "Model Folder";
            this.Component.Description =
                "Gets the folder of the saved Rhino model (.3dm) and the full path of the saved Grasshopper file. Works only after the files are saved.";

            SetTip(this.Component.Params.Input, 0, "Get",
                "Set True to read the paths. False = idle.");
            SetTip(this.Component.Params.Output, 0, "Rh_path",
                "Folder that contains the saved .3dm file. Warns if the Rhino file is not saved yet.");
            SetTip(this.Component.Params.Output, 1, "Gh_path",
                "Full path of the saved Grasshopper file (.gh / .ghx). Warns if it is not saved yet.");
        }

        // 2. Defaults (safe startup)
        Rh_path = "Toggle Get to True";
        Gh_path = "Toggle Get to True";

        if (!Get) return;

        try
        {
            // 3. Rhino model (.3dm) folder
            RhinoDoc doc = RhinoDoc.ActiveDoc;
            if (doc == null || string.IsNullOrEmpty(doc.Path))
            {
                Rh_path = "Rhino document not saved.";
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Rhino document not saved.");
            }
            else
            {
                Rh_path = Path.GetDirectoryName(doc.Path);
            }

            // 4. Grasshopper document (.gh / .ghx) path
            // Use THIS component's own document rather than the active canvas:
            // OnPingDocument() returns the document the component lives in, so
            // it still works when another Grasshopper window has focus.
            GH_Document ghDoc = this.Component?.OnPingDocument();
            if (ghDoc == null)
                ghDoc = Grasshopper.Instances.ActiveCanvas?.Document;

            if (ghDoc == null || string.IsNullOrEmpty(ghDoc.FilePath))
            {
                Gh_path = "Grasshopper file not saved.";
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Grasshopper file not saved.");
            }
            else
            {
                Gh_path = ghDoc.FilePath;
            }
        }
        catch (Exception ex)
        {
            // Show the error on the pin AND as a red balloon
            Rh_path = "Error: " + ex.Message;
            Gh_path = "Error: " + ex.Message;
            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Path retrieval error: " + ex.Message);
        }
    }

    // Sets the name, nickname and hover tooltip of one input/output pin,
    // guarded by index in case the component has fewer params than expected.
    private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].Name = name;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}
