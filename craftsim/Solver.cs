using ImGuiNET;
using System.Linq;

namespace craftsim;

// some thoughts:
// - any time we want to regain some dura, we can bait pliant and use manip
// - assuming 12% pliant, 3% good, 12% good omen, average cost to get pliant from random starting point is ~24cp
// - this means that estimated manip cost is 24+48 = 72, meaning 10dura is worth ~18cp
// - if we were to use full cost manip as a rule, it would be worth ~24cp instead - but we should rarely be doing that
// - if we start in some non-interesting state, we would need to pay ~31cp to get pliant (because we pay 7cp to start from 'random' state)
// repair actions:
// - if we get random pliant and manip is not active, using manip is effectively winning 24cp (because we don't need to bait), which is the baseline worth of pliant
// - using pliant mm is getting 30dura = 54cp for 44cp, meaning it's effective 10cp win
// - using pliant manip to clip 1 stack is getting 35dura = 63cp for 48cp, meaning it's effective 15cp win, making it better than mm
// - using pliant manip to clip 2 stacks is getting 30dura = 54cp for 48cp, meaning is's 8cp win compared to 10cp mm - which makes sense, as it's a strict loss
// assuming 60dura craft, the real dura sweet spot is exactly 25dura
// - any more and we can't use manip+mm on double pliant
// - any less and we might be forced to use observe and waste conditions
// - so general flow is - exploit buffs (mume/vene/inno), outside buffs get manip and get to sweet spot before starting next phase, exploit random procs for iq/progress if while waiting
// synth action comparison
// - basic synth is 120p for 18cp = 6.67p/cp - baseline to finish the craft
// - careful synth is 180p for 7+18cp = 7.2p/cp - slightly more efficient way to finish the craft
// - prudent synth is 180p for 18+9cp = 6.67p/cp -  alternative way to finish the craft, with different cp/durability cost spread
// - observe+focused synth is 200p for 7+5+18cp = 6.67p/cp - TODO consider, might be a good way to exploit malleable?.. good all around, too bad we have better actions
// - groundwork is 360p for 18+36cp = 6.67p/cp - very expensive durability wise, TODO consider for veneration/waste-not utilization?
// - rapid synth is 500p for 18cp with 50% success (75% under centered) = 13.89p/cp (20.83p/cp under centered) - this is extremely efficient and thus is our baseline
// - intensive synth is 400p for 6+18cp = 16.67p/cp (9.09p/cp if we assume good is worth 20cp) - very efficient, good alternative to rapid if gamble is undesired, uses up good condition
// touch action comparison
// - basic touch combo is 1iq + 100/125/150p for 18+18cp = 2.78/3.47/4.17p/cp - too restrictive for reacting to procs, too inefficient if broken, maybe useful under waste-not?..
// - hasty touch is 100p for 1iq + 18cp with 60% success (85% under centered) = 3.33p/cp (4.72p/cp under centered) - decent way to exploit centered/sturdy for iq
// - observe+focused touch is 1iq + 150p for 7+18+18cp = 3.49p/cp - this is a baseline for finisher phase, very good opportunity to react for procs
// - prep touch is 2iq + 200p for 40+36cp = 2.63p/cp - very expensive and not very efficient, but still the best quality action, so decent to exploit good/sturdy for quality if under gs+inno
// - precise touch is 2iq + 150p for 18+18cp = 4.17p/cp (2.67p/cp if we assume good is worth 20cp) - very efficient way of getting iq, and decent way to exploit good for quality under gs/inno
// - prudent touch is 1iq + 100p for 25+9cp = 2.94p/cp - very efficient way of getting iq, and a reasonable alternative for finisher when low on dura
// - finnesse is 100p for 32cp = 3.13p/cp - even better alternative for finisher when low on dura, quite expensive cp wise though
// general flow
// - if starting with mume, use buff for rapid/intensive, and potentially use vene for some more free progress
// -- one concern here is using too much dura here and being forced to bait for pliant and waste good conditions
// - after mume (or from start, if not using it), aim for progress and iq
// -- if low on dura, observe on useless conditions to preserve dura and bait pliant
// -- if high on dura and need more progress, consider vene + rapid spam
// - after reaching 10iq, focus on quality instead - use gs/inno combos
// -- consider what to do with progress: either force finish before starting quality (wastes good opportunities like centered later between combos) or just start quality combos immediately (harder to estimate needed cp to finish the craft)
// - after reaching quality cap, just get progress
public class Solver
{
    public bool UseReflectOpener;
    public bool MuMeRequireVeneration; // if true, we use veneration immediately after mume, disregarding any conditions (except maybe pliant for manip)
    public bool MuMeAllowIntensive = true; // if true, we allow spending mume on intensive (400p) rather than rapid (500p) if good condition procs
    public bool MuMeIntensiveLastResort = true; // if true and we're on last step of mume, use intensive (forcing via H&S if needed) rather than hoping for rapid (unless we have centered)
    public bool MuMeSkipNormal = false; // if true, observe rather than use actions during unfavourable conditions to conserve durability
    public int MuMeMinStepsForManip = 2; // if this or less rounds are remaining on mume, don't use manipulation under favourable conditions
    public int MuMeMinStepsForVene = 1; // if this or less rounds are remaining on mume, don't use veneration
    public int MidMaxPliantManipClip = 1; // max number of manipulation stacks we can tolerate losing by reapplying manip on pliant
    public int MidMasterMendLeeway = 5; // min durability deficit to keep after master's mend (e.g. if we'd want to use buff under manip on next steps)
    public bool MidAllowVeneration = true; // if true, we allow using veneration during iq phase if we lack a lot of progress
    public bool MidAllowIntensive = true; // if true, we allow spending good condition on intensive if we still need progress
    public bool MidAllowCenteredHasty = true; // if true, we consider centered hasty touch a good move for building iq (85% reliability)
    public bool MidAllowSturdyHasty = true; // if true, we consider sturdy hasty touch a good move for building iq (50% reliability), otherwise we use combo
    public bool MidBaitPliantWithObserve = true; // if true, when very low on durability and without manip active, we use observe rather than normal manip
    public bool FinisherBaitGoodByregot = true; // if true, use careful observations to try baiting good byregot
    public bool FinisherBaitPliantManip = false; // if true, use careful observations to try baiting pliant manip/mm
    public bool FinisherAllowPrep = true; // if true, we consider prep touch a good move for finisher under good+inno+gs
    public bool EmergencyCPBaitGood = false; // if true, we allow spending careful observations to try baiting good for tricks when we really lack cp

