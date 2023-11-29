using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace craftsim;

public class RecommendationUI : IDisposable
{
    private Simulator _sim = new(new(), 0);
    private Solver _solver = new();
    private Recipe? _curRecipe;
    private CraftAction _lastAction;
    private uint _classId;

    private Dictionary<uint, CraftAction> _idToAction = new();
    private uint[,] _actionIds = new uint[(int)CraftAction.Count, 8];

    private unsafe delegate bool UseActionDelegate(ActionManager* self, ActionType actionType, uint actionID, ulong targetID, uint itemLocation, uint callType, uint comboRouteID, bool* outOptGTModeStarted);
    private Hook<UseActionDelegate> _useActionHook;

    public unsafe RecommendationUI()
    {
        AssignActionID(CraftAction.BasicSynthesis, [100001, 100015, 100030, 100075, 100045, 100060, 100090, 100105]);
        AssignActionID(CraftAction.CarefulSynthesis, [100203, 100204, 100205, 100206, 100207, 100208, 100209, 100210]);
        AssignActionID(CraftAction.RapidSynthesis, [100363, 100364, 100365, 100366, 100367, 100368, 100369, 100370]);
        AssignActionID(CraftAction.FocusedSynthesis, [100235, 100236, 100237, 100238, 100239, 100240, 100241, 100242]);
        AssignActionID(CraftAction.Groundwork, [100403, 100404, 100405, 100406, 100407, 100408, 100409, 100410]);
        AssignActionID(CraftAction.IntensiveSynthesis, [100315, 100316, 100317, 100318, 100319, 100320, 100321, 100322]);
        AssignActionID(CraftAction.PrudentSynthesis, [100427, 100428, 100429, 100430, 100431, 100432, 100433, 100434]);
        AssignActionID(CraftAction.MuscleMemory, [100379, 100380, 100381, 100382, 100383, 100384, 100385, 100386]);
        AssignActionID(CraftAction.BasicTouch, [100002, 100016, 100031, 100076, 100046, 100061, 100091, 100106]);
        AssignActionID(CraftAction.StandardTouch, [100004, 100018, 100034, 100078, 100048, 100064, 100093, 100109]);
        AssignActionID(CraftAction.AdvancedTouch, [100411, 100412, 100413, 100414, 100415, 100416, 100417, 100418]);
        AssignActionID(CraftAction.HastyTouch, [100355, 100356, 100357, 100358, 100359, 100360, 100361, 100362]);
        AssignActionID(CraftAction.FocusedTouch, [100243, 100244, 100245, 100246, 100247, 100248, 100249, 100250]);
        AssignActionID(CraftAction.PreparatoryTouch, [100299, 100300, 100301, 100302, 100303, 100304, 100305, 100306]);
        AssignActionID(CraftAction.PreciseTouch, [100128, 100129, 100130, 100131, 100132, 100133, 100134, 100135]);
        AssignActionID(CraftAction.PrudentTouch, [100227, 100228, 100229, 100230, 100231, 100232, 100233, 100234]);
        AssignActionID(CraftAction.TrainedFinnesse, [100435, 100436, 100437, 100438, 100439, 100440, 100441, 100442]);
        AssignActionID(CraftAction.Reflect, [100387, 100388, 100389, 100390, 100391, 100392, 100393, 100394]);
        AssignActionID(CraftAction.ByregotBlessing, [100339, 100340, 100341, 100342, 100343, 100344, 100345, 100346]);
        AssignActionID(CraftAction.TrainedEye, [100283, 100284, 100285, 100286, 100287, 100288, 100289, 100290]);
        AssignActionID(CraftAction.DelicateSynthesis, [100323, 100324, 100325, 100326, 100327, 100328, 100329, 100330]);
        AssignActionID(CraftAction.Veneration, [19297, 19298, 19299, 19300, 19301, 19302, 19303, 19304]);
        AssignActionID(CraftAction.Innovation, [19004, 19005, 19006, 19007, 19008, 19009, 19010, 19011]);
        AssignActionID(CraftAction.GreatStrides, [260, 261, 262, 263, 264, 265, 266, 267]);
        AssignActionID(CraftAction.TricksOfTrade, [100371, 100372, 100373, 100374, 100375, 100376, 100377, 100378]);
        AssignActionID(CraftAction.MastersMend, [100003, 100017, 100032, 100047, 100062, 100077, 100092, 100107]);
        AssignActionID(CraftAction.Manipulation, [4574, 4575, 4576, 4577, 4578, 4579, 4580, 4581]);
        AssignActionID(CraftAction.WasteNot, [4631, 4632, 4633, 4634, 4635, 4636, 4637, 4638]);
        AssignActionID(CraftAction.WasteNot2, [4639, 4640, 4641, 4642, 4643, 4644, 19002, 19003]);
        AssignActionID(CraftAction.Observe, [100010, 100023, 100040, 100053, 100070, 100082, 100099, 100113]);
        AssignActionID(CraftAction.CarefulObservation, [100395, 100396, 100397, 100398, 100399, 100400, 100401, 100402]);
        AssignActionID(CraftAction.FinalAppraisal, [19012, 19013, 19014, 19015, 19016, 19017, 19018, 19019]);
        AssignActionID(CraftAction.HeartAndSoul, [100419, 100420, 100421, 100422, 100423, 100424, 100425, 100426]);

        _useActionHook = Service.Hook.HookFromSignature<UseActionDelegate>("E8 ?? ?? ?? ?? EB 64 B1 01", UseActionDetour);
        _useActionHook.Enable();
    }

