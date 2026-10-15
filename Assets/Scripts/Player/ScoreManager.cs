
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviourPunCallbacks
{
    [Header("HUD")]
    [SerializeField] private TMP_Text localScoreText;
    [SerializeField] private TMP_Text opponentScoreText;

    private string ScoreKey(int actorNumber)
    {
        return $"Score_{actorNumber}";
    }

    private void Start()
    {
        if (!photonView.IsMine)
            return;

        if (PhotonNetwork.InRoom)
        {
            InitializeScore();
            RefreshScores();
        }
    }

    private void InitializeScore()
    {
        string key = ScoreKey(PhotonNetwork.LocalPlayer.ActorNumber);

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key))
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new Hashtable { { key, 0 } }
            );
        }
    }

    [PunRPC]
    public void AwardKillPoints(int points)
    {
        if (!photonView.IsMine || !PhotonNetwork.InRoom)
            return;

        string key = ScoreKey(PhotonNetwork.LocalPlayer.ActorNumber);

        int currentScore = 0;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
            key, out object value))
        {
            currentScore = (int)value;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { key, currentScore + points } }
        );
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable propertiesThatChanged)
    {
        RefreshScores();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshScores();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshScores();
    }

    private void RefreshScores()
    {
        if (!photonView.IsMine || !PhotonNetwork.InRoom)
            return;

        Player localPlayer = PhotonNetwork.LocalPlayer;

        Player opponent = null;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber != localPlayer.ActorNumber)
            {
                opponent = player;
                break;
            }
        }

        int localScore = GetScore(localPlayer.ActorNumber);

        if (localScoreText != null)
        {
            localScoreText.text = $"{localScore}";
        }

        if (opponentScoreText != null)
        {
            if (opponent != null)
            {
                int opponentScore = GetScore(opponent.ActorNumber);

                opponentScoreText.text =
                    $"{opponentScore}";
            }
            else
            {
                opponentScoreText.text = "0";
            }
        }
    }

    private int GetScore(int actorNumber)
    {
        string key = ScoreKey(actorNumber);

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
            key, out object value))
        {
            return (int)value;
        }

        return 0;
    }
}

