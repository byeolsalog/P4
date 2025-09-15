using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using UnityEngine;
using System;

public class LoginManager
{
    public async Task InitUnityService()
    {
        UnityServices.Initialized += () => Debug.Log("UGS 초기화 완료");
        UnityServices.InitializeFailed += (ctx) => Debug.Log($"UGS 초기화 실패: {ctx.Message}");

        try
        {
            await UnityServices.InitializeAsync();
        }
        catch (ServicesInitializationException ex)
        {
            Debug.LogError(ex.Message);
        }
        
        AuthenticationService.Instance.SignedIn += async () =>
        {
            var playerName = await AuthenticationService.Instance.GetPlayerNameAsync();

            Debug.Log("로그인 성공");
            Debug.Log($"Player ID : {AuthenticationService.Instance.PlayerId}");
            Debug.Log($"Player Name : {playerName}");
            Debug.Log($"Player AccessToken : {AuthenticationService.Instance.AccessToken}");
        };

        AuthenticationService.Instance.SignedOut += () => Debug.Log("로그아웃");
        AuthenticationService.Instance.Expired += () => Debug.Log("세션 만료");
    }
}
