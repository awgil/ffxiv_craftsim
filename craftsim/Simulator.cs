using System;
using System.Linq;

namespace craftsim;

public enum CraftStatus
{
    InProgress,
    FailedDurability,
    FailedMinQuality,
    SucceededQ1,
    SucceededQ2,
    SucceededQ3,

    Count
}

public enum ForcedResult
{
    Random,
    ForceSuccess,
    ForceFail
}

public enum ExecuteResult
{
    CantUse,
    Failed,
    Succeeded
}

public class Simulator
{
    public CraftState Craft;
    public Random Rng;

    public Simulator(CraftState craft, int seed)
    {
        Craft = craft;
        Rng = new(seed);
    }

    public StepState CreateInitial() => new() { Index = 1, Durability = Craft.CraftDurability, RemainingCP = Craft.StatCP, CarefulObservationLeft = Craft.Specialist ? 3 : 0, HeartAndSoulAvailable = Craft.Specialist, ActionSuccessRoll = Rng.NextDouble(), NextStateRoll = Rng.NextDouble() };

    public CraftStatus Status(StepState step)
    {
        return step.Progress < Craft.CraftProgress
            ? (step.Durability > 0 ? CraftStatus.InProgress : CraftStatus.FailedDurability)
            : (step.Quality < Craft.CraftQualityMin1 ? CraftStatus.FailedMinQuality : step.Quality < Craft.CraftQualityMin2 ? CraftStatus.SucceededQ1 : step.Quality < Craft.CraftQualityMin3 ? CraftStatus.SucceededQ2 : CraftStatus.SucceededQ3);
    }

    public (ExecuteResult, StepState) Execute(StepState step, CraftAction action, ForcedResult result = ForcedResult.Random)
    {
        if (Status(step) != CraftStatus.InProgress)
            return (ExecuteResult.CantUse, step); // can't execute action on craft that is not in progress

        var success = result switch
        {
            ForcedResult.ForceSuccess => true,
            ForcedResult.ForceFail => false,
            _ => step.ActionSuccessRoll < GetSuccessRate(step, action)
        };

        // TODO: check level requirements
        if (!CanUseAction(step, action))
            return (ExecuteResult.CantUse, step); // can't use action because of special conditions

        var next = new StepState();
        next.Index = SkipUpdates(action) ? step.Index : step.Index + 1;
        next.Progress = step.Progress + (success ? CalculateProgress(step, action) : 0);
        next.Quality = step.Quality + (success ? CalculateQuality(step, action) : 0);
        next.IQStacks = step.IQStacks;
        if (success)
        {
            if (next.Quality != step.Quality)
                ++next.IQStacks;
            if (action is CraftAction.PreciseTouch or CraftAction.PreparatoryTouch or CraftAction.Reflect)
                ++next.IQStacks;
            if (next.IQStacks > 10)
                next.IQStacks = 10;
            if (action == CraftAction.ByregotBlessing)
                next.IQStacks = 0;
        }

        next.WasteNotLeft = action switch
        {
            CraftAction.WasteNot => GetNewBuffDuration(step, 4),
            CraftAction.WasteNot2 => GetNewBuffDuration(step, 8),
            _ => GetOldBuffDuration(step.WasteNotLeft, action)
        };
        next.ManipulationLeft = action == CraftAction.Manipulation ? GetNewBuffDuration(step, 8) : GetOldBuffDuration(step.ManipulationLeft, action);
        next.GreatStridesLeft = action == CraftAction.GreatStrides ? GetNewBuffDuration(step, 3) : GetOldBuffDuration(step.GreatStridesLeft, action, next.Quality != step.Quality);
        next.InnovationLeft = action == CraftAction.Innovation ? GetNewBuffDuration(step, 4) : GetOldBuffDuration(step.InnovationLeft, action);
        next.VenerationLeft = action == CraftAction.Veneration ? GetNewBuffDuration(step, 4) : GetOldBuffDuration(step.VenerationLeft, action);
        next.MuscleMemoryLeft = action == CraftAction.MuscleMemory ? GetNewBuffDuration(step, 5) : GetOldBuffDuration(step.MuscleMemoryLeft, action, next.Progress != step.Progress);
        next.FinalAppraisalLeft = action == CraftAction.FinalAppraisal ? GetNewBuffDuration(step, 5) : GetOldBuffDuration(step.FinalAppraisalLeft, action, next.Progress >= Craft.CraftProgress);
        next.CarefulObservationLeft = step.CarefulObservationLeft - (action == CraftAction.CarefulObservation ? 1 : 0);
        next.HeartAndSoulActive = action == CraftAction.HeartAndSoul || step.HeartAndSoulActive && (step.Condition is CraftCondition.Good or CraftCondition.Excellent || !ConsumeHeartAndSoul(action));
        next.HeartAndSoulAvailable = step.HeartAndSoulAvailable && action != CraftAction.HeartAndSoul;
        next.PrevComboAction = action; // note: even stuff like final appraisal and h&s break combos

        if (step.FinalAppraisalLeft > 0 && next.Progress >= Craft.CraftProgress)
            next.Progress = Craft.CraftProgress - 1;
        if (action == CraftAction.TrainedEye)
            next.Quality = Craft.CraftQualityMax;

        next.RemainingCP = step.RemainingCP - GetCPCost(step, action);
        if (next.RemainingCP < 0)
            return (ExecuteResult.CantUse, step); // can't use action because of insufficient cp
        if (action == CraftAction.TricksOfTrade) // can't fail
            next.RemainingCP = Math.Min(Craft.StatCP, next.RemainingCP + 20);

        // assume these can't fail
        next.Durability = step.Durability - GetDurabilityCost(step, action);
        if (next.Durability > 0)
        {
            int repair = 0;
            if (action == CraftAction.MastersMend)
                repair += 30;
            if (step.ManipulationLeft > 0 && !SkipUpdates(action))
                repair += 5;
            next.Durability = Math.Min(Craft.CraftDurability, next.Durability + repair);
        }

        next.Condition = action is CraftAction.FinalAppraisal or CraftAction.HeartAndSoul ? step.Condition : GetNextCondition(step);
        next.ActionSuccessRoll = Rng.NextDouble();
        next.NextStateRoll = Rng.NextDouble();

        return (success ? ExecuteResult.Succeeded : ExecuteResult.Failed, next);
    }

