using UnityEngine;

[CreateAssetMenu(menuName = "Cards/Treasure Card")]
public class TreasureCard : ScriptableObject
{
    public string cardName = "Treasure";

    [TextArea]
    public string description = "";

    [Header("Shop Rules")]
    public int cost = 1000;
    public bool stackable = false;

    [Header("Passive Modifiers")]
    public int hpModifier = 0;
    public int mightModifier = 0;
    public int arcaneModifier = 0;
    public int maxHandModifier = 0;
    public int maxEquipModifier = 0;
    public int pitfallBonus = 0;
    public int runawayModifier = 0;

    [Header("Rule Flags")]
    public bool blocksRunaway = false;
    public bool blocksReprieveUse = false;

    [Header("Future Effects")]
    public TreasureEffectType effectType = TreasureEffectType.Passive;
    public TreasureTriggerType triggerType = TreasureTriggerType.Passive;
}

public enum TreasureEffectType
{
    Passive,
    StatModifier,
    DrawCards,
    GainGold,
    Heal,
    CombatModifier,
    PitfallModifier,
    Special
}

public enum TreasureTriggerType
{
    Passive,
    StartTurn,
    EndTurn,
    OnMoveRoll,
    OnLand,
    DuringCombat,
    CavePhase,
    OnDeath
}
