using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Medición del cuerpo de Eira y encaje de la cápsula.
    ///
    /// Vive en runtime (y no dentro de PlayerController) porque lo usan tanto
    /// el juego como el editor, en EiraRepair. Antes estaba duplicado en los
    /// dos sitios y ya se había desfasado una vez, igual que pasó con el umbral
    /// de normalización de quaternions: dos copias del mismo número que nadie
    /// sincroniza.
    ///
    /// PIE DE LA MEDICIÓN
    /// ------------------
    /// Se usa SkinnedMeshRenderer.localBounds, NO Renderer.bounds.
    ///
    /// Renderer.bounds es el AABB *del fotograma actual*: depende de la pose
    /// animada. Medir en el Awake significaba medir a Eira en el estado en el
    /// que el Animator todavía no había evaluado nada, y el resultado cambiaba
    /// según el clip. Con una pierna recogida o un brazo levantado, el AABB
    /// crecía, la cápsula salía alta y el modelo se quedaba por encima del
    /// suelo: eso era lo que pasaba al saltar.
    ///
    /// localBounds es el AABB de la pose de referencia, y no cambia con la
    /// animación, así que el resultado es idéntico cada vez que se ejecuta.
    ///
    /// SOLO MALLAS DE PIEL
    /// -------------------
    /// Se miden únicamente los SkinnedMeshRenderer (el cuerpo). Los auras,
    /// anillos, trazas y partículas son MeshRenderer o
    /// ParticleSystemRenderer, y si entran en la medida sus AABB (mucho más
    /// grandes) empujan los pies del personaje hacia arriba. Si el modelo no
    /// tuviera ninguna malla de piel, se cae al resto de renderers.
    /// </summary>
    public static class EiraBodyFit
    {
        /// <summary>Altura mínima creíble para un cuerpo humano.</summary>
        public const float MinPlausibleHeight = 0.4f;

        /// <summary>
        /// Altura máxima creíble. Un poco por encima de capsuleMaxHeight (2.1)
        /// para no recortar el diagnóstico, pero lo bastante baja para que un
        /// modelo corrupto o con escala 100 no produzca una cápsula de 175 m.
        /// </summary>
        public const float MaxPlausibleHeight = 2.6f;

        /// <summary>
        /// Localiza el transform del cuerpo de Eira.
        ///
        /// Se busca por lo que de verdad lo identifica (una malla de piel) y
        /// no por el nombre. El nombre era "visual" en minúsculas mientras que
        /// el hijo se llama "EiraBase", y Transform.Find distingue mayúsculas,
        /// así que devolvía null en silencio.
        ///
        /// Si hay varias mallas de piel se elige la más alta, que es la del
        /// torso y no la de un arma o un accesorio.
        /// </summary>
        public static Transform FindVisual(Transform root)
        {
            if (root == null)
                return null;

            Transform named = root.Find("visual");
            if (named != null)
                return named;

            SkinnedMeshRenderer[] meshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            Transform best = null;
            float bestHeight = float.NegativeInfinity;

            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] == null)
                    continue;

                float h = meshes[i].localBounds.size.y;

                if (h <= bestHeight)
                    continue;

                bestHeight = h;
                best = meshes[i].transform;
            }

            return best;
        }

        /// <summary>
        /// Bounds del cuerpo en el espacio local de <paramref name="visual"/>.
        /// Estable entre llamadas: no depende de la pose ni del frame.
        /// </summary>
        public static bool Measure(Transform visual, out Bounds local)
        {
            local = default;

            if (visual == null)
                return false;

            if (!Encapsulate(visual, true, out local) && !Encapsulate(visual, false, out local))
                return false;

            return local.size.sqrMagnitude > 0.0001f;
        }

        /// <summary>
        /// Tamaño del cuerpo en metros de mundo, no en unidades locales.
        ///
        /// La medida sale en el espacio de 'visual'. Si el FBX se importó con
        /// una escala propia (un modelado en centímetros, por ejemplo), ahí
        /// dentro un humano puede medir 175 y en el mundo 1.75. La cápsula
        /// vive en el espacio del personaje, así que necesita metros.
        ///
        /// Por eso NO se fuerza visual.localScale = 1: esa escala es del
        /// modelo y forzar a 1 dejaría a Eira del tamaño de un edificio si el
        /// importador la necesitaba.
        /// </summary>
        public static Vector3 WorldSize(Transform visual, Bounds local)
        {
            Vector3 scale = visual.lossyScale;

            return new Vector3(
                local.size.x * scale.x,
                local.size.y * scale.y,
                local.size.z * scale.z
            );
        }

        /// <summary>
        /// Altura del cuerpo en metros, o 0 si la medida no es creíble.
        ///
        /// Un cuerpo humano mide entre 0.4 y 2.6 m. Si la medida se sale de
        /// ese rango es que el modelo o la jerarquía están mal, y encajar la
        /// cápsula con ella dejaría a Eira del tamaño de un edificio o
        /// invisible. Mejor no tocar nada y avisar.
        /// </summary>
        public static float PlausibleHeight(Transform visual, Bounds local)
        {
            float h = WorldSize(visual, local).y;

            if (float.IsNaN(h) || float.IsInfinity(h))
                return 0f;

            return h >= MinPlausibleHeight && h <= MaxPlausibleHeight ? h : 0f;
        }

        /// <summary>
        /// Acumula los bounds de los renderers indicados.
        /// </summary>
        /// <param name="skinnedOnly">
        /// true para medir solo el cuerpo (mallas de piel), false para medir
        /// cualquier renderer.
        /// </param>
        static bool Encapsulate(Transform visual, bool skinnedOnly, out Bounds local)
        {
            local = default;
            bool any = false;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];

                if (r == null || !r.enabled)
                    continue;

                bool isSkinned = r is SkinnedMeshRenderer;

                if (skinnedOnly && !isSkinned)
                    continue;

                if (!skinnedOnly && isSkinned)
                    continue;

                // En una malla de piel, localBounds está en el espacio local
                // del renderer y no se mueve con la animación. En el resto,
                // bounds viene en espacio de mundo.
                bool localSpace = isSkinned;
                Bounds box = isSkinned
                    ? ((SkinnedMeshRenderer)r).localBounds
                    : r.bounds;

                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = new Vector3(
                        (c & 1) == 0 ? box.min.x : box.max.x,
                        (c & 2) == 0 ? box.min.y : box.max.y,
                        (c & 4) == 0 ? box.min.z : box.max.z
                    );

                    Vector3 world = localSpace ? r.transform.TransformPoint(corner) : corner;
                    Vector3 lp = visual.InverseTransformPoint(world);

                    if (!any)
                    {
                        local = new Bounds(lp, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        local.Encapsulate(lp);
                    }
                }
            }

            return any;
        }

        /// <summary>
        /// Coloca el modelo con los pies en y = 0 y el cuerpo centrado en XZ,
        /// y deriva la cápsula de esa misma medida.
        /// </summary>
        /// <param name="radiusFactor">Radio como fracción del ancho medido.</param>
        /// <param name="footMargin">Margen entre los pies y el suelo de la cápsula.</param>
        /// <param name="maxHeight">Tope de altura de la cápsula.</param>
        /// <param name="alignVisual">Alinear el modelo a la cápsula.</param>
        /// <param name="fitCapsule">Ajustar la cápsula al modelo.</param>
        /// <returns>false si la medida no era creíble y no se ha tocado nada.</returns>
        public static bool Apply(
            Transform visual,
            CharacterController cc,
            float radiusFactor,
            float footMargin,
            float maxHeight,
            bool alignVisual,
            bool fitCapsule)
        {
            if (visual == null)
                return false;

            if (!Measure(visual, out Bounds local))
                return false;

            if (PlausibleHeight(visual, local) <= 0f)
                return false;

            if (alignVisual)
            {
                // El suelo de la cápsula es y = 0 porque el transform de Eira
                // está a los pies. min.y del AABB de la pose de referencia es
                // la suela, así que basta con restarlo.
                //
                // El desplazamiento va en unidades locales de 'visual' porque
                // es lo que toma localPosition, y localRotation/Scale no se
                // tocan: son del modelo.
                visual.localPosition = new Vector3(
                    -local.center.x,
                    -local.min.y,
                    -local.center.z
                );

                visual.localRotation = Quaternion.identity;
            }

            if (!fitCapsule || cc == null)
                return true;

            // Todo en metros: la cápsula vive en el espacio del personaje.
            Vector3 size = WorldSize(visual, local);

            float modelHeight = size.y + Mathf.Max(0f, footMargin);
            float modelWidth = Mathf.Max(size.x, size.z);

            float height = Mathf.Min(modelHeight, maxHeight);

            // Radio corto a propósito: sirve para no engancharse en los huecos.
            // El ancho real de los hombros es mayor que el de una cápsula.
            float radius = Mathf.Clamp(
                Mathf.Min(modelWidth * 0.5f * radiusFactor, height * 0.5f),
                0.15f,
                0.36f
            );

            cc.height = height;
            cc.radius = radius;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
            cc.stepOffset = Mathf.Min(cc.stepOffset, height * 0.45f);

            if (cc.slopeLimit < 30f)
                cc.slopeLimit = 45f;

            return true;
        }
    }
}
