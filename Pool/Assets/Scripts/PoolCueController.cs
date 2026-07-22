using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the cue stick: aiming (rotation around cue ball) and shooting (charge + release).
/// Supports Mouse, Touch (via Pointer), and Keyboard input using the NEW Input System.
///
/// Controls:
///   MOUSE:   Move to aim | Click+Hold to charge | Release to shoot
///   TOUCH:   Drag to aim | Hold 0.25s to charge | Release to shoot
///   KEYBOARD: Arrow keys / A-D to aim | Space to charge | Release Space to shoot
/// </summary>
public class PoolCueController : MonoBehaviour
{
    [Header("Aim Settings")]
    [Tooltip("Rotation sensitivity. Degrees per pixel of drag.")]
    public float aimSensitivity = 0.2f;

    [Tooltip("Keyboard rotation speed in degrees per second.")]
    public float keyboardAimSpeed = 100f;

    [Header("Power Settings")]
    [Tooltip("Maximum distance the stick can be pulled back while charging.")]
    public float maxPullBackDistance = 1.5f;

    [Tooltip("Speed at which the stick is pulled back during charging.")]
    public float chargeSpeed = 2.0f;

    [Tooltip("Maximum impulse force applied to the cue ball.")]
    public float maxShootForce = 25.0f;

    [Tooltip("Minimum tap force (even with barely any charge).")]
    public float minShootForce = 2.0f;

    [Header("Touch Settings")]
    [Tooltip("Hold duration in seconds before charging starts (prevents accidental shots).")]
    public float holdToChargeDelay = 0.25f;

    // References
    private GameObject cueBall;
    private Rigidbody cueBallRb;

    // Stick state
    private float currentAngle = 0f;
    private float currentPullBack = 0f;
    private bool isCharging = false;
    private float stickLength = 0f;
    private float ballRadius = 0f;
    private float baseGap = 0.15f;

    // Input tracking
    private bool isInputActive = false;
    private Vector2 lastInputPos;
    private float inputHoldTimer = 0f;

    // Aiming prediction lines
    private LineRenderer lineRendererCue;
    private LineRenderer lineRendererTarget;
    private LineRenderer lineRendererDeflect;
    private PoolGameSetup setupHelper;

    private void Start()
    {
        InitializeReferences();
        CreateAimingLines();
    }

    private void OnEnable()
    {
        // Reset state on enable
        currentPullBack = 0f;
        isCharging = false;
        isInputActive = false;
        inputHoldTimer = 0f;

        // Match rotation to current stick-to-ball direction
        if (cueBall != null)
        {
            Vector3 diff = cueBall.transform.position - transform.position;
            diff.y = 0;
            if (diff.magnitude > 0.1f)
            {
                currentAngle = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
            }
        }
    }

    private void OnDisable()
    {
        HideAimingLines();
    }

    private void InitializeReferences()
    {
        setupHelper = FindFirstObjectByType<PoolGameSetup>();
        if (setupHelper != null)
        {
            cueBall = setupHelper.cueBall;
            baseGap = setupHelper.stickBallGap;
            if (cueBall != null)
            {
                cueBallRb = cueBall.GetComponent<Rigidbody>();
                ballRadius = 0.5f * cueBall.transform.localScale.y;
            }
        }

        // Default Cylinder mesh height = 2.0 along local Y
        stickLength = transform.localScale.y * 2f;

        // Ensure stick colliders are disabled so it never physically interferes with balls
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    private bool CanControl()
    {
        return cueBall != null &&
               cueBall.activeSelf &&
               PoolGameManager.Instance != null &&
               PoolGameManager.Instance.currentState == PoolGameManager.GameState.Playing &&
               !PoolGameManager.Instance.IsShotInProgress;
    }

    private void Update()
    {
        if (!CanControl()) return;

        // ── KEYBOARD INPUT ──
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            // Keyboard Aim (Arrow Keys / A-D)
            float keyAim = 0f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) keyAim -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) keyAim += 1f;
            if (keyAim != 0f && !isCharging)
            {
                currentAngle += keyAim * keyboardAimSpeed * Time.deltaTime;
            }

