using UnityEngine;

public class TestSound : MonoBehaviour
{
    [SerializeField] private AudioClip clip;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("P presionada, reproduciendo");
            Debug.Log("Volume: " + AudioListener.volume + " | Pause: " + AudioListener.pause);
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, 1f);
        }
    }
}