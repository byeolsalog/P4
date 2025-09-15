using UnityEngine;

public class Utils
{
    
}

public static class Debug
{
    public static void Log(string msg)
    {
        UnityEngine.Debug.Log(msg);
    }

    public static void LogError(string msg)
    {
        UnityEngine.Debug.LogError(msg);
    }
}
