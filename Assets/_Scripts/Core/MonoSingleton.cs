using UnityEngine;

namespace CampusRun.Core
{
    /// <summary>
    /// 게임 전역에서 유일성을 보장하고 씬 전환 시 파괴되지 않는 제네릭 싱글톤 베이스 클래스입니다.
    /// GameManager, UIManager, SoundManager 등 전역 매니저 클래스에 상속하여 사용합니다.
    /// </summary>
    /// <typeparam name="T">MonoBehaviour 파생 매니저 클래스 타입</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isShuttingDown = false;

        public static T Instance
        {
            get
            {
                if (_isShuttingDown)
                {
                    Debug.LogWarning($"[MonoSingleton] 어플리케이션 종료 중 인스턴스 접근 감지: {typeof(T).Name}");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindAnyObjectByType<T>();
                        if (_instance == null)
                        {
                            GameObject singletonObject = new GameObject(typeof(T).Name);
                            _instance = singletonObject.AddComponent<T>();
                        }
                    }
                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"[MonoSingleton] 중복 인스턴스 감지되어 제거됨: {gameObject.name}");
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