    public int BaseProgress()
    {
        int res = Craft.StatCraftsmanship * 10 / Craft.CraftProgressDivider + 2;
        if (Craft.StatLevel <= Craft.CraftLevel) // TODO: verify this condition, teamcraft uses 'rlvl' here
            res = res * Craft.CraftProgressModifier / 100;
        return res;
    }

    public int BaseQuality()
    {
        int res = Craft.StatControl * 10 / Craft.CraftQualityDivider + 35;
        if (Craft.StatLevel <= Craft.CraftLevel) // TODO: verify this condition, teamcraft uses 'rlvl' here
            res = res * Craft.CraftQualityModifier / 100;
        return res;
    }

    public bool CanUseAction(StepState step, CraftAction action) => action switch
    {
        CraftAction.IntensiveSynthesis or CraftAction.PreciseTouch or CraftAction.TricksOfTrade => step.Condition is CraftCondition.Good or CraftCondition.Excellent || step.HeartAndSoulActive,
        CraftAction.PrudentSynthesis or CraftAction.PrudentTouch => step.WasteNotLeft == 0,
        CraftAction.MuscleMemory or CraftAction.Reflect => step.Index == 1,
        CraftAction.TrainedFinnesse => step.IQStacks == 10,
        CraftAction.ByregotBlessing => step.IQStacks > 0,
        CraftAction.TrainedEye => !Craft.CraftExpert && Craft.StatLevel >= Craft.CraftLevel + 10 && step.Index == 1,
        CraftAction.CarefulObservation => step.CarefulObservationLeft > 0,
        CraftAction.HeartAndSoul => step.HeartAndSoulAvailable,
        _ => true
    };

