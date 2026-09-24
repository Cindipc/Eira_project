using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EiraGame.Editor
{
    public enum HumanStyle { Eira, Nova, Kael }

    // Construye los personajes estilizados a partir de primitivas (sin mallas externas).
    public static class ModelFactory
    {
        static Material litMat;
        static Material glowMat;
        static readonly Dictionary<string, Material> savedMaterials = new Dictionary<string, Material>();

        static Material Persist(string key, string fname, Material m)
        {
            if (savedMaterials.TryGetValue(key, out var existing) && existing != null) return existing;
            var dir = "Assets/Eira/Materials/ModelFactory";
            var path = dir + "/" + fname + ".mat";
            if (System.IO.File.Exists(path))
            {
                var old = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (old != null) { savedMaterials[key] = old; return old; }
            }
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(m, path);
            savedMaterials[key] = m;
            return m;
        }

        public static Material Lit(Color c, float metal = 0f, float smooth = 0.3f)
        {
            string key = "lit" + c + "|" + metal + "|" + smooth;
            if (savedMaterials.TryGetValue(key, out var hit) && hit != null) return hit;
            if (litMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                litMat = new Material(sh != null ? sh : Shader.Find("Standard"));
            }
            var m = new Material(litMat);
            m.name = "mat_" + c.ToString().Replace('(', '_').Replace(')', '_') + "_" + metal;
            m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            m.enableInstancing = true;
            return Persist(key, "lit_" + ((int)(c.r * 255)).ToString("X2") + ((int)(c.g * 255)).ToString("X2") + ((int)(c.b * 255)).ToString("X2") + "_" + (int)(metal * 100), m);
        }

        public static Material Glow(Color c)
        {
            string key = "glow" + c;
            if (savedMaterials.TryGetValue(key, out var hit) && hit != null) return hit;
            if (glowMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                glowMat = new Material(sh != null ? sh : Shader.Find("Standard"));
            }
            var m = new Material(glowMat);
            m.name = "glow_" + c.ToString();
            m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.3f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.8f);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.6f); }
            return Persist(key, "glow_" + ((int)(c.r * 255)).ToString("X2") + ((int)(c.g * 255)).ToString("X2") + ((int)(c.b * 255)).ToString("X2"), m);
        }

        public static GameObject Prim(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat, int layer = 0, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.layer = layer;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (mat != null && r != null) r.material = mat;
            return go;
        }

        public static void DestroyPrimCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        public static TextMesh AddLabel(GameObject parent, string text, float size, Color color)
        {
            var go = new GameObject("Label", typeof(TextMesh));
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, 0.14f);
            var tm = go.GetComponent<TextMesh>();
            try { tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            return tm;
        }

        // ---- Humanoides (Eira / NOVA / Kael) ----
        public static GameObject BuildHumanoid(string name, HumanStyle style, Material[] mats)
        {
            var root = new GameObject(name);
            var vis = new GameObject("Visual");
            vis.transform.SetParent(root.transform, false);
            vis.transform.localPosition = Vector3.zero;

            var jacket = mats[0];
            var skin = mats[1];
            var dark = mats[2];

            // torso
            var torso = Prim(vis.transform, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.44f, 0.46f, 0.26f), jacket);
            torso.name = "torso";

            if (style == HumanStyle.Kael)
            {
                torso.transform.localScale = new Vector3(0.5f, 0.6f, 0.32f);
                torso.transform.localPosition = new Vector3(0f, 1.28f, 0f);
            }

            // paneles laterales del cuerpo (NOVA: arquitectura blanca/negra)
            if (style == HumanStyle.Nova)
            {
                Prim(vis.transform, PrimitiveType.Cube, new Vector3(0f, 1.15f, 0.14f), new Vector3(0.42f, 0.3f, 0.02f), dark);
                Prim(vis.transform, PrimitiveType.Cube, new Vector3(0f, 1.28f, -0.15f), new Vector3(0.44f, 0.2f, 0.02f), dark);
                AddLabel(root, "NOVA", 0.05f, new Color(0.2f, 0.6f, 1f));
            }

            // cabeza
            var headPivot = new GameObject("head");
            headPivot.transform.SetParent(vis.transform, false);
            headPivot.transform.localPosition = new Vector3(0f, 1.48f, 0f);
            var headGo = Prim(headPivot.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.32f, skin);
            headGo.name = "headMesh";

            if (style == HumanStyle.Nova)
            {
                Prim(headPivot.transform, PrimitiveType.Cube, new Vector3(0f, 0f, -0.18f), new Vector3(0.34f, 0.16f, 0.04f), dark);
                Prim(headPivot.transform, PrimitiveType.Sphere, new Vector3(0.4f, 0.02f, 0.16f), Vector3.one * 0.09f, glowMat ?? Glow(new Color(0.2f, 0.6f, 1f)));
            }
            else
            {
                Prim(headPivot.transform, PrimitiveType.Sphere, new Vector3(0.24f, 0.04f, 0.2f), Vector3.one * 0.06f, glowMat ?? Glow(style == HumanStyle.Kael ? new Color(1f, 0.6f, 0.1f) : new Color(0.25f, 0.55f, 1f)));
                Prim(headPivot.transform, PrimitiveType.Sphere, new Vector3(-0.24f, 0.04f, 0.2f), Vector3.one * 0.05f, skin);
                // pelo
                var hairCol = style == HumanStyle.Eira ? new Color(0.25f, 0.16f, 0.12f) : new Color(0.08f, 0.07f, 0.07f);
                var hair = Prim(headPivot.transform, PrimitiveType.Sphere, new Vector3(0f, 0.12f, -0.04f), new Vector3(1.15f, 0.85f, 1.05f), Lit(hairCol));
                Object.DestroyImmediate(hair.GetComponent<Collider>());
            }

            // piernas
            BuildLimb(vis.transform, "legL", new Vector3(0.13f, 0.98f, 0f), new Vector3(0.115f, 0.46f, 0.115f), dark, style == HumanStyle.Nova);
            BuildLimb(vis.transform, "legR", new Vector3(-0.13f, 0.98f, 0f), new Vector3(0.115f, 0.46f, 0.115f), dark, style == HumanStyle.Nova);
            // brazos
            var armMat = style == HumanStyle.Nova ? dark : jacket;
            BuildLimb(vis.transform, "armL", new Vector3(0.3f, 1.38f, 0f), new Vector3(0.09f, 0.3f, 0.09f), armMat, false);
            BuildLimb(vis.transform, "armR", new Vector3(-0.3f, 1.38f, 0f), new Vector3(0.09f, 0.3f, 0.09f), armMat, false);

            if (style == HumanStyle.Kael)
            {
                // abrigo largo: extensión bajo el torso
                Prim(vis.transform, PrimitiveType.Cube, new Vector3(0f, 0.72f, 0f), new Vector3(0.52f, 0.6f, 0.3f), jacket);
                // vendas/arpés metálicos en mangas
                Prim(vis.transform, PrimitiveType.Cube, new Vector3(0.34f, 1.2f, 0f), new Vector3(0.1f, 0.06f, 0.14f), mats[3] ?? jacket);
                Prim(vis.transform, PrimitiveType.Cube, new Vector3(-0.34f, 1.2f, 0f), new Vector3(0.1f, 0.06f, 0.14f), mats[3] ?? jacket);
            }

            // botas
            if (style != HumanStyle.Kael)
            {
                Prim(vis.transform, PrimitiveType.Cube, new Vector3(0.13f, 0.08f, 0f), new Vector3(0.14f, 0.09f, 0.24f), dark);
                Prim(vis.transform, PrimitiveType.Cube, new Vector3(-0.13f, 0.08f, 0f), new Vector3(0.14f, 0.09f, 0.24f), dark);
            }

            return root;
        }

        static void BuildLimb(Transform vis, string name, Vector3 pivotPos, Vector3 meshScale, Material mat, bool tech)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(vis, false);
            pivot.transform.localPosition = pivotPos;
            var mesh = Prim(pivot.transform, PrimitiveType.Capsule, new Vector3(0f, -meshScale.y, 0f), Vector3.one, mat);
            mesh.transform.localScale = meshScale;
            if (tech)
            {
                Prim(pivot.transform, PrimitiveType.Cube, new Vector3(0f, -meshScale.y, meshScale.z), new Vector3(meshScale.x * 1.3f, meshScale.y, 0.02f), Glow(new Color(0.2f, 0.5f, 1f)));
            }
        }

        // ---- Dron ----
        public static GameObject BuildDrone(string name, Material[] mats)
        {
            var root = new GameObject(name);
            var body = Prim(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one, mats[0]);
            body.transform.localScale = new Vector3(0.3f, 0.18f, 0.3f);
            var ring = Prim(root.transform, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, mats[1]);
            ring.transform.localScale = new Vector3(0.46f, 0.03f, 0.46f);
            ring.transform.Rotate(90f, 0f, 0f);
            var blade1 = Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), Vector3.one, mats[1]);
            blade1.transform.localScale = new Vector3(0.4f, 0.015f, 0.06f);
            var blade2 = Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), Vector3.one, mats[1]);
            blade2.transform.localScale = new Vector3(0.06f, 0.015f, 0.4f);
            var eye = new GameObject("eyeLight");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            var eyeGo = Prim(eye.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.09f, Glow(new Color(1f, 0.25f, 0.15f)));
            Object.DestroyImmediate(eyeGo.GetComponent<Collider>());
            var eye2 = new GameObject("eyeLight2");
            eye2.transform.SetParent(root.transform, false);
            eye2.transform.localPosition = new Vector3(0f, 0f, -0.22f);
            Prim(eye2.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.06f, Glow(new Color(0.8f, 0.2f, 0.1f)));
            return root;
        }

        // ---- Máquina Guardiana (jefe) ----
        public static GameObject BuildGuardian(string name, Material[] mats)
        {
            var root = new GameObject(name);
            var metal = mats[0];
            var metal2 = mats[1];
            // patas
            Prim(root.transform, PrimitiveType.Cube, new Vector3(0.45f, 0.42f, 0f), new Vector3(0.45f, 0.85f, 0.5f), metal2);
            Prim(root.transform, PrimitiveType.Cube, new Vector3(-0.45f, 0.42f, 0f), new Vector3(0.45f, 0.85f, 0.5f), metal2);
            // cuerpo
            Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(2.3f, 1.5f, 1.2f), metal);
            // torreta/cabeza
            Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 2.3f, 0f), Vector3.one, metal);
            var headBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(headBase.GetComponent<Collider>());
            headBase.transform.SetParent(root.transform, false);
            headBase.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);
            headBase.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            headBase.GetComponent<Renderer>().material = metal2;
            // núcleo
            var core = new GameObject("core");
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = new Vector3(0f, 1.2f, 0.6f);
            var coreGo = Prim(core.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.55f, Glow(new Color(0.3f, 0.8f, 1f)));
            Object.DestroyImmediate(coreGo.GetComponent<Collider>());
            // ojo
            var eye = new GameObject("eye");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 2.25f, 0.62f);
            Prim(eye.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(1.1f, 0.18f, 0.1f), Glow(new Color(1f, 0.2f, 0.1f)));
            // luz roja
            var lightGo = new GameObject("redLight", typeof(Light));
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0.8f);
            var l = lightGo.GetComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.2f, 0.15f);
            l.range = 8f;
            l.intensity = 4f;
            return root;
        }

        // ---- Puerta corrediza ----
        public static GameObject BuildSlidingDoor(string name, Material[] mats)
        {
            var root = new GameObject(name);
            var door = Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.55f, 0f), new Vector3(4.4f, 3.0f, 0.35f), mats[0], keepCollider: true);
            door.name = "doorPanel";
            var strip = Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.55f, 0.2f), new Vector3(4.0f, 0.14f, 0.04f), Glow(new Color(0.2f, 0.9f, 0.8f)));
            strip.transform.position = door.transform.position + new Vector3(0f, 0f, 0.2f);
            var dc = root.AddComponent<EiraGame.DoorControl>();
            dc.doorPanel = door.transform;
            return root;
        }

        // ---- Consola (panel activador) ----
        public static GameObject BuildPanel(string name, Material[] mats, bool tall = false)
        {
            var root = new GameObject(name);
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.3f, 1.9f, 0.7f);
            col.center = new Vector3(0f, 0.95f, 0f);
            Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(0.85f, 1.5f, 0.5f), mats[0]);
            var glow = new GameObject("Glow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.9f, 0.3f);
            var glowGo = Prim(glow.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.6f, 0.42f, 0.06f), Glow(new Color(0.2f, 0.7f, 1f)));
            Object.DestroyImmediate(glowGo.GetComponent<Collider>());
            return root;
        }

        // ---- Registro de información ----
        public static GameObject BuildDataLog(string name, Material[] mats)
        {
            var root = new GameObject(name);
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.4f;
            Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), Vector3.one, mats[0]).transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            var ring = new GameObject("Glow");
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            var ringGo = Prim(ring.transform, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, Glow(new Color(0.35f, 0.85f, 1f)));
            ringGo.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);
            ringGo.transform.Rotate(90f, 0f, 0f);
            Object.DestroyImmediate(ringGo.GetComponent<Collider>());
            return root;
        }

        // ---- Botiquín / cápsula ----
        public static GameObject BuildPickup(string name, PickupKind kind, Material[] mats)
        {
            var root = new GameObject(name);
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.9f;
            if (kind == PickupKind.Medkit)
            {
                var body = Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), new Vector3(0.5f, 0.3f, 0.5f), mats[0]);
                Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0.26f), new Vector3(0.7f, 0.14f, 0.05f), mats[2]);
                Prim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0.26f), new Vector3(0.14f, 0.7f, 0.05f), mats[2]);
            }
            else
            {
                var cell = Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), Vector3.one, Glow(new Color(0.2f, 0.6f, 1f)));
                cell.transform.localScale = new Vector3(0.35f, 0.55f, 0.35f);
            }
            return root;
        }
    }
}