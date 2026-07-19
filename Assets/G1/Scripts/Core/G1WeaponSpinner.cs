using UnityEngine;

public class G1WeaponSpinner : MonoBehaviour
{
    private float startY;

    void Start()
    {
        startY = transform.position.y;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
        // Gentle floating bob
        float newY = startY + Mathf.Sin(Time.time * 2.5f) * 0.12f;
        Vector3 pos = transform.position;
        pos.y = newY;
        transform.position = pos;
    }
}
