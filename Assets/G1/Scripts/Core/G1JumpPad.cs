using UnityEngine;

public class G1JumpPad : MonoBehaviour
{
    public float launchForce = 13.5f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var move = other.GetComponent<PlayerMovement>();
            if (move != null)
            {
                move.Launch(launchForce);
                // Pitch up horde roar for an energy burst launch sound effect
                G1Audio.Play("horde_roar", transform.position, 0.4f, 1.8f);
                Debug.Log("🚀 JUMP PAD: Launched Player!");
            }
        }
    }
}
