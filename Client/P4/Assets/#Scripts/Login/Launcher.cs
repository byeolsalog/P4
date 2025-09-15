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
        // ���ͳ� ���� ���� üũ
        if(Application.internetReachability == NetworkReachability.NotReachable)
        {
            // ���ͳ� ���� �ȵǾ�����.
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }        

        // UGS �ʱ�ȭ
        await GameManager.Login.InitUnityService();
        Debug.Log("�α��� �Ϸ�");
    }
}
