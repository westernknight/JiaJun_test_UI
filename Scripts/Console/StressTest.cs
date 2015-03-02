using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;


/*  添加StressTest 组件
 *  当manager发送STS_SERVER_STRESS_TEST消息过来
 *  则自动从D:\\UserJsons读取json 进行测试
 * 
 * 
 */
public class StressTest : MonoBehaviour
{

    List<string> debugJsons = new List<string>();

    Queue<string> processJsons = new Queue<string>();

    [HideInInspector]
    public int peakMemory;
    void Awake()
    {

    }
    public void InitJson(int serverId)
    {
        debugJsons.Clear();
        DirectoryInfo di = new DirectoryInfo("D:\\UserJsons");
        FileInfo[] fi = di.GetFiles();
        foreach (var item in fi)
        {
            if (Path.GetExtension(item.Name) == ".json")
            {
                using (StreamReader sr = new StreamReader( item.Open(FileMode.Open,FileAccess.Read,FileShare.Read   )))
                {
                    string json = sr.ReadToEnd();
                    LitJson.JsonData js = LitJson.JsonMapper.ToObject(json);
                    js["templateNameURL"] = "file:///" + "D:/Bundles/" + (string)js["templateName"] + ".unity3d";
                    js["id"] = (System.Environment.TickCount * serverId).ToString();
                    Debug.Log(js.ToJson());
                    debugJsons.Add(js.ToJson());
                }
            }
        }
        ResetJson();
    }
    public string GetJson()
    {
        if (processJsons.Count > 0)
        {
            return processJsons.Dequeue();
        }
        return "";
    }
    public void ResetJson()
    {
        processJsons.Clear();
        foreach (var item in debugJsons)
        {
            processJsons.Enqueue(item);
        }
    }


    IEnumerator Checking()
    {
        while (true)
        {
            yield return new WaitForSeconds(1);
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo ps = new System.Diagnostics.ProcessStartInfo();
            ps.FileName = "cmd";
            ps.Arguments = string.Format(@"/c @echo off && for /f ""tokens=5"" %i in ('tasklist /NH /FI ""PID eq {0}""') do echo %i ", System.Diagnostics.Process.GetCurrentProcess().Id);
            ps.UseShellExecute = false;
            ps.RedirectStandardOutput = true;
            ps.CreateNoWindow = true;
            p.StartInfo = ps;
            p.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    int catchMem = int.Parse(e.Data.Replace(",", ""));
                    if (catchMem > peakMemory)
                    {
                        peakMemory = catchMem;
                    }
                }

            };
            p.Start();
            p.BeginOutputReadLine();
            p.WaitForExit();

        }
    }
    public void StartRecord()
    {

        StartCoroutine("Checking");
        peakMemory = 0;

    }
    public void EndRecord()
    {
        StopCoroutine("Checking");
    }

}
