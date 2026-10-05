using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;

namespace EiraGame
{
    // HUD construido por código: puntos, vidas, objetivos, misiones, barra de jefe,
    // subtítulos, daño y pantallas de victoria/derrota.
    public class HUDManager : MonoBehaviour
    {
        static Font cachedFont;

        Text scoreText, heartsText, objectiveText, promptText, logText, subtitleText;
        Text missionHeader, missionLines, controlsText;
        GameObject missionsPanel, bossObj, damageObj, winPanel, overPanel, controlsObj, pausePanel;
        GameObject reticleObj;
        GameObject markerObj;
        Transform markerArrow;
        Image heartFill, healthFill, bossFill, damageImg;
        float subtitleTimer, logTimer, damageFade, controlsTimer;
        float reticleKick;
        readonly List<(string, bool)> missionList = new List<(string, bool)>();

        // Sprites de la mira generados por código: el proyecto no tiene
        // sprite sheet, y una Image sin sprite es un rectángulo opaco que
        // tapa media pantalla.
        static Sprite ringSprite;
        static Sprite dotSprite;
        static Sprite arrowSprite;
        static float reticleBaseSize = 20f;

        static Sprite RingSprite
        {
            get
            {
                if (ringSprite == null)
                    ringSprite = MakeRingSprite(64, 4f);

                return ringSprite;
            }
        }

        static Sprite DotSprite
        {
            get
            {
                if (dotSprite == null)
                    dotSprite = MakeDotSprite(16);

                return dotSprite;
            }
        }

        /// <summary>Triángulo apuntando hacia arriba (se rota hacia el objetivo).</summary>
        static Sprite ArrowSprite
        {
            get
            {
                if (arrowSprite != null)
                    return arrowSprite;

                const int w = 40;
                const int h = 46;

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "ObjectiveArrow",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                var pixels = new Color[w * h];

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // Triangulo: mas ancho abajo, punta arriba.
                        float t = (float)y / (h - 1);
                        float halfWidth = Mathf.Lerp(1f, w * 0.5f - 1f, t);

                        float d = Mathf.Abs(x - w * 0.5f);

                        // Se deja un hueco en el centro para leer como flecha.
                        bool inner = t > 0.45f && d < halfWidth * 0.45f;

                        float a = d <= halfWidth && !inner ? 1f : 0f;

                        pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                    }
                }

                tex.SetPixels(pixels);
                tex.Apply();

                arrowSprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, w, h),
                    new Vector2(0.5f, 0.5f),
                    100f);

                return arrowSprite;
            }
        }

        static Sprite MakeRingSprite(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ReticleRing",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float r = size * 0.5f;
            float inner = r - thickness;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(
                        new Vector2(x + 0.5f, y + 0.5f),
                        new Vector2(r, r));

                    float a = Mathf.Clamp01((d - inner) / 1.5f);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        static Sprite MakeDotSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ReticleDot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float r = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(
                        new Vector2(x + 0.5f, y + 0.5f),
                        new Vector2(r, r));

                    tex.SetPixel(
                        x, y,
                        new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
            }

            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            EnsureFont();
            BuildCanvas();
            Subscribe();
            RefreshLives();
            RefreshMissions();
            RefreshBars();
        }

        void EnsureFont()
        {
            if (cachedFont != null) return;
            try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (cachedFont == null)
            {
                try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
        }

        Font F => cachedFont;

        void Subscribe()
        {
            GameEvents.OnScoreAdded += a => { if (scoreText != null) scoreText.text = "PUNTOS  " + GameManager.Instance.Score; };
            GameEvents.OnObjective += s => { if (objectiveText != null) objectiveText.text = s; };
            GameEvents.OnInteractPrompt += s =>
            {
                if (promptText == null) return;
                promptText.text = string.IsNullOrEmpty(s) ? "" : s;
                promptText.gameObject.SetActive(!string.IsNullOrEmpty(s));
            };
            GameEvents.OnNovaSpeak += (s, d) => ShowSubtitle(s, d);
            GameEvents.OnSubtitle += (s, d) => ShowSubtitle(s, d);
            GameEvents.OnPickedInfo += s =>
            {
                if (logText != null) { logText.text = "▸ " + s; logText.gameObject.SetActive(true); }
                logTimer = 9f;
            };
            GameEvents.OnHeartDanger += () =>
            {
                if (heartsText != null) heartsText.color = Color.red;
                DamageTint(new Color(0.9f, 0.3f, 0.2f), 0.25f);
            };
            GameEvents.OnSetBossBar += f =>
            {
                if (bossObj == null) return;
                bossObj.SetActive(f > 0f);
                if (bossFill != null) bossFill.fillAmount = Mathf.Clamp01(f);
            };
            GameEvents.OnObjectiveMarkerMoved += p =>
            {
                if (markerObj == null) return;
                if (p == Vector3.zero)
                {
                    markerObj.SetActive(false);
                    return;
                }
                markerObj.SetActive(true);
                markerWorld = p;
            };
            GameEvents.OnDroneDestroyed += p =>
            {
                if (logText != null)
                {
                    logText.text = "▸ Dron derribado  +" + p;
                    logText.gameObject.SetActive(true);
                    logTimer = 6f;
                }
            };
        }

        Vector3 markerWorld;
        Camera uiCamera;

        /// <summary>
        /// Dibuja la flecha del objetivo en el borde de la pantalla cuando el
        /// punto queda fuera de cuadro. Es la parte que evita que la jugadora
        /// deambule: si el objetivo no se ve, la flecha dice hacia donde ir.
        /// </summary>
        private void UpdateObjectiveArrow()
        {
            if (markerObj == null)
                return;

            var gm = GameManager.Instance;

            if (gm == null || gm.State != GameState.Playing)
            {
                markerObj.SetActive(false);
                return;
            }

            if (uiCamera == null)
                uiCamera = Camera.main;

            if (uiCamera == null)
            {
                markerObj.SetActive(false);
                return;
            }

            Vector3 screen = uiCamera.WorldToScreenPoint(markerWorld);

            bool behind = screen.z <= 0f;

            // Si esta delante y dentro de la pantalla, el anillo 3D ya
            // sirve de guia: la flecha solo aparece en el borde.
            if (!behind && screen.x > 60f && screen.x < Screen.width - 60f &&
                screen.y > 90f && screen.y < Screen.height - 60f)
            {
                markerObj.SetActive(false);
                return;
            }

            markerObj.SetActive(true);

            // Detras de la camara: se proyecta al lado contrario.
            if (behind)
            {
                screen.x = Screen.width - screen.x;
                screen.y = 40f;
            }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 toTarget = new Vector2(screen.x, screen.y) - center;

            if (toTarget.sqrMagnitude < 0.01f)
                toTarget = Vector2.up;

            toTarget.Normalize();

            float margin = 70f;

            float x = Mathf.Clamp(
                center.x + toTarget.x * (Screen.width * 0.5f - margin),
                margin,
                Screen.width - margin);

            float y = Mathf.Clamp(
                center.y + toTarget.y * (Screen.height * 0.5f - margin),
                margin,
                Screen.height - margin);

            var rt = markerObj.GetComponent<RectTransform>();

            if (rt != null)
                rt.anchoredPosition = new Vector2(x, y);

            // La flecha apunta hacia el objetivo, no siempre hacia arriba.
            float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;

            if (markerArrow != null)
                markerArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        void BuildCanvas()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                try { es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); } catch { }
            }

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var root = canvasGo.transform;

            // --- parte superior izquierda: puntos y vidas ---
            var scoreRect = MakeLabel(root, "PUNTOS  0", 36, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(600, 50), Color.white);
            scoreText = scoreRect.GetComponent<Text>();

            heartsText = MakeTextScaled(root, "♥ ♥ ♥", 34, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -70), new Vector2(400, 45), new Color(1f, 0.35f, 0.35f));

            // --- barras de vida y corazón (superior derecha) ---
            BuildBar(root, "HealthBar", new Color(0.2f, 0.7f, 0.4f), out healthFill, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-380, -20), new Vector2(360, 34));
            BuildBar(root, "HeartBar", new Color(0.2f, 0.55f, 1f), out heartFill, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-380, -56), new Vector2(360, 30));

            var healthTxt = MakeTextScaled(root, "VIDA", 16, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-756, -22), new Vector2(330, 30), new Color(0.8f, 0.9f, 0.8f));
            var heartTxt = MakeTextScaled(root, "CORAZÓN", 16, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-756, -58), new Vector2(330, 30), new Color(0.8f, 0.9f, 1f));

            // --- objetivo (abajo-centro) ---
            objectiveText = MakeTextScaled(root, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1400, 50), new Color(0.9f, 0.95f, 1f));

            // --- prompt de interacción ---
            promptText = MakeTextScaled(root, "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1200, 40), new Color(0.4f, 1f, 0.7f));
            promptText.gameObject.SetActive(false);

            // --- subtítulos / diálogo ---
            subtitleText = MakeTextScaled(root, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), new Vector2(0, 0), new Vector2(1500, 120), new Color(1f, 0.95f, 0.8f));
            subtitleText.gameObject.SetActive(false);

            // --- log de información (abajo-izquierda) ---
            logText = MakeTextScaled(root, "", 24, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 40), new Vector2(900, 120), new Color(0.7f, 0.85f, 1f));
            logText.gameObject.SetActive(false);

            // --- controles (abajo-derecha, se apaga sola) ---
            // Los textos deben coincidir con EiraInput: si divergen, el juego
            // parece roto aunque funcione.
            controlsObj = new GameObject("Controls", typeof(RectTransform));
            controlsObj.transform.SetParent(root, false);
            var crt2 = controlsObj.GetComponent<RectTransform>();
            crt2.anchorMin = new Vector2(1, 0);
            crt2.anchorMax = new Vector2(1, 0);
            crt2.pivot = new Vector2(1, 0);
            crt2.anchoredPosition = new Vector2(-30, 30);
            crt2.sizeDelta = new Vector2(430, 190);
            var cbg = controlsObj.AddComponent<Image>();
            cbg.color = new Color(0.03f, 0.05f, 0.08f, 0.7f);
            cbg.raycastTarget = false;
            controlsText = MakeTextScaled(controlsObj.transform,
                "WASD / flechas  Mover\n" +
                "Ratón            Cámara\n" +
                "Shift            Correr\n" +
                "Espacio          Saltar\n" +
                "Ctrl / C         Agacharse\n" +
                "Clic izq.        Disparar (mantén)\n" +
                "E                Interactuar\n" +
                "F (o Q)          Pulso\n" +
                "Tab              Misiones\n" +
                "H                Ayuda de controles\n" +
                "Esc              Pausa",
                18, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -10), new Vector2(410, 198),
                new Color(0.82f, 0.88f, 0.95f));
            controlsTimer = 18f;

            // --- mira (centro) ---
            // El disparo se calcula hacia el centro de la pantalla, así que
            // la mira tiene que estar ahí: si no, el jugador ve disparar
            // hacia otro lado.
            reticleObj = new GameObject("Reticle", typeof(RectTransform));
            reticleObj.transform.SetParent(root, false);
            var rr = reticleObj.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.5f, 0.5f);
            rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.pivot = new Vector2(0.5f, 0.5f);
            rr.anchoredPosition = Vector2.zero;
            rr.sizeDelta = new Vector2(26, 26);

            var ring = new GameObject("Ring", typeof(RectTransform));
            ring.transform.SetParent(reticleObj.transform, false);
            var ringRt = ring.GetComponent<RectTransform>();
            ringRt.anchorMin = new Vector2(0.5f, 0.5f);
            ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(20, 20);
            var ringImg = ring.AddComponent<Image>();
            ringImg.sprite = RingSprite;
            ringImg.color = new Color(0.4f, 0.95f, 1f, 0.85f);
            ringImg.raycastTarget = false;

            var dot = new GameObject("Dot", typeof(RectTransform));
            dot.transform.SetParent(reticleObj.transform, false);
            var dotRt = dot.GetComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0.5f, 0.5f);
            dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = new Vector2(4, 4);
            var dotImg = dot.AddComponent<Image>();
            dotImg.sprite = DotSprite;
            dotImg.color = new Color(0.4f, 0.95f, 1f, 0.95f);
            dotImg.raycastTarget = false;

            reticleBaseSize = 20f;

            // --- flecha de objetivo (borde de pantalla) ---
            markerObj = new GameObject("ObjectiveArrow", typeof(RectTransform));
            markerObj.transform.SetParent(root, false);
            var mr = markerObj.GetComponent<RectTransform>();
            mr.anchorMin = new Vector2(0f, 0f);
            mr.anchorMax = new Vector2(0f, 0f);
            mr.pivot = new Vector2(0.5f, 0.5f);
            mr.sizeDelta = new Vector2(38, 44);
            var mbg = markerObj.AddComponent<Image>();
            mbg.sprite = ArrowSprite;
            mbg.color = new Color(0.35f, 1f, 0.7f, 0.95f);
            mbg.raycastTarget = false;
            markerArrow = mbg.transform;
            markerObj.SetActive(false);

            // --- panel de misiones ---
            missionsPanel = new GameObject("MissionsPanel", typeof(RectTransform));
            missionsPanel.transform.SetParent(root, false);
            var mp = missionsPanel.GetComponent<RectTransform>();
            mp.anchorMin = new Vector2(1, 0.5f); mp.anchorMax = new Vector2(1, 0.5f);
            mp.pivot = new Vector2(1, 0.5f);
            mp.anchoredPosition = new Vector2(-30, 0);
            mp.sizeDelta = new Vector2(460, 360);
            var mpImg = missionsPanel.AddComponent<Image>();
            mpImg.color = new Color(0.03f, 0.05f, 0.08f, 0.85f);
            missionHeader = MakeTextScaled(missionsPanel.transform, "MISIONES  [Tab]", 26, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -10), new Vector2(430, 40), new Color(0.6f, 0.8f, 1f));
            missionLines = MakeTextScaled(missionsPanel.transform, "", 24, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -60), new Vector2(430, 300), Color.white);
            MakeTextScaled(missionsPanel.transform, "Pulsa Tab para alternar", 16, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(400, 24), new Color(0.5f, 0.6f, 0.7f));
            missionsPanel.SetActive(false);

            // --- barra del jefe ---
            bossObj = new GameObject("BossBar", typeof(RectTransform));
            bossObj.transform.SetParent(root, false);
            var bb = bossObj.GetComponent<RectTransform>();
            bb.anchorMin = new Vector2(0.5f, 1f); bb.anchorMax = new Vector2(0.5f, 1f);
            bb.anchoredPosition = new Vector2(0, -90);
            bb.sizeDelta = new Vector2(700, 26);
            var tag = new GameObject("BossTag", typeof(RectTransform));
            tag.transform.SetParent(bossObj.transform, false);
            var tagRt = tag.GetComponent<RectTransform>();
            tagRt.anchorMin = new Vector2(0, 1); tagRt.anchorMax = new Vector2(1, 1);
            tagRt.anchoredPosition = new Vector2(0, 20); tagRt.sizeDelta = new Vector2(0, 26);
            MakeTextScaled(tag.transform, "MÁQUINA GUARDIANA   —   Activa los 3 terminales con F", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(700, 26), new Color(1f, 0.5f, 0.4f));
            var bbBg = bossObj.AddComponent<Image>();
            bbBg.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(bossObj.transform, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(3, 3); frt.offsetMax = new Vector2(-3, -3);
            bossFill = fillGo.GetComponent<Image>();
            bossFill.type = Image.Type.Filled;
            bossFill.fillMethod = Image.FillMethod.Horizontal;
            bossFill.color = new Color(1f, 0.3f, 0.2f);
            bossObj.SetActive(false);

            // --- overlay de daño ---
            damageObj = new GameObject("DamageOverlay", typeof(RectTransform));
            damageObj.transform.SetParent(root, false);
            var drt = damageObj.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            damageImg = damageObj.AddComponent<Image>();
            damageImg.color = new Color(0.6f, 0.05f, 0.03f, 0f);
            damageImg.raycastTarget = false;
            damageObj.SetActive(false);

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string levelTitle = GetLevelTitle(currentScene);

            // --- pantallas finales ---
            winPanel = BuildCenterPanel(root, "VICTORIA", levelTitle, new Color(0.15f, 0.35f, 0.2f));
            overPanel = BuildCenterPanel(root, "DERROTA", levelTitle, new Color(0.4f, 0.1f, 0.1f));

            foreach (GameObject end in new[] { winPanel, overPanel })
            {
                MakeButton(end.transform, "Jugar de nuevo", new Vector2(0, -320), () =>
                {
                    if (GameManager.Instance != null) GameManager.Instance.LoadNextLevel();
                });
                MakeButton(end.transform, "Menú principal", new Vector2(0, -390), () =>
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(EiraConst.IntroVideoScene);
                });
                end.SetActive(false);
            }

            // --- pausa ---
            pausePanel = BuildCenterPanel(root, "PAUSA", levelTitle, new Color(0.2f, 0.4f, 0.6f));
            var pauseDetail = pausePanel.transform.Find("PanelDetail")?.GetComponent<Text>();
            if (pauseDetail != null)
                pauseDetail.text = "WASD mover · Ratón cámara · Shift correr\nEspacio saltar · Ctrl agacharse\nE interactuar · F pulso\nH ayuda de controles";
            MakeButton(pausePanel.transform, "Continuar", new Vector2(0, -290), () =>
            {
                if (GameManager.Instance != null) GameManager.Instance.TogglePause();
            });
            MakeButton(pausePanel.transform, "Reiniciar nivel", new Vector2(0, -360), () =>
            {
                if (GameManager.Instance != null) GameManager.Instance.RestartLevel();
            });
            MakeButton(pausePanel.transform, "Menú principal", new Vector2(0, -430), () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(EiraConst.IntroVideoScene);
            });
            pausePanel.SetActive(false);
        }

        private string GetLevelTitle(string sceneName)
        {
            return sceneName switch
            {
                EiraConst.Level1Scene => "Nivel 1 · El Despertar",
                EiraConst.Level2Scene => "Nivel 2 · Calles Destruidas",
                EiraConst.Level3Scene => "Nivel 3 · El Núcleo",
                _ => "Nivel Desconocido"
            };
        }

        void BuildBar(Transform parent, string name, Color fillColor, out Image fill, Vector2 anchor, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var bg = new GameObject(name, typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(parent, false);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            bg.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            var fgo = new GameObject(bg.name + "Fill", typeof(Image));
            fgo.transform.SetParent(bg.transform, false);
            var frt = fgo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2, 2); frt.offsetMax = new Vector2(-2, -2);
            fill = fgo.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = fillColor;
        }

        Text MakeLabel(Transform parent, string text, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            return MakeTextScaled(parent, text, size, anchor, anchorMin, anchorMax, pos, sizeDelta, color);
        }

        Text MakeTextScaled(Transform parent, string text, int size, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = F;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        GameObject BuildCenterPanel(Transform parent, string title, string subtitle, Color tint)
        {
            var go = new GameObject(title + "Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760, 480);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.02f, 0.03f, 0.05f, 0.96f);

            MakeTextScaled(go.transform, title, 58, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(0, 80), Color.white);
            MakeTextScaled(go.transform, subtitle, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(700, 40), tint);
            MakeTextScaled(go.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(700, 120), new Color(0.9f, 0.92f, 1f)).name = "PanelDetail";

            return go;
        }

        Button MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Button), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280, 56);
            go.GetComponent<Image>().color = new Color(0.18f, 0.3f, 0.5f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(onClick);

            var child = new GameObject("Label", typeof(RectTransform), typeof(Text));
            child.transform.SetParent(go.transform, false);
            var crt = child.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var t = child.GetComponent<Text>();
            t.font = F;
            t.text = label;
            t.fontSize = 26;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return btn;
        }

        void Update()
        {
            if (subtitleTimer > 0f)
            {
                subtitleTimer -= Time.deltaTime;
                if (subtitleTimer <= 0f && subtitleText != null) subtitleText.gameObject.SetActive(false);
            }
            if (logTimer > 0f)
            {
                logTimer -= Time.deltaTime;
                if (logTimer <= 0f && logText != null) logText.gameObject.SetActive(false);
            }
            if (damageFade > 0f)
            {
                damageFade -= Time.deltaTime * 1.4f;
                if (damageImg != null) damageImg.color = new Color(0.6f, 0.05f, 0.03f, Mathf.Max(0f, damageFade));
                if (damageFade <= 0f && damageObj != null) damageObj.SetActive(false);
            }
            if (heartsText != null) heartsText.color = Color.Lerp(heartsText.color, new Color(1f, 0.35f, 0.35f), 3f * Time.deltaTime);

            if (EiraInput.ToggleMissionsDown() && missionsPanel != null)
            {
                missionsPanel.SetActive(!missionsPanel.activeSelf);
                ShowControls();
            }

            // El panel de controles se atenúa solo, pero vuelve con Tab.
            if (controlsTimer > 0f)
            {
                controlsTimer -= Time.unscaledDeltaTime;

                if (controlsTimer <= 0f && controlsObj != null)
                    controlsObj.SetActive(false);
            }

            // La mira se abre un instante con cada disparo y vuelve a
            // cerrarse. Da feedback inmediato de que el arma ha respondido,
            // que es lo que el jugador nota al mantener el botón.
            UpdateReticle();

            UpdateObjectiveArrow();

            if (EiraInput.SkipDown() && GameManager.Instance != null)
                GameManager.Instance.TogglePause();
            // R solo reinicia desde la pausa: durante el juego el jugador
            // puede estar usando la misma tecla para otra cosa.
            if (EiraInput.RestartDown() && GameManager.Instance != null &&
                GameManager.Instance.State == GameState.Paused)
                GameManager.Instance.RestartLevel();
        }

        private void ShowControls()
        {
            if (controlsObj == null)
                return;

            controlsObj.SetActive(true);
            controlsTimer = 18f;
        }

        /// <summary>
        /// Retroceso visual de la mira. No usa corrutinas ni Invoke: si el
        /// HUD se pausa a mitad del efecto, el estado se queda coherente.
        /// </summary>
        private         void UpdateReticle()
        {
            if (reticleObj == null)
                return;

            var gm = GameManager.Instance;
            bool playing =
                gm == null ||
                gm.State == GameState.Playing;

            // En pausa la mira no aparece: estorba y además el juego está
            // congelado, así que no hay nada a lo que apuntar.
            if (!playing)
            {
                reticleObj.SetActive(false);
                reticleKick = 0f;
                return;
            }

            if (!reticleObj.activeSelf)
                reticleObj.SetActive(true);

            var player = gm != null ? gm.Player : null;
            var weapon = player != null ? player.Weapon : null;

            if (weapon != null && weapon.FiredThisFrame)
                reticleKick = 1f;

            reticleKick = Mathf.Max(
                0f,
                reticleKick - Time.deltaTime * 6f
            );

            float scale = 1f + reticleKick * 0.9f;

            var rt = reticleObj.GetComponent<RectTransform>();

            if (rt != null)
            {
                rt.sizeDelta = new Vector2(
                    reticleBaseSize * scale * 1.3f,
                    reticleBaseSize * scale * 1.3f);
            }

            foreach (var img in reticleObj.GetComponentsInChildren<Image>())
            {
                var c = img.color;
                c.a = Mathf.Lerp(0.85f, 0.25f, reticleKick);
                img.color = c;
            }
        }

        public void SetPaused(bool paused)
        {
            if (pausePanel == null)
                return;

            pausePanel.SetActive(paused);
        }

        public void ShowSubtitle(string s, float dur)
        {
            if (subtitleText == null) return;
            subtitleText.text = s;
            subtitleText.gameObject.SetActive(true);
            subtitleTimer = dur;
        }

        public void FlashDamage()
        {
            if (damageImg == null) return;
            damageFade = 0.7f;
            damageObj.SetActive(true);
            damageImg.color = new Color(0.6f, 0.05f, 0.03f, 0.7f);
        }

        void DamageTint(Color c, float strength)
        {
            if (damageImg == null) return;
            damageFade = Mathf.Max(damageFade, strength);
            damageObj.SetActive(true);
            damageImg.color = new Color(c.r, c.g, c.b, strength);
        }

        public void RefreshLives()
        {
            if (heartsText == null || GameManager.Instance == null) return;
            int lives = GameManager.Instance.Lives;
            string s = "";
            for (int i = 0; i < EiraConst.MaxLives; i++)
                s += i < lives ? "♥" : "·";
            heartsText.text = "VIDAS  " + s;
        }

        public void RefreshBars()
        {
            if (healthFill == null || heartFill == null || GameManager.Instance == null) return;
            healthFill.fillAmount = Mathf.Clamp01(GameManager.Instance.PlayerHealth / EiraConst.MaxHealth);
            heartFill.fillAmount = Mathf.Clamp01(GameManager.Instance.PlayerHeart);
        }

        public void RefreshMissions()
        {
            if (missionLines == null || GameManager.Instance == null) return;
            missionsPanel.SetActive(true);
            string s = "";
            for (int i = 0; i < Missions.Names.Length; i++)
            {
                bool done = GameManager.Instance.IsMissionDone(i);
                s += (done ? "✔ " : "○ ") + Missions.Names[i] + "\n";
            }
            missionLines.text = s;
            missionsPanel.SetActive(false);
        }

        public void ShowWin(int score, int lives)
        {
            winPanel.SetActive(true);
            var detail = winPanel.transform.Find("PanelDetail")?.GetComponent<Text>();
            string levelName = GetLevelTitle(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            if (detail != null)
                detail.text = "Puntos totales: " + score + "\nVidas restantes: " + lives + "\n\nEira y NOVA cruzan la salida.\n" + levelName + " completado.";
            overPanel.SetActive(false);
            missionList.Add(("Victoria", true));
        }

        public void ShowGameOver(int score)
        {
            overPanel.SetActive(true);
            var detail = overPanel.transform.Find("PanelDetail")?.GetComponent<Text>();
            if (detail != null)
                detail.text = "Puntos: " + score + "\n\nPerdiste todas tus vidas.\nLa llave del año 3000 tendrá que esperar.";
            winPanel.SetActive(false);
        }
    }
}