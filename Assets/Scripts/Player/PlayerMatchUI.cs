using UnityEngine;
using TMPro;
using Photon.Pun;

public class PlayerMatchUI : MonoBehaviour
{
    [Header("Match UI")]
    [SerializeField] public TMP_Text timerText;
    [SerializeField] public GameObject waitingPanel;
    [SerializeField] public TMP_Text waitingText;
    [SerializeField] public TMP_Text phaseTextUI;
    [SerializeField] public GameObject phasePanel;

    private void Start()
    {
        PhotonView playerView = GetComponentInParent<PhotonView>();

        if (playerView == null || !playerView.IsMine)
            return;

        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.RegisterPlayerUI(this);
        }
        else
        {
            Debug.LogError("[PlayerMatchUI] MatchManager not found!");
        }
    }
}