    public void Dispose()
    {
        _useActionHook.Dispose();
    }

    public unsafe void Draw()
    {
        if (!Refresh())
        {
            ImGui.TextUnformatted("Craft not running!");
            return;
        }

        ImGui.TextUnformatted($"Stats: {_sim.Craft.StatCraftsmanship}/{_sim.Craft.StatControl}/{_sim.Craft.StatCP} (L{_sim.Craft.StatLevel}){(_sim.Craft.Specialist ? " (spec)" : "")}{(_sim.Craft.Splendorous ? " (splend)" : "")}");
        ImGui.TextUnformatted($"Craft: L{_sim.Craft.CraftLevel}{(_sim.Craft.CraftExpert ? "X" : "")} {_sim.Craft.CraftDurability}dur, {_sim.Craft.CraftProgress}p, {_sim.Craft.CraftQualityMax}q ({_sim.Craft.CraftQualityMin1}/{_sim.Craft.CraftQualityMin2}/{_sim.Craft.CraftQualityMin3})");
        ImGui.TextUnformatted($"Base progress: {_sim.BaseProgress()} ({_sim.Craft.CraftProgress * 100.0 / _sim.BaseProgress():f2}p), base quality: {_sim.BaseQuality()} ({_sim.Craft.CraftQualityMax * 100.0 / _sim.BaseQuality():f2}p)");
        ImGui.Separator();

        var step = _sim.Steps.Last();
        DrawProgress("Progress", step.Progress, _sim.Craft.CraftProgress);
        DrawProgress("Quality", step.Quality, _sim.Craft.CraftQualityMax);
        DrawProgress("Durability", step.Durability, _sim.Craft.CraftDurability);
        DrawProgress("CP", step.RemainingCP, _sim.Craft.StatCP);

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

        ImGui.Separator();
        var rec = _solver.SolveNextStep(_sim);
        if (ImGui.Button($"Recommendation: {rec}") && _classId < 8)
        {
            var id = _actionIds[(int)rec, _classId];
            if (id != 0)
            {
                ActionManager.Instance()->UseAction(id > 100000 ? ActionType.CraftAction : ActionType.Action, id);
            }
        }
    }

    private void DrawProgress(string label, int value, int max)
    {
        ImGui.ProgressBar((float)value / max, new(100, 0), $"{value * 100.0 / max:f2}% ({value}/{max})");
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
    }

    private void AddBuff(StringBuilder sb, string mnem, int left)
    {
        if (left > 0)
            sb.Append($" {mnem}:{left}");
    }

