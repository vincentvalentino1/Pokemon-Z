using UnityEngine;

public enum AIBehaviorType
{
    Passive,        // Wanders, ignores player (or flees on physical contact depending on implementation)
    Aggressive,     // Sees player, pauses (Notices), then chases
    Fleeing         // Sees player and runs away
}

[CreateAssetMenu(fileName = "New Behavior Profile", menuName = "Game Content/Pokemon/Behaviour Profile")]
public class WorldPokemonBehaviorProfile : ScriptableObject
{
    [Header("Behavior Type")]
    public AIBehaviorType BehaviorType = AIBehaviorType.Passive;

    [Header("Detection")]
    [Tooltip("How close the player must be for this Pokemon to notice them.")]
    public float NoticeRadius = 8f;
    
    [Tooltip("How close the player must be to immediately trigger an encounter.")]
    public float EngageRadius = 1.5f;

    [Header("Locomotion")]
    [Tooltip("Standard wandering speed.")]
    public float RoamSpeed = 1.7f;
    
    [Tooltip("Speed when chasing the player or fleeing.")]
    public float ChaseSpeed = 4.5f;

    [Tooltip("Turn speed in logic and visual smoothing.")]
    public float RotationSpeed = 8f;

    [Header("Territory")]
    [Tooltip("How far the Pokemon will wander from its central spawn origin.")]
    public float RoamRadius = 5f;

    [Tooltip("How far the Pokemon is willing to chase before giving up and returning. Measured from spawn origin.")]
    public float LeashDistance = 15f;

    [Tooltip("How long to wait after losing distance before fully returning to normal state.")]
    public float DisengageTime = 2f;
}
