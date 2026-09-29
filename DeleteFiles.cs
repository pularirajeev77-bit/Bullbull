/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2026.04.05
  Component: DeleteFiles
  Description:
    Deletes all files in a specified folder matching a given extension.
    Triggered by a Boolean push button. Deletion is PERMANENT (no Recycle Bin).
*/

using System;
using System.IO;
using System.Collections.Generic;
using Grasshopper.Kernel;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
		string FolderPath,
		string FileExtension,
		bool Button,
		ref object Deleted,
		ref object Msg)
    {
        // 1. Set Component Metadata + pin tooltips (once)
        if (this.Component != null && this.Component.Message != "Delete Files")
        {
            this.Component.Message = "Delete Files";
            this.Component.NickName = "DelFiles";
            this.Component.Name = "Delete Files";
            this.Component.Description =
                "Deletes all files with a given extension from a folder. PERMANENT - files do not go to the Recycle Bin. Runs only when Button is True.";

            SetTip(this.Component.Params.Input, 0, "FolderPath",
                "Folder to delete files from (absolute path). Only this folder, not sub-folders.");
            SetTip(this.Component.Params.Input, 1, "FileExtension",
                "Extension to match, e.g. .bak or txt. Only files with exactly this extension are deleted.");
            SetTip(this.Component.Params.Input, 2, "Button",
                "Set True (a button) to delete NOW. Deletion is PERMANENT - no Recycle Bin.");
            SetTip(this.Component.Params.Output, 0, "Deleted",
                "Names of the files that were deleted (and any that failed, marked with their error).");
            SetTip(this.Component.Params.Output, 1, "Msg",
                "Summary of what happened.");
        }

        // 2. Initialize safe outputs
        var deletedList = new List<string>();
        string message = "";

        try
        {
            // 3. Safety checks
            if (!Button)
            {
                Msg = "Toggle Button to True to delete files.";
                Deleted = deletedList;
                return;
            }

            if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
            {
                message = "Folder not found: " + FolderPath;
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, message);
                Msg = message;
                Deleted = deletedList;
                return;
            }

            if (string.IsNullOrWhiteSpace(FileExtension))
            {
                message = "No file extension provided (e.g. '.txt' or '.bak').";
                this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, message);
                Msg = message;
                Deleted = deletedList;
                return;
            }

            // 4. File Deletion Logic
            // Normalize extension
            string ext = FileExtension.Trim();
            if (!ext.StartsWith(".")) ext = "." + ext;

            // Collect matching files, then filter to an EXACT extension match.
            // Directory.GetFiles("*.bak") also returns files whose extension
            // merely STARTS with "bak" when the pattern extension is 3 chars
            // (a documented Windows quirk, e.g. "*.xls" also matches ".xlsx"),
            // so without this filter it could delete more than intended.
            string[] found = Directory.GetFiles(FolderPath, "*" + ext, SearchOption.TopDirectoryOnly);

            var files = new List<string>();
            foreach (string f in found)
            {
                if (string.Equals(Path.GetExtension(f), ext, StringComparison.OrdinalIgnoreCase))
                    files.Add(f);
            }

            int count = 0;
            int failed = 0;

            foreach (string file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedList.Add(Path.GetFileName(file));
                    count++;
                }
                catch (Exception ex)
                {
                    // Track specific file errors (e.g. file in use)
                    deletedList.Add(Path.GetFileName(file) + " -> FAILED: " + ex.Message);
                    failed++;
                }
            }

            // 5. Final Message status
            if (count == 0 && failed == 0)
                message = "No '" + ext + "' files found in folder.";
            else
            {
                message = "Deleted " + count + " file(s) with extension '" + ext + "'.";
                if (failed > 0)
                {
                    message += " " + failed + " could not be deleted.";
                    this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        failed + " file(s) could not be deleted (see Deleted output).");
                }
            }
        }
        catch (Exception ex)
        {
            message = "System Error: " + ex.Message;
            this.Component?.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
        }

        // 6. Final Assignment to Output Pins
        Deleted = deletedList;
        Msg = message;
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
