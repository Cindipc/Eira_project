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
        Text missionHeader, missionLines;
        GameObject missionsPanel, bossObj, damageObj, winPanel, overPanel;
        Image heartFill, healthFill, bossFill, damageImg;
        float subtitleTimer, logTimer, damageFade;
        readonly List<(string, bool)> missionList = new List<(string, bool)>();

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
        }

        void BuildCanvas()
        {
            if (FindObjectOfType<EventSystem>() == null)
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

            // --- pantallas finales ---
            winPanel = BuildCenterPanel(root, "VICTORIA", new Color(0.15f, 0.35f, 0.2f));
            overPanel = BuildCenterPanel(root, "DERROTA", new Color(0.4f, 0.1f, 0.1f));
            winPanel.SetActive(false);
            overPanel.SetActive(false);
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

        GameObject BuildCenterPanel(Transform parent, string title, Color tint)
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
            MakeTextScaled(go.transform, "Nivel 1 · El Despertar", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(700, 40), tint);
            MakeTextScaled(go.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(700, 120), new Color(0.9f, 0.92f, 1f)).name = "PanelDetail";

            var restart = MakeButton(go.transform, "Jugar de nuevo", new Vector2(0, -320), () => SceneManager.LoadScene(EiraConst.Level1Scene));
            MakeButton(go.transform, "Menú principal", new Vector2(0, -390), () => SceneManager.LoadScene(EiraConst.IntroScene));
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
                missionsPanel.SetActive(!missionsPanel.activeSelf);
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
            if (detail != null)
                detail.text = "Puntos totales: " + score + "\nVidas restantes: " + lives + "\n\nEira y NOVA cruzan la salida hacia la ciudad de las ruinas.\nCapítulo 1 completado.";
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