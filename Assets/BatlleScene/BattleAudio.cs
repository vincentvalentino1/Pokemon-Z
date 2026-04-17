using UnityEngine;

public class BattleAudio : MonoBehaviour
{
    [Header("─── Audio Clips ───")]
    public AudioClip HitNormalClip;
    public AudioClip HitSuperEffectiveClip;
    public AudioClip HitNotEffectiveClip;
    public AudioClip CriticalHitClip;
    public AudioClip FaintClip;
    public AudioClip MoveSelectClip;

    AudioSource _source;

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        if (_source == null)
            _source = gameObject.AddComponent<AudioSource>();
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
        Play(HitNormalClip);
    }

    void HandleCrit(bool isCrit)
    {
        if (isCrit) Play(CriticalHitClip);
    }

    void HandleEffectiveness(float eff)
    {
        if (eff > 1.5f) Play(HitSuperEffectiveClip);
        else if (eff < 0.9f && eff > 0.01f) Play(HitNotEffectiveClip);
    }

    void HandleFaint(PokemonInstance pokemon)
    {
        Play(FaintClip);
    }

    void Play(AudioClip clip)
    {
        if (clip != null && _source != null)
            _source.PlayOneShot(clip);
    }
}
