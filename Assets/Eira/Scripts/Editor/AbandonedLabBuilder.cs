using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;
using System.Collections.Generic;

namespace EiraGame.Editor
{
    /// <summary>
    /// Construye el escenario de laboratorio abandonado y destruido para Nivel 1.
    /// 
    /// Usa lo que ya existe (FuturisticCity_Env, mesh/materiales, prefabs) y añade
    /// daños, escombros, máquinas destruidas, robots destruidos y efectos de ambientación.
    /// No mueve spawn/puzzle/jefe/salida ni colliders del recorrido.
    /// </summary>
    public static class AbandonedLabBuilder
    {
        private const string MenuPath = "Eira/Laboratorio abandonado (Nivel 1)";
        private const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
        private const string RootName = "LabAbandonado";
        private const string CityName = "FuturisticCity_Environment";

        private static class Mat
        {
            public static Material metalDark;
            public static Material metalRust;
            public static Material panelBroken;
            public static Material cable;
            public static Material debrisMat;
            public static Material glassBroken;
            public static Material emissionCyanFlicker;
            public static Material emissionRed;
            public static Material smokeMat;
            public static Material sparksMat;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SetupMaterials();

            // Asegurar que el entorno futurista exista pero podemos ajustarlo
            GameObject city = GameObject.Find(CityName);
            if (city == null)
            {
                city = GameObject.Find("FuturisticCity_Env");
            }

            // Desactivar SunDia/SunTarde para ambiente interior
            ToggleLights(false);

            // Crear root
            GameObject root = GameObject.Find(RootName);
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
            root = new GameObject(RootName);
            root.isStatic = false;

            // Construir elementos
            BuildLighting(root.transform);
            BuildDamageAndDebris(root.transform);
            BuildDestroyedMachines(root.transform);
            BuildDestroyedRobots(root.transform);
            BuildEffects(root.transform);
            BuildSpawnAreaDetails(root.transform);
            MarkStaticGeometry(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AbandonedLabBuilder] Laboratorio abandonado creado en " + RootName);
        }

        [MenuItem(MenuPath + " (Regenerar)", true)]
        private static bool CanRegenerate()
        {
            return GameObject.Find(RootName) != null;
        }

        [MenuItem(MenuPath + " (Regenerar)")]
        public static void Regenerate()
        {
            // mismo que Build: destruye y reconstruye
            var root = GameObject.Find(RootName);
            if (root != null) Object.DestroyImmediate(root);
            Build();
        }

        private static void SetupMaterials()
        {
            Mat.metalDark = ModelFactory.Lit(new Color(0.18f, 0.2f, 0.24f), 0.25f, 0.3f);
            Mat.metalRust = ModelFactory.Lit(new Color(0.32f, 0.26f, 0.2f), 0.1f, 0.25f);
            Mat.panelBroken = ModelFactory.Lit(new Color(0.12f, 0.16f, 0.22f), 0.15f, 0.35f);
            Mat.cable = ModelFactory.Lit(new Color(0.05f, 0.06f, 0.08f), 0.05f, 0.2f);
            Mat.debrisMat = ModelFactory.Lit(new Color(0.22f, 0.22f, 0.22f), 0.15f, 0.25f);
            Mat.glassBroken = ModelFactory.Lit(new Color(0.5f, 0.7f, 0.85f), 0f, 0.6f);
            Mat.emissionCyanFlicker = ModelFactory.Glow(new Color(0.15f, 0.7f, 0.95f));
            Mat.emissionRed = ModelFactory.Glow(new Color(1f, 0.15f, 0.12f));
            Mat.smokeMat = ModelFactory.Glow(new Color(0.5f, 0.5f, 0.55f));
            Mat.sparksMat = ModelFactory.Glow(new Color(1f, 0.8f, 0.3f));
        }

        private static void ToggleLights(bool day)
        {
            var sunDia = GameObject.Find("SunDia");
            if (sunDia != null) sunDia.SetActive(day);
            var sunTarde = GameObject.Find("SunTarde");
            if (sunTarde != null) sunTarde.SetActive(day);
            var sunOld = GameObject.Find("Sun");
            if (sunOld != null) sunOld.SetActive(day);
        }

        private static void BuildLighting(Transform parent)
        {
            var lighting = new GameObject("LabLighting");
            lighting.transform.SetParent(parent, false);

            // Luz ambiente tenue
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.04f, 0.06f, 0.1f);
            RenderSettings.ambientEquatorColor = new Color(0.02f, 0.04f, 0.08f);
            RenderSettings.ambientGroundColor = new Color(0.01f, 0.01f, 0.02f);
            RenderSettings.reflectionIntensity = 0.25f;

            // Skybox oscuro (fallback)
            var sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (sky != null)
            {
                var darkSky = new Material(sky);
                darkSky.SetColor("_SkyTint", new Color(0.02f, 0.03f, 0.08f));
                darkSky.SetFloat("_Exposure", 0.3f);
                RenderSettings.skybox = darkSky;
            }

            // Luces de emergencia rojas
            Color red = new Color(1f, 0.15f, 0.12f);
            var spots = new[] { 20f, 50f, 80f, 110f };
            foreach (var x in spots)
            {
                var lgo = new GameObject("EmergencyRed");
                lgo.transform.SetParent(lighting.transform, false);
                lgo.transform.position = new Vector3(x, 3f, 8f);
                var li = lgo.AddComponent<Light>();
                li.type = LightType.Point;
                li.color = red;
                li.intensity = 0.8f;
                li.range = 10f;
                li.shadows = LightShadows.None;
                var flick = lgo.AddComponent<LabFlicker>();
                flick.targetLight = li;
                flick.baseIntensity = 0.8f;
                flick.mode = FlickerMode.Blink;
                flick.speed = 2f;
                flick.jitter = 0.2f;

                var lgo2 = new GameObject("EmergencyRed");
                lgo2.transform.SetParent(lighting.transform, false);
                lgo2.transform.position = new Vector3(x, 3f, -8f);
                var li2 = lgo2.AddComponent<Light>();
                li2.type = LightType.Point;
                li2.color = red;
                li2.intensity = 0.8f;
                li2.range = 10f;
                li2.shadows = LightShadows.None;
                var f2 = lgo2.AddComponent<LabFlicker>();
                f2.targetLight = li2;
                f2.baseIntensity = 0.8f;
                f2.mode = FlickerMode.Blink;
                f2.speed = 2.2f;
            }

            // Acentos cian parpadeantes
            var cyanPts = new[] { 10f, 40f, 70f, 100f };
            foreach (var x in cyanPts)
            {
                var c = new GameObject("AccentCyan");
                c.transform.SetParent(lighting.transform, false);
                c.transform.position = new Vector3(x, 2.8f, 0f);
                var cl = c.AddComponent<Light>();
                cl.type = LightType.Point;
                cl.color = new Color(0.2f, 0.85f, 0.98f);
                cl.intensity = 0.9f;
                cl.range = 8f;
                cl.shadows = LightShadows.Hard;
                var cf = c.AddComponent<LabFlicker>();
                cf.targetLight = cl;
                cf.baseIntensity = 0.9f;
                cf.mode = FlickerMode.Flicker;
                cf.speed = 12f;
                cf.jitter = 0.6f;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.04f, 0.06f, 0.12f);
            RenderSettings.fogDensity = 0.05f;
        }

