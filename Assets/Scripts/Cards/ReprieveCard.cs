using System;
using UnityEngine;

#region Enums

public enum ReprieveUseTiming
{
    AnyTime,
    BeforeRoll,
    AfterRoll,
    DuringCombat,
    OutOfCombat,
    Passive
}

public enum ReprieveEffectType
{
    Heal,
    Gold,
    DiceReroll,
    DiceSet,
    DiceGold,
    TeleportSelf,
    TeleportOther,
    Damage,
    BuffNextCombat,
    GainTitle,
    StealCard,
    StealTitle,
    Trap,
    Special
}

#endregion

[CreateAssetMenu(menuName = "Cards/Reprieve Card")]
public class ReprieveCard : ScriptableObject
{
    [Header("Info")]
    public string cardName;
    [TextArea] public string description;
    public ReprieveUseTiming useTiming;
    public ReprieveEffectType effectType;

    [Header("Primary Values")]
    public int healAmount;
    public int mightAmount;
    public int arcaneAmount;
    public int goldAmount;
    public int secondaryAmount;

    [Header("Pitfall Modifiers")]
    public int pitfallBonus;

    [Header("Dice Modifiers")]
    public bool allowsReroll;
    public bool allowsSetValue;

    [Header("Special Flags")]
    public bool returnsToHandOnSuccess;
    public bool becomesTitle;

    [NonSerialized] private bool returnToHandThisUse;

    public void Apply(PlayerPawn player)
    {
        switch (effectType)
        {
            case ReprieveEffectType.Heal:
                ApplyHeal(player);
                break;

            case ReprieveEffectType.Gold:
                ApplyGold(player);
                break;

            case ReprieveEffectType.DiceReroll:
                ApplyDiceReroll();
                break;

            case ReprieveEffectType.DiceSet:
                ApplyDiceSet(player);
                break;

            case ReprieveEffectType.DiceGold:
                ApplyDiceGold(player);
                break;

            case ReprieveEffectType.BuffNextCombat:
                ApplyCombatBuff(player);
                break;

            case ReprieveEffectType.TeleportSelf:
                StartTeleportSelf(player);
                break;

            case ReprieveEffectType.TeleportOther:
                StartTeleportOther(player);
                break;

            case ReprieveEffectType.StealTitle:
                StartStealTitle(player);
                break;

            case ReprieveEffectType.StealCard:
                StartStealCard(player);
                break;

            case ReprieveEffectType.Damage:
                ApplyDamage(player);
                break;

            case ReprieveEffectType.GainTitle:
                ApplyGainTitle(player);
                break;

            case ReprieveEffectType.Trap:
                ApplyTrap(player);
                break;

            case ReprieveEffectType.Special:
                ApplySpecial(player);
                break;

            default:
                Debug.LogWarning($"No reprieve implementation found for effect type {effectType}.");
                break;
        }

        if (GameManager.Instance?.playerHUD != null)
            GameManager.Instance.playerHUD.Refresh();
    }

