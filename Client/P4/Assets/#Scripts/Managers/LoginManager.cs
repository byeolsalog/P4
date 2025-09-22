using Firebase;
using Firebase.Auth;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using System.Threading.Tasks;
using UnityEngine;

public class LoginManager
{
    private FirebaseAuth _auth;
    private FirebaseUser _user;

    public async Task<bool> InitializeAsync()
    {
        try
        {
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError($"Firebase �ʱ�ȭ ����: {dependencyStatus}");
                return false;
            }

            _auth = FirebaseAuth.DefaultInstance;
            PlayGamesPlatform.Activate(); // 2.1.0������ Config ���ŵ�

            Debug.Log("Firebase �� GPGS �ʱ�ȭ ����");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"�ʱ�ȭ �� ���� �߻�: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SignInAnonymouslyAsync()
    {
        if (_auth.CurrentUser != null) return true;

        try
        {
            await _auth.SignInAnonymouslyAsync();
            _user = _auth.CurrentUser;
            Debug.Log($"Firebase �͸� �α��� ����: ({_user.UserId})");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Firebase �͸� �α��� ����: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SignInWithGpgsAsync()
    {
        if (_auth.CurrentUser != null && !_auth.CurrentUser.IsAnonymous)
        {
            Debug.Log("�̹� GPGS �������� �α��εǾ� �ֽ��ϴ�.");
            return true;
        }

        try
        {
            string serverAuthCode = await GetGpgsServerAuthCode();
            if (string.IsNullOrEmpty(serverAuthCode)) return false;

            Credential credential = PlayGamesAuthProvider.GetCredential(serverAuthCode);

            if (_auth.CurrentUser != null && _auth.CurrentUser.IsAnonymous)
            {
                Debug.Log("���� �͸� ������ GPGS �������� �����մϴ�...");
                await _auth.CurrentUser.LinkWithCredentialAsync(credential);
                _user = _auth.CurrentUser;
                Debug.Log($"���� ���� ����: {_user.DisplayName} ({_user.UserId})");
            }
            else
            {
                Debug.Log("GPGS �������� Firebase�� ���� �α����մϴ�...");
                await _auth.SignInWithCredentialAsync(credential);
                _user = _auth.CurrentUser;
                Debug.Log($"Firebase �α��� ����: {_user.DisplayName} ({_user.UserId})");
            }
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"GPGS/Firebase ���� ����: {ex.Message}");
            return false;
        }
    }

    private Task<string> GetGpgsServerAuthCode()
    {
        var tcs = new TaskCompletionSource<string>();

        void RequestAuthCode()
        {
            PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
            {
                if (!string.IsNullOrEmpty(code))
                {
                    Debug.Log($"[GPGS] ServerAuthCode ȹ�� ����: {code.Substring(0, 10)}...");
                    tcs.TrySetResult(code);
                }
                else
                {
                    Debug.LogError("[GPGS] ServerAuthCode ȹ�� ���� (null or empty)");
                    tcs.TrySetException(new System.Exception("GPGS Server Auth Code is null or empty."));
                }
            });
        }

        if (PlayGamesPlatform.Instance.IsAuthenticated())
        {
            RequestAuthCode();
        }
        else
        {
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status == SignInStatus.Success)
                {
                    Debug.Log("[GPGS] �α��� ����, ServerAuthCode ��û �õ�...");
                    RequestAuthCode();
                }
                else
                {
                    Debug.LogError($"[GPGS] �α��� ����: {status}");
                    tcs.TrySetException(new System.Exception($"GPGS Sign-In failed with status: {status}"));
                }
            });
        }

        return tcs.Task;
    }
}