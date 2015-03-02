using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

public class ExtraSettings
{

    [MenuItem("Extra Settings/Show MT Rendering Status")]
    static void ShowMTRenderingStatus()
    {
        UnityEditor.EditorUtility.DisplayDialog("MT Rendering Status",
            (UnityEditor.PlayerSettings.MTRendering)
            ? "Multi-Threading Rendering is currently ON"
            : "Multi-Threading Rendering is currently OFF"
            , "OK");
    }
    [MenuItem("Extra Settings/Enable MT Rendering")]
    static void EnableMTRendering()
    {
        UnityEditor.PlayerSettings.MTRendering = true;
    }

    [MenuItem("Extra Settings/Disable MT Rendering")]
    static void DisableMTRendering()
    {
        UnityEditor.PlayerSettings.MTRendering = false;
    }
}

[AddComponentMenu("Transform/Follow Transform")]
public class FollowTransform : MonoBehaviour
{
}
#endif