using UnityEngine;

public class GameManager : MonoBehaviour
{
    static GameManager _instance;
    static GameManager Instance { get { Init(); return _instance; } }
    
    LoginManager _login = new LoginManager();
    public static LoginManager Login { get { return Instance._login; } }

    static void Init()
    {   
        if(_instance == null)
        {
            GameObject go = GameObject.Find("GameManager");
            if( go == null)
            {
                go = new GameObject { name = "GameManager" };
                go.AddComponent<GameManager>();
            }

            DontDestroyOnLoad(go);
            _instance = go.GetComponent<GameManager>();
        }
    }
}
