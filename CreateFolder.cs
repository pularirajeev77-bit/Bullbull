/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2026.04.05
  Component: CreateFolder
  Description:
    Creates a folder at the specified path + name.
    Executes only when 'Create' toggle is True.
*/

using System;
using System.IO;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
		string Path,
		string Name,
		bool Create,
		ref object FolderPath,
		ref object Msg)
    {
        // 1. Set Component Metadata + pin tooltips (once)
        if (this.Component != null && this.Component.Message != "Create Folder")
        {
            this.Component.Message = "Create Folder";
            this.Component.NickName = "CrtFold";
            this.Component.Name = "Create Folder";
            this.Component.Description =
                "Creates a folder at Path\\Name. Runs only when Create is True.";

            SetTip(this.Component.Params.Input, 0, "Path",
                "Base folder that will contain the new folder (an absolute path like C:\\Jobs).");
            SetTip(this.Component.Params.Input, 1, "Name",
                "Name of the folder to create inside Path. May include sub-folders (a\\b makes both).");
            SetTip(this.Component.Params.Input, 2, "Create",
                "Set True to actually create the folder. False = preview the path only, nothing is written.");
            SetTip(this.Component.Params.Output, 0, "FolderPath",
                "The full path (Path + Name), whether or not it was created.");
            SetTip(this.Component.Params.Output, 1, "Msg",
                "Status: created, already exists, waiting for Create, or an error.");
        }

        // 2. Initialize defaults
        FolderPath = null;
        Msg = "";

        try
        {
            // 3. Input validation
            if (string.IsNullOrWhiteSpace(Path))
            {
                Msg = "Please provide a valid base path.";
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, Msg);
                return;
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                Msg = "Please provide a folder name.";
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, Msg);
                return;
            }

            // Trim leading slashes on Name. Path.Combine("C:\\Jobs", "\\Sub")
            // ignores the base and returns "\\Sub", so a stray leading slash
            // would silently create the folder in the wrong place.
            string cleanName = Name.Trim().TrimStart('\\', '/');

            // Reject characters that are never legal in a path. Separators
            // (\ /) are NOT in this set, so a sub-folder name like a\b still
            // works; anything else illegal is left to the try/catch below.
            if (cleanName.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
            {
                Msg = "Folder name contains invalid characters.";
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, Msg);
                return;
            }

            // 4. Path Logic
            string target = System.IO.Path.Combine(Path.Trim(), cleanName);
            FolderPath = target;

            if (!Create)
            {
                Msg = "Set 'Create' = True to make the folder.";
                return;
            }

            // 5. Execution Logic
            if (!Directory.Exists(target))
            {
                Directory.CreateDirectory(target);
                Msg = "Folder created: " + target;
            }
            else
            {
                Msg = "Folder already exists: " + target;
            }
        }
        catch (Exception ex)
        {
            // Show the error on the output pin AND as a red balloon
            Msg = "Error: " + ex.Message;
            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Folder creation error: " + ex.Message);
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