    public bool CanPlay(PlayerPawn player)
    {
        if (GameManager.Instance == null)
            return false;

        bool isDiceManipulationCard = IsDiceManipulationCard();
        bool hasPendingDiceDecision = HasPendingDiceDecision();
        bool isCavePhase =
            GameManager.Instance.CurrentState == GameState.CavePhase ||
            (player != null && player.inCavePhase && GameManager.Instance.GetActivePlayer() == player);
        bool isPilfer = GameManager.Instance.CurrentState == GameState.Pilfer;

        if ((cardName == "Shop Voucher" || cardName == "Mythic Moonstone") &&
            (ShopManager.Instance == null || !ShopManager.Instance.CanUseTradeCard(player)))
            return false;

        if (!GameManager.Instance.CanPlayReprieve(player))
            return false;

        if (isDiceManipulationCard && hasPendingDiceDecision)
            return true;

        if (isPilfer)
        {
            if (effectType != ReprieveEffectType.DiceReroll &&
                effectType != ReprieveEffectType.DiceSet)
                return false;

            switch (useTiming)
            {
                case ReprieveUseTiming.BeforeRoll:
                    return PilferManager.Instance != null &&
                           PilferManager.Instance.IsPilferActive &&
                           !PilferManager.Instance.HasPendingPilferRoll;

                case ReprieveUseTiming.AfterRoll:
                    return PilferManager.Instance != null &&
                           PilferManager.Instance.HasPendingPilferRoll;

                case ReprieveUseTiming.AnyTime:
                    return PilferManager.Instance != null &&
                           PilferManager.Instance.IsPilferActive &&
                           !PilferManager.Instance.IsRollingPilferDice;
            }

            return false;
        }

        if (isCavePhase)
        {
            if (useTiming == ReprieveUseTiming.DuringCombat || useTiming == ReprieveUseTiming.Passive)
                return false;

            if (effectType == ReprieveEffectType.TeleportSelf ||
                effectType == ReprieveEffectType.TeleportOther ||
                effectType == ReprieveEffectType.Trap)
                return false;

            if (DiceManager.Instance != null && DiceManager.Instance.IsRolling)
                return false;

            if (DiceManager.Instance != null && DiceManager.Instance.HasPendingRoll)
            {
                return effectType == ReprieveEffectType.DiceReroll ||
                       effectType == ReprieveEffectType.DiceSet;
            }

            if (isDiceManipulationCard)
                return false;

            return true;
        }

        switch (useTiming)
        {
            case ReprieveUseTiming.BeforeRoll:
                return DiceManager.Instance.CurrentContext == DiceRollContext.None &&
                       (CombatManager.Instance == null || !CombatManager.Instance.HasPendingCombatRoll);

            case ReprieveUseTiming.AfterRoll:
                return hasPendingDiceDecision;

            case ReprieveUseTiming.DuringCombat:
                return GameManager.Instance.CurrentState == GameState.Combat ||
                       DiceManager.Instance.CurrentContext == DiceRollContext.Combat;

            case ReprieveUseTiming.OutOfCombat:
                return GameManager.Instance.CurrentState != GameState.Combat &&
                       DiceManager.Instance.CurrentContext != DiceRollContext.Combat;

            case ReprieveUseTiming.AnyTime:
                if (DiceManager.Instance.IsRolling ||
                    DiceManager.Instance.HasPendingRoll ||
                    (CombatManager.Instance != null &&
                     (CombatManager.Instance.IsRollingCombatDice || CombatManager.Instance.HasPendingCombatRoll)))
                    return false;
                return true;
        }

        return true;
    }

    private bool IsDiceManipulationCard()
    {
        return effectType == ReprieveEffectType.DiceReroll ||
               effectType == ReprieveEffectType.DiceSet;
    }

    private bool HasPendingDiceDecision()
    {
        bool standardDicePending =
            DiceManager.Instance != null &&
            DiceManager.Instance.HasPendingRoll &&
            !DiceManager.Instance.IsRolling;

        bool combatDicePending =
            CombatManager.Instance != null &&
            CombatManager.Instance.HasPendingCombatRoll &&
            !CombatManager.Instance.IsRollingCombatDice;

        bool pilferDicePending =
            PilferManager.Instance != null &&
            PilferManager.Instance.HasPendingPilferRoll &&
            !PilferManager.Instance.IsRollingPilferDice;

        return standardDicePending || combatDicePending || pilferDicePending;
    }

    public bool Play(PlayerPawn player)
    {
        if (!CanPlay(player))
            return false;

        if (TitleSystem.Instance != null && TitleSystem.Instance.TryHandlePotionUse(player, this))
            return true;

        bool requiresPendingResolution = RequiresPendingResolution(player);
        bool handlesOwnAsyncResolution = HandlesOwnAsyncResolution();

        if (requiresPendingResolution)
            GameManager.Instance.BeginPendingReprieve(this, player);

        Apply(player);

        if (!requiresPendingResolution && !handlesOwnAsyncResolution)
            GameManager.Instance.ResolveReprieveCard(this, player);

        return true;
    }

