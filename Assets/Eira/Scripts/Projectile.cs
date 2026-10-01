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
        Light glow;

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
            if (Physics.SphereCast(
                    from,
                    Radius,
                    velocity.normalized,
                    out RaycastHit hit,
                    travel,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                Collider target = FindDrone(hit.collider);

                if (target != null)
                {
                    DroneController drone =
                        target.GetComponent<DroneController>();

                    if (drone != null)
                    {
                        CombatFX.Impact(hit.point, Color.white);
                        drone.OnShot(damage, velocity.normalized, owner);
                        Expire();
                        return;
                    }
                }

                // Muro: la bala no lo atraviesa.
                CombatFX.Impact(hit.point, Color.Lerp(Color.white, Color.gray, 0.5f));
                Expire();
                return;
            }

            transform.position = to;

            // Orienta la estela en la dirección del vuelo.
            if (trail != null)
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        }

        /// <summary>
        /// El SphereCast suele dar la pared, no el drón, porque el drón está
        /// detrás. Se busca en un SphereOverlap del punto de impacto.
        /// </summary>
        Collider FindDrone(Collider first)
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

                if (d == null || d.gameObject == owner)
                    continue;

                float dist = (overlaps[i].bounds.center - transform.position).sqrMagnitude;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = overlaps[i];
                }
            }

            return best;
        }

        void Expire()
        {
            if (trail != null)
                trail.emitting = false;

            Destroy(gameObject);
        }
    }
}