    public bool SkipUpdates(CraftAction action) => action is CraftAction.CarefulObservation or CraftAction.FinalAppraisal or CraftAction.HeartAndSoul;
    public bool ConsumeHeartAndSoul(CraftAction action) => action is CraftAction.IntensiveSynthesis or CraftAction.PreciseTouch or CraftAction.TricksOfTrade;

    public double GetSuccessRate(StepState step, CraftAction action)
    {
        var rate = action switch
        {
            CraftAction.FocusedSynthesis or CraftAction.FocusedTouch => step.PrevComboAction == CraftAction.Observe ? 1.0 : 0.5,
            CraftAction.RapidSynthesis => 0.5,
            CraftAction.HastyTouch => 0.6,
            _ => 1.0
        };
        if (step.Condition == CraftCondition.Centered)
            rate += 0.25;
        return rate;
    }

    public int GetCPCost(StepState step, CraftAction action)
    {
        var cost = action switch
        {
            CraftAction.CarefulSynthesis => 7,
            CraftAction.FocusedSynthesis => 5,
            CraftAction.Groundwork => 18,
            CraftAction.IntensiveSynthesis => 6,
            CraftAction.PrudentSynthesis => 18,
            CraftAction.MuscleMemory => 6,
            CraftAction.BasicTouch => 18,
            CraftAction.StandardTouch => step.PrevComboAction == CraftAction.BasicTouch ? 18 : 32,
            CraftAction.AdvancedTouch => step.PrevComboAction == CraftAction.StandardTouch ? 18 : 46,
            CraftAction.FocusedTouch => 18,
            CraftAction.PreparatoryTouch => 40,
            CraftAction.PreciseTouch => 18,
            CraftAction.PrudentTouch => 25,
            CraftAction.TrainedFinnesse => 32,
            CraftAction.Reflect => 6,
            CraftAction.ByregotBlessing => 24,
            CraftAction.TrainedEye => 250,
            CraftAction.DelicateSynthesis => 32,
            CraftAction.Veneration => 18,
            CraftAction.Innovation => 18,
            CraftAction.GreatStrides => 32,
            CraftAction.MastersMend => 88,
            CraftAction.Manipulation => 96,
            CraftAction.WasteNot => 56,
            CraftAction.WasteNot2 => 98,
            CraftAction.Observe => 7,
            CraftAction.FinalAppraisal => 1,
            _ => 0
        };
        if (step.Condition == CraftCondition.Pliant)
            cost -= cost / 2; // round up
        return cost;
    }

    public int GetDurabilityCost(StepState step, CraftAction action)
    {
        var cost = action switch
        {
            CraftAction.BasicSynthesis or CraftAction.CarefulSynthesis or CraftAction.RapidSynthesis or CraftAction.FocusedSynthesis or CraftAction.IntensiveSynthesis or CraftAction.MuscleMemory => 10,
            CraftAction.BasicTouch or CraftAction.StandardTouch or CraftAction.AdvancedTouch or CraftAction.HastyTouch or CraftAction.FocusedTouch or CraftAction.PreciseTouch or CraftAction.Reflect => 10,
            CraftAction.ByregotBlessing or CraftAction.DelicateSynthesis => 10,
            CraftAction.Groundwork or CraftAction.PreparatoryTouch => 20,
            CraftAction.PrudentSynthesis or CraftAction.PrudentTouch => 5,
            _ => 0
        };
        if (step.WasteNotLeft > 0)
            cost -= cost / 2; // round up
        if (step.Condition == CraftCondition.Sturdy)
            cost -= cost / 2; // round up
        return cost;
    }

    public int GetNewBuffDuration(StepState step, int baseDuration) => baseDuration + (step.Condition == CraftCondition.Primed ? 2 : 0);
    public int GetOldBuffDuration(int prevDuration, CraftAction action, bool consume = false) => consume || prevDuration == 0 ? 0 : SkipUpdates(action) ? prevDuration : prevDuration - 1;

