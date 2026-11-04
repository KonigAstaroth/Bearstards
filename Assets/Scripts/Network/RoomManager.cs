using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
public class RoomManager : MonoBehaviourPunCallbacks
{
    private void Start()
    {
        Debug.Log("Connecting...");
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }else if (PhotonNetwork.InLobby)
        {
            SceneManager.LoadScene("Lobby");
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined to Lobby");
        PhotonNetwork.AutomaticallySyncScene = true;
        SceneManager.LoadScene("Lobby");
    }
}
