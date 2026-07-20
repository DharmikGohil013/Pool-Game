using UnityEngine;

/// <summary>
/// Attach to each pocket hole (Capsule) on the pool table.
/// Detects when a ball enters the pocket and notifies the GameManager.
/// Automatically configures itself as a trigger on Awake.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PocketTrigger : MonoBehaviour
{
    private void Awake()
    {
        // Ensure collider is set as trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PoolGameManager.Instance == null) return;
        if (PoolGameManager.Instance.currentState != PoolGameManager.GameState.Playing) return;

        // Only handle balls (objects with non-kinematic Rigidbody)
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            PoolGameManager.Instance.BallEnteredPocket(other.gameObject);
        }
    }
}
