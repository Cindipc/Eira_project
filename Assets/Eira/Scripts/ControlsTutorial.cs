using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;

namespace EiraGame
{
    /// <summary>
    /// Tutorial de controles en pantalla.
    ///
    /// No es una lista estática: el panel va marcando cada acción a medida que
    /// el jugador la hace. Es la diferencia entre "saber qué tecla es" y
    /// "haber pulsado ya esa tecla", que es lo que hace que un juego nuevo se
    /// entienda sin manual.
    ///
    /// Se abre solo al empezar el juego, se puede volver a abrir con H y se
    /// salta con Esc o Intro. Cuando el jugador completa todo, el panel se
    /// recoge y deja solo una línea de recordatorio.
    /// </summary>
    public class ControlsTutorial : MonoBehaviour
    {
        // =========================================================
        // PASOS
        // =========================================================

        /// <summary>
        /// Cada paso dice qué hay que hacer y cómo se comprueba. La condición
        /// se evalúa cada frame, así que el tutorial lee el mismo input que
        /// usa el juego: si el control cambia en EiraInput, esto lo sigue sin
        /// tocar nada más.
        /// </summary>
        struct Step
        {
            public string action;
            public string keys;
            public string pad;
            public float hold;          // segundos que hay que mantener
            public System.Func<bool> test;
            public string hint;
        }

        static List<Step> BuildSteps()
        {
            return new List<Step>
            {
                new Step
                {
                    action = "Mover a Eira",
                    keys   = "W A S D",
                    pad    = "Stick izq.",
                    hold   = 0.35f,
                    test   = () => EiraInput.MoveAxis().sqrMagnitude > 0.2f,
                    hint   = "Mantén una dirección para que Eira ande"
                },
                new Step
                {
                    action = "Mirar alrededor",
                    keys   = "Ratón",
                    pad    = "Stick der.",
                    hold   = 0.25f,
                    test   = () => EiraInput.LookDelta().sqrMagnitude > 4f,
                    hint   = "La cámara gira con el ratón"
                },
                new Step
                {
                    action = "Correr",
                    keys   = "Shift",
                    pad    = "L3",
                    hold   = 0.3f,
                    test   = () => EiraInput.Sprint(),
                    hint   = "Corre mientras lo mantienes"
                },
                new Step
                {
                    action = "Saltar",
                    keys   = "Espacio",
                    pad    = "A / Cross",
                    hold   = 0.1f,
                    test   = () => EiraInput.JumpDown() || EiraInput.JumpHeld(),
                    hint   = "Mantén para saltar más alto"
                },
                new Step
                {
                    action = "Agacharse",
                    keys   = "Ctrl / C",
                    pad    = "B / Circle",
                    hold   = 0.3f,
                    test   = () => EiraInput.CrouchHeld(),
                    hint   = "Agáchate para pasar por huecos bajos"
                },
                new Step
                {
                    action = "Disparar",
                    keys   = "Clic izq.",
                    pad    = "RT",
                    hold   = 0.2f,
                    test   = () => EiraInput.AttackDown() || EiraInput.AttackHeld(),
                    hint   = "Mantén el clic para disparar seguido"
                },
                new Step
                {
                    action = "Interactuar",
                    keys   = "E",
                    pad    = "X / Square",
                    hold   = 0.1f,
                    test   = () => EiraInput.InteractDown(),
                    hint   = "Puertas, paneles y terminales"
                },
                new Step
                {
                    action = "Pulso de Eira",
                    keys   = "F  /  Q",
                    pad    = "Y / LT",
                    hold   = 0.1f,
                    test   = () => EiraInput.AbilityDown(),
                    hint   = "Pulso: empuja y daña lo que tengas cerca"
                },
            };
        }

        // =========================================================
        // ESTADO
        // =========================================================

        // Colores: los mismos tonos que usa HUDManager para que el panel no
        // parezca de otro juego.
        static readonly Color Bg = new Color(0.03f, 0.05f, 0.08f, 0.82f);
        static readonly Color RowBg = new Color(1f, 1f, 1f, 0.04f);
        static readonly Color ActiveBg = new Color(0.4f, 1f, 0.7f, 0.16f);
        static readonly Color DoneBg = new Color(0.3f, 0.85f, 0.45f, 0.2f);
        static readonly Color PendingText = new Color(0.55f, 0.62f, 0.7f, 1f);
        static readonly Color ActiveText = Color.white;
        static readonly Color DoneText = new Color(0.45f, 0.95f, 0.6f, 1f);
        static readonly Color KeyBg = new Color(0.12f, 0.2f, 0.3f, 1f);
        static readonly Color Accent = new Color(0.4f, 1f, 0.7f, 1f);

