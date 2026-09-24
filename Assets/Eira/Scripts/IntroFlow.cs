using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace EiraGame
{
    // Introducción cinematográfica: narra el Capítulo 1 "El despertar" como texto en pantalla,
    // sobre una pequeña puesta en escena, y al final lleva al Nivel 1.
    public class IntroFlow : MonoBehaviour
    {
        class Step
        {
            public string title;
            public string body;
            public Step(string t, string b) { title = t; body = b; }
        }

        static readonly Step[] Steps =
        {
            new Step("EIRA", "LA LLAVE DEL AÑO 3000\n\nCapítulo 1 — El despertar"),
            new Step("Prólogo · Año 2026", "Eira vivía con una enfermedad del corazón.\nSintiéndose una carga para su familia, tomó\nuna decisión que terminó con su vida.\n\n...o eso creía."),
            new Step("Capítulo 1 — El despertar", "Eira abrió los ojos.\nSu corazón latía.\nNo estaba en su habitación.\nEstaba en una plataforma metálica, dentro de un enorme edificio abandonado, lleno de cables y máquinas destruidas."),
            new Step("", "Una pantalla se encendió:\n\nAÑO 3000\n\nEira quedó paralizada."),
            new Step("", "\"¿Dónde estoy?\"\n\nLa puerta del laboratorio se abrió.\nUn androide entró, con el cuerpo parcialmente\ndestruido y un ojo parpadeando.\n\n— No te acerques."),
            new Step("", "El androide levantó lentamente las manos.\n\n— No voy a hacerte daño.\n\n— ¿Puedes hablar?\n\n— Sí. Pero tú eres la anomalía."),
            new Step("", "— Los humanos desaparecieron hace décadas.\n\nEira sintió un escalofrío.\nEl androide miró la pantalla y dijo:\n\n— Bienvenida al año 3000."),
            new Step("Nivel 1 · El Despertar", "Con NOVA como guía, Eira debe:\nexplorar el laboratorio, encontrar información\nsobre el año 3000, activar las puertas y\ndesactivar a la Máquina Guardiana para escapar."),
        };

        Text titleText, bodyText, hintText;
        Button startButton;
        int index;
        float typeTimer;
        string fullBody;
        float orbit;
        Transform heroNova, heroEira;

        void Start()
        {
            BuildUi();
            BuildStage();
            GameEvents.ResetAll();
            AudioFX.Init();
            AudioFX.PlayTone(220f, 0.8f, 0.3f, true, 110f);
            ShowStep(0);
        }

        void Update()
        {
            orbit += Time.deltaTime * 0.05f;
            var cam = Camera.main;
            if (cam != null)
            {
                float c = Mathf.Cos(orbit), s = Mathf.Sin(orbit);
                cam.transform.position = new Vector3(0f, 1.4f, -6f) + new Vector3(s * 0.6f, 0f, -c * 0.4f + 0.4f);
                cam.transform.LookAt(new Vector3(0f, 1.1f, 0.5f));
            }

            if (heroNova != null) heroNova.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.6f) * 3f, 0f);
            if (heroEira != null) heroEira.localRotation = Quaternion.Euler(0f, -Mathf.Sin(Time.time * 0.5f) * 3f, 0f);

            // máquina de escribir
            if (typeTimer < float.MaxValue && bodyText != null)
            {
                typeTimer += Time.deltaTime;
                int show = Mathf.FloorToInt(typeTimer * 60f);
                bodyText.text = fullBody.Substring(0, Mathf.Min(show, fullBody.Length));
                if (show >= fullBody.Length) typeTimer = float.MaxValue;
            }

            if (startButton.gameObject.activeSelf) return;
            if (EiraInput.AdvanceDown()) Next();
            if (EiraInput.SkipDown()) JumpToFinal();
        }

        void BuildUi()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                try { es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions(); } catch { }
            }

            var canvasGo = new GameObject("IntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var root = canvasGo.transform;

            var font = TryFont();

            var center = new GameObject("Center", typeof(RectTransform));
            center.transform.SetParent(root, false);
            var crt = center.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            titleText = MakeText(center.transform, "", 66, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.78f), new Vector2(1500, 110), new Color(0.85f, 0.92f, 1f), font);
            bodyText = MakeText(center.transform, "", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(1500, 340), new Color(0.93f, 0.95f, 1f), font);
            hintText = MakeText(center.transform, "Espacio / E  · siguiente    ·    Esc  · omitir", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.12f), new Vector2(1500, 40), new Color(0.5f, 0.6f, 0.75f), font);

            startButton = MakeIntroButton(center.transform, "C O M E N Z A R   N I V E L   1", new Vector2(0.5f, 0.2f), () => SceneManager.LoadScene(EiraConst.Level1Scene), font);
            startButton.gameObject.SetActive(false);
        }

        void BuildStage()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.06f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.035f;
            Camera.main.backgroundColor = new Color(0.01f, 0.015f, 0.03f);
            Camera.main.clearFlags = CameraClearFlags.SolidColor;

            var dirLight = new GameObject("StageLight", typeof(Light));
            var dl = dirLight.GetComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(0.3f, 0.4f, 0.6f, 1f);
            dl.intensity = 0.5f;
            dl.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "StageFloor";
            Object.Destroy(ground.GetComponent<Collider>());
            ground.transform.localScale = new Vector3(6f, 1f, 4f);
            ground.transform.position = new Vector3(0f, -0.02f, 0.5f);
            SetLitColor(ground.GetComponent<Renderer>(), new Color(0.03f, 0.045f, 0.07f));

            heroNova = BuildAndroid(new Vector3(-1.4f, 0f, 0.6f));
            heroEira = BuildHeroine(new Vector3(1.5f, 0f, 0.4f));

            var blue = new GameObject("BlueLight", typeof(Light));
            var bl = blue.GetComponent<Light>();
            bl.type = LightType.Point;
            bl.color = new Color(0.25f, 0.45f, 1f);
            bl.intensity = 2.2f;
            bl.range = 8f;
            blue.transform.position = new Vector3(0f, 1.6f, 0.6f);

            var rim = new GameObject("RimLight", typeof(Light));
            var rl = rim.GetComponent<Light>();
            rl.type = LightType.Point;
            rl.color = new Color(0.05f, 0.1f, 0.25f);
            rl.intensity = 1.2f;
            rl.range = 6f;
            rim.transform.position = new Vector3(0f, 0.8f, 3f);
        }

        Transform BuildAndroid(Vector3 pos)
        {
            var root = new GameObject("NOVA_Silhouette");
            root.transform.position = pos;
            var torso = Prim(root.transform, PrimitiveType.Capsule, new Vector3(0f, 1.15f, 0f), Vector3.one * 0.45f, new Vector3(0.5f, 0.42f, 0.28f));
            SetLitColor(torso.GetComponent<Renderer>(), new Color(0.88f, 0.9f, 0.94f));
            var arm = Prim(root.transform, PrimitiveType.Capsule, new Vector3(0.42f, 1.05f, 0f), Vector3.one, new Vector3(0.1f, 0.45f, 0.1f));
            SetLitColor(arm.GetComponent<Renderer>(), new Color(0.88f, 0.9f, 0.94f));
            var arm2 = Prim(root.transform, PrimitiveType.Capsule, new Vector3(-0.42f, 1.05f, 0f), Vector3.one, new Vector3(0.1f, 0.45f, 0.1f));
            SetLitColor(arm2.GetComponent<Renderer>(), new Color(0.88f, 0.9f, 0.94f));
            var head = Prim(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.6f, Vector3.one);
            SetLitColor(head.GetComponent<Renderer>(), new Color(0.92f, 0.94f, 0.97f));
            var eye = Prim(root.transform, PrimitiveType.Sphere, new Vector3(0.34f, 1.72f, 0.34f), Vector3.one * 0.5f, Vector3.one * 0.09f);
            SetLitColor(eye.GetComponent<Renderer>(), new Color(0.2f, 0.6f, 1f));
            return root.transform;
        }

        Transform BuildHeroine(Vector3 pos)
        {
            var root = new GameObject("Eira_Silhouette");
            root.transform.position = pos;
            var torso = Prim(root.transform, PrimitiveType.Capsule, new Vector3(0f, 1.1f, 0f), Vector3.one * 0.38f, new Vector3(0.42f, 0.36f, 0.24f));
            SetLitColor(torso.GetComponent<Renderer>(), new Color(0.06f, 0.09f, 0.12f));
            var leg = Prim(root.transform, PrimitiveType.Capsule, new Vector3(0.14f, 0.5f, 0f), Vector3.one, new Vector3(0.12f, 0.4f, 0.12f));
            SetLitColor(leg.GetComponent<Renderer>(), new Color(0.1f, 0.12f, 0.14f));
            var leg2 = Prim(root.transform, PrimitiveType.Capsule, new Vector3(-0.14f, 0.5f, 0f), Vector3.one, new Vector3(0.12f, 0.4f, 0.12f));
            SetLitColor(leg2.GetComponent<Renderer>(), new Color(0.1f, 0.12f, 0.14f));
            var head = Prim(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.66f, 0f), Vector3.one * 0.5f, Vector3.one);
            SetLitColor(head.GetComponent<Renderer>(), new Color(0.75f, 0.58f, 0.4f));
            var hair = Prim(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.76f, -0.06f), Vector3.one * 0.45f, new Vector3(1.1f, 0.9f, 1.1f));
            SetLitColor(hair.GetComponent<Renderer>(), new Color(0.16f, 0.1f, 0.08f));
            return root.transform;
        }

        static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "p";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = localScale;
            return go;
        }

        static void SetLitColor(Renderer r, Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            r.material = new Material(sh != null ? sh : Shader.Find("Standard")) { color = c };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static UnityEngine.Font TryFont()
        {
            try { return Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf"); } catch { }
            try { return Resources.GetBuiltinResource<UnityEngine.Font>("Arial.ttf"); } catch { }
            return null;
        }

        static Text MakeText(Transform parent, string text, int size, TextAnchor align, Vector2 anchor, Vector2 sizeDelta, Color color, UnityEngine.Font font)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Button MakeIntroButton(Transform parent, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick, UnityEngine.Font font)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Button), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = new Vector2(520, 70);
            go.GetComponent<Image>().color = new Color(0.1f, 0.35f, 0.6f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(onClick);

            var child = new GameObject("Label", typeof(RectTransform), typeof(Text));
            child.transform.SetParent(go.transform, false);
            var crt = child.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var t = child.GetComponent<Text>();
            t.font = font;
            t.text = label;
            t.fontSize = 28;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return btn;
        }

        void ShowStep(int i)
        {
            index = i;
            var step = Steps[i];
            titleText.text = step.title;
            fullBody = step.body;
            typeTimer = 0f;
            bodyText.text = "";
            if (i == Steps.Length - 1)
            {
                startButton.gameObject.SetActive(true);
                hintText.gameObject.SetActive(false);
                AudioFX.Win();
            }
            else
            {
                startButton.gameObject.SetActive(false);
            }
        }

        void Next()
        {
            if (typeTimer != float.MaxValue) { typeTimer = float.MaxValue; bodyText.text = fullBody; return; }
            if (index < Steps.Length - 1)
            {
                ShowStep(index + 1);
                AudioFX.Beep();
            }
        }

        void JumpToFinal()
        {
            ShowStep(Steps.Length - 1);
        }
    }
}