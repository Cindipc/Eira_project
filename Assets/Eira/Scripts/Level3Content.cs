using UnityEngine;

namespace EiraGame
{
    // Misiones del Nivel 3 "La ultima guerra" (dentro del Nucleo Central).
    public static class Level3Missions
    {
        public const int MEnterNucleo = 0;
        public const int MDefeatControlled = 1;
        public const int MDisableSecurity = 2;
        public const int MFaceKael = 3;
        public const int MReachCore = 4;
        public const int MChooseDestiny = 5;

        public const int ControlledNeeded = 6;
        public const int SecurityNeeded = 3;
        public const int RebelsNeeded = 3;
        public const int FilesNeeded = 2;

        public static readonly string[] Names =
        {
            "Entra en el Nucleo Central",
            "Derrota a los androides controlados [" + ControlledNeeded + "]",
            "Desactiva los sistemas de seguridad [" + SecurityNeeded + "]",
            "Enfrentate a Kael",
            "Llega hasta el sistema central",
            "Elige el destino de humanos y androides",
        };
    }

    // Tabla de puntos del Nivel 3.
    public static class Level3Points
    {
        public const int Enemy = 100;
        public const int Rebel = 200;
        public const int Mission = 500;
        public const int SecretFile = 300;
        public const int NoDeath = 1000;
        public const int DefeatKael = 2000;
        public const int FreeNucleo = 5000;
    }

    public enum FinalChoice { None, Humans, Androids, Freedom }
}
