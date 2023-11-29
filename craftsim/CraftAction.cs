namespace craftsim;

public enum CraftAction
{
    None,

    BasicSynthesis, // 120p progress, 10dur cost
    CarefulSynthesis, // 180p progress, 7cp + 10 dur cost
    RapidSynthesis, // 500p progress, 10 dur cost, 50% success
    FocusedSynthesis, // 200p progress, 5cp + 10 dur cost, 50% success unless after observe
    Groundwork, // 360p progress, 18cp + 20 dur cost, half potency if durability left is less than required
    IntensiveSynthesis, // 400p progress, 6cp + 10 dur cost, requires good/excellent condition or heart&soul
    PrudentSynthesis, // 180p progress, 18cp + 5 dur cost, can't be used under waste-not
    MuscleMemory, // 300p progress, 6cp + 10 dur cost, requires first step, applies buff

    BasicTouch, // 100p quality, 18cp + 10 dur cost
    StandardTouch, // 125p quality, 18cp + 10 dur cost if used after basic touch (otherwise 32cp)
    AdvancedTouch, // 150p quality, 18cp + 10 dur cost if used after standard touch (otherwise 46cp)
    HastyTouch, // 100p quality, 10 dur cost, 60% success
    FocusedTouch, // 150p quality, 18cp + 10 dur cost, 50% success unless after observe
    PreparatoryTouch, // 200p quality, 40cp + 20 dur cost, 1 extra iq stack
    PreciseTouch, // 150p quality, 18cp + 10 dur cost, 1 extra iq stack, requires good/excellent condition or heart&soul
    PrudentTouch, // 100p quality, 25cp + 5 dur cost, can't be used under waste-not
    TrainedFinnesse, // 100p quality, 32cp cost, requires 10 iq stacks
    Reflect, // 100p quality, 6cp + 10 dur cost, requires first step, 1 extra iq stack

    ByregotBlessing, // 100p+20*IQ quality, 24cp + 10 dur cost, removes iq
    TrainedEye, // max quality, 250cp, requires first step & low level recipe
    DelicateSynthesis, // 100p progress + 100p quality, 32cp + 10 dur cost

    Veneration, // increases progress gains, 18cp cost
    Innovation, // increases quality gains, 18cp cost
    GreatStrides, // next quality action is significantly better, 32cp cost
    TricksOfTrade, // gain 20 cp, requires good/excellent condition or heart&soul
    MastersMend, // gain 30 durability, 88cp cost
    Manipulation, // gain 5 durability/step, 96cp cost
    WasteNot, // reduce durability costs, 56cp cost
    WasteNot2, // reduce durability costs, 98cp cost
    Observe, // do nothing, 7cp cost
    CarefulObservation, // change condition
    FinalAppraisal, // next progress action can't finish craft, does not tick buffs or change conditions, 1cp cost
    HeartAndSoul, // next good-only action can be used without condition, does not tick buffs or change conditions

    Count
}
