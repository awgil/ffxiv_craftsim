using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace craftsim;

class TransitionDB
{
    private Dictionary<string, TransitionTable> _transitions = new();
    private TransitionTable? _active;
    private string _activeName = "";
    private string _newName = "";

    public TransitionDB()
    {
        LoadFromJSON(Service.Config.TransitionDB);
    }

    public void Update()
    {
        _active?.Update();
    }

    public void Draw()
    {
        DrawCombo();
        DrawRenameNew();
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
        _transitions.Clear();
        foreach (var (k, v) in json)
        {
            var varr = v as JsonArray;
            if (varr != null)
                _transitions[k] = new TransitionTable(varr);
        }
        _active?.Deactivate();
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
                _active?.Deactivate();
                _active = null;
                _activeName = "";
            }
            foreach (var (k, v) in _transitions)
            {
                if (ImGui.Selectable(k, _active == v))
                {
                    _active?.Deactivate();
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
                _active?.Deactivate();
                _active = new();
                _activeName = _newName;
                _transitions[_newName] = _active;
            }
        }
    }
}
