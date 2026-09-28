
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  /*
    Members:
      RhinoDoc RhinoDocument
      GH_Document GrasshopperDocument
      IGH_Component Component
      int Iteration

    Methods (Virtual & overridable):
      Print(string text)
      Print(string format, params object[] args)
      Reflect(object obj)
      Reflect(object obj, string method_name)
  */



  private void RunScript(List<string> Mtext, ref object Stext)
  {
    // Set Component Metadata (Rhino 8 feature)
        Component.Message = "Multi > single line text";
        Component.NickName = "Multi > Single-Line-Text";

    if (Mtext == null || Mtext.Count == 0)
    {
        Stext = "";
        return;
    }

    List<string> lines = new List<string>();
    foreach (var item in Mtext)
    {
        if (item != null)
            lines.Add(item.ToString());
    }

    Stext = string.Join(Environment.NewLine, lines);
  }
}
