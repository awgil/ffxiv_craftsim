using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Linq;
using System.Text;

namespace craftsim;

public class SimulatorUI
{
    private Random _rng = new();
    private int _seed = 0;
    private CraftState _craft;
    private Simulator? _sim;
    private ActionResult _forcedResult;
    private Solver _solver = new();

    public SimulatorUI(CraftState craft)
    {
        _craft = craft;
    }

    public void Draw()
    {
        if (ImGui.Button("Restart!"))
        {
            _seed = _rng.Next();
            _sim = new(_craft, _seed);
        }
        ImGui.SameLine();
        if (ImGui.Button("Restart and solve"))
        {
            _seed = _rng.Next();
            _sim = new(_craft, _seed);
            _solver.Solve(_sim);
        }
        ImGui.SameLine();
        if (ImGui.Button("Restart and solve until error"))
        {
            do
            {
                _seed = _rng.Next();
                _sim = new(_craft, _seed);
                _solver.Solve(_sim);
            }
            while (_sim.Status() is not CraftStatus.InProgress/* and not CraftStatus.FailedDurability*/);
        }
        ImGui.SameLine();
        if (ImGui.Button($"Restart with seed:"))
            _sim = new(_craft, _seed);
        ImGui.SameLine();
        ImGui.InputInt("###Seed", ref _seed);
        if (_sim == null)
            return;

        if (ImGui.Button("Solve next"))
            _sim.Execute(_solver.SolveNextStep(_sim));
        ImGui.SameLine();
        if (ImGui.Button("Solve all"))
            _solver.Solve(_sim);
        ImGui.SameLine();
        ImGui.TextUnformatted($"Suggestion: {_solver.SolveNextStep(_sim)}");

        var status = _sim.Status();
        ImGui.TextUnformatted($"{status}; base progress = {_sim.BaseProgress()}, base quality = {_sim.BaseQuality()}");
        if (status == CraftStatus.InProgress && ImGui.CollapsingHeader("Manual actions"))
        {
            int cntr = 0;
            var curStep = _sim.Steps.Last();
            foreach (var opt in Enum.GetValues(typeof(CraftAction)).Cast<CraftAction>())
            {
                if (opt == CraftAction.None)
                    continue;

                if ((cntr++ % 6) != 0)
                    ImGui.SameLine();
                using var dis = ImRaii.Disabled(!_sim.CanUseAction(curStep, opt) || curStep.RemainingCP < _sim.GetCPCost(curStep, opt));
                if (ImGui.Button($"{opt} ({_sim.GetCPCost(curStep, opt)}cp, {_sim.GetDurabilityCost(curStep, opt)}dur)"))
                {
                    _sim.Execute(opt, _forcedResult);
                    _forcedResult = ActionResult.Random;
                }
            }

            using (var combo = ImRaii.Combo("Override last condition", curStep.Condition.ToString()))
            {
                if (combo)
                {
                    foreach (var opt in Enum.GetValues(typeof(CraftCondition)).Cast<CraftCondition>())
                    {
                        if (ImGui.Selectable(opt.ToString(), curStep.Condition == opt))
                        {
                            curStep.Condition = opt;
                        }
                    }
                }
            }

            using (var combo = ImRaii.Combo("Override next action success", _forcedResult.ToString()))
            {
                if (combo)
                {
                    foreach (var opt in Enum.GetValues(typeof(ActionResult)).Cast<ActionResult>())
                    {
                        if (ImGui.Selectable(opt.ToString(), _forcedResult == opt))
                        {
                            _forcedResult = opt;
                        }
                    }
                }
            }
        }

        foreach (var step in _sim.Steps)
        {
            DrawProgress(step.Progress, _craft.CraftProgress);
            ImGui.SameLine();
            DrawProgress(step.Quality, _craft.CraftQualityMax);
            ImGui.SameLine();
            DrawProgress(step.Durability, _craft.CraftDurability);
            ImGui.SameLine();
            DrawProgress(step.RemainingCP, _craft.StatCP);
            ImGui.SameLine();

            var sb = new StringBuilder($"{step.Condition}; IQ:{step.IQStacks}");
            AddBuff(sb, "WN", step.WasteNotLeft);
            AddBuff(sb, "Manip", step.ManipulationLeft);
            AddBuff(sb, "GS", step.GreatStridesLeft);
            AddBuff(sb, "Inno", step.InnovationLeft);
            AddBuff(sb, "Vene", step.VenerationLeft);
            AddBuff(sb, "MuMe", step.MuscleMemoryLeft);
            AddBuff(sb, "FA", step.FinalAppraisalLeft);
            if (step.HeartAndSoulActive)
                sb.Append(" HnS");
            if (step.Action != CraftAction.None)
                sb.Append($"; used {step.Action}{(step.ActionSucceeded ? "" : " (fail)")}");
            ImGui.TextUnformatted(sb.ToString());
        }
    }

    private void DrawProgress(int a, int b) => ImGui.ProgressBar((float)a / b, new(150, 0), $"{a * 100.0f / b:f2}% ({a}/{b})");

    private void AddBuff(StringBuilder sb, string mnem, int left)
    {
        if (left > 0)
            sb.Append($" {mnem}:{left}");
    }
}
