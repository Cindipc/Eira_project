using UnityEngine;

namespace EiraGame
{
    // Control del personaje de Eira (tercera persona): mover, correr, saltar, agacharse,
    // interactuar con objetos y usar la habilidad genética que gasta la energía del corazón.
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 3.6f;
        public float runSpeed = 6.2f;
        public float crouchSpeed = 1.7f;
        public float jumpVelocity = 6.2f;
        public float gravity = 20f;
        public float abilityRadius = 4.6f;
        public float abilityCost = 12f;
        public float heartRegen = 1.6f;
        public float heartDanger = 15f;
        public float pulseInterval = 0.35f;

        public Transform visual;
        public Transform legL, legR, armL, armR, torso, head;
        public float hideZoneLevel;   // modificado por HideSpotZone
        public bool IsCrouching { get; private set; }
        public float Health { get; private set; }

        CharacterController cc;
        Vector3 velocity;
        float yaw;
        float phase;
        float invuln;
        float heart = EiraConst.MaxHeart;
        float pulseCooldown;
        System.Collections.Generic.List<Interactable> near = new System.Collections.Generic.List<Interactable>();

        public float DetectionMultiplier
        {
            get
            {
                if (!IsCrouching) return 1f;
                if (hideZoneLevel > 0.5f) return 0.07f;
                return 0.28f;
            }
        }

        public float HeartFrac => heart / EiraConst.MaxHeart;
        public GameState PlayerState => GameManager.Instance != null ? GameManager.Instance.State : GameState.Playing;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Health = EiraConst.MaxHealth;

