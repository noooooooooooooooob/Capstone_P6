using UnityEngine;

public static class DeviceChecker
{
    /// <summary>
    /// Checks if the current hardware running the application is a Meta Quest 3 or Quest 3S.
    /// Used to determine if depth-based Mixed Reality features (like MR Scene Setup) should be safely run.
    /// </summary>
    public static bool IsQuest3()
    {
#if UNITY_EDITOR
        // Allow Quest 3 features in Editor when testing with Link / dummy logic
        return true; 
#endif
        
        string model = SystemInfo.deviceModel.ToLower();
        
        // Example device model strings for Quest 3 / Quest 3S usually contain "quest 3" or similar identifiers provided by Android OS
        if (model.Contains("quest 3"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the current hardware is a Meta Quest 2.
    /// </summary>
    public static bool IsQuest2()
    {
        string model = SystemInfo.deviceModel.ToLower();
        return model.Contains("quest 2");
    }
}