    public void Draw()
    {
        ImGui.Checkbox("Use Reflect instead of MuMe in opener", ref UseReflectOpener);
        ImGui.Checkbox("MuMe: use veneration asap, disregarding most conditions", ref MuMeRequireVeneration);
        ImGui.Checkbox("MuMe: allow spending mume on intensive (400p) rather than rapid (500p) if good condition procs", ref MuMeAllowIntensive);
        ImGui.Checkbox("MuMe: if at last step of mume and not centered, use intensive (forcing via H&S if necessary)", ref MuMeIntensiveLastResort);
        ImGui.Checkbox("MuMe: observe during unfavourable conditions instead of spending dura on normal rapids", ref MuMeSkipNormal);
        ImGui.SliderInt("MuMe: allow manipulation only if more than this amount of steps remain on mume", ref MuMeMinStepsForManip, 0, 5);
        ImGui.SliderInt("MuMe: allow veneration only if more than this amount of steps remain on mume", ref MuMeMinStepsForVene, 0, 5);
        ImGui.SliderInt("Mid: max steps we allow clipping by reapplying manip on pliant", ref MidMaxPliantManipClip, 0, 8);
        ImGui.SliderInt("Mid: allow master mend only if at least this durability deficit remains", ref MidMasterMendLeeway, 0, 30);
        ImGui.Checkbox("Mid: allow veneration if we still have large progress deficit (> rapid)", ref MidAllowVeneration);
        ImGui.Checkbox("Mid: spend good procs on intensive synth if we need more progress", ref MidAllowIntensive);
        ImGui.Checkbox("Mid: consider centered hasty a good move for building iq stacks (85% success, 10 dura)", ref MidAllowCenteredHasty);
        ImGui.Checkbox("Mid: consider sturdy hasty a good move for building iq stacks (50% success, 5 dura)", ref MidAllowSturdyHasty);
        ImGui.Checkbox("Mid: on low dura, prefer observe to non-pliant manip", ref MidBaitPliantWithObserve);
        ImGui.Checkbox("Finisher: use careful observation to try baiting good for byregot", ref FinisherBaitGoodByregot);
        ImGui.Checkbox("Finisher: use careful observation to try baiting pliant for manip/mm", ref FinisherBaitPliantManip);
        ImGui.Checkbox("Finisher: consider prep touch a good move under good+inno+gs, assuming we have enough dura", ref FinisherAllowPrep);
        ImGui.Checkbox("Emergency: use careful observation to try baiting good for tricks if really low on cp", ref EmergencyCPBaitGood);
    }

    public StepState Solve(Simulator sim, StepState step)
    {
        while (true)
        {
            var (res, next) = sim.Execute(step, SolveNextStep(sim, step));
            if (res == ExecuteResult.CantUse)
                return step;
            step = next;
        }
    }

