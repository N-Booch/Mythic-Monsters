using UnityEngine;

[CreateAssetMenu(menuName = "Titles/Title")]
public class TitleData : ScriptableObject
{
    public enum TitleEffectType
    {
        StatModifier,
        DrawCard,
        Heal,
        ForcedDiscard,
        SwapTreasure,
        DiceRollEffect,
        ConditionalBuff,
        PassiveRuleChange,
        Special
    }

    public enum TitleTriggerType
    {
        Passive,
        EndTurn,
        StartTurn,
        OnMoveRoll,
        AfterReprieveUse,
        OnPotionUse,
        DuringCombat,
        OnBiomeCheck,
        OnCombatWin
    }

    public string titleName;

    [TextArea]
    public string description;

    public TitleEffectType effectType;
    public TitleTriggerType triggerType;

    public int mightModifier;
    public int arcaneModifier;
    public int hpModifier;

    public int maxHandModifier;
    public int maxEquipModifier;
    public int maxReprievesPerTurnModifier;
    public int movementRollModifier;
    public int runawayModifier;

    public bool blocksReprieveUse;
    public bool blocksRunaway;

    public bool stackable;
}
