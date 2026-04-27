public static class CombatSceneState
{
    public static CombatPresentationSnapshot Snapshot { get; private set; }

    public static bool HasSnapshot => Snapshot != null;

    public static void SetSnapshot(CombatPresentationSnapshot snapshot)
    {
        Snapshot = snapshot?.Clone();
    }

    public static void Clear()
    {
        Snapshot = null;
    }
}
