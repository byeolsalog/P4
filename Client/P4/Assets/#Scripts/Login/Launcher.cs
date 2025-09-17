using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEditor;
using System.Net.Http;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System.Threading.Tasks;

public class Launcher : MonoBehaviour
{
    [SerializeField] private Transform _loginParent;
    [SerializeField] private Transform _gameStartParent;
    [SerializeField] private Transform _CCDParent;

    [SerializeField] private TextMeshProUGUI _version;
    [SerializeField] private TextMeshProUGUI _addressableDataText;
    [SerializeField] private Image _progress;

    private static readonly HttpClient httpClient = new HttpClient();
    private const string ServerUrl = "http://p4.ddns.net:5020/version/Android";

    public List<string> keysOrLabels = new List<string>()
    {
        "TestLebel",
    };

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

        // ���� üũ
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(ServerUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            Debug.Log($"���� ����: {json}");

            VersionResponse data = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionResponse>(json);
            if (data == null || string.IsNullOrEmpty(data.minVersion))
            {
                Debug.LogError("�������� ���� ������ ���� �� �����ϴ�.");
                return;
            }

            string localVersion = Application.version;
            _version.text = localVersion;
            if (IsLowerVersion(localVersion, data.minVersion))
            {
                Debug.LogError("�� ������ ���� �ʽ��ϴ�. ������Ʈ�� �ʿ��մϴ�.");
            }
            else if(IsLowerVersion(localVersion, data.latestVersion))
            {
                _version.text = data.latestVersion;
                if (data.forceUpdate)
                {
                    Debug.Log("���ο� ������ �ֽ��ϴ�. ������Ʈ�� �ʿ��մϴ�.");
                }
                else
                {
                    Debug.Log("�� �������� ������Ʈ�� �����մϴ�.");
                }                
            }
            else
            {
                Debug.Log("���� üũ �Ϸ�");
                _version.text = data.latestVersion;
            }

            if(!string.IsNullOrEmpty(data.notice))
            {
                Debug.Log($"��������: {data.notice}");
            }
        }
        catch (HttpRequestException e)
        {
            Debug.LogError($"���� üũ �� ���� �߻�: {e.Message}");
            return;
        }
        catch(System.Exception ex)
        {
            Debug.LogError($"�� �� ���� ���� �߻�: {ex.Message}");
            return;
        }

        // UGS �ʱ�ȭ
        await GameManager.Login.InitUnityService();
        Debug.Log("�α��� �Ϸ�");

        AsyncOperationHandle<long> handle = Addressables.GetDownloadSizeAsync(keysOrLabels);        
        if(handle.Status == AsyncOperationStatus.Succeeded)
        {
            long downloadSize = handle.Result;
            if(downloadSize > 0)
            {
                _CCDParent.gameObject.SetActive(true);
                _progress.fillAmount = 0f;
                _addressableDataText.text = $"{downloadSize / (1024f * 1024f):F2} MB";
                var dlHandle = Addressables.DownloadDependenciesAsync(keysOrLabels, Addressables.MergeMode.Union, true);
                await dlHandle.Task;

                while(!dlHandle.IsDone)
                {
                    _progress.fillAmount = dlHandle.PercentComplete;
                    long downloadedBytes = (long)(downloadSize * dlHandle.PercentComplete);
                    _addressableDataText.text = $"{downloadedBytes / (1024f * 1024f):F2}MB / {downloadSize / (1024f * 1024f):F2}MB";
                    await Task.Yield();
                }

                _CCDParent.gameObject.SetActive(false);

                if (dlHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log("�ٿ�ε� �Ϸ�");
                }
                else
                {
                    Debug.LogError("�ٿ�ε� ����");
                    return;
                }
            }
            else
            {
                Debug.Log("�ٿ�ε� �ʿ� ����");
            }
        }
        else
        {
            Debug.LogError("�ٿ�ε� �뷮 üũ ����");
            return;
        }
        _version.text = "��巹���� �Ϸ�";
    }

    private bool IsLowerVersion(string local, string min)
    {
        // ������ ���� �� (1.2.3 ���� ����)
        var lv = local.Split('.');
        var mv = min.Split('.');

        for (int i = 0; i < Mathf.Min(lv.Length, mv.Length); i++)
        {
            int l = int.Parse(lv[i]);
            int m = int.Parse(mv[i]);

            if (l < m) return true;
            if (l > m) return false;
        }

        return false;
    }

    public void OnClickClearCache()
    {
        Caching.ClearCache();
    }
}

[System.Serializable]
public class VersionResponse
{
    public string latestVersion;
    public string minVersion;
    public bool forceUpdate;
    public string notice;
}