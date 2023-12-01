using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace craftsim;

public class SimulatorUI
{
    private Random _rng = new();
    private int _seed = 0;
    private CraftState _craft;
    private Simulator? _sim;
    private List<(StepState state, CraftAction action, bool success)> _steps = new();
    private ForcedResult _forcedResult;
    private Solver _solver = new();

    public SimulatorUI(CraftState craft)
    {
        _craft = craft;
    }

    public void Draw()
    {
        if (ImGui.Button("Restart!"))
        {
            Restart(_rng.Next());
        }
        ImGui.SameLine();
        if (ImGui.Button("Restart and solve"))
        {
            Restart(_rng.Next());
            SolveRest();
        }
        ImGui.SameLine();
        if (ImGui.Button("Restart and solve until error"))
        {
            do
            {
                Restart(_rng.Next());
                SolveRest();
            }
            while (_sim!.Status(_steps.Last().state) is not CraftStatus.InProgress/* and not CraftStatus.FailedDurability*/);
        }
        ImGui.SameLine();
        if (ImGui.Button($"Restart with seed:"))
            Restart(_seed);
        ImGui.SameLine();
        ImGui.InputInt("###Seed", ref _seed);
        if (_sim == null)
            return;

        if (ImGui.Button("Solve next"))
            SolveNext();
        ImGui.SameLine();
        if (ImGui.Button("Solve all"))
            SolveRest();
        ImGui.SameLine();
        ImGui.TextUnformatted($"Suggestion: {_solver.SolveNextStep(_sim, _steps.Last().state)}");

        if (ImGui.CollapsingHeader("Solver strategy"))
            _solver.Draw();

        var status = _sim.Status(_steps.Last().state);
        ImGui.TextUnformatted($"{status}; base progress = {_sim.BaseProgress()}, base quality = {_sim.BaseQuality()}");
        if (status == CraftStatus.InProgress && ImGui.CollapsingHeader("Manual actions"))
        {
            int cntr = 0;
            var curStep = _steps.Last().state;
            foreach (var opt in Enum.GetValues(typeof(CraftAction)).Cast<CraftAction>())
            {
                if (opt == CraftAction.None)
                    continue;

                if ((cntr++ % 6) != 0)
                    ImGui.SameLine();
                using var dis = ImRaii.Disabled(!_sim.CanUseAction(curStep, opt) || curStep.RemainingCP < _sim.GetCPCost(curStep, opt));
                if (ImGui.Button($"{opt} ({_sim.GetCPCost(curStep, opt)}cp, {_sim.GetDurabilityCost(curStep, opt)}dur)"))
                {
                    _sim.Execute(curStep, opt, _forcedResult);
                    _forcedResult = ForcedResult.Random;
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
                    foreach (var opt in Enum.GetValues(typeof(ForcedResult)).Cast<ForcedResult>())
                    {
                        if (ImGui.Selectable(opt.ToString(), _forcedResult == opt))
                        {
                            _forcedResult = opt;
                        }
                    }
                }
            }
        }

        foreach (var step in _steps)
        {
            DrawProgress(step.state.Progress, _craft.CraftProgress);
            ImGui.SameLine();
            DrawProgress(step.state.Quality, _craft.CraftQualityMax);
            ImGui.SameLine();
            DrawProgress(step.state.Durability, _craft.CraftDurability);
            ImGui.SameLine();
            DrawProgress(step.state.RemainingCP, _craft.StatCP);
            ImGui.SameLine();

            var sb = new StringBuilder($"{step.state.Condition}; IQ:{step.state.IQStacks}");
            AddBuff(sb, "WN", step.state.WasteNotLeft);
            AddBuff(sb, "Manip", step.state.ManipulationLeft);
            AddBuff(sb, "GS", step.state.GreatStridesLeft);
            AddBuff(sb, "Inno", step.state.InnovationLeft);
            AddBuff(sb, "Vene", step.state.VenerationLeft);
            AddBuff(sb, "MuMe", step.state.MuscleMemoryLeft);
            AddBuff(sb, "FA", step.state.FinalAppraisalLeft);
            if (step.state.HeartAndSoulActive)
                sb.Append(" HnS");
            if (step.action != CraftAction.None)
                sb.Append($"; used {step.action}{(step.success ? "" : " (fail)")}");
            ImGui.TextUnformatted(sb.ToString());
        }
    }

    private void Restart(int seed)
    {
        _seed = seed;
        _sim = new(_craft, seed);
        _steps.Clear();
        _steps.Add((_sim.CreateInitial(), CraftAction.None, false));
    }

    private bool SolveNext()
    {
        if (_sim == null)
            return false;
        var state = _steps.Last().state;
        var action = _solver.SolveNextStep(_sim, state);
        var (res, next) = _sim.Execute(state, action);
        if (res == ExecuteResult.CantUse)
            return false;
        _steps[_steps.Count - 1] = (state, action, res == ExecuteResult.Succeeded);
        _steps.Add((next, CraftAction.None, false));
        return true;
    }

    private void SolveRest()
    {
        while (SolveNext())
            ;
    }

    private void DrawProgress(int a, int b) => ImGui.ProgressBar((float)a / b, new(150, 0), $"{a * 100.0f / b:f2}% ({a}/{b})");

    private void AddBuff(StringBuilder sb, string mnem, int left)
    {
        if (left > 0)
            sb.Append($" {mnem}:{left}");
    }
}
