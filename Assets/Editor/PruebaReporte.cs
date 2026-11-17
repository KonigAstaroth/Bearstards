using System;
using UnityEditor;
using UnityEngine;

// Prueba del reporte de partidas contra un Django local (http://localhost:8000).
// Uso: Unity -batchmode -projectPath . -executeMethod PruebaReporte.Ejecutar -logFile -
// (sin -quit: este método llama a EditorApplication.Exit al terminar)
public static class PruebaReporte
{
    private const string ClaveToken = "bearstards.sesion.token";
    private const string ClaveUsuario = "bearstards.sesion.usuario";
    private const string Password = "ClaveSegura!2026";
    private const string UrlSinServidor = "http://127.0.0.1:9/api";
    private const double TimeoutSegundos = 90.0;

    [Serializable]
    public class SaludDto
    {
        public bool ok;
    }

    private static int correctas;
    private static int fallidas;
    private static bool terminado;
    private static double inicio;

    private static bool usarLocalPrevio;
    private static string urlLocalPrevia;
    private static readonly string[] clavesRespaldo = { ClaveToken, ClaveUsuario, ReportePartida.ClaveCola };
    private static bool[] habia;
    private static string[] valores;
    private static SessionManager gestor;
    private static bool gestorCreado;

    [MenuItem("Bearstards/Prueba de reporte de partidas (local)")]
    public static void Ejecutar()
    {
        correctas = 0;
        fallidas = 0;
        terminado = false;
        inicio = EditorApplication.timeSinceStartup;

        // Solo en memoria: no se guarda el asset
        usarLocalPrevio = ApiConfig.Actual.usarLocal;
        urlLocalPrevia = ApiConfig.Actual.urlLocal;
        ApiConfig.Actual.usarLocal = true;
        ApiConfig.Actual.urlLocal = "http://localhost:8000/api";

        // Respaldo de PlayerPrefs para dejarlos como estaban
        habia = new bool[clavesRespaldo.Length];
        valores = new string[clavesRespaldo.Length];
        for (int i = 0; i < clavesRespaldo.Length; i++)
        {
            habia[i] = PlayerPrefs.HasKey(clavesRespaldo[i]);
            valores[i] = PlayerPrefs.GetString(clavesRespaldo[i], string.Empty);
        }
        PlayerPrefs.DeleteKey(ClaveToken);
        PlayerPrefs.DeleteKey(ReportePartida.ClaveCola);

        gestorCreado = SessionManager.Instance == null;
        gestor = gestorCreado ? SessionManager.Crear() : SessionManager.Instance;
        gestor.CargarSesionGuardada();

        EditorApplication.update += Vigilar;
        Log("Iniciando contra " + ApiConfig.Actual.UrlBase);
        Correr();
    }

    private static void Vigilar()
    {
        if (terminado) return;
        if (EditorApplication.timeSinceStartup - inicio > TimeoutSegundos)
        {
            Fallo("Tiempo agotado (" + TimeoutSegundos + " s)");
            Terminar();
        }
    }

    private static async void Correr()
    {
        try
        {
            await Pasos();
        }
        catch (Exception e)
        {
            Fallo("Excepción: " + e);
        }
        Terminar();
    }