    // TODO: malleable/primed states
    public CraftAction SolveNextStep(Simulator sim, StepState step)
    {
        if (step.Index == 1)
        {
            // always open with special action
            // comparison:
            // - mume is worth ~800p of progress (assuming we spend the buff on rapid), which is approximately equal to 3.2 rapids, which is 32 dura or ~76.8cp
            // - reflect is worth ~2 prudents of iq stacks minus 100p of quality, which is approximately equal to 50cp + 10 dura or ~74cp minus value of quality
            // so on paper mume seems to be better
            return UseReflectOpener ? CraftAction.Reflect : CraftAction.MuscleMemory;
        }

        if (step.MuscleMemoryLeft > 0) // mume still active - means we have very little progress and want more progress asap
            return SolveOpenerMuMe(sim, step);

        // see what we need to finish the craft
        var remainingProgress = sim.Craft.CraftProgress - step.Progress;
        var estBasicSynthProgress = sim.BaseProgress() * 120 / 100;
        var estCarefulSynthProgress = sim.BaseProgress() * 180 / 100; // minimal, assuming no procs/buffs
        var reservedCPForProgress = remainingProgress <= estBasicSynthProgress ? 0 : 7;
        var progressDeficit = remainingProgress - estCarefulSynthProgress; // if >0, we need more progress before we can start finisher

        if (step.IQStacks > 0)
        {
            // see if we can do byregot right now and top up quality
            var byregotQuality = sim.CalculateQuality(step, CraftAction.ByregotBlessing);
            var byregotDura = sim.GetDurabilityCost(step, CraftAction.ByregotBlessing);
            if (step.Quality + byregotQuality >= sim.Craft.CraftQualityMin3 && step.Durability > byregotDura && step.RemainingCP >= sim.GetCPCost(step, CraftAction.ByregotBlessing) + reservedCPForProgress)
                return CraftAction.ByregotBlessing;
        }

        // TODO: consider that we might want to use some touch actions after byregot's, if we have spare cp/durability...
        // TODO: if we're extremely low on CP, consider doing some careful observations to fish for tricks?
        if (step.IQStacks == 0 && step.Quality > 0 || step.Quality >= sim.Craft.CraftQualityMin3 || step.Quality >= sim.Craft.CraftQualityMin1 && step.RemainingCP < 24 + reservedCPForProgress)
            return SolveFinishProgress(sim, step, progressDeficit); // we've used byregot's, or we're at max quality anyway, or we can't byregot anymore - just finish the craft now

        if (step.VenerationLeft > 0 && progressDeficit > 0)
            return UseIfEnoughCP(sim, step, SolveMidProgress(sim, step, progressDeficit)); // we still have veneration running and need more progress, focus on that...

        if (step.IQStacks < 10)
            return UseIfEnoughCP(sim, step, SolveMidIQ(sim, step, progressDeficit)); // we need more iq (and maybe some progress too)

        // okay, we're at 10 iq stacks here, finisher time
        if (progressDeficit > 0)
            return UseIfEnoughCP(sim, step, SolveMidProgress(sim, step, progressDeficit)); // we still need progress, handle that before starting finisher

        // do the finisher
        var freeCP = step.RemainingCP - reservedCPForProgress - 24;
        var finishAction = SolveFinishQuality(sim, step, freeCP);
        if (step.RemainingCP >= sim.GetCPCost(step, finishAction))
            return finishAction;
        // we just don't have enough cp for a finisher, bail
        return SolveFinishProgress(sim, step, progressDeficit);
    }

    private CraftAction SolveOpenerMuMe(Simulator sim, StepState step)
    {
        // we don't really have any concerns about cp or durability during mume - we might end up with quite low final durability though...
        if (step.Condition == CraftCondition.Pliant && step.MuscleMemoryLeft > MuMeMinStepsForManip && step.ManipulationLeft == 0)
            return CraftAction.Manipulation;
        if (MuMeRequireVeneration && step.VenerationLeft == 0)
            return CraftAction.Veneration;
        if (step.Condition is CraftCondition.Centered or CraftCondition.Sturdy)
            return CraftAction.RapidSynthesis; // centered/sturdy => rapid, very good value
        var canUseIntensive = sim.CanUseAction(step, CraftAction.IntensiveSynthesis);
        if (canUseIntensive && MuMeAllowIntensive)
            return CraftAction.IntensiveSynthesis; // good and we are allowed to spend charge on intensive
        if (step.VenerationLeft == 0 && step.MuscleMemoryLeft > MuMeMinStepsForVene)
            return CraftAction.Veneration; // other conditions - use veneration
        if (MuMeIntensiveLastResort && step.MuscleMemoryLeft == 1)
            return canUseIntensive ? CraftAction.IntensiveSynthesis : step.HeartAndSoulAvailable ? CraftAction.HeartAndSoul : CraftAction.RapidSynthesis; // last chance
        if (MuMeSkipNormal && step.MuscleMemoryLeft > 1 && step.Durability < sim.Craft.CraftDurability)
            return CraftAction.Observe; // conserve durability rather than gamble away
        return CraftAction.RapidSynthesis; // try rapid, we can try again if it fails
    }

