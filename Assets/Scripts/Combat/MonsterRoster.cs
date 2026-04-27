using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MonsterRoster : MonoBehaviour
{
    public static MonsterRoster Instance;

    private const string MonsterAssetFolder = "Assets/ScriptableObjects/Monsters";

    private readonly Dictionary<Monster.MonsterBiome, List<Monster>> monstersByBiome = new Dictionary<Monster.MonsterBiome, List<Monster>>();
    private readonly HashSet<Monster> revealedMonsters = new HashSet<Monster>();
    private readonly HashSet<Monster> defeatedMonsters = new HashSet<Monster>();

    private Monster currentPeakMythic;

    public Monster CurrentPeakMythic => currentPeakMythic;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        Initialize();
    }

    private void Start()
    {
        if (GetTotalMonsterCount() == 0)
            Initialize();
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        MonsterRoster existing = FindFirstObjectByType<MonsterRoster>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject rosterObject = new GameObject("MonsterRoster");
        rosterObject.AddComponent<MonsterRoster>();
    }

    public void Initialize()
    {
        monstersByBiome.Clear();
        revealedMonsters.Clear();
        defeatedMonsters.Clear();
        currentPeakMythic = null;

        foreach (Monster.MonsterBiome biome in System.Enum.GetValues(typeof(Monster.MonsterBiome)))
            monstersByBiome[biome] = new List<Monster>();

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:Monster", new[] { MonsterAssetFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Monster monster = AssetDatabase.LoadAssetAtPath<Monster>(path);
            if (monster == null)
                continue;

            monstersByBiome[monster.biome].Add(monster);
        }
#endif

        foreach (List<Monster> biomeMonsters in monstersByBiome.Values)
            ShuffleList(biomeMonsters);

        SelectRandomPeakMythic();

        int totalCount = GetTotalMonsterCount();
        if (totalCount == 0)
            Debug.LogWarning("MonsterRoster could not find any Monster assets.");
        else
            Debug.Log($"MonsterRoster initialized with {totalCount} monsters.");
    }

    public List<Monster> GetAvailableMonsters(Monster.MonsterBiome biome)
    {
        List<Monster> available = new List<Monster>();
        if (!monstersByBiome.TryGetValue(biome, out List<Monster> biomeMonsters))
            return available;

        foreach (Monster monster in biomeMonsters)
        {
            if (monster == null)
                continue;

            if (biome == Monster.MonsterBiome.Peak)
            {
                if (monster == currentPeakMythic && !defeatedMonsters.Contains(monster))
                    available.Add(monster);

                continue;
            }

            if (!defeatedMonsters.Contains(monster))
                available.Add(monster);
        }

        return available;
    }

    public bool IsRevealed(Monster monster)
    {
        return monster != null && revealedMonsters.Contains(monster);
    }

    public bool IsDefeated(Monster monster)
    {
        return monster != null && defeatedMonsters.Contains(monster);
    }

    public void RevealMonster(Monster monster)
    {
        if (monster != null)
            revealedMonsters.Add(monster);
    }

    public void MarkDefeated(Monster monster)
    {
        if (monster == null)
            return;

        defeatedMonsters.Add(monster);
        revealedMonsters.Remove(monster);
    }

    public bool ReplacePeakMythic()
    {
        List<Monster> mythics = GetAllPeakMythics();
        if (mythics.Count <= 1)
            return false;

        Monster previous = currentPeakMythic;
        do
        {
            currentPeakMythic = mythics[Random.Range(0, mythics.Count)];
        }
        while (currentPeakMythic == previous);

        revealedMonsters.Remove(previous);
        Debug.Log($"Peak mythic replaced. A new hidden mythic awaits on the Peak.");
        return true;
    }

    private void SelectRandomPeakMythic()
    {
        List<Monster> mythics = GetAllPeakMythics();
        if (mythics.Count == 0)
            return;

        currentPeakMythic = mythics[Random.Range(0, mythics.Count)];
        revealedMonsters.Remove(currentPeakMythic);
        Debug.Log("A new hidden mythic monster has been placed on the Peak.");
    }

    private List<Monster> GetAllPeakMythics()
    {
        List<Monster> mythics = new List<Monster>();
        if (!monstersByBiome.TryGetValue(Monster.MonsterBiome.Peak, out List<Monster> biomeMonsters))
            return mythics;

        foreach (Monster monster in biomeMonsters)
        {
            if (monster != null && monster.isMythicMonster)
                mythics.Add(monster);
        }

        return mythics;
    }

    private int GetTotalMonsterCount()
    {
        int total = 0;
        foreach (List<Monster> biomeMonsters in monstersByBiome.Values)
            total += biomeMonsters.Count;

        return total;
    }

    private void ShuffleList(List<Monster> monsters)
    {
        if (monsters == null)
            return;

        for (int i = monsters.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            Monster temp = monsters[i];
            monsters[i] = monsters[swapIndex];
            monsters[swapIndex] = temp;
        }
    }
}
