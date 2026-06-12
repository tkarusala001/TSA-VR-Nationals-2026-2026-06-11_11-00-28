// -----------------------------------------------------------------------------
//  Singleton.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Generic, scene-persistent MonoBehaviour singleton base class.
//  All long-lived managers (GameManager, AudioManager, PerformanceManager, etc.)
//  derive from this so we get a single, globally reachable instance without
//  scattering FindObjectOfType calls through gameplay code.
//
//  Design notes:
//   * The instance is resolved lazily but a manager placed in the boot scene
//     will register itself in Awake(), which is the normal path.
//   * Duplicate instances (e.g. a manager that survives an additive scene load
//     and then meets a second copy) are destroyed defensively.
//   * Set _persistAcrossScenes = true for managers that must outlive scene
//     loads. Our museum runs as a single scene with additive room *activation*
//     rather than scene loads, so persistence is mostly a safety net.
// -----------------------------------------------------------------------------

using UnityEngine;

namespace Decrypted.Util
{
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T _instance;
        private static bool _isQuitting;
        private static readonly object _lock = new object();

        [Header("Singleton")]
        [Tooltip("If true this object is moved to the root and kept alive across scene loads.")]
        [SerializeField] private bool _persistAcrossScenes = true;

        /// <summary>Globally reachable instance. Returns null during application quit.</summary>
        public static T Instance
        {
            get
            {
                if (_isQuitting) return null;
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<T>();
                    }
                    return _instance;
                }
            }
        }

        /// <summary>True when an instance exists and the app is not quitting.</summary>
        public static bool Exists => _instance != null && !_isQuitting;

        protected virtual void Awake()
        {
            lock (_lock)
            {
                if (_instance != null && _instance != this)
                {
                    // A second copy showed up; the first one wins.
                    Destroy(gameObject);
                    return;
                }

                _instance = (T)this;

                if (_persistAcrossScenes)
                {
                    // DontDestroyOnLoad only works on root objects.
                    if (transform.parent != null) transform.SetParent(null);
                    DontDestroyOnLoad(gameObject);
                }
            }

            OnSingletonAwake();
        }

        /// <summary>Override instead of Awake() in subclasses (call order is guaranteed).</summary>
        protected virtual void OnSingletonAwake() { }

        protected virtual void OnApplicationQuit() => _isQuitting = true;

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
