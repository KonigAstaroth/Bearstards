using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Agrega al Lobby el bloque con el jugador logueado y el boton de cerrar sesion.
// Batch: -batchmode -executeMethod ConfigurarLobbySesion.Configurar -quit
public static class ConfigurarLobbySesion
{
    private const string RutaEscena = "Assets/Scenes/Lobby.unity";
    private const string NombreGrupo = "PerfilSesion";

    [MenuItem("Bearstards/Configurar Lobby con sesión")]
    public static void Configurar()
    {
        try
        {
            Scene escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);

            Transform canvas = BuscarEnEscena(escena, "Canvas");
            if (canvas == null) throw new Exception("No se encontro 'Canvas' en " + RutaEscena);

            Transform exitButton = BuscarRecursivo(canvas, "ExitButton");
            if (exitButton == null) throw new Exception("No se encontro 'ExitButton' dentro del Canvas");

            Text textoBase = exitButton.GetComponentInChildren<Text>(true);
            if (textoBase == null) throw new Exception("'ExitButton' no tiene un Text (Legacy) hijo para clonar");

            // Idempotente: si ya existe, se reconstruye.
            Transform viejo = BuscarRecursivo(canvas, NombreGrupo);
            while (viejo != null)
            {
                UnityEngine.Object.DestroyImmediate(viejo.gameObject);
                viejo = BuscarRecursivo(canvas, NombreGrupo);
            }

            // Grupo en la esquina superior derecha (libre: el logo va arriba al centro y Controls abajo a la derecha).
            GameObject grupo = new GameObject(NombreGrupo, typeof(RectTransform));
            grupo.layer = canvas.gameObject.layer;
            RectTransform rtGrupo = grupo.GetComponent<RectTransform>();
            rtGrupo.SetParent(canvas, false);
            rtGrupo.SetAsLastSibling();
            rtGrupo.anchorMin = new Vector2(1f, 1f);
            rtGrupo.anchorMax = new Vector2(1f, 1f);
            rtGrupo.pivot = new Vector2(1f, 1f);
            rtGrupo.anchoredPosition = new Vector2(-40f, -40f);
            rtGrupo.sizeDelta = new Vector2(560f, 260f);
            rtGrupo.localScale = Vector3.one;

            Text nombre = ClonarTexto(textoBase, rtGrupo, "NombreTexto");
            ColocarArriba(nombre.rectTransform, 0f, 50f);
            nombre.fontSize = 36;
            nombre.fontStyle = FontStyle.Bold;
            nombre.alignment = TextAnchor.MiddleRight;
            nombre.color = Color.white;
            nombre.text = "Jugador";

            Text stats = ClonarTexto(textoBase, rtGrupo, "EstadisticasTexto");
            ColocarArriba(stats.rectTransform, -55f, 80f);
            stats.fontSize = 22;
            stats.fontStyle = FontStyle.Normal;
            stats.alignment = TextAnchor.UpperRight;
            stats.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            stats.text = "Puntos: 0 · Partidas: 0 · Kills: 0\nVictorias: 0 · Derrotas: 0 · Empates: 0";

            GameObject botonGo = UnityEngine.Object.Instantiate(exitButton.gameObject, rtGrupo, false);
            botonGo.name = "CerrarSesionButton";
            RectTransform rtBoton = botonGo.GetComponent<RectTransform>();
            rtBoton.anchorMin = new Vector2(1f, 1f);
            rtBoton.anchorMax = new Vector2(1f, 1f);
            rtBoton.pivot = new Vector2(1f, 1f);
            rtBoton.anchoredPosition = new Vector2(0f, -150f);
            rtBoton.sizeDelta = new Vector2(220f, 90f);
            rtBoton.localScale = Vector3.one;

            Button boton = botonGo.GetComponent<Button>();
            if (boton == null) throw new Exception("El clon de 'ExitButton' no tiene Button");
            while (boton.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(boton.onClick, 0);
            }

            Text etiqueta = botonGo.GetComponentInChildren<Text>(true);
            if (etiqueta != null)
            {
                etiqueta.text = "Cerrar sesión";
                etiqueta.fontSize = 26;
                // Que la etiqueta llene el boton y quede centrada
                RectTransform rtEtiqueta = etiqueta.rectTransform;
                rtEtiqueta.anchorMin = Vector2.zero;
                rtEtiqueta.anchorMax = Vector2.one;
                rtEtiqueta.pivot = new Vector2(0.5f, 0.5f);
                rtEtiqueta.offsetMin = new Vector2(10f, 6f);
                rtEtiqueta.offsetMax = new Vector2(-10f, -6f);
                etiqueta.alignment = TextAnchor.MiddleCenter;
                etiqueta.resizeTextForBestFit = true;
                etiqueta.resizeTextMinSize = 14;
                etiqueta.resizeTextMaxSize = 26;
            }
            else
            {
                Debug.LogWarning("[ConfigurarLobbySesion] El boton clonado no tiene Text para la etiqueta");
            }

            LobbyPerfil perfil = grupo.AddComponent<LobbyPerfil>();
            SerializedObject so = new SerializedObject(perfil);
            AsignarCampo(so, "nombreTexto", nombre);
            AsignarCampo(so, "estadisticasTexto", stats);
            AsignarCampo(so, "botonCerrarSesion", boton);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena)) throw new Exception("No se pudo guardar " + RutaEscena);

            Debug.Log("[ConfigurarLobbySesion] OK");
        }
        catch (Exception e)
        {
            Debug.LogError("[ConfigurarLobbySesion] Error: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static Text ClonarTexto(Text original, RectTransform padre, string nombre)
    {
        GameObject go = UnityEngine.Object.Instantiate(original.gameObject, padre, false);
        go.name = nombre;

        // Por si el original tuviera hijos.
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        }

        Text texto = go.GetComponent<Text>();
        texto.raycastTarget = false;
        texto.horizontalOverflow = HorizontalWrapMode.Wrap;
        texto.verticalOverflow = VerticalWrapMode.Overflow;
        texto.rectTransform.localScale = Vector3.one;
        return texto;
    }

    // Estira el texto a lo ancho del grupo, pegado arriba.
    private static void ColocarArriba(RectTransform rt, float y, float alto)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, alto);
    }

    private static void AsignarCampo(SerializedObject so, string campo, UnityEngine.Object valor)
    {
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null) throw new Exception("LobbyPerfil no tiene el campo '" + campo + "'");
        prop.objectReferenceValue = valor;
    }

    private static Transform BuscarEnEscena(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            Transform t = BuscarRecursivo(raiz.transform, nombre);
            if (t != null) return t;
        }
        Debug.LogError("[ConfigurarLobbySesion] Falta el objeto '" + nombre + "' en la escena");
        return null;
    }

    private static Transform BuscarRecursivo(Transform padre, string nombre)
    {
        if (padre.name == nombre) return padre;
        foreach (Transform hijo in padre)
        {
            Transform t = BuscarRecursivo(hijo, nombre);
            if (t != null) return t;
        }
        return null;
    }
}
