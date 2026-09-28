/*
  Author: Rajeev Pulari + ChatGPT
  Rhino 8 | Grasshopper C#
  Version: 2026.04.05
  Component: Bullbull_Calculation
  Description:
    Calculates weight (kg), area (m²), and volume (m³) from Breps.
    Auto-adaptive (item/list/tree via GH access modes).
*/

using System;
using System.Collections.Generic;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;

public class Script_Instance : GH_ScriptInstance
{
    // Professional Signature: Strongly Typed Inputs for Rhino 8
    // Outputs: out object Wt, out object Area, out object Vol, out object VolRaw
    private void RunScript(
		List<Brep> Breps,
		double Density,
		string ModelUnits,
		ref object Wt,
		ref object Area,
		ref object Vol,
		ref object VolRaw)
    {
        // 1. Set Component Metadata (Rhino 8 Native)
        // Check prevents unnecessary UI refreshes which can disrupt data streams
        if (this.Component != null && this.Component.Message != "Calculator 2.0")
        {
            this.Component.Message = "Calculator 2.0";
            this.Component.NickName = "Calci";
            this.Component.Name = "Calci";
            this.Component.Description = "Weight, area and volume of solids (Breps).";

            // Tooltips: hover text shown on each input/output pin. Set once,
            // inside the same guard, so it isn't rewritten every solution.
            SetTip(this.Component.Params.Input,  0, "Breps",      "Solids to measure (list).");
            SetTip(this.Component.Params.Input,  1, "Density",    "Material density in kg/m3 (steel ~ 7850, concrete ~ 2400).");
            SetTip(this.Component.Params.Input,  2, "ModelUnits", "Rhino file units: mm, cm or m. Anything else gives 0.");
            SetTip(this.Component.Params.Output, 0, "Wt",         "Weight in kilograms (volume in m3 x Density).");
            SetTip(this.Component.Params.Output, 1, "Area",       "Surface area in square metres (m2).");
            SetTip(this.Component.Params.Output, 2, "Vol",        "Volume in cubic metres (m3).");
            SetTip(this.Component.Params.Output, 3, "VolRaw",     "Volume in the model's own units, before conversion.");
        }

        // 2. Initialize Output Lists
        var weights = new List<double>();
        var areas = new List<double>();
        var volumes = new List<double>();
        var raws = new List<double>();

        try
        {
            // 3. Early exit if no geometry
            if (Breps == null || Breps.Count == 0)
            {
                Wt = weights; Area = areas; Vol = volumes; VolRaw = raws;
                return;
            }

            // 4. Unit Conversion Setup
            string units = (ModelUnits ?? "mm").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(units)) units = "mm";

            // 5. Process each Brep independently
            foreach (Brep brep in Breps)
            {
                if (brep == null)
                {
                    weights.Add(double.NaN);
                    areas.Add(double.NaN);
                    volumes.Add(double.NaN);
                    raws.Add(double.NaN);
                    continue;
                }

                // Compute Mass Properties
                VolumeMassProperties vProps = VolumeMassProperties.Compute(brep);
                AreaMassProperties aProps = AreaMassProperties.Compute(brep);

                if (vProps == null || aProps == null)
                {
                    weights.Add(double.NaN);
                    areas.Add(double.NaN);
                    volumes.Add(double.NaN);
                    raws.Add(double.NaN);
                    continue;
                }

                double rawVol = vProps.Volume;
                double rawArea = aProps.Area;
                double vol_m3 = 0;
                double area_m2 = 0;

                // Professional Unit Conversion (Logic Preserved)
                switch (units)
                {
                    case "mm": 
                        vol_m3 = rawVol * 1e-9; 
                        area_m2 = rawArea * 1e-6; 
                        break;
                    case "cm": 
                        vol_m3 = rawVol * 1e-6; 
                        area_m2 = rawArea * 1e-4; 
                        break;
                    case "m":  
                        vol_m3 = rawVol;         
                        area_m2 = rawArea;         
                        break;
                    default:   
                        vol_m3 = 0; 
                        area_m2 = 0; 
                        break;
                }

                double weight_kg = vol_m3 * Density;

                weights.Add(weight_kg);
                areas.Add(area_m2);
                volumes.Add(vol_m3);
                raws.Add(rawVol);
            }
        }
        catch (Exception ex)
        {
            // Professional error reporting to the component "out" window
            Print("Error in Bullbull_Calculation: " + ex.Message);
            Wt = Area = Vol = VolRaw = null;
            return;
        }

        // 6. Final Assignment to Output Pins
        Wt = weights;
        Area = areas;
        Vol = volumes;
        VolRaw = raws;
    }

    // Sets the name, nickname and hover tooltip of one input/output pin,
    // guarding the index in case the component has fewer params than expected.
    private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
    {
        if (ps == null || i < 0 || i >= ps.Count) return;
        ps[i].Name = name;
        ps[i].NickName = name;
        ps[i].Description = tip;
    }
}