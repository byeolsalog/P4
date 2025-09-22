using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressablesManager
{
    private bool _isInitialized = false;
    public List<string> labels = new List<string>() { "TestLabel" };

    public event Action<long> OnDownloadStarted;
    public event Action<float, long, long> OnProgressUpdated;
    public event Action<bool> OnDownloadCompleted;

    /// <summary>
    /// Addressables 시스템을 초기화합니다.
    /// </summary>
    public async Task InitAddressables()
    {
        if (_isInitialized)
        {
            Debug.Log("Addressables 이미 초기화됨");
            return;
        }

        var handle = Addressables.InitializeAsync();
        handle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log("Addressables 초기화 성공");
                _isInitialized = true;
            }
            else
            {
                Debug.LogError($"Addressables 초기화 실패: {op.OperationException}");
            }
        };

        await handle.Task;        
    }

    /// <summary>
    /// 모든 종속성을 다운로드합니다.
    /// </summary>
    public async Task<bool> DownloadAllDependenciesAsync()
    {
        // 다운로드할 크기를 먼저 확인합니다.
        bool success = false;
        try
        {
            long totalDownloadSize = await GetTotalDownloadSize(labels);

            if (totalDownloadSize <= 0)
            {
                Debug.Log("다운로드할 에셋이 없습니다. (이미 캐시됨)");
                OnDownloadCompleted?.Invoke(true);
                return true;
            }

            OnDownloadStarted?.Invoke(totalDownloadSize);

            var downloadHandle = Addressables.DownloadDependenciesAsync(labels, Addressables.MergeMode.Union, true);
            downloadHandle.Completed += (op) =>
            {
                success = op.Status == AsyncOperationStatus.Succeeded;
                if (success) Debug.Log("모든 에셋 다운로드 성공");
                else Debug.LogError($"에셋 다운로드 실패: {op.OperationException}");

                OnDownloadCompleted?.Invoke(success);
            };

            // 진행률 추적을 위해 while 루프 사용
            while (!downloadHandle.IsDone)
            {
                float percent = downloadHandle.PercentComplete;
                long downloadedBytes = (long)(totalDownloadSize * percent);
                OnProgressUpdated?.Invoke(percent, downloadedBytes, totalDownloadSize);
                await Task.Yield();
            }

            Debug.Log($"결과 : {success}");
            return success;
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 다운로드 크기를 확인합니다.
    /// </summary>
    private async Task<long> GetTotalDownloadSize(List<string> labelsToCheck)
    {
        var sizeHandle = Addressables.GetDownloadSizeAsync(labelsToCheck);
        long downloadSize = -1;

        // Completed 이벤트를 사용하여 결과를 안전하게 저장합니다.
        sizeHandle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                downloadSize = op.Result;
            }
            else
            {
                Debug.LogError($"다운로드 크기 확인 실패: {op.OperationException}");
            }
            // 결과를 얻었으므로 핸들을 해제합니다.
            try
            {
                Addressables.Release(op);
            }
            catch (Exception e)
            {
                Debug.LogError($"어드레서블 release 실패 : {e.Message}");
            }            
        };

        await sizeHandle.Task;
        return downloadSize;
    }
}