            // Keyboard Charge (Space)
            if (kb.spaceKey.wasPressedThisFrame && !isInputActive)
            {
                isCharging = true;
                currentPullBack = 0f;
            }
            if (kb.spaceKey.isPressed && isCharging && !isInputActive)
            {
                currentPullBack = Mathf.Min(maxPullBackDistance, currentPullBack + chargeSpeed * Time.deltaTime);
            }
            if (kb.spaceKey.wasReleasedThisFrame && !isInputActive)
            {
                if (isCharging && currentPullBack > 0.05f)
                {
                    Shoot();
                }
                isCharging = false;
                currentPullBack = 0f;
            }
        }

        // ── POINTER INPUT (Mouse + Touch unified) ──
        Pointer pointer = Pointer.current;
        if (pointer != null)
        {
            HandlePointerInput(pointer);

            // PC Mouse hover aim fallback (aim by simply moving mouse without clicking)
            if (pointer is Mouse && !pointer.press.isPressed && !isInputActive)
            {
                float mouseDeltaX = pointer.delta.x.ReadValue();
                currentAngle += mouseDeltaX * aimSensitivity * 0.5f;
            }
        }

        // ── UPDATE STICK VISUAL POSITION ──
        UpdateStickTransform();

        // ── DRAW AIMING LINE ──
        DrawAimingLine();
    }

    private void HandlePointerInput(Pointer pointer)
    {
        Vector2 currentPos = pointer.position.ReadValue();

        // ── INPUT DOWN ──
        if (pointer.press.wasPressedThisFrame)
        {
            isInputActive = true;
            lastInputPos = currentPos;
            isCharging = false;
            currentPullBack = 0f;
        }

        // ── INPUT HELD ──
        if (pointer.press.isPressed && isInputActive)
        {
            Vector2 delta = currentPos - lastInputPos;

            // Horizontal movement always aims (rotates) the stick, even while pulling back!
            currentAngle += delta.x * aimSensitivity;

            // Vertical movement (downwards) charges the stick pull-back power
            // Scaled so ~250-300 pixels of downward drag fully charges the stick
            float dragToPowerScale = maxPullBackDistance / 250f;
            float verticalDrag = -delta.y * dragToPowerScale;

            currentPullBack = Mathf.Clamp(currentPullBack + verticalDrag, 0f, maxPullBackDistance);
            
            if (currentPullBack > 0.05f)
            {
                isCharging = true;
            }
            else
            {
                isCharging = false;
            }

            lastInputPos = currentPos;
        }

        // ── INPUT UP ──
        if (pointer.press.wasReleasedThisFrame && isInputActive)
        {
            // Shoot only if there is a significant pull-back charge
            if (isCharging && currentPullBack > 0.1f)
            {
                Shoot();
            }
            else
            {
                // Cancel/reset if released with barely any pull-back
                currentPullBack = 0f;
                isCharging = false;
            }

            isInputActive = false;
        }
    }

    private void UpdateStickTransform()
    {
        if (cueBall == null) return;

        // Calculate rotation around cue ball
        Quaternion rotation = Quaternion.AngleAxis(currentAngle, Vector3.up);

        // Position offset: center of cylinder back by half-length + ball radius + gap + pull-back
        float totalOffset = (stickLength / 2f) + ballRadius + baseGap + currentPullBack;
        Vector3 offsetDir = rotation * new Vector3(0f, 0f, -totalOffset);

        // Place stick behind cue ball
        Vector3 targetPos = cueBall.transform.position + offsetDir;
        targetPos.y = transform.position.y; // Maintain stick height

        transform.position = targetPos;

        // Align stick rotation to point at the cue ball using configured stickRotation X (default 0)
        float rotX = setupHelper != null ? setupHelper.stickRotation.x : 0f;
        transform.rotation = rotation * Quaternion.Euler(rotX, 0f, 0f);
    }

    private void Shoot()
    {
        if (cueBallRb == null) return;

        // Direction from stick towards cue ball (horizontal only)
        Vector3 shootDir = (cueBall.transform.position - transform.position);
        shootDir.y = 0f;
        shootDir.Normalize();

        // Force based on charge amount
        float chargeRatio = currentPullBack / maxPullBackDistance;
        float force = Mathf.Lerp(minShootForce, maxShootForce, chargeRatio);

        // Apply impulse to cue ball
        cueBallRb.AddForce(shootDir * force, ForceMode.Impulse);

        // Reset state
        currentPullBack = 0f;
        isCharging = false;
        isInputActive = false;

        // Hide prediction lines when shot starts
        HideAimingLines();

        // Immediately hide the cue stick GameObject when shot is fired
        gameObject.SetActive(false);

        // Notify game manager
        if (PoolGameManager.Instance != null)
        {
            PoolGameManager.Instance.NotifyShotStarted();
        }
    }

    // ─── AIMING LINE GENERATION ──────────────────────────────

    private void CreateAimingLines()
    {
        // Simple default sprite/line material (works in Standard and Universal Render Pipeline)
        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null) lineShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
        Material lineMaterial = new Material(lineShader);

        lineRendererCue = CreateLineObject("AimLine_Cue", lineMaterial, new Color(1f, 1f, 1f, 0.4f), 0.04f);
        lineRendererTarget = CreateLineObject("AimLine_Target", lineMaterial, new Color(0f, 1f, 0f, 0.7f), 0.04f);
        lineRendererDeflect = CreateLineObject("AimLine_Deflect", lineMaterial, new Color(1f, 0.5f, 0f, 0.4f), 0.04f);
    }

    private LineRenderer CreateLineObject(string name, Material mat, Color color, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.positionCount = 0;
        lr.useWorldSpace = true;
        lr.enabled = false;
        return lr;
    }

    private void HideAimingLines()
    {
        if (lineRendererCue != null) lineRendererCue.enabled = false;
        if (lineRendererTarget != null) lineRendererTarget.enabled = false;
        if (lineRendererDeflect != null) lineRendererDeflect.enabled = false;
    }

    private void DrawAimingLine()
    {
        if (cueBall == null || !gameObject.activeInHierarchy)
        {
            HideAimingLines();
            return;
        }

        // Direction pointing from the cue stick through the cue ball
        Vector3 direction = (cueBall.transform.position - transform.position);
        direction.y = 0f;
        direction.Normalize();

        Vector3 origin = cueBall.transform.position;
        float radius = ballRadius;
        float lineY = origin.y; // Keep lines elevated at the center-height of the balls

        // Temporarily disable the cue ball's collider so the SphereCast doesn't hit itself
        Collider cueCollider = cueBall.GetComponent<Collider>();
        if (cueCollider != null) cueCollider.enabled = false;

        RaycastHit hit;
        // Perform SphereCast to calculate physical ball collision point
        bool hasHit = Physics.SphereCast(origin, radius, direction, out hit, 15f);

        // Re-enable the cue ball's collider immediately
        if (cueCollider != null) cueCollider.enabled = true;

        if (hasHit)
        {
            // Center position of cue ball at point of collision
            Vector3 cueBallImpactCenter = origin + direction * hit.distance;
            cueBallImpactCenter.y = lineY;

            // Draw line from current cue ball center to impact position
            Vector3 startPos = origin;
            startPos.y = lineY;

            lineRendererCue.positionCount = 2;
            lineRendererCue.SetPosition(0, startPos);
            lineRendererCue.SetPosition(1, cueBallImpactCenter);
            lineRendererCue.enabled = true;

            // Check if we hit an object ball or striker (black 8-ball)
            GameObject hitObj = hit.collider.gameObject;
            bool hitBall = hitObj.name.ToLower().Contains("ball") || (setupHelper != null && hitObj == setupHelper.striker);

            if (hitBall && hitObj.GetComponent<Rigidbody>() != null)
            {
                // Target ball projection line (impact center to target center direction)
                Vector3 targetBallPos = hitObj.transform.position;
                targetBallPos.y = lineY;

                Vector3 objectBallDir = (targetBallPos - cueBallImpactCenter);
                objectBallDir.y = 0f;
                objectBallDir.Normalize();

                lineRendererTarget.positionCount = 2;
                lineRendererTarget.SetPosition(0, targetBallPos);
                lineRendererTarget.SetPosition(1, targetBallPos + objectBallDir * 1.5f);
                lineRendererTarget.enabled = true;

                // Deflection path of the cue ball (perpendicular to object ball direction)
                Vector3 tangent = Vector3.Cross(objectBallDir, Vector3.up).normalized;
                if (Vector3.Dot(direction, tangent) < 0f)
                {
                    tangent = -tangent;
                }
                tangent.y = 0f;

                lineRendererDeflect.positionCount = 2;
                lineRendererDeflect.SetPosition(0, cueBallImpactCenter);
                lineRendererDeflect.SetPosition(1, cueBallImpactCenter + tangent * 1.0f);
                lineRendererDeflect.enabled = true;
            }
            else
            {
                // Hit wall or cushion — hide target/deflect paths
                lineRendererTarget.enabled = false;
                lineRendererDeflect.enabled = false;
            }
        }
        else
        {
            // No hit, draw line forward into empty space
            Vector3 startPos = origin;
            startPos.y = lineY;
            Vector3 endPos = origin + direction * 8f;
            endPos.y = lineY;

            lineRendererCue.positionCount = 2;
            lineRendererCue.SetPosition(0, startPos);
            lineRendererCue.SetPosition(1, endPos);
            lineRendererCue.enabled = true;

            lineRendererTarget.enabled = false;
            lineRendererDeflect.enabled = false;
        }
    }
}
