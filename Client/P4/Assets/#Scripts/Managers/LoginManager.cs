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
                Debug.LogError($"Firebase 초기화 실패: {dependencyStatus}");
                return false;
            }

            _auth = FirebaseAuth.DefaultInstance;
            PlayGamesPlatform.Activate(); // 2.1.0에서는 Config 제거됨

            Debug.Log("Firebase 및 GPGS 초기화 성공");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"초기화 중 예외 발생: {ex.Message}");
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
            Debug.Log($"Firebase 익명 로그인 성공: ({_user.UserId})");
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Firebase 익명 로그인 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SignInWithGpgsAsync()
    {
        if (_auth.CurrentUser != null && !_auth.CurrentUser.IsAnonymous)
        {
            Debug.Log("이미 GPGS 계정으로 로그인되어 있습니다.");
            return true;
        }

        try
        {
            string serverAuthCode = await GetGpgsServerAuthCode();
            if (string.IsNullOrEmpty(serverAuthCode)) return false;

            Credential credential = PlayGamesAuthProvider.GetCredential(serverAuthCode);

            if (_auth.CurrentUser != null && _auth.CurrentUser.IsAnonymous)
            {
                Debug.Log("기존 익명 계정을 GPGS 계정으로 연동합니다...");
                await _auth.CurrentUser.LinkWithCredentialAsync(credential);
                _user = _auth.CurrentUser;
                Debug.Log($"계정 연동 성공: {_user.DisplayName} ({_user.UserId})");
            }
            else
            {
                Debug.Log("GPGS 계정으로 Firebase에 새로 로그인합니다...");
                await _auth.SignInWithCredentialAsync(credential);
                _user = _auth.CurrentUser;
                Debug.Log($"Firebase 로그인 성공: {_user.DisplayName} ({_user.UserId})");
            }
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"GPGS/Firebase 연동 실패: {ex.Message}");
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
                    Debug.Log($"[GPGS] ServerAuthCode 획득 성공: {code.Substring(0, 10)}...");
                    tcs.TrySetResult(code);
                }
                else
                {
                    Debug.LogError("[GPGS] ServerAuthCode 획득 실패 (null or empty)");
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
                    Debug.Log("[GPGS] 로그인 성공, ServerAuthCode 요청 시도...");
                    RequestAuthCode();
                }
                else
                {
                    Debug.LogError($"[GPGS] 로그인 실패: {status}");
                    tcs.TrySetException(new System.Exception($"GPGS Sign-In failed with status: {status}"));
                }
            });
        }

        return tcs.Task;
    }
}