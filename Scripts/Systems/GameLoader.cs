using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Enums;

public class GameLoader : AsyncLoader
{
    [SerializeField] private SaveLoadSystem _saveLoadSystem = null;
    [SerializeField] private int sceneIndexToLoad = 1;
    [SerializeField] private static CutScene _cutScene = CutScene.LevelOne;

    //public List<Component> GameModules = new List<Component>();
    private static GameLoader _instance = null;
    private static int _sceneIndex = 1;

    public static Transform SystemsParent { get => _systemsParent; }
    private static Transform _systemsParent = null;

    private readonly static List<Action> _queuedCallbacks = new List<Action>();

    public static CutScene CutScene { get => _cutScene; set => _cutScene = value; }

    protected override void Awake()
    {
        Debug.Log("GameLoader Starting");

        // Safety check
        if (_instance != null && _instance != this)
        {
            Debug.Log("A duplicate instance of the GameLoader was found, and will be ignored. Only one instance is permitted");
            Destroy(gameObject);
            return;
        }

        // Set reference to this instance
        _queuedCallbacks.Clear();
        _instance = this;

        // Make persistent
        DontDestroyOnLoad(gameObject);

        // Scene Index Check
        if (sceneIndexToLoad < 0 || sceneIndexToLoad >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log($"Invalid Scene Index {sceneIndexToLoad} ... using default value of {_sceneIndex}");
        }
        else
        {
            Debug.Log($"Scene index to load is set to {sceneIndexToLoad}");
            _sceneIndex = sceneIndexToLoad;
        }

        // Setup System GameObject
        GameObject systemsGO = new GameObject("[Services]");
        _systemsParent = systemsGO.transform;
        DontDestroyOnLoad(systemsGO);

        // Queue up loading routines
        Enqueue(IntializeCoreSystems(_systemsParent), 1); // Things we need to have
        Enqueue(InitializeModularSystems(_systemsParent), 2); // Optional

        // Check for any CallOnComplete callbacks that were queued through Awake before this instance was made
        ProcessQueuedCallbacks();

        // Because Unity can hold onto static values between sessions.
        ResetVariables();

        // Set completion callback
        GameLoader.CallOnComplete(OnComplete);
    }

    private IEnumerator IntializeCoreSystems(Transform systemsParent)
    {
        Debug.Log("Loading Core Systems");
        _saveLoadSystem.Initialize();
        _saveLoadSystem.Load();
        //var monoUtilGO = new GameObject("Monobehaviour Utility");
        //monoUtilGO.transform.SetParent(systemsParent);
        //var monoUtilComp = monoUtilGO.AddComponent<MonoUtil>().Initialize();
        //ServiceLocator.Register<MonoUtil>(monoUtilComp);

        var eventBusSystem = Resources.Load("EventBusSystem");
        var eventBusSystemGO = Instantiate(eventBusSystem) as GameObject;
        eventBusSystemGO.transform.SetParent(systemsParent);
        eventBusSystemGO.name = "EventBusSystem";
        eventBusSystemGO.GetComponent<EventBusSystem>().Initialize();

        var resourceManagerGO = new GameObject("Resource Manager");
        resourceManagerGO.transform.SetParent(systemsParent);
        var resourceManagerComp = resourceManagerGO.AddComponent<ResourceManager>().Initialize();
        ServiceLocator.Register<ResourceManager>(resourceManagerComp);

        var uiManagerPrefab = Resources.Load("UIManager");
        var uiManagerGO = Instantiate(uiManagerPrefab) as GameObject;
        uiManagerGO.transform.SetParent(systemsParent);
        uiManagerGO.name = "UIManager";
        var uiManagerComp = uiManagerGO.GetComponent<UIManager>();
        uiManagerComp.Initialize();
        ServiceLocator.Register<UIManager>(uiManagerComp);

        var sceneLoaderPrefab = Resources.Load("SceneLoaderManager");
        var sceneLoaderGO = Instantiate(sceneLoaderPrefab) as GameObject;
        sceneLoaderGO.transform.SetParent(systemsParent);
        sceneLoaderGO.name = "SceneLoaderManager";
        var sceneLoaderComp = sceneLoaderGO.GetComponent<SceneLoaderManager>();
        sceneLoaderComp.Initialize(uiManagerComp.StatefulObject);
        ServiceLocator.Register<SceneLoaderManager>(sceneLoaderComp);

        var wwiseManagerPrefab = Resources.Load("WwiseManager");
        var wwiseManagerGO = Instantiate(wwiseManagerPrefab) as GameObject;
        wwiseManagerGO.transform.SetParent(systemsParent);
        wwiseManagerGO.name = "WwiseManager";
        var wwiseManagersComp = wwiseManagerGO.GetComponent<WwiseManager>().Initialize();
        ServiceLocator.Register<IWwiseManager>(wwiseManagersComp);

        //_saveLoadSystem.Load();

        yield return null;
    }

    private IEnumerator InitializeModularSystems(Transform systemsParent)
    {
        // Setup Additional Systems as needed
        //Debug.Log("Loading Modular Systems");

        //foreach (var module in GameModules)
        //{
        //    if (module is IGameModule)
        //    {
        //        IGameModule gameModule = module as IGameModule;
        //        gameModule.Load();
        //    }
        //}

        yield return null;
    }

    private IEnumerator LoadInitialScene(int index)
    {
        int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;
        if (index != activeSceneIndex)
        {
            Debug.Log($"GameLoader -> Starting Scene Load: {index}");
            yield return SceneManager.LoadSceneAsync(index);
        }
        else
        {
            // We are already have the desired scene loaded.
            Debug.Log("GameLoader -> Skipping Scene Load: Scene is already active");
            yield break;
        }
    }

    protected override void ResetVariables()
    {
        base.ResetVariables();
    }

    public static void CallOnComplete(Action callback)
    {
        if (!_instance)
        {
            _queuedCallbacks.Add(callback); // Uses the static List declared in the header of the script
            return;
        }

        _instance.CallOnComplete_Internal(callback); // Adds the function in the inherited queue. Each queue belongs to their instance, THEY ARE NOT SHARED!
    }

    // Handles everything that is added in the static List before the instance of the GameLoader is created
    private void ProcessQueuedCallbacks()
    {
        foreach (var callback in _queuedCallbacks)
        {
            callback?.Invoke();
        }
        _queuedCallbacks.Clear();
    }

    // AsyncLoader completion callback
    private void OnComplete()
    {
        Debug.Log("GameLoader Finished Initializing");
        StartCoroutine(LoadInitialScene(_sceneIndex));
    }
}