
using UnityEngine;
using TMPro;
using System.Collections;

public class LoadingText : MonoBehaviour
{
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private float interval = 0.5f;

    private void Start()
    {
        StartCoroutine(AnimateLoading());
    }

    private IEnumerator AnimateLoading()
    {
        int dots = 0;

        while (true)
        {
            loadingText.text = "Loading" + new string('.', dots);

            dots = (dots + 1) % 4;

            yield return new WaitForSeconds(interval);
        }
    }
}
