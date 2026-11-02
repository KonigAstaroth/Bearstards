using Photon.Pun;
using UnityEngine;

public class ReturnToLobby : MonoBehaviourPun
{
    public void GoToLobby()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("Lobby");
        }
    }
}
