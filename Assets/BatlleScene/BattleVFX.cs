using UnityEngine;

public class BattleVFX : MonoBehaviour
{
    [Header("─── Effect Prefabs ───")]
    public GameObject HitEffectPrefab;
    public GameObject CritEffectPrefab;
    public GameObject FaintEffectPrefab;
    public GameObject SuperEffectivePrefab;

    PokemonSpawner _spawner;

    void Awake()
    {
        _spawner = GetComponentInParent<PokemonSpawner>();
        if (_spawner == null)
            _spawner = FindObjectOfType<PokemonSpawner>();
    }

    void OnEnable()
    {
        BattleEvents.OnDamageDealt += HandleDamage;
        BattleEvents.OnCriticalHit += HandleCrit;
        BattleEvents.OnSuperEffective += HandleEffectiveness;
        BattleEvents.OnFainted += HandleFaint;
    }

    void OnDisable()
    {
        BattleEvents.OnDamageDealt -= HandleDamage;
        BattleEvents.OnCriticalHit -= HandleCrit;
        BattleEvents.OnSuperEffective -= HandleEffectiveness;
        BattleEvents.OnFainted -= HandleFaint;
    }

    void HandleDamage(PokemonInstance target, int amount, bool isPlayer)
    {
        Transform pos = isPlayer ? _spawner?.PlayerModel : _spawner?.EnemyModel;
        SpawnEffect(HitEffectPrefab, pos);
    }

    void HandleCrit(bool isCrit)
    {
        if (isCrit) SpawnEffect(CritEffectPrefab, null);
    }

    void HandleEffectiveness(float eff)
    {
        if (eff > 1.5f) SpawnEffect(SuperEffectivePrefab, null);
    }

    void HandleFaint(PokemonInstance pokemon)
    {
        SpawnEffect(FaintEffectPrefab, null);
    }

    void SpawnEffect(GameObject prefab, Transform target)
    {
        if (prefab == null) return;
        Vector3 pos = target != null ? target.position : Vector3.zero;
        GameObject fx = Instantiate(prefab, pos, Quaternion.identity);
        Destroy(fx, 3f);
    }
}