        private static void BuildDamageAndDebris(Transform parent)
        {
            var dmg = new GameObject("DamageDebris");
            dmg.transform.SetParent(parent, false);

            // Huecos/paneles rotos distribuidos
            var cracks = new[] { new Vector3(15, 2.5f, 11f), new Vector3(45, 2.5f, -11f), new Vector3(75, 2.5f, 11f), new Vector3(105, 2.5f, -11f) };
            foreach (var p in cracks)
            {
                var panel = ModelFactory.Prim(dmg.transform, PrimitiveType.Cube, p, new Vector3(2f, 1.2f, 0.05f), Mat.panelBroken, keepCollider: false);
                panel.transform.Rotate(10f * Random.Range(-1,2), 0, 15f * Random.Range(-1,2));
                var pb = panel.AddComponent<Rigidbody>();
                pb.isKinematic = true;
            }

            // Escombros en suelo
            for (int i = 0; i < 25; i++)
            {
                var pos = new Vector3(5 + i * 4.5f + Random.Range(-0.8f, 0.8f), 0.1f, Random.Range(-8f, 8f));
                var s = new Vector3(Random.Range(0.3f, 1.2f), Random.Range(0.1f, 0.4f), Random.Range(0.3f, 1.0f));
                ModelFactory.Prim(dmg.transform, PrimitiveType.Cube, pos, s, Mat.debrisMat);
            }

            // Tuberías rotas y cables
            for (int i = 0; i < 8; i++)
            {
                var x = 10 + i * 12f;
                var pipe = ModelFactory.Prim(dmg.transform, PrimitiveType.Cylinder, new Vector3(x, 3.2f, 10.5f), new Vector3(0.15f, 2f, 0.15f), Mat.metalRust);
                pipe.transform.Rotate(90, 0, 0);
                var cableObj = ModelFactory.Prim(dmg.transform, PrimitiveType.Cube, new Vector3(x, 2.2f, -10.5f), new Vector3(0.05f, 0.05f, 6f), Mat.cable);
                cableObj.transform.Rotate(Random.Range(-15,15), Random.Range(-20,20), 0);
            }
        }

        private static void BuildDestroyedMachines(Transform parent)
        {
            var mach = new GameObject("DestroyedMachines");
            mach.transform.SetParent(parent, false);

            var spots = new[] { new Vector3(8,0,-7), new Vector3(30,0,7), new Vector3(65,0,-7), new Vector3(95,0,7) };
            for (int i = 0; i < spots.Length; i++)
            {
                var rootM = new GameObject("ConsolaRota_" + (i+1));
                rootM.transform.SetParent(mach.transform, false);
                rootM.transform.position = spots[i];
                rootM.transform.Rotate(0, i*45, 0);

                ModelFactory.Prim(rootM.transform, PrimitiveType.Cube, new Vector3(0,0.25f,0), new Vector3(1.6f,0.5f,1.2f), Mat.metalRust, keepCollider:false);
                ModelFactory.Prim(rootM.transform, PrimitiveType.Cube, new Vector3(-0.3f,0.8f,-0.2f), new Vector3(0.6f,0.6f,0.6f), Mat.panelBroken);
                rootM.transform.Rotate(15, -10, 5);
                ModelFactory.Prim(rootM.transform, PrimitiveType.Cube, new Vector3(0.4f,0.5f,0.3f), new Vector3(0.3f,0.3f,0.3f), Mat.metalDark);
                AddSparkEmitter(rootM.transform, new Vector3(0,0.8f,0.6f));
            }
        }

        private static void BuildDestroyedRobots(Transform parent)
        {
            var bots = new GameObject("DestroyedRobots");
            bots.transform.SetParent(parent, false);

            var spots = new[] { new Vector3(18,0,7), new Vector3(55,0,-7), new Vector3(85,0,6), new Vector3(115,0,-6) };
            for (int i = 0; i < spots.Length; i++)
            {
                var r = new GameObject("AndroidRoto_" + (i+1));
                r.transform.SetParent(bots.transform, false);
                r.transform.position = spots[i];
                r.transform.Rotate(0, 180 + i*30, -90f + Random.Range(-10,10));
                r.transform.localScale = new Vector3(0.8f,0.8f,0.8f);

                ModelFactory.Prim(r.transform, PrimitiveType.Cube, new Vector3(0,0.3f,0), new Vector3(0.25f,0.6f,0.25f), Mat.metalRust);
                ModelFactory.Prim(r.transform, PrimitiveType.Cube, new Vector3(0,0.9f,0), new Vector3(0.4f,0.4f,0.3f), Mat.panelBroken);
                ModelFactory.Prim(r.transform, PrimitiveType.Cube, new Vector3(0,1.25f,0), new Vector3(0.25f,0.2f,0.25f), Mat.metalDark);
                var arm = ModelFactory.Prim(r.transform, PrimitiveType.Cube, new Vector3(0.25f,0.8f,0), new Vector3(0.1f,0.4f,0.1f), Mat.metalRust);
                arm.transform.Rotate(-30,0,-45);
                var head = ModelFactory.Prim(r.transform, PrimitiveType.Cube, new Vector3(0,1.4f,0), new Vector3(0.2f,0.15f,0.2f), Mat.glassBroken);
                var flick = head.AddComponent<LabFlicker>();
                flick.emissiveRenderer = head.GetComponent<Renderer>();
                flick.emissionColor = new Color(0.2f,0.8f,0.95f);
                flick.baseIntensity = 0;
                flick.emissionIntensity = 1.5f;
                flick.mode = FlickerMode.Flicker;
                flick.speed = 14f;
            }
        }

