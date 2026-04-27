using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Monster")]
public class Monster : ScriptableObject
{
    public enum MythicEffectType
    {
        None,
        BurnOnFailedRoll,
        Special
    }

    public enum MonsterBiome
    {
        Mountain,
        Forest,
        Wasteland,
        Ocean,
        Peak
    }

    [Header("Monster Info")]
    public string monsterName = "Slayer Satan";
    public MonsterBiome biome = MonsterBiome.Mountain;
    public bool isMythicMonster = false;
    [TextArea] public string mythicEffectDescription = "";

    [Header("Combat Stats")]
    public int maxHP = 6;
    public int might = 4;

    [Header("Mythic Combat Effect")]
    public MythicEffectType mythicEffectType = MythicEffectType.None;
    public int mythicEffectValue = 0;

    [Header("Rewards")]
    public TitleData rewardTitle;

    public string RewardTitleName => rewardTitle != null ? rewardTitle.titleName : string.Empty;
    public string RewardTitleDescription => rewardTitle != null ? rewardTitle.description : string.Empty;

    public int health;

    public bool IsDead => health <= 0;

    private void OnEnable()
    {
        ResetRuntimeState();
    }

    public void ResetRuntimeState()
    {
        health = maxHP;
    }

    public void PrepareForCombat()
    {
        ResetRuntimeState();
    }

    public void TakeDamage(int amount)
    {
        health = Mathf.Max(health - amount, 0);
        Debug.Log($"{monsterName} takes {amount} damage (HP {health})");
    }
}