        List<Step> steps;
        float[] progress;
        Row[] rows;

        class Row
        {
            public RectTransform rect;
            public Image background;
            public Image check;
            public Text keyLabel;
            public Text padLabel;
            public Text actionLabel;
            public Text hintLabel;
        }

        Font font;
        Sprite keySprite;

        GameObject panel;
        Image panelImage;
        Text progressLabel;
        Text hintLabel;

        bool open = true;
        bool finished;
        int current;
        float sinceStart;
        float rowPulse;

        // =========================================================
        // ARRANQUE
        // =========================================================

        /// <summary>
        /// Se crea solo al entrar en una escena de juego. Se hace por código y
        /// no dejando un objeto en la escena para que no se pierda al
        /// regenerar el nivel con el constructor de escenarios, que rehace los
        /// GameObjects de la escena.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<ControlsTutorial>() != null)
                return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            if (!scene.IsValid() || IsIntroScene(scene.name))
                return;

            new GameObject("ControlsTutorial").AddComponent<ControlsTutorial>();
        }

        void Awake()
        {
            // Se construye su propio Canvas en vez de colgarse del HUD: así el
            // tutorial aparece aunque el HUD todavía no exista, y no se rompe
            // si alguien lo quita de la escena.
            EnsureFont();

            steps = BuildSteps();
            progress = new float[steps.Count];

            BuildCanvas();
            BuildPanel();

            // En la intro no hay nada que tutorializar todavía: solo en
            // gameplay.
            open = !IsIntroScene();
            panel.SetActive(open);
        }

