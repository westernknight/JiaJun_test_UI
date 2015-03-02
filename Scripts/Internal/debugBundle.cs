using UnityEngine;
using System.Collections;
using System.Diagnostics;
using System.IO;

public class debugBundle : MonoBehaviour
{

    AssetBundle assetBundle;
    int pressCount = 0;
    bool startdebug = false;
    string debugBundleName = "FR_News";
    string debugLevelName = "FR_News";
    string debugStopAt = "30";
    string debugDescribe = "describe";
    string debuglog = "memory_log.txt";
    FileStream fileStream;

    BlueTaleEngine bte;

    string debugjson =  @" {""templateName"":""FR_News"",""templateNameURL"":""http://192.168.0.195/bte/Public/template/FR_News.unity3d"",""sequence"":[{""dataType"":""simple""},{""dataType"":""simple"",""action"":""BeQuiet""},{""dataType"":""simple""}],""id"":""242""} ";
    void Awake()
    {
        Screen.SetResolution(480, 270, false);
        DontDestroyOnLoad(gameObject);
        FileInfo fi = new FileInfo(debugBundleName + ".txt");

        bte = BlueTaleEngine.instance;
        bte.responseEvent += (msg) =>
            {
                LitJson.JsonData arguments = LitJson.JsonMapper.ToObject(msg);
                if ((int)arguments["responseCode"] == (int)BlueTaleEngine.ResponseCode.generateVideoDone)
                {
                    StartCoroutine(willdone());
                }
                
            };

    }

    void OnGUI()
    {
        GUILayout.Label("bundle_name");
        debugBundleName = GUILayout.TextField(debugBundleName);
        GUILayout.Label("level_name");
        debugLevelName = GUILayout.TextField(debugLevelName);
        debugDescribe = GUILayout.TextField(debugDescribe);
        debugStopAt = GUILayout.TextField(debugStopAt);
        if (startdebug == true)
        {

            string msg = "stop debug " + "debuged: " + pressCount;
            if (GUILayout.Button(msg))
            {
                startdebug = false;
            }
        }
        else
        {
            string msg = "start debug " + "debuged: " + pressCount;
            if (GUILayout.Button(msg))
            {
                startdebug = true;

                StartCoroutine(ie());
               
            }
        }

    }
    IEnumerator ie()
    {
        WWW www = new WWW("file:///" + System.Environment.CurrentDirectory + "/bundles/" + debugBundleName + ".unity3d");
        yield return www;
        assetBundle = www.assetBundle;
        www.Dispose();
        www = null;

        LitJson.JsonData json;
        json = LitJson.JsonMapper.ToObject(debugjson);
        json["requestCode"] = (int)BlueTaleEngine.RequestCode.generateVideo;
        bte.request(json.ToJson());
        //Application.LoadLevel(debugLevelName);
        //StartCoroutine(willdone());
    }
    IEnumerator willdone()
    {
        yield return new WaitForSeconds(1);
        Application.LoadLevel("BTE_Empty");

        if (assetBundle)
        {
            assetBundle.Unload(true);
            assetBundle = null;
        }
        pressCount++;
        if (startdebug)
        {
            if (pressCount == int.Parse(debugStopAt))
            {
                startdebug = false;
            }
            else
            {
                StartCoroutine(ie());
            }
        }
        if (true)
        {
            string cmd = string.Format("/c tasklist|find \"{0}\">>\"{1}\"", Process.GetCurrentProcess().ProcessName, debugBundleName + ".txt");
            Process.Start("cmd", cmd);
            //string cmd = string.Format("/c ping /n 15 127.0.0.1");
            //Process p = new Process();
            //ProcessStartInfo ps = new ProcessStartInfo();
            //ps.FileName = "cmd";
            //ps.Arguments = cmd;
            //ps.UseShellExecute = false;
            //ps.RedirectStandardOutput = true;
            //p.StartInfo = ps;
            //p.Start();
            //string result = p.StandardOutput.ReadToEnd();
            //p.WaitForExit();
            //UnityEngine.Debug.Log("here");
            //FileInfo fi = new FileInfo(debugBundleName + ".txt");
            //fileStream = fi.Open(FileMode.Append, FileAccess.Write,FileShare.Read);
            //using (StreamWriter sw = new StreamWriter(fileStream))
            //{
            //    sw.Write(result);
            //}

        }

    }
}
