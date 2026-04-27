using UnityEngine;

public class TitleSystem : MonoBehaviour
{
    public static TitleSystem Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Trigger(TitleData.TitleTriggerType trigger, PlayerPawn contextPlayer = null)
    {
        if (GameManager.Instance == null || GameManager.Instance.players == null)
            return;

        foreach (PlayerPawn player in GameManager.Instance.players)
        {
            foreach (TitleData title in player.titles)
            {
                if (!player.IsTitleEffectActive(title) || title.triggerType != trigger)
                    continue;

                ResolveTitleEffect(player, title, contextPlayer);
            }
        }
    }

    private void ResolveTitleEffect(PlayerPawn player, TitleData title, PlayerPawn contextPlayer)
    {
        switch (title.titleName)
        {
            case "Abstemious":
                ResolveAbstemious(player, contextPlayer);
                return;

            case "Jolly":
                ResolveJolly(player, contextPlayer);
                return;

            case "Lazy":
                ResolveLazy(player, contextPlayer);
                return;

            case "Lucky":
                ResolveLucky(player, contextPlayer);
                return;

            case "Clumsy":
                ResolveClumsy(player, contextPlayer);
                return;

            case "Chaotic":
            case "Cautious":
            case "Dehydrated":
            case "Lustful":
                return;
        }

        switch (title.effectType)
        {
            case TitleData.TitleEffectType.DrawCard:
                if (player == contextPlayer)
                    player.DrawReprieveCard();
                break;

            case TitleData.TitleEffectType.Heal:
                if (player == contextPlayer && title.hpModifier > 0)
                    player.Heal(title.hpModifier);
                break;
        }
    }

    private void ResolveAbstemious(PlayerPawn player, PlayerPawn contextPlayer)
    {
        if (player != contextPlayer)
            return;

        if (GameManager.Instance.reprievesUsedThisTurn > 0)
            return;

        int healAmount = player.GetEffectiveMaxHP() - player.currentHP;
        if (healAmount > 0)
            player.Heal(healAmount);
    }

    private void ResolveJolly(PlayerPawn player, PlayerPawn contextPlayer)
    {
        if (player != contextPlayer)
            return;

        int roll = Random.Range(1, 7);
        if (roll >= 5)
            player.DrawReprieveCard();
    }

    private void ResolveLazy(PlayerPawn player, PlayerPawn contextPlayer)
    {
        if (player != contextPlayer)
            return;

        player.Heal(2);
    }

    private void ResolveLucky(PlayerPawn player, PlayerPawn contextPlayer)
    {
        if (player != contextPlayer)
            return;

        player.DrawReprieveCard();
    }

    private void ResolveClumsy(PlayerPawn player, PlayerPawn contextPlayer)
    {
        if (player != contextPlayer)
            return;

        ReprieveCard discardedCard = player.RemoveRandomCardFromHand();
        if (discardedCard == null)
            return;

        ReprieveDeck.Instance.Discard(discardedCard);
        Debug.Log($"{player.playerName} discarded {discardedCard.cardName} due to Clumsy.");
    }

    public bool TryHandlePotionUse(PlayerPawn player, ReprieveCard card)
    {
        if (player == null || card == null)
            return false;

        if (!player.HasTitle("Dehydrated") || !IsPotionCard(card))
            return false;

        GameManager.Instance?.BeginPendingReprieve(card, player);
        GameManager.Instance?.LockEndTurn();

        DiceManager.Instance.Roll(
            DiceRollContext.Special,
            player,
            roll => ResolveDehydratedPotionRoll(player, card, roll));

        return true;
    }

    public bool ShouldForceCautiousRunaway(PlayerPawn player)
    {
        return player != null &&
               player.HasTitle("Cautious") &&
               player.currentHP > 0 &&
               player.currentHP <= 3;
    }

    private void ResolveDehydratedPotionRoll(PlayerPawn player, ReprieveCard card, int roll)
    {
        if (player == null || card == null)
        {
            GameManager.Instance?.CancelPendingReprieve();
            return;
        }

        if (roll <= 2)
        {
            Debug.Log($"{player.playerName}'s Dehydrated title prevented {card.cardName} from taking effect.");
            GameManager.Instance?.ResolvePendingReprieve();
            return;
        }

        if (roll == 6)
            card.MarkReturnToHandThisUse();

        card.Apply(player);
        GameManager.Instance?.ResolvePendingReprieve();

        if (roll == 6)
            Debug.Log($"{player.playerName}'s Dehydrated title returned {card.cardName} to hand.");
    }

    private bool IsPotionCard(ReprieveCard card)
    {
        return card != null &&
               !string.IsNullOrWhiteSpace(card.cardName) &&
               card.cardName.IndexOf("Potion", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
