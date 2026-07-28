using UnityEngine;

/// <summary>
/// Automatically adjusts camera size (orthographicSize or perspective distance/FOV)
/// dynamically based on mobile phone screen aspect ratio, resolution, and orientation.
/// Ensures the entire pool table and UI remain 100% visible on all phone screens without being cut off.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class ResponsiveCameraScaler : MonoBehaviour
{
    [Header("Table Target Bounds")]
    [Tooltip("Total width of the pool table (X-axis). Default: 8.44")]
    public float tableWidth = 8.44f;

    [Tooltip("Total length of the pool table (Z-axis). Default: 14.95")]
    public float tableLength = 14.95f;

    [Header("Screen Padding")]
    [Tooltip("Horizontal padding around the table (X-axis).")]
    public float horizontalPadding = 0.5f;

    [Tooltip("Vertical padding around the table (Z-axis).")]
    public float verticalPadding = 1.0f;

    private Camera cam;
    private int lastScreenWidth = 0;
    private int lastScreenHeight = 0;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        AdjustCameraSize();
    }

    private void Start()
    {
        AdjustCameraSize();
    }

    private void Update()
    {
        // Detect screen resize / orientation change dynamically
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            AdjustCameraSize();
        }
    }

    private void OnValidate()
    {
        if (cam == null) cam = GetComponent<Camera>();
        AdjustCameraSize();
    }

    [ContextMenu("Adjust Camera Size")]
    public void AdjustCameraSize()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        float aspect = cam.aspect;
        if (aspect <= 0.01f) return;

        float targetHalfWidth = (tableWidth / 2f) + horizontalPadding;
        float targetHalfHeight = (tableLength / 2f) + verticalPadding;

        if (cam.orthographic)
        {
            float orthoSizeFromWidth = targetHalfWidth / aspect;
            float orthoSizeFromHeight = targetHalfHeight;

            cam.orthographicSize = Mathf.Max(orthoSizeFromHeight, orthoSizeFromWidth);
        }
        else
        {
            // For perspective camera, adjust height position so table fits
            float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
            float requiredDistHeight = targetHalfHeight / Mathf.Tan(fovRad / 2f);

            float horizFovRad = 2f * Mathf.Atan(Mathf.Tan(fovRad / 2f) * aspect);
            float requiredDistWidth = targetHalfWidth / Mathf.Tan(horizFovRad / 2f);

            float requiredDist = Mathf.Max(requiredDistHeight, requiredDistWidth);
            Vector3 pos = transform.position;
            pos.y = requiredDist;
            transform.position = pos;
        }
    }
}
