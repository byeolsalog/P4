using UnityEngine;
using System.Net.Http;
using System.Threading.Tasks;

// ���� üũ ����� ��Ÿ���� ������
public enum VersionCheckResult
{
    UpToDate,         // �ֽ� ����
    OptionalUpdate,   // ���� ������Ʈ ����
    ForceUpdate,      // ���� ������Ʈ �ʿ�
    Failed            // ���� üũ ����
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
                Debug.LogError("�������� ���� ������ ���� �� �����ϴ�.");
                return VersionCheckResult.Failed;
            }

            LatestVersion = data.latestVersion;
            Notice = data.notice;
            string localVersion = Application.version;

            if (IsLowerVersion(localVersion, data.minVersion))
            {
                Debug.LogError("�� ������ ���� ���� ������Ʈ�� �ʿ��մϴ�.");
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
            Debug.LogError($"���� üũ �� ���� �߻�: {ex.Message}");
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