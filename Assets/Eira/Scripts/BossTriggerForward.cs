using UnityEngine;

namespace EiraGame
{
    // Reenvía eventos de trigger de un collider hijo al guardián (para activar el jefe
    // con un área de detección más grande que el sólido del cuerpo).
    public class BossTriggerForward : MonoBehaviour
    {
        public GuardianBoss boss;

        void OnTriggerEnter(Collider other)
        {
            if (boss == null) return;
            if (other.GetComponent<PlayerController>() != null)
                boss.Activate();
        }
    }
}