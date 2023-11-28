namespace craftsim;

public enum CraftCondition
{
    Normal,
    Good, // 1.75x quality (with splendorous tools)
    Excellent,
    Poor,
    Centered, // +25% success rate
    Sturdy, // -50% durability loss
    Pliant, // -50% cp cost
    Malleable, // x1.5 progress increase
    Primed, // +2 steps for buffs
    GoodOmen, // next is good

    Count
}