        static bool IsIntroScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            return scene.IsValid() && IsIntroScene(scene.name);
        }

        static bool IsIntroScene(string name)
        {
            return name == EiraConst.IntroScene
                || name == EiraConst.IntroVideoScene;
        }

        void EnsureFont()
        {
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
        }

        Sprite KeySprite
        {
            get
            {
                if (keySprite != null)
                    return keySprite;

                // Rectángulo redondeado generado por código. El proyecto no
                // tiene sprite sheet y una Image sin sprite sale como un
                // bloque opaco.
                const int size = 32;
                const int radius = 8;

                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "KeyCap",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                var px = new Color32[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        px[y * size + x] = Rounded(x, y, size, radius);
                    }
                }

                tex.SetPixels32(px);
                tex.Apply();

                keySprite = Sprite.Create(
                    tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f
                );

                return keySprite;
            }
        }

        static Color32 Rounded(int x, int y, int size, int radius)
        {
            // Distancia con esquinas redondeadas: dentro del radio, opaco;
            // fuera, transparente. Suficiente para una tecla y mucho más
            // barato que un shader.
            float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0f);
            float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            float a = Mathf.Clamp01(radius - d);

            return new Color32(255, 255, 255, (byte)(a * 255f));
        }

        void BuildCanvas()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject(
                    "EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)
                );

                try { es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
                catch { }
            }

            var go = new GameObject(
                "ControlsTutorialCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)
            );

            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Por encima del HUD (que va en 10) pero por debajo de las
            // pantallas de victoria y derrota, que son modales.
            canvas.sortingOrder = 15;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // =========================================================
        // PANEL
        // =========================================================

        void BuildPanel()
        {
            const float rowH = 54f;
            const float headerH = 96f;
            const float footerH = 40f;
            const float width = 430f;

            float height = headerH + rowH * steps.Count + footerH + 24f;

            // El panel siempre va bajo el Canvas que acabamos de crear, nunca
            // bajo el propio ControlsTutorial. Así la jerarquía UI es correcta
            // y no depende de dónde esté colgado el componente.
            var canvas = GetComponentInChildren<Canvas>(true);
            Transform parent = canvas != null ? canvas.transform : transform;

            panel = new GameObject("ControlsTutorialPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-30, 0);
            rt.sizeDelta = new Vector2(width, height);

            panelImage = panel.GetComponent<Image>();
            panelImage.color = Bg;
            panelImage.raycastTarget = false;

            // Barra de acento arriba: da identidad al panel sin necesidad de
            // imágenes.
            var accentBar = MakeImage("Accent", panel.transform, Accent);
            var art = accentBar.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0, 1);
            art.anchorMax = new Vector2(1, 1);
            art.pivot = new Vector2(0.5f, 1f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(0, 4);
            accentBar.GetComponent<Image>().raycastTarget = false;

            var title = MakeText(
                "Title", panel.transform, "CONTROLES", 26, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -14),
                new Vector2(300, 34), Color.white
            );

            title.raycastTarget = false;

            progressLabel = MakeText(
                "Progress", panel.transform, "", 18, TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -14),
                new Vector2(90, 34), new Color(0.6f, 0.7f, 0.8f)
            );

            // Línea separadora bajo la cabecera.
            var line = MakeImage("Divider", panel.transform, new Color(1, 1, 1, 0.12f));
            var lrt = line.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 1);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0, -headerH);
            lrt.sizeDelta = new Vector2(-28, 1);
            line.GetComponent<Image>().raycastTarget = false;

            rows = new Row[steps.Count];

            for (int i = 0; i < steps.Count; i++)
            {
                rows[i] = BuildRow(panel.transform, i, -headerH - 10 - rowH * (i + 0.5f), rowH, width);
            }

            // Pie: recordatorio permanente.
            var footer = MakeImage("Footer", panel.transform, RowBg);
            var frt = footer.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0, 0);
            frt.anchorMax = new Vector2(1, 0);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.anchoredPosition = new Vector2(0, 10);
            frt.sizeDelta = new Vector2(-28, footerH);
            footer.raycastTarget = false;

            MakeText(
                "FooterText", footer.transform, "H  ayuda    ·    Esc  saltar", 17,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero,
                new Vector2(0, 0), new Color(0.65f, 0.72f, 0.8f)
            );

            hintLabel = MakeText(
                "Hint", panel.transform, "", 17, TextAnchor.UpperCenter,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, footerH + 16),
                new Vector2(-30, 40), new Color(0.5f, 0.58f, 0.68f)
            );
        }

        Row BuildRow(Transform parent, int index, float y, float rowH, float width)
        {
            var go = new GameObject("Row" + index, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, y + rowH * 0.5f);
            rt.sizeDelta = new Vector2(-28, rowH - 6);

            var bg = go.GetComponent<Image>();
            bg.color = RowBg;
            bg.raycastTarget = false;

            var row = new Row { rect = rt, background = bg };

            // Casilla de completada a la izquierda.
            var check = MakeImage("Check", go.transform, new Color(1, 1, 1, 0.25f));
            var crt = check.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0.5f);
            crt.anchorMax = new Vector2(0, 0.5f);
            crt.pivot = new Vector2(0, 0.5f);
            crt.anchoredPosition = new Vector2(10, 0);
            crt.sizeDelta = new Vector2(18, 18);
            check.GetComponent<Image>().raycastTarget = false;
            row.check = check.GetComponent<Image>();

            // Tecla de teclado en una "capuchón".
            var cap = MakeImage("KeyCap", go.transform, KeyBg);
            cap.GetComponent<Image>().sprite = KeySprite;
            cap.GetComponent<Image>().type = Image.Type.Sliced;
            cap.GetComponent<Image>().raycastTarget = false;

            var krt = cap.GetComponent<RectTransform>();
            krt.anchorMin = new Vector2(0, 0.5f);
            krt.anchorMax = new Vector2(0, 0.5f);
            krt.pivot = new Vector2(0, 0.5f);
            krt.anchoredPosition = new Vector2(38, 0);
            krt.sizeDelta = new Vector2(30, 30);

            row.keyLabel = MakeText(
                "Keys", go.transform, steps[index].keys, 15, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, 0), Color.white
            );

            // Etiqueta de mando a la derecha.
            row.padLabel = MakeText(
                "Pad", go.transform, steps[index].pad, 15, TextAnchor.MiddleRight,
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0),
                new Vector2(140, 30), new Color(0.55f, 0.7f, 0.9f)
            );

            // Nombre de la acción, encima de la fila.
            row.actionLabel = MakeText(
                "Action", go.transform, steps[index].action, 19, TextAnchor.MiddleLeft,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 0),
                new Vector2(240, 30), PendingText
            );

            return row;
        }

        Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<Image>();
        }

        Text MakeText(
            string name, Transform parent, string value, int size, TextAnchor align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(
                anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f,
                anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f
            );
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = value;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            return t;
        }

        // =========================================================
        // BUCLE
        // =========================================================

        void Update()
        {
            // unscaled: el tutorial tiene que seguir funcionando con el juego
            // en pausa, que es justo cuando se consulta la ayuda.
            float dt = Time.unscaledDeltaTime;

            sinceStart += dt;

            if (ToggleDown())
                SetOpen(!open);

            if (open && (SkipDown() || ConfirmDown()))
            {
                SetOpen(false);
                MarkAllDone();
                return;
            }

            if (!open)
                return;

            UpdateProgress(dt);
            UpdateVisuals(dt);
        }

        bool ToggleDown()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;

            if (kb != null && (kb.hKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame))
                return true;

            var pad = UnityEngine.InputSystem.Gamepad.current;

            return pad != null && pad.selectButton.wasPressedThisFrame;
        }

        bool SkipDown()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                return true;

            var pad = UnityEngine.InputSystem.Gamepad.current;

            return pad != null && pad.startButton.wasPressedThisFrame;
        }

        bool ConfirmDown()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;

            if (kb != null && kb.enterKey.wasPressedThisFrame)
                return true;

            var pad = UnityEngine.InputSystem.Gamepad.current;

            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        void SetOpen(bool value)
        {
            open = value;

            if (panel != null)
                panel.SetActive(open);
        }

        void UpdateProgress(float dt)
        {
            bool anyPending = false;

            for (int i = 0; i < steps.Count; i++)
            {
                if (progress[i] >= steps[i].hold)
                    continue;

                bool doing = false;

                try
                {
                    doing = steps[i].test != null && steps[i].test();
                }
                catch
                {
                    // Un paso mal escrito no puede tumbar el tutorial.
                    doing = false;
                }

                if (doing)
                    progress[i] += dt;
                else
                    progress[i] = 0f;

                if (progress[i] < steps[i].hold)
                    anyPending = true;
            }

            current = FirstPending();

            if (!anyPending && !finished)
            {
                finished = true;

                Debug.Log(
                    "[Eira] Tutorial de controles completado. " +
                    "El panel se recoge; se puede volver a abrir con H."
                );

                SetOpen(false);
            }
        }

        int FirstPending()
        {
            for (int i = 0; i < steps.Count; i++)
                if (progress[i] < steps[i].hold)
                    return i;

            return -1;
        }

        void MarkAllDone()
        {
            for (int i = 0; i < steps.Count; i++)
                progress[i] = steps[i].hold;

            current = -1;
            finished = true;
        }

        void UpdateVisuals(float dt)
        {
            rowPulse += dt;

            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                bool done = progress[i] >= steps[i].hold;
                bool active = !done && i == current;

                Color bg = done ? DoneBg : active ? ActiveBg : RowBg;

                // Latido suave en la fila activa: llama la atención sin
                // molestar con un parpadeo.
                if (active)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(rowPulse * 3.4f);
                    bg.a = Mathf.Lerp(ActiveBg.a, ActiveBg.a + 0.12f, pulse);
                }

                row.background.color = bg;
                row.check.color = done
                    ? new Color(0.45f, 0.95f, 0.6f, 1f)
                    : new Color(1, 1, 1, active ? 0.7f : 0.2f);

                row.actionLabel.color = done ? DoneText : active ? ActiveText : PendingText;
                row.keyLabel.color = done ? DoneText : Color.white;
                row.padLabel.color = done
                    ? new Color(0.45f, 0.8f, 0.9f, 0.8f)
                    : new Color(0.55f, 0.7f, 0.9f, 0.6f + (active ? 0.3f : 0f));

                // La fila hecha se encoge un poco: el panel deja de ser una
                // lista plana y se ve qué falta.
                float scale = done ? 0.97f : 1f;
                row.rect.localScale = new Vector3(1f, scale, 1f);
            }

            int completed = 0;

            for (int i = 0; i < steps.Count; i++)
                if (progress[i] >= steps[i].hold)
                    completed++;

            progressLabel.text = completed + " / " + steps.Count;

            string hint = current >= 0 && current < steps.Count
                ? steps[current].hint
                : "";

            if (hintLabel.text != hint)
                hintLabel.text = hint;

            // Los primeros 2.5 s el panel entra desde la derecha.
            float slide = sinceStart < 0.35f
                ? Mathf.SmoothStep(0, 1, sinceStart / 0.35f)
                : 1f;

            if (slide < 1f)
            {
                var rt = panel.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(-30 + (1f - slide) * 90f, 0);
            }
        }
    }
}
