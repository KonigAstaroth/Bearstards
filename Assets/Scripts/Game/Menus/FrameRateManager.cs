using UnityEngine;

public class FrameRateManager : MonoBehaviour
{
    private int menuFPS = 60;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = menuFPS;
    }

    public void SetMenuFps()
    {
        Application.targetFrameRate = menuFPS;
    }
}
