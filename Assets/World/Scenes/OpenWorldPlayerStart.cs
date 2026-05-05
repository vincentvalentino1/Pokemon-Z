using UnityEngine;

public class OpenWorldPlayerStart : MonoBehaviour
{
    public string SpawnPointId = "default";
    public bool UseAsDefaultSpawn = true;
    public bool AlignPlayerRotation = true;

    public string NormalizedSpawnPointId =>
        string.IsNullOrWhiteSpace(SpawnPointId)
            ? "default"
            : SpawnPointId.Trim().ToLowerInvariant();

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(SpawnPointId))
            SpawnPointId = "default";
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.22f, 0.8f, 1f, 0.9f);
        Vector3 origin = transform.position;
        Gizmos.DrawWireSphere(origin + Vector3.up * 0.2f, 0.45f);
        Gizmos.DrawLine(origin, origin + transform.forward * 1.4f);
        Gizmos.DrawWireCube(origin + Vector3.up * 0.9f, new Vector3(0.55f, 1.8f, 0.55f));
    }
#endif
}