        private static void AddSparkEmitter(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.4f;
            main.startSpeed = 4f;
            main.startSize = 0.02f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 20;

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

            var life = ps.sizeOverLifetime;
            life.enabled = true;
            AnimationCurve curve = AnimationCurve.EaseInOut(0,1,1,0);
            life.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = CombatFX.GlowMaterial(new Color(1f,0.8f,0.3f), 8f);
        }

        private static void BuildEffects(Transform parent)
        {
            var fx = new GameObject("LabEffects");
            fx.transform.SetParent(parent, false);

            var smokePts = new[] { new Vector3(25,1,2), new Vector3(60,1,-2), new Vector3(90,1,3) };
            foreach (var p in smokePts)
            {
                var sgo = new GameObject("Smoke");
                sgo.transform.SetParent(fx.transform, false);
                sgo.transform.position = p;
                var ps = sgo.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true;
                main.startLifetime = 6f;
                main.startSpeed = 0.4f;
                main.startSize = 0.8f;
                main.startColor = new Color(0.3f,0.3f,0.35f,0.15f);
                main.maxParticles = 40;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 10f;
                shape.radius = 0.5f;
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.1f;
                noise.frequency = 0.3f;
                var rend = ps.GetComponent<ParticleSystemRenderer>();
                rend.material = CombatFX.GlowMaterial(new Color(0.5f,0.5f,0.55f), 0.5f);
                rend.renderMode = ParticleSystemRenderMode.Billboard;
            }

            var sparkPts = new[] { new Vector3(32,0.8f,0.6f), new Vector3(67,0.8f,-0.6f), new Vector3(97,0.8f,0.6f) };
            foreach (var p in sparkPts)
            {
                AddSparkEmitter(fx.transform, p);
            }
        }

        private static void BuildSpawnAreaDetails(Transform parent)
        {
            var area = new GameObject("SpawnPlatform");
            area.transform.SetParent(parent, false);
            area.transform.position = new Vector3(6, -0.1f, 0);

            var plat = ModelFactory.Prim(area.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(2.5f, 0.2f, 3f), Mat.metalDark, keepCollider: false);
            plat.name = "Camilla_Plataforma";
            var rail = ModelFactory.Prim(area.transform, PrimitiveType.Cube, new Vector3(0,0.4f,-1.4f), new Vector3(2.4f,0.05f,0.1f), Mat.metalRust);
            var cables = ModelFactory.Prim(area.transform, PrimitiveType.Cube, new Vector3(-0.5f,0.25f,1f), new Vector3(0.05f,0.5f,0.8f), Mat.cable);
            cables.transform.Rotate(0,20,0);
            ModelFactory.Prim(area.transform, PrimitiveType.Cube, new Vector3(0.5f,0.25f,1f), new Vector3(0.05f,0.5f,0.8f), Mat.cable).transform.Rotate(0,-20,0);
        }

        private static void MarkStaticGeometry(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var go = r.gameObject;
                if (go.name.Contains("Sparks") || go.name.Contains("Smoke")) continue;
                go.isStatic = true;
            }
            var lights = root.GetComponentsInChildren<Light>(true);
            foreach (var l in lights) l.shadows = LightShadows.None;
        }
    }
}