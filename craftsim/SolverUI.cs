using ImGuiNET;
using System;
using System.Linq;

namespace craftsim;

public class SolverUI
{
    private CraftState _craft;
    private Solver _solver = new();
    private int _newExperimentCount = 100000;
    // last run stats
    private int _numExperiments;
    private int[] _numOutcomes = new int[(int)CraftStatus.Count];
    private float _averagePliantCost = float.NaN;

    public SolverUI(CraftState craft)
    {
        _craft = craft;
    }

    public void Draw()
    {
        if (ImGui.CollapsingHeader("Solver setup"))
            _solver.Draw();
        ImGui.InputInt("Num experiments", ref _newExperimentCount);
        ImGui.SameLine();
        if (ImGui.Button("Run!"))
            RunExperiments();

        if (_numExperiments > 0)
        {
            DrawNumber("Execution errors", _numOutcomes[0]);
            DrawNumber("Fails (durability)", _numOutcomes[1]);
            DrawNumber("Fails (quality)", _numOutcomes[2]);
            DrawNumber("Success Q1", _numOutcomes[3]);
            DrawNumber("Success Q2", _numOutcomes[4]);
            DrawNumber("Success Q3", _numOutcomes[5]);
            ImGui.TextUnformatted($"Average yield: {(double)(_numOutcomes[3] + 2 * _numOutcomes[4] + 3 * _numOutcomes[5]) / _numExperiments:f3}");
        }

        ImGui.Separator();
        if (ImGui.Button(!float.IsNaN(_averagePliantCost) ? $"Average pliant cost: {_averagePliantCost:f3}" : "Calculate average pliant cost"))
            RunPliantCalc();
    }

    private void DrawNumber(string prompt, int count) => ImGui.TextUnformatted($"{prompt}: {count} ({count * 100.0 / _numExperiments:f2}%)");

    private void RunExperiments()
    {
        Array.Fill(_numOutcomes, 0);
        var rng = new Random();
        for (int i = 0; i < _newExperimentCount; ++i)
        {
            var sim = new Simulator(_craft, rng.Next());
            var res = _solver.Solve(sim, sim.CreateInitial());
            ++_numOutcomes[(int)sim.Status(res)];
        }
        _numExperiments = _newExperimentCount;
    }

    private void RunPliantCalc()
    {
        _averagePliantCost = 0;
        var rng = new Random();
        for (int i = 0; i < _newExperimentCount; ++i)
        {
            var sim = new Simulator(_craft, rng.Next());
            var (_, step) = sim.Execute(sim.CreateInitial(), CraftAction.Observe); // start from random condition
            while (step.Condition != CraftCondition.Pliant)
            {
                if (step.Condition == CraftCondition.Good)
                {
                    _averagePliantCost -= 20;
                    (_, step) = sim.Execute(step, CraftAction.TricksOfTrade);
                }
                else
                {
                    _averagePliantCost += sim.GetCPCost(step, CraftAction.Observe);
                    (_, step) = sim.Execute(step, CraftAction.Observe);
                }
            }
        }
        _averagePliantCost /= _newExperimentCount;
    }
}
