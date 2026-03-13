using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    [Range(0.01f, 30f)]
    public float smoothSpeed = 10f;

    [Header("Bounds (Optional)")]
    public bool clampToBounds;
    public Vector2 minBounds = new Vector2(-20f, -20f);
    public Vector2 maxBounds = new Vector2(20f, 20f);

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        var desired = target.position + offset;
        var smoothed = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);

        if (clampToBounds)
        {
            var halfHeight = _camera != null ? _camera.orthographicSize : 5f;
            var halfWidth = _camera != null ? halfHeight * _camera.aspect : 5f;

            smoothed.x = Mathf.Clamp(smoothed.x, minBounds.x + halfWidth, maxBounds.x - halfWidth);
            smoothed.y = Mathf.Clamp(smoothed.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);
        }

        transform.position = smoothed;
    }
}