    private unsafe bool Refresh()
    {
        var addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis");
        if (addon == null || addon->AtkUnitBase.AtkValuesCount <= 25 || addon->ItemName == null)
            return false;

        var curStep = addon->AtkUnitBase.AtkValues[15].UInt;
        if (curStep < 1)
            return false;
        var cond = (CraftCondition)addon->AtkUnitBase.AtkValues[12].Int;
        if (cond >= CraftCondition.Count)
            return false;

        var player = Service.ClientState.LocalPlayer;
        if (player == null)
            return false;

        var classID = player.ClassJob.Id - 8;
        var itemID = addon->AtkUnitBase.AtkValues[16].UInt;
        if (_curRecipe?.ItemResult.Row != itemID || _classId != classID)
        {
            _classId = classID;
            _curRecipe = Service.LuminaGameData.GetExcelSheet<Recipe>()?.FirstOrDefault(r => r.ItemResult.Row == itemID && r.CraftType.Row == classID);
            var lt = _curRecipe?.RecipeLevelTable.Value;

            var weapon = Service.LuminaRow<Item>(InventoryManager.Instance()->GetInventorySlot(InventoryType.EquippedItems, 0)->ItemID);
            _sim.Craft.StatCraftsmanship = PlayerState.Instance()->Attributes[70];
            _sim.Craft.StatControl = PlayerState.Instance()->Attributes[71];
            _sim.Craft.StatCP = (int)player.MaxCp;
            _sim.Craft.StatLevel = player.Level;
            _sim.Craft.Specialist = InventoryManager.Instance()->GetInventorySlot(InventoryType.EquippedItems, 13)->ItemID != 0;
            _sim.Craft.Splendorous = weapon?.Description.ToString().Contains("are 1.75 times higher") ?? false; // TODO this is cursed
            _sim.Craft.CraftExpert = _curRecipe?.IsExpert ?? false;
            _sim.Craft.CraftLevel = lt?.ClassJobLevel ?? 0;
            _sim.Craft.CraftDurability = lt?.Durability * _curRecipe?.DurabilityFactor / 100 ?? 0; // atkvalue[8]
            _sim.Craft.CraftProgress = lt?.Difficulty * _curRecipe?.DifficultyFactor / 100 ?? 0; // atkvalue[6]
            _sim.Craft.CraftProgressDivider = lt?.ProgressDivider ?? 180;
            _sim.Craft.CraftProgressModifier = lt?.ProgressModifier ?? 100;
            _sim.Craft.CraftQualityDivider = lt?.QualityDivider ?? 180;
            _sim.Craft.CraftQualityModifier = lt?.QualityModifier ?? 180;
            _sim.Craft.CraftQualityMax = (int)(lt?.Quality * _curRecipe?.QualityFactor / 100 ?? 0); // atkvalue[17]
            if (addon->AtkUnitBase.AtkValues[22].UInt != 0)
            {
                _sim.Craft.CraftQualityMin1 = addon->AtkUnitBase.AtkValues[22].Int * 10;
                _sim.Craft.CraftQualityMin2 = addon->AtkUnitBase.AtkValues[23].Int * 10;
                _sim.Craft.CraftQualityMin3 = addon->AtkUnitBase.AtkValues[24].Int * 10;
            }
            else
            {
                _sim.Craft.CraftQualityMin1 = _sim.Craft.CraftQualityMin2 = addon->AtkUnitBase.AtkValues[18].Int;
                _sim.Craft.CraftQualityMin3 = _sim.Craft.CraftQualityMax;
            }
        }

        if (curStep > 1 && _sim.Steps.Count == 1)
            _sim.Steps.Add(_sim.Steps.Last());
        else if (curStep == 1 && _sim.Steps.Count != 1)
            _sim.Steps.RemoveRange(1, _sim.Steps.Count - 1);

        var step = _sim.Steps.Last();
        step.Progress = addon->AtkUnitBase.AtkValues[5].Int;
        step.Quality = addon->AtkUnitBase.AtkValues[9].Int;
        step.Durability = addon->AtkUnitBase.AtkValues[7].Int;
        step.RemainingCP = (int)player.CurrentCp;
        step.Condition = cond;
        step.IQStacks = GetStatusParam(player, 251);
        step.WasteNotLeft = GetStatusParam(player, 257);
        if (step.WasteNotLeft == 0)
            step.WasteNotLeft = GetStatusParam(player, 252);
        step.ManipulationLeft = GetStatusParam(player, 1164);
        step.GreatStridesLeft = GetStatusParam(player, 254);
        step.InnovationLeft = GetStatusParam(player, 2189);
        step.VenerationLeft = GetStatusParam(player, 2226);
        step.MuscleMemoryLeft = GetStatusParam(player, 2191);
        step.FinalAppraisalLeft = GetStatusParam(player, 2190);
        step.CarefulObservationLeft = ActionManager.Instance()->GetActionStatus(ActionType.CraftAction, 100395) == 0 ? 1 : 0;
        step.HeartAndSoulActive = GetStatusParam(player, 2665) > 0;
        step.HeartAndSoulAvailable = ActionManager.Instance()->GetActionStatus(ActionType.CraftAction, 100419) == 0;
        step.PrevComboAction = _lastAction;
        return true;
    }

    private int GetStatusParam(PlayerCharacter pc, uint id) => pc.StatusList.FirstOrDefault(s => s.StatusId == id)?.Param ?? 0;

    private void AssignActionID(CraftAction action, uint[] ids)
    {
        foreach (var id in ids)
        {
            var classRow = id > 100000 ? Service.LuminaRow<Lumina.Excel.GeneratedSheets.CraftAction>(id)?.ClassJob : Service.LuminaRow<Lumina.Excel.GeneratedSheets.Action>(id)?.ClassJob;
            if (classRow == null)
                throw new Exception($"Failed to find definition for {action} {id}");
            var c = classRow.Row - 8;
            if (c >= 8)
                throw new Exception($"Unexpected class {classRow.Row} ({classRow.Value?.Abbreviation}) for {action} {id}");
            ref var entry = ref _actionIds[(int)action, c];
            if (entry != 0)
                throw new Exception($"Duplicate entry for {classRow.Value?.Abbreviation} {action}: {id} and {entry}");
            entry = id;
            _idToAction[id] = action;
        }
    }

    private unsafe bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionID, ulong targetID, uint itemLocation, uint callType, uint comboRouteID, bool* outOptGTModeStarted)
    {
        _lastAction = actionType switch
        {
            ActionType.Action => actionID < 100000 ? _idToAction.GetValueOrDefault(actionID) : CraftAction.None,
            ActionType.CraftAction => actionID > 100000 ? _idToAction.GetValueOrDefault(actionID) : CraftAction.None,
            _ => CraftAction.None
        };
        return _useActionHook.Original(self, actionType, actionID, targetID, itemLocation, callType, comboRouteID, outOptGTModeStarted);
    }
}