    private CraftAction SolveMidDurabilityManagement(Simulator sim, StepState step)
    {
        // during the mid phase, durability is a serious concern
        if (step.ManipulationLeft > 0 && step.Durability + 5 > sim.Craft.CraftDurability)
            return CraftAction.None; // we're high on dura, doing anything here will waste manip durability

        if (step.Condition == CraftCondition.Pliant)
        {
            // see if we can utilize pliant for manip/mm
            if (step.ManipulationLeft <= MidMaxPliantManipClip)
                return CraftAction.Manipulation;
            if (step.Durability + 30 + (step.ManipulationLeft > 0 ? 5 : 0) + MidMasterMendLeeway <= sim.Craft.CraftDurability)
                return CraftAction.MastersMend;
            // TODO: consider waste-not...
            return CraftAction.None;
        }

        if (step.Durability <= (step.Condition == CraftCondition.Sturdy ? 5 : 10))
        {
            // we really need to do something about durability, we don't even have useful actions to perform
            if (step.Condition == CraftCondition.Good)
                return CraftAction.TricksOfTrade;
            if (step.ManipulationLeft > 0)
                return CraftAction.Observe; // just regen a bit...
            // TODO: consider careful observation to bait pliant - this sounds much worse than using them to try baiting good byregot
            return MidBaitPliantWithObserve ? CraftAction.Observe : CraftAction.Manipulation; // pliant will save 48cp, which is almost 7 observes, so looks worthwhile?
        }

        // we still have some durability left, do nothing...
        return CraftAction.None;
    }

    private CraftAction SolveProgress(Simulator sim, StepState step, int progressDeficit)
    {
        // we are typically concerned with durability here, cp should be fine
        if (MidAllowVeneration && step.VenerationLeft == 0 && progressDeficit > sim.CalculateProgress(step, CraftAction.RapidSynthesis))
            return CraftAction.Veneration; // TODO: reconsider this heuristic (we've reached 10 iq and still lack tons of progress)
        if (progressDeficit <= sim.CalculateProgress(step, CraftAction.PrudentSynthesis))
            return SafeCraftAction(sim, step, CraftAction.PrudentSynthesis); // TODO: reconsider (minimal cost action when we need just a little more progress)
        if (step.Condition == CraftCondition.Good && MidAllowIntensive)
            return SafeCraftAction(sim, step, CraftAction.IntensiveSynthesis);
        return SafeCraftAction(sim, step, CraftAction.RapidSynthesis);
    }

    private CraftAction SolveMidProgress(Simulator sim, StepState step, int progressDeficit)
    {
        // focus on progress (either veneration is still up, or we want to move to finisher phase asap)
        var duraAction = SolveMidDurabilityManagement(sim, step); // manage durability
        if (duraAction != CraftAction.None)
            return duraAction;
        return SolveProgress(sim, step, progressDeficit);
    }

    private CraftAction SolveFinishProgress(Simulator sim, StepState step, int progressDeficit)
    {
        if (progressDeficit > 0 && step.Durability > 10)
        {
            var progressAction = SolveProgress(sim, step, progressDeficit);
            var progressAfterAction = step.Progress + sim.CalculateProgress(step, progressAction);
            var willFinishWithBasic = progressAfterAction + sim.BaseProgress() * 120 / 100 >= sim.Craft.CraftProgress;
            if (step.RemainingCP >= sim.GetCPCost(step, progressAction) + (willFinishWithBasic ? 0 : 7))
                return progressAction;
        }

        // TODO: if we can regain some cp with tricks, do that - maybe we'll be able to get some quality on next step...
        //if (last.Condition == CraftCondition.Good && last.Quality < sim.Craft.CraftQualityMin3)
        //    return CraftAction.TricksOfTrade;

        // TODO: consider prudent synth (dura > 5 but <= 10); our options:
        // - prudent + basic = 300p, 18 cp, >5 dura
        // - prudent + careful = 360p, 25 cp, >5 dura
        // - careful + basic = 300p, 7 cp, >10 dura
        // - prudent + prudent + basic = 480p, 36 cp, >10 dura - but we would probably want to byregot instead?
        return sim.GetCPCost(step, CraftAction.CarefulSynthesis) < step.RemainingCP ? CraftAction.CarefulSynthesis : CraftAction.BasicSynthesis;
    }

    private CraftAction SolveMidIQ(Simulator sim, StepState step, int progressDeficit)
    {
        var duraAction = SolveMidDurabilityManagement(sim, step);
        if (duraAction != CraftAction.None)
            return duraAction; // manage durability

        // mid stage - either get more progress if we get favourable conditios or more iq stacks
        // - normal touches are 42cp/iq (33 on pliant, 30 on sturdy)
        // - hasty touch is ~40cp/iq (20 on sturdy, 28 on centered), but it's a gamble
        // - prep touch is 44cp/iq, so not worth it
        // - precise touch is 21cp/iq (much better on pliant/sturdy with h&s), but requires good/h&s
        // - prudent touch is 37cp/iq (25 on pliant)
        // this means sturdy hasty (unreliable) = precise > pliant prudent > centered hasty (85%) > sturdy combo >> prudent > hasty (unreliable) > normal combo
        if (step.Condition == CraftCondition.Centered)
        {
            if (progressDeficit > 0)
                return SafeCraftAction(sim, step, CraftAction.RapidSynthesis); // we still need more progress, this seems to be the best use of the centered
            if (MidAllowCenteredHasty)
                return CraftAction.HastyTouch;
            // otherwise - just ignore this condition
        }

        // precise is the best value - and there's no point in holding h&s, the earlier we use it the better
        if (sim.CanUseAction(step, CraftAction.PreciseTouch))
            return CraftAction.PreciseTouch;
        else if (step.HeartAndSoulAvailable)
            return CraftAction.HeartAndSoul;

        if (step.Condition == CraftCondition.Sturdy)
            return MidAllowSturdyHasty ? CraftAction.HastyTouch : sim.NextTouchCombo(step);

        // just use prudent
        return CraftAction.PrudentTouch;
    }