            if (visual != null)
            {
                legL = visual.Find("legL");
                legR = visual.Find("legR");
                armL = visual.Find("armL");
                armR = visual.Find("armR");
                torso = visual.Find("torso");
                head = visual.Find("head");
            }
        }

        // Rebusca las partes del visual (legL/legR/armL/armR/torso/head) si aún no se
        // han cacheado, p.ej. cuando el modelo nativo se construye después de Awake.
        public void RefreshBones()
        {
            if (visual == null) return;
            if (legL == null) legL = visual.Find("legL");
            if (legR == null) legR = visual.Find("legR");
            if (armL == null) armL = visual.Find("armL");
            if (armR == null) armR = visual.Find("armR");
            if (torso == null) torso = visual.Find("torso");
            if (head == null) head = visual.Find("head");
        }

        // Desplazamientos (en espiral) para buscar un punto de apoyo libre cerca del spawn.
        static readonly Vector2[] EscapeOffsets =
        {
            Vector2.zero,
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(1.5f, 1.5f), new Vector2(-1.5f, 1.5f), new Vector2(1.5f, -1.5f), new Vector2(-1.5f, -1.5f),
            new Vector2(3f, 0f), new Vector2(-3f, 0f), new Vector2(0f, 3f), new Vector2(0f, -3f),
            new Vector2(3f, 3f), new Vector2(-3f, 3f), new Vector2(3f, -3f), new Vector2(-3f, -3f),
        };

        void Start()
        {
            LandOnFloor();
        }

        // Evita nacer atascado dentro de la geometría del escenario: baja hasta el suelo
        // y, si sigue dentro de una pared, se desplaza en espiral hasta hallar un punto libre.
        public void LandOnFloor()
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            var start = transform.position;
            cc.enabled = false;

            for (int i = 0; i < EscapeOffsets.Length; i++)
            {
                var p = start + new Vector3(EscapeOffsets[i].x, 0f, EscapeOffsets[i].y);
                if (!DropToTopSurface(ref p)) continue;
                transform.position = p;
                if (!OverlapsSolid(p))
                {
                    cc.enabled = true;
                    return;
                }
            }

            // plan B: subir hasta salir de la pared y apoyarse arriba (podrá saltar al suelo)
            for (int i = 0; i < 30 && OverlapsSolid(transform.position); i++)
                transform.position += Vector3.up * 0.4f;
            var pos = transform.position;
            if (DropToTopSurface(ref pos)) transform.position = pos;
            cc.enabled = true;
        }

        bool DropToTopSurface(ref Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 25f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                p = new Vector3(p.x, hit.point.y + 0.03f, p.z);
                return true;
            }
            return false;
        }

        bool OverlapsSolid(Vector3 p)
        {
            float r = Mathf.Max(0.05f, cc.radius * 0.9f);
            float halfH = Mathf.Max(0.05f, cc.height * 0.5f - cc.radius);
            var c = p + cc.center;
            foreach (var col in Physics.OverlapCapsule(c - Vector3.up * halfH, c + Vector3.up * halfH, r))
            {
                if (col == null || col.isTrigger) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                return true;
            }
            return false;
        }

        public void RespawnAt(Vector3 pos, Quaternion rot)
        {
            cc.enabled = false;
            transform.position = pos;
            transform.rotation = rot;
            velocity = Vector3.zero;
            cc.enabled = true;
            Health = EiraConst.MaxHealth;
            invuln = 2.2f;
            heart = Mathf.Max(heart, EiraConst.MaxHeart * 0.5f);
            LandOnFloor();
        }

        void Update()
        {
            RefreshBones();
            invuln = Mathf.Max(0f, invuln - Time.deltaTime);

            if (PlayerState == GameState.Playing)
            {
                TickMovement();
                TickAbilities();
            }
            else
            {
                velocity.y = Mathf.Max(velocity.y - gravity * Time.deltaTime, -40f);
                cc.Move(velocity * Time.deltaTime);
            }
        }

        void TickMovement()
        {
            bool grounded = cc.isGrounded;
            var input = EiraInput.MoveAxis();
            bool sprint = EiraInput.Sprint() && !IsCrouching;
            bool crouch = EiraInput.CrouchHeld();

            if (crouch != IsCrouching)
            {
                IsCrouching = crouch;
                ApplyCrouch();
            }

            // rotación de cámara (yaw) se comparte con FollowCamera
            var cam = GameManager.Instance != null ? GameManager.Instance.CameraYaw : 0f;
            yaw = cam;

            var moveDir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
            float speed = IsCrouching ? crouchSpeed : (sprint ? runSpeed : walkSpeed);
            var targetVel = moveDir * speed;

            Vector3 mv = cc.velocity;
            mv.x = Mathf.MoveTowards(mv.x, targetVel.x, 30f * Time.deltaTime);
            mv.z = Mathf.MoveTowards(mv.z, targetVel.z, 30f * Time.deltaTime);

            if (grounded && EiraInput.JumpDown() && !IsCrouching)
                velocity.y = jumpVelocity;
            if (!grounded)
                velocity.y = Mathf.Max(velocity.y - gravity * Time.deltaTime, -50f);
            else
                velocity.y = mv.y = -1f;

            var finalVel = new Vector3(mv.x, velocity.y, mv.z);
            cc.Move(finalVel * Time.deltaTime);

            // orientar el modelo según la dirección de movimiento (solo yaw)
            if (input.magnitude > 0.05f)
            {
                var fwd = Quaternion.Euler(0f, Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, fwd, 12f * Time.deltaTime);
            }

            // puntos de daño por caída
            if (transform.position.y < -3f)
            {
                TakeDamage(20f, null);
                var gm = GameManager.Instance;
                if (gm != null) gm.RespawnPlayer();
            }

            Animate(input.magnitude, grounded);
        }

        void ApplyCrouch()
        {
            float targetH = IsCrouching ? 1.06f : 1.75f;
            float targetCenter = IsCrouching ? 0.53f : 0.875f;
            cc.height = targetH;
            cc.center = new Vector3(0f, targetCenter, 0f);
            if (visual != null)
                visual.localScale = Vector3.Lerp(visual.localScale, IsCrouching ? new Vector3(1f, 0.6f, 1f) : Vector3.one, 0.25f);
        }

        void Animate(float inputMag, bool grounded)
        {
            if (visual == null) return;
            float speed = Mathf.Clamp01(cc.velocity.magnitude / runSpeed);
            if (grounded && speed > 0.02f)
                phase += Time.deltaTime * (4f + speed * 6f);
            float amp = grounded ? speed * 45f : 10f;

            if (legL != null) legL.localRotation = Quaternion.Euler(-amp * Mathf.Sin(phase), 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(amp * Mathf.Sin(phase), 0f, 0f);
            if (armL != null) armL.localRotation = Quaternion.Euler(amp * Mathf.Sin(phase), 0f, 0f);
            if (armR != null) armR.localRotation = Quaternion.Euler(-amp * Mathf.Sin(phase), 0f, 0f);

            float lean = grounded ? speed * 6f : 0f;
            if (torso != null) torso.localRotation = Quaternion.Euler(lean, 0f, 0f);
            if (head != null) head.localRotation = Quaternion.Euler(-lean * 0.5f, 0f, 0f);
        }

        void TickAbilities()
        {
            // regeneración del corazón
            heart = Mathf.Min(EiraConst.MaxHeart, heart + heartRegen * Time.deltaTime);
            if (heart < heartDanger)
            {
                GameEvents.HeartDanger();
                AudioFX.Heart();
            }

            pulseCooldown = Mathf.Max(0f, pulseCooldown - Time.deltaTime);
            if (EiraInput.AbilityDown())
                UseAbility();

            if (EiraInput.InteractDown())
                TryInteract();

            // prompt del objeto más cercano
            string prompt = null;
            Interactable best = null;
            float bestD = float.MaxValue;
            for (int i = near.Count - 1; i >= 0; i--)
            {
                if (near[i] == null) { near.RemoveAt(i); continue; }
                if (!near[i].CanInteract(this)) continue;
                float d = (near[i].transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = near[i]; }
            }
            if (best != null) prompt = best.Prompt;
            GameEvents.InteractPrompt(prompt);
        }

        // Habilidad genética: pulso que activa consolas cercanas gastando energía del corazón.
        void UseAbility()
        {
            if (pulseCooldown > 0f || heart < 1f) return;
            if (heart < abilityCost || heart < heartDanger * 0.8f)
            {
                GameEvents.HeartDanger();
                AudioFX.Heart();
                return;
            }

            pulseCooldown = pulseInterval;
            heart -= abilityCost;
            AudioFX.Ability();
            GameEvents.AbilityUsed();

            var colls = Physics.OverlapSphere(transform.position + Vector3.up * 1f, abilityRadius);
            foreach (var c in colls)
            {
                var it = c.GetComponent<Interactable>();
                if (it != null) it.OnAbilityPulse(this);
            }

            // defensa: el pulso aleja a los androides (drones) cercanos
            bool repelled = false;
            var pulseCenter = transform.position + Vector3.up * 1f;
            var drones = UnityEngine.Object.FindObjectsOfType<DroneController>();
            foreach (var drone in drones)
            {
                if (drone == null) continue;
                if (Vector3.Distance(pulseCenter, drone.transform.position) <= abilityRadius)
                {
                    drone.OnDefensePulse(transform.position);
                    repelled = true;
                }
            }
            if (repelled)
            {
                GameManager.Instance?.AddScore(Points.AvoidEnemy);
                GameEvents.Subtitle("Pulso defensivo: dron repelido. +" + Points.AvoidEnemy, 1.8f);
            }
        }

        void TryInteract()
        {
            Interactable best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < near.Count; i++)
            {
                if (near[i] == null || !near[i].CanInteract(this)) continue;
                float d = (near[i].transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = near[i]; }
            }
            if (best != null) best.OnInteract(this);
        }

        public void TakeDamage(float amount, GameObject attacker)
        {
            if (PlayerState != GameState.Playing) return;
            if (invuln > 0f)
            {
                amount *= 0f;
            }
            Health -= amount;
            invuln = Mathf.Max(invuln, 0.9f);
            if (amount > 0f)
            {
                AudioFX.Hurt();
                GameManager.Instance?.OnPlayerHit();
            }
            if (Health <= 0f)
            {
                GameManager.Instance?.OnPlayerDied();
            }
        }

        public void Heal(float amount)
        {
            Health = Mathf.Clamp(Health + amount, 0f, EiraConst.MaxHealth);
            GameManager.Instance?.RefreshPlayerHud();
        }

        public void RestoreHeart(float amount)
        {
            heart = Mathf.Min(EiraConst.MaxHeart, heart + amount);
            GameManager.Instance?.RefreshPlayerHud();
        }

        public void DeregisterInteractable(Interactable it)
        {
            near.Remove(it);
        }

        void OnTriggerEnter(Collider other)
        {
            var it = other.GetComponent<Interactable>();
            if (it != null) near.Add(it);
        }

        void OnTriggerExit(Collider other)
        {
            var it = other.GetComponent<Interactable>();
            if (it != null) near.Remove(it);
        }
    }
}