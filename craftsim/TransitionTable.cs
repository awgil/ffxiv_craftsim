using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System.Linq;
using System;
using System.Text.Json.Nodes;

namespace craftsim;

class TransitionTable
{
    private const int NumConditions = (int)CraftCondition.Count;

    private int _transTotal;
    private int[] _transTo = new int[NumConditions];
    private int[] _transFrom = new int[NumConditions];
    private int[,] _trans = new int[NumConditions, NumConditions]; // [from, to]

    private DateTime _throttleAction;
    private int _prevStepCount;
    private int _prevCond;
    public bool Auto;

    public bool Modified { get; private set; }

    public TransitionTable() { }

    public TransitionTable(JsonArray json) : base()
    {
        LoadFromJSON(json);
    }

    public void Update()
    {
        var (step, cond) = GetCurrentState();
        if (step != _prevStepCount)
        {
            if (step > 1 && _prevStepCount != 0)
                RecordTransition(_prevCond, cond);
            _prevStepCount = step;
            _prevCond = cond;
        }

        if (Auto && step > 0 && Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.Crafting40] && DateTime.Now >= _throttleAction)
            Step();
    }

    public void Draw()
    {
        var (step, cond) = GetCurrentState();
        var player = Service.ClientState.LocalPlayer;
        if (step > 0 && player != null)
        {
            ImGui.TextUnformatted($"Step: {step}, Condition: {cond}, CP: {player.CurrentCp}");
            ImGui.Checkbox("Auto repeat", ref Auto);
            if (ImGui.Button("Step"))
                Step();
        }

        using var table = ImRaii.Table("results", _transFrom.Count(t => t > 0) + 2);
        if (table)
        {
            ImGui.TableSetupColumn("To", ImGuiTableColumnFlags.WidthFixed, 100);
            for (int from = 0; from < NumConditions; ++from)
                if (_transFrom[from] > 0)
                    ImGui.TableSetupColumn(((CraftCondition)from).ToString(), ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableHeadersRow();

            for (int to = 0; to < NumConditions; ++to)
            {
                if (_transTo[to] == 0)
                    continue;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(((CraftCondition)to).ToString());
                for (int from = 0; from < NumConditions; ++from)
                {
                    if (_transFrom[from] == 0)
                        continue;
                    ImGui.TableNextColumn();
                    DrawProgress(_trans[from, to], _transFrom[from]);
                }
                ImGui.TableNextColumn();
                DrawProgress(_transTo[to], _transTotal);
            }
        }
    }

    public void Deactivate()
    {
        _prevStepCount = _prevCond = 0;
        Auto = false;
    }

    public void Clear()
    {
        _transTotal = 0;
        Array.Fill(_transTo, 0);
        Array.Fill(_transFrom, 0);
        for (int i = 0; i < NumConditions; ++i)
            for (int j = 0; j < NumConditions; ++j)
                _trans[i, j] = 0;
    }

    public JsonArray SaveToJSON()
    {
        Modified = false;
        var res = new JsonArray();
        for (int from = 0; from < NumConditions; ++from)
        {
            var row = new JsonArray();
            for (int to = 0; to < NumConditions; ++to)
                row.Add(_trans[from, to]);
            res.Add(row);
        }
        return res;
    }

    public void LoadFromJSON(JsonArray json)
    {
        Clear();
        for (int from = 0; from < Math.Min(NumConditions, json.Count); ++from)
        {
            var row = json[from] as JsonArray;
            if (row != null)
                for (int to = 0; to < Math.Min(NumConditions, row.Count); ++to)
                    RecordTransition(from, to, row[to]?.GetValue<int>() ?? 0);
        }
        Modified = false;
    }

    public void RecordTransition(int from, int to, int count = 1)
    {
        _transTotal += count;
        _transTo[to] += count;
        _transFrom[from] += count;
        _trans[from, to] += count;
        Modified = true;
    }

    private unsafe (int step, int condition) GetCurrentState()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("Synthesis");
        if (addon == null || addon->AtkValuesCount <= 15)
            return (0, 0);

        var step = addon->AtkValues[15].Int;
        var cond = addon->AtkValues[12].Int;
        if (cond >= NumConditions)
            throw new Exception($"Condition {cond} out of range");
        return (step, cond);
    }

    private unsafe void Step()
    {
        uint action = Service.ClientState.LocalPlayer?.CurrentCp >= 7 ? 100010u : 100001u;
        ActionManager.Instance()->UseAction(ActionType.CraftAction, action);
        _throttleAction = DateTime.Now.AddSeconds(0.5);
    }

    private void DrawProgress(int a, int b) => ImGui.ProgressBar((float)a / b, new(130, 0), $"{a * 100.0f / b:f2}% ({a}/{b})");
}