    private CraftAction SolveFinishDurabilityManagementPliant(Simulator sim, StepState step, int availableCP)
    {
        var effectiveDura = step.Durability + step.ManipulationLeft * 5; // since we are going to use a lot of non-dura actions (buffs/observes), this is what really matters

        // estimate how much cp do we need to utilize current durability
        var estHalfComboCost = 34; // rough baseline - every 10 extra dura is one half-combo, which requires 34cp (1/2 inno + observe+focused) - TODO: should it also include 1/2 GS?
        var estNumHalfCombosWithCurrentDura = effectiveDura <= 20 ? 0 : (effectiveDura + 9) / 10; // 11-20 dura is 0 half-combos, 21-30 is 1, ...
        var estCPNeededToUtilizeCurrentDura = estHalfComboCost * estNumHalfCombosWithCurrentDura;

        if (step.ManipulationLeft <= 1 && availableCP >= 48 + estCPNeededToUtilizeCurrentDura + 4 * estHalfComboCost)
            return CraftAction.Manipulation;
        if (availableCP >= 44 + estCPNeededToUtilizeCurrentDura + 3 * estHalfComboCost && effectiveDura + 30 <= sim.Craft.CraftDurability)
            return CraftAction.MastersMend;
        return CraftAction.None;
    }

    private CraftAction SolveFinishQualityStart(Simulator sim, StepState step, int freeCP)
    {
        // assume we don't have GS/inno
        // finisher combo not started yet, it's best chance to regain some dura if we have enough cp to utilize it
        // we need around 30 effective durability to start a new combo
        if (step.Condition == CraftCondition.Good)
            return CraftAction.TricksOfTrade; // not worth doing precise without buffs

        var effectiveDura = step.Durability + step.ManipulationLeft * 5; // since we are going to use a lot of non-dura actions (buffs/observes), this is what really matters
        var cpToSpendOnQuality = freeCP - 50; // we'll need to get gs+inno up for byregot, rest is free to spend
        // TODO: centered/sturdy - consider doing hasty if we have durability to spare?

        if (effectiveDura <= 10)
        {
            // we're very low on durability - not enough to even byregot
            // try to recover some, even if we can't utilize it well later - at worst we can do some hasty's
            if (step.Condition != CraftCondition.Pliant && freeCP >= 44)
            {
                // see if we can try baiting pliant by careful observations
                if (FinisherBaitPliantManip && step.CarefulObservationLeft > 0)
                    return CraftAction.CarefulObservation;
                // we really don't want to waste cp on non-pliant manip/mm, so try exploring some alternatives
                // one option is to use some (inno) finesse, this is quite expensive cp-wise, but slightly more effective than using full-cost manip + focused+observe
                // heuristic: we want to have at least 18 (inno) + 4x32 (finnesse) + 88 (mm) == 234 leftover cp for that to be worthwhile
                if (cpToSpendOnQuality >= 234)
                    return CraftAction.Innovation;
                // TODO: consider baiting pliant with normal observes?
            }
            if (step.ManipulationLeft <= 1 && freeCP >= sim.GetCPCost(step, CraftAction.Manipulation))
                return CraftAction.Manipulation;
            if (freeCP >= sim.GetCPCost(step, CraftAction.MastersMend))
                return CraftAction.MastersMend;
            if (step.Condition != CraftCondition.Pliant && freeCP >= 44 + 7)
                return CraftAction.Observe; // we don't have enough for mm, but might get lucky if we try baiting it with observe...
            // try to regain some cp via tricks
            var emergencyAction = EmergencyRestoreCP(sim, step);
            if (emergencyAction != CraftAction.None)
                return emergencyAction;
            // we don't even have enough cp for mm - oh well, get some buff up, otherwise pray for sturdy/good
            if (sim.GetDurabilityCost(step, CraftAction.ByregotBlessing) < step.Durability) // sturdy, so byregot asap - we won't get a better chance to salvage the situation
                return CraftAction.ByregotBlessing;
            if (freeCP >= sim.GetCPCost(step, CraftAction.GreatStrides))
                return CraftAction.GreatStrides;
            if (freeCP >= sim.GetCPCost(step, CraftAction.Innovation))
                return CraftAction.Innovation;
            // nope, too little cp for anything... try observes
            if (freeCP >= sim.GetCPCost(step, CraftAction.Observe))
                return CraftAction.Observe;
            if (step.CarefulObservationLeft > 0)
                return CraftAction.CarefulObservation;
            // i give up :)
            return SolveFinishProgress(sim, step, 0);
        }
        else if (step.Condition == CraftCondition.Pliant)
        {
            // okay, this is a good chance to recover some durability if we can utilize it
            var duraAction = SolveFinishDurabilityManagementPliant(sim, step, cpToSpendOnQuality);
            if (duraAction != CraftAction.None)
                return duraAction;

            // nope, we don't need dura, just GS if we have at least some cp to spare
            if (freeCP >= 16)
                return CraftAction.GreatStrides;
            // ok we're really low on cp - we're gonna byregot soon, at least get inno up
            if (freeCP >= 9)
                return CraftAction.Innovation;
            // we just don't have cp at all - use byregot
            return BestByregot(sim, step);
        }
        else if (effectiveDura <= 20)
        {
            // we can do byregot, but not much else - see whether we can restore some durability
            // see if we can try baiting pliant by careful observations
            if (FinisherBaitPliantManip && step.CarefulObservationLeft > 0 && step.ManipulationLeft <= 1)
                return CraftAction.CarefulObservation;

            // our options:
            // - inno + gs + byregot - baseline to finish the craft if we don't have spare cp
            // - inno + 1-2x finesse + gs + byregot - requires 64 spare cp, gives 300p = 4.69 p/cp
            // - inno + 4x finesse - requires 128+18=146 spare cp, gives 600p = 4.11 p/cp
            // - normal manip + 4 half-combos - requires at least 96+2*18+4*25 = 232 spare cp, gives 900p = 3.87 p/cp
            // - mm + full combo + (inno) + half-combo + byregot - requires at least 88+18+3*25 = 181 spare cp, gives 675p = 3.72 p/cp
            // TODO: for now, just inno and react to what happens later (e.g. spam finesse if possible)
            return CraftAction.Innovation;
        }
        else
        {
            // we have enough cp to do at least one normal quality action
            // TODO: think about whether we want to treat effectiveDura <= 30 as a special case - for now, assume finesse+finesse is a good option for second half-combo if we have cp to spare
            // our options:
            // - inno + (half-combo) + gs + byregot - baseline if we don't have spare cp
            // - gs + inno + half-combo + gs + byregot - extra 32cp for ~150p (assuming our half-combo is observe+focused) - this is a good option, approximately equivalent to (inno) finesse, but allows to exploit conditions better
            return cpToSpendOnQuality >= 32 ? CraftAction.GreatStrides : CraftAction.Innovation;
        }
    }

