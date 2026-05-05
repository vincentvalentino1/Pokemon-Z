using System;
using UnityEngine;

[Serializable]
public class WorldPokemonRuntimeData
{
    public string UniqueID { get; private set; }
    public PokemonInstance Instance { get; private set; }
    
    // Encounter parameters
    public WorldPokemonBehaviorProfile BehaviorProfile { get; set; }
    public Vector3 WorldPosition { get; set; }
    public Quaternion WorldRotation { get; set; }
    public bool DestroyAfterEncounter { get; set; }
    
    public WorldPokemonRuntimeData(PokemonInstance instance, WorldPokemonBehaviorProfile behaviorProfile, bool destroyAfterEncounter)
    {
        UniqueID = System.Guid.NewGuid().ToString();
        Instance = instance;
        BehaviorProfile = behaviorProfile;
        DestroyAfterEncounter = destroyAfterEncounter;
    }
}
