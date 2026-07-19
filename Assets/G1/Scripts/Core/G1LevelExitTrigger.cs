using UnityEngine;
using UnityEngine.SceneManagement;

public class G1LevelExitTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.LogWarning("🎉 LEVEL COMPLETE! You escaped the facility!");
            // Reload the scene after 3 seconds to restart the demo
            Invoke("ReloadScene", 3.0f);
        }
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