    private CraftAction SolveFinishQuality(Simulator sim, StepState step, int freeCP)
    {
        // some rough estimations (potency numbers are pre-iq for simplicity, since it just effectively doubles the base quality rate at this point):
        // - typically after iq stacks we need ~2250p worth of quality
        // - byregot under inno+gs would give us 750p, plus extra 562.5p if good
        // - this means we would need around 1500p from normal actions
        // our options (two step 'half combos', so inno covers two; all except finesse and prep costs 10 dura):
        // - observe + focused = 225p for 25cp (49 effective) = 9.00p/cp (4.59 eff) - baseline for effectiveness
        // - prudent + prudent = 300p for 50cp (74 effective) = 6.00p/cp (4.05 eff) - doesn't seem to be worth it?
        // - [gs before inno] + observe + focused = 375p for 57cp (81 effective) = 6.58p/cp (4.63 eff) - good way to spend excessive cp
        // - finesse + finesse = 300p for 64cp (64 effective) = 4.69p/cp (4.69 eff) - does not cost dura, but very expensive cp wise
        // - prep = 300p for 40cp and 20 dura (88 effective) = 4.41p/cp eff - not worth it unless we have some conditions or just want to burn leftover dura
        // - gs + prep = 500p for 72cp and 20 dura (120 effective) = 4.16p/cp eff - not worth it unless we have some good omen or just want to burn leftover dura
        // good condition:
        // - tricks (20cp) is worth ~100p, probably less because of inno (but it's a good option if no buffs are up)
        // - prep touch gives extra 225p (or 375p under gs+inno), which is the most efficient use of good (but expensive)
        // - after observe, focused or precise are equivalent; the good is worth extra ~169p (or ~281p under gs)
        // - otherwise replacing observe with precise is decent (effectively finishes half-combo in 1 step)
        // centered condition
        // - TODO consider hasty?
        // sturdy condition
        // - ignored and wasted if we'd like to use buff
        // - prep is the best way to utilize sturdy
        // pliant condition
        // - best used on manip (48cp worth), if we have enough cp to utilize extra durability
        // - also reasonable to use on GS (16cp worth) or prep (20 cp worth), if GS/inno stack is already active
        // - can even be used on focused (9cp worth) if it pops after observe
        // manip allows us to replace 4 finesse+finesse half-combos with observe+focused:
        // - finesse+finesse = 300p for 64cp = 4.69p/cp
        // - 1/4 manip + observe+focused = 225p for 25+24cp = 4.59p/cp; if manip is cast on pliant, that changes to 6.08p/cp
        // how to determine whether we have spare cp/dura for more quality or should just byregot?
        // - we need to always be at >10 durability, so that we can always do a byregot+synth
        // - freeCP accounts for byregot and last progress step, so anything >0 can be used for more quality
        // - we really want byregot to be under inno+gs, so using any quality action now will require 32cp for (re)applying gs + 18cp for (re)applying inno unless we have at least 3 steps left
        // - if we have more cp still, we have following options:
        // -- (inno) + finesse + byregot-combo - needs 32cp and no dura, gives 150p quality
        // -- (inno) + prudent + byregot-combo - needs 25cp and 5 dura, gives 150p quality
        // -- (inno) + half-combo + byregot-combo - needs 25-72cp (observe+focused - gs+prep) and 10-20 dura, gives 225-500p quality; inno needs to be reapplied unless at 4 steps
        // -- gs + inno + half-combo - needs 57cp+ and 10+ dura, gives 375p quality; it's something to consider only if inno is not running now
        // -- extra cp/durability can be burned by doing multiple half-combos, but that's a decision to be made on later steps
        // -- if we have tons of cp but not enough durability, we might want to manip; this is reasonable if we have enough cp to do extra 4 half-combos (136 cp minimum + manip cost)
        var cpToSpendOnQuality = freeCP - 32 - (step.InnovationLeft > 2 ? 0 : 18); // we'll need to get gs up for byregot and maybe reapply inno if we do not go for the quality finisher now

        if (step.InnovationLeft == 0)
        {
            if (step.GreatStridesLeft == 0)
            {
                return SolveFinishQualityStart(sim, step, freeCP);
            }
            else
            {
                // GS without inno, see if we want to use it asap
                if (step.Condition == CraftCondition.Pliant)
                {
                    // pliant after GS - this is a decent chance to recover some durability if we need it
                    // if we're at last step of GS for whatever reason - assume it's ok to waste it...
                    var duraAction = SolveFinishDurabilityManagementPliant(sim, step, cpToSpendOnQuality);
                    if (duraAction != CraftAction.None)
                        return duraAction;
                }

                if (step.Condition == CraftCondition.Good && CanUseActionSafelyInFinisher(sim, step, CraftAction.PreciseTouch, cpToSpendOnQuality))
                    return CraftAction.PreciseTouch;
                if (step.PrevComboAction == CraftAction.Observe && CanUseActionSafelyInFinisher(sim, step, CraftAction.FocusedTouch, cpToSpendOnQuality))
                    return CraftAction.FocusedTouch; // this is weird, why would we do gs->observe?.. maybe we're low on cp?

                if (step.GreatStridesLeft == 1)
                {
                    // we really want to use gs now on other touches (prudent/finesse), doing inno now would waste it
                    // TODO: hasty? basic combo? prep?
                    if (CanUseActionSafelyInFinisher(sim, step, CraftAction.PrudentTouch, cpToSpendOnQuality))
                        return CraftAction.PrudentTouch;
                    if (cpToSpendOnQuality >= sim.GetCPCost(step, CraftAction.TrainedFinnesse))
                        return CraftAction.TrainedFinnesse;
                }
                else
                {
                    if (cpToSpendOnQuality >= sim.GetCPCost(step, CraftAction.Innovation))
                        return CraftAction.Innovation;
                }

                // last condition failing means that we have very low cp, try to recover somehow (tricks/byregot/...)
                return SolveFinishQualityStart(sim, step, freeCP);
            }
        }
        else
        {
            // innovation up
            // our options:
            // - gs + byregot - if we're low on cp or will finish the craft with current quality
            // - manip/mm on pliant if needed
            // - prep / precise if good (or pliant?)
            // - observe + focused
            // - prudent
            // - finesse
            // - gs on pliant + some touch
            // - hasty on low cp to burn dura
            if (step.Condition == CraftCondition.Good)
            {
                // good options are prep and precise (focused after observe is the same as precise, so don't bother)
                // prep is ~2x the cost, quality is 525 vs 393.75 (no gs) or 875 vs 656.25 (with gs), meaning it's worth an extra 131.25/218.75p
                // we can compare good prep with good precise + focused combo, which is an extra 225p
                // all in all, it feels like prep is only worth it under gs?..
                if (FinisherAllowPrep && step.GreatStridesLeft > 0 && CanUseActionSafelyInFinisher(sim, step, CraftAction.PreparatoryTouch, cpToSpendOnQuality))
                    return CraftAction.PreparatoryTouch;
                // otherwise use precise if possible
                if (CanUseActionSafelyInFinisher(sim, step, CraftAction.PreciseTouch, cpToSpendOnQuality))
                    return CraftAction.PreciseTouch;
                // otherwise ignore good condition and see what else can we do
            }
            else if (step.Condition == CraftCondition.Pliant && step.GreatStridesLeft != 1)
            {
                // we won't waste gs if we do manip/mm now - see if we want it
                // we don't really care about wasting last step of inno, it's no different from wasting any other step
                var duraAction = SolveFinishDurabilityManagementPliant(sim, step, cpToSpendOnQuality);
                if (duraAction != CraftAction.None)
                    return duraAction;
                // otherwise ignore pliant and just save some cp on touch actions
            }

            if (step.PrevComboAction == CraftAction.Observe && CanUseActionSafelyInFinisher(sim, step, CraftAction.FocusedTouch, cpToSpendOnQuality))
                return CraftAction.FocusedTouch; // complete focused half-combo

            // try spending some durability for using some other half-combo action:
            // - observe + focused if we have enough time on gs/inno is 150p for 25cp
            // - prudent is 100p for 25cp, so less efficient - but useful if we don't have enough time/durability for full half-combo
            // - pliant gs (+ prudent) is extra ~66p for 16cp, so it's an option i guess, especially considering we might get some better condition (TODO consider this)
            // - finesse is 100p for 32cp, which is even less efficient, but does not cost durability
            // - hasty is a fine way to spend excess durability if low on cp
            if (step.InnovationLeft != 1 && step.GreatStridesLeft != 1 && step.Durability > (step.ManipulationLeft > 0 ? 5 : 10) && step.Durability + 5 * step.ManipulationLeft > 20 && cpToSpendOnQuality >= sim.GetCPCost(step, CraftAction.Observe) + 18)
                return CraftAction.Observe;
            if (CanUseActionSafelyInFinisher(sim, step, CraftAction.PrudentTouch, cpToSpendOnQuality))
                return CraftAction.PrudentTouch;
            if (cpToSpendOnQuality >= sim.GetCPCost(step, CraftAction.TrainedFinnesse))
                return CraftAction.TrainedFinnesse;
            // see if we can regain some cp via tricks
            var emergencyAction = EmergencyRestoreCP(sim, step);
            if (emergencyAction != CraftAction.None)
                return emergencyAction;
            if (CanUseActionSafelyInFinisher(sim, step, CraftAction.HastyTouch, cpToSpendOnQuality))
                return CraftAction.HastyTouch; // better than nothing i guess...

            // ok, we're out of options - use gs + byregot
            if (step.GreatStridesLeft == 0 && freeCP >= sim.GetCPCost(step, CraftAction.GreatStrides))
                return CraftAction.GreatStrides;
            return BestByregot(sim, step);
        }
    }

