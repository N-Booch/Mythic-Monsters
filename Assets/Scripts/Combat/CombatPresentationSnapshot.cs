using System;

[Serializable]
public class CombatPresentationSnapshot
{
    public string mode;
    public string headerText;
    public string playerName;
    public string playerStatsText;
    public string opponentName;
    public string opponentStatsText;
    public string rewardText;
    public string effectText;
    public string diceCountText;
    public string rollText;
    public string statusText;
    public string accentLabel;
    public int playerAttackValue;
    public int playerMightValue;
    public int playerRollValue;
    public int monsterDefenseValue;
    public int diceCountValue;
    public bool hasResolvedRoll;

    public CombatPresentationSnapshot Clone()
    {
        return new CombatPresentationSnapshot
        {
            mode = mode,
            headerText = headerText,
            playerName = playerName,
            playerStatsText = playerStatsText,
            opponentName = opponentName,
            opponentStatsText = opponentStatsText,
            rewardText = rewardText,
            effectText = effectText,
            diceCountText = diceCountText,
            rollText = rollText,
            statusText = statusText,
            accentLabel = accentLabel,
            playerAttackValue = playerAttackValue,
            playerMightValue = playerMightValue,
            playerRollValue = playerRollValue,
            monsterDefenseValue = monsterDefenseValue,
            diceCountValue = diceCountValue,
            hasResolvedRoll = hasResolvedRoll
        };
    }
}
