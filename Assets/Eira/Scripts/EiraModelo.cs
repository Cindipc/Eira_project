using System;
using UnityEngine;

namespace EiraGame
{
    // Construye a Eira de forma nativa (primitivas): una mujer con cabello largo,
    // ropa negra, piel clara, ojos claros y cara completa (ojos, cejas, nariz, boca, orejas).
    // Las partes se nombran torso/head/legL/legR/armL/armR para que PlayerController las anime.
    public static class EiraModelo
    {
        public static bool IsNativa(Transform visual)
        {
            return visual != null && visual.Find("torso") != null && visual.Find("head") != null;
        }

        public static void EnsureNativa(Transform visual, Func<Color, Material> matFactory)
        {
            if (visual == null || IsNativa(visual)) return;

            // quitar mesh importado / provisional
            var mf = visual.GetComponent<MeshFilter>();
            if (mf != null) DestroyIf(mf);
            var mr = visual.GetComponent<MeshRenderer>();
            if (mr != null) DestroyIf(mr);
            for (int i = visual.childCount - 1; i >= 0; i--)
                DestroyIf(visual.GetChild(i).gameObject);

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

            BuildNativa(visual, matFactory);
        }

        public static void BuildNativa(Transform vis, Func<Color, Material> mat)
        {
            var mSkin = mat(new Color(0.96f, 0.76f, 0.63f));
            var mCloth = mat(new Color(0.055f, 0.055f, 0.068f));
            var mBoot = mat(new Color(0.02f, 0.02f, 0.025f));
            var mHair = mat(new Color(0.14f, 0.10f, 0.09f));
            var mEye = mat(new Color(0.45f, 0.72f, 1f));
            var mLip = mat(new Color(0.42f, 0.11f, 0.11f));
            var mAcc = mat(new Color(0.6f, 0.63f, 0.68f));

            // ---- piernas (negras) con botas ----
            Leg(vis, "legL", new Vector3(0.10f, 0.85f, 0f), mCloth, mBoot);
            Leg(vis, "legR", new Vector3(-0.10f, 0.85f, 0f), mCloth, mBoot);

            // cadera
            Part(vis, PrimitiveType.Cube, new Vector3(0f, 0.78f, 0f), new Vector3(0.27f, 0.13f, 0.17f), mCloth);

            // ---- torso con cintura y silueta femenina ----
            var torso = Pivot(vis, "torso", new Vector3(0f, 0.85f, 0f));
            Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.21f, 0.13f, 0.14f), mCloth);
            Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.23f, 0f), new Vector3(0.29f, 0.20f, 0.17f), mCloth);
            Part(torso, PrimitiveType.Sphere, new Vector3(0.085f, 0.30f, 0.075f), new Vector3(0.055f, 0.05f, 0.04f), mCloth);
            Part(torso, PrimitiveType.Sphere, new Vector3(-0.085f, 0.30f, 0.075f), new Vector3(0.055f, 0.05f, 0.04f), mCloth);
            Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.41f, 0f), new Vector3(0.37f, 0.15f, 0.21f), mCloth);
            Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.27f, 0.085f), new Vector3(0.03f, 0.46f, 0.01f), mAcc);
            Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.485f, 0f), new Vector3(0.15f, 0.035f, 0.04f), mCloth);
            Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.56f, 0f), new Vector3(0.09f, 0.09f, 0.09f), mSkin);

            // ---- brazos con mangas negras ----
            Arm(vis, "armL", new Vector3(0.225f, 1.26f, 0f), mCloth, mSkin);
            Arm(vis, "armR", new Vector3(-0.225f, 1.26f, 0f), mCloth, mSkin);

            // ---- cabeza con cara ----
            var head = Pivot(vis, "head", new Vector3(0f, 1.44f, 0f));
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.10f, 0f), new Vector3(0.30f, 0.37f, 0.30f), mSkin);
            Part(head, PrimitiveType.Sphere, new Vector3(0.155f, 0.10f, 0.005f), new Vector3(0.032f, 0.06f, 0.035f), mSkin);
            Part(head, PrimitiveType.Sphere, new Vector3(-0.155f, 0.10f, 0.005f), new Vector3(0.032f, 0.06f, 0.035f), mSkin);
            Part(head, PrimitiveType.Sphere, new Vector3(0.075f, 0.145f, 0.135f), new Vector3(0.035f, 0.024f, 0.02f), mEye);
            Part(head, PrimitiveType.Sphere, new Vector3(-0.075f, 0.145f, 0.135f), new Vector3(0.035f, 0.024f, 0.02f), mEye);
            Part(head, PrimitiveType.Cube, new Vector3(0.075f, 0.185f, 0.13f), new Vector3(0.09f, 0.014f, 0.02f), mHair);
            Part(head, PrimitiveType.Cube, new Vector3(-0.075f, 0.185f, 0.13f), new Vector3(0.09f, 0.014f, 0.02f), mHair);
            Part(head, PrimitiveType.Cube, new Vector3(0f, 0.115f, 0.148f), new Vector3(0.03f, 0.05f, 0.028f), mSkin);
            Part(head, PrimitiveType.Cube, new Vector3(0f, 0.072f, 0.14f), new Vector3(0.06f, 0.022f, 0.015f), mLip);

            // cabello largo (casquete + flequillo + mechones que caen por la espalda y los hombros)
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.13f, -0.02f), new Vector3(0.345f, 0.38f, 0.37f), mHair);
            Part(head, PrimitiveType.Cube, new Vector3(0f, 0.26f, 0.085f), new Vector3(0.31f, 0.065f, 0.09f), mHair);
            // mechones laterales enmarcando la cara
            Part(head, PrimitiveType.Cube, new Vector3(0.20f, 0.0f, 0.02f), new Vector3(0.06f, 0.36f, 0.05f), mHair);
            Part(head, PrimitiveType.Cube, new Vector3(-0.20f, 0.0f, 0.02f), new Vector3(0.06f, 0.36f, 0.05f), mHair);
            // masa principal del pelo por la espalda (hasta la espalda media)
            Part(head, PrimitiveType.Cube, new Vector3(0f, -0.22f, -0.16f), new Vector3(0.34f, 0.64f, 0.07f), mHair);
            Part(head, PrimitiveType.Cube, new Vector3(0f, -0.62f, -0.13f), new Vector3(0.24f, 0.22f, 0.05f), mHair);
            Part(head, PrimitiveType.Sphere, new Vector3(0f, -0.72f, -0.12f), new Vector3(0.15f, 0.13f, 0.05f), mHair);
            // mechones frontales que caen sobre los hombros
            Part(head, PrimitiveType.Cube, new Vector3(0.15f, -0.12f, 0.17f), new Vector3(0.055f, 0.32f, 0.05f), mHair);
            Part(head, PrimitiveType.Cube, new Vector3(-0.15f, -0.12f, 0.17f), new Vector3(0.055f, 0.32f, 0.05f), mHair);
        }

        static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        static GameObject Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(t);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyIf(c);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material = m;
            return go;
        }

        static void DestroyIf(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        static void Leg(Transform vis, string name, Vector3 hipPos, Material cloth, Material boot)
        {
            var p = Pivot(vis, name, hipPos);
            Part(p, PrimitiveType.Capsule, new Vector3(0f, -0.40f, 0f), new Vector3(0.115f, 0.80f, 0.115f), cloth);
            Part(p, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.04f), new Vector3(0.135f, 0.10f, 0.26f), boot);
        }

        static void Arm(Transform vis, string name, Vector3 shoulderPos, Material sleeve, Material skin)
        {
            var p = Pivot(vis, name, shoulderPos);
            Part(p, PrimitiveType.Capsule, new Vector3(0f, -0.29f, 0f), new Vector3(0.075f, 0.58f, 0.075f), sleeve);
            Part(p, PrimitiveType.Sphere, new Vector3(0f, -0.60f, 0f), new Vector3(0.09f, 0.095f, 0.09f), skin);
        }
    }
}