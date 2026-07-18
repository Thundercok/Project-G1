using UnityEngine;

/// Number keys / scroll wheel select one active weapon holder at a time.
public class WeaponSwitcher : MonoBehaviour
{
    public GameObject[] weapons;
    int index;

    void Start()
    {
        Select(0);
    }

    void Update()
    {
        for (int i = 0; i < weapons.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                Select(i);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.01f)
            Select((index + 1) % weapons.Length);
        else if (scroll < -0.01f)
            Select((index + weapons.Length - 1) % weapons.Length);
    }

    void Select(int i)
    {
        index = i;
        for (int j = 0; j < weapons.Length; j++)
            weapons[j].SetActive(j == i);
    }
}
