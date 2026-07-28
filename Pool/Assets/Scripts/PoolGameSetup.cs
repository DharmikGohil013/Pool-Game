using System.Collections.Generic;
using UnityEngine;

public class PoolGameSetup : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The pool table GameObject. If null, we will look for 'Table' in the scene.")]
    public GameObject table;

    [Tooltip("The White Cue Ball. If null, we will look for 'White Cue Ball' under the parent.")]
    public GameObject cueBall;

    [Tooltip("The Black Striker Ball. If null, we will look for 'Striker' under the parent.")]
    public GameObject striker;

    [Tooltip("The Cue Stick GameObject. If null, we will look for 'Cylinder', 'Stick' or 'CueStick' in the scene.")]
    public GameObject cueStick;

    [Tooltip("List of object balls. If empty, we will auto-detect them from the children of this object.")]
    public List<GameObject> objectBalls = new List<GameObject>();

    [Header("Physics Settings")]
    [Tooltip("Mass of each pool ball.")]
    public float ballMass = 1.0f;

    [Tooltip("Linear drag of each ball to simulate rolling resistance.")]
    public float ballDrag = 0.8f;

    [Tooltip("Angular drag of each ball to simulate spin resistance.")]
    public float ballAngularDrag = 0.8f;

    [Tooltip("Friction of the ball physics material.")]
    public float ballFriction = 0.05f;

    [Tooltip("Bounciness of the ball physics material.")]
    public float ballBounciness = 0.65f;

    [Tooltip("Friction of the table surface physics material.")]
    public float tableFriction = 0.15f;

    [Tooltip("Bounciness of the table surface physics material (0.0 prevents vertical tennis-ball bouncing).")]
    public float tableBounciness = 0.0f;

    [Header("Rack Layout Settings")]
    [Tooltip("Z-coordinate offset from the table center for the cue ball (head spot).")]
    public float cueBallZOffset = -3.5f;

    [Tooltip("Z-coordinate offset from the table center for the rack apex (foot spot).")]
    public float rackApexZOffset = 3.5f;

    [Tooltip("Extra spacing gap factor to prevent balls overlapping on start (1.0 = tight pack, 1.002 = perfect tight rack).")]
    public float spacingFactor = 1.002f;

    [Tooltip("Rotation applied to racked balls so numbers face upright to the camera.")]
    public Vector3 ballRotation = new Vector3(0f, 0f, 0f);

    [Header("Stick Setup Settings")]
    [Tooltip("Gap between the stick tip and the cue ball.")]
    public float stickBallGap = 0.15f;

    [Tooltip("Rotation of the cue stick.")]
    public Vector3 stickRotation = new Vector3(5.5f, 0f, 0f);

    private void Start()
    {
        // Automatically set up the game at startup
        SetupGame();
    }

    [ContextMenu("Setup Game")]
    public void SetupGame()
    {
        AutoFindReferences();
        ApplyPhysicsSettings();
        PositionBalls();
        PositionStick();
        Debug.Log("Pool Game Setup completed successfully!");
    }

    /// <summary>
    /// Automatically searches the scene and children for references if they are not assigned.
    /// </summary>
    [ContextMenu("Auto-Find References")]
    public void AutoFindReferences()
    {
        // Find Table
        if (table == null)
        {
            table = GameObject.Find("pool_table_scene");
            if (table == null) table = GameObject.Find("Table");
            if (table == null)
            {
                foreach (GameObject go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    string name = go.name.ToLower();
                    if (name.Contains("table") || name.Contains("pool_table"))
                    {
                        table = go;
                        break;
                    }
                }
            }
        }

        // Find Cue Ball, Striker and Object Balls among children of searchRoot or in scene
        Transform searchRoot = transform;
        
        GameObject ballParent = GameObject.Find("ball");
        if (ballParent != null)
        {
            searchRoot = ballParent.transform;
        }

        if (searchRoot != null && searchRoot.childCount > 0)
        {
            if (cueBall == null)
            {
                foreach (Transform child in searchRoot)
                {
                    string name = child.name.ToLower();
                    if (name.Contains("white") || name.Contains("cue"))
                    {
                        cueBall = child.gameObject;
                        break;
                    }
                }
            }

            if (striker == null)
            {
                foreach (Transform child in searchRoot)
                {
                    string name = child.name.ToLower();
                    if (child.gameObject != cueBall && (name.Contains("black") || name.Contains("striker") || name == "8"))
                    {
                        striker = child.gameObject;
                        break;
                    }
                }
            }

            // Populate object balls if list is empty or incomplete
            objectBalls = new List<GameObject>();
            for (int i = 0; i < searchRoot.childCount; i++)
            {
                GameObject child = searchRoot.GetChild(i).gameObject;
                
                // Skip Cue Ball and Striker
                if (child == cueBall || child == striker)
                    continue;

                objectBalls.Add(child);
            }
        }

        // Find Cue Stick in scene
        if (cueStick == null)
        {
            cueStick = GameObject.Find("CueStick");
            if (cueStick == null) cueStick = GameObject.Find("Cylinder");
            if (cueStick == null) cueStick = GameObject.Find("Stick");
            if (cueStick == null)
            {
                foreach (GameObject go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    string name = go.name.ToLower();
                    if (name.Contains("stick") || name.Contains("cylinder"))
                    {
                        if (go != table && go != cueBall && go != striker && !objectBalls.Contains(go))
                        {
                            cueStick = go;
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies Rigidbody and Collider configurations with proper physical settings to Table and Balls.
    /// </summary>
    [ContextMenu("Apply Physics Settings")]
    public void ApplyPhysicsSettings()
    {
        // 1. Table Physics Setup: Remove BoxColliders and attach MeshColliders to Table and all child meshes
        if (table != null)
        {
            // Create Table Physics Material
            PhysicsMaterial tableMat = new PhysicsMaterial
            {
                name = "TablePhysicsMaterial",
                staticFriction = tableFriction,
                dynamicFriction = tableFriction,
                bounciness = 0.0f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            // Remove BoxColliders from table object itself and children (except Hall pocket triggers)
            BoxCollider[] oldBoxes = table.GetComponentsInChildren<BoxCollider>(true);
            foreach (BoxCollider box in oldBoxes)
            {
                if (box == null) continue;
                if (box.gameObject.name.ToLower().Contains("hall") || (box.transform.parent != null && box.transform.parent.name.ToLower().Contains("hall")))
                    continue;

                DestroyImmediate(box);
            }

            // Ensure MeshCollider is attached ONLY to table mesh (pooltable_low), NOT ceiling_light
            MeshFilter[] meshFilters = table.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                string name = mf.gameObject.name.ToLower();
                if (name.Contains("hall") || name.Contains("light") || name.Contains("ceiling") || name.Contains("lamp"))
                    continue;
                if (mf.transform.parent != null)
                {
                    string pName = mf.transform.parent.name.ToLower();
                    if (pName.Contains("hall") || pName.Contains("light") || pName.Contains("ceiling"))
                        continue;
                }

                MeshCollider mc = mf.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = mf.gameObject.AddComponent<MeshCollider>();
                }
                mc.sharedMesh = mf.sharedMesh;
                mc.material = tableMat;
            }

            // Ensure Table Rigidbody is kinematic so it stays stationary
            Rigidbody tableRb = table.GetComponent<Rigidbody>();
            if (tableRb != null)
            {
                tableRb.isKinematic = true;
                tableRb.useGravity = false;
            }
        }
        else
        {
            Debug.LogWarning("Table reference is missing. Physics settings not applied to table.");
        }

        // Create Ball Physics Material
        PhysicsMaterial ballMat = new PhysicsMaterial
        {
            name = "BallPhysicsMaterial",
            staticFriction = ballFriction,
            dynamicFriction = ballFriction,
            bounciness = ballBounciness,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Average
        };

        // 2. Setup all balls
        List<GameObject> allBalls = new List<GameObject>();
        if (cueBall != null) allBalls.Add(cueBall);
        if (striker != null) allBalls.Add(striker);
        if (objectBalls != null) allBalls.AddRange(objectBalls);

        foreach (GameObject ball in allBalls)
        {
            if (ball == null) continue;

            // Ensure SphereCollider exists and is centered with exact radius matching mesh bounds
            SphereCollider sc = ball.GetComponent<SphereCollider>();
            if (sc == null)
            {
                sc = ball.AddComponent<SphereCollider>();
            }
            sc.material = ballMat;
            sc.center = Vector3.zero; // Fix offset center on imported meshes!

            Renderer rend = ball.GetComponent<Renderer>();
            if (rend == null) rend = ball.GetComponentInChildren<Renderer>();
            if (rend != null && ball.transform.lossyScale.x > 0.001f)
            {
                float meshWorldRadius = Mathf.Max(rend.bounds.extents.x, rend.bounds.extents.z);
                sc.radius = meshWorldRadius / ball.transform.lossyScale.x;
            }
            else
            {
                sc.radius = 0.5f;
            }

            // Ensure Rigidbody exists and has the correct settings
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = ball.AddComponent<Rigidbody>();
            }

            rb.mass = ballMass;
            rb.linearDamping = ballDrag;
            rb.angularDamping = ballAngularDrag;
            rb.useGravity = true;
            rb.isKinematic = false;
            
            // Continuous dynamic collision detection is essential to prevent fast balls tunneling through others
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        // 3. Setup Cue Stick physics (prevent it falling or causing rapid physics glitches)
        if (cueStick != null)
        {
            Rigidbody stickRb = cueStick.GetComponent<Rigidbody>();
            if (stickRb == null)
            {
                stickRb = cueStick.AddComponent<Rigidbody>();
            }
            stickRb.isKinematic = true;
            stickRb.useGravity = false;

            // Disable all colliders on cue stick and its children so it never physically touches or pushes balls
            Collider[] stickColliders = cueStick.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in stickColliders)
            {
                col.enabled = false;
            }
        }
    }

    public bool IsSolidBall(GameObject ball)
    {
        if (ball == null) return true;
        string name = ball.name.ToLower();
        if (name.Contains("black") || name.Contains("striker") || name == "8") return false;

        string digits = System.Text.RegularExpressions.Regex.Match(name, @"\d+").Value;
        if (int.TryParse(digits, out int ballNum))
        {
            if (ballNum >= 8 && ballNum <= 15) return false;
            if (ballNum >= 1 && ballNum <= 7) return true;
        }

        if (name == "billiard_ball") return true;

        if (objectBalls != null && objectBalls.Contains(ball))
        {
            int index = objectBalls.IndexOf(ball);
            return index < (objectBalls.Count / 2);
        }

        return true;
    }

    public float GetBallRadius(GameObject ball)
    {
        if (ball != null)
        {
            // 1. Prefer un-rotated mesh filter bounds
            MeshFilter mf = ball.GetComponent<MeshFilter>();
            if (mf == null) mf = ball.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 ext = mf.sharedMesh.bounds.extents;
                float localRadius = Mathf.Max(ext.x, ext.y, ext.z);
                float scale = Mathf.Max(ball.transform.lossyScale.x, ball.transform.lossyScale.z);
                if (localRadius * scale > 0.01f)
                    return localRadius * scale;
            }

            // 2. Fallback to SphereCollider
            SphereCollider sc = ball.GetComponent<SphereCollider>();
            if (sc != null && sc.radius > 0.01f)
            {
                float scale = Mathf.Max(ball.transform.lossyScale.x, ball.transform.lossyScale.z);
                return sc.radius * scale;
            }

            // 3. Fallback to Renderer bounds
            Renderer r = ball.GetComponent<Renderer>();
            if (r == null) r = ball.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                float ext = Mathf.Max(r.bounds.extents.x, r.bounds.extents.z);
                if (ext > 0.01f) return ext;
            }
        }

        float s = ball != null ? ball.transform.localScale.y : 1.0f;
        return 0.5f * s;
    }

    public float GetTableTopY()
    {
        if (table != null)
        {
            // 1. Precise surface detection: Raycast down from above table center onto table colliders
            Collider[] colliders = table.GetComponentsInChildren<Collider>(true);
            Vector3 rayStart = table.transform.position + Vector3.up * 50f;
            float feltSurfaceY = -9999f;

            foreach (Collider col in colliders)
            {
                if (col == null || col.isTrigger) continue;
                string name = col.gameObject.name.ToLower();
                if (name.Contains("hall") || name.Contains("light") || name.Contains("ceiling") || name.Contains("lamp") || name.Contains("pocket") || name.Contains("trigger"))
                    continue;

                Ray ray = new Ray(rayStart, Vector3.down);
                if (col.Raycast(ray, out RaycastHit hit, 100f))
                {
                    if (hit.point.y > feltSurfaceY)
                    {
                        feltSurfaceY = hit.point.y;
                    }
                }
            }

            if (feltSurfaceY > -9000f)
            {
                return feltSurfaceY;
            }

            // Fallback: Check Mesh filter bounds
            Renderer[] renderers = table.GetComponentsInChildren<Renderer>(true);
            float maxY = -9999f;
            bool found = false;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                string name = r.gameObject.name.ToLower();
                if (name.Contains("hall") || name.Contains("light") || name.Contains("ceiling") || name.Contains("lamp"))
                    continue;

                if (r.bounds.max.y > maxY)
                {
                    maxY = r.bounds.max.y;
                    found = true;
                }
            }
            if (found) return maxY;

            return table.transform.position.y + (table.transform.localScale.y / 2f);
        }

        return -3.267339f;
    }

    /// <summary>
    /// Positions the Cue Ball and racks the object balls (including the black striker ball) in a triangle.
    /// </summary>
    [ContextMenu("Position Balls")]
    public void PositionBalls()
    {
        if (table == null)
        {
            Debug.LogError("Cannot position balls because Table is not assigned!");
            return;
        }

        // Get actual visual ball radius from Mesh bounds
        float ballRadius = GetBallRadius(cueBall);
        if (ballRadius <= 0.01f && objectBalls != null && objectBalls.Count > 0)
        {
            ballRadius = GetBallRadius(objectBalls[0]);
        }
        if (ballRadius <= 0.01f) ballRadius = 0.3f;

        // Calculate Y position based on exact Table Mesh surface top and ball radius
        float tableTopY = GetTableTopY();
        float targetY = tableTopY + ballRadius;

        // Position Cue Ball
        if (cueBall != null)
        {
            cueBall.SetActive(true);
            cueBall.transform.position = new Vector3(table.transform.position.x, targetY, table.transform.position.z + cueBallZOffset);
            Rigidbody rb = cueBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }
        }

        // Position Object Balls in a standard 8-Ball triangle rack
        float d = ballRadius * 2f;
        float spacingX = d * spacingFactor;
        float spacingZ = d * Mathf.Sqrt(3f) / 2f * spacingFactor;

        // Row coordinate offsets: (X offset multiplier, Row index multiplier)
        List<Vector2> rackSlots = new List<Vector2>()
        {
            // Row 0 (1 ball - Apex)
            new Vector2(0f, 0f),
            
            // Row 1 (2 balls)
            new Vector2(-0.5f, 1f),
            new Vector2(0.5f, 1f),
            
            // Row 2 (3 balls) - Index 4 is the middle slot where 8-Ball goes!
            new Vector2(-1f, 2f),
            new Vector2(0f, 2f),
            new Vector2(1f, 2f),
            
            // Row 3 (4 balls)
            new Vector2(-1.5f, 3f),
            new Vector2(-0.5f, 3f),
            new Vector2(0.5f, 3f),
            new Vector2(1.5f, 3f),
            
            // Row 4 (5 balls)
            new Vector2(-2f, 4f),
            new Vector2(-1f, 4f),
            new Vector2(0f, 4f),
            new Vector2(1f, 4f),
            new Vector2(2f, 4f)
        };

        // Separate object balls into Solids and Stripes
        List<GameObject> solidsList = new List<GameObject>();
        List<GameObject> stripesList = new List<GameObject>();

        if (objectBalls != null)
        {
            foreach (GameObject b in objectBalls)
            {
                if (b == null) continue;
                if (IsSolidBall(b))
                {
                    solidsList.Add(b);
                }
                else
                {
                    stripesList.Add(b);
                }
            }
        }

        // Standard official 8-Ball Rack pattern matching reference image:
        // Slot 0 (Row 1): Solid (Apex)
        // Slot 1 (Row 2, L): Stripe
        // Slot 2 (Row 2, R): Solid
        // Slot 3 (Row 3, L): Solid
        // Slot 4 (Row 3, Center): 8-Ball (Striker)
        // Slot 5 (Row 3, R): Stripe
        // Slot 6 (Row 4, Pos 1): Stripe
        // Slot 7 (Row 4, Pos 2): Solid
        // Slot 8 (Row 4, Pos 3): Stripe
        // Slot 9 (Row 4, Pos 4): Solid
        // Slot 10 (Row 5, Pos 1 / Left Corner): Solid
        // Slot 11 (Row 5, Pos 2): Stripe
        // Slot 12 (Row 5, Pos 3): Stripe
        // Slot 13 (Row 5, Pos 4): Solid
        // Slot 14 (Row 5, Pos 5 / Right Corner): Stripe

        int[] solidSlotIndices = new int[] { 0, 2, 3, 7, 9, 10, 13 };
        int[] stripeSlotIndices = new int[] { 1, 5, 6, 8, 11, 12, 14 };

        Dictionary<int, GameObject> slotToBallMap = new Dictionary<int, GameObject>();

        if (striker != null)
        {
            slotToBallMap[4] = striker;
        }

        for (int i = 0; i < solidSlotIndices.Length; i++)
        {
            int slot = solidSlotIndices[i];
            if (i < solidsList.Count)
            {
                slotToBallMap[slot] = solidsList[i];
            }
        }

        for (int i = 0; i < stripeSlotIndices.Length; i++)
        {
            int slot = stripeSlotIndices[i];
            if (i < stripesList.Count)
            {
                slotToBallMap[slot] = stripesList[i];
            }
        }

        // Fallback for any unmapped balls
        List<GameObject> unmappedBalls = new List<GameObject>();
        if (objectBalls != null)
        {
            foreach (GameObject b in objectBalls)
            {
                if (b != null && !slotToBallMap.ContainsValue(b))
                {
                    unmappedBalls.Add(b);
                }
            }
        }
        int unmappedIdx = 0;
        for (int i = 0; i < rackSlots.Count; i++)
        {
            if (!slotToBallMap.ContainsKey(i) && unmappedIdx < unmappedBalls.Count)
            {
                slotToBallMap[i] = unmappedBalls[unmappedIdx++];
            }
        }

        // Apply positions
        Vector3 tablePos = table.transform.position;
        foreach (var entry in slotToBallMap)
        {
            int slotIdx = entry.Key;
            GameObject ball = entry.Value;

            if (ball == null) continue;

            ball.SetActive(true);

            Vector2 offsetMultiplier = rackSlots[slotIdx];
            float posX = tablePos.x + (offsetMultiplier.x * spacingX);
            float posZ = tablePos.z + rackApexZOffset + (offsetMultiplier.y * spacingZ);

            ball.transform.position = new Vector3(posX, targetY, posZ);
            ball.transform.rotation = Quaternion.Euler(ballRotation);
            
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }
        }
    }

    /// <summary>
    /// Positions the Cue Stick behind the Cue Ball, aligned and ready.
    /// </summary>
    [ContextMenu("Position Stick")]
    public void PositionStick()
    {
        if (cueStick == null || cueBall == null) return;

        float elevationAngle = stickRotation.x > 0.1f ? stickRotation.x : 5.5f;
        Quaternion rotation = Quaternion.AngleAxis(0f, Vector3.up) * Quaternion.Euler(elevationAngle, 0f, 0f);
        cueStick.transform.rotation = rotation;

        float ballRadius = GetBallRadius(cueBall);

        Renderer rend = cueStick.GetComponent<Renderer>();
        if (rend == null) rend = cueStick.GetComponentInChildren<Renderer>();
        float stickLength = rend != null ? rend.bounds.size.z : cueStick.transform.localScale.y * 2f;

        float totalOffset = (stickLength / 2f) + ballRadius + stickBallGap;
        Vector3 stickDir = rotation * Vector3.forward;

        cueStick.transform.position = cueBall.transform.position - stickDir * totalOffset;

        Rigidbody rb = cueStick.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        cueStick.SetActive(true);
    }
}
