using UnityEngine;

public class GameManager : MonoBehaviour
{
    static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            Init();
            return _instance;
        }
    }

    // 각 기능별 매니저들을 관리
    private LoginManager _login = new LoginManager();
    private AddressablesManager _addressables = new AddressablesManager();
    private VersionManager _version = new VersionManager();

    public static LoginManager Login => Instance._login;
    public static AddressablesManager Addressables => Instance._addressables;
    public static VersionManager Version => Instance._version;

    private void Awake()
    {
        Init();
    }

    static void Init()
    {
        if (_instance == null)
        {
            GameObject go = GameObject.Find("GameManager");
            if (go == null)
            {
                go = new GameObject { name = "GameManager" };
                go.AddComponent<GameManager>();
            }

            DontDestroyOnLoad(go);
            _instance = go.GetComponent<GameManager>();
        }
    }
}