    public bool ShouldReturnToHandOnResolve()
    {
        return returnsToHandOnSuccess || returnToHandThisUse;
    }

    public void ResetRuntimeState()
    {
        returnToHandThisUse = false;
    }

    public void MarkReturnToHandThisUse()
    {
        returnToHandThisUse = true;
    }

    private bool RequiresPendingResolution(PlayerPawn player)
    {
        if (HandlesOwnAsyncResolution())
            return false;

        switch (effectType)
        {
            case ReprieveEffectType.DiceGold:
            case ReprieveEffectType.TeleportSelf:
            case ReprieveEffectType.TeleportOther:
            case ReprieveEffectType.StealCard:
            case ReprieveEffectType.StealTitle:
            case ReprieveEffectType.Trap:
                return true;

            case ReprieveEffectType.Damage:
                return !CanDamageCurrentMonster() || cardName == "Bone Boomerang";

            default:
                return false;
        }
    }

    private bool HandlesOwnAsyncResolution()
    {
        return effectType == ReprieveEffectType.DiceSet && UsesVariableDiceSetValue();
    }

    private void ApplyHeal(PlayerPawn player)
    {
        int before = player.currentHP;
        player.Heal(healAmount);

        Debug.Log(
            $"{player.playerName} healed {player.currentHP - before} HP " +
            $"({player.currentHP}/{player.GetEffectiveMaxHP()})"
        );
    }

    private void ApplyGold(PlayerPawn player)
    {
        if (cardName == "Shop Voucher")
        {
            if (!TryRedeemShopVoucher(player))
                Debug.Log("Shop Voucher can only be used with an active shop or wandering trader.");
            return;
        }

        if (cardName == "Mythic Moonstone")
        {
            if (!TrySellMythicMoonstone(player))
                Debug.Log("Mythic Moonstone can only be sold with an active shop or wandering trader.");
            return;
        }

        player.AddGold(goldAmount);
    }

    private void ApplyDiceReroll()
    {
        if (PilferManager.Instance != null && PilferManager.Instance.CanRerollPendingPilferRoll())
        {
            PilferManager.Instance.RerollPendingPilferRoll();
            return;
        }

        if (CombatManager.Instance != null && CombatManager.Instance.CanRerollPendingCombatRoll())
        {
            CombatManager.Instance.RerollPendingCombatRoll();
            return;
        }

        if (!DiceManager.Instance.CanRerollPending())
        {
            Debug.Log("Cannot reroll - no pending dice roll.");
            return;
        }

        DiceManager.Instance.RerollPending();
    }

    private void ApplyDiceSet(PlayerPawn player)
    {
        if (UsesVariableDiceSetValue())
        {
            DiceValueSelectionUI.EnsureExists();
            DiceValueSelectionUI.Instance.Show(
                "Choose the die value",
                selectedValue =>
                {
                    ApplyForcedDiceValue(selectedValue);
                    GameManager.Instance.ResolveReprieveCard(this, player, false);
                });
            return;
        }

        ApplyForcedDiceValue(secondaryAmount);
    }

    private void ApplyForcedDiceValue(int value)
    {
        if (PilferManager.Instance != null && PilferManager.Instance.CanForcePendingPilferRoll())
        {
            PilferManager.Instance.ForcePendingPilferRoll(value);
            return;
        }

        if (CombatManager.Instance != null && CombatManager.Instance.CanForcePendingCombatRoll())
        {
            CombatManager.Instance.ForcePendingCombatRoll(value);
            return;
        }

        if (!DiceManager.Instance.CanForceResult())
        {
            Debug.Log("Cannot set roll - no pending dice roll.");
            return;
        }

        DiceManager.Instance.ForceResult(value);
    }

