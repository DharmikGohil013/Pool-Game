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
    public float ballDrag = 1.2f;

    [Tooltip("Angular drag of each ball to simulate spin resistance.")]
    public float ballAngularDrag = 1.2f;

    [Tooltip("Friction of the ball physics material.")]
    public float ballFriction = 0.05f;

    [Tooltip("Bounciness of the ball physics material.")]
    public float ballBounciness = 0.85f;

    [Tooltip("Friction of the table surface physics material.")]
    public float tableFriction = 0.15f;

    [Tooltip("Bounciness of the table surface physics material.")]
    public float tableBounciness = 0.2f;

    [Header("Rack Layout Settings")]
    [Tooltip("Z-coordinate offset from the table center for the cue ball (head spot).")]
    public float cueBallZOffset = -3.5f;

    [Tooltip("Z-coordinate offset from the table center for the rack apex (foot spot).")]
    public float rackApexZOffset = 3.5f;

    [Tooltip("Extra spacing gap factor to prevent balls overlapping on start (1.0 = tight pack, 1.02 = 2% gap).")]
    public float spacingFactor = 1.02f;

    [Header("Stick Setup Settings")]
    [Tooltip("Gap between the stick tip and the cue ball.")]
    public float stickBallGap = 0.15f;

    [Tooltip("Rotation of the cue stick.")]
    public Vector3 stickRotation = new Vector3(0f, 0f, 0f);

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
        // 1. Table Physics Setup
        if (table != null)
        {
            // Ensure Table has a Collider
            Collider tableCollider = table.GetComponent<Collider>();
            if (tableCollider == null)
            {
                tableCollider = table.AddComponent<BoxCollider>();
            }

            // Create and assign Table Physics Material
            PhysicsMaterial tableMat = new PhysicsMaterial
            {
                name = "TablePhysicsMaterial",
                staticFriction = tableFriction,
                dynamicFriction = tableFriction,
                bounciness = tableBounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };
            tableCollider.material = tableMat;

            // Make sure the Table does not move!
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
            bounceCombine = PhysicsMaterialCombine.Maximum
        };

        // 2. Setup all balls
        List<GameObject> allBalls = new List<GameObject>();
        if (cueBall != null) allBalls.Add(cueBall);
        if (striker != null) allBalls.Add(striker);
        if (objectBalls != null) allBalls.AddRange(objectBalls);

        foreach (GameObject ball in allBalls)
        {
            if (ball == null) continue;

            // Ensure SphereCollider exists and is centered with standard unit radius
            SphereCollider sc = ball.GetComponent<SphereCollider>();
            if (sc == null)
            {
                sc = ball.AddComponent<SphereCollider>();
            }
            sc.material = ballMat;
            sc.center = Vector3.zero; // Fix offset center on imported meshes!
            sc.radius = 0.5f;        // Standard sphere radius matching mesh scale

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

        // Calculate Y position based on Table top surface and ball radius
        float tableTopY = table.transform.position.y + (table.transform.localScale.y / 2f);
        
        // Get ball radius (assume cue ball scale if available, else 0.5f)
        float ballScale = 1.0f;
        if (cueBall != null)
        {
            ballScale = cueBall.transform.localScale.y;
        }
        else if (objectBalls != null && objectBalls.Count > 0 && objectBalls[0] != null)
        {
            ballScale = objectBalls[0].transform.localScale.y;
        }
        float ballRadius = 0.5f * ballScale;
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
            }
        }

        // Position Object Balls in a triangle rack
        // We have 12 object balls + 1 striker (black ball) = 13 balls to position.
        // Let's create the coordinate offsets for a standard triangle layout (pointing in negative Z direction):
        // Row 0: 1 ball (Apex)
        // Row 1: 2 balls
        // Row 2: 3 balls (Striker goes in the middle)
        // Row 3: 4 balls
        // Row 4: 3 balls (remaining)
        
        float d = ballRadius * 2f;
        float spacingX = d * spacingFactor;
        float spacingZ = d * Mathf.Sqrt(3f) / 2f * spacingFactor;

        // Row coordinate offsets: (X offset multiplier, Row index multiplier)
        List<Vector2> rackSlots = new List<Vector2>()
        {
            // Row 0 (1 ball)
            new Vector2(0f, 0f),
            
            // Row 1 (2 balls)
            new Vector2(-0.5f, 1f),
            new Vector2(0.5f, 1f),
            
            // Row 2 (3 balls) - Index 4 is the middle slot where Striker (8-Ball) goes!
            new Vector2(-1f, 2f),
            new Vector2(0f, 2f), // Index 4: Striker Slot
            new Vector2(1f, 2f),
            
            // Row 3 (4 balls)
            new Vector2(-1.5f, 3f),
            new Vector2(-0.5f, 3f),
            new Vector2(0.5f, 3f),
            new Vector2(1.5f, 3f),
            
            // Row 4 (5 balls for full 15-ball triangle rack)
            new Vector2(-2f, 4f),
            new Vector2(-1f, 4f),
            new Vector2(0f, 4f),
            new Vector2(1f, 4f),
            new Vector2(2f, 4f)
        };

        // Separate out object balls list and create a master list of balls to place
        List<GameObject> ballsToRack = new List<GameObject>();
        
        // We want the striker to go specifically to index 4 (middle of row 2)
        // Let's build a map from slot index to GameObject
        Dictionary<int, GameObject> slotToBallMap = new Dictionary<int, GameObject>();
        
        if (striker != null)
        {
            slotToBallMap[4] = striker;
        }

        int currentObjBallIdx = 0;
        for (int i = 0; i < rackSlots.Count; i++)
        {
            // Skip the striker slot since it is already assigned
            if (slotToBallMap.ContainsKey(i))
                continue;

            if (objectBalls != null && currentObjBallIdx < objectBalls.Count)
            {
                slotToBallMap[i] = objectBalls[currentObjBallIdx];
                currentObjBallIdx++;
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
            
            // Reset velocities in case they are already moving
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
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

        // Apply stick rotation
        cueStick.transform.eulerAngles = stickRotation;

        // Calculate Y position based on Table top surface and ball radius
        float tableTopY = table != null ? (table.transform.position.y + (table.transform.localScale.y / 2f)) : cueBall.transform.position.y;
        float ballScale = cueBall.transform.localScale.y;
        float ballRadius = 0.5f * ballScale;

        // A cylinder's length is its scale.y * 2.0f
        float stickLength = cueStick.transform.localScale.y * 2f;
        
        // Position stick behind the cue ball along the Z-axis
        Vector3 cueBallPos = cueBall.transform.position;
        float stickPosZ = cueBallPos.z - (stickLength / 2f) - ballRadius - stickBallGap;

        // Set stick position (using Y of 0.25f or dynamic Y based on ball center height)
        cueStick.transform.position = new Vector3(cueBallPos.x, cueBallPos.y, stickPosZ);
        
        // Ensure its Rigidbody velocity is reset (only if not kinematic)
        Rigidbody rb = cueStick.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Set active last so controller calculates rotation using the correct starting position
        cueStick.SetActive(true);
    }
}
