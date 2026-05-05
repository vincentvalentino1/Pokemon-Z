using UnityEngine;

[RequireComponent(typeof(Collider))]
public class OpenWorldScenePortal : MonoBehaviour
{
    [Header("Destination")]
    public string TargetSceneName = "";
    public string TargetSpawnPointId = "default";

    [Header("Activation")]
    public bool TriggerOnEnter = true;
    public bool RequireButtonPress = false;
    public KeyCode InteractionKey = KeyCode.E;
    public float TravelCooldown = 0.4f;

    Transform _playerInside;
    float _lastTravelTime = -10f;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        _playerInside = other.transform;
        if (TriggerOnEnter && !RequireButtonPress)
            TryTravel();
    }

    void OnTriggerExit(Collider other)
    {
        if (_playerInside == other.transform)
            _playerInside = null;
    }

    void Update()
    {
        if (!RequireButtonPress || _playerInside == null)
            return;

        if (WasInteractPressed())
            TryTravel();
    }

    void TryTravel()
    {
        if (Time.unscaledTime - _lastTravelTime < TravelCooldown)
            return;

        if (OpenWorldPauseMenu.IsOpen || OpenWorldEncounterPromptUI.IsOpen)
            return;

        OpenWorldEncounterManager session = OpenWorldEncounterManager.EnsureSessionExists();
        if (session == null || !session.TravelToScene(TargetSceneName, TargetSpawnPointId))
            return;

        _lastTravelTime = Time.unscaledTime;
    }

    static bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player") || other.GetComponentInParent<OpenWorldPlayerController>() != null)
            return true;

        return OpenWorldSceneLookup.TryGetPlayerTransform(out Transform player) &&
               other.transform.root == player.root;
    }

    bool WasInteractPressed()
    {
        return Input.GetKeyDown(InteractionKey);
    }
}
