using UnityEngine;
using UnityEngine.UI;

public class PokemonSpawner : MonoBehaviour
{
    [Header("─── Spawn Points ───")]
    public Transform PlayerSpawnPoint;
    public Transform EnemySpawnPoint;

    public Transform PlayerModel { get; private set; }
    public Transform EnemyModel { get; private set; }

    public Vector3 PlayerOrigin { get; private set; }
    public Vector3 EnemyOrigin { get; private set; }

    public void SpawnBoth(PokemonInstance player, PokemonInstance enemy)
    {
        DestroyAll();
        PlayerModel = SpawnModel(player, PlayerSpawnPoint);
        EnemyModel = SpawnModel(enemy, EnemySpawnPoint);
        PlayerOrigin = PlayerModel != null ? PlayerModel.localPosition : Vector3.zero;
        EnemyOrigin = EnemyModel != null ? EnemyModel.localPosition : Vector3.zero;
    }

    public void DestroyAll()
    {
        if (PlayerModel != null)
        {
            Destroy(PlayerModel.gameObject);
            PlayerModel = null;
        }
        if (EnemyModel != null)
        {
            Destroy(EnemyModel.gameObject);
            EnemyModel = null;
        }
    }

    public void ResetVisuals()
    {
        ResetSpriteVisual(PlayerModel, PlayerOrigin);
        ResetSpriteVisual(EnemyModel, EnemyOrigin);
    }

    static void ResetSpriteVisual(Transform t, Vector3 origin)
    {
        if (t == null) return;
        t.localPosition = origin;
        t.localScale = Vector3.one;
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        Image img = t.GetComponent<Image>();
        if (sr != null) { sr.color = Color.white; sr.enabled = true; }
        if (img != null) { img.color = Color.white; img.enabled = true; }
    }

    Transform SpawnModel(PokemonInstance pokemon, Transform spawnPoint)
    {
        if (pokemon == null || pokemon.SpeciesData == null || spawnPoint == null)
            return null;

        string path = "Pokemon/" + pokemon.SpeciesData.PokemonName;
        GameObject prefab = Resources.Load<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogWarning($"[PokemonSpawner] No prefab found at Resources/{path}");
            return null;
        }

        GameObject spawned = Instantiate(prefab, spawnPoint);
        spawned.transform.localPosition = Vector3.zero;
        spawned.transform.localRotation = Quaternion.identity;
        return spawned.transform;
    }
}
