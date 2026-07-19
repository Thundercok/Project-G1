using UnityEngine;
using UnityEngine.SceneManagement;

/// Player death: freeze controls, fade to black, reload the scene.
[RequireComponent(typeof(HealthSystem))]
public sealed class G1PlayerDeath : MonoBehaviour
{
    public float fadeTime = 1.5f;
    public float holdTime = 0.6f;

    float diedAt = -1f;
    bool dead;

    void Awake()
    {
        GetComponent<HealthSystem>().OnDeath += (p, n) => Die();
    }

    void Die()
    {
        if (dead)
            return;
        dead = true;
        diedAt = Time.time;
        G1Audio.Play2D("player_hurt", 1f, 0.55f, 0f);   // low-pitched groan

        var move = GetComponent<PlayerMovement>();
        if (move) move.enabled = false;
        var look = GetComponentInChildren<MouseLook>();
        if (look) look.enabled = false;
        foreach (var weapon in GetComponentsInChildren<WeaponBase>())
            weapon.enabled = false;
    }

    void Update()
    {
        if (dead && Time.time > diedAt + fadeTime + holdTime)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (!dead)
            return;
        float alpha = Mathf.Clamp01((Time.time - diedAt) / fadeTime);
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
