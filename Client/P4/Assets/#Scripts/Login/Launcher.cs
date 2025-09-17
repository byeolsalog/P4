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

        // 버전 체크
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(ServerUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            Debug.Log($"서버 응답: {json}");

            VersionResponse data = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionResponse>(json);
            if (data == null || string.IsNullOrEmpty(data.minVersion))
            {
                Debug.LogError("서버에서 버전 정보를 읽을 수 없습니다.");
                return;
            }

            string localVersion = Application.version;
            _version.text = localVersion;
            if (IsLowerVersion(localVersion, data.minVersion))
            {
                Debug.LogError("앱 버전이 맞지 않습니다. 업데이트가 필요합니다.");
            }
            else if(IsLowerVersion(localVersion, data.latestVersion))
            {
                _version.text = data.latestVersion;
                if (data.forceUpdate)
                {
                    Debug.Log("새로운 버전이 있습니다. 업데이트가 필요합니다.");
                }
                else
                {
                    Debug.Log("새 버전으로 업데이트가 가능합니다.");
                }                
            }
            else
            {
                Debug.Log("버전 체크 완료");
                _version.text = data.latestVersion;
            }

            if(!string.IsNullOrEmpty(data.notice))
            {
                Debug.Log($"공지사항: {data.notice}");
            }
        }
        catch (HttpRequestException e)
        {
            Debug.LogError($"버전 체크 중 오류 발생: {e.Message}");
            return;
        }
        catch(System.Exception ex)
        {
            Debug.LogError($"알 수 없는 오류 발생: {ex.Message}");
            return;
        }

        // UGS 초기화
        await GameManager.Login.InitUnityService();
        Debug.Log("로그인 완료");

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
                    Debug.Log("다운로드 완료");
                }
                else
                {
                    Debug.LogError("다운로드 실패");
                    return;
                }
            }
            else
            {
                Debug.Log("다운로드 필요 없음");
            }
        }
        else
        {
            Debug.LogError("다운로드 용량 체크 실패");
            return;
        }
        _version.text = "어드레서블 완료";
    }

    private bool IsLowerVersion(string local, string min)
    {
        // 간단한 버전 비교 (1.2.3 형식 기준)
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