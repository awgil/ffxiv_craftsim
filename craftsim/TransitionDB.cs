using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace craftsim;

class TransitionDB
{
    private GameCraftState _game;
    private Dictionary<string, TransitionTable> _transitions = new();
    private TransitionTable? _active;
    private string _activeName = "";
    private string _newName = "";

    private DateTime _throttleAction;
    private int _prevStepCount;
    private CraftCondition _prevCond;
    public bool Auto;

    public TransitionDB(GameCraftState game)
    {
        _game = game;
        LoadFromJSON(Service.Config.TransitionDB);
    }

    public void Update()
    {
        var step = _game.CurStep?.Index ?? 0;
        var cond = _game.CurStep?.Condition ?? CraftCondition.Normal;
        if (step != _prevStepCount)
        {
            if (step > 1 && _prevStepCount != 0)
                _active?.RecordTransition((int)_prevCond, (int)cond);
            _prevStepCount = step;
            _prevCond = cond;
        }

        if (Auto && step > 0 && Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.Crafting40] && DateTime.Now >= _throttleAction)
            Step();
    }

    public void Draw()
    {
        DrawCombo();
        DrawRenameNew();

        var player = Service.ClientState.LocalPlayer;
        if (_prevStepCount > 0 && player != null)
        {
            ImGui.TextUnformatted($"Step: {_prevStepCount}, Condition: {_prevCond}, CP: {player.CurrentCp}");
            ImGui.Checkbox("Auto repeat", ref Auto);
            if (ImGui.Button("Step"))
                Step();
        }

        _active?.Draw();
    }

    public JsonObject SaveToJSON()
    {
        var res = new JsonObject();
        foreach (var (k, v) in _transitions)
            res[k] = v.SaveToJSON();
        return res;
    }

    public void LoadFromJSON(JsonObject json)
    {
        Auto = false;
        _transitions.Clear();
        foreach (var (k, v) in json)
        {
            var varr = v as JsonArray;
            if (varr != null)
                _transitions[k] = new TransitionTable(varr);
        }
        _active = null;
        _activeName = "";
    }

    private void DrawCombo()
    {
        using var combo = ImRaii.Combo("Transition table", _activeName);
        if (combo)
        {
            if (ImGui.Selectable("", _active == null))
            {
                Auto = false;
                _active = null;
                _activeName = "";
            }
            foreach (var (k, v) in _transitions)
            {
                if (ImGui.Selectable(k, _active == v))
                {
                    Auto = false;
                    _active = v;
                    _activeName = k;
                }
            }
        }
        else
        {
            ImGui.SameLine();
            using (ImRaii.Disabled(_active == null || !_active.Modified))
            {
                if (ImGui.Button("Save"))
                {
                    Service.Config.TransitionDB = SaveToJSON();
                    Service.SaveConfig();
                }
            }
        }
    }

    private void DrawRenameNew()
    {
        ImGui.InputText("###newname", ref _newName, 500);
        using (ImRaii.Disabled(_newName.Length == 0 || _transitions.ContainsKey(_newName)))
        {
            ImGui.SameLine();
            using (ImRaii.Disabled(_active == null))
            {
                if (ImGui.Button("Rename") && _active != null)
                {
                    _transitions.Remove(_activeName);
                    _transitions[_newName] = _active;
                    _activeName = _newName;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("New"))
            {
                Auto = false;
                _active = new();
                _activeName = _newName;
                _transitions[_newName] = _active;
            }
        }
    }

    private void Step()
    {
        _game.UseAction(_prevCond == CraftCondition.Good ? CraftAction.TricksOfTrade : Service.ClientState.LocalPlayer?.CurrentCp >= 7 ? CraftAction.Observe : CraftAction.BasicSynthesis);
        _throttleAction = DateTime.Now.AddSeconds(0.5);
    }
}
