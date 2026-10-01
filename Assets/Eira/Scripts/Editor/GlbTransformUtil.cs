using UnityEngine;

namespace EiraGame.Editor
{
    /// <summary>
    /// Utilidades de rotación para los importadores GLB.
    ///
    /// glTF exige que 'rotation' sea un quaternion normalizado, pero los
    /// exportadores reales escriben floats truncados: un quaternion con módulo
    /// 0.9987 o 1.0004. Unity lo guarda tal cual (new Quaternion no
    /// normaliza) y cualquier llamada a ToEulerAngles/eulerAngles lanza
    /// "QuaternionToEuler: Input quaternion was not normalized".
    /// </summary>
    public static class GlbTransformUtil
    {
        /// <summary>
        /// Tolerancia de normalización.
        ///
        /// OJO: este valor tiene que ser MÁS ESTRECHO que el umbral con el que
        /// Unity avisa, no al revés. El motor compara |sqrt(módulo) - 1| contra
        /// 1e-5, es decir, unos 2e-5 sobre el módulo. Con el 1e-4 que usaba
        /// antes, un quaternion de módulo 1.000042 (truncado a 5 decimales al
        /// serializar) pasaba el filtro como "bueno" y el Inspector seguía
        /// lanzando el aviso en cada redibujado.
        ///
        /// 1e-6 es unas 20 veces más estricto que el motor, así que cualquier
        /// valor que Unity considere mal normalizado entra aquí.
        /// </summary>
        public const float Epsilon = 1e-6f;

        /// <summary>
        /// Comprueba si Unity consideraría este quaternion "no normalizado".
        /// Devuelve true también para NaN, infinito y módulo ~0, que son
        /// errores más graves y hacen que ToEulerAngles devuelva basura.
        /// </summary>
        public static bool IsNormalized(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (float.IsNaN(mag) || float.IsInfinity(mag)) return false;
            return Mathf.Abs(mag - 1f) <= Epsilon;
        }

        /// <summary>
        /// Normaliza un quaternion, devolviendo identity si es degenerado
        /// (módulo ~0, NaN o infinito).
        /// </summary>
        public static Quaternion NormalizeSafe(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (float.IsNaN(mag) || float.IsInfinity(mag) || mag < Epsilon)
                return Quaternion.identity;
            if (Mathf.Abs(mag - 1f) < Epsilon)
                return q;
            float inv = 1f / mag;
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }

        /// <summary>
        /// Construye un quaternion desde las componentes x,y,z,w de glTF
        /// (que llegan como float[] en el nodo), normalizado y validado.
        /// </summary>
        public static Quaternion FromGltf(float[] xyzw)
        {
            if (xyzw == null || xyzw.Length < 4) return Quaternion.identity;
            return NormalizeSafe(new Quaternion(xyzw[0], xyzw[1], xyzw[2], xyzw[3]));
        }

        /// <summary>
        /// Extrae la rotación de una matriz glTF. Primero separa la escala de
        /// cada columna (una matriz con escala no uniforme tiene shear y
        /// m.rotation devuelve un quaternion no normalizado), y después
        /// normaliza el resultado.
        /// </summary>
        public static Quaternion RotationFromMatrix(Matrix4x4 m)
        {
            Vector3 c0 = m.GetColumn(0);
            Vector3 c1 = m.GetColumn(1);
            Vector3 c2 = m.GetColumn(2);

            float sx = c0.magnitude;
            float sy = c1.magnitude;
            float sz = c2.magnitude;
            if (sx > Epsilon) c0 /= sx;
            if (sy > Epsilon) c1 /= sy;
            if (sz > Epsilon) c2 /= sz;

            if (sx < Epsilon || sy < Epsilon || sz < Epsilon) return Quaternion.identity;

            // Gram-Schmidt: garantiza base ortonormal aunque la original
            // tuviera shear residual.
            Vector3 x = c0.normalized;
            Vector3 y = c1 - x * Vector3.Dot(x, c1);
            if (y.sqrMagnitude < Epsilon * Epsilon) return Quaternion.identity;
            y.Normalize();
            Vector3 z = Vector3.Cross(x, y);

            var basis = new Matrix4x4();
            basis.SetColumn(0, x);
            basis.SetColumn(1, y);
            basis.SetColumn(2, z);
            basis.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));

            return NormalizeSafe(basis.rotation);
        }

        /// <summary>
        /// Normaliza la rotación local de un transform si no lo está, y
        /// devuelve true si hubo que corregirla.
        /// </summary>
        public static bool Fix(Transform t)
        {
            if (t == null) return false;
            var q = t.localRotation;
            if (IsNormalized(q)) return false;
            t.localRotation = NormalizeSafe(q);
            return true;
        }
    }
}