    private CraftAction BestByregot(Simulator sim, StepState step)
    {
        // if we still have careful observations, we might wanna try baiting good
        // TODO: sturdy byregot if we're at critical dura/cp (e.g. 10 dura and no manip/no cp for observes)
        return !FinisherBaitGoodByregot || step.CarefulObservationLeft == 0 || (step.Condition is CraftCondition.Good or CraftCondition.Excellent) ? CraftAction.ByregotBlessing : CraftAction.CarefulObservation;
    }

    private bool CanUseActionSafelyInFinisher(Simulator sim, StepState step, CraftAction action, int availableCP)
    {
        var duraCost = sim.GetDurabilityCost(step, action);
        return step.Durability > duraCost && step.Durability + 5 * step.ManipulationLeft - duraCost > 10 && availableCP >= sim.GetCPCost(step, action);
    }

    public CraftAction SafeCraftAction(Simulator sim, StepState step, CraftAction action) => sim.WillFinishCraft(step, action) ? CraftAction.FinalAppraisal : action;

    // try to use tricks, if needed use h&s
    public CraftAction EmergencyRestoreCP(Simulator sim, StepState step)
    {
        if (sim.CanUseAction(step, CraftAction.TricksOfTrade))
            return CraftAction.TricksOfTrade;
        if (step.HeartAndSoulAvailable)
            return CraftAction.HeartAndSoul;
        if (EmergencyCPBaitGood && step.CarefulObservationLeft > 0)
            return CraftAction.CarefulObservation; // try baiting good?..
        return CraftAction.None;
    }

    // if not enough cp, it's an emergency (e.g. out of cp mid craft due to really shit luck), try tricks or just bail...
    public CraftAction UseIfEnoughCP(Simulator sim, StepState step, CraftAction action)
    {
        if (step.RemainingCP >= sim.GetCPCost(step, action))
            return action;
        var emergencyAction = EmergencyRestoreCP(sim, step);
        if (emergencyAction != CraftAction.None)
            return emergencyAction;
        // no idea...
        return CraftAction.BasicSynthesis;
    }
}
