using UnityEngine;
using UnityEngine.SceneManagement;

public static class DemoScript
{
    public static void LoadAScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }
}
