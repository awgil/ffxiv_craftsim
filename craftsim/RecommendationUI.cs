using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Text;

namespace craftsim;

public class RecommendationUI : IDisposable
{
    private Simulator _sim = new(new(), 0);
    private Solver _solver = new();
    private Recipe? _curRecipe;
    private CraftAction _lastAction;

    private unsafe delegate bool UseActionDelegate(ActionManager* self, ActionType actionType, uint actionID, ulong targetID, uint itemLocation, uint callType, uint comboRouteID, bool* outOptGTModeStarted);
    private Hook<UseActionDelegate> _useActionHook;

    public unsafe RecommendationUI()
    {
        _useActionHook = Service.Hook.HookFromSignature<UseActionDelegate>("E8 ?? ?? ?? ?? EB 64 B1 01", UseActionDetour);
        _useActionHook.Enable();
    }

    public void Dispose()
    {
        _useActionHook.Dispose();
    }

    public void Draw()
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
        ImGui.TextUnformatted($"Recommendation: {_solver.SolveNextStep(_sim)}");
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

        var itemID = addon->AtkUnitBase.AtkValues[16].UInt;
        if (_curRecipe?.ItemResult.Row != itemID)
        {
            _curRecipe = Service.LuminaGameData.GetExcelSheet<Recipe>()?.FirstOrDefault(r => r.ItemResult.Row == itemID && r.CraftType.Row == player.ClassJob.Id - 8);
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

    private unsafe bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionID, ulong targetID, uint itemLocation, uint callType, uint comboRouteID, bool* outOptGTModeStarted)
    {
        _lastAction = (actionType, actionID) switch
        {
            (ActionType.CraftAction, 100001 or 100015 or 100030 or 100075 or 100045 or 100060 or 100090 or 100105) => CraftAction.BasicSynthesis,
            (ActionType.CraftAction, 100203 or 100204 or 100205 or 100206 or 100207 or 100208 or 100209 or 100210) => CraftAction.CarefulSynthesis,
            (ActionType.CraftAction, 100363 or 100364 or 100365 or 100366 or 100367 or 100368 or 100369 or 100370) => CraftAction.RapidSynthesis,
            (ActionType.CraftAction, 100235 or 100236 or 100237 or 100238 or 100239 or 100240 or 100241 or 100242) => CraftAction.FocusedSynthesis,
            (ActionType.CraftAction, 100403 or 100404 or 100405 or 100406 or 100407 or 100408 or 100409 or 100410) => CraftAction.Groundwork,
            (ActionType.CraftAction, 100315 or 100316 or 100317 or 100318 or 100319 or 100320 or 100321 or 100322) => CraftAction.IntensiveSynthesis,
            (ActionType.CraftAction, 100427 or 100428 or 100429 or 100430 or 100431 or 100432 or 100433 or 100434) => CraftAction.PrudentSynthesis,
            (ActionType.CraftAction, 100379 or 100380 or 100381 or 100382 or 100383 or 100384 or 100385 or 100386) => CraftAction.MuscleMemory,
            (ActionType.CraftAction, 100002 or 100016 or 100031 or 100076 or 100046 or 100061 or 100091 or 100106) => CraftAction.BasicTouch,
            (ActionType.CraftAction, 100004 or 100018 or 100034 or 100078 or 100048 or 100064 or 100093 or 100109) => CraftAction.StandardTouch,
            (ActionType.CraftAction, 100411 or 100412 or 100413 or 100414 or 100415 or 100416 or 100417 or 100418) => CraftAction.AdvancedTouch,
            (ActionType.CraftAction, 100355 or 100356 or 100357 or 100358 or 100359 or 100360 or 100361 or 100362) => CraftAction.HastyTouch,
            (ActionType.CraftAction, 100243 or 100244 or 100245 or 100246 or 100247 or 100248 or 100249 or 100250) => CraftAction.FocusedTouch,
            (ActionType.CraftAction, 100299 or 100300 or 100301 or 100302 or 100303 or 100304 or 100305 or 100306) => CraftAction.PreparatoryTouch,
            (ActionType.CraftAction, 100128 or 100129 or 100130 or 100131 or 100132 or 100133 or 100134 or 100135) => CraftAction.PreciseTouch,
            (ActionType.CraftAction, 100227 or 100228 or 100229 or 100230 or 100231 or 100232 or 100233 or 100234) => CraftAction.PrudentTouch,
            (ActionType.CraftAction, 100435 or 100436 or 100437 or 100438 or 100439 or 100440 or 100441 or 100442) => CraftAction.TrainedFinnesse,
            (ActionType.CraftAction, 100387 or 100388 or 100389 or 100390 or 100391 or 100392 or 100393 or 100394) => CraftAction.Reflect,
            (ActionType.CraftAction, 100339 or 100340 or 100341 or 100342 or 100343 or 100344 or 100345 or 100346) => CraftAction.ByregotBlessing,
            (ActionType.CraftAction, 100283 or 100284 or 100285 or 100286 or 100287 or 100288 or 100289 or 100290) => CraftAction.TrainedEye,
            (ActionType.CraftAction, 100323 or 100324 or 100325 or 100326 or 100327 or 100328 or 100329 or 100330) => CraftAction.DelicateSynthesis,
            (ActionType.Action, 19297 or 19298 or 19299 or 19300 or 19301 or 19302 or 19303 or 19304) => CraftAction.Veneration,
            (ActionType.Action, 19004 or 19005 or 19006 or 19007 or 19008 or 19009 or 19010 or 19011) => CraftAction.Innovation,
            (ActionType.Action, 260 or 261 or 262 or 263 or 264 or 265 or 266 or 267) => CraftAction.GreatStrides,
            (ActionType.CraftAction, 100371 or 100372 or 100373 or 100374 or 100375 or 100376 or 100377 or 100378) => CraftAction.TricksOfTrade,
            (ActionType.CraftAction, 100003 or 100017 or 100032 or 100047 or 100062 or 100077 or 100092 or 100107) => CraftAction.MastersMend,
            (ActionType.Action, 4574 or 4575 or 4576 or 4577 or 4578 or 4579 or 4580 or 4581) => CraftAction.Manipulation,
            (ActionType.Action, 4631 or 4632 or 4633 or 4634 or 4635 or 4636 or 4637 or 4638) => CraftAction.WasteNot,
            (ActionType.Action, 4639 or 4640 or 4641 or 4642 or 4643 or 4644 or 19002 or 19003) => CraftAction.WasteNot2,
            (ActionType.CraftAction, 100010 or 100023 or 100040 or 100053 or 100070 or 100082 or 100099 or 100113) => CraftAction.Observe,
            (ActionType.CraftAction, 100395 or 100396 or 100397 or 100398 or 100399 or 100400 or 100401 or 100402) => CraftAction.CarefulObservation,
            (ActionType.Action, 19012 or 19013 or 19014 or 19015 or 19016 or 19017 or 19018 or 19019) => CraftAction.FinalAppraisal,
            (ActionType.CraftAction, 100419 or 100420 or 100421 or 100422 or 100423 or 100424 or 100425 or 100426) => CraftAction.HeartAndSoul,
            _ => CraftAction.None
        };
        return _useActionHook.Original(self, actionType, actionID, targetID, itemLocation, callType, comboRouteID, outOptGTModeStarted);
    }
}
