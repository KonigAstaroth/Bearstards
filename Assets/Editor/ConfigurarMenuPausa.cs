using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Arma el menú de pausa (Esc) en la escena de la partida, con el botón y la fuente del Lobby.
// Batch: -batchmode -executeMethod ConfigurarMenuPausa.Configurar -quit
public static class ConfigurarMenuPausa
{
    private const string RutaPartida = "Assets/Scenes/Store.unity";
    private const string RutaLobby = "Assets/Scenes/Lobby.unity";
    private const string NombreRaiz = "MenuPausa";

    [MenuItem("Bearstards/Configurar menú de pausa")]
    public static void Configurar()
    {
        Scene lobby = default;
        try
        {
            Scene partida = EditorSceneManager.OpenScene(RutaPartida, OpenSceneMode.Single);
            lobby = EditorSceneManager.OpenScene(RutaLobby, OpenSceneMode.Additive);

            Transform botonBase = BuscarEnEscena(lobby, "ExitButton");
            if (botonBase == null || botonBase.GetComponent<Button>() == null)
                throw new Exception("No se encontró 'ExitButton' (con Button) en " + RutaLobby);
            Text textoBase = botonBase.GetComponentInChildren<Text>(true);
            if (textoBase == null) throw new Exception("'ExitButton' no tiene un Text (Legacy) hijo");

            SceneManager.SetActiveScene(partida);

            // Idempotente: se reconstruye desde cero.
            foreach (GameObject raiz in partida.GetRootGameObjects())
            {
                if (raiz.name == NombreRaiz) UnityEngine.Object.DestroyImmediate(raiz);
            }

            // Canvas propio encima de todo, escalado igual en cualquier resolución.
            var raizGo = new GameObject(NombreRaiz, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(raizGo, partida);
            var canvas = raizGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var escalador = raizGo.GetComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920, 1080);
            escalador.matchWidthOrHeight = 0.5f;

            // Fondo oscuro de pantalla completa (se activa con Esc)
            var panel = CrearUI("Panel", raizGo.transform);
            Estirar(panel);
            var fondo = panel.gameObject.AddComponent<Image>();
            fondo.color = new Color(0f, 0f, 0f, 0.75f);

            ClonarTexto(textoBase, panel, "Titulo", "PAUSED", 80, new Vector2(0, 260), new Vector2(900, 120));

            var opciones = CrearUI("Opciones", panel);
            Estirar(opciones);
            var continuar = ClonarBoton(botonBase, opciones, "ResumeButton", "Resume", new Vector2(0, 80));
            var salirPartida = ClonarBoton(botonBase, opciones, "LeaveMatchButton", "Leave match", new Vector2(0, -40));
            var salirJuego = ClonarBoton(botonBase, opciones, "QuitGameButton", "Quit game", new Vector2(0, -160));

            var confirmar = CrearUI("Confirmar", panel);
            Estirar(confirmar);
            var pregunta = ClonarTexto(textoBase, confirmar, "Pregunta", "Leave the match?", 40, new Vector2(0, 60), new Vector2(1000, 140));
            var si = ClonarBoton(botonBase, confirmar, "YesButton", "Yes", new Vector2(-150, -100));
            var no = ClonarBoton(botonBase, confirmar, "NoButton", "No", new Vector2(150, -100));
            confirmar.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            var menu = raizGo.AddComponent<MenuPausa>();
            var so = new SerializedObject(menu);
            Asignar(so, "panel", panel.gameObject);
            Asignar(so, "panelOpciones", opciones.gameObject);
            Asignar(so, "panelConfirmar", confirmar.gameObject);
            Asignar(so, "textoConfirmar", pregunta);
            Asignar(so, "botonContinuar", continuar);
            Asignar(so, "botonSalirPartida", salirPartida);
            Asignar(so, "botonSalirJuego", salirJuego);
            Asignar(so, "botonConfirmar", si);
            Asignar(so, "botonCancelar", no);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Sin EventSystem los botones no reciben clics.
            if (!TieneEventSystem(partida))
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(es, partida);
            }

            EditorSceneManager.CloseScene(lobby, true); // el Lobby no se guarda
            lobby = default;
            EditorSceneManager.MarkSceneDirty(partida);
            EditorSceneManager.SaveScene(partida);
            Debug.Log("[ConfigurarMenuPausa] OK");
        }
        catch (Exception e)
        {
            Debug.LogError("[ConfigurarMenuPausa] " + e);
            if (lobby.IsValid() && lobby.isLoaded) EditorSceneManager.CloseScene(lobby, true);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static bool TieneEventSystem(Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.GetComponentInChildren<EventSystem>(true) != null) return true;
        }
        return false;
    }

    private static RectTransform CrearUI(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = 5; // UI
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        return rt;
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Text ClonarTexto(Text original, Transform padre, string nombre, string texto, int tamano, Vector2 pos, Vector2 tam)
    {
        var go = UnityEngine.Object.Instantiate(original.gameObject, padre, false);
        go.name = nombre;
        var t = go.GetComponent<Text>();
        t.text = texto;
        t.color = Color.white;
        t.fontSize = tamano;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 14;
        t.resizeTextMaxSize = tamano;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        rt.localScale = Vector3.one;
        return t;
    }

    private static Button ClonarBoton(Transform original, Transform padre, string nombre, string texto, Vector2 pos)
    {
        var go = UnityEngine.Object.Instantiate(original.gameObject, padre, false);
        go.name = nombre;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;

        var boton = go.GetComponent<Button>();
        while (boton.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(boton.onClick, 0);
        boton.interactable = true;

        var etiqueta = go.GetComponentInChildren<Text>(true);
        if (etiqueta != null) etiqueta.text = texto;
        return boton;
    }

    private static void Asignar(SerializedObject so, string campo, UnityEngine.Object valor)
    {
        var prop = so.FindProperty(campo);
        if (prop == null) throw new Exception("MenuPausa no tiene el campo " + campo);
        prop.objectReferenceValue = valor;
    }

    private static Transform BuscarEnEscena(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            Transform t = BuscarRecursivo(raiz.transform, nombre);
            if (t != null) return t;
        }
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
