using UnityEngine;

public class OpenWorldCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform Target;

    [Header("Follow")]
    public Vector3 Offset = new Vector3(0f, 8f, -6f);
    public float PositionSmooth = 8f;

    [Header("Look At")]
    public bool RotateTowardsTarget = true;
    public float RotationSmooth = 10f;

    void LateUpdate()
    {
        if (TryGetComponent<OpenWorldMouseLook>(out _))
            return;

        if (Target == null)
            TryResolveTarget();

        if (Target == null)
            return;

        Vector3 desiredPosition = Target.position + Offset;
        float positionLerp = Mathf.Max(0f, PositionSmooth) * Time.deltaTime;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionLerp);

        if (!RotateTowardsTarget)
            return;

        Vector3 lookDirection = Target.position - transform.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        float rotationLerp = Mathf.Max(0f, RotationSmooth) * Time.deltaTime;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerp);
    }

    void TryResolveTarget()
    {
        if (OpenWorldSceneLookup.TryGetPlayerTransform(out Transform playerTransform))
            Target = playerTransform;
    }
}
