using UnityEngine;

/// HL-style +use: press E while looking at something usable within reach.
public class PlayerUse : MonoBehaviour
{
    public Camera viewCamera;
    public float reach = 2.2f;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;
        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, reach))
        {
            IUsable usable = hit.collider.GetComponentInParent<IUsable>();
            usable?.OnUse(gameObject);
        }
    }
}
