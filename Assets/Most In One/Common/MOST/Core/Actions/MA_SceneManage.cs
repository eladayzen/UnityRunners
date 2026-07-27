using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Solo.MOST_IN_ONE
{
    [Serializable]
    public class MOSTAction_SceneManage : MOST_ActionCore
    {
        public MOSTAction_SceneManage() { ActionName = "Scene Manage"; }

        // Runtime-only: UnityEngine.SceneManagement operations only.
        // Inspector attributes are kept for readability and operation-specific visibility.

        public enum SceneManageOperation
        {
            LoadScene,
            LoadSceneAsync,
            ReloadActiveScene,
            ReloadActiveSceneAsync,
            AllowPendingAsyncSceneActivation,

            UnloadSceneAsync,
            UnloadActiveSceneAsync,
            UnloadAllLoadedScenesExceptTarget,
            UnloadUnusedAssets,

            CreateScene,
            SetActiveScene,
            MoveGameObjectToScene,
            MoveGameObjectsToScene,
            MergeScenes,
            DontDestroyOnLoad,

            QueryActiveScene,
            QueryTargetScene,
            QuerySourceScene,
            QueryDestinationScene,
            QueryAllLoadedScenes,
            GetBuildIndexByScenePath,
            GetScenePathByBuildIndex,
            GetRuntimeSceneCount,
            GetRuntimeLoadedSceneCount,
            GetBuildSettingsSceneCount,
            GetSceneAtLoadedIndex,
            GetSceneByName,
            GetSceneByPath,
            GetSceneByBuildIndex,
            GetTargetSceneRootGameObjects,
            GetTargetSceneRootCount,
            GetTargetSceneHandle,
            GetTargetSceneIsValid,
            GetTargetSceneIsLoaded,
            GetTargetSceneIsDirty,
            GetTargetSceneIsSubScene
        }

        public enum SceneIdMode
        {
            Active,
            NameOrPath,
            Name,
            Path,
            BuildIndex,
            LoadedSceneIndex
        }

        private enum AsyncPurpose
        {
            None,
            Load,
            Unload,
            UnloadUnusedAssets,
        }

        [Serializable]
        public struct SceneLocator
        {
            [Tooltip("How this scene should be found. Active uses the current active scene; NameOrPath is best for loading scenes.")]
            public SceneIdMode mode;

            [HideIfAll("mode", SceneIdMode.NameOrPath, false, "mode", SceneIdMode.Name, false)]
            [Tooltip("Scene name, scene path, or either. For loading, this can be a Build Settings name or an Assets/.../.unity path.")]
            public string nameOrPath;

            [HideIfAll("mode", SceneIdMode.Path, false)]
            [Tooltip("Exact scene asset path, for example Assets/Scenes/Game.unity.")]
            public string path;

            [HideIfAll("mode", SceneIdMode.BuildIndex, false)]
            [Tooltip("Scene index from Build Settings.")]
            public int buildIndex;

            [HideIfAll("mode", SceneIdMode.LoadedSceneIndex, false)]
            [Tooltip("Index inside the currently managed/loaded scenes list.")]
            public int loadedSceneIndex;

            public static SceneLocator Active()
            {
                return new SceneLocator { mode = SceneIdMode.Active };
            }

            public static SceneLocator ByNameOrPath(string value)
            {
                return new SceneLocator { mode = SceneIdMode.NameOrPath, nameOrPath = value };
            }
        }

        [Serializable] public class StringEvent : UnityEvent<string> { }
        [Serializable] public class IntEvent : UnityEvent<int> { }
        [Serializable] public class FloatEvent : UnityEvent<float> { }
        [Serializable] public class BoolEvent : UnityEvent<bool> { }
        [Serializable] public class SceneInfoEvent : UnityEvent<string, int, string, bool> { }
        [Serializable] public class GameObjectEvent : UnityEvent<GameObject> { }
        [Serializable] public class GameObjectArrayEvent : UnityEvent<GameObject[]> { }

        [Serializable]
        public class MoveGameObjectSettings
        {
            [BigHeader("Move Object References", 180, 220, 255, 13)]
            [Required("Required")]
            [InnerHint("Root object")]
            [Tooltip("Root GameObject to move into the destination scene. SceneManager.MoveGameObjectToScene requires a root GameObject.")]
            public GameObject gameObjectToMove;

            [Tooltip("Root GameObjects to move into the destination scene. Null entries are ignored.")]
            public GameObject[] gameObjectsToMove;

            [Tooltip("Detach objects from parents before moving them because SceneManager.MoveGameObjectToScene requires root GameObjects.")]
            public bool detachFromParentBeforeMove = true;
        }

        [Serializable]
        public class DontDestroySettings
        {
            [Required("Required")]
            [InnerHint("Persistent object")]
            [Tooltip("Object to pass to DontDestroyOnLoad.")]
            public UnityEngine.Object target;
        }


        [BigHeader("Scene Manage", 252, 191, 1, 17)]
        [HelpBox("Choose one operation, then only the settings used by that operation stay visible. Async operations are tracked from OnUpdate, so no coroutine is required.", HelpBoxKind.Info)]
        [GUIColor(0.86f, 0.95f, 1f, 1f)]
        [Tooltip("The scene action to execute when PlayAction calls this MOST action.")]
        public SceneManageOperation operation = SceneManageOperation.LoadSceneAsync;

        [BigHeader("Operation Settings", 120, 210, 255, 15)]
        [HelpBox("Only the fields used by the selected runtime operation are editable/visible. Target, source, destination, load, unload, create, and object settings are filtered from this section.", HelpBoxKind.Info)]
        [HideIfAll("operation", SceneManageOperation.LoadScene, false, "operation", SceneManageOperation.LoadSceneAsync, false, "operation", SceneManageOperation.UnloadSceneAsync, false, "operation", SceneManageOperation.UnloadAllLoadedScenesExceptTarget, false, "operation", SceneManageOperation.SetActiveScene, false, "operation", SceneManageOperation.QueryTargetScene, false, "operation", SceneManageOperation.GetBuildIndexByScenePath, false, "operation", SceneManageOperation.GetScenePathByBuildIndex, false, "operation", SceneManageOperation.GetSceneAtLoadedIndex, false, "operation", SceneManageOperation.GetSceneByName, false, "operation", SceneManageOperation.GetSceneByPath, false, "operation", SceneManageOperation.GetSceneByBuildIndex, false, "operation", SceneManageOperation.GetTargetSceneRootGameObjects, false, "operation", SceneManageOperation.GetTargetSceneRootCount, false, "operation", SceneManageOperation.GetTargetSceneHandle, false, "operation", SceneManageOperation.GetTargetSceneIsValid, false, "operation", SceneManageOperation.GetTargetSceneIsLoaded, false, "operation", SceneManageOperation.GetTargetSceneIsDirty, false, "operation", SceneManageOperation.GetTargetSceneIsSubScene, false)]
        [Tooltip("Main scene locator used by the selected operation.")]
        public SceneLocator targetScene = new SceneLocator { mode = SceneIdMode.NameOrPath, nameOrPath = "SceneName" };

        [HideIfAll("operation", SceneManageOperation.MergeScenes, false, "operation", SceneManageOperation.QuerySourceScene, false)]
        [Tooltip("Source scene for merge/query operations.")]
        public SceneLocator sourceScene = new SceneLocator { mode = SceneIdMode.Active };

        [HideIfAll("operation", SceneManageOperation.MoveGameObjectToScene, false, "operation", SceneManageOperation.MoveGameObjectsToScene, false, "operation", SceneManageOperation.MergeScenes, false, "operation", SceneManageOperation.QueryDestinationScene, false)]
        [Tooltip("Destination scene for move/merge/query operations.")]
        public SceneLocator destinationScene = new SceneLocator { mode = SceneIdMode.NameOrPath, nameOrPath = "SceneName" };

        [HideIfAll("operation", SceneManageOperation.LoadScene, false, "operation", SceneManageOperation.LoadSceneAsync, false)]
        [Tooltip("Single replaces current scenes. Additive keeps current scenes and adds the loaded scene.")]
        public LoadSceneMode loadSceneMode = LoadSceneMode.Single;

        [HideIfAll("operation", SceneManageOperation.LoadScene, false, "operation", SceneManageOperation.LoadSceneAsync, false, "operation", SceneManageOperation.CreateScene, false)]
        [Tooltip("Optional local physics scene mode used by LoadSceneParameters or CreateSceneParameters.")]
        public LocalPhysicsMode localPhysicsMode = LocalPhysicsMode.None;

        [HideIfAll("operation", SceneManageOperation.LoadScene, false, "operation", SceneManageOperation.LoadSceneAsync, false)]
        [Tooltip("Use LoadSceneParameters so load mode and local physics mode are both applied.")]
        public bool useLoadSceneParameters = true;

        [HideIfAll("operation", SceneManageOperation.LoadSceneAsync, false)]
        [Tooltip("When false, async load waits at 90% until AllowPendingAsyncSceneActivation or Stop releases it.")]
        public bool allowSceneActivation = true;

        [HideIfAll("operation", SceneManageOperation.LoadScene, false, "operation", SceneManageOperation.LoadSceneAsync, false)]
        [Tooltip("Set the loaded scene as active after the action can resolve it.")]
        public bool setLoadedSceneActiveWhenDone = false;

        [HideIfAll("operation", SceneManageOperation.UnloadSceneAsync, false, "operation", SceneManageOperation.UnloadActiveSceneAsync, false, "operation", SceneManageOperation.UnloadAllLoadedScenesExceptTarget, false)]
        [Tooltip("Extra Unity options for scene unloading.")]
        public UnloadSceneOptions unloadSceneOptions = UnloadSceneOptions.None;

        [HideIfAll("operation", SceneManageOperation.UnloadSceneAsync, false, "operation", SceneManageOperation.UnloadActiveSceneAsync, false, "operation", SceneManageOperation.UnloadAllLoadedScenesExceptTarget, false)]
        [Tooltip("Run Resources.UnloadUnusedAssets after unload operations finish.")]
        public bool unloadUnusedAssetsAfterUnload = false;

        [HideIfAll("operation", SceneManageOperation.CreateScene, false)]
        [Tooltip("Name for a runtime-created scene.")]
        public string newSceneName = "Runtime Scene";

        [HideIfAll("operation", SceneManageOperation.CreateScene, false)]
        [Tooltip("Use CreateSceneParameters so local physics mode can be applied.")]
        public bool useCreateSceneParameters = true;

        [HideIfAll("operation", SceneManageOperation.CreateScene, false)]
        [Tooltip("Make the created scene the active scene immediately.")]
        public bool setCreatedSceneActive = true;

        [HideIfAll("operation", SceneManageOperation.MoveGameObjectToScene, false, "operation", SceneManageOperation.MoveGameObjectsToScene, false)]
        [Tooltip("Object movement settings. The wrapper is hidden by operation; the inner object reference uses Required.")]
        public MoveGameObjectSettings moveObjectSettings = new MoveGameObjectSettings();

        [HideIfAll("operation", SceneManageOperation.DontDestroyOnLoad, false)]
        [Tooltip("DontDestroyOnLoad settings. The inner target reference uses Required.")]
        public DontDestroySettings dontDestroySettings = new DontDestroySettings();

        [Line(1f, 1f, 0.35f, 0.35f, 0.35f, 1f, 8f)]
        [BigHeader("Runtime Status", 160, 240, 255, 15)]
        [HelpBox("These fields are updated by the action at runtime and are intentionally read-only in the Inspector.", HelpBoxKind.Info)]
        [ReadOnly]
        [Tooltip("Average normalized progress of all tracked async operations.")]
        public float currentAsyncProgress;

        [ReadOnly]
        [Tooltip("True while this action is tracking one or more async operations.")]
        public bool hasRunningAsyncOperations;

        [ReadOnly]
        [Tooltip("Number of async operations currently tracked by this action.")]
        public int trackedAsyncOperationCount;

        [ReadOnly]
        [Tooltip("The operation most recently started through PlayAction.")]
        public string lastPlayedOperation = string.Empty;

        [ReadOnly]
        [Tooltip("True if the most recent play failed.")]
        public bool lastActionFailed;

        [ReadOnly]
        [Tooltip("Latest action/debug message sent through onMessage.")]
        public string lastMessage = string.Empty;

        [ReadOnly]
        [Tooltip("Latest scene name reported by the action.")]
        public string lastSceneName = string.Empty;

        [ReadOnly]
        [Tooltip("Latest scene build index reported by the action.")]
        public int lastSceneBuildIndex = -1;

        [ReadOnly]
        [Tooltip("Latest scene path reported by the action.")]
        public string lastScenePath = string.Empty;

        [ReadOnly]
        [Tooltip("Latest scene loaded state reported by the action.")]
        public bool lastSceneIsLoaded;

        [ReadOnly]
        [Tooltip("Latest scene handle reported by the action.")]
        public int lastSceneHandle;

        [ReadOnly]
        [Tooltip("Latest scene root GameObject count reported by the action.")]
        public int lastSceneRootCount = -1;

        [ReadOnly]
        [Tooltip("Latest root GameObjects returned by GetTargetSceneRootGameObjects.")]
        public GameObject[] lastRootGameObjects;

        [Line(1f, 1f, 0.35f, 0.35f, 0.35f, 1f, 8f)]
        [BigHeader("Logging / Async Stop Behavior", 200, 235, 160, 15)]
        [GUIColor("green")]
        [Tooltip("Print action messages to the Unity Console.")]
        public bool logMessages;

        [ReadOnlyIf("allowSceneActivation", true)]
        [Tooltip("Only matters when allowSceneActivation is false. Stop will release blocked async activation so the load can finish.")]
        public bool releaseBlockedActivationOnStop = true;

        [Tooltip("Clear tracked async operations when StopAction is called.")]
        public bool clearTrackedOperationsOnStop = false;

        [BigHeader("Unity Scene Events", 255, 150, 150, 15)]
        [GUIColor("cyan")]
        [Tooltip("Subscribe to SceneManager.sceneLoaded, sceneUnloaded, and activeSceneChanged.")]
        public bool subscribeToUnitySceneEvents = true;

        [Line(1f, 1f, 0.35f, 0.35f, 0.35f, 1f, 8f)]
        [BigHeader("Action Callbacks", 255, 215, 115, 15)]
        [GUIColor(1f, 0.92f, 0.55f, 1f, true)]
        public UnityEvent onActionStarted = new UnityEvent();
        [GUIColor(1f, 0.92f, 0.55f, 1f, true)]
        public UnityEvent onActionCompleted = new UnityEvent();
        [GUIColor(1f, 0.92f, 0.55f, 1f, true)]
        public UnityEvent onActionFailed = new UnityEvent();

        [BigHeader("Async Callbacks", 115, 220, 255, 15)]
        [GUIColor(0.65f, 0.9f, 1f, 1f, true)]
        public UnityEvent onAsyncStarted = new UnityEvent();
        [GUIColor(0.65f, 0.9f, 1f, 1f, true)]
        public UnityEvent onAsyncCompleted = new UnityEvent();
        [GUIColor(0.65f, 0.9f, 1f, 1f, true)]
        public FloatEvent onAsyncProgress = new FloatEvent();

        [BigHeader("Result Callbacks", 180, 200, 255, 15)]
        [GUIColor(0.75f, 0.85f, 1f, 1f, true)]
        public StringEvent onMessage = new StringEvent();
        [GUIColor(0.75f, 0.85f, 1f, 1f, true)]
        public IntEvent onIntResult = new IntEvent();
        [GUIColor(0.75f, 0.85f, 1f, 1f, true)]
        public BoolEvent onBoolResult = new BoolEvent();
        [GUIColor(0.75f, 0.85f, 1f, 1f, true)]
        public SceneInfoEvent onSceneInfo = new SceneInfoEvent();
        [GUIColor(0.75f, 0.85f, 1f, 1f, true)]
        public GameObjectEvent onRootGameObject = new GameObjectEvent();
        [GUIColor(0.75f, 0.85f, 1f, 1f, true)]
        public GameObjectArrayEvent onRootGameObjects = new GameObjectArrayEvent();

        [BigHeader("Forwarded Unity Scene Events", 255, 165, 185, 15)]
        [HideIfAny("subscribeToUnitySceneEvents", false, false)]
        public UnityEvent onUnitySceneLoaded = new UnityEvent();

        [HideIfAny("subscribeToUnitySceneEvents", false, false)]
        public UnityEvent onUnitySceneUnloaded = new UnityEvent();

        [HideIfAny("subscribeToUnitySceneEvents", false, false)]
        public UnityEvent onUnityActiveSceneChanged = new UnityEvent();

        [HideIfAny("subscribeToUnitySceneEvents", false, false)]
        public SceneInfoEvent onUnitySceneLoadedInfo = new SceneInfoEvent();

        [HideIfAny("subscribeToUnitySceneEvents", false, false)]
        public SceneInfoEvent onUnitySceneUnloadedInfo = new SceneInfoEvent();

        [HideIfAny("subscribeToUnitySceneEvents", false, false)]
        public StringEvent onUnityActiveSceneChangedInfo = new StringEvent();


        private class TrackedAsyncOperation
        {
            public AsyncOperation operation;
            public AsyncPurpose purpose;
            public SceneLocator locator;
        }

        [NonSerialized] private List<TrackedAsyncOperation> _trackedAsyncOperations;
        [NonSerialized] private bool _subscribedToSceneEvents;
        [NonSerialized] private bool _actionFailedThisPlay;


        public override void OnAwake()
        {
            base.OnAwake();

            if (subscribeToUnitySceneEvents)
                SubscribeToSceneEvents();
        }

        public override void OnValidate()
        {
            base.OnValidate();

            if (targetScene.loadedSceneIndex < 0) targetScene.loadedSceneIndex = 0;
            if (sourceScene.loadedSceneIndex < 0) sourceScene.loadedSceneIndex = 0;
            if (destinationScene.loadedSceneIndex < 0) destinationScene.loadedSceneIndex = 0;

            if (newSceneName == null) newSceneName = string.Empty;
            if (lastPlayedOperation == null) lastPlayedOperation = string.Empty;
            if (lastMessage == null) lastMessage = string.Empty;
            if (lastSceneName == null) lastSceneName = string.Empty;
            if (lastScenePath == null) lastScenePath = string.Empty;
            if (moveObjectSettings == null) moveObjectSettings = new MoveGameObjectSettings();
            if (dontDestroySettings == null) dontDestroySettings = new DontDestroySettings();
            if (lastRootGameObjects == null) lastRootGameObjects = new GameObject[0];
        }

        public override void OnLateUpdate()
        {
            base.OnLateUpdate();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            TickAsyncOperations();
        }

        protected override void Play()
        {
            _actionFailedThisPlay = false;
            lastActionFailed = false;
            lastPlayedOperation = operation.ToString();
            lastMessage = string.Empty;
            onActionStarted.Invoke();

            try
            {
                switch (operation)
                {
                    case SceneManageOperation.LoadScene:
                        LoadSceneSync();
                        CompleteAction();
                        break;

                    case SceneManageOperation.LoadSceneAsync:
                        LoadSceneAsync();
                        break;

                    case SceneManageOperation.ReloadActiveScene:
                        ReloadActiveScene(false);
                        CompleteAction();
                        break;

                    case SceneManageOperation.ReloadActiveSceneAsync:
                        ReloadActiveScene(true);
                        break;

                    case SceneManageOperation.AllowPendingAsyncSceneActivation:
                        SetPendingAsyncSceneActivation(true);
                        CompleteAction();
                        break;

                    case SceneManageOperation.UnloadSceneAsync:
                        TrackAsyncOperation(UnloadScene(targetScene), AsyncPurpose.Unload, targetScene);
                        break;

                    case SceneManageOperation.UnloadActiveSceneAsync:
                        TrackAsyncOperation(SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene(), unloadSceneOptions), AsyncPurpose.Unload, SceneLocator.Active());
                        break;

                    case SceneManageOperation.UnloadAllLoadedScenesExceptTarget:
                        UnloadAllLoadedScenesExceptTarget();
                        break;

                    case SceneManageOperation.UnloadUnusedAssets:
                        TrackAsyncOperation(Resources.UnloadUnusedAssets(), AsyncPurpose.UnloadUnusedAssets, SceneLocator.Active());
                        break;

                    case SceneManageOperation.CreateScene:
                        CreateRuntimeScene();
                        CompleteAction();
                        break;

                    case SceneManageOperation.SetActiveScene:
                        SetActiveSceneByLocator(targetScene);
                        CompleteAction();
                        break;

                    case SceneManageOperation.MoveGameObjectToScene:
                        MoveSingleGameObjectToScene();
                        CompleteAction();
                        break;

                    case SceneManageOperation.MoveGameObjectsToScene:
                        MoveManyGameObjectsToScene();
                        CompleteAction();
                        break;

                    case SceneManageOperation.MergeScenes:
                        MergeSourceIntoDestination();
                        CompleteAction();
                        break;

                    case SceneManageOperation.DontDestroyOnLoad:
                        MarkObjectDontDestroyOnLoad();
                        CompleteAction();
                        break;

                    case SceneManageOperation.QueryActiveScene:
                        ReportSceneInfo(SceneManager.GetActiveScene(), "Active Scene");
                        CompleteAction();
                        break;

                    case SceneManageOperation.QueryTargetScene:
                        ReportSceneInfo(ResolveScene(targetScene), "Target Scene");
                        CompleteAction();
                        break;

                    case SceneManageOperation.QuerySourceScene:
                        ReportSceneInfo(ResolveScene(sourceScene), "Source Scene");
                        CompleteAction();
                        break;

                    case SceneManageOperation.QueryDestinationScene:
                        ReportSceneInfo(ResolveScene(destinationScene), "Destination Scene");
                        CompleteAction();
                        break;

                    case SceneManageOperation.QueryAllLoadedScenes:
                        QueryAllLoadedScenes();
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetBuildIndexByScenePath:
                        GetBuildIndexByScenePath();
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetScenePathByBuildIndex:
                        GetScenePathByBuildIndex();
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetRuntimeSceneCount:
                        ReportInt("Runtime managed scene count", SceneManager.sceneCount);
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetRuntimeLoadedSceneCount:
                        ReportInt("Runtime fully loaded scene count", GetLoadedSceneCountSafe());
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetBuildSettingsSceneCount:
                        ReportInt("Build Settings scene count", SceneManager.sceneCountInBuildSettings);
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetSceneAtLoadedIndex:
                        ReportSceneInfo(GetSceneAtLoadedIndexSafe(targetScene.loadedSceneIndex), "Scene At Loaded Index " + targetScene.loadedSceneIndex);
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetSceneByName:
                        ReportSceneInfo(SceneManager.GetSceneByName(targetScene.nameOrPath), "Scene By Name");
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetSceneByPath:
                        ReportSceneInfo(SceneManager.GetSceneByPath(!string.IsNullOrEmpty(targetScene.path) ? targetScene.path : targetScene.nameOrPath), "Scene By Path");
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetSceneByBuildIndex:
                        ReportSceneInfo(SceneManager.GetSceneByBuildIndex(targetScene.buildIndex), "Scene By Build Index");
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetTargetSceneRootGameObjects:
                        GetTargetSceneRootGameObjects();
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetTargetSceneRootCount:
                        {
                            Scene scene = ResolveScene(targetScene, false);
                            ReportInt("Target scene root count", scene.IsValid() ? scene.rootCount : -1);
                            CompleteAction();
                            break;
                        }

                    case SceneManageOperation.GetTargetSceneHandle:
                        {
                            Scene scene = ResolveScene(targetScene, false);
                            ReportInt("Target scene handle", scene.IsValid() ? scene.handle : 0);
                            CompleteAction();
                            break;
                        }

                    case SceneManageOperation.GetTargetSceneIsValid:
                        ReportBool("Target scene is valid", ResolveScene(targetScene, false).IsValid());
                        CompleteAction();
                        break;

                    case SceneManageOperation.GetTargetSceneIsLoaded:
                        {
                            Scene scene = ResolveScene(targetScene, false);
                            ReportBool("Target scene is loaded", scene.IsValid() && scene.isLoaded);
                            CompleteAction();
                            break;
                        }

                    case SceneManageOperation.GetTargetSceneIsDirty:
                        {
                            Scene scene = ResolveScene(targetScene, false);
                            ReportBool("Target scene is dirty", scene.IsValid() && scene.isDirty);
                            CompleteAction();
                            break;
                        }

                    case SceneManageOperation.GetTargetSceneIsSubScene:
                        {
                            Scene scene = ResolveScene(targetScene, false);
                            ReportBool("Target scene is sub-scene", GetSceneIsSubSceneCompat(scene));
                            CompleteAction();
                            break;
                        }


                    default:
                        FailAction("Unsupported SceneManage operation: " + operation);
                        break;
                }
            }
            catch (Exception exception)
            {
                FailAction(exception.Message);
            }
        }

        protected override void Stop()
        {
            if (_trackedAsyncOperations != null)
            {
                for (int i = 0; i < _trackedAsyncOperations.Count; i++)
                {
                    AsyncOperation asyncOperation = _trackedAsyncOperations[i].operation;
                    if (asyncOperation != null && !asyncOperation.isDone && releaseBlockedActivationOnStop)
                        asyncOperation.allowSceneActivation = true;
                }

                if (clearTrackedOperationsOnStop)
                    _trackedAsyncOperations.Clear();
            }

            trackedAsyncOperationCount = _trackedAsyncOperations != null ? _trackedAsyncOperations.Count : 0;
            hasRunningAsyncOperations = trackedAsyncOperationCount > 0;
        }

        public override void OnDestroy()
        {
            UnsubscribeFromSceneEvents();
            base.OnDestroy();
        }

        private void LoadSceneSync()
        {
            if (targetScene.mode == SceneIdMode.BuildIndex)
            {
                if (useLoadSceneParameters)
                {
                    Scene loadedScene = SceneManager.LoadScene(targetScene.buildIndex, BuildLoadSceneParameters());
                    ReportSceneInfo(loadedScene, "Loaded Scene");
                    if (setLoadedSceneActiveWhenDone)
                        SetActiveSceneByScene(loadedScene);
                }
                else
                {
                    SceneManager.LoadScene(targetScene.buildIndex, loadSceneMode);
                }

                return;
            }

            string nameOrPath = GetSceneNameOrPathForLoading(targetScene);
            if (string.IsNullOrEmpty(nameOrPath))
            {
                FailAction("LoadScene needs a scene name, path, or build index.");
                return;
            }

            if (useLoadSceneParameters)
            {
                Scene loadedScene = SceneManager.LoadScene(nameOrPath, BuildLoadSceneParameters());
                ReportSceneInfo(loadedScene, "Loaded Scene");
                if (setLoadedSceneActiveWhenDone)
                    SetActiveSceneByScene(loadedScene);
            }
            else
            {
                SceneManager.LoadScene(nameOrPath, loadSceneMode);
            }
        }

        private void LoadSceneAsync()
        {
            AsyncOperation asyncOperation;

            if (targetScene.mode == SceneIdMode.BuildIndex)
            {
                asyncOperation = useLoadSceneParameters
                    ? SceneManager.LoadSceneAsync(targetScene.buildIndex, BuildLoadSceneParameters())
                    : SceneManager.LoadSceneAsync(targetScene.buildIndex, loadSceneMode);
            }
            else
            {
                string nameOrPath = GetSceneNameOrPathForLoading(targetScene);
                if (string.IsNullOrEmpty(nameOrPath))
                {
                    FailAction("LoadSceneAsync needs a scene name, path, or build index.");
                    return;
                }

                asyncOperation = useLoadSceneParameters
                    ? SceneManager.LoadSceneAsync(nameOrPath, BuildLoadSceneParameters())
                    : SceneManager.LoadSceneAsync(nameOrPath, loadSceneMode);
            }

            TrackAsyncOperation(asyncOperation, AsyncPurpose.Load, targetScene);
        }

        private void ReloadActiveScene(bool async)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                FailAction("Could not reload active scene because the active scene is invalid.");
                return;
            }

            SceneLocator activeLocator = new SceneLocator
            {
                mode = activeScene.buildIndex >= 0 ? SceneIdMode.BuildIndex : SceneIdMode.NameOrPath,
                buildIndex = activeScene.buildIndex,
                nameOrPath = !string.IsNullOrEmpty(activeScene.path) ? activeScene.path : activeScene.name
            };

            if (async)
            {
                AsyncOperation asyncOperation = activeLocator.mode == SceneIdMode.BuildIndex
                    ? SceneManager.LoadSceneAsync(activeLocator.buildIndex, LoadSceneMode.Single)
                    : SceneManager.LoadSceneAsync(activeLocator.nameOrPath, LoadSceneMode.Single);

                TrackAsyncOperation(asyncOperation, AsyncPurpose.Load, activeLocator);
            }
            else
            {
                if (activeLocator.mode == SceneIdMode.BuildIndex)
                    SceneManager.LoadScene(activeLocator.buildIndex, LoadSceneMode.Single);
                else
                    SceneManager.LoadScene(activeLocator.nameOrPath, LoadSceneMode.Single);
            }
        }

        private AsyncOperation UnloadScene(SceneLocator locator)
        {
            Scene scene = ResolveScene(locator, false);
            if (scene.IsValid())
                return SceneManager.UnloadSceneAsync(scene, unloadSceneOptions);

            if (locator.mode == SceneIdMode.BuildIndex)
                return SceneManager.UnloadSceneAsync(locator.buildIndex, unloadSceneOptions);

            string nameOrPath = GetSceneNameOrPathForLoading(locator);
            if (!string.IsNullOrEmpty(nameOrPath))
                return SceneManager.UnloadSceneAsync(nameOrPath, unloadSceneOptions);

            FailAction("UnloadSceneAsync needs a valid loaded scene, scene name, scene path, or build index.");
            return null;
        }

        private void UnloadAllLoadedScenesExceptTarget()
        {
            Scene keepScene = ResolveScene(targetScene, false);
            if (!keepScene.IsValid())
                keepScene = SceneManager.GetActiveScene();

            bool startedAny = false;

            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                    continue;

                if (keepScene.IsValid() && scene.handle == keepScene.handle)
                    continue;

                AsyncOperation asyncOperation = SceneManager.UnloadSceneAsync(scene, unloadSceneOptions);
                if (asyncOperation != null)
                {
                    startedAny = true;
                    TrackAsyncOperation(asyncOperation, AsyncPurpose.Unload, new SceneLocator
                    {
                        mode = SceneIdMode.LoadedSceneIndex,
                        loadedSceneIndex = i,
                        nameOrPath = scene.name,
                        path = scene.path,
                        buildIndex = scene.buildIndex
                    });
                }
            }

            if (!startedAny)
                CompleteAction();
        }

        private void CreateRuntimeScene()
        {
            string sceneName = string.IsNullOrEmpty(newSceneName) ? "Runtime Scene" : newSceneName;
            Scene scene = useCreateSceneParameters
                ? SceneManager.CreateScene(sceneName, new CreateSceneParameters(localPhysicsMode))
                : SceneManager.CreateScene(sceneName);

            if (setCreatedSceneActive)
                SetActiveSceneByScene(scene);

            ReportSceneInfo(scene, "Created Scene");
        }

        private void SetActiveSceneByLocator(SceneLocator locator)
        {
            Scene scene = ResolveScene(locator);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                FailAction("Cannot set active scene. Scene is invalid or not loaded.");
                return;
            }

            SetActiveSceneByScene(scene);
        }

        private void SetActiveSceneByScene(Scene scene)
        {
            bool result = scene.IsValid() && scene.isLoaded && SceneManager.SetActiveScene(scene);
            onBoolResult.Invoke(result);

            if (result)
                ReportSceneInfo(scene, "Active Scene Set");
            else
                FailAction("SceneManager.SetActiveScene failed.");
        }

        private void MoveSingleGameObjectToScene()
        {
            if (moveObjectSettings == null || moveObjectSettings.gameObjectToMove == null)
            {
                FailAction("MoveGameObjectToScene needs a GameObject in moveObjectSettings.gameObjectToMove.");
                return;
            }

            MoveGameObject(moveObjectSettings.gameObjectToMove, ResolveScene(destinationScene));
        }

        private void MoveManyGameObjectsToScene()
        {
            if (moveObjectSettings == null || moveObjectSettings.gameObjectsToMove == null || moveObjectSettings.gameObjectsToMove.Length == 0)
            {
                FailAction("MoveGameObjectsToScene needs at least one GameObject in moveObjectSettings.gameObjectsToMove.");
                return;
            }

            Scene scene = ResolveScene(destinationScene);
            for (int i = 0; i < moveObjectSettings.gameObjectsToMove.Length; i++)
            {
                if (moveObjectSettings.gameObjectsToMove[i] != null)
                    MoveGameObject(moveObjectSettings.gameObjectsToMove[i], scene);
            }
        }

        private void MoveGameObject(GameObject objectToMove, Scene destination)
        {
            if (!destination.IsValid() || !destination.isLoaded)
            {
                FailAction("Cannot move GameObject. Destination scene is invalid or not loaded.");
                return;
            }

            if (moveObjectSettings != null && moveObjectSettings.detachFromParentBeforeMove && objectToMove.transform.parent != null)
                objectToMove.transform.SetParent(null, true);

            SceneManager.MoveGameObjectToScene(objectToMove, destination);
            ReportSceneInfo(destination, "Moved GameObject To Scene");
        }

        private void MergeSourceIntoDestination()
        {
            Scene source = ResolveScene(sourceScene);
            Scene destination = ResolveScene(destinationScene);

            if (!source.IsValid() || !destination.IsValid())
            {
                FailAction("MergeScenes needs valid source and destination scenes.");
                return;
            }

            SceneManager.MergeScenes(source, destination);
            ReportSceneInfo(destination, "Merged Scene Destination");
        }

        private void MarkObjectDontDestroyOnLoad()
        {
            if (dontDestroySettings == null || dontDestroySettings.target == null)
            {
                FailAction("DontDestroyOnLoad needs a target in dontDestroySettings.target.");
                return;
            }

            UnityEngine.Object.DontDestroyOnLoad(dontDestroySettings.target);
            Message("Marked object as DontDestroyOnLoad: " + dontDestroySettings.target.name);
        }

        private void QueryAllLoadedScenes()
        {
            onIntResult.Invoke(SceneManager.sceneCount);
            Message("Loaded/managed scene count: " + SceneManager.sceneCount + " | Fully loaded scene count: " + GetLoadedSceneCountSafe());

            for (int i = 0; i < SceneManager.sceneCount; i++)
                ReportSceneInfo(SceneManager.GetSceneAt(i), "Scene At Index " + i);
        }

        private int GetLoadedSceneCountSafe()
        {
            int count = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                    count++;
            }

            return count;
        }

        private void GetBuildIndexByScenePath()
        {
            string path = !string.IsNullOrEmpty(targetScene.path) ? targetScene.path : targetScene.nameOrPath;
            int index = SceneUtility.GetBuildIndexByScenePath(path);
            onIntResult.Invoke(index);
            Message("Build index for scene path '" + path + "': " + index);
        }

        private void GetScenePathByBuildIndex()
        {
            string path = SceneUtility.GetScenePathByBuildIndex(targetScene.buildIndex);
            onMessage.Invoke(path);
            Message("Scene path for build index " + targetScene.buildIndex + ": " + path);
        }

        private Scene GetSceneAtLoadedIndexSafe(int index)
        {
            if (index < 0 || index >= SceneManager.sceneCount)
            {
                FailAction("GetSceneAt needs an index between 0 and " + (SceneManager.sceneCount - 1) + ". Given: " + index);
                return default(Scene);
            }

            return SceneManager.GetSceneAt(index);
        }

        private void GetTargetSceneRootGameObjects()
        {
            Scene scene = ResolveScene(targetScene);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                FailAction("GetRootGameObjects needs a valid loaded target scene.");
                return;
            }

            lastRootGameObjects = scene.GetRootGameObjects();
            onRootGameObjects.Invoke(lastRootGameObjects);
            onIntResult.Invoke(lastRootGameObjects != null ? lastRootGameObjects.Length : 0);

            if (lastRootGameObjects != null)
            {
                for (int i = 0; i < lastRootGameObjects.Length; i++)
                {
                    if (lastRootGameObjects[i] != null)
                        onRootGameObject.Invoke(lastRootGameObjects[i]);
                }
            }

            ReportSceneInfo(scene, "Target Scene Root GameObjects");
        }

        private void ReportInt(string label, int value)
        {
            onIntResult.Invoke(value);
            Message(label + ": " + value);
        }

        private void ReportBool(string label, bool value)
        {
            onBoolResult.Invoke(value);
            Message(label + ": " + value);
        }

        private bool GetSceneIsSubSceneCompat(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            System.Reflection.PropertyInfo property = typeof(Scene).GetProperty("isSubScene", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property == null || property.PropertyType != typeof(bool))
                return false;

            try
            {
                return (bool)property.GetValue(scene, null);
            }
            catch
            {
                return false;
            }
        }

        private LoadSceneParameters BuildLoadSceneParameters()
        {
            return new LoadSceneParameters(loadSceneMode, localPhysicsMode);
        }

        private void TrackAsyncOperation(AsyncOperation asyncOperation, AsyncPurpose purpose, SceneLocator locator)
        {
            if (asyncOperation == null)
            {
                FailAction("Async operation could not start. Unity returned null.");
                return;
            }

            EnsureAsyncList();

            if (purpose == AsyncPurpose.Load)
                asyncOperation.allowSceneActivation = allowSceneActivation;

            _trackedAsyncOperations.Add(new TrackedAsyncOperation
            {
                operation = asyncOperation,
                purpose = purpose,
                locator = locator
            });

            trackedAsyncOperationCount = _trackedAsyncOperations.Count;
            hasRunningAsyncOperations = true;
            onAsyncStarted.Invoke();
        }

        private void TickAsyncOperations()
        {
            if (_trackedAsyncOperations == null || _trackedAsyncOperations.Count == 0)
            {
                hasRunningAsyncOperations = false;
                trackedAsyncOperationCount = 0;
                currentAsyncProgress = 0f;
                return;
            }

            float totalProgress = 0f;
            int validCount = 0;
            bool completedAny = false;

            for (int i = _trackedAsyncOperations.Count - 1; i >= 0; i--)
            {
                TrackedAsyncOperation tracked = _trackedAsyncOperations[i];

                if (tracked == null || tracked.operation == null)
                {
                    _trackedAsyncOperations.RemoveAt(i);
                    continue;
                }

                validCount++;
                totalProgress += NormalizeAsyncProgress(tracked.operation);

                if (tracked.operation.isDone)
                {
                    completedAny = true;
                    HandleTrackedAsyncCompleted(tracked);
                    _trackedAsyncOperations.RemoveAt(i);
                }
            }

            currentAsyncProgress = validCount > 0 ? totalProgress / validCount : 0f;
            onAsyncProgress.Invoke(currentAsyncProgress);

            trackedAsyncOperationCount = _trackedAsyncOperations.Count;
            hasRunningAsyncOperations = trackedAsyncOperationCount > 0;

            if (completedAny && _trackedAsyncOperations.Count == 0)
            {
                onAsyncCompleted.Invoke();
                CompleteAction();
            }
        }

        private float NormalizeAsyncProgress(AsyncOperation asyncOperation)
        {
            if (asyncOperation == null)
                return 0f;

            if (asyncOperation.isDone)
                return 1f;

            if (asyncOperation.progress >= 0.9f && !asyncOperation.allowSceneActivation)
                return 0.99f;

            return Mathf.Clamp01(asyncOperation.progress / 0.9f);
        }

        private void HandleTrackedAsyncCompleted(TrackedAsyncOperation tracked)
        {
            switch (tracked.purpose)
            {
                case AsyncPurpose.Load:
                    Scene loadedScene = ResolveScene(tracked.locator, false);
                    if (loadedScene.IsValid())
                    {
                        ReportSceneInfo(loadedScene, "Async Loaded Scene");
                        if (setLoadedSceneActiveWhenDone)
                            SetActiveSceneByScene(loadedScene);
                    }
                    break;

                case AsyncPurpose.Unload:
                    if (unloadUnusedAssetsAfterUnload)
                        TrackAsyncOperation(Resources.UnloadUnusedAssets(), AsyncPurpose.UnloadUnusedAssets, SceneLocator.Active());
                    break;
            }
        }

        private void SetPendingAsyncSceneActivation(bool value)
        {
            if (_trackedAsyncOperations == null)
                return;

            for (int i = 0; i < _trackedAsyncOperations.Count; i++)
            {
                AsyncOperation asyncOperation = _trackedAsyncOperations[i].operation;
                if (asyncOperation != null && !asyncOperation.isDone)
                    asyncOperation.allowSceneActivation = value;
            }

            Message("Set pending async scene activation to: " + value);
        }

        private Scene ResolveScene(SceneLocator locator)
        {
            return ResolveScene(locator, true);
        }

        private Scene ResolveScene(SceneLocator locator, bool warnIfInvalid)
        {
            Scene scene = default(Scene);

            switch (locator.mode)
            {
                case SceneIdMode.Active:
                    scene = SceneManager.GetActiveScene();
                    break;

                case SceneIdMode.Name:
                    scene = SceneManager.GetSceneByName(locator.nameOrPath);
                    break;

                case SceneIdMode.Path:
                    scene = SceneManager.GetSceneByPath(!string.IsNullOrEmpty(locator.path) ? locator.path : locator.nameOrPath);
                    break;

                case SceneIdMode.NameOrPath:
                    scene = ResolveByNameOrPath(locator.nameOrPath);
                    break;

                case SceneIdMode.BuildIndex:
                    scene = SceneManager.GetSceneByBuildIndex(locator.buildIndex);
                    break;

                case SceneIdMode.LoadedSceneIndex:
                    if (locator.loadedSceneIndex >= 0 && locator.loadedSceneIndex < SceneManager.sceneCount)
                        scene = SceneManager.GetSceneAt(locator.loadedSceneIndex);
                    break;
            }

            if (warnIfInvalid && !scene.IsValid())
                Message("Scene locator did not resolve a valid scene. Mode: " + locator.mode);

            return scene;
        }

        private Scene ResolveByNameOrPath(string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath))
                return default(Scene);

            Scene scene = SceneManager.GetSceneByPath(nameOrPath);
            if (scene.IsValid())
                return scene;

            scene = SceneManager.GetSceneByName(nameOrPath);
            if (scene.IsValid())
                return scene;

            string filenameWithoutExtension = GetFileNameWithoutUnityExtension(nameOrPath);
            if (!string.IsNullOrEmpty(filenameWithoutExtension) && filenameWithoutExtension != nameOrPath)
                scene = SceneManager.GetSceneByName(filenameWithoutExtension);

            return scene;
        }

        private string GetSceneNameOrPathForLoading(SceneLocator locator)
        {
            if (locator.mode == SceneIdMode.Path && !string.IsNullOrEmpty(locator.path))
                return locator.path;

            if (!string.IsNullOrEmpty(locator.nameOrPath))
                return locator.nameOrPath;

            if (!string.IsNullOrEmpty(locator.path))
                return locator.path;

            return string.Empty;
        }

        private string GetFileNameWithoutUnityExtension(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string normalized = value.Replace("\\", "/");
            int slashIndex = normalized.LastIndexOf('/');
            string fileName = slashIndex >= 0 ? normalized.Substring(slashIndex + 1) : normalized;

            if (fileName.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                fileName = fileName.Substring(0, fileName.Length - ".unity".Length);

            return fileName;
        }

        private void ReportSceneInfo(Scene scene, string label)
        {
            bool valid = scene.IsValid();
            string name = valid ? scene.name : string.Empty;
            int buildIndex = valid ? scene.buildIndex : -1;
            string path = valid ? scene.path : string.Empty;
            bool loaded = valid && scene.isLoaded;
            int handle = valid ? scene.handle : 0;
            int rootCount = valid ? scene.rootCount : -1;

            lastSceneName = name;
            lastSceneBuildIndex = buildIndex;
            lastScenePath = path;
            lastSceneIsLoaded = loaded;
            lastSceneHandle = handle;
            lastSceneRootCount = rootCount;

            onSceneInfo.Invoke(name, buildIndex, path, loaded);
            Message(label + " | valid=" + valid + ", loaded=" + loaded + ", name=" + name + ", buildIndex=" + buildIndex + ", rootCount=" + rootCount + ", handle=" + handle + ", path=" + path);
        }

        private void CompleteAction()
        {
            if (!_actionFailedThisPlay)
            {
                lastActionFailed = false;
                onActionCompleted.Invoke();
            }
        }

        private void FailAction(string message)
        {
            _actionFailedThisPlay = true;
            lastActionFailed = true;
            lastMessage = message;

            if (logMessages)
                Debug.LogWarning("[MOSTAction_SceneManage] " + message);

            onMessage.Invoke(message);
            onActionFailed.Invoke();
        }

        private void Message(string message)
        {
            lastMessage = message;

            if (logMessages)
                Debug.Log("[MOSTAction_SceneManage] " + message);

            onMessage.Invoke(message);
        }

        private void EnsureAsyncList()
        {
            if (_trackedAsyncOperations == null)
                _trackedAsyncOperations = new List<TrackedAsyncOperation>();
        }

        private void SubscribeToSceneEvents()
        {
            if (_subscribedToSceneEvents)
                return;

            SceneManager.sceneLoaded += HandleUnitySceneLoaded;
            SceneManager.sceneUnloaded += HandleUnitySceneUnloaded;
            SceneManager.activeSceneChanged += HandleUnityActiveSceneChanged;
            _subscribedToSceneEvents = true;
        }

        private void UnsubscribeFromSceneEvents()
        {
            if (!_subscribedToSceneEvents)
                return;

            SceneManager.sceneLoaded -= HandleUnitySceneLoaded;
            SceneManager.sceneUnloaded -= HandleUnitySceneUnloaded;
            SceneManager.activeSceneChanged -= HandleUnityActiveSceneChanged;
            _subscribedToSceneEvents = false;
        }

        private void HandleUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            onUnitySceneLoaded.Invoke();
            onUnitySceneLoadedInfo.Invoke(scene.name, scene.buildIndex, scene.path, scene.isLoaded);
            Message("Unity sceneLoaded event: " + scene.name + " | mode=" + mode);
        }

        private void HandleUnitySceneUnloaded(Scene scene)
        {
            onUnitySceneUnloaded.Invoke();
            onUnitySceneUnloadedInfo.Invoke(scene.name, scene.buildIndex, scene.path, scene.isLoaded);
            Message("Unity sceneUnloaded event: " + scene.name);
        }

        private void HandleUnityActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            onUnityActiveSceneChanged.Invoke();
            onUnityActiveSceneChangedInfo.Invoke(oldScene.name + " -> " + newScene.name);
            Message("Unity activeSceneChanged event: " + oldScene.name + " -> " + newScene.name);
        }

    }
}
