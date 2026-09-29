/*
  Author: Rajeev Pulari + Gemini
  Rhino 8 | Grasshopper C#
  Version: 2025.11.10
  Component: Timer Counter v3.5 (Stable)
  Description:
    A counter driven by System.Diagnostics.Stopwatch. Counts up from start to
    end, one step per 'delay' milliseconds, and can loop or reset. State is
    kept PER COMPONENT across solutions, so several timers can run at once.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

public class Script_Instance : GH_ScriptInstance
{
  // Per-component state. The original used STATIC fields, so every Timer
  // Counter on the canvas shared one counter/stopwatch and fought each other.
  // Keyed by the component's InstanceGuid, each component now keeps its own
  // state, and it still persists across solutions.
  private class TState
  {
    public double counter = 0;
    public bool isRunning = false;
    public Stopwatch watch = new Stopwatch();
  }
  private static readonly Dictionary<Guid, TState> _states = new Dictionary<Guid, TState>();

  private void RunScript(
		int start,
		int end,
		double delay,
		bool loop,
		bool Run,
		bool reset,
		ref object I,
		ref object pct)
  {
    // 0) Metadata (names/tooltips once) + live status message every solve
    if (this.Component != null)
    {
      if (this.Component.Name != "Timer Counter")
      {
        this.Component.Name = "Timer Counter";
        this.Component.NickName = "TimerCnt";
        this.Component.Description =
            "Counts up from start to end, one step per 'delay' ms. Can loop or reset. Each component keeps its own state.";

        SetTip(this.Component.Params.Input, 0, "start", "First count value.");
        SetTip(this.Component.Params.Input, 1, "end",   "Last count value (must be greater than start).");
        SetTip(this.Component.Params.Input, 2, "delay", "Milliseconds per step. 0 or less uses 100.");
        SetTip(this.Component.Params.Input, 3, "loop",  "True = jump back to start after reaching end instead of stopping.");
        SetTip(this.Component.Params.Input, 4, "Run",   "True = count. False = pause (keeps the current value).");
        SetTip(this.Component.Params.Input, 5, "reset", "True = set the count back to start and stop.");
        SetTip(this.Component.Params.Output, 0, "I",   "Current count value.");
        SetTip(this.Component.Params.Output, 1, "pct", "Progress from start to end, 0-100 %.");
      }
      this.Component.Message = Run ? "Counting..." : "Paused";
    }

    // Get this component's own state
    Guid key = (this.Component != null) ? this.Component.InstanceGuid : Guid.Empty;
    TState st;
    if (!_states.TryGetValue(key, out st))
    {
      st = new TState { counter = start };
      _states[key] = st;
    }

    // 1) Safety defaults
    if (delay <= 0) delay = 100;

    // end must be strictly greater than start (this counter only counts up).
    // Original only handled end == start; end < start would snap straight to
    // end. Both degenerate cases now return cleanly.
    if (end <= start)
    {
      st.isRunning = false;
      st.watch.Reset();
      I = (double)start;
      pct = 100.0;
      return;
    }

    // 2) Reset
    if (reset)
    {
      st.counter = start;
      st.isRunning = false;
      st.watch.Reset();
    }

    // 3) Run
    if (Run)
    {
      if (!st.isRunning)
      {
        st.watch.Restart();
        st.isRunning = true;
      }
      else
      {
        double elapsed = st.watch.Elapsed.TotalMilliseconds;
        if (elapsed >= delay)
        {
          int steps = (int)(elapsed / delay);
          st.counter += steps;
          st.watch.Restart();

          if (st.counter >= end)
          {
            if (loop)
              st.counter = start;
            else
            {
              st.counter = end;
              st.isRunning = false;
              st.watch.Reset();
            }
          }
        }
      }

      // Re-run on the next frame while counting. This is the standard
      // self-updating-timer trick; the stopwatch above is what actually
      // paces the steps, so this just keeps the component alive.
      if (st.isRunning)
        this.Component.ExpireSolution(true);
    }
    else
    {
      st.isRunning = false;
      st.watch.Stop(); // pause, keep progress
    }

    // 4) Outputs
    I = st.counter;

    double totalRange = (double)end - start;
    double pctVal = ((st.counter - start) / totalRange) * 100.0;
    pct = Math.Max(0.0, Math.Min(100.0, pctVal));
  }

  private void SetTip(System.Collections.Generic.IList<IGH_Param> ps, int i, string name, string tip)
  {
    if (ps == null || i < 0 || i >= ps.Count) return;
    ps[i].Name = name;
    ps[i].NickName = name;
    ps[i].Description = tip;
  }
}