    private void ApplyDiceGold(PlayerPawn player)
    {
        Debug.Log($"{cardName} played - rolling for gold");
        GameManager.Instance.LockEndTurn();

        DiceManager.Instance.Roll(
            DiceRollContext.Gold,
            player,
            roll =>
            {
                int goldGained = roll <= 2 ? 200 : roll <= 4 ? 400 : 600;
                player.AddGold(goldGained);

                Debug.Log($"{player.playerName} rolled {roll} and gained {goldGained} gold");

                if (GameManager.Instance?.playerHUD != null)
                    GameManager.Instance.playerHUD.Refresh();

                GameManager.Instance.OnDiceGoldResolved();
                GameManager.Instance.ResolvePendingReprieve();
            }
        );
    }

    private void ApplyCombatBuff(PlayerPawn player)
    {
        Debug.Log($"Buffing next combat stats for {player.playerName}");

        if (mightAmount > 0)
        {
            player.nextCombatMightBonus = Mathf.Min(
                player.nextCombatMightBonus + mightAmount,
                PlayerPawn.hardCap - player.GetEffectiveMight()
            );
        }

        if (arcaneAmount > 0)
        {
            player.nextCombatArcaneBonus = Mathf.Min(
                player.nextCombatArcaneBonus + arcaneAmount,
                PlayerPawn.hardCap - player.GetEffectiveArcane()
            );
        }

        if (healAmount > 0)
            player.Heal(healAmount);
    }

    private void StartTeleportSelf(PlayerPawn player)
    {
        HandUIManager.Instance.ToggleHand(player);
        ShopManager.Instance?.SuspendTradeUI();

        Func<BoardSpace, bool> filter = null;
        if (cardName == "Danger Seeker Stone")
            filter = space => space.spaceType == SpaceType.Monster;

        BoardTargetingManager.Instance.StartSelectSpace(
            selectedSpace =>
            {
                ExecuteTeleport(player, selectedSpace);
                ShopManager.Instance?.ResumeTradeUI();
                GameManager.Instance.ResolvePendingReprieve(false);
            },
            filter
        );
    }

    private void StartTeleportOther(PlayerPawn user)
    {
        HandUIManager.Instance.ToggleHand(user);
        ShopManager.Instance?.SuspendTradeUI();

        var eligibleTargets = new System.Collections.Generic.List<PlayerPawn>();
        if (GameManager.Instance != null && GameManager.Instance.players != null)
        {
            foreach (PlayerPawn candidate in GameManager.Instance.players)
            {
                if (candidate == null || candidate == user || candidate.isDead)
                    continue;

                eligibleTargets.Add(candidate);
            }
        }

        if (eligibleTargets.Count == 0)
        {
            Debug.Log($"{cardName} has no valid player targets.");
            ShopManager.Instance?.ResumeTradeUI();
            GameManager.Instance.CancelPendingReprieve();
            return;
        }

        PlayerSelectionUI.EnsureExists();
        PlayerSelectionUI.Instance.BeginSelection(
            eligibleTargets,
            $"{user.playerName}: choose a player to teleport",
            target =>
            {
                if (target == null)
                {
                    ShopManager.Instance?.ResumeTradeUI();
                    GameManager.Instance.CancelPendingReprieve();
                    return;
                }

                Func<BoardSpace, bool> filter = null;
                if (cardName == "Pitfall Peridot")
                    filter = space => space.spaceType == SpaceType.Pitfall;

                BoardTargetingManager.Instance.StartSelectSpace(
                    selectedSpace =>
                    {
                        ExecuteTeleport(target, selectedSpace);
                        ShopManager.Instance?.ResumeTradeUI();
                        GameManager.Instance.ResolvePendingReprieve(false);
                    },
                    filter
                );
            },
            $"Cancel {cardName}");
    }