    private static async Awaitable Pasos()
    {
        var salud = await ApiClient.Get<SaludDto>("/health/", conToken: false);
        if (!Comprobar(salud.Ok && salud.Datos != null && salud.Datos.ok, "GET /health/", salud)) return;

        // 1. Dos usuarios nuevos
        string sufijo = Guid.NewGuid().ToString("N").Substring(0, 8);
        string usuarioA = "repA_" + sufijo;
        string usuarioB = "repB_" + sufijo;

        var regA = await gestor.Registrar(usuarioA, string.Empty, Password);
        if (!Comprobar(regA.Ok && gestor.HaySesion, "Registro de " + usuarioA, regA)) return;
        int idA = gestor.Jugador.id;

        var regB = await gestor.Registrar(usuarioB, string.Empty, Password);
        if (!Comprobar(regB.Ok && gestor.HaySesion, "Registro de " + usuarioB, regB)) return;
        int idB = gestor.Jugador.id;
        Comprobar(idA > 0 && idB > 0 && idA != idB, $"Ids distintos ({idA}, {idB})");

        // 2. Login como A y reporte válido
        var loginA = await gestor.IniciarSesion(usuarioA, Password);
        if (!Comprobar(loginA.Ok && gestor.HaySesion && gestor.Jugador.id == idA, "Login como A", loginA)) return;
        string tokenA = gestor.Token;

        string match1 = "prueba-" + sufijo + "-1";
        Comprobar(await ReportePartida.Enviar(Reporte(match1, idA, "victoria", idB, "derrota")),
            "A reporta " + match1 + " -> guardada");
        Comprobar(await ReportePartida.Enviar(Reporte(match1, idA, "victoria", idB, "derrota")),
            "A repite " + match1 + " -> 409 cuenta como guardada");
        Comprobar(ReportePartida.CantidadPendientes == 0, "Cola vacía tras reportes válidos");

        // 3. Login como B: el mismo match_id -> 409
        var loginB = await gestor.IniciarSesion(usuarioB, Password);
        if (!Comprobar(loginB.Ok && gestor.HaySesion && gestor.Jugador.id == idB, "Login como B", loginB)) return;
        Comprobar(await ReportePartida.Enviar(Reporte(match1, idA, "victoria", idB, "derrota")),
            "B reporta " + match1 + " -> 409 cuenta como guardada");

        // 4. Reporte inválido (dos victorias) -> rechazado y no se encola
        string match2 = "prueba-" + sufijo + "-2";
        bool invalido = await ReportePartida.Enviar(Reporte(match2, idA, "victoria", idB, "victoria"));
        Comprobar(!invalido && !ReportePartida.EstaPendiente(match2) && ReportePartida.CantidadPendientes == 0,
            "Reporte con dos victorias -> rechazado y fuera de la cola");

        // 5. Sin conexión -> queda en la cola
        string match3 = "prueba-" + sufijo + "-3";
        ApiConfig.Actual.urlLocal = UrlSinServidor;
        bool sinRed;
        try
        {
            sinRed = await ReportePartida.Enviar(Reporte(match3, idA, "derrota", idB, "victoria"));
        }
        finally
        {
            ApiConfig.Actual.urlLocal = "http://localhost:8000/api";
        }
        Comprobar(!sinRed && ReportePartida.EstaPendiente(match3) && ReportePartida.CantidadPendientes == 1,
            "Sin conexión -> " + match3 + " queda pendiente");

        // 6. Reintento con conexión -> se envía y la cola queda vacía
        await ReportePartida.ReintentarPendientes();
        Comprobar(ReportePartida.CantidadPendientes == 0 && !PlayerPrefs.HasKey(ReportePartida.ClaveCola),
            "ReintentarPendientes envía y vacía la cola");

        // 7. Estadísticas de A (con su token, sin otro login)
        PlayerPrefs.SetString(ClaveToken, tokenA);
        gestor.CargarSesionGuardada();
        var yoA = await gestor.RefrescarJugador();
        Comprobar(yoA.Ok && yoA.Datos != null && yoA.Datos.id == idA && yoA.Datos.partidas == 2,
            $"GET /yo/ de A -> partidas 2 (tiene {(yoA.Datos != null ? yoA.Datos.partidas : -1)})", yoA);
    }

    private static ReportePartidaDto Reporte(string matchId, int idA, string resA, int idB, string resB)
    {
        return new ReportePartidaDto
        {
            match_id = matchId,
            por_abandono = false,
            jugadores = new[]
            {
                new ParticipanteDto { id = idA, puntos = 900, kills = 3, resultado = resA, abandono = false },
                new ParticipanteDto { id = idB, puntos = 400, kills = 1, resultado = resB, abandono = false }
            }
        };
    }

    private static bool Comprobar<T>(bool condicion, string nombre, ApiResultado<T> resultado)
    {
        string extra = resultado == null ? string.Empty : $" [status {resultado.Status}{(string.IsNullOrEmpty(resultado.Error) ? "" : ", error: " + resultado.Error)}]";
        return Comprobar(condicion, nombre + extra);
    }

    private static bool Comprobar(bool condicion, string nombre)
    {
        if (condicion)
        {
            correctas++;
            Log("OK " + nombre);
        }
        else
        {
            Fallo(nombre);
        }
        return condicion;
    }

    private static void Fallo(string mensaje)
    {
        fallidas++;
        Debug.LogError("[PruebaReporte] FALLO " + mensaje);
    }

    private static void Log(string mensaje)
    {
        Debug.Log("[PruebaReporte] " + mensaje);
    }

    private static void Terminar()
    {
        if (terminado) return;
        terminado = true;
        EditorApplication.update -= Vigilar;

        // Restaurar estado previo
        ApiConfig.Actual.usarLocal = usarLocalPrevio;
        ApiConfig.Actual.urlLocal = urlLocalPrevia;
        for (int i = 0; i < clavesRespaldo.Length; i++)
        {
            if (habia[i]) PlayerPrefs.SetString(clavesRespaldo[i], valores[i]);
            else PlayerPrefs.DeleteKey(clavesRespaldo[i]);
        }
        PlayerPrefs.Save();

        if (gestor != null)
        {
            // El objeto oculto creado en modo edición no debe quedar vivo al entrar a Play
            if (gestorCreado && !Application.isPlaying) UnityEngine.Object.DestroyImmediate(gestor.gameObject);
            else gestor.CargarSesionGuardada();
            gestor = null;
        }

        bool exito = fallidas == 0 && correctas > 0;
        Log($"Resumen: {correctas} OK, {fallidas} FALLO -> {(exito ? "ÉXITO" : "FALLÓ")}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exito ? 0 : 1);
        }
    }
}
