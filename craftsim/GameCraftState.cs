using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace craftsim;

public unsafe class GameCraftState : IDisposable
{
    public uint ClassId { get; private set; }
    public Lumina.Excel.GeneratedSheets.Recipe? CurRecipe { get; private set; }
    public CraftState? CurState { get; private set; }
    public StepState? CurStep { get; private set; }
    public CraftAction LastAction { get; private set; }

    private Dictionary<uint, CraftAction> _idToAction = new();
    private uint[,] _actionIds = new uint[(int)CraftAction.Count, 8];

    private delegate bool UseActionDelegate(ActionManager* self, ActionType actionType, uint actionID, ulong targetID, uint itemLocation, uint callType, uint comboRouteID, bool* outOptGTModeStarted);
    private Hook<UseActionDelegate> _useActionHook;

    public GameCraftState()
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

    public void Update()
    {
        var player = Service.ClientState.LocalPlayer;
        var addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis");
        if (player == null || addon == null || addon->AtkUnitBase.AtkValuesCount <= 25 || addon->ItemName == null)
        {
            ClassId = player != null ? player.ClassJob.Id - 8 : 8;
            CurRecipe = null;
            CurState = null;
            CurStep = null;
            LastAction = CraftAction.None;
            return;
        }

        var curStep = addon->AtkUnitBase.AtkValues[15].Int;
        var cond = (CraftCondition)addon->AtkUnitBase.AtkValues[12].Int;
        if (curStep < 1 || cond >= CraftCondition.Count)
        {
            ClassId = player != null ? player.ClassJob.Id - 8 : 8;
            CurRecipe = null;
            CurState = null;
            CurStep = null;
            LastAction = CraftAction.None;
            return;
        }

        var classID = player.ClassJob.Id - 8;
        var itemID = addon->AtkUnitBase.AtkValues[16].UInt;
        if (CurRecipe?.ItemResult.Row != itemID || ClassId != classID)
        {
            ClassId = classID;
            CurRecipe = Service.LuminaGameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Recipe>()?.FirstOrDefault(r => r.ItemResult.Row == itemID && r.CraftType.Row == classID);
            var lt = CurRecipe?.RecipeLevelTable.Value;

            var weapon = Service.LuminaRow<Lumina.Excel.GeneratedSheets.Item>(InventoryManager.Instance()->GetInventorySlot(InventoryType.EquippedItems, 0)->ItemID);
            CurState = new();
            CurState.StatCraftsmanship = PlayerState.Instance()->Attributes[70];
            CurState.StatControl = PlayerState.Instance()->Attributes[71];
            CurState.StatCP = (int)player.MaxCp;
            CurState.StatLevel = player.Level;
            CurState.Specialist = InventoryManager.Instance()->GetInventorySlot(InventoryType.EquippedItems, 13)->ItemID != 0;
            CurState.Splendorous = weapon?.Description.ToString().Contains("are 1.75 times higher") ?? false; // TODO this is cursed
            CurState.CraftExpert = CurRecipe?.IsExpert ?? false;
            CurState.CraftLevel = lt?.ClassJobLevel ?? 0;
            CurState.CraftDurability = lt?.Durability * CurRecipe?.DurabilityFactor / 100 ?? 0; // atkvalue[8]
            CurState.CraftProgress = lt?.Difficulty * CurRecipe?.DifficultyFactor / 100 ?? 0; // atkvalue[6]
            CurState.CraftProgressDivider = lt?.ProgressDivider ?? 180;
            CurState.CraftProgressModifier = lt?.ProgressModifier ?? 100;
            CurState.CraftQualityDivider = lt?.QualityDivider ?? 180;
            CurState.CraftQualityModifier = lt?.QualityModifier ?? 180;
            CurState.CraftQualityMax = (int)(lt?.Quality * CurRecipe?.QualityFactor / 100 ?? 0); // atkvalue[17]
            if (addon->AtkUnitBase.AtkValues[22].UInt != 0)
            {
                CurState.CraftQualityMin1 = addon->AtkUnitBase.AtkValues[22].Int * 10;
                CurState.CraftQualityMin2 = addon->AtkUnitBase.AtkValues[23].Int * 10;
                CurState.CraftQualityMin3 = addon->AtkUnitBase.AtkValues[24].Int * 10;
            }
            else
            {
                CurState.CraftQualityMin1 = CurState.CraftQualityMin2 = addon->AtkUnitBase.AtkValues[18].Int;
                CurState.CraftQualityMin3 = CurState.CraftQualityMax;
            }
        }

        if (!Service.Condition[ConditionFlag.Crafting40])
        {
            CurStep ??= new();
            CurStep.Index = curStep;
            CurStep.Progress = addon->AtkUnitBase.AtkValues[5].Int;
            CurStep.Quality = addon->AtkUnitBase.AtkValues[9].Int;
            CurStep.Durability = addon->AtkUnitBase.AtkValues[7].Int;
            CurStep.RemainingCP = (int)player.CurrentCp;
            CurStep.Condition = cond;
            CurStep.IQStacks = GetStatusParam(player, 251);
            CurStep.WasteNotLeft = GetStatusParam(player, 257);
            if (CurStep.WasteNotLeft == 0)
                CurStep.WasteNotLeft = GetStatusParam(player, 252);
            CurStep.ManipulationLeft = GetStatusParam(player, 1164);
            CurStep.GreatStridesLeft = GetStatusParam(player, 254);
            CurStep.InnovationLeft = GetStatusParam(player, 2189);
            CurStep.VenerationLeft = GetStatusParam(player, 2226);
            CurStep.MuscleMemoryLeft = GetStatusParam(player, 2191);
            CurStep.FinalAppraisalLeft = GetStatusParam(player, 2190);
            CurStep.CarefulObservationLeft = ActionManager.Instance()->GetActionStatus(ActionType.CraftAction, GetActionID(CraftAction.CarefulObservation)) == 0 ? 1 : 0;
            CurStep.HeartAndSoulActive = GetStatusParam(player, 2665) > 0;
            CurStep.HeartAndSoulAvailable = ActionManager.Instance()->GetActionStatus(ActionType.CraftAction, GetActionID(CraftAction.HeartAndSoul)) == 0;
            CurStep.PrevComboAction = LastAction;
        }
    }

    public void UseAction(CraftAction action)
    {
        var id = GetActionID(action);
        if (id != 0)
            ActionManager.Instance()->UseAction(id > 100000 ? ActionType.CraftAction : ActionType.Action, id);
    }

    private uint GetActionID(CraftAction action) => ClassId < 8 ? _actionIds[(int)action, ClassId] : 0;

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

    private bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionID, ulong targetID, uint itemLocation, uint callType, uint comboRouteID, bool* outOptGTModeStarted)
    {
        LastAction = actionType switch
        {
            ActionType.Action => actionID < 100000 ? _idToAction.GetValueOrDefault(actionID) : CraftAction.None,
            ActionType.CraftAction => actionID > 100000 ? _idToAction.GetValueOrDefault(actionID) : CraftAction.None,
            _ => CraftAction.None
        };
        return _useActionHook.Original(self, actionType, actionID, targetID, itemLocation, callType, comboRouteID, outOptGTModeStarted);
    }
}
