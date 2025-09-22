using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Launcher : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _loginButtonsParent;
    [SerializeField] private GameObject _CCDParent;
    [SerializeField] private TextMeshProUGUI _versionText;
    [SerializeField] private TextMeshProUGUI _addressableDataText;
    [SerializeField] private Image _progress;
    [SerializeField] private TextMeshProUGUI _statusMessage; // ✅ 상태 메시지 추가

    private Task<bool> _addressablesDownloadTask;

    private async void Start()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            ShowStatus("인터넷 연결 없음. 앱을 종료합니다.");
            QuitApplication();
            return;
        }

        _loginButtonsParent.SetActive(false);
        _CCDParent.SetActive(false);
        SubscribeToEvents();

        _versionText.text = $"v{Application.version}";
        var versionResult = await GameManager.Version.CheckVersionAsync();
        if (versionResult == VersionCheckResult.Failed || versionResult == VersionCheckResult.ForceUpdate)
        {
            ShowStatus("버전 체크 실패 또는 업데이트 필요.");
            QuitApplication();
            return;
        }
        _versionText.text = $"v{GameManager.Version.LatestVersion}";

        bool firebaseInitSuccess = await GameManager.Login.InitializeAsync();
        if (!firebaseInitSuccess)
        {
            ShowStatus("Firebase 초기화 실패.");
            QuitApplication();
            return;
        }

        await GameManager.Addressables.InitAddressables();

        _addressablesDownloadTask = GameManager.Addressables.DownloadAllDependenciesAsync();

        _loginButtonsParent.SetActive(true);
    }

    #region Button Click Handlers
    public async void OnClick_SignInAnonymously()
    {
        _loginButtonsParent.SetActive(false);
        ShowStatus("익명 로그인 중...");
        var loginTask = GameManager.Login.SignInAnonymouslyAsync();
        await ProcessLoginAndDownload(loginTask);
    }

    public async void OnClick_SignInWithGoogle()
    {
        _loginButtonsParent.SetActive(false);
        ShowStatus("Google 로그인 중...");
        var loginTask = GameManager.Login.SignInWithGpgsAsync();
        await ProcessLoginAndDownload(loginTask);
    }

    private async Task ProcessLoginAndDownload(Task<bool> loginTask)
    {
        await Task.WhenAll(_addressablesDownloadTask, loginTask);

        bool downloadSuccess = await _addressablesDownloadTask;
        bool loginSuccess = await loginTask;

        if (downloadSuccess && loginSuccess)
        {
            ShowStatus("로그인 및 다운로드 완료. 로비로 이동합니다...");
            ProceedToLobby();
        }
        else
        {
            ShowStatus($"실패! Download: {downloadSuccess}, Login: {loginSuccess}");
            _loginButtonsParent.SetActive(true);
        }
    }
    #endregion

    private async void ProceedToLobby()
    {
        await Task.Delay(1000); // ✅ UX: 짧은 로딩 딜레이
        Debug.Log("모든 준비 완료. 로비 씬으로 이동합니다.");
        // UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
    }

    #region Event Handlers
    private void SubscribeToEvents()
    {
        GameManager.Addressables.OnDownloadStarted += HandleDownloadStarted;
        GameManager.Addressables.OnProgressUpdated += HandleProgressUpdated;
    }

    private void UnsubscribeFromEvents()
    {
        GameManager.Addressables.OnDownloadStarted -= HandleDownloadStarted;
        GameManager.Addressables.OnProgressUpdated -= HandleProgressUpdated;
    }

    private void HandleDownloadStarted(long totalSize)
    {
        _CCDParent.SetActive(true);
        _progress.fillAmount = 0f;
        _addressableDataText.text = $"0.00 MB / {totalSize / (1024f * 1024f):F2} MB";
        ShowStatus("데이터 다운로드 시작...");
    }

    private void HandleProgressUpdated(float percent, long downloadedBytes, long totalSize)
    {
        _progress.fillAmount = percent;
        _addressableDataText.text = $"{downloadedBytes / (1024f * 1024f):F2} MB / {totalSize / (1024f * 1024f):F2} MB";
    }
    #endregion

    public void OnClickCloseCCDWindow()
    {
        _CCDParent.SetActive(false);
    }

    public async void OnClickClearCache()
    {
         await ClearCache();
    }

    public async Task ClearCache()
    {
        Debug.Log("어드레서블 캐시 삭제 시작...");

        // 1. 현재 로드된 로케이터 정리
        Addressables.ClearResourceLocators();

        // 2. Unity Caching API 사용 (모든 캐시 삭제)
        bool success = Caching.ClearCache();
        if (success)
        {
            Debug.Log("Unity Caching.ClearCache() 성공");
        }
        else
        {
            Debug.Log("Unity Caching.ClearCache() 실패 (다른 작업이 진행 중일 수 있음)");
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void QuitApplication()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowStatus(string message)
    {
        if (_statusMessage != null)
        {
            _statusMessage.text = message;
        }
        Debug.Log(message);
    }
}