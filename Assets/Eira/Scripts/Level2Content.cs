using UnityEngine;

namespace EiraGame
{
    // Misiones del Nivel 2 "La caza". Ocupan los 6 slots de GameManager.missionsDone.
    public static class Level2Missions
    {
        public const int MEscapeCity = 0;
        public const int MFindRebels = 1;
        public const int MTechPieces = 2;
        public const int MProjectFiles = 3;
        public const int MAvoidPatrols = 4;
        public const int MRefuge = 5;

        public const int RebelsNeeded = 4;
        public const int TechPiecesNeeded = 3;
        public const int ProjectFilesNeeded = 3;

        public static readonly string[] Names =
        {
            "Escapa de la ciudad destruida",
            "Encuentra y libera a los rebeldes [" + RebelsNeeded + "]",
            "Recupera piezas tecnologicas [" + TechPiecesNeeded + "]",
            "Descubre los archivos del Proyecto Eira [" + ProjectFilesNeeded + "]",
            "Evita las patrullas de androides",
            "Llega al refugio rebelde y encuentra el Nucleo Central",
        };
    }

    // Tabla de puntos del Nivel 2.
    public static class Level2Points
    {
        public const int Android = 100;
        public const int Rebel = 200;
        public const int Hack = 150;
        public const int File = 200;
        public const int SideQuest = 250;
        public const int LevelComplete = 1000;
    }
}
