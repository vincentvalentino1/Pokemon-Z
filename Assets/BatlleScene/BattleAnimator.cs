using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BattleAnimator : MonoBehaviour
{
    [Header("─── Tween Timing ───")]
    [Range(0.03f, 0.15f)] public float LungeWindUpTime = 0.06f;
    [Range(0.05f, 0.2f)]  public float LungeForwardTime = 0.08f;
    [Range(0.05f, 0.2f)]  public float LungeReturnTime = 0.10f;
    [Range(0.02f, 0.1f)]  public float HitFlashTime = 0.04f;
    [Range(0.05f, 0.2f)]  public float HitKnockbackTime = 0.12f;
    [Range(0.05f, 0.2f)]  public float HitRecoverTime = 0.10f;

    [Header("─── UI: Turn Transition ───")]
    public Image TurnTransitionOverlay;

    PokemonSpawner _spawner;

    public void Init(PokemonSpawner spawner)
    {
        _spawner = spawner;
    }

    public Transform GetSprite(bool isPlayer) =>
        isPlayer ? _spawner.PlayerModel : _spawner.EnemyModel;

    public Vector3 GetOrigin(bool isPlayer) =>
        isPlayer ? _spawner.PlayerOrigin : _spawner.EnemyOrigin;

    public IEnumerator AttackLunge(bool isPlayer)
    {
        Transform sprite = GetSprite(isPlayer);
        if (sprite == null) yield break;

        Vector3 origin = GetOrigin(isPlayer);
        sprite.localPosition = origin;

        bool isUI = sprite is RectTransform;
        float distance = isUI ? 40f : 0.5f;
        float pullBack = distance * 0.15f;
        Vector3 direction = isPlayer ? Vector3.right : Vector3.left;

        float elapsed = 0f;
        while (elapsed < LungeWindUpTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / LungeWindUpTime);
            sprite.localPosition = Vector3.Lerp(origin, origin - direction * pullBack, EaseOutQuad(t));
            sprite.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.95f, 1.05f, 1f), t);
            yield return null;
        }

        Vector3 windUpPos = sprite.localPosition;
        Vector3 lungeTarget = origin + direction * distance;
        elapsed = 0f;
        while (elapsed < LungeForwardTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / LungeForwardTime);
            sprite.localPosition = Vector3.LerpUnclamped(windUpPos, lungeTarget, EaseOutBack(t));
            sprite.localScale = Vector3.Lerp(new Vector3(0.95f, 1.05f, 1f), new Vector3(1.1f, 0.95f, 1f), t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < LungeReturnTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / LungeReturnTime);
            sprite.localPosition = Vector3.Lerp(lungeTarget, origin, EaseInQuad(t));
            sprite.localScale = Vector3.Lerp(new Vector3(1.1f, 0.95f, 1f), Vector3.one, t);
            yield return null;
        }

        sprite.localPosition = origin;
        sprite.localScale = Vector3.one;
    }

    public IEnumerator HitFlash(bool isPlayer)
    {
        Transform sprite = GetSprite(isPlayer);
        if (sprite == null) yield break;

        SpriteRenderer sr = sprite.GetComponent<SpriteRenderer>();
        Image img = sprite.GetComponent<Image>();
        if (sr == null && img == null) yield break;

        Vector3 origin = GetOrigin(isPlayer);
        Color originalSR  = sr  != null ? sr.color  : Color.white;
        Color originalImg = img != null ? img.color : Color.white;

        bool isUI = sprite is RectTransform;
        float knockDist = isUI ? 20f : 0.25f;
        Vector3 knockDir = isPlayer ? Vector3.left : Vector3.right;

        if (sr != null)  sr.color  = Color.white;
        if (img != null) img.color = Color.white;
        yield return new WaitForSeconds(HitFlashTime);

        float elapsed = 0f;
        Vector3 knockTarget = origin + knockDir * knockDist;
        while (elapsed < HitKnockbackTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / HitKnockbackTime);
            sprite.localPosition = Vector3.Lerp(origin, knockTarget, EaseOutQuad(t));
            sprite.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.05f, 0.95f, 1f), EaseOutQuad(t));
            float colorT = EaseOutQuad(t);
            if (sr != null)  sr.color  = Color.Lerp(Color.white, originalSR, colorT);
            if (img != null) img.color = Color.Lerp(Color.white, originalImg, colorT);
            yield return null;
        }

        elapsed = 0f;
        Vector3 knockPos = sprite.localPosition;
        Vector3 knockScale = sprite.localScale;
        while (elapsed < HitRecoverTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / HitRecoverTime);
            sprite.localPosition = Vector3.Lerp(knockPos, origin, EaseInQuad(t));
            sprite.localScale = Vector3.Lerp(knockScale, Vector3.one, t);
            yield return null;
        }

        sprite.localPosition = origin;
        sprite.localScale = Vector3.one;
        if (sr != null)  sr.color  = originalSR;
        if (img != null) img.color = originalImg;

        for (int i = 0; i < 2; i++)
        {
            if (sr != null) sr.enabled = false;
            if (img != null) img.enabled = false;
            yield return new WaitForSeconds(0.03f);
            if (sr != null) sr.enabled = true;
            if (img != null) img.enabled = true;
            yield return new WaitForSeconds(0.03f);
        }
    }

    public IEnumerator FaintAnimation(bool isPlayer)
    {
        Transform sprite = GetSprite(isPlayer);
        if (sprite == null) yield break;

        SpriteRenderer sr = sprite.GetComponent<SpriteRenderer>();
        Image img = sprite.GetComponent<Image>();

        Vector3 origin = GetOrigin(isPlayer);
        sprite.localPosition = origin;
        bool isUI = sprite is RectTransform;
        float slideDistance = isUI ? 80f : 1.5f;
        float staggerRange = isUI ? 6f : 0.08f;

        float elapsed = 0f;
        while (elapsed < 0.15f)
        {
            elapsed += Time.deltaTime;
            float rx = Random.Range(-staggerRange, staggerRange);
            float ry = Random.Range(-staggerRange, staggerRange);
            sprite.localPosition = origin + new Vector3(rx, ry, 0f);
            yield return null;
        }
        sprite.localPosition = origin;

        Color startSR  = sr  != null ? sr.color  : Color.white;
        Color startImg = img != null ? img.color : Color.white;

        elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / 0.5f);
            float eased = EaseInQuad(progress);

            sprite.localPosition = origin + Vector3.down * eased * slideDistance;

            float alpha = 1f - progress;
            if (sr != null)  sr.color  = new Color(startSR.r,  startSR.g,  startSR.b,  alpha);
            if (img != null) img.color = new Color(startImg.r, startImg.g, startImg.b, alpha);

            yield return null;
        }

        if (sr != null)  sr.enabled = false;
        if (img != null) img.enabled = false;
        sprite.localPosition = origin;
        sprite.localScale = Vector3.one;
    }

    public IEnumerator CameraShake(float intensity, float duration)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        Transform camT = cam.transform;
        Vector3 origin = camT.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float decay = 1f - (elapsed / duration);
            float x = Random.Range(-intensity, intensity) * decay;
            float y = Random.Range(-intensity, intensity) * decay;
            camT.localPosition = origin + new Vector3(x, y, 0f);
            yield return null;
        }

        camT.localPosition = origin;
    }

    public IEnumerator TurnTransitionPunch()
    {
        if (TurnTransitionOverlay != null)
        {
            Color c = Color.black;
            c.a = 0f;
            TurnTransitionOverlay.color = c;
            TurnTransitionOverlay.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < 0.08f)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0f, 0.3f, Mathf.Clamp01(elapsed / 0.08f));
                TurnTransitionOverlay.color = c;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0.3f, 0f, Mathf.Clamp01(elapsed / 0.15f));
                TurnTransitionOverlay.color = c;
                yield return null;
            }

            c.a = 0f;
            TurnTransitionOverlay.color = c;
            TurnTransitionOverlay.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(0.2f);
        }
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
    }

    static float EaseInQuad(float t) => t * t;

    static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
}
