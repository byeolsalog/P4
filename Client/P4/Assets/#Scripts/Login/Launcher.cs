using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using UnityEditor;

public class Launcher : MonoBehaviour
{
    [SerializeField] private Transform _loginParent;
    [SerializeField] private Transform _gameStartParent;

    private async void Start()
    {
        // 인터넷 연결 상태 체크
        if(Application.internetReachability == NetworkReachability.NotReachable)
        {
            // 인터넷 연결 안되어있음.
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }        

        // UGS 초기화
        await GameManager.Login.InitUnityService();
        Debug.Log("로그인 완료");
    }
}
