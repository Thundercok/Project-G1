using UnityEngine;

/// Trigger volume that auto-opens a SlidingDoor when the player nears it —
/// so exits under combat pressure don't force a stop-and-press-E moment.
[RequireComponent(typeof(BoxCollider))]
public sealed class G1ProximityDoorOpener : MonoBehaviour
{
    public SlidingDoor door;

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (door != null && other.CompareTag("Player"))
            door.Open();
    }
}
