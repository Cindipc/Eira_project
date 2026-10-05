using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Proyectil de Eira. Se mueve a mano (no con Rigidbody) para que el
    /// comportamiento sea idéntico en cualquier frame rate.
    ///
    /// Lo importante: entre el frame anterior y este puede recorrer más de un
    /// metro, así que un simple Trigger o un CheckSphere en el destino
    /// dejaría pasar drones en medio. Por eso barre el segmento recorrido
    /// con un SphereCast y solo entonces avanza.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        /// <summary>Velocidad en m/s. Suficiente para recorrer el pasillo entero.</summary>
        const float Speed = 42f;

        /// <summary>Radio de barrido. Algo mayor que el dron para que impacte limpio.</summary>
        const float Radius = 0.28f;

        /// <summary>Vida máxima: si no impacta, se desvanece sola.</summary>
        const float Life = 3.5f;

        float damage = 12f;
        float life = Life;
        Vector3 velocity;
        TrailRenderer trail;
        Transform owner;
        Transform ownerRoot;
        Light glow;

        readonly RaycastHit[] sweep = new RaycastHit[24];

        public static Projectile Fire(
            Vector3 position,
            Vector3 direction,
            float damage,
            Transform owner,
            Color color)
        {
            var go = new GameObject("Projectile");
            go.transform.position = position;

            var p = go.AddComponent<Projectile>();
            p.damage = damage;
            p.owner = owner;
            p.ownerRoot = ResolveOwnerRoot(owner);
            p.velocity = direction.normalized * Speed;

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Visual";
            orb.transform.SetParent(go.transform, false);
            orb.transform.localScale = Vector3.one * 0.18f;

            var col = orb.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            var renderer = orb.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CombatFX.GlowMaterial(color, 6f);

            // La luz hace que el disparo se vea en un corredor oscuro, que es
            // justo el problema que tenía el nivel.
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 6f;
            l.intensity = 2.2f;
            l.shadows = LightShadows.None;
            p.glow = l;

            var t = go.AddComponent<TrailRenderer>();
            t.time = 0.12f;
            t.startWidth = 0.16f;
            t.endWidth = 0f;
            t.material = CombatFX.GlowMaterial(color, 4f);
            t.emitting = true;
            p.trail = t;

            return p;
        }

        /// <summary>
        /// Raiz de quien dispara. La bola nace junto al pecho de Eira, dentro
        /// de su propia capsula, asi que sin esto el primer impacto del
        /// barrido seria Eira misma y la bola se desvaneceria en el aire.
        /// </summary>
        static Transform ResolveOwnerRoot(Transform owner)
        {
            if (owner == null)
                return null;

            PlayerController pc = owner.GetComponentInParent<PlayerController>();

            if (pc != null)
                return pc.transform;

            NovaCompanion nova = owner.GetComponentInParent<NovaCompanion>();

            if (nova != null)
                return nova.transform;

            return owner;
        }

        bool BelongsToOwner(Transform hit)
        {
            if (hit == null)
                return true;

            if (ownerRoot != null &&
                (hit == ownerRoot || hit.IsChildOf(ownerRoot)))
            {
                return true;
            }

            return hit.name.IndexOf("NOVA", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            life -= dt;

            if (life <= 0f)
            {
                Expire();
                return;
            }

            float travel = Speed * dt;
            Vector3 from = transform.position;
            Vector3 to = from + velocity * dt;

            // Barrido del segmento: detecta drones y muros en el camino.
            // Se recoge toda la lista y se queda con el impacto mas cercano
            // que no sea de quien dispara, porque SphereCast no garantiza el
            // orden y el primero puede ser el cuerpo de Eira.
            int count = Physics.SphereCastNonAlloc(
                from,
                Radius,
                velocity.normalized,
                sweep,
                travel,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            RaycastHit closest = default;
            bool hasHit = false;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (BelongsToOwner(sweep[i].transform))
                    continue;

                if (sweep[i].distance >= closestDistance)
                    continue;

                closestDistance = sweep[i].distance;
                closest = sweep[i];
                hasHit = true;
            }

            if (hasHit)
            {
                Collider target = FindDrone(closest.collider, closest.distance);

                if (target != null)
                {
                    DroneController drone =
                        target.GetComponent<DroneController>();

                    if (drone != null)
                    {
                        CombatFX.Impact(closest.point, Color.white);

                        // El arma de Eira derriba un androide de un disparo.
                        // El damage del Inspector (12) se queda corto frente a
                        // la vida del dron (40), asi que el impacto se lleva
                        // toda la vida que le quede: un disparo, un dron.
                        float lethal = Mathf.Max(damage, drone.maxHealth);

                        drone.OnShot(lethal, velocity.normalized, owner);
                        Expire();
                        return;
                    }
                }

                // Muro: la bala no lo atraviesa.
                CombatFX.Impact(closest.point, Color.Lerp(Color.white, Color.gray, 0.5f));
                Expire();
                return;
            }

            transform.position = to;

            // Orienta la estela en la dirección del vuelo.
            if (trail != null)
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        }

        /// <summary>
        /// Localiza el dron que hay que dañar. Si el impacto del barrido ya es
        /// un dron, ese es el. Si no, se busca a uno en un SphereOverlap por
        /// delante, pero solo si estaba ANTES que el muro: con los drones ya
        /// siendo detectables, sin este filtro una bala que se estrella contra
        /// una pared podría matar a un androide situado detrás de ella.
        /// </summary>
        Collider FindDrone(Collider first, float wallDistance)
        {
            if (first != null && first.GetComponentInParent<DroneController>() != null)
                return first;

            var overlaps = Physics.OverlapSphere(
                transform.position + velocity.normalized * 0.5f,
                Radius + 0.45f,
                ~0,
                QueryTriggerInteraction.Ignore);

            Collider best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < overlaps.Length; i++)
            {
                var d = overlaps[i].GetComponentInParent<DroneController>();

                if (d == null || BelongsToOwner(d.transform))
                    continue;

                float dist = (overlaps[i].bounds.center - transform.position).magnitude;

                // El borde cercano del dron tiene que caer antes que el muro.
                if (dist - WidestAxis(overlaps[i].bounds) > wallDistance)
                    continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = overlaps[i];
                }
            }

            return best;
        }

        /// <summary>Mayor semilado del collider: aproxima su alcance en cualquier eje.</summary>
        static float WidestAxis(Bounds bounds)
        {
            return Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
        }

        void Expire()
        {
            if (trail != null)
                trail.emitting = false;

            Destroy(gameObject);
        }
    }
}
