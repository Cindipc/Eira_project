using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace EiraGame
{
    /// <summary>
    /// Escenario 3D y velo cinematográfico de la introducción.
    ///
    /// Va aparte de IntroFlow porque el archivo de la intro ya es largo y
    /// porque el escenario tiene su propio ciclo de vida: se construye una vez
    /// y luego solo se anima y se vuelve a tintar según el capítulo.
    ///
    /// La idea es que la intro tenga guion de color. Cada capítulo pide un
    /// acento y todo el escenario (retícula del suelo, neones, aros del
    /// corazón, motas de polvo) se desplaza hacia ese color, de forma que la
    /// historia se lee también sin leer el texto: azul frío al empezar, rojo
    /// en la muerte, cian al despertar, verde en las ruinas.
    /// </summary>
    public class IntroStage : MonoBehaviour
    {
        // =========================================================
        // PALETA
        // =========================================================

        /// <summary>
        /// Acento por capítulo. El orden corresponde a los pasos de
        /// IntroFlow.Steps; si se añaden pasos hay que añadir aquí el color.
        /// </summary>
        public static readonly Color[] ChapterAccents =
        {
            new Color(0.45f, 0.75f, 1.00f),   // E I R A
            new Color(0.50f, 0.78f, 0.95f),   // AÑO 2026
            new Color(0.60f, 0.80f, 1.00f),   // EL ÚLTIMO LATIDO
            new Color(0.30f, 0.34f, 0.55f),   // silencio
            new Color(0.35f, 0.95f, 0.85f),   // DESPERTAR
            new Color(0.45f, 0.85f, 0.80f),   // AÑO 3000
            new Color(0.55f, 0.90f, 0.60f),   // LA ANOMALÍA
            new Color(0.70f, 0.85f, 0.70f),   // NOVA
            new Color(0.70f, 0.85f, 0.70f),   // EIRA
            new Color(0.70f, 0.85f, 0.70f),   // NOVA
            new Color(0.75f, 0.80f, 0.80f),   // LA VERDAD
            new Color(0.90f, 0.70f, 0.45f),   // 974 AÑOS
            new Color(0.95f, 0.55f, 0.35f),   // EL MUNDO QUE QUEDÓ
            new Color(0.95f, 0.50f, 0.30f),   // Y ENTONCES...
            new Color(1.00f, 0.42f, 0.35f),   // EL MISTERIO
            new Color(1.00f, 0.55f, 0.30f),   // LA LLAVE
            new Color(0.95f, 0.60f, 0.45f),   // EIRA
            new Color(0.90f, 0.70f, 0.50f),   // NOVA
            new Color(0.40f, 0.95f, 0.85f),   // CAPÍTULO 1
            new Color(0.40f, 1.00f, 0.70f),   // NIVEL 1
        };

        public static Color AccentFor(int step)
        {
            if (ChapterAccents.Length == 0)
                return Color.white;

            return ChapterAccents[
                Mathf.Clamp(step, 0, ChapterAccents.Length - 1)
            ];
        }

        static readonly Color DeepBase = new Color(0.008f, 0.012f, 0.024f);
        static readonly Color FloorBase = new Color(0.016f, 0.024f, 0.044f);
        static readonly Color PanelBase = new Color(0.022f, 0.030f, 0.052f);

        // =========================================================
        // ESTADO
        // =========================================================

        /// <summary>
        /// Material que emite en el color del capítulo. La emisión se cambia
        /// por _EmissionColor, que sí existe en URP/Lit, así que el tintado
        /// funciona de verdad y no es un color que no lee nadie.
        /// </summary>
        class Glow
        {
            public Material material;
            public Color albedo;        // color base, antes de emitir
            public float intensity;     // cuanta luz da
            public bool tintAlbedo;     // si además hay que teñir la superficie
        }

        readonly List<Glow> glows = new List<Glow>();
        readonly List<Renderer> pickups = new List<Renderer>();

        Color accent = ChapterAccents[0];
        Color accentTarget = ChapterAccents[0];

        Transform heartRing;
        Transform[] heartPieces = new Transform[0];
        readonly List<Material> heartMaterials = new List<Material>();

        ParticleSystem sparks;

        Image vignette;
        RawImage scanlines;

        float time;
        float heartbeat;
        float heartTimer;
        float orbit;

        Camera cam;
        readonly Vector3 camLookAt = new Vector3(0f, 1.15f, 0.5f);

        // =========================================================
        // MONTAJE
        // =========================================================

        public static IntroStage Attach(Transform parent)
        {
            var go = new GameObject("IntroStage");
            go.transform.SetParent(parent, false);
            return go.AddComponent<IntroStage>();
        }

        void Awake()
        {
            Build();
        }

        void Build()
        {
            BuildEnvironment();
            BuildBackdrop();
            BuildNeons();
            BuildHeart();
            BuildParticles();
            BuildOverlay();
        }

        // ---------------------------------------------------------
        // SUELO
        // ---------------------------------------------------------

        void BuildEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = DeepBase;
            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.07f, 0.12f);

            cam = Camera.main;

            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = DeepBase;
                cam.fieldOfView = 42f;
            }

            // Retícula en el suelo. Antes era un plano liso y la escena no
            // tenía escala ni profundidad; esto da las dos cosas.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "StageFloor";
            floor.transform.position = new Vector3(0f, 0f, 0.5f);
            floor.transform.localScale = new Vector3(9f, 1f, 6f);
            Destroy(floor.GetComponent<Collider>());

            var grid = new Material(LitShader())
            {
                mainTexture = GridTexture(),
                color = FloorBase
            };

            grid.mainTextureScale = new Vector2(4f, 3f);
            EnableEmission(grid, Color.white);

            floor.GetComponent<Renderer>().material = grid;

            // El suelo es un emisor más: la retícula se enciende con el
            // acento del capítulo.
            AddGlow(grid, FloorBase, 1.1f, false);

            // Plataforma: separa a los personajes del suelo y les da sitio.
            var dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dais.name = "Dais";
            dais.transform.position = new Vector3(0f, 0.015f, 0.5f);
            dais.transform.localScale = new Vector3(2.6f, 0.015f, 1.5f);
            Destroy(dais.GetComponent<Collider>());
            dais.GetComponent<Renderer>().material = LitMaterial(FloorBase * 1.6f);

            var edge = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            edge.name = "DaisEdge";
            edge.transform.position = new Vector3(0f, 0.032f, 0.5f);
            edge.transform.localScale = new Vector3(2.62f, 0.02f, 1.52f);
            Destroy(edge.GetComponent<Collider>());

            var edgeMat = NeonMaterial(FloorBase, 1.6f, true);
            edge.GetComponent<Renderer>().material = edgeMat;
            AddGlow(edgeMat, FloorBase, 1.6f, true);
        }

        // ---------------------------------------------------------
        // FONDO
        // ---------------------------------------------------------

        void BuildBackdrop()
        {
            var root = new GameObject("Backdrop").transform;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "BackWall";
            wall.transform.SetParent(root, false);
            wall.transform.position = new Vector3(0f, 3.2f, 6.6f);
            wall.transform.localScale = new Vector3(18f, 7f, 0.3f);
            Destroy(wall.GetComponent<Collider>());
            wall.GetComponent<Renderer>().material = LitMaterial(PanelBase);

            // Estantes: dan profundidad y evitan que el fondo sea un color
            // plano detrás de los personajes.
            for (int i = 0; i < 3; i++)
            {
                var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.name = "Shelf";
                shelf.transform.SetParent(root, false);
                shelf.transform.position = new Vector3(0f, 0.9f + i * 1.5f, 6.1f);
                shelf.transform.localScale = new Vector3(17f, 0.12f, 0.7f);
                Destroy(shelf.GetComponent<Collider>());
                shelf.GetComponent<Renderer>().material =
                    LitMaterial(PanelBase * 2.2f);
            }

            // Pilares laterales con luz de borde: enmarcan la escena y
            // separan las siluetas del fondo.
            for (int i = 0; i < 4; i++)
            {
                float side = i < 2 ? -1f : 1f;
                float z = 1.2f + (i % 2) * 3.6f;

                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Pillar";
                pillar.transform.SetParent(root, false);
                pillar.transform.position = new Vector3(side * 4.7f, 2.2f, z);
                pillar.transform.localScale = new Vector3(0.7f, 4.4f, 0.7f);
                Destroy(pillar.GetComponent<Collider>());
                pillar.GetComponent<Renderer>().material =
                    LitMaterial(PanelBase * 1.5f);

                var stripMat = NeonMaterial(PanelBase, 2.2f, false);

                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = "PillarStrip";
                strip.transform.SetParent(pillar.transform, false);
                strip.transform.localPosition = new Vector3(-side * 0.37f, 0f, 0f);
                strip.transform.localScale = new Vector3(0.03f, 0.88f, 0.12f);
                Destroy(strip.GetComponent<Collider>());
                strip.GetComponent<Renderer>().material = stripMat;

                AddGlow(stripMat, PanelBase, 2.2f, false);
            }

            // Tuberías arriba: lectura de laboratorio.
            for (int i = 0; i < 3; i++)
            {
                var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pipe.name = "Pipe";
                pipe.transform.SetParent(root, false);
                pipe.transform.position =
                    new Vector3(0f, 4.0f + i * 0.55f, 5.4f - i * 0.9f);
                pipe.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                pipe.transform.localScale = new Vector3(0.09f, 9f, 0.09f);
                Destroy(pipe.GetComponent<Collider>());
                pipe.GetComponent<Renderer>().material =
                    LitMaterial(new Color(0.03f, 0.04f, 0.06f));
            }
        }

        void BuildNeons()
        {
            var key = NewLight("KeyLight", LightType.Directional);
            key.color = new Color(0.30f, 0.40f, 0.60f);
            key.intensity = 0.35f;
            key.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

            // Luz de relleno teñida: es la que hace que la escena cambie de
            // color con el capítulo y no solo las neones.
            var fill = NewLight("AccentLight", LightType.Point);
            fill.color = accent;
            fill.intensity = 2.4f;
            fill.range = 9f;
            fill.transform.position = new Vector3(0f, 1.7f, 0.8f);

            var rim = NewLight("RimLight", LightType.Point);
            rim.color = new Color(0.06f, 0.10f, 0.28f);
            rim.intensity = 1.5f;
            rim.range = 7f;
            rim.transform.position = new Vector3(0f, 1.0f, 3.2f);

            // Barra de neón delante de los personajes: recorta la silueta.
            var barMat = NeonMaterial(FloorBase, 3f, true);

            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "NeonBar";
            bar.transform.position = new Vector3(0f, 0.05f, -1.9f);
            bar.transform.localScale = new Vector3(9f, 0.02f, 0.12f);
            Destroy(bar.GetComponent<Collider>());
            bar.GetComponent<Renderer>().material = barMat;

            AddGlow(barMat, FloorBase, 3f, true);
        }

        static Light NewLight(string name, LightType type)
        {
            var go = new GameObject(name, typeof(Light));
            var l = go.GetComponent<Light>();

            l.type = type;
            l.shadows = LightShadows.None;

            return l;
        }

        // ---------------------------------------------------------
        // CORAZÓN
        // ---------------------------------------------------------

        void BuildHeart()
        {
            heartRing = new GameObject("HeartRing").transform;
            heartRing.position = new Vector3(1.5f, 1.35f, 1.7f);

            // Tres aros concéntricos detrás de Eira. No es un modelo de
            // corazón: es la constante vital, que late y puede apagarse.
            heartPieces = new Transform[3];

            for (int i = 0; i < 3; i++)
            {
                var m = NeonMaterial(DeepBase, 2.4f - i * 0.6f, false);

                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "HeartRing" + i;
                ring.transform.SetParent(heartRing, false);

                float scale = 0.7f + i * 0.5f;

                ring.transform.localScale =
                    new Vector3(scale, 0.012f, scale * 0.62f);

                Destroy(ring.GetComponent<Collider>());

                var r = ring.GetComponent<Renderer>();
                r.material = m;
                r.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;

                heartPieces[i] = ring.transform;
                heartMaterials.Add(m);
            }
        }

        // ---------------------------------------------------------
        // PARTÍCULAS
        // ---------------------------------------------------------

        void BuildParticles()
        {
            // Motas de polvo: hacen visible el cono de la luz. Sin ellas la
            // escena se siente plana y vacía.
            var dustGo = new GameObject("Dust");
            dustGo.transform.position = new Vector3(0f, 0f, 0.5f);
            var dust = dustGo.AddComponent<ParticleSystem>();

            var main = dust.main;
            main.startLifetime = 9f;
            main.startSpeed = 0.12f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.7f, 0.85f, 1f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;
            main.playOnAwake = true;
            main.gravityModifier = 0.02f;

            var emission = dust.emission;
            emission.rateOverTime = 18f;

            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(11f, 5f, 8f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            var dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
            dustRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            dustRenderer.material = UnlitMaterial(
                new Color(0.8f, 0.9f, 1f, 0.3f)
            );
            dustRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            // Chispas que suben desde la plataforma.
            var sparkGo = new GameObject("Sparks");
            sparkGo.transform.position = new Vector3(0f, 0.1f, 0.5f);
            sparks = sparkGo.AddComponent<ParticleSystem>();

            var smain = sparks.main;
            smain.startLifetime = 2.4f;
            smain.startSpeed = 0.5f;
            smain.startSize = 0.05f;
            smain.simulationSpace = ParticleSystemSimulationSpace.World;
            smain.maxParticles = 120;
            smain.playOnAwake = true;
            smain.startColor = accent;

            var shapeOsc = sparks.shape;
            shapeOsc.shapeType = ParticleSystemShapeType.Circle;
            shapeOsc.radius = 1.6f;
            shapeOsc.rotation = new Vector3(-90f, 0f, 0f);

            var semission = sparks.emission;
            semission.rateOverTime = 7f;

            var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            sparkRenderer.material = UnlitMaterial(accent);
            sparkRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------------------------------------------------------
        // VELO CINEMATOGRÁFICO
        // ---------------------------------------------------------

        void BuildOverlay()
        {
            // Capa de UI propia. sortingOrder 18: por encima de la escena 3D
            // y por debajo del texto de IntroFlow, que va en 20.
            var go = new GameObject(
                "IntroOverlay",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)
            );

            go.transform.SetParent(transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 18;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var root = go.GetComponent<RectTransform>();

            // Bandas superior e inferior: letterbox. Dan el aire de "estamos
            // viendo una escena" y encuadran el texto.
            Stretch(
                NewImage("LetterTop", root, Color.black)
                    .GetComponent<RectTransform>(),
                new Vector2(0f, 0.945f), new Vector2(1f, 1f)
            );

            Stretch(
                NewImage("LetterBottom", root, Color.black)
                    .GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 0.045f)
            );

            // Viñeta: oscurece las esquinas y empuja la mirada al centro.
            vignette = NewImage("Vignette", root, new Color(0, 0, 0, 0.6f));
            Stretch(vignette.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            vignette.sprite = VignetteSprite(256);
            vignette.raycastTarget = false;

            // Barrido de líneas muy tenue: da textura de monitor sin ensuciar.
            // RawImage y no Image porque lo que se desplaza es el patrón de la
            // textura (uvRect), y Image no expone esa propiedad.
            var raw = new GameObject(
                "Scanlines", typeof(RectTransform), typeof(RawImage)
            );

            raw.transform.SetParent(root, false);

            scanlines = raw.GetComponent<RawImage>();
            scanlines.texture = ScanlineTexture(120);
            scanlines.color = new Color(1, 1, 1, 0.05f);
            scanlines.raycastTarget = false;

            Stretch(raw.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<Image>();
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // =========================================================
        // API
        // =========================================================

        /// <summary>
        /// Pide el color del capítulo. No se aplica de golpe: todo se va
        /// desplazando hacia el acento en un par de segundos, que es lo que
        /// hace que el cambio se lea como intencionado y no como un fallo.
        /// </summary>
        public void SetAccent(Color color)
        {
            accentTarget = color;
        }

        /// <summary>
        /// Aplica el acento ya, sin transición. Para el primer fotograma: si no,
        /// la escena arranca en el color del capítulo 0 y salta al suyo.
        /// </summary>
        public void SnapAccent(Color color)
        {
            accent = color;
            accentTarget = color;
            ApplyAccent();
        }

        /// <summary>
        /// Intensidad del latido, de 0 a 1. En el capítulo de la muerte baja a
        /// 0 y el corazón se apaga; al despertar vuelve.
        /// </summary>
        public void SetHeartbeat(float value)
        {
            heartbeat = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Deja que una silueta siga al acento, para que Eira y NOVA no se
        /// queden fuera de la paleta del capítulo.
        /// </summary>
        public void RegisterSilhouette(Renderer r)
        {
            if (r == null)
                return;

            pickups.Add(r);

            var m = r.material;
            AddGlow(m, m.color, 0.7f, true);
        }

        // =========================================================
        // BUCLE
        // =========================================================

        void Update()
        {
            float dt = Time.deltaTime;
            time += dt;
            orbit += dt * 0.05f;

            UpdateAccent(dt);
            UpdateCamera();
            UpdateHeart(dt);
            UpdateOverlay();
        }

        void UpdateAccent(float dt)
        {
            if (ColorDistance(accent, accentTarget) <= 0.002f)
                return;

            // Lerp exponencial: independiente del framerate.
            float k = 1f - Mathf.Exp(-2.2f * dt);

            accent = Color.Lerp(accent, accentTarget, k);
            ApplyAccent();
        }

        void ApplyAccent()
        {
            for (int i = 0; i < glows.Count; i++)
            {
                var g = glows[i];

                if (g.material == null)
                    continue;

                g.material.SetColor("_EmissionColor", accent * g.intensity);

                if (g.tintAlbedo)
                    g.material.color = g.albedo * 0.3f + accent * 0.35f;
            }

            if (sparks != null)
            {
                // MainModule es un struct con puntero al sistema nativo: se
                // modifica en local y basta. La propiedad main no tiene
                // setter, así que asignarla de vuelta no compila.
                var sparkMain = sparks.main;
                sparkMain.startColor = new ParticleSystem.MinMaxGradient(accent);

                var r = sparks.GetComponent<ParticleSystemRenderer>();

                if (r != null && r.material != null)
                    r.material.color = accent;
            }

            // La niebla se tiñe con el acento: es lo que hace que el fondo
            // deje de ser gris azulado en cuanto cambia el capítulo.
            RenderSettings.fogColor = DeepBase + accent * 0.05f;

            var fill = GameObject.Find("AccentLight");

            if (fill != null)
            {
                var l = fill.GetComponent<Light>();

                if (l != null)
                    l.color = accent;
            }
        }

        void AddGlow(Material m, Color albedo, float intensity, bool tintAlbedo)
        {
            if (m == null)
                return;

            glows.Add(new Glow
            {
                material = m,
                albedo = albedo,
                intensity = intensity,
                tintAlbedo = tintAlbedo
            });
        }

        static float ColorDistance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) +
                   Mathf.Abs(a.b - b.b);
        }

        void UpdateCamera()
        {
            // Se reintenta cada frame si no hay cámara: si el escenario se
            // monta antes de que exista la MainCamera, guardarla una sola vez
            // en Awake dejaría la intro sin cámara para siempre.
            if (cam == null)
                cam = Camera.main;

            if (cam == null)
                return;

            // Órbita corta y baja: la cámara respira, no da vueltas, para que
            // el texto siga siendo lo protagonista.
            float c = Mathf.Cos(orbit);
            float s = Mathf.Sin(orbit);

            cam.transform.position =
                new Vector3(0f, 1.45f, -6.5f) +
                new Vector3(s * 0.7f, 0f, -c * 0.45f + 0.45f);

            cam.transform.LookAt(camLookAt);
        }

        void UpdateHeart(float dt)
        {
            if (heartRing == null || heartPieces.Length == 0)
                return;

            // Latido en dos tiempos (lub-dub): subida rápida y bajada lenta.
            // Un seno puro se lee como vibración, no como corazón.
            heartTimer += dt * (0.9f + heartbeat * 0.7f);

            float phase = Mathf.Repeat(heartTimer, 1f);

            float beat =
                phase < 0.10f ? phase / 0.10f
                : phase < 0.26f ? 1f - (phase - 0.10f) / 0.16f
                : phase < 0.36f ? (phase - 0.26f) / 0.10f * 0.6f
                : 0f;

            float strength = beat * (0.12f + heartbeat * 0.88f);

            heartRing.localScale = Vector3.one * (1f + strength * 0.22f);

            for (int i = 0; i < heartPieces.Length; i++)
            {
                // Los aros exteriores van con retraso: onda que se propaga.
                float b = Mathf.Clamp01(strength - i * 0.09f);

                if (i < heartMaterials.Count && heartMaterials[i] != null)
                {
                    heartMaterials[i].SetColor(
                        "_EmissionColor",
                        accent * (0.6f + b * 3.2f)
                    );
                }

                heartPieces[i].localRotation = Quaternion.Euler(
                    0f, 0f, (heartTimer * 8f + i * 40f) % 360f
                );
            }
        }

        void UpdateOverlay()
        {
            if (vignette == null)
                return;

            // La viñeta respira despacio. Muy poco: si se nota, molesta.
            float glow = 0.5f + 0.5f * Mathf.Sin(time * 1.6f);

            float level = 0.56f + glow * 0.05f + heartbeat * 0.05f;

            var c = vignette.color;
            vignette.color = new Color(
                c.r, c.g, c.b,
                Mathf.Lerp(c.a, level, Time.deltaTime * 3f)
            );

            if (scanlines != null)
            {
                // Se desplaza uvRect, no el rect: mover el rect sacaría la
                // imagen del canvas en vez de desplazar el patrón.
                float offset = Mathf.Repeat(time * 0.05f, 1f);

                scanlines.uvRect = new Rect(0f, offset, 1f, 1f);
            }
        }

        // =========================================================
        // MATERIALES
        // =========================================================

        static Shader LitShader()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");

            return sh != null ? sh : Shader.Find("Standard");
        }

        static Shader UnlitShader()
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");

            if (sh != null)
                return sh;

            sh = Shader.Find("Unlit/Color");
            return sh != null ? sh : Shader.Find("Sprites/Default");
        }

        Material LitMaterial(Color color)
        {
            return new Material(LitShader())
            {
                color = color,
                mainTexture = SoftCircle(64).texture
            };
        }

        /// <summary>
        /// Superficie que además emite. La emisión va en _EmissionColor, que
        /// existe en URP/Lit, y es lo que se recolorea con el acento.
        /// </summary>
        Material NeonMaterial(Color albedo, float intensity, bool tintAlbedo)
        {
            var m = new Material(LitShader())
            {
                color = albedo,
                mainTexture = SoftCircle(64).texture
            };

            m.SetFloat("_Smoothness", 0.5f);

            EnableEmission(m, Color.white);

            AddGlow(m, albedo, intensity, tintAlbedo);

            return m;
        }

        static void EnableEmission(Material m, Color emission)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        Material UnlitMaterial(Color color)
        {
            return new Material(UnlitShader()) { color = color };
        }

        /// <summary>
        /// Retícula del suelo. La textura solo tiene la base oscura y las
        /// líneas en blanco; el color lo pone _EmissionColor, así que al
        /// cambiar el acento el suelo entero se re-tiñe sin tocar la textura.
        /// </summary>
        static Texture2D GridTexture()
        {
            const int size = 128;
            const int cells = 8;
            const float line = 2f;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "StageGrid",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var px = new Color32[size * size];
            float cell = size / (float)cells;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x % cell;
                    float fy = y % cell;

                    bool minor =
                        fx < line || fy < line ||
                        fx > cell - line - 1f || fy > cell - line - 1f;

                    // Cada cuarta línea es una línea mayor: los cuadrados
                    // grandes de una retícula técnica.
                    bool major =
                        x % (cell * 4f) < line * 1.5f ||
                        y % (cell * 4f) < line * 1.5f;

                    int value = major ? 255 : minor ? 120 : 0;

                    px[y * size + x] = new Color32(
                        (byte)value, (byte)value, (byte)value, 255
                    );
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true);

            return tex;
        }

        // =========================================================
        // SPRITES GENERADOS
        // =========================================================

        /// <summary>
        /// Disco suave. Se usa como textura de las neones para que brillen con
        /// el centro y se difuminen en el borde; con una textura lisa, un
        /// plano emisivo se ve como un rectángulo recortado.
        /// </summary>
        static Sprite SoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SoftCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var px = new Color32[size * size];
            float r = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(
                        new Vector2(x, y), new Vector2(r, r)
                    ) / r;

                    float a = Mathf.Clamp01(1f - d);
                    a *= a;

                    px[y * size + x] =
                        new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply();

            return Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f
            );
        }

        static Sprite VignetteSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Vignette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var px = new Color32[size * size];
            float r = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - r) / r;
                    float dy = (y - r) / r;

                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Pow(d, 1.7f));

                    px[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply();

            return Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f
            );
        }

        static Texture2D ScanlineTexture(int height)
        {
            var tex = new Texture2D(4, height, TextureFormat.RGBA32, false)
            {
                name = "Scanlines",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var px = new Color32[4 * height];

            for (int y = 0; y < height; y++)
            {
                var c = y % 3 == 0
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);

                for (int x = 0; x < 4; x++)
                    px[y * 4 + x] = c;
            }

            tex.SetPixels32(px);
            tex.Apply();

            return tex;
        }
    }
}