    public int CalculateProgress(StepState step, CraftAction action)
    {
        int potency = action switch
        {
            CraftAction.BasicSynthesis => Craft.StatLevel >= 31 ? 120 : 100,
            CraftAction.CarefulSynthesis => Craft.StatLevel >= 82 ? 180 : 150,
            CraftAction.RapidSynthesis => Craft.StatLevel >= 63 ? 500 : 250,
            CraftAction.FocusedSynthesis => 200,
            CraftAction.Groundwork => step.Durability >= GetDurabilityCost(step, action) ? (Craft.StatLevel >= 86 ? 360 : 300) : (Craft.StatLevel >= 86 ? 180 : 150),
            CraftAction.IntensiveSynthesis => 400,
            CraftAction.PrudentSynthesis => 180,
            CraftAction.MuscleMemory => 300,
            CraftAction.DelicateSynthesis => 100,
            _ => 0
        };
        if (potency == 0)
            return 0;

        float buffMod = 1 + (step.MuscleMemoryLeft > 0 ? 1 : 0) + (step.VenerationLeft > 0 ? 0.5f : 0);
        float effPotency = potency * buffMod;

        float condMod = step.Condition == CraftCondition.Malleable ? 1.5f : 1;
        return (int)(BaseProgress() * condMod * effPotency / 100);
    }

    public int CalculateQuality(StepState step, CraftAction action)
    {
        int potency = action switch
        {
            CraftAction.BasicTouch => 100,
            CraftAction.StandardTouch => 125,
            CraftAction.AdvancedTouch => 150,
            CraftAction.HastyTouch => 100,
            CraftAction.FocusedTouch => 150,
            CraftAction.PreparatoryTouch => 200,
            CraftAction.PreciseTouch => 150,
            CraftAction.PrudentTouch => 100,
            CraftAction.TrainedFinnesse => 100,
            CraftAction.Reflect => 100,
            CraftAction.ByregotBlessing => 100 + 20 * step.IQStacks,
            CraftAction.DelicateSynthesis => 100,
            _ => 0
        };
        if (potency == 0)
            return 0;

        float buffMod = (1 + 0.1f * step.IQStacks) * (1 + (step.GreatStridesLeft > 0 ? 1 : 0) + (step.InnovationLeft > 0 ? 0.5f : 0));
        float effPotency = potency * buffMod;

        float condMod = step.Condition switch
        {
            CraftCondition.Good => Craft.Splendorous ? 1.75f : 1.5f,
            CraftCondition.Excellent => 4,
            CraftCondition.Poor => 0.5f,
            _ => 1
        };
        return (int)(BaseQuality() * condMod * effPotency / 100);
    }

    public bool WillFinishCraft(StepState step, CraftAction action) => step.FinalAppraisalLeft == 0 && step.Progress + CalculateProgress(step, action) >= Craft.CraftProgress;

    public CraftAction NextTouchCombo(StepState step) => step.PrevComboAction switch
    {
        CraftAction.BasicTouch => CraftAction.StandardTouch,
        CraftAction.StandardTouch => CraftAction.AdvancedTouch,
        _ => CraftAction.BasicTouch
    };

    public CraftCondition GetNextCondition(StepState step) => step.Condition switch
    {
        CraftCondition.Normal => GetTransitionByRoll(step),
        CraftCondition.Good => Craft.CraftExpert ? GetTransitionByRoll(step) : CraftCondition.Normal,
        CraftCondition.Excellent => CraftCondition.Poor,
        CraftCondition.Poor => CraftCondition.Normal,
        CraftCondition.GoodOmen => CraftCondition.Good,
        _ => GetTransitionByRoll(step)
    };

    public CraftCondition GetTransitionByRoll(StepState step)
    {
        double roll = step.NextStateRoll;
        for (int i = 1; i < Craft.CraftConditionProbabilities.Length; ++i)
        {
            roll -= Craft.CraftConditionProbabilities[i];
            if (roll < 0)
                return (CraftCondition)i;
        }
        return CraftCondition.Normal;
    }
}
