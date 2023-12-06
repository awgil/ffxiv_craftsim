using ImGuiNET;

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
// - primed manip is free 10dura = 18cp for full cost, meaning it's ~30cp more expensive than pliant; observing would be ~31cp, meaning generally pliant is an ok alternative
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
    public bool MuMeIntensiveGood = true; // if true, we allow spending mume on intensive (400p) rather than rapid (500p) if good condition procs
    public bool MuMeIntensiveMalleable = false; // if true and we have malleable during mume, use intensive rather than hoping for rapid
    public bool MuMeIntensiveLastResort = true; // if true and we're on last step of mume, use intensive (forcing via H&S if needed) rather than hoping for rapid (unless we have centered)
    public bool MuMePrimedManip = false; // if true, allow using primed manipulation after veneration is up on mume
    public bool MuMeAllowObserve = false; // if true, observe rather than use actions during unfavourable conditions to conserve durability
    public int MuMeMinStepsForManip = 2; // if this or less rounds are remaining on mume, don't use manipulation under favourable conditions
    public int MuMeMinStepsForVene = 1; // if this or less rounds are remaining on mume, don't use veneration
    public int MidMinIQForHSPrecise = 10; // min iq stacks where we use h&s+precise; 10 to disable
    public bool MidBaitPliantWithObservePreQuality = true; // if true, when very low on durability and without manip active during pre-quality phase, we use observe rather than normal manip
    public bool MidBaitPliantWithObserveAfterIQ = true; // if true, when very low on durability and without manip active after iq is stacked, we use observe rather than normal manip or inno+finnesse
    public bool MuMePrimedManipPreQuality = true; // if true, allow using primed manipulation during pre-quality phase
    public bool MuMePrimedManipAfterIQ = true; // if true, allow using primed manipulation during after iq is stacked
    public bool MidKeepHighDuraUnbuffed = true; // if true, observe rather than use actions during unfavourable conditions to conserve durability when no buffs are active
    public bool MidKeepHighDuraVeneration = false; // if true, observe rather than use actions during unfavourable conditions to conserve durability when veneration is active
    public bool MidAllowVenerationGoodOmen = true; // if true, we allow using veneration during iq phase if we lack a lot of progress on good omen
    public bool MidAllowVenerationAfterIQ = true; // if true, we allow using veneration after iq is fully stacked if we still lack a lot of progress
    public bool MidAllowIntensiveUnbuffed = false; // if true, we allow spending good condition on intensive if we still need progress when no buffs are active
    public bool MidAllowIntensiveVeneration = false; // if true, we allow spending good condition on intensive if we still need progress when veneration is active
    public bool MidAllowPrecise = true; // if true, we allow spending good condition on precise touch if we still need iq
    public bool MidAllowSturdyPreсise = false; // if true,we consider sturdy+h&s+precise touch a good move for building iq
    public bool MidAllowCenteredHasty = true; // if true, we consider centered hasty touch a good move for building iq (85% reliability)
    public bool MidAllowSturdyHasty = true; // if true, we consider sturdy hasty touch a good move for building iq (50% reliability), otherwise we use combo
    public bool MidAllowGoodPrep = true; // if true, we consider prep touch a good move for finisher under good+inno+gs
    public bool MidAllowSturdyPrep = true; // if true, we consider prep touch a good move for finisher under sturdy+inno
    public bool MidGSBeforeInno = true; // if true, we start quality combos with gs+inno rather than just inno
    public bool MidFinishProgressBeforeQuality = true; // if true, at 10 iq we first finish progress before starting on quality
    public bool MidObserveGoodOmenForTricks = false; // if true, we'll observe on good omen where otherwise we'd use tricks on good
    public bool FinisherBaitGoodByregot = true; // if true, use careful observations to try baiting good byregot
    public bool EmergencyCPBaitGood = false; // if true, we allow spending careful observations to try baiting good for tricks when we really lack cp

    public void Draw()
    {
        ImGui.Checkbox("Use Reflect instead of MuMe in opener", ref UseReflectOpener);
        ImGui.Checkbox("MuMe: allow spending mume on intensive (400p) rather than rapid (500p) if good condition procs", ref MuMeIntensiveGood);
        ImGui.Checkbox("MuMe: if malleable during mume, use H&S + intensive", ref MuMeIntensiveMalleable);
        ImGui.Checkbox("MuMe: if at last step of mume and not centered, use intensive (forcing via H&S if necessary)", ref MuMeIntensiveLastResort);
        ImGui.Checkbox("MuMe: use primed manipulation, if veneration is already active", ref MuMePrimedManip);
        ImGui.Checkbox("MuMe: observe during unfavourable conditions instead of spending dura on normal rapids", ref MuMeAllowObserve);
        ImGui.SliderInt("MuMe: allow manipulation only if more than this amount of steps remain on mume", ref MuMeMinStepsForManip, 0, 5);
        ImGui.SliderInt("MuMe: allow veneration only if more than this amount of steps remain on mume", ref MuMeMinStepsForVene, 0, 5);
        ImGui.SliderInt("Mid: min iq stacks to spend h&s on precise (10 to disable)", ref MidMinIQForHSPrecise, 0, 10);
        ImGui.Checkbox("Mid: on low dura, prefer observe to non-pliant manip before iq is stacked", ref MidBaitPliantWithObservePreQuality);
        ImGui.Checkbox("Mid: on low dura, prefer observe to non-pliant manip / inno+finesse after iq is stacked", ref MidBaitPliantWithObserveAfterIQ);
        ImGui.Checkbox("Mid: use manipulation on primed before iq is stacked", ref MuMePrimedManipPreQuality);
        ImGui.Checkbox("Mid: use manipulation on primed after iq is stacked, if enough cp is available to utilize durability well", ref MuMePrimedManipAfterIQ);
        ImGui.Checkbox("Mid: allow observes during unfavourable conditions without buffs", ref MidKeepHighDuraUnbuffed);
        ImGui.Checkbox("Mid: allow observes during unfavourable conditions under veneration", ref MidKeepHighDuraVeneration);
        ImGui.Checkbox("Mid: allow veneration if we still have large progress deficit (> intensive) on good omen", ref MidAllowVenerationGoodOmen);
        ImGui.Checkbox("Mid: allow veneration if we still have large progress deficit (> rapid) after iq is stacked", ref MidAllowVenerationAfterIQ);
        ImGui.Checkbox("Mid: spend good procs on intensive synth if we need more progress without buffs", ref MidAllowIntensiveUnbuffed);
        ImGui.Checkbox("Mid: spend good procs on intensive synth if we need more progress under veneration", ref MidAllowIntensiveVeneration);
        ImGui.Checkbox("Mid: spend good procs on precise touch if we need more iq stacks", ref MidAllowPrecise);
        ImGui.Checkbox("Mid: consider sturdy h&s+precise touch a good move for building iq stacks", ref MidAllowSturdyPreсise);
        ImGui.Checkbox("Mid: consider centered hasty a good move for building iq stacks (85% success, 10 dura)", ref MidAllowCenteredHasty);
        ImGui.Checkbox("Mid: consider sturdy hasty a good move for building iq stacks (50% success, 5 dura)", ref MidAllowSturdyHasty);
        ImGui.Checkbox("Mid: consider prep touch a good move under good+inno+gs, assuming we have enough dura", ref MidAllowGoodPrep);
        ImGui.Checkbox("Mid: consider prep touch a good move under sturdy+inno, assuming we have enough dura", ref MidAllowSturdyPrep);
        ImGui.Checkbox("Mid: use gs before inno+quality combos", ref MidGSBeforeInno);
        ImGui.Checkbox("Mid: finish progress before starting quality phase", ref MidFinishProgressBeforeQuality);
        ImGui.Checkbox("Mid: observe on good omen if we would otherwise use tricks on good", ref MidObserveGoodOmenForTricks);
        ImGui.Checkbox("Finisher: use careful observation to try baiting good for byregot", ref FinisherBaitGoodByregot);
        ImGui.Checkbox("Emergency: use careful observation to try baiting good for tricks if really low on cp", ref EmergencyCPBaitGood);
    }

    public StepState Solve(Simulator sim, StepState step)
    {
        while (true)
        {
            var (res, next) = sim.Execute(step, SolveNextStep(sim, step).Item1);
            if (res == ExecuteResult.CantUse)
                return step;
            step = next;
        }
    }

    public (CraftAction, string) SolveNextStep(Simulator sim, StepState step)
    {
        if (step.Index == 1)
        {
            // always open with special action
            // comparison:
            // - mume is worth ~800p of progress (assuming we spend the buff on rapid), which is approximately equal to 3.2 rapids, which is 32 dura or ~76.8cp
            // - reflect is worth ~2 prudents of iq stacks minus 100p of quality, which is approximately equal to 50cp + 10 dura or ~74cp minus value of quality
            // so on paper mume seems to be better
            return (UseReflectOpener ? CraftAction.Reflect : CraftAction.MuscleMemory, "opener");
        }

        if (step.MuscleMemoryLeft > 0) // mume still active - means we have very little progress and want more progress asap
            return (SolveOpenerMuMe(sim, step), "mume");

        // see what we need to finish the craft
        var remainingProgress = sim.Craft.CraftProgress - step.Progress;
        var estBasicSynthProgress = sim.BaseProgress() * 120 / 100;
        var estCarefulSynthProgress = sim.BaseProgress() * 180 / 100; // minimal, assuming no procs/buffs
        var reservedCPForProgress = remainingProgress <= estBasicSynthProgress ? 0 : 7;
        var progressDeficit = remainingProgress - estCarefulSynthProgress; // if >0, we need more progress before we can start finisher
        var cpAvailableForQuality = step.RemainingCP - reservedCPForProgress;

        // see if we can do byregot right now and top up quality
        var finishQualityAction = SolveFinishQuality(sim, step, cpAvailableForQuality);
        if (finishQualityAction.Item1 != CraftAction.None)
            return finishQualityAction;

        var isMid = step.Quality < sim.Craft.CraftQualityMin3 && (step.Quality < sim.Craft.CraftQualityMin1 || cpAvailableForQuality >= 24);
        if (isMid)
        {
            // we still need quality and have cp available - we're mid craft
            var midAction = SolveMid(sim, step, progressDeficit, cpAvailableForQuality);
            if (step.RemainingCP >= sim.GetCPCost(step, midAction.Item1))
                return midAction;
            // try restoring some cp...
            var emergencyAction = EmergencyRestoreCP(sim, step);
            if (emergencyAction != CraftAction.None)
                return (emergencyAction, "mid: emergency cp");
            // oh well, bail...
        }

        // try to finish the craft
        return (SolveFinishProgress(sim, step), isMid ? "finish emergency" : "finish");
    }

    private CraftAction SolveOpenerMuMe(Simulator sim, StepState step)
    {
        // we don't really have any concerns about cp or durability during mume - we might end up with quite low final durability though...
        var lastChance = step.MuscleMemoryLeft == 1; // if we don't use successful touch now, we'll waste mume
        if (step.Condition == CraftCondition.Pliant)
        {
            // pliant is manip > vene > ignore
            if (step.MuscleMemoryLeft > MuMeMinStepsForManip && step.ManipulationLeft == 0)
                return CraftAction.Manipulation;
            if (step.MuscleMemoryLeft > MuMeMinStepsForVene && step.VenerationLeft == 0)
                return CraftAction.Veneration;
        }
        else if (step.Condition == CraftCondition.Primed && MuMePrimedManip)
        {
            // primed is vene > manip > ignore
            if (step.MuscleMemoryLeft > MuMeMinStepsForVene && step.VenerationLeft == 0)
                return CraftAction.Veneration;
            if (step.MuscleMemoryLeft > MuMeMinStepsForManip && step.ManipulationLeft == 0)
                return CraftAction.Manipulation;
        }
        else if (step.Condition == CraftCondition.Centered)
        {
            // centered rapid is very good value, even disregarding last-chance or veneration concerns
            return CraftAction.RapidSynthesis;
        }
        else if (step.Condition == CraftCondition.Sturdy)
        {
            // last-chance intensive or rapid, regardless of veneration
            return SolveOpenerMuMeTouch(sim, step, MuMeIntensiveLastResort && lastChance);
        }
        else if (step.Condition == CraftCondition.Malleable)
        {
            // last-chance/preferred intensive or rapid, regardless of veneration
            return SolveOpenerMuMeTouch(sim, step, MuMeIntensiveMalleable || MuMeIntensiveLastResort && lastChance);
        }
        else if (step.Condition == CraftCondition.Good && MuMeIntensiveGood)
        {
            // good and we want to spend on intensive
            return CraftAction.IntensiveSynthesis;
        }

        // ok we have a normal/ignored condition
        if (step.MuscleMemoryLeft > MuMeMinStepsForVene && step.VenerationLeft == 0)
            return CraftAction.Veneration;
        if (MuMeAllowObserve && step.MuscleMemoryLeft > 1 && step.Durability < sim.Craft.CraftDurability)
            return CraftAction.Observe; // conserve durability rather than gamble away
        return SolveOpenerMuMeTouch(sim, step, MuMeIntensiveLastResort && lastChance);
    }

    private CraftAction SolveOpenerMuMeTouch(Simulator sim, StepState step, bool intensive)
        => !intensive ? CraftAction.RapidSynthesis : sim.CanUseAction(step, CraftAction.IntensiveSynthesis) ? CraftAction.IntensiveSynthesis : step.HeartAndSoulAvailable ? CraftAction.HeartAndSoul : CraftAction.RapidSynthesis;

    private (CraftAction, string) SolveMid(Simulator sim, StepState step, int progressDeficit, int availableCP)
    {
        var reservedCPForFinisher = 24 + 32 + (step.InnovationLeft > 2 ? 0 : 18); // we'll need to get gs up for byregot and maybe reapply inno if we do not go for the quality finisher now
        if (step.IQStacks < 10 || progressDeficit > 0 && MidFinishProgressBeforeQuality)
        {
            return SolveMidPreQuality(sim, step, progressDeficit, availableCP);
        }
        else if (step.GreatStridesLeft == 0 && step.InnovationLeft == 0)
        {
            return SolveMidStartQuality(sim, step, progressDeficit, availableCP, reservedCPForFinisher);
        }
        else
        {
            return SolveMidQuality(sim, step, availableCP, reservedCPForFinisher);
        }
    }

    private (CraftAction, string) SolveMidPreQuality(Simulator sim, StepState step, int progressDeficit, int availableCP)
    {
        // build up iq, or finish up progress before moving to quality
        // see if there are nice conditions to exploit
        var venerationActive = progressDeficit > 0 && step.VenerationLeft > 0;
        var allowObserveOnLowDura = venerationActive ? MidKeepHighDuraVeneration : MidKeepHighDuraUnbuffed;
        var allowIntensive = venerationActive ? MidAllowIntensiveVeneration : MidAllowIntensiveUnbuffed;
        var allowPrecise = MidAllowPrecise && (!allowObserveOnLowDura || step.ManipulationLeft > 0 || step.Durability > 25) /*&& !venerationActive*/;
        if (progressDeficit > 0 && SolveMidHighPriorityProgress(sim, step, allowIntensive) is var highPrioProgress && highPrioProgress != CraftAction.None)
            return (SafeCraftAction(sim, step, highPrioProgress), "mid pre quality: high-prio progress");
        if (step.IQStacks < 10 && SolveMidHighPriorityIQ(sim, step, allowPrecise) is var highPrioIQ && highPrioIQ != CraftAction.None)
            return (highPrioIQ, "mid pre quality: high-prio iq");
        if (step.Condition == CraftCondition.Good)
            return (CraftAction.TricksOfTrade, "mid pre quality: high-prio tricks"); // progress/iq below decided not to use good, so spend it on tricks
        // TODO: observe on good omen?..

        // ok, durability management time
        var duraAction = SolveMidDurabilityPreQuality(sim, step, availableCP, allowObserveOnLowDura, progressDeficit > 0);
        if (duraAction != CraftAction.None)
            return (duraAction, "mid pre quality: durability");

        // dura is fine - see what else can we do
        if (step.Condition == CraftCondition.GoodOmen && MidAllowVenerationGoodOmen && MidAllowIntensiveVeneration && progressDeficit > sim.CalculateProgress(step, CraftAction.IntensiveSynthesis))
            return (CraftAction.Veneration, "mid pre quality: good omen vene"); // next step would be intensive, vene is a good choice here

        if (step.IQStacks < 10 && !venerationActive)
        {
            // we want more iq:
            // - normal touches are 36cp/iq (27 on pliant/sturdy)
            // - hasty touch is ~30cp/iq (15 on sturdy, 21 on centered), but it's a gamble
            // - prep touch is 38cp/iq, so not worth it
            // - precise touch is 18cp/iq (much better on pliant/sturdy with h&s), but requires good/h&s
            // - prudent touch is 34cp/iq (22 on pliant)
            // this means sturdy hasty (unreliable) = precise > centered hasty (85%) = pliant prudent >> sturdy combo > hasty (unreliable) > prudent  > normal combo
            // note that most conditions are handled before calling this
            if (step.IQStacks >= MidMinIQForHSPrecise && step.IQStacks < 9 && step.Durability > sim.GetDurabilityCost(step, CraftAction.PreciseTouch))
            {
                if (sim.CanUseAction(step, CraftAction.PreciseTouch))
                    return (CraftAction.PreciseTouch, "mid pre quality: iq");
                else if (step.HeartAndSoulAvailable)
                    return (CraftAction.HeartAndSoul, "mid pre quality: iq");
            }

            // just use prudent
            if (step.Durability > sim.GetDurabilityCost(step, CraftAction.PrudentTouch))
                return (CraftAction.PrudentTouch, "mid pre quality: iq");
        }
        else
        {
            // focus on progress
            if (MidAllowVenerationAfterIQ && step.VenerationLeft == 0 && progressDeficit > sim.CalculateProgress(step, CraftAction.RapidSynthesis) && step.Durability + 5 * step.ManipulationLeft > 20)
                return (CraftAction.Veneration, "mid pre quality: progress"); // TODO: reconsider this heuristic
            if (progressDeficit <= sim.CalculateProgress(step, CraftAction.PrudentSynthesis) && step.Durability > sim.GetDurabilityCost(step, CraftAction.PrudentSynthesis))
                return (SafeCraftAction(sim, step, CraftAction.PrudentSynthesis), "mid pre quality: progress"); // TODO: reconsider (minimal cost action when we need just a little more progress)
            if (step.Durability > sim.GetDurabilityCost(step, CraftAction.RapidSynthesis))
                return (SafeCraftAction(sim, step, CraftAction.RapidSynthesis), "mid pre quality: progress");
        }
        // wait...
        return (CraftAction.Observe, "mid pre quality: no options");
    }

    private (CraftAction, string) SolveMidStartQuality(Simulator sim, StepState step, int progressDeficit, int availableCP, int reservedCP)
    {
        // no buffs up, this is a good chance to get some dura back if needed, and then get some iq/progress/quality, maybe start dedicated progress/quality phase
        // first see whether we have some nice conditions to exploit for progress or iq
        if (progressDeficit > 0 && SolveMidHighPriorityProgress(sim, step, true) is var highPrioProgress && highPrioProgress != CraftAction.None)
            return (SafeCraftAction(sim, step, highPrioProgress), "mid start quality: high-prio progress");
        if (step.Condition == CraftCondition.Good)
            return (CraftAction.TricksOfTrade, "mid start quality: high-prio tricks");

        // on good omen, our choice is either observe+tricks (+13cp) or gs+precise (300p for 50cp+10dura), meaning that using gs+precise is 4.76p/cp effectively
        // our baseline for 10dura is inno+focused (225p for 9+7+18cp = 6.61p/cp) or gs+inno+focused (375p for 32+9+7+18cp = 5.68p/cp)
        // so prefer observing on good omen
        if (MidObserveGoodOmenForTricks && step.Condition == CraftCondition.GoodOmen)
            return (CraftAction.Observe, "mid start quality: good omen -> high-prio tricks");

        // ok, durability management time
        var duraAction = SolveMidDurabilityStartQuality(sim, step, availableCP);
        if (duraAction != CraftAction.None)
            return (duraAction, "mid start quality: durability");

        // dura is fine - see what else can we do
        if (step.Condition == CraftCondition.GoodOmen && MidAllowVenerationGoodOmen && progressDeficit > sim.CalculateProgress(step, CraftAction.IntensiveSynthesis))
            return (CraftAction.Veneration, "mid start quality: good omen vene"); // next step would be intensive, vene is a good choice here

        var freeCP = availableCP - 24;
        var cpToSpendOnQuality = availableCP - reservedCP;

        // we need around >20 effective durability to start a new combo
        var effectiveDura = step.Durability + step.ManipulationLeft * 5;
        // TODO: reconsider this condition and the whole block of code below, it's a bit meh, and probably should be a part of dura management function
        if (effectiveDura <= 10 && cpToSpendOnQuality < 88 + 18 + 4 * 32)
        {
            // we're very low on durability - not enough to even byregot - and not enough cp to regain it normally
            // try some emergency actions
            if (step.Condition != CraftCondition.Pliant && freeCP >= 44 + 7)
                return (CraftAction.Observe, "mid start quality: critical dura"); // we don't have enough for mm, but might get lucky if we try baiting it with observe...
            // we don't even have enough cp for mm - oh well, get some buff up, otherwise pray for sturdy/good
            if (sim.GetDurabilityCost(step, CraftAction.ByregotBlessing) < step.Durability) // sturdy, so byregot asap - we won't get a better chance to salvage the situation
                return (CraftAction.ByregotBlessing, "mid start quality: critical dura & sturdy");
            if (freeCP >= sim.GetCPCost(step, CraftAction.GreatStrides))
                return (CraftAction.GreatStrides, "mid start quality: critical dura");
            if (freeCP >= sim.GetCPCost(step, CraftAction.Innovation))
                return (CraftAction.Innovation, "mid start quality: critical dura");
            // nope, too little cp for anything... try observes
            if (freeCP >= sim.GetCPCost(step, CraftAction.Observe))
                return (CraftAction.Observe, "mid start quality: critical dura & emergency cp");
            if (step.CarefulObservationLeft > 0)
                return (CraftAction.CarefulObservation, "mid start quality: critical dura & emergency cp");
            // i give up :)
            return (CraftAction.ByregotBlessing, "mid start quality: critical dura & emergency cp"); // let the caller handle lack of cp
        }

        // main choice here is whether to use gs before inno
        // - if we use gs+inno, we'll have 2 steps to use touch - enough for a full half-combo, and an opportunity to react to pliant
        // - gs is 32cp; using it on focused is extra 150p = 4.69p/cp, which is equal to extra finesse (but with opportunity to react to conditions)
        // - spending (normal) gs on 100p touch is worse than using finesse under inno, so don't bother if we don't have enough dura
        // - gs is a good way to spend pliant if we don't need dura and don't have inno up, even if we're going to use 100p touches
        // as a conclusion, we use gs if we have enough dura or we have pliant
        // TODO: is it a good idea to use gs on primed? it's only marginally useful (if we get pliant on next step), primed inno is a free ~9cp
        if (MidGSBeforeInno && step.Condition != CraftCondition.Primed && (step.Condition == CraftCondition.Pliant || effectiveDura > 20) && freeCP >= sim.GetCPCost(step, CraftAction.GreatStrides) + 18 + 7 + 18)
            return (CraftAction.GreatStrides, "mid start quality");
        // just inno and react to what happens...
        return (CraftAction.Innovation, "mid start quality");
    }

    private (CraftAction, string) SolveMidQuality(Simulator sim, StepState step, int availableCP, int reservedCP)
    {
        // some rough estimations (potency numbers are pre-iq for simplicity, since it just effectively doubles the base quality rate at this point):
        // - typically after iq stacks we need ~2250p worth of quality
        // - byregot under inno+gs would give us 750p, plus extra 562.5p if good
        // - this means we would need around 1500p from normal actions
        // our options (two step 'half combos', so inno covers two; all except finesse and prep costs 10 dura):
        // - observe + focused = 225p for 25cp (43 effective) = 9.00p/cp (5.23 eff) - baseline for effectiveness
        // - prudent + prudent = 300p for 50cp (68 effective) = 6.00p/cp (4.41 eff) - doesn't seem to be worth it?
        // - [gs before inno] + observe + focused = 375p for 57cp (75 effective) = 6.58p/cp (5.00 eff) - good way to spend excessive cp and allows reacting to conditions
        // - finesse + finesse = 300p for 64cp (64 effective) = 4.69p/cp (4.69 eff) - does not cost dura, but very expensive cp wise
        // - prep = 300p for 40cp and 20 dura (76 effective) = 3.94p/cp eff - not worth it unless we have some conditions or just want to burn leftover dura
        // - gs + prep = 500p for 72cp and 20 dura (108 effective) = 4.62p/cp eff - not worth it unless we have good omen or just want to burn leftover dura
        // good condition:
        // - tricks (20cp) is worth ~100p, probably less because of inno (but it's a good option if no buffs are up)
        // - prep touch gives extra 225p (or 375p under gs+inno), which is the most efficient use of good (but expensive)
        // - after observe, focused or precise are equivalent; the good is worth extra ~169p (or ~281p under gs)
        // - otherwise replacing observe with precise is decent (effectively finishes half-combo in 1 step)
        // centered condition
        // - TODO consider hasty?
        // sturdy condition
        // - ignored and wasted if we'd like to use inno/gs
        // - otherwise straight upgrade to any action
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
        // - freeCP accounts for inno+gs+byregot and last progress step, so anything >0 can be used for more quality
        // - we really want byregot to be under inno+gs, so using any quality action now will require 32cp for (re)applying gs + 18cp for (re)applying inno unless we have at least 3 steps left
        // - if we have more cp still, we have following options:
        // -- (inno) + finesse + byregot-combo - needs 32cp and no dura, gives 150p quality
        // -- (inno) + prudent + byregot-combo - needs 25cp and 5 dura, gives 150p quality
        // -- (inno) + half-combo + byregot-combo - needs 25-72cp (observe+focused - gs+prep) and 10-20 dura, gives 225-500p quality; inno needs to be reapplied unless at 4 steps
        // -- gs + inno + half-combo - needs 57cp+ and 10+ dura, gives 375p quality; it's something to consider only if inno is not running now
        // -- extra cp/durability can be burned by doing multiple half-combos, but that's a decision to be made on later steps
        // -- if we have tons of cp but not enough durability, we might want to manip; this is reasonable if we have enough cp to do extra 4 half-combos (136 cp minimum + manip cost)
        var freeCP = availableCP - reservedCP;
        var effectiveDura = step.Durability + step.ManipulationLeft * 5;
        if (step.InnovationLeft == 0)
        {
            // gs without inno - we generally just want to inno asap, unless some conditions change the priorities
            if (step.Condition == CraftCondition.Pliant)
            {
                // pliant after GS - this is a decent chance to recover some durability if we need it
                // if we're at last step of GS for whatever reason - assume it's ok to waste it (TODO)
                var duraAction = SolveMidDurabilityQualityPliant(sim, step, freeCP);
                if (duraAction != CraftAction.None)
                    return (duraAction, "mid quality gs-only: durability");
            }

            // TODO: consider good/sturdy prep or good tricks - do we want that without inno? some quick simulation shows it to be a slight loss...
            if (step.Condition == CraftCondition.Good && CanUseActionSafelyInFinisher(sim, step, CraftAction.PreciseTouch, freeCP))
                return (CraftAction.PreciseTouch, "mid quality gs-only: utilize good");
            if (step.PrevComboAction == CraftAction.Observe && CanUseActionSafelyInFinisher(sim, step, CraftAction.FocusedTouch, freeCP))
                return (CraftAction.FocusedTouch, "mid quality gs-only: after observe?"); // this is weird, why would we do gs->observe?.. maybe we're low on cp?

            if (step.GreatStridesLeft == 1)
            {
                // we really want to use gs now on other touches (prudent/finesse), doing inno now would waste it
                // TODO: hasty? basic combo? prep?
                if (CanUseActionSafelyInFinisher(sim, step, CraftAction.PrudentTouch, freeCP))
                    return (CraftAction.PrudentTouch, "mid quality gs-only last chance");
                if (freeCP >= sim.GetCPCost(step, CraftAction.TrainedFinnesse))
                    return (CraftAction.TrainedFinnesse, "mid quality gs-only last chance");
            }

            // inno up
            return (CraftAction.Innovation, "mid quality: gs->inno");
        }

        // inno (or gs+inno) up - do some half-combos
        // our options:
        // - gs + byregot - if we're low on cp or will finish the craft with current quality
        // - manip/mm on pliant if needed
        // - prep / precise if good (or pliant?)
        // - prep on sturdy
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
            if (MidAllowGoodPrep && step.GreatStridesLeft > 0 && CanUseActionSafelyInFinisher(sim, step, CraftAction.PreparatoryTouch, freeCP))
                return (CraftAction.PreparatoryTouch, "mid quality: gs+inno+good");
            // otherwise use precise if possible
            if (CanUseActionSafelyInFinisher(sim, step, CraftAction.PreciseTouch, freeCP))
                return (CraftAction.PreciseTouch, "mid quality: good");
            // otherwise ignore good condition and see what else can we do
            // note: using tricks here seems to be a slight loss according to sim, which is expected
        }

        if (step.Condition == CraftCondition.Sturdy)
        {
            // during sturdy, prep becomes 300/500p for 40cp+10dura = 5.17/8.62 p/cp (depending on gs)
            // in comparison, focused (assuming we did observe before) is 225/375p for 18cp+5dura = 8.33/13.89p/cp - it is more efficient
            // prudent (if we didn't observe) is 150/250p for 25cp+3dura = 4.93/8.22 p/cp
            // so it doesn't really seem to be worth it?..
            if (MidAllowSturdyPrep && step.PrevComboAction != CraftAction.Observe && CanUseActionSafelyInFinisher(sim, step, CraftAction.PreparatoryTouch, freeCP))
                return (CraftAction.PreparatoryTouch, "mid quality: sturdy");
        }

        if (step.Condition == CraftCondition.Pliant && step.GreatStridesLeft != 1)
        {
            // we won't waste gs if we do manip/mm now - see if we want it
            // we don't really care about wasting last step of inno, it's no different from wasting any other step
            var duraAction = SolveMidDurabilityQualityPliant(sim, step, freeCP);
            if (duraAction != CraftAction.None)
                return (duraAction, "mid quality: durability");
            // otherwise ignore pliant and just save some cp on touch actions
        }

        if (step.Condition == CraftCondition.GoodOmen && step.GreatStridesLeft == 0 && step.InnovationLeft > 1)
        {
            // get gs up for gs+inno+good (prep/precise)
            // gs is 32p for at least 225/262.5p (depending on splendorous)
            var nextStepDura = step.Durability + (step.ManipulationLeft > 0 ? 5 : 0);
            if (nextStepDura > 10 && effectiveDura > 20 && freeCP >= 32 + 18)
                return (CraftAction.GreatStrides, "mid quality: good omen gs");
        }

        if (step.PrevComboAction == CraftAction.Observe && CanUseActionSafelyInFinisher(sim, step, CraftAction.FocusedTouch, freeCP))
            return (CraftAction.FocusedTouch, "mid quality"); // complete focused half-combo

        // try spending some durability for using some other half-combo action:
        // - observe + focused if we have enough time on gs/inno is 150p for 25cp
        // - prudent is 100p for 25cp, so less efficient - but useful if we don't have enough time/durability for full half-combo
        // - pliant gs (+ prudent) is extra ~66p for 16cp, so it's an option i guess, especially considering we might get some better condition (TODO consider this)
        // - finesse is 100p for 32cp, which is even less efficient, but does not cost durability
        // - hasty is a fine way to spend excess durability if low on cp
        if (step.InnovationLeft != 1 && step.GreatStridesLeft != 1)
        {
            // observe, if we can do focused on next step, and if we're not going to waste it due to good omen
            // note that on good omen we still prefer using observe rather than waste gs on 100p touch (TODO: consider using something else if gs is not up on good omen)
            var nextStepDura = step.Durability + (step.ManipulationLeft > 0 ? 5 : 0);
            if (nextStepDura > 10 && effectiveDura > 20 && freeCP >= sim.GetCPCost(step, CraftAction.Observe) + 18)
                return (CraftAction.Observe, "mid quality: focused");
        }

        // some less efficient alternatives
        if (CanUseActionSafelyInFinisher(sim, step, CraftAction.PrudentTouch, freeCP))
            return (CraftAction.PrudentTouch, "mid quality: alt");
        if (freeCP >= sim.GetCPCost(step, CraftAction.TrainedFinnesse))
            return (CraftAction.TrainedFinnesse, "mid quality: alt");

        // we're low on cp, see if we can regain some cp via tricks
        var emergencyAction = EmergencyRestoreCP(sim, step);
        if (emergencyAction != CraftAction.None)
            return (emergencyAction, "mid quality: emergency cp");
        if (CanUseActionSafelyInFinisher(sim, step, CraftAction.HastyTouch, freeCP))
            return (CraftAction.HastyTouch, "mid quality: emergency hasty"); // better than nothing i guess...

        // ok, we're out of options - use gs + byregot
        if (step.GreatStridesLeft == 0 && availableCP >= sim.GetCPCost(step, CraftAction.GreatStrides) + 24)
            return (CraftAction.GreatStrides, "mid quality: emergency gs+byregot");
        if (step.Condition is not CraftCondition.Good and not CraftCondition.Excellent && step.Durability > 10)
        {
            // try baiting good
            if (step.GreatStridesLeft != 1 && step.InnovationLeft != 1 && availableCP >= sim.GetCPCost(step, CraftAction.Observe) + 24)
                return (CraftAction.Observe, "mid quality: emergency byregot bait good");
            if (FinisherBaitGoodByregot && step.CarefulObservationLeft > 0)
                return (CraftAction.CarefulObservation, "mid quality: emergency byregot bait good");
        }
        return (CraftAction.ByregotBlessing, "mid quality: emergency byregot");
    }

    // TODO: consider waste-not...
    private CraftAction SolveMidDurabilityPreQuality(Simulator sim, StepState step, int availableCP, bool allowObserveOnLowDura, bool wantProgress)
    {
        // during the mid phase, durability is a serious concern
        if (step.ManipulationLeft > 0 && step.Durability + 5 > sim.Craft.CraftDurability)
            return CraftAction.None; // we're high on dura, doing anything here will waste manip durability

        if (step.Condition == CraftCondition.Pliant)
        {
            // see if we can utilize pliant for manip/mm
            if (step.ManipulationLeft <= 1 && availableCP >= sim.GetCPCost(step, CraftAction.Manipulation))
                return CraftAction.Manipulation;
            if (step.Durability + 30 + (step.ManipulationLeft > 0 ? 5 : 0) <= sim.Craft.CraftDurability && availableCP >= sim.GetCPCost(step, CraftAction.MastersMend))
                return CraftAction.MastersMend;
            return CraftAction.None;
        }

        // primed manipulation is a reasonable action too
        if (MuMePrimedManipPreQuality && step.Condition == CraftCondition.Primed && step.ManipulationLeft == 0 && availableCP >= sim.GetCPCost(step, CraftAction.Manipulation))
            return CraftAction.Manipulation;

        var criticalDurabilityThreshold = step.Condition != CraftCondition.Sturdy ? 10 : 5;
        var wantObserveOnLowDura = allowObserveOnLowDura && step.Condition switch
        {
            CraftCondition.Normal or CraftCondition.Good or CraftCondition.GoodOmen or CraftCondition.Primed => true, // these are all 'observable'
            CraftCondition.Malleable => !wantProgress, // this is useless if we don't need more progress
            _ => false
        };
        var lowDurabilityThreshold = wantObserveOnLowDura ? (step.ManipulationLeft > 0 ? 20 : 25) : criticalDurabilityThreshold;
        if (step.Durability <= lowDurabilityThreshold)
        {
            // we really need to do something about durability, we don't even have useful actions to perform
            if (step.Condition == CraftCondition.Good)
                return CraftAction.TricksOfTrade;
            if (step.ManipulationLeft > 0)
                return CraftAction.Observe; // just regen a bit...
            // TODO: consider careful observation to bait pliant - this sounds much worse than using them to try baiting good byregot
            if (MidBaitPliantWithObservePreQuality)
                return CraftAction.Observe; // try baiting pliant - this will save us 48cp at the cost of ~7+24cp
            if (step.Durability <= criticalDurabilityThreshold)
                return CraftAction.Manipulation; // bait the bullet and manip on normal
        }

        // we still have some durability left, do nothing...
        return CraftAction.None;
    }

    private CraftAction SolveMidDurabilityStartQuality(Simulator sim, StepState step, int availableCP)
    {
        // when we start doing quality, we do a lot of observes/buffs, so effective dura matters more than actual
        var effectiveDura = step.Durability + step.ManipulationLeft * 5;
        if (effectiveDura > sim.Craft.CraftDurability)
            return CraftAction.None; // we're high on dura, doing anything here will waste manip durability

        if (step.Condition == CraftCondition.Pliant)
        {
            return SolveMidDurabilityQualityPliant(sim, step, availableCP);
        }

        if (MuMePrimedManipAfterIQ && step.Condition == CraftCondition.Primed && step.ManipulationLeft == 0 && availableCP >= sim.GetCPCost(step, CraftAction.Manipulation) + EstimateCPToUtilizeDurabilityForQuality(effectiveDura, 5))
        {
            return CraftAction.Manipulation;
        }

        if (effectiveDura <= 10)
        {
            // we're very low on durability - not enough to even byregot
            // try to recover some, even if we can't utilize it well later - at worst we can do some hasty's
            // we really don't want to waste cp on non-pliant manip/mm, so try exploring some alternatives:
            // - observe and wait for pliant, then do normal half-combos (~31cp to save ~48cp)
            // - inno + finesse - quite expensive cp-wise (600p for 146=18+4*32cp = 4.11p/cp), but slightly more effective than using full-cost manip + focused+observe (450p for 116=96/2+18+2*25cp = 3.88p/cp)
            var freeCP = availableCP - (88 + 18 + 32 + 24); // we need at least this much cp to do a normal mm + inno + gs + byregot
            if (MidBaitPliantWithObserveAfterIQ && freeCP >= 7)
                return CraftAction.Observe; // try baiting pliant - this will save us 48cp at the cost of ~7+24cp
            if (freeCP >= 18 + 4 * 32) // inno + 4xfinesse
                return CraftAction.None;
            // just do a normal manip/mm
            if (step.ManipulationLeft <= 1 && availableCP >= sim.GetCPCost(step, CraftAction.Manipulation) + 24)
                return CraftAction.Manipulation;
            if (availableCP >= sim.GetCPCost(step, CraftAction.MastersMend) + 24)
                return CraftAction.MastersMend;
        }

        // TODO: consider doing something (baiting?) if effective durability is <= 20 (enough for one half-combo) or 30 (enough for two half-combos)
        return CraftAction.None;
    }

    private CraftAction SolveMidDurabilityQualityPliant(Simulator sim, StepState step, int availableCP)
    {
        var effectiveDura = step.Durability + step.ManipulationLeft * 5; // since we are going to use a lot of non-dura actions (buffs/observes), this is what really matters
        if (step.ManipulationLeft <= 1 && availableCP >= sim.GetCPCost(step, CraftAction.Manipulation) + EstimateCPToUtilizeDurabilityForQuality(effectiveDura, 4))
            return CraftAction.Manipulation;
        if (effectiveDura + 30 <= sim.Craft.CraftDurability && availableCP >= sim.GetCPCost(step, CraftAction.MastersMend) + EstimateCPToUtilizeDurabilityForQuality(effectiveDura, 3))
            return CraftAction.MastersMend;
        return CraftAction.None;
    }

    private int EstimateCPToUtilizeDurabilityForQuality(int effectiveDura, int extraHalfCombos)
    {
        var estHalfComboCost = 34; // rough baseline - every 10 extra dura is one half-combo, which requires 34cp (1/2 inno + observe+focused) - TODO: should it also include 1/2 GS?
        var estNumHalfCombosWithCurrentDura = effectiveDura <= 20 ? 0 : (effectiveDura + 9) / 10; // 11-20 dura is 0 half-combos, 21-30 is 1, ...
        var estCPNeededToUtilizeCurrentDura = estHalfComboCost * estNumHalfCombosWithCurrentDura;
        return effectiveDura <= 10 ? 0 : estCPNeededToUtilizeCurrentDura + extraHalfCombos * estHalfComboCost;
    }

    private CraftAction SolveMidHighPriorityProgress(Simulator sim, StepState step, bool allowIntensive)
    {
        // high-priority progress actions (exploit conditions)
        if (step.Condition == CraftCondition.Good && allowIntensive && step.Durability > sim.GetDurabilityCost(step, CraftAction.IntensiveSynthesis))
            return CraftAction.IntensiveSynthesis;
        if (step.Condition is CraftCondition.Centered or CraftCondition.Sturdy or CraftCondition.Malleable && step.Durability > sim.GetDurabilityCost(step, CraftAction.RapidSynthesis))
            return CraftAction.RapidSynthesis;
        return CraftAction.None;
    }

    private CraftAction SolveMidHighPriorityIQ(Simulator sim, StepState step, bool allowPrecise)
    {
        if (step.Condition is CraftCondition.Good or CraftCondition.Excellent && allowPrecise && step.Durability > sim.GetDurabilityCost(step, CraftAction.PreciseTouch))
            return CraftAction.PreciseTouch;
        if (step.Condition == CraftCondition.Centered && MidAllowCenteredHasty && step.Durability > sim.GetDurabilityCost(step, CraftAction.HastyTouch))
            return CraftAction.HastyTouch;
        if (step.Condition == CraftCondition.Sturdy && MidAllowSturdyPreсise && (step.HeartAndSoulActive || step.HeartAndSoulAvailable) && step.Durability > sim.GetDurabilityCost(step, CraftAction.PreciseTouch))
            return step.HeartAndSoulActive ? CraftAction.PreciseTouch : CraftAction.HeartAndSoul;
        if (step.Condition == CraftCondition.Sturdy && step.Durability > sim.GetDurabilityCost(step, CraftAction.HastyTouch))
            return MidAllowSturdyHasty ? CraftAction.HastyTouch : sim.NextTouchCombo(step);
        return CraftAction.None;
    }

    // see if we can do gs+inno+byregot right now to get to the quality goal
    private (CraftAction, string) SolveFinishQuality(Simulator sim, StepState step, int availableCP)
    {
        if (step.IQStacks == 0)
            return (CraftAction.None, "fq: no iq"); // we can't even byregot now...

        var missingQuality = sim.Craft.CraftQualityMin3 - step.Quality;
        if (missingQuality <= 0)
            return (CraftAction.None, "fq: at cap"); // we're already at cap

        var byregotDura = sim.GetDurabilityCost(step, CraftAction.ByregotBlessing);
        var byregotCP = sim.GetCPCost(step, CraftAction.ByregotBlessing);
        if (step.Durability <= byregotDura || availableCP < byregotCP)
            return (CraftAction.None, "fq: no cp/dura"); // can't use

        var byregotQuality = sim.CalculateQuality(step, CraftAction.ByregotBlessing);
        if (missingQuality <= byregotQuality)
            return (CraftAction.ByregotBlessing, "fq: immediate"); // byregot now to complete the craft

        if (step.GreatStridesLeft > 1 && step.InnovationLeft == 0 && availableCP >= sim.GetCPCost(step, CraftAction.Innovation) + 24)
        {
            // try [gs]+inno+byregot
            var adjBuffMod = (1 + 0.1f * step.IQStacks) * 2.5f;
            float effPotency = (100 + 20 * step.IQStacks) * adjBuffMod;
            float condMod = step.Condition != CraftCondition.GoodOmen ? 1 : sim.Craft.Splendorous ? 1.75f : 1.5f;
            var adjQuality = (int)(sim.BaseQuality() * condMod * effPotency / 100);
            if (missingQuality <= adjQuality)
                return (CraftAction.Innovation, "fq: inno->byregot");
        }
        else if (step.GreatStridesLeft == 0 && availableCP >= sim.GetCPCost(step, CraftAction.GreatStrides) + 24)
        {
            // try gs+byregot
            var adjBuffMod = (1 + 0.1f * step.IQStacks) * (step.InnovationLeft > 1 ? 2.5f : 2.0f);
            float effPotency = (100 + 20 * step.IQStacks) * adjBuffMod;
            float condMod = step.Condition != CraftCondition.GoodOmen ? 1 : sim.Craft.Splendorous ? 1.75f : 1.5f;
            var adjQuality = (int)(sim.BaseQuality() * condMod * effPotency / 100);
            if (missingQuality <= adjQuality)
                return (CraftAction.GreatStrides, "fq: gs->byregot");

            if (step.InnovationLeft <= 1 && availableCP >= sim.GetCPCost(step, CraftAction.GreatStrides) + 18 + 24)
            {
                // try gs+inno+byregot
                adjBuffMod = (1 + 0.1f * step.IQStacks) * 2.5f;
                effPotency = (100 + 20 * step.IQStacks) * adjBuffMod;
                // condmod is always 1
                adjQuality = (int)(sim.BaseQuality() * effPotency / 100);
                if (missingQuality <= adjQuality)
                    return (CraftAction.GreatStrides, "fq: gs->inno->byregot");
            }
        }

        return (CraftAction.None, "fq: not enough"); // byregot is not enough
    }

    private CraftAction SolveFinishProgress(Simulator sim, StepState step)
    {
        // we have some options to finish the progress:
        // - rapid spam is efficient, but can fail - we use it if we can't do a guaranteed finish
        // - prudent can be thought of converting 11cp into 5 dura, which is less efficient than baiting pliant (which is random to an extent), but more efficient than normal manip
        // current algo:
        // - if we get pliant and have enough cp to do 4 observe+focused after that, we use it
        // - otherwise as long as we have cp, we use most efficient actions; we observe if we're low on dura, trying to bait better conditions
        // - if we're out of cp, we spam rapid, and then finish with careful/basic
        // TODO: veneration outside pliant/good-omen
        // TODO: primed? probably quite pointless at this point...
        if (step.Condition is CraftCondition.Good or CraftCondition.Excellent)
        {
            if (CanUseSynthForFinisher(sim, step, CraftAction.IntensiveSynthesis))
                return CraftAction.IntensiveSynthesis;
            return CraftAction.TricksOfTrade;
        }

        if (step.Condition == CraftCondition.Pliant)
        {
            if (step.ManipulationLeft <= 1 && step.RemainingCP >= 48 + 4 * 12)
                return CraftAction.Manipulation;
            if (step.Durability + 30 + (step.ManipulationLeft > 0 ? 5 : 0) <= sim.Craft.CraftDurability && step.RemainingCP >= 44 + 3 * 12)
                return CraftAction.MastersMend;
            if (step.RemainingCP >= sim.GetCPCost(step, CraftAction.Veneration) && step.VenerationLeft <= 1)
                return CraftAction.Veneration; // good use of pliant
            if (CanUseSynthForFinisher(sim, step, CraftAction.PrudentSynthesis))
                return CraftAction.PrudentSynthesis; // biggest cp cost synth
            // nothing good to use pliant for...
        }

        if (step.Condition == CraftCondition.GoodOmen && step.RemainingCP >= sim.GetCPCost(step, CraftAction.Veneration) + 6 && step.VenerationLeft <= 1)
        {
            return CraftAction.Veneration; // we'll use intensive next...
        }

        // TODO: prioritize rapid during centered?..
        //if (step.Condition is CraftCondition.Centered && step.Durability > sim.GetDurabilityCost(step, CraftAction.RapidSynthesis))
        //    return CraftAction.RapidSynthesis; // use centered condition

        // best possible use of malleable is hs+intensive - but only bother if careful won't suffice
        if (step.Condition == CraftCondition.Malleable && CanUseSynthForFinisher(sim, step, CraftAction.IntensiveSynthesis) && (step.HeartAndSoulAvailable || step.HeartAndSoulActive) && step.Progress + sim.CalculateProgress(step, step.RemainingCP >= 7 ? CraftAction.CarefulSynthesis : CraftAction.BasicSynthesis) < sim.Craft.CraftProgress)
            return step.HeartAndSoulActive ? CraftAction.IntensiveSynthesis : CraftAction.HeartAndSoul;

        if (step.PrevComboAction == CraftAction.Observe && CanUseSynthForFinisher(sim, step, CraftAction.FocusedSynthesis))
            return CraftAction.FocusedSynthesis;

        if (step.Condition is CraftCondition.Normal or CraftCondition.Pliant or CraftCondition.Centered or CraftCondition.Primed && step.ManipulationLeft > 0 && step.Durability <= 10 && step.RemainingCP >= sim.GetCPCost(step, CraftAction.Observe) + 5)
            return CraftAction.Observe; // regen a bit of dura and use focused

        if (CanUseSynthForFinisher(sim, step, CraftAction.CarefulSynthesis))
            return CraftAction.CarefulSynthesis;

        if (CanUseSynthForFinisher(sim, step, CraftAction.PrudentSynthesis))
            return CraftAction.PrudentSynthesis;

        // we're out of cp, use rapids if we have some dura left
        if (sim.GetDurabilityCost(step, CraftAction.RapidSynthesis) < step.Durability)
            return CraftAction.RapidSynthesis;

        // and we're out of dura - finish craft with basic if it's ok, otherwise try rapid
        if (step.Progress + sim.CalculateProgress(step, CraftAction.BasicSynthesis) >= sim.Craft.CraftProgress)
            return CraftAction.BasicSynthesis;

        // try to finish with hs+intensive
        if (step.RemainingCP >= sim.GetCPCost(step, CraftAction.IntensiveSynthesis) && (sim.CanUseAction(step, CraftAction.IntensiveSynthesis) || step.HeartAndSoulAvailable))
            return sim.CanUseAction(step, CraftAction.IntensiveSynthesis) ? CraftAction.IntensiveSynthesis : CraftAction.HeartAndSoul;

        // just pray
        return CraftAction.RapidSynthesis;
    }

    private bool CanUseSynthForFinisher(Simulator sim, StepState step, CraftAction action)
        => step.RemainingCP >= sim.GetCPCost(step, action) && (step.Durability > sim.GetDurabilityCost(step, action) || step.Progress + sim.CalculateProgress(step, action) >= sim.Craft.CraftProgress);

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
}
