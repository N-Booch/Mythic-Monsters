using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatHUDUI : MonoBehaviour
{
    public static CombatHUDUI Instance;

    private CombatPresentationSnapshot snapshot = new CombatPresentationSnapshot();

    private GameObject panel;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI matchupText;
    private TextMeshProUGUI monsterStatsText;
    private TextMeshProUGUI rewardText;
    private TextMeshProUGUI effectText;
    private TextMeshProUGUI rollText;
    private TextMeshProUGUI statusText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureRuntimeUI();
        Hide();
    }

    public static CombatHUDUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        CombatHUDUI existing = FindFirstObjectByType<CombatHUDUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("CombatHUDUI");
        CombatHUDUI manager = managerObject.AddComponent<CombatHUDUI>();
        manager.EnsureRuntimeUI();
        manager.Hide();
        return manager;
    }

    public void ShowCombat(PlayerPawn player, Monster monster)
    {
        EnsureRuntimeUI();
        panel.SetActive(true);
        snapshot.mode = "combat";
        snapshot.rollText = string.Empty;
        snapshot.playerAttackValue = 0;
        Refresh(player, monster);
        SetStatus("Combat active. Play reprieves or click Roll.");
        SetPendingRoll(null, 0);
    }

    public void ShowPilfer(PlayerPawn attacker, PlayerPawn defender, PlayerPawn currentRollPlayer)
    {
        EnsureRuntimeUI();
        panel.SetActive(true);
        snapshot.mode = "pilfer";
        snapshot.rollText = string.Empty;
        snapshot.playerAttackValue = 0;
        RefreshPilfer(attacker, defender, currentRollPlayer);
        SetStatus("Pilfer active. Current player may play dice cards or click Roll.");
        SetPendingRoll(null, 0);
    }

    public void Refresh(PlayerPawn player, Monster monster)
    {
        if (panel == null || player == null || monster == null)
            return;

        int baseMight = player.GetEffectiveMight();
        int baseArcane = player.GetEffectiveArcane();
        int combatMight = player.GetCombatMight();
        int combatArcane = player.GetCombatArcane();
        int mightBonus = combatMight - baseMight;
        int arcaneBonus = combatArcane - baseArcane;

        headerText.text = monster.isMythicMonster ? "Mythic Combat" : "Combat";
        snapshot.headerText = headerText.text;
        snapshot.accentLabel = monster.isMythicMonster ? "Peak Duel" : "Board Encounter";
        snapshot.diceCountText = $"Dice: {GetCombatDiceCount(combatArcane)}";
        snapshot.playerMightValue = combatMight;
        if (!snapshot.hasResolvedRoll)
        {
            snapshot.playerRollValue = GetCombatDiceCount(combatArcane) <= 0 ? 0 : 0;
            snapshot.playerAttackValue = GetCombatDiceCount(combatArcane) <= 0 ? combatMight : 0;
        }
        snapshot.monsterDefenseValue = monster.might;
        snapshot.diceCountValue = GetCombatDiceCount(combatArcane);
        matchupText.text =
            $"{player.GetDisplayNameWithTitles(42)}\n" +
            $"{FormatCombatStatText("Might", baseMight, mightBonus)}  |  {FormatCombatStatText("Arcane", baseArcane, arcaneBonus)}  |  HP {player.currentHP}/{player.GetEffectiveMaxHP()}";
        snapshot.playerName = player.GetDisplayNameWithTitles(42);
        snapshot.playerStatsText = $"{FormatCombatStatText("Might", baseMight, mightBonus)}  |  {FormatCombatStatText("Arcane", baseArcane, arcaneBonus)}  |  HP {player.currentHP}/{player.GetEffectiveMaxHP()}";

        monsterStatsText.text =
            $"{monster.monsterName}\n" +
            $"Monster HP {monster.health}/{monster.maxHP}  |  Monster Might {monster.might}";
        snapshot.opponentName = monster.monsterName;
        snapshot.opponentStatsText = $"Monster HP {monster.health}/{monster.maxHP}  |  Monster Might {monster.might}";

        rewardText.text = monster.isMythicMonster
            ? "Reward: Victory"
            : $"Reward: {monster.RewardTitleName}";
        snapshot.rewardText = rewardText.text;

        if (monster.isMythicMonster && !string.IsNullOrWhiteSpace(monster.mythicEffectDescription))
            effectText.text = $"Effect: {monster.mythicEffectDescription}";
        else if (!monster.isMythicMonster && !string.IsNullOrWhiteSpace(monster.RewardTitleDescription))
            effectText.text = $"Effect: {monster.RewardTitleDescription}";
        else
            effectText.text = string.Empty;

        snapshot.effectText = effectText.text;
        PushSnapshot();
    }

    private string FormatCombatStatText(string label, int baseValue, int bonus)
    {
        if (bonus == 0)
            return $"{label} {baseValue}";

        string sign = bonus > 0 ? "+" : string.Empty;
        return $"{label} {baseValue} ({sign}{bonus})";
    }

    public void RefreshPilfer(PlayerPawn attacker, PlayerPawn defender, PlayerPawn currentRollPlayer)
    {
        if (panel == null || attacker == null || defender == null)
            return;

        headerText.text = "Pilfer";
        snapshot.headerText = headerText.text;
        snapshot.accentLabel = "Roadside Ambush";
        snapshot.diceCountText = string.Empty;
        snapshot.playerMightValue = attacker.GetCombatMight(false);
        if (!snapshot.hasResolvedRoll)
        {
            snapshot.playerRollValue = 0;
            snapshot.playerAttackValue = 0;
        }
        snapshot.monsterDefenseValue = defender.GetCombatMight(false);
        snapshot.diceCountValue = GetCombatDiceCount(attacker.GetCombatArcane(false));
        matchupText.text =
            $"{attacker.GetDisplayNameWithTitles(42)}\n" +
            $"Might {attacker.GetCombatMight(false)}  |  Arcane {attacker.GetCombatArcane(false)}  |  HP {attacker.currentHP}/{attacker.GetEffectiveMaxHP()}";
        snapshot.playerName = attacker.GetDisplayNameWithTitles(42);
        snapshot.playerStatsText = $"Might {attacker.GetCombatMight(false)}  |  Arcane {attacker.GetCombatArcane(false)}  |  HP {attacker.currentHP}/{attacker.GetEffectiveMaxHP()}";

        monsterStatsText.text =
            $"{defender.GetDisplayNameWithTitles(42)}\n" +
            $"Might {defender.GetCombatMight(false)}  |  Arcane {defender.GetCombatArcane(false)}  |  HP {defender.currentHP}/{defender.GetEffectiveMaxHP()}";
        snapshot.opponentName = defender.GetDisplayNameWithTitles(42);
        snapshot.opponentStatsText = $"Might {defender.GetCombatMight(false)}  |  Arcane {defender.GetCombatArcane(false)}  |  HP {defender.currentHP}/{defender.GetEffectiveMaxHP()}";

        rewardText.text = "Winner steals 1 reprieve. If the loser dies, winner may also take 1 treasure.";
        snapshot.rewardText = rewardText.text;
        effectText.text = currentRollPlayer != null
            ? $"Current roll: {currentRollPlayer.GetDisplayNameWithTitles(42)}"
            : string.Empty;
        snapshot.effectText = effectText.text;
        PushSnapshot();
    }

    public void SetPendingRoll(int[] results, int total)
    {
        if (rollText == null)
            return;

        if (results == null)
        {
            int[] displayedResults = CombatManager.Instance != null ? CombatManager.Instance.GetDisplayedCombatRollResults() : null;
            if (snapshot.mode == "combat" && displayedResults != null && displayedResults.Length > 0 && CombatManager.Instance != null && CombatManager.Instance.CurrentPlayer != null)
            {
                int combatMight = CombatManager.Instance.CurrentPlayer.GetCombatMight();
                int displayedTotal = 0;
                string[] displayedParts = new string[displayedResults.Length];
                for (int i = 0; i < displayedResults.Length; i++)
                {
                    displayedTotal += displayedResults[i];
                    displayedParts[i] = $"[{displayedResults[i]}]";
                }

                snapshot.playerMightValue = combatMight;
                snapshot.playerRollValue = displayedTotal;
                snapshot.playerAttackValue = combatMight + displayedTotal;
                snapshot.diceCountValue = displayedResults.Length;
                snapshot.hasResolvedRoll = true;
                rollText.text = $"Roll: {string.Join(" ", displayedParts)}   Sum {displayedTotal}   ATK {snapshot.playerAttackValue}";
            }
            else if (snapshot.mode == "pilfer" && displayedResults != null && displayedResults.Length > 0)
            {
                int displayedTotal = 0;
                string[] displayedParts = new string[displayedResults.Length];
                for (int i = 0; i < displayedResults.Length; i++)
                {
                    displayedTotal += displayedResults[i];
                    displayedParts[i] = $"[{displayedResults[i]}]";
                }

                snapshot.playerRollValue = displayedTotal;
                snapshot.playerAttackValue = snapshot.playerMightValue + displayedTotal;
                snapshot.diceCountValue = displayedResults.Length;
                snapshot.hasResolvedRoll = true;
                rollText.text = $"Roll: {string.Join(" ", displayedParts)}   Sum {displayedTotal}   ATK {snapshot.playerAttackValue}";
            }
            else
            {
                rollText.text = string.Empty;
                snapshot.hasResolvedRoll = false;
                snapshot.playerRollValue = 0;
                if (snapshot.mode == "combat" && CombatManager.Instance != null && CombatManager.Instance.CurrentPlayer != null)
                {
                    int combatMight = CombatManager.Instance.CurrentPlayer.GetCombatMight();
                    snapshot.playerMightValue = combatMight;
                    snapshot.playerAttackValue = snapshot.diceCountValue <= 0 ? combatMight : 0;
                }
                else if (snapshot.mode == "pilfer")
                {
                    snapshot.playerAttackValue = 0;
                }
            }
            snapshot.rollText = rollText.text;
            PushSnapshot();
            return;
        }

        if (results.Length == 0)
        {
            rollText.text = "No dice rolled";
            snapshot.hasResolvedRoll = true;
            snapshot.playerRollValue = 0;
            if (snapshot.mode == "combat" && CombatManager.Instance != null && CombatManager.Instance.CurrentPlayer != null)
            {
                snapshot.playerMightValue = CombatManager.Instance.CurrentPlayer.GetCombatMight();
                snapshot.playerAttackValue = snapshot.playerMightValue;
            }
            else if (snapshot.mode == "pilfer")
            {
                snapshot.playerAttackValue = snapshot.playerMightValue;
            }
            snapshot.rollText = rollText.text;
            PushSnapshot();
            return;
        }

        string[] parts = new string[results.Length];
        for (int i = 0; i < results.Length; i++)
            parts[i] = $"[{results[i]}]";

        if (CombatManager.Instance != null && CombatManager.Instance.CurrentPlayer != null && snapshot.mode == "combat")
        {
            int combatTotal = CombatManager.Instance.CurrentPlayer.GetCombatMight() + total;
            snapshot.playerMightValue = CombatManager.Instance.CurrentPlayer.GetCombatMight();
            snapshot.playerRollValue = total;
            rollText.text = $"Roll: {string.Join(" ", parts)}   Sum {total}   ATK {combatTotal}";
            snapshot.playerAttackValue = combatTotal;
            snapshot.monsterDefenseValue = CombatManager.Instance.CurrentMonster != null ? CombatManager.Instance.CurrentMonster.might : snapshot.monsterDefenseValue;
            snapshot.diceCountValue = results.Length;
            snapshot.hasResolvedRoll = true;
        }
        else if (snapshot.mode == "pilfer")
        {
            snapshot.playerRollValue = total;
            snapshot.playerAttackValue = snapshot.playerMightValue + total;
            snapshot.diceCountValue = results.Length;
            snapshot.hasResolvedRoll = true;
            rollText.text = $"Roll: {string.Join(" ", parts)}   Sum {total}   ATK {snapshot.playerAttackValue}";
        }
        else
        {
            rollText.text = $"Roll: {string.Join(" ", parts)}   Sum {total}";
        }

        snapshot.rollText = rollText.text;
        PushSnapshot();
    }

    public void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;

        snapshot.statusText = text;
        PushSnapshot();
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);

        CombatSceneState.Clear();
    }

    private void EnsureRuntimeUI()
    {
        if (panel != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("RuntimeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        panel = new GameObject("CombatHUDPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = new Vector2(560f, 420f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.16f, 0.07f, 0.07f, 0.92f);

        headerText = CreateText("Header", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -14f), new Vector2(-18f, -58f), 31f, FontStyles.Bold);
        matchupText = CreateText("Matchup", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -72f), new Vector2(-18f, -156f), 23f, FontStyles.Normal);
        monsterStatsText = CreateText("MonsterStats", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -168f), new Vector2(-18f, -252f), 23f, FontStyles.Normal);
        rewardText = CreateText("Reward", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -264f), new Vector2(-18f, -302f), 21f, FontStyles.Bold);
        effectText = CreateText("Effect", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -308f), new Vector2(-18f, -352f), 18f, FontStyles.Italic);
        rollText = CreateText("Roll", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -356f), new Vector2(-18f, -390f), 19f, FontStyles.Normal);
        statusText = CreateText("Status", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -392f), new Vector2(-18f, -424f), 18f, FontStyles.Normal);

        matchupText.textWrappingMode = TextWrappingModes.Normal;
        monsterStatsText.textWrappingMode = TextWrappingModes.Normal;
        effectText.textWrappingMode = TextWrappingModes.Normal;
        statusText.textWrappingMode = TextWrappingModes.Normal;
        rollText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void PushSnapshot()
    {
        CombatSceneState.SetSnapshot(snapshot);
    }

    private int GetCombatDiceCount(int arcane)
    {
        if (arcane <= -3)
            return 0;
        if (arcane <= 2)
            return 1;
        if (arcane <= 5)
            return 2;
        if (arcane <= 8)
            return 3;
        if (arcane <= 11)
            return 4;
        return 5;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float fontSize,
        FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        return text;
    }
}
