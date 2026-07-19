using UnityEngine;

/// Number keys / scroll wheel select one active weapon holder at a time.
public class WeaponSwitcher : MonoBehaviour
{
    public GameObject[] weapons;
    public bool[] unlocked;
    int index;

    void Start()
    {
        if (unlocked == null || unlocked.Length != weapons.Length)
        {
            unlocked = new bool[weapons.Length];
            unlocked[0] = true; // Crowbar unlocked by default
        }
        Select(0);
    }

    void Update()
    {
        for (int i = 0; i < weapons.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && IsUnlocked(i))
                Select(i);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.01f)
            SelectNext(1);
        else if (scroll < -0.01f)
            SelectNext(-1);
    }

    public bool IsUnlocked(int i)
    {
        if (i < 0 || i >= weapons.Length) return false;
        if (unlocked == null || i >= unlocked.Length) return true;
        return unlocked[i];
    }

    public void Unlock(int i)
    {
        if (i >= 0 && i < unlocked.Length)
        {
            unlocked[i] = true;
            Select(i);
        }
    }

    public void Select(int i)
    {
        index = i;
        for (int j = 0; j < weapons.Length; j++)
            weapons[j].SetActive(j == i);
    }

    void SelectNext(int dir)
    {
        int next = index;
        for (int attempt = 0; attempt < weapons.Length; attempt++)
        {
            next = (next + dir + weapons.Length) % weapons.Length;
            if (IsUnlocked(next))
            {
                Select(next);
                break;
            }
        }
    }
}
