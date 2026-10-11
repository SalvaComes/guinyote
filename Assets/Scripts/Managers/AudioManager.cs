using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuinoteApp.Managers
{
    /// <summary>Eventos de juego que tienen un efecto de sonido asociado.</summary>
    public enum TipoSonido
    {
        CartaRobar,
        CartaLanzar,
        Cante40,
        Cante20,
        Victoria,
        Derrota,
        UltimasBaza
    }

    /// <summary>Clip(s) y volumen de un <see cref="TipoSonido"/>, configurables desde el Inspector.</summary>
    [Serializable]
    public class SonidoConfig
    {
        public TipoSonido tipo;

        [Tooltip("Si hay varios clips se elige uno al azar en cada reproduccion (da variedad).")]
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volumen = 1f;
    }

    /// <summary>
    /// Reproduce los efectos de sonido del juego. Singleton que sobrevive a los cambios de escena.
    /// Uso: <c>AudioManager.Reproducir(TipoSonido.CartaLanzar)</c> (no falla si no hay AudioManager
    /// en la escena) o <c>AudioManager.Instancia.ReproducirSonido(...)</c>.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instancia { get; private set; }

        [SerializeField] private List<SonidoConfig> sonidos = new List<SonidoConfig>();
        [SerializeField, Range(0f, 1f)] private float volumenGeneral = 1f;
        [SerializeField] private bool silenciado;

        private AudioSource fuente;
        private readonly Dictionary<TipoSonido, SonidoConfig> porTipo = new Dictionary<TipoSonido, SonidoConfig>();

        public float VolumenGeneral
        {
            get => volumenGeneral;
            set => volumenGeneral = Mathf.Clamp01(value);
        }

        public bool Silenciado
        {
            get => silenciado;
            set => silenciado = value;
        }

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            transform.SetParent(null); // DontDestroyOnLoad exige un objeto raiz
            DontDestroyOnLoad(gameObject);

            fuente = gameObject.AddComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0f; // juego 2D

            ReconstruirIndice();
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        /// <summary>Reproduce el efecto asociado al evento. Si no hay clip configurado, no hace nada.</summary>
        public void ReproducirSonido(TipoSonido tipo)
        {
            if (silenciado || fuente == null) return;

            if (!porTipo.TryGetValue(tipo, out SonidoConfig config) || config.clips == null || config.clips.Length == 0)
            {
                Debug.LogWarning($"[AudioManager] Sin clip para {tipo}.");
                return;
            }

            AudioClip clip = config.clips[UnityEngine.Random.Range(0, config.clips.Length)];
            if (clip == null) return;

            // PlayOneShot permite que se solapen varios sonidos (p. ej. lanzar carta + cante)
            fuente.PlayOneShot(clip, config.volumen * volumenGeneral);
        }

        /// <summary>Atajo estatico seguro: ignora la llamada si aun no existe el AudioManager.</summary>
        public static void Reproducir(TipoSonido tipo)
        {
            if (Instancia != null) Instancia.ReproducirSonido(tipo);
        }

        private void ReconstruirIndice()
        {
            porTipo.Clear();
            foreach (SonidoConfig s in sonidos)
                porTipo[s.tipo] = s; // si un tipo esta repetido gana el ultimo
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Instancia == this) ReconstruirIndice();
        }
#endif
    }
}
