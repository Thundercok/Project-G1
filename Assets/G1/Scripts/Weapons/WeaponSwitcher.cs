using UnityEngine;

/// Number keys / scroll wheel select one active weapon holder at a time.
public class WeaponSwitcher : MonoBehaviour
{
    public GameObject[] weapons;
    public bool[] unlocked;
    int index;

    void EnsureUnlockedArray()
    {
        int targetLength = weapons != null ? weapons.Length : 0;
        if (targetLength == 0) return;

        if (unlocked == null || unlocked.Length != targetLength)
        {
            bool[] newUnlocked = new bool[targetLength];
            newUnlocked[0] = true; // Crowbar unlocked by default
            if (unlocked != null)
            {
                for (int i = 0; i < Mathf.Min(unlocked.Length, targetLength); i++)
                {
                    if (unlocked[i]) newUnlocked[i] = true;
                }
            }
            unlocked = newUnlocked;
        }
    }

    void Start()
    {
        EnsureUnlockedArray();
        Select(index);
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
        EnsureUnlockedArray();
        if (i < 0 || i >= weapons.Length) return false;
        return unlocked[i];
    }

    public void Unlock(int i)
    {
        EnsureUnlockedArray();
        if (i >= 0 && i < unlocked.Length)
        {
            unlocked[i] = true;
            Select(i);
        }
    }

    public void Select(int i)
    {
        EnsureUnlockedArray();
        if (i < 0 || i >= weapons.Length) return;
        index = i;
        for (int j = 0; j < weapons.Length; j++)
            weapons[j].SetActive(j == i);
    }

    void SelectNext(int dir)
    {
        EnsureUnlockedArray();
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
