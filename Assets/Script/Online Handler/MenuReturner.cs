using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Loads the main menu (after an optional delay) from its own little object.
// It can't run on the launcher: Fusion's runner.Shutdown() destroys the GameObject the NetworkRunner
// sits on (the launcher), which would kill the coroutine before the menu loads.
public class MenuReturner : MonoBehaviour
{
    private static MenuReturner _active;

    public static void Load(int sceneBuildIndex, float delay, GameObject destroyFirst)
    {
        if (_active != null) return; // Already on the way back

        var go = new GameObject("MenuReturner");
        DontDestroyOnLoad(go);
        _active = go.AddComponent<MenuReturner>();
        _active.StartCoroutine(_active.Run(sceneBuildIndex, delay, destroyFirst));
    }

    private IEnumerator Run(int sceneBuildIndex, float delay, GameObject destroyFirst)
    {
        // Let the current Fusion callback finish first
        yield return null;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        // The old launcher (if Fusion didn't already destroy it); the menu scene brings a fresh one
        if (destroyFirst != null) Destroy(destroyFirst);
        SceneManager.LoadScene(sceneBuildIndex);

        _active = null;
        Destroy(gameObject);
    }
}
