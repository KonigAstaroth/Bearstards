using System;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Muestra el jugador con sesion en el Lobby y permite cerrar sesion.
public class LobbyPerfil : MonoBehaviour
{
    [SerializeField] private Text nombreTexto;
    [SerializeField] private Text estadisticasTexto;
    [SerializeField] private Button botonCerrarSesion;

    private bool saliendo;

    private void OnEnable()
    {
        SessionManager sesion = SessionManager.Instance;
        if (sesion == null || !sesion.HaySesion)
        {
            IrAlLogin();
            return;
        }

        sesion.SesionCambiada += AlCambiarSesion;
        sesion.SesionExpirada += AlExpirarSesion;

        if (botonCerrarSesion != null)
        {
            botonCerrarSesion.onClick.AddListener(CerrarSesion);
            botonCerrarSesion.interactable = true;
        }

        Mostrar(sesion.Jugador);
        Refrescar();
    }

    private void OnDisable()
    {
        SessionManager sesion = SessionManager.Instance;
        if (sesion != null)
        {
            sesion.SesionCambiada -= AlCambiarSesion;
            sesion.SesionExpirada -= AlExpirarSesion;
        }

        if (botonCerrarSesion != null)
        {
            botonCerrarSesion.onClick.RemoveListener(CerrarSesion);
        }
    }

    private async void Refrescar()
    {
        try
        {
            ApiResultado<JugadorDto> resultado = await SessionManager.Instance.RefrescarJugador();

            // El objeto pudo destruirse mientras esperabamos.
            if (this == null || !isActiveAndEnabled || saliendo) return;

            if (resultado != null && resultado.Ok && resultado.Datos != null)
            {
                Mostrar(resultado.Datos);
            }
            else if (resultado != null)
            {
                Debug.LogWarning("[LobbyPerfil] No se pudo refrescar el jugador: " + resultado.Error);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void Mostrar(JugadorDto jugador)
    {
        if (jugador == null)
        {
            if (nombreTexto != null) nombreTexto.text = "";
            if (estadisticasTexto != null) estadisticasTexto.text = "";
            return;
        }

        if (nombreTexto != null) nombreTexto.text = jugador.username;

        if (estadisticasTexto != null)
        {
            estadisticasTexto.text =
                $"Puntos: {jugador.puntos_totales} · Partidas: {jugador.partidas} · Kills: {jugador.kills}\n" +
                $"Victorias: {jugador.victorias} · Derrotas: {jugador.derrotas} · Empates: {jugador.empates}";
        }
    }

    private void AlCambiarSesion()
    {
        if (this == null || saliendo) return;

        SessionManager sesion = SessionManager.Instance;
        if (sesion == null || !sesion.HaySesion)
        {
            IrAlLogin();
            return;
        }

        Mostrar(sesion.Jugador);
    }

    private void AlExpirarSesion()
    {
        if (this == null || saliendo) return;

        Debug.Log("[LobbyPerfil] La sesion expiro");
        IrAlLogin();
    }

    public async void CerrarSesion()
    {
        if (saliendo) return;
        saliendo = true;

        if (botonCerrarSesion != null) botonCerrarSesion.interactable = false;

        try
        {
            if (SessionManager.Instance != null)
            {
                await SessionManager.Instance.CerrarSesion();
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        // Aunque el objeto se haya destruido, igual hay que salir de Photon e ir al login.
        DesconectarYCargarLogin();
    }

    private void IrAlLogin()
    {
        saliendo = true;
        DesconectarYCargarLogin();
    }

    private static void DesconectarYCargarLogin()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        SceneManager.LoadScene("Login");
    }
}
