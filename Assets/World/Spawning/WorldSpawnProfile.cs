using UnityEngine;

[System.Serializable]
public class SpawnProfileEntry
{
    [Header("Identity")]
    public PokemonData Species;
    
    [Tooltip("Optional. If left blank, defaults to the prefab named by the Species Pokedex.")]
    public GameObject SpawnPrefabOverride;

    [Header("Leveling")]
    [Range(1, 100)] public int MinLevel = 2;
    [Range(1, 100)] public int MaxLevel = 5;

    [Header("Frequency")]
    [Tooltip("Spawn weight relative to other valid entries.")]
    [Range(1, 100)] public int Weight = 30;

    [Header("AI & Personality")]
    [Tooltip("Dictates awareness, chase mechanics, and speed parameters for this species in the wild.")]
    public WorldPokemonBehaviorProfile BehaviorProfile;
}

[CreateAssetMenu(fileName = "New Spawn Profile", menuName = "Game Content/Pokemon/Route Spawn Profile")]
public class WorldSpawnProfile : ScriptableObject
{
    [Header("Spawn Configuration")]
    public SpawnProfileEntry[] SpawnTable;
}
