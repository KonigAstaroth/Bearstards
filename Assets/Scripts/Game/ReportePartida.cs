using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Cómo terminó un intento de reportar una partida
public enum ResultadoEnvio
{
    Guardada,   // 201, o 409 (el otro cliente ya la reportó)
    Pendiente,  // sin conexión, error del servidor o sin sesión: queda en la cola
    Rechazada,  // la API la rechazó (400...): no se reintenta
    SinDatos,   // no se pudo construir el reporte
    YaEnviada   // ya se reportó en esta sesión
}

// Reporta el resultado de la partida a la API (POST /partidas/).
// Cada cliente reporta con su propio token; la API descarta el repetido con 409.
public static class ReportePartida
{
    public const string ClaveCola = "bearstards.reportes.pendientes";
    public const int MaxPendientes = 20;
    private const string Ruta = "/partidas/";

    [Serializable]
    private class ColaReportes
    {
        public ReportePartidaDto[] reportes = new ReportePartidaDto[0];
    }

    // match_id ya reportados (o en curso) en esta sesión del juego
    private static readonly HashSet<string> enviados = new HashSet<string>();
    private static bool reintentando;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        // Por si el "domain reload" está desactivado
        enviados.Clear();
        reintentando = false;
    }

    // Arma el cuerpo con las propiedades de la sala. Null si falta algo.
    public static ReportePartidaDto Construir()
    {
        if (!PhotonNetwork.InRoom) return Descartar("no estás en una sala");
        if (!EstadoPartida.Terminada) return Descartar("la partida aún no termina");

        string matchId = EstadoPartida.MatchId;
        if (string.IsNullOrEmpty(matchId)) return Descartar("falta MatchId");
        if (matchId.Length > 64) return Descartar("MatchId con más de 64 caracteres");

        IReadOnlyList<int> actores = EstadoPartida.Actores;
        if (actores.Count != 2) return Descartar($"se esperaban 2 actores y hay {actores.Count}");

        int abandono = EstadoPartida.ActorAbandono;
        bool porAbandono = abandono != -1;
        if (porAbandono && abandono != actores[0] && abandono != actores[1])
        {
            return Descartar($"el actor que abandonó ({abandono}) no está en la partida");
        }

        var jugadores = new ParticipanteDto[2];
        for (int i = 0; i < 2; i++)
        {
            int actor = actores[i];
            int apiId = EstadoPartida.ApiId(actor);
            if (apiId <= 0) return Descartar($"el actor {actor} no tiene ApiId");

            string resultado;
            if (porAbandono)
            {
                // Quien abandona pierde, sin importar el marcador
                resultado = actor == abandono ? "derrota" : "victoria";
            }
            else
            {
                resultado = EstadoPartida.Resultado(actor);
            }

            jugadores[i] = new ParticipanteDto
            {
                id = apiId,
                puntos = EstadoPartida.Puntos(actor),
                kills = Mathf.Max(0, EstadoPartida.Kills(actor)),
                resultado = resultado,
                abandono = porAbandono && actor == abandono
            };
        }

        if (jugadores[0].id == jugadores[1].id) return Descartar("los dos jugadores tienen el mismo ApiId");

        return new ReportePartidaDto { match_id = matchId, por_abandono = porAbandono, jugadores = jugadores };
    }

    private static ReportePartidaDto Descartar(string motivo)
    {
        Debug.LogWarning("[ReportePartida] No se puede armar el reporte: " + motivo);
        return null;
    }

    // Construye y envía la partida actual, una sola vez por match_id en esta sesión
    public static async Awaitable<ResultadoEnvio> ReportarPartidaActual()
    {
        ReportePartidaDto reporte = Construir();
        if (reporte == null) return ResultadoEnvio.SinDatos;
        if (!enviados.Add(reporte.match_id))
        {
            Debug.Log("[ReportePartida] Ya se reportó " + reporte.match_id);
            return ResultadoEnvio.YaEnviada;
        }
        return await EnviarDetallado(reporte);
    }

    // true si quedó guardada (201 o 409)
    public static async Awaitable<bool> Enviar(ReportePartidaDto reporte)
    {
        return await EnviarDetallado(reporte) == ResultadoEnvio.Guardada;
    }

    public static async Awaitable<ResultadoEnvio> EnviarDetallado(ReportePartidaDto reporte)
    {
        if (reporte == null || string.IsNullOrEmpty(reporte.match_id)) return ResultadoEnvio.SinDatos;

        SessionManager sesion = SessionManager.Instance;
        if (sesion == null || !sesion.TieneTokenGuardado)
        {
            Debug.Log("[ReportePartida] Sin sesión: " + reporte.match_id + " queda pendiente");
            Encolar(reporte);
            return ResultadoEnvio.Pendiente;
        }

        var resultado = await ApiClient.Post<PartidaCreadaDto>(Ruta, reporte);
        switch (Clasificar(resultado))
        {
            case ResultadoEnvio.Guardada:
                Debug.Log(resultado.Status == 409
                    ? "[ReportePartida] " + reporte.match_id + " ya estaba reportada (409)"
                    : "[ReportePartida] Partida guardada: " + reporte.match_id);
                return ResultadoEnvio.Guardada;

            case ResultadoEnvio.Pendiente:
                Debug.LogWarning($"[ReportePartida] No se pudo enviar {reporte.match_id} ({resultado.Status}: {resultado.Error}); queda pendiente");
                Encolar(reporte);
                return ResultadoEnvio.Pendiente;

            default:
                Debug.LogError($"[ReportePartida] La API rechazó {reporte.match_id} ({resultado.Status}): {resultado.Error}");
                return ResultadoEnvio.Rechazada;
        }
    }

    private static ResultadoEnvio Clasificar(ApiResultado<PartidaCreadaDto> r)
    {
        if (r.Ok || r.Status == 409) return ResultadoEnvio.Guardada;
        // Sin conexión, servidor caído, demasiadas peticiones o token rechazado: se reintenta luego
        if (r.Status <= 0 || r.Status >= 500 || r.Status == 429 || r.Status == 401) return ResultadoEnvio.Pendiente;
        return ResultadoEnvio.Rechazada;
    }

    // --- Cola de pendientes (PlayerPrefs) ---

    public static int CantidadPendientes => LeerCola().Count;

    public static bool EstaPendiente(string matchId) =>
        LeerCola().Exists(r => r != null && r.match_id == matchId);

    private static List<ReportePartidaDto> LeerCola()
    {
        string json = PlayerPrefs.GetString(ClaveCola, string.Empty);
        if (string.IsNullOrEmpty(json)) return new List<ReportePartidaDto>();
        try
        {
            ColaReportes cola = JsonUtility.FromJson<ColaReportes>(json);
            var lista = new List<ReportePartidaDto>();
            if (cola != null && cola.reportes != null)
            {
                foreach (ReportePartidaDto r in cola.reportes)
                {
                    if (r != null && !string.IsNullOrEmpty(r.match_id)) lista.Add(r);
                }
            }
            return lista;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ReportePartida] Cola ilegible, se descarta: " + e.Message);
            return new List<ReportePartidaDto>();
        }
    }

    private static void GuardarCola(List<ReportePartidaDto> lista)
    {
        if (lista.Count == 0)
        {
            PlayerPrefs.DeleteKey(ClaveCola);
        }
        else
        {
            PlayerPrefs.SetString(ClaveCola, JsonUtility.ToJson(new ColaReportes { reportes = lista.ToArray() }));
        }
        PlayerPrefs.Save();
    }

    private static void Encolar(ReportePartidaDto reporte)
    {
        List<ReportePartidaDto> lista = LeerCola();
        lista.RemoveAll(r => r.match_id == reporte.match_id);
        lista.Add(reporte);
        // Si se llena, se pierden los más viejos
        while (lista.Count > MaxPendientes) lista.RemoveAt(0);
        GuardarCola(lista);
    }

    private static void Quitar(string matchId)
    {
        List<ReportePartidaDto> lista = LeerCola();
        if (lista.RemoveAll(r => r.match_id == matchId) > 0) GuardarCola(lista);
    }

    // Reenvía los pendientes en los que participa el jugador con sesión
    public static async Awaitable ReintentarPendientes()
    {
        if (reintentando) return;
        SessionManager sesion = SessionManager.Instance;
        if (sesion == null || !sesion.HaySesion) return;

        reintentando = true;
        try
        {
            int miId = sesion.Jugador.id;
            List<ReportePartidaDto> lista = LeerCola();
            if (lista.Count == 0) return;

            int guardadas = 0;
            foreach (ReportePartidaDto reporte in lista)
            {
                if (!Participa(reporte, miId)) continue;
                // Si la sesión cambió a mitad de camino, paramos
                if (sesion != SessionManager.Instance || !sesion.HaySesion || sesion.Jugador.id != miId) break;

                var resultado = await ApiClient.Post<PartidaCreadaDto>(Ruta, reporte);
                ResultadoEnvio estado = Clasificar(resultado);
                if (estado == ResultadoEnvio.Pendiente)
                {
                    Debug.LogWarning($"[ReportePartida] Reintento de {reporte.match_id} falló ({resultado.Status}); se queda en la cola");
                    break; // probablemente sigue sin conexión
                }

                if (estado == ResultadoEnvio.Guardada)
                {
                    guardadas++;
                    Debug.Log("[ReportePartida] Pendiente enviada: " + reporte.match_id + " (" + resultado.Status + ")");
                }
                else
                {
                    Debug.LogError($"[ReportePartida] Pendiente rechazada {reporte.match_id} ({resultado.Status}): {resultado.Error}; se descarta");
                }
                Quitar(reporte.match_id);
            }

            if (guardadas > 0)
            {
                Debug.Log($"[ReportePartida] {guardadas} partida(s) pendiente(s) guardada(s)");
                // Para que las estadísticas del perfil ya las incluyan
                if (sesion == SessionManager.Instance && sesion.HaySesion) await sesion.RefrescarJugador();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[ReportePartida] Error al reintentar: " + e);
        }
        finally
        {
            reintentando = false;
        }
    }

    private static bool Participa(ReportePartidaDto reporte, int id)
    {
        if (reporte.jugadores == null) return false;
        foreach (ParticipanteDto p in reporte.jugadores)
        {
            if (p != null && p.id == id) return true;
        }
        return false;
    }
}
