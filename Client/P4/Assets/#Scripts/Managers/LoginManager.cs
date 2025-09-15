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
        UnityServices.Initialized += () => Debug.Log("UGS �ʱ�ȭ �Ϸ�");
        UnityServices.InitializeFailed += (ctx) => Debug.Log($"UGS �ʱ�ȭ ����: {ctx.Message}");

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

            Debug.Log("�α��� ����");
            Debug.Log($"Player ID : {AuthenticationService.Instance.PlayerId}");
            Debug.Log($"Player Name : {playerName}");
            Debug.Log($"Player AccessToken : {AuthenticationService.Instance.AccessToken}");
        };

        AuthenticationService.Instance.SignedOut += () => Debug.Log("�α׾ƿ�");
        AuthenticationService.Instance.Expired += () => Debug.Log("���� ����");
    }
}
