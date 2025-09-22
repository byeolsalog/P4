using UnityEngine;
using System.Net.Http;
using System.Threading.Tasks;

// 버전 체크 결과를 나타내는 열거형
public enum VersionCheckResult
{
    UpToDate,         // 최신 버전
    OptionalUpdate,   // 선택 업데이트 가능
    ForceUpdate,      // 강제 업데이트 필요
    Failed            // 버전 체크 실패
}

public class VersionManager
{
    private static readonly HttpClient httpClient = new HttpClient();
    private const string ServerUrl = "http://3.24.195.47:5020/version/Android";

    public string LatestVersion { get; private set; }
    public string Notice { get; private set; }

    public async Task<VersionCheckResult> CheckVersionAsync()
    {
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(ServerUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            VersionResponse data = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionResponse>(json);

            if (data == null || string.IsNullOrEmpty(data.minVersion))
            {
                Debug.LogError("서버에서 버전 정보를 읽을 수 없습니다.");
                return VersionCheckResult.Failed;
            }

            LatestVersion = data.latestVersion;
            Notice = data.notice;
            string localVersion = Application.version;

            if (IsLowerVersion(localVersion, data.minVersion))
            {
                Debug.LogError("앱 버전이 낮아 강제 업데이트가 필요합니다.");
                return VersionCheckResult.ForceUpdate;
            }

            if (IsLowerVersion(localVersion, data.latestVersion))
            {
                return data.forceUpdate ? VersionCheckResult.ForceUpdate : VersionCheckResult.OptionalUpdate;
            }

            return VersionCheckResult.UpToDate;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"버전 체크 중 오류 발생: {ex.Message}");
            return VersionCheckResult.Failed;
        }
    }

    private bool IsLowerVersion(string local, string target)
    {
        var lv = new System.Version(local);
        var tv = new System.Version(target);
        return lv.CompareTo(tv) < 0;
    }

    [System.Serializable]
    private class VersionResponse
    {
        public string latestVersion;
        public string minVersion;
        public bool forceUpdate;
        public string notice;
    }
}