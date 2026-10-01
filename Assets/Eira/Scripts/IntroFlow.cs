
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace EiraGame
{
    /// <summary>
    /// Introducción cinematográfica de EIRA.
    /// Presenta el despertar de Eira en el año 3000
    /// y conduce al jugador al Nivel 1.
    /// </summary>
    public class IntroFlow : MonoBehaviour
    {
        class Step
        {
            public string title;
            public string body;

            public Step(string t, string b)
            {
                title = t;
                body = b;
            }
        }

        static readonly Step[] Steps =
        {
            new Step(
                "E I R A",
                "A veces, morir no significa que tu historia haya terminado."
            ),

            new Step(
                "AÑO 2026",
                "Eira había aprendido a vivir con un corazón que podía fallarle en cualquier momento.\n\n"
                + "Cada día era una batalla.\n"
                + "Cada latido, una advertencia."
            ),

            new Step(
                "EL ÚLTIMO LATIDO",
                "Aquella noche tomó una decisión.\n\n"
                + "Una decisión de la que no habría regreso.\n\n"
                + "Su corazón se detuvo."
            ),

            new Step(
                "",
                "Silencio.\n\n"
                + "Oscuridad.\n\n"
                + "Nada."
            ),

            new Step(
                "DESPERTAR",
                "Un latido.\n\n"
                + "Otro.\n\n"
                + "Eira abrió los ojos."
            ),

            new Step(
                "AÑO 3000",
                "El aire olía a metal quemado.\n\n"
                + "Luces de emergencia parpadeaban sobre ella.\n\n"
                + "No reconocía el lugar.\n"
                + "No reconocía las máquinas.\n"
                + "No reconocía el mundo."
            ),

            new Step(
                "LA ANOMALÍA",
                "Una voz desconocida atravesó el laboratorio."
            ),

            new Step(
                "NOVA",
                "— No te muevas."
            ),

            new Step(
                "EIRA",
                "— ¿Quién eres?\n\n"
                + "La figura apareció entre las sombras."
            ),

            new Step(
                "NOVA",
                "— Me llaman NOVA.\n\n"
                + "— ¿Dónde estoy?\n\n"
                + "Silencio."
            ),

            new Step(
                "LA VERDAD",
                "— Año 3000.\n\n"
                + "Eira no respondió."
            ),

            new Step(
                "974 AÑOS",
                "— Eso es imposible.\n\n"
                + "NOVA la observó durante unos segundos.\n\n"
                + "— Para ti, sí."
            ),

            new Step(
                "EL MUNDO QUE QUEDÓ",
                "Las ciudades cayeron.\n"
                + "Los gobiernos desaparecieron.\n"
                + "La humanidad se extinguió.\n\n"
                + "Pero las máquinas continuaron."
            ),

            new Step(
                "Y ENTONCES...",
                "NOVA mostró una grabación.\n\n"
                + "Un rostro.\n\n"
                + "El rostro de Eira."
            ),

            new Step(
                "EL MISTERIO",
                "— ¿Por qué hay registros tuyos en nuestros sistemas?\n\n"
                + "Eira sintió que el frío recorría su cuerpo."
            ),

            new Step(
                "LA LLAVE",
                "— Tú no despertaste por accidente.\n\n"
                + "— Alguien te trajo aquí."
            ),

            new Step(
                "EIRA",
                "— ¿Quién?"
            ),

            new Step(
                "NOVA",
                "— Eso es precisamente lo que tenemos que descubrir."
            ),

            new Step(
                "CAPÍTULO 1",
                "EL DESPERTAR\n\n"
                + "La respuesta está en las ruinas.\n\n"
                + "Y algo en ellas sabe que Eira ha vuelto."
            ),

            new Step(
                "NIVEL 1 · EL DESPERTAR",
                "EXPLORA EL LABORATORIO\n\n"
                + "• Descubre dónde estás.\n"
                + "• Recupera los registros perdidos.\n"
                + "• Activa el sistema de seguridad.\n"
                + "• Encuentra una salida.\n\n"
                + "Pero ten cuidado.\n\n"
                + "No todas las máquinas están dormidas."
            ),
        };

        Text titleText;
        Text bodyText;
        Text hintText;

        Button startButton;

        int index;
        float typeTimer;
        string fullBody;

        Transform heroNova;
        Transform heroEira;

        IntroStage stage;

        void Start()
        {
            BuildUi();

            GameEvents.ResetAll();
            AudioFX.Init();

            AudioFX.PlayTone(
                110f,
                1.5f,
                0.25f,
                true,
                55f
            );

            // El escenario va primero: la cámara y las luces las pone él, y
            // las siluetas se registran después para que se tiñan con el
            // acento del capítulo.
            stage = IntroStage.Attach(transform);

            BuildStage();

            stage.SnapAccent(
                IntroStage.AccentFor(0)
            );

            ShowStep(0);
        }

        void Update()
        {
            // La cámara, la órbita, el corazón y el velo los anima IntroStage.

            if (heroNova != null)
            {
                heroNova.localRotation =
                    Quaternion.Euler(
                        0f,
                        Mathf.Sin(Time.time * 0.6f) * 3f,
                        0f
                    );
            }

            if (heroEira != null)
            {
                heroEira.localRotation =
                    Quaternion.Euler(
                        0f,
                        -Mathf.Sin(Time.time * 0.5f) * 3f,
                        0f
                    );
            }

            // Efecto máquina de escribir
            if (
                typeTimer < float.MaxValue &&
                bodyText != null
            )
            {
                typeTimer += Time.deltaTime;

                int show =
                    Mathf.FloorToInt(
                        typeTimer * 45f
                    );

                bodyText.text =
                    fullBody.Substring(
                        0,
                        Mathf.Min(
                            show,
                            fullBody.Length
                        )
                    );

                if (show >= fullBody.Length)
                    typeTimer = float.MaxValue;
            }

            if (
                startButton != null &&
                startButton.gameObject.activeSelf
            )
            {
                return;
            }

            if (EiraInput.AdvanceDown())
                Next();

            if (EiraInput.SkipDown())
                JumpToFinal();
        }

        void BuildUi()
        {
            if (
                FindObjectOfType<EventSystem>() == null
            )
            {
                var es = new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule)
                );

                try
                {
                    es.GetComponent<InputSystemUIInputModule>()
                        .AssignDefaultActions();
                }
                catch
                {
                }
            }

            var canvasGo = new GameObject(
                "IntroCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

            canvasGo.transform.SetParent(
                transform,
                false
            );

            var canvas =
                canvasGo.GetComponent<Canvas>();

            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder = 20;

            var scaler =
                canvasGo.GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode
                    .ScaleWithScreenSize;

            scaler.referenceResolution =
                new Vector2(1920, 1080);

            var root = canvasGo.transform;

            var font = TryFont();

            var center = new GameObject(
                "Center",
                typeof(RectTransform)
            );

            center.transform.SetParent(
                root,
                false
            );

            var crt =
                center.GetComponent<RectTransform>();

            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            titleText = MakeText(
                center.transform,
                "",
                72,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.78f),
                new Vector2(1500, 120),
                new Color(0.85f, 0.92f, 1f),
                font
            );

            bodyText = MakeText(
                center.transform,
                "",
                34,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f),
                new Vector2(1500, 390),
                new Color(0.93f, 0.95f, 1f),
                font
            );

            hintText = MakeText(
                center.transform,
                "ESPACIO / E  ·  CONTINUAR       ESC  ·  OMITIR",
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.08f),
                new Vector2(1500, 40),
                new Color(0.5f, 0.6f, 0.75f),
                font
            );

            startButton = MakeIntroButton(
                center.transform,
                "C O M E N Z A R",
                new Vector2(0.5f, 0.2f),
                () =>
                    SceneManager.LoadScene(
                        EiraConst.Level1Scene
                    ),
                font
            );

            startButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Solo las siluetas. El suelo, la niebla, la cámara y las luces los
        /// pone IntroStage, que además los va tiñendo con el acento de cada
        /// capítulo.
        /// </summary>
        void BuildStage()
        {
            heroNova =
                BuildAndroid(
                    new Vector3(
                        -1.4f,
                        0f,
                        0.6f
                    )
                );

            heroEira =
                BuildHeroine(
                    new Vector3(
                        1.5f,
                        0f,
                        0.4f
                    )
                );

            // Las siluetas se registran en el escenario para que su material
            // reciba el color del capítulo. Si no, se quedan en un gris
            // apagado mientras todo lo demás se mueve de color.
            if (stage != null)
            {
                RegisterSilhouette(heroNova);
                RegisterSilhouette(heroEira);
            }
        }

        void RegisterSilhouette(Transform root)
        {
            if (root == null || stage == null)
                return;

            var renderers =
                root.GetComponentsInChildren<Renderer>(
                    true
                );

            for (int i = 0; i < renderers.Length; i++)
            {
                stage.RegisterSilhouette(renderers[i]);
            }
        }

        Transform BuildAndroid(Vector3 pos)
        {
            var root =
                new GameObject(
                    "NOVA_Silhouette"
                );

            root.transform.position =
                pos;

            var torso = Prim(
                root.transform,
                PrimitiveType.Capsule,
                new Vector3(
                    0f,
                    1.15f,
                    0f
                ),
                Vector3.one * 0.45f,
                new Vector3(
                    0.5f,
                    0.42f,
                    0.28f
                )
            );

            SetLitColor(
                torso.GetComponent<Renderer>(),
                new Color(
                    0.88f,
                    0.9f,
                    0.94f
                )
            );

            var arm = Prim(
                root.transform,
                PrimitiveType.Capsule,
                new Vector3(
                    0.42f,
                    1.05f,
                    0f
                ),
                Vector3.one,
                new Vector3(
                    0.1f,
                    0.45f,
                    0.1f
                )
            );

            SetLitColor(
                arm.GetComponent<Renderer>(),
                new Color(
                    0.88f,
                    0.9f,
                    0.94f
                )
            );

            var arm2 = Prim(
                root.transform,
                PrimitiveType.Capsule,
                new Vector3(
                    -0.42f,
                    1.05f,
                    0f
                ),
                Vector3.one,
                new Vector3(
                    0.1f,
                    0.45f,
                    0.1f
                )
            );

            SetLitColor(
                arm2.GetComponent<Renderer>(),
                new Color(
                    0.88f,
                    0.9f,
                    0.94f
                )
            );

            var head = Prim(
                root.transform,
                PrimitiveType.Sphere,
                new Vector3(
                    0f,
                    1.72f,
                    0f
                ),
                Vector3.one * 0.6f,
                Vector3.one
            );

            SetLitColor(
                head.GetComponent<Renderer>(),
                new Color(
                    0.92f,
                    0.94f,
                    0.97f
                )
            );

            return root.transform;
        }

        Transform BuildHeroine(Vector3 pos)
        {
            var root =
                new GameObject(
                    "Eira_Silhouette"
                );

            root.transform.position =
                pos;

            var torso = Prim(
                root.transform,
                PrimitiveType.Capsule,
                new Vector3(
                    0f,
                    1.1f,
                    0f
                ),
                Vector3.one * 0.38f,
                new Vector3(
                    0.42f,
                    0.36f,
                    0.24f
                )
            );

            SetLitColor(
                torso.GetComponent<Renderer>(),
                new Color(
                    0.06f,
                    0.09f,
                    0.12f
                )
            );

            var leg = Prim(
                root.transform,
                PrimitiveType.Capsule,
                new Vector3(
                    0.14f,
                    0.5f,
                    0f
                ),
                Vector3.one,
                new Vector3(
                    0.12f,
                    0.4f,
                    0.12f
                )
            );

            SetLitColor(
                leg.GetComponent<Renderer>(),
                new Color(
                    0.1f,
                    0.12f,
                    0.14f
                )
            );

            var leg2 = Prim(
                root.transform,
                PrimitiveType.Capsule,
                new Vector3(
                    -0.14f,
                    0.5f,
                    0f
                ),
                Vector3.one,
                new Vector3(
                    0.12f,
                    0.4f,
                    0.12f
                )
            );

            SetLitColor(
                leg2.GetComponent<Renderer>(),
                new Color(
                    0.1f,
                    0.12f,
                    0.14f
                )
            );

            var head = Prim(
                root.transform,
                PrimitiveType.Sphere,
                new Vector3(
                    0f,
                    1.66f,
                    0f
                ),
                Vector3.one * 0.5f,
                Vector3.one
            );

            SetLitColor(
                head.GetComponent<Renderer>(),
                new Color(
                    0.75f,
                    0.58f,
                    0.4f
                )
            );

            var hair = Prim(
                root.transform,
                PrimitiveType.Sphere,
                new Vector3(
                    0f,
                    1.76f,
                    -0.06f
                ),
                Vector3.one * 0.45f,
                new Vector3(
                    1.1f,
                    0.9f,
                    1.1f
                )
            );

            SetLitColor(
                hair.GetComponent<Renderer>(),
                new Color(
                    0.02f,
                    0.02f,
                    0.025f
                )
            );

            return root.transform;
        }

        static GameObject Prim(
            Transform parent,
            PrimitiveType type,
            Vector3 pos,
            Vector3 scale,
            Vector3 localScale
        )
        {
            var go =
                GameObject.CreatePrimitive(
                    type
                );

            go.name = "p";

            Destroy(
                go.GetComponent<Collider>()
            );

            go.transform.SetParent(
                parent,
                false
            );

            go.transform.localPosition =
                pos;

            go.transform.localScale =
                localScale;

            return go;
        }

        static void SetLitColor(
            Renderer r,
            Color c
        )
        {
            var sh =
                Shader.Find(
                    "Universal Render Pipeline/Lit"
                );

            r.material =
                new Material(
                    sh != null
                        ? sh
                        : Shader.Find("Standard")
                )
                {
                    color = c
                };

            r.shadowCastingMode =
                UnityEngine.Rendering
                    .ShadowCastingMode.Off;
        }

        static Font TryFont()
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf"
                );
            }
            catch
            {
            }

            try
            {
                return Resources.GetBuiltinResource<Font>(
                    "Arial.ttf"
                );
            }
            catch
            {
            }

            return null;
        }

        static Text MakeText(
            Transform parent,
            string text,
            int size,
            TextAnchor align,
            Vector2 anchor,
            Vector2 sizeDelta,
            Color color,
            Font font
        )
        {
            var go =
                new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(Text)
                );

            go.transform.SetParent(
                parent,
                false
            );

            var rt =
                go.GetComponent<RectTransform>();

            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = sizeDelta;

            var t =
                go.GetComponent<Text>();

            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;

            t.horizontalOverflow =
                HorizontalWrapMode.Wrap;

            t.verticalOverflow =
                VerticalWrapMode.Overflow;

            return t;
        }

        Button MakeIntroButton(
            Transform parent,
            string label,
            Vector2 anchor,
            UnityEngine.Events.UnityAction onClick,
            Font font
        )
        {
            var go =
                new GameObject(
                    "Button",
                    typeof(RectTransform),
                    typeof(Button),
                    typeof(Image)
                );

            go.transform.SetParent(
                parent,
                false
            );

            var rt =
                go.GetComponent<RectTransform>();

            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;

            rt.sizeDelta =
                new Vector2(
                    520,
                    70
                );

            var image =
                go.GetComponent<Image>();

            image.color =
                new Color(
                    0.08f,
                    0.32f,
                    0.62f,
                    1f
                );

            var btn =
                go.GetComponent<Button>();

            btn.targetGraphic = image;
            btn.onClick.AddListener(onClick);

            var child =
                new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(Text)
                );

            child.transform.SetParent(
                go.transform,
                false
            );

            var crt =
                child.GetComponent<RectTransform>();

            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            var t =
                child.GetComponent<Text>();

            t.font = font;
            t.text = label;
            t.fontSize = 28;
            t.alignment =
                TextAnchor.MiddleCenter;
            t.color = Color.white;

            return btn;
        }

        void ShowStep(int i)
        {
            index = i;

            var step = Steps[i];

            titleText.text =
                step.title;

            fullBody =
                step.body;

            typeTimer = 0f;

            bodyText.text = "";

            bool finalStep =
                i == Steps.Length - 1;

            startButton.gameObject
                .SetActive(finalStep);

            hintText.gameObject
                .SetActive(!finalStep);

            ApplyChapterLook(i);

            if (finalStep)
            {
                AudioFX.Win();
            }
        }

        /// <summary>
        /// Traduce el paso en color. Cada capítulo pide su acento al
        /// escenario y el título se tiñe con el mismo color, así que el texto
        /// y el mundo van juntos.
        ///
        /// El latido va aparte porque cuenta otra cosa: si el corazón deja de
        /// latir, la historia deja de tener a nadie, y eso se nota más sin
        /// texto que con texto.
        /// </summary>
        void ApplyChapterLook(int i)
        {
            var accent =
                IntroStage.AccentFor(i);

            if (stage != null)
            {
                stage.SetAccent(accent);
                stage.SetHeartbeat(HeartbeatFor(i));
            }

            // El título se ilumina un poco para que se separe del fondo sin
            // dejar de ser blanco.
            titleText.color =
                Color.Lerp(
                    Color.white,
                    accent,
                    0.45f
                );

            bodyText.color =
                new Color(
                    0.93f,
                    0.95f,
                    1f
                );
        }

        /// <summary>
        /// 0 = sin pulso (los capítulos de la muerte), 1 = a tope.
        /// El latido real de Eira no empieza hasta que despierta.
        /// </summary>
        static float HeartbeatFor(int i)
        {
            if (i <= 2)
                return 0.15f;

            if (i == 3)
                return 0f;

            if (i <= 5)
                return 0.35f;

            if (i <= 7)
                return 0.7f;

            if (i <= 10)
                return 0.55f;

            if (i <= 13)
                return 0.85f;

            if (i <= 17)
                return 1f;

            return 0.8f;
        }

        void Next()
        {
            if (
                typeTimer != float.MaxValue
            )
            {
                typeTimer =
                    float.MaxValue;

                bodyText.text =
                    fullBody;

                return;
            }

            if (
                index < Steps.Length - 1
            )
            {
                ShowStep(index + 1);
                AudioFX.Beep();
            }
        }

        void JumpToFinal()
        {
            ShowStep(
                Steps.Length - 1
            );
        }
    }
}