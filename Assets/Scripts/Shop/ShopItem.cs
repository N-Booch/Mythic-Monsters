using UnityEngine;

[CreateAssetMenu(menuName = "Shop/Shop Item")]
public class ShopItem : ScriptableObject
{
    public string itemName;
    public int cost;

    [TextArea]
    public string description;

    public TreasureCard treasureReward;
    public ReprieveCard reprieveReward;

    public void Apply(PlayerPawn player)
    {
        if (treasureReward != null)
            player.equippedTreasures.Add(treasureReward);

        if (reprieveReward != null)
            player.hand.Add(reprieveReward);
    }
}