    private void StartStealTitle(PlayerPawn user)
    {
        HandUIManager.Instance.ToggleHand(user);
        TitleUIManager.EnsureExists();
        ShopManager.Instance?.SuspendTradeUI();

        var eligibleTargets = new System.Collections.Generic.List<PlayerPawn>();
        if (GameManager.Instance != null && GameManager.Instance.players != null)
        {
            foreach (PlayerPawn candidate in GameManager.Instance.players)
            {
                if (candidate == null || candidate == user || candidate.isDead)
                    continue;

                if (!candidate.CanHaveTitlePilfered())
                    continue;

                if (!candidate.HasTitles())
                    continue;

                eligibleTargets.Add(candidate);
            }
        }

        if (eligibleTargets.Count == 0)
        {
            Debug.Log("Title Transfer has no valid targets.");
            ShopManager.Instance?.ResumeTradeUI();
            GameManager.Instance.CancelPendingReprieve();
            return;
        }

        PlayerSelectionUI.EnsureExists();
        PlayerSelectionUI.Instance.BeginSelection(
            eligibleTargets,
            $"{user.playerName}: choose a player to steal a title from",
            target =>
            {
                if (target == null)
                {
                    ShopManager.Instance?.ResumeTradeUI();
                    GameManager.Instance.CancelPendingReprieve();
                    return;
                }

                TitleUIManager.Instance.BeginTitleSelection(
                    target,
                    $"{user.playerName}: choose a title to steal from {target.playerName}",
                    selectedTitle =>
                    {
                        if (selectedTitle == null)
                        {
                            ShopManager.Instance?.ResumeTradeUI();
                            GameManager.Instance.CancelPendingReprieve();
                            return;
                        }

                        target.RemoveTitle(selectedTitle);
                        user.AddTitle(selectedTitle);

                        Debug.Log($"{user.playerName} stole title '{selectedTitle.titleName}' from {target.playerName}");

                        TitleUIManager.Instance.Hide();
                        ShopManager.Instance?.ResumeTradeUI();
                        GameManager.Instance.ResolvePendingReprieve();
                    },
                    true,
                    "Cancel Title Transfer");
            },
            "Cancel Title Transfer");
    }

    private void StartStealCard(PlayerPawn user)
    {
        HandUIManager.Instance.ToggleHand(user);
        ShopManager.Instance?.SuspendTradeUI();

        var eligibleTargets = new System.Collections.Generic.List<PlayerPawn>();
        if (GameManager.Instance != null && GameManager.Instance.players != null)
        {
            foreach (PlayerPawn candidate in GameManager.Instance.players)
            {
                if (candidate == null || candidate == user || candidate.isDead || !candidate.HasReprieveCards())
                    continue;

                eligibleTargets.Add(candidate);
            }
        }

        if (eligibleTargets.Count == 0)
        {
            Debug.Log("Hoarder's Honey has no valid player targets.");
            ShopManager.Instance?.ResumeTradeUI();
            GameManager.Instance.CancelPendingReprieve();
            return;
        }

        StealCardUI.Instance.Show(
            $"{user.playerName}: choose a player to steal a reprieve from",
            () =>
            {
                ShopManager.Instance?.ResumeTradeUI();
                GameManager.Instance.CancelPendingReprieve();
            });

        PlayerSelectionUI.EnsureExists();
        PlayerSelectionUI.Instance.BeginSelection(
            eligibleTargets,
            $"{user.playerName}: choose a player to steal a reprieve from",
            target =>
            {
                if (target == null)
                {
                    StealCardUI.Instance.Hide();
                    ShopManager.Instance?.ResumeTradeUI();
                    GameManager.Instance.CancelPendingReprieve();
                    return;
                }

                StealCardUI.Instance.Show(
                    $"{user.playerName}: choose 1 reprieve to steal from {target.playerName}",
                    () =>
                    {
                        ShopManager.Instance?.ResumeTradeUI();
                        GameManager.Instance.CancelPendingReprieve();
                    });

                HandUIManager.Instance.BeginCardSelection(
                    target,
                    $"{user.playerName}: choose 1 reprieve to steal from {target.playerName}",
                    stolenCard =>
                    {
                        if (stolenCard == null)
                        {
                            StealCardUI.Instance.Hide();
                            ShopManager.Instance?.ResumeTradeUI();
                            GameManager.Instance.CancelPendingReprieve();
                            return;
                        }

                        target.hand.Remove(stolenCard);
                        user.hand.Add(stolenCard);

                        StealCardUI.Instance.Hide();
                        HandUIManager.Instance.Refresh();
                        ShopManager.Instance?.ResumeTradeUI();
                        GameManager.Instance.ResolvePendingReprieve();
                    }
                );
            },
            "Cancel Hoarder's Honey");
    }

