using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

// Respuesta vacía (por ejemplo, logout -> 204)
[Serializable]
public class Vacio { }

// Cliente HTTP mínimo para la API de Bearstards
public static class ApiClient
{
    private const string SinConexionMensaje = "Sin conexión con el servidor.";

    // "campo": ["msg1", "msg2"]
    private static readonly Regex RegexCampo = new Regex(
        "\"((?:[^\"\\\\]|\\\\.)+)\"\\s*:\\s*\\[((?:\\s*\"(?:[^\"\\\\]|\\\\.)*\"\\s*,?)*)\\s*\\]");
    private static readonly Regex RegexCadena = new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"");

    public static async Awaitable<ApiResultado<T>> Get<T>(string ruta, bool conToken = true)
    {
        return await Enviar<T>(UnityWebRequest.kHttpVerbGET, ruta, null, conToken);
    }

    public static async Awaitable<ApiResultado<T>> Post<T>(string ruta, object cuerpo, bool conToken = true)
    {
        return await Enviar<T>(UnityWebRequest.kHttpVerbPOST, ruta, cuerpo, conToken);
    }

    private static async Awaitable<ApiResultado<T>> Enviar<T>(string metodo, string ruta, object cuerpo, bool conToken)
    {
        var resultado = new ApiResultado<T>();
        ApiConfig config = ApiConfig.Actual;
        string url = config.UrlBase + ruta;

        SessionManager sesion = SessionManager.Instance;
        string token = conToken && sesion != null ? sesion.Token : null;
        bool tokenEnviado = !string.IsNullOrEmpty(token);

        var reloj = Stopwatch.StartNew();

        using (var peticion = new UnityWebRequest(url, metodo))
        {
            peticion.downloadHandler = new DownloadHandlerBuffer();
            if (cuerpo != null)
            {
                byte[] datos = Encoding.UTF8.GetBytes(JsonUtility.ToJson(cuerpo));
                peticion.uploadHandler = new UploadHandlerRaw(datos);
                peticion.SetRequestHeader("Content-Type", "application/json");
            }
            peticion.SetRequestHeader("Accept", "application/json");
            if (tokenEnviado)
            {
                peticion.SetRequestHeader("Authorization", "Token " + token);
            }
            peticion.timeout = Mathf.Max(1, config.timeoutSegundos);

            Exception excepcion = null;
            try
            {
                await peticion.SendWebRequest();
            }
            catch (Exception e)
            {
                // Ej.: http bloqueado por "insecureHttpOption"
                excepcion = e;
            }

            reloj.Stop();
            long status = peticion.responseCode;
            string texto = peticion.downloadHandler != null ? peticion.downloadHandler.text : null;

            bool sinConexion = status == 0
                || peticion.result == UnityWebRequest.Result.ConnectionError
                || peticion.result == UnityWebRequest.Result.InProgress;

            if (sinConexion)
            {
                resultado.Status = 0;
                resultado.Error = SinConexionMensaje;
                string detalle = excepcion != null ? excepcion.Message : peticion.error;
                Debug.LogWarning($"[API] {metodo} {ruta} -> sin conexión ({detalle}) en {reloj.ElapsedMilliseconds} ms");
                return resultado;
            }

            resultado.Status = status;
            Debug.Log($"[API] {metodo} {ruta} -> {status} en {reloj.ElapsedMilliseconds} ms");

            if (status >= 200 && status < 300)
            {
                resultado.Ok = true;
                if (typeof(T) != typeof(Vacio) && !string.IsNullOrWhiteSpace(texto))
                {
                    try
                    {
                        resultado.Datos = JsonUtility.FromJson<T>(texto);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[API] Respuesta inválida en {ruta}: {e.Message}");
                        resultado.Ok = false;
                        resultado.Error = "Respuesta inválida del servidor.";
                    }
                }
                return resultado;
            }

            resultado.Ok = false;
            resultado.Error = MensajeError(texto, status);

            // Solo invalida si el token rechazado sigue siendo el actual (no una respuesta vieja)
            SessionManager gestor = SessionManager.Instance;
            if (status == 401 && tokenEnviado && gestor != null && gestor.Token == token)
            {
                gestor.SesionInvalida();
            }

            return resultado;
        }
    }

    // Convierte el cuerpo de error en un mensaje legible
    private static string MensajeError(string texto, long status)
    {
        if (!string.IsNullOrWhiteSpace(texto) && texto.TrimStart().StartsWith("{"))
        {
            try
            {
                ErrorDto error = JsonUtility.FromJson<ErrorDto>(texto);
                if (error != null && !string.IsNullOrEmpty(error.detail))
                {
                    return error.detail;
                }
            }
            catch (Exception)
            {
                // No era JSON válido; seguimos
            }

            var partes = new List<string>();
            foreach (Match campo in RegexCampo.Matches(texto))
            {
                string nombre = Desescapar(campo.Groups[1].Value);
                var mensajes = new List<string>();
                foreach (Match cadena in RegexCadena.Matches(campo.Groups[2].Value))
                {
                    mensajes.Add(Desescapar(cadena.Groups[1].Value));
                }
                if (mensajes.Count == 0) continue;

                string unidos = string.Join(" ", mensajes);
                partes.Add(nombre == "non_field_errors" ? unidos : nombre + ": " + unidos);
            }
            if (partes.Count > 0)
            {
                return string.Join(" · ", partes);
            }
        }

        if (status == 429) return "Demasiados intentos. Espera un momento.";
        if (status >= 500) return $"Error del servidor ({status}).";
        return $"Error {status}";
    }

    private static string Desescapar(string valor)
    {
        try
        {
            return Regex.Unescape(valor);
        }
        catch (Exception)
        {
            return valor;
        }
    }
}
