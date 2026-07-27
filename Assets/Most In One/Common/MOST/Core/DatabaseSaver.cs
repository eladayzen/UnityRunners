using UnityEngine;
using UnityEngine.Scripting;

namespace Solo.MOST_IN_ONE
{
    [Preserve]
    public class DatabaseSaver : MonoBehaviour
    {
        static DatabaseSaver _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (_instance) return;
            var go = new GameObject("MOST_Database_DelayedSaver");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<DatabaseSaver>();
        }

        void Update()
        {
            float now = Time.unscaledTime;
            foreach (var db in MOST_Database.GetLoadedDatabases())
            {
                if (db == null) continue;
                db.TickDelayedSave(now);
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            FlushAll();
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) return;
            FlushAll();
        }

        void OnApplicationQuit()
        {
            FlushAll();
        }

        void FlushAll()
        {
            foreach (var db in MOST_Database.GetLoadedDatabases())
            {
                if (db == null) continue;
                db.Save();
            }
        }
    }
}
