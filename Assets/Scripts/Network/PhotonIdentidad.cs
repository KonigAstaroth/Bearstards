using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Liga la identidad de Photon con la cuenta de la API.
public static class PhotonIdentidad
{
    public const string ClaveApiId = "apiId";

    // Hay que llamarlo ANTES de conectar para que el UserId de Photon sea el id de la API.
    public static void Aplicar(JugadorDto jugador)
    {
        if (jugador == null)
        {
            Debug.LogWarning("[PhotonIdentidad] No hay jugador para aplicar");
            return;
        }

        PhotonNetwork.NickName = jugador.username;
        PhotonNetwork.AuthValues = new AuthenticationValues(jugador.id.ToString());

        // Fuera de una room se guarda en local y se sincroniza al entrar.
        if (PhotonNetwork.LocalPlayer != null)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { ClaveApiId, jugador.id } });
        }
    }

    // Id de la API de un jugador de Photon, o -1 si no lo tiene.
    public static int ApiIdDe(Player p)
    {
        if (p == null || p.CustomProperties == null) return -1;
        if (p.CustomProperties.TryGetValue(ClaveApiId, out object valor) && valor is int id) return id;
        return -1;
    }

    // Indica si la identidad actual de Photon es la de este jugador.
    public static bool CoincideCon(JugadorDto j)
    {
        if (j == null) return false;

        string userId = PhotonNetwork.AuthValues != null ? PhotonNetwork.AuthValues.UserId : null;
        if (userId != j.id.ToString()) return false;
        if (PhotonNetwork.NickName != j.username) return false;

        return ApiIdDe(PhotonNetwork.LocalPlayer) == j.id;
    }
}