    private void ApplyDamage(PlayerPawn player)
    {
        if (CanDamageCurrentMonster())
        {
            Monster monster = CombatManager.Instance.CurrentMonster;
            monster.TakeDamage(secondaryAmount);

            if (cardName == "Bone Boomerang")
            {
                BeginBoneBoomerangReturnRoll(player);
                return;
            }

            CombatManager.Instance.OnMonsterDamagedExternally();

            return;
        }

        if (cardName == "Bomb")
        {
            StartBombTargetSelection(player);
            return;
        }

        PlayerTargetingManager.Instance.StartSelectPlayer(target =>
        {
            if (target == null || target == player)
                return;

            target.TakeDamage(secondaryAmount);
            ShopManager.Instance?.ResumeTradeUI();
            GameManager.Instance.ResolvePendingReprieve();
        });
    }

    private bool CanDamageCurrentMonster()
    {
        return GameManager.Instance != null &&
               GameManager.Instance.CurrentState == GameState.Combat &&
               CombatManager.Instance != null &&
               CombatManager.Instance.CurrentMonster != null;
    }

    private void StartBombTargetSelection(PlayerPawn user)
    {
        if (user == null || GameManager.Instance == null || GameManager.Instance.players == null)
        {
            GameManager.Instance?.CancelPendingReprieve();
            return;
        }

        var eligibleTargets = GetEligibleBombTargets(user);
        if (eligibleTargets.Count == 0)
        {
            Debug.Log("Bomb has no valid player targets in the same biome.");
            GameManager.Instance.CancelPendingReprieve();
            return;
        }

        ShopManager.Instance?.SuspendTradeUI();
        PlayerSelectionUI.EnsureExists();
        PlayerSelectionUI.Instance.BeginSelection(
            eligibleTargets,
            $"{user.playerName}: choose a player in the same biome to damage with Bomb",
            selectedTarget =>
            {
                ShopManager.Instance?.ResumeTradeUI();

                if (selectedTarget == null)
                {
                    GameManager.Instance.CancelPendingReprieve();
                    return;
                }

                selectedTarget.TakeDamage(secondaryAmount);
                Debug.Log($"{user.playerName} used Bomb on {selectedTarget.playerName} for {secondaryAmount} damage.");
                GameManager.Instance.ResolvePendingReprieve();
            },
            "Cancel Bomb");
    }

    private System.Collections.Generic.List<PlayerPawn> GetEligibleBombTargets(PlayerPawn user)
    {
        var eligibleTargets = new System.Collections.Generic.List<PlayerPawn>();
        bool isCavePhase = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.CavePhase;
        int userBiomeIndex = user.GetCurrentBiomeIndex();

        foreach (PlayerPawn target in GameManager.Instance.players)
        {
            if (target == null || target == user || target.isDead)
                continue;

            bool sameContext = isCavePhase
                ? target.inCavePhase || target == GameManager.Instance.GetActivePlayer() || user.inCavePhase
                : target.GetCurrentBiomeIndex() == userBiomeIndex;

            if (sameContext)
                eligibleTargets.Add(target);
        }

        return eligibleTargets;
    }

    private void BeginBoneBoomerangReturnRoll(PlayerPawn player)
    {
        if (player == null || DiceManager.Instance == null)
        {
            FinalizeBoneBoomerangReturn(UnityEngine.Random.Range(1, 7));
            return;
        }

        CombatHUDUI.Instance?.SetStatus("Bone Boomerang return roll ready. Confirm or use dice cards.");
        DiceManager.Instance.Roll(
            DiceRollContext.Special,
            player,
            FinalizeBoneBoomerangReturn);
    }

