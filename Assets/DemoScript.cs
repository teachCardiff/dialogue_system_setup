using UnityEngine;
using UnityEngine.SceneManagement;

public class DemoScript : MonoBehaviour
{
    public void LoadAScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }
}
