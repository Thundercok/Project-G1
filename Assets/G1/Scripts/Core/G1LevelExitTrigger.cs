using UnityEngine;
using UnityEngine.SceneManagement;

/// Campaign flow: entering the trigger loads the next scene (or restarts the
/// current one when nextScene is empty). Clears the checkpoint — saves are
/// per-level.
public class G1LevelExitTrigger : MonoBehaviour
{
    public string nextScene = "";
    public float delay = 2.5f;

    bool fired;

    private void OnTriggerEnter(Collider other)
    {
        if (fired || !other.CompareTag("Player"))
            return;
        fired = true;
        Debug.Log("Level complete → " +
                  (string.IsNullOrEmpty(nextScene) ? "restart" : nextScene));
        PlayerPrefs.DeleteKey("G1_CP_Data");
        G1Audio.Play2D("pickup", 0.8f, 0.8f);
        Invoke(nameof(LoadNext), delay);
    }

    private void LoadNext()
    {
        SceneManager.LoadScene(string.IsNullOrEmpty(nextScene)
            ? SceneManager.GetActiveScene().name : nextScene);
    }
}