    private void FinalizeBoneBoomerangReturn(int roll)
    {
        Debug.Log($"Bone Boomerang return roll: {roll}");

        if (roll >= 5)
        {
            returnToHandThisUse = true;
            CombatHUDUI.Instance?.SetStatus($"Bone Boomerang returns to hand on a roll of {roll}.");
        }
        else
        {
            CombatHUDUI.Instance?.SetStatus($"Bone Boomerang does not return on a roll of {roll}.");
        }

        CombatManager.Instance?.OnMonsterDamagedExternally();
        GameManager.Instance?.ResolvePendingReprieve();
    }

    private void ApplyGainTitle(PlayerPawn player)
    {
        TitleData grantedTitle = CreateTitleFromCard();
        if (grantedTitle == null)
        {
            Debug.LogWarning($"No title could be created for {cardName}.");
            return;
        }

        player.AddTitle(grantedTitle);
    }

    private void ApplyTrap(PlayerPawn player)
    {
        HandUIManager.Instance.ToggleHand(player);

        BoardSpace currentSpace = player.currentSpace ?? BoardManager.Instance.GetSpaceAt(player.currentIndex);
        if (currentSpace == null)
        {
            Debug.LogWarning("Cannot place trap because the player's current space could not be found.");
            GameManager.Instance.CancelPendingReprieve();
            return;
        }

        currentSpace.PlaceMonsterSnare(player, Mathf.Max(secondaryAmount, 3));
        Debug.Log($"{player.playerName} placed {cardName} on space {currentSpace.spaceIndex}.");
        GameManager.Instance.ResolvePendingReprieve();
    }

    private void ApplySpecial(PlayerPawn player)
    {
        if (cardName == "Mythical Mulligan")
        {
            MonsterRoster.EnsureExists();
            bool replaced = MonsterRoster.Instance != null && MonsterRoster.Instance.ReplacePeakMythic();
            if (replaced)
                Debug.Log($"{player.playerName} used Mythical Mulligan. A new hidden Mythic Monster now waits on the Peak.");
            else
                Debug.Log("Mythical Mulligan had no effect because no alternate Mythic Monster was available.");
            return;
        }

        Debug.Log($"{cardName} is recognized as a special reprieve but still needs its custom rules implemented.");
    }

    private TitleData CreateTitleFromCard()
    {
        if (!becomesTitle)
            return null;

        if (cardName == "Chosen Chalice")
        {
            TitleData title = ScriptableObject.CreateInstance<TitleData>();
            title.titleName = "Chosen";
            title.description = "Chosen (+1 Might and Arcane)";
            title.effectType = TitleData.TitleEffectType.StatModifier;
            title.triggerType = TitleData.TitleTriggerType.Passive;
            title.mightModifier = 1;
            title.arcaneModifier = 1;
            title.stackable = false;
            return title;
        }

        return null;
    }

    private bool TryRedeemShopVoucher(PlayerPawn player)
    {
        return ShopManager.Instance != null && ShopManager.Instance.RedeemShopVoucher(player);
    }

    private bool TrySellMythicMoonstone(PlayerPawn player)
    {
        return ShopManager.Instance != null && ShopManager.Instance.SellMythicMoonstone(player, goldAmount);
    }

    private void ExecuteTeleport(PlayerPawn target, BoardSpace selectedSpace)
    {
        if (selectedSpace == null)
        {
            Debug.LogWarning("Teleport failed because no board space was selected.");
            return;
        }

        target.transform.position = selectedSpace.transform.position;
        target.currentIndex = selectedSpace.spaceIndex;
        target.currentSpace = selectedSpace;

        selectedSpace.OnPlayerLand(target);
    }

    private bool UsesVariableDiceSetValue()
    {
        return cardName == "Chrono Cookie";
    }
}
