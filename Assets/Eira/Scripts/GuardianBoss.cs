using UnityEngine;

namespace EiraGame
{
    // Jefe del Nivel 1: la Máquina Guardiana. Bloquea la salida del laboratorio.
    // Para desactivarla, Eira debe cargar 3 terminales con su habilidad mientras esquiva sus ataques.
    public class GuardianBoss : MonoBehaviour
    {
        public int neededTerminals = 3;
        public float moveSpeed = 1.4f;
        public float attackDamage = 22f;
        public float attackRange = 3.5f;
        public float attackPeriod = 2.6f;
        public DoorControl exitDoor;
        public Transform core;
        public Transform eye;
        public Light redLight;

        int terminalsCharged;
        float stunTimer;
        float attackTimer;
        bool active;
        bool defeated;
        Vector3 startPos;

        public bool IsActive => active;

        void Start()
        {
            startPos = transform.position;
        }

        void Update()
        {
            if (defeated) return;
            if (!active) return;

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return;

            if (stunTimer > 0f)
            {
                stunTimer -= Time.deltaTime;
                Flash(true);
                return;
            }

            Flash(false);
            var toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > 1.8f)
            {
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z), moveSpeed * Time.deltaTime);
            }
            if (transform.childCount > 0)
            {
                var dir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector3.forward;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 4f * Time.deltaTime);
            }

            attackTimer -= Time.deltaTime;
            if (dist < attackRange && attackTimer <= 0f)
            {
                attackTimer = attackPeriod;
                player.TakeDamage(attackDamage, gameObject);
            }

            if (core != null) core.Rotate(0f, 90f * Time.deltaTime, 0f);
        }

        void Flash(bool tint)
        {
            if (eye == null) return;
            var r = eye.GetComponent<Renderer>();
            if (r != null)
            {
                float t = tint ? 1f : (terminalsCharged > 0 ? 0.4f : 0f);
                r.material.color = Color.Lerp(new Color(0.8f, 0.2f, 0.1f), Color.cyan, Mathf.Clamp01(t * 0.5f));
            }
            if (redLight != null) redLight.intensity = tint ? 8f : 4f;
        }

        public void Activate()
        {
            active = true;
            GameEvents.SetBossBar(1f);
            var gm = GameManager.Instance;
            if (gm != null) gm.OnBossActive();
        }

        public void OnTerminalCharged()
        {
            if (defeated) return;
            terminalsCharged++;
            stunTimer = 3.2f;
            GameEvents.SetBossBar(Mathf.Max(0f, 1f - terminalsCharged / (float)neededTerminals));
            AudioFX.Ability();
            var gm = GameManager.Instance;
            if (gm != null) gm.OnTerminalChargedProgress(terminalsCharged, neededTerminals);

            if (terminalsCharged >= neededTerminals)
                Defeat();
        }

        void Defeat()
        {
            defeated = true;
            active = false;
            AudioFX.Win();
            GameEvents.BossDefeated();
            GameEvents.SetBossBar(0f);
            var fall = transform.rotation * Quaternion.Euler(0f, 0f, 90f);
            transform.rotation = fall;
            if (redLight != null) redLight.intensity = 0f;
            var gm = GameManager.Instance;
            if (gm != null) gm.OnBossDefeated();
            if (exitDoor != null) exitDoor.Open();
        }

        void OnTriggerEnter(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null && !active && !defeated) Activate();
        }
    }
}