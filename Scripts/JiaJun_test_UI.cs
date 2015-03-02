
//#define MEM_DEBUG


using UnityEngine;
using System.Collections;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;
using BTEServer;
using BlueTaleManager;
using System.Text;
using System.Collections.Generic;
using System;
using log4net;
using System.ComponentModel;
using System.Threading;






/// <summary>
/// 实现数据处理，报告manager异常信息
/// </summary>
public class JiaJun_test_UI : MonoBehaviour
{
    BlueTaleEngine bte;

    ServerImpl server;
    BTESocketLibrary bteLibrary;
    AssetBundle assetBundle;

    FileInfo configFile;
    DoneFileDetal doneFileDetal = new DoneFileDetal();//记录生成视频时server的状态情况，
    LitJson.JsonData jsonConfigFileContent;
    Dictionary<string, string> localAudioFileMapper = new Dictionary<string, string>();
    List<string> downloadFile = new List<string>();
    string downloadFileTempDirectory;
    float recordProgress;//报告进度
    BackgroundWorker progressWorker;
    bool isRecordDone = true;//判断录制是否完成，若完成，可以再次接收任务
    int processVideoId = 0;//进行录制的视频id
    string workingjson;
    StreamWriter logWriter;
    public static FileInfo logFileHandle;
    StressTest stressTest = null;
    DateTime startLoadBundleTime;
    bool loadBundleStarted = false;
    FileStream updateMutexStream = null;
    /// <summary>
    /// 线程告诉这里要处理的任务，任务有cmd，和参数
    /// </summary>
    struct Mission
    {
        public BTESTSCommand cmd;
        public object data;
    }
    Queue<Mission> dataQueue = new Queue<Mission>();
    List<string> soundExtentionSport = new List<string>();
    public bool connectToManager = true;
    [HideInInspector]
    public int myServerId;//server自身工作id
    bool bstressTest = false;//若是压力测试，则从类StressTest拿json自动测试，测试函数是StressTestFunc

    STS_RECORD_VIDEO_Struct loadBundleData;
    /// <summary>
    /// 读取json文件
    /// </summary>
    void ReadConfigFile()
    {
        configFile = new FileInfo("server_config.inf");


        if (configFile.Exists == false)
        {
            FileStream fs = configFile.Create();
            fs.Close();
        }

        using (StreamReader sr = new StreamReader(configFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read)))
        {
            string content = sr.ReadToEnd();
            if (string.IsNullOrEmpty(content))
            {
                content = @"{""IPAddress"":""127.0.0.1"",""Port"":5555,""soundExtention"":[{""0"":""wav"",""1"":""mp3"",""2"":""ogg"",""3"":""3gpp""}]}";

            }

            jsonConfigFileContent = LitJson.JsonMapper.ToObject(content);
            if (((IDictionary)jsonConfigFileContent).Contains("soundExtention"))
            {
                for (int i = 0; i < jsonConfigFileContent["soundExtention"].Count; i++)
                {
                    foreach (string kvp in ((IDictionary)jsonConfigFileContent["soundExtention"][i]).Keys)
                    {
                        Debug.Log("support " + jsonConfigFileContent["soundExtention"][i][kvp]);
                        soundExtentionSport.Add((string)(jsonConfigFileContent["soundExtention"][i][kvp]));
                    }
                }
            }
            else
            {
                LogHelper.WriteLog(typeof(JiaJun_test_UI), "warnning: no sound file extention support,load default support sound file extention and have saved config.(wav,mp3,ogg,3gpp)");
                LitJson.JsonData extention = new LitJson.JsonData();
                extention["0"] = "wav";
                extention["1"] = "mp3";
                extention["2"] = "ogg";
                extention["3"] = "3gpp";
                jsonConfigFileContent["soundExtention"] = new LitJson.JsonData();
                jsonConfigFileContent["soundExtention"].SetJsonType(LitJson.JsonType.Array);
                jsonConfigFileContent["soundExtention"].Add(extention);

                for (int i = 0; i < jsonConfigFileContent["soundExtention"].Count; i++)
                {
                    foreach (string kvp in ((IDictionary)jsonConfigFileContent["soundExtention"][i]).Keys)
                    {
                        Debug.Log("support " + jsonConfigFileContent["soundExtention"][i][kvp]);
                        soundExtentionSport.Add((string)(jsonConfigFileContent["soundExtention"][i][kvp]));
                    }
                }
            }

        }

    }
    void LogCallback(string condition, string stackTrace, LogType type)
    {
        string logstring = "";
        switch (type)
        {
            case LogType.Assert:
                logstring = DateTime.Now.ToString("[yyyy-MM-dd hh:mm:ss]") + "[id: " + processVideoId + "]" + "LogType.Assert: " + condition + " " + stackTrace;
                break;
            case LogType.Error:
                logstring = DateTime.Now.ToString("[yyyy-MM-dd hh:mm:ss]") + "[id: " + processVideoId + "]" + "LogType.Error: " + condition + " " + stackTrace;
                break;
            case LogType.Exception:
                logstring = DateTime.Now.ToString("[yyyy-MM-dd hh:mm:ss]") + "[id: " + processVideoId + "]" + "LogType.Exception: " + condition + " " + stackTrace;
                break;
            case LogType.Log:
                stackTrace = "";
                logstring = DateTime.Now.ToString("[yyyy-MM-dd hh:mm:ss]") + "[id: " + processVideoId + "]" + "LogType.Log: " + condition + " " + stackTrace;
                break;
            case LogType.Warning:
                stackTrace = "";
                logstring = DateTime.Now.ToString("[yyyy-MM-dd hh:mm:ss]") + "[id: " + processVideoId + "]" + "LogType.Warning: " + condition + " " + stackTrace;
                break;
            default:
                break;
        }
        if (logWriter != null)
        {
            logWriter.WriteLine(logstring);

            logWriter.Flush();
        }
        if (DebugBridge.instance != null)
        {
            DebugBridge.instance.Logcat(logstring);
        }
    }

    void Awake()
    {

        Console.WriteLine("[build:" + System.IO.File.GetLastWriteTime(this.GetType().Assembly.Location) + "]");
        Debug.Log("[build:" + System.IO.File.GetLastWriteTime(this.GetType().Assembly.Location) + "]");
        ReadConfigFile();
        Screen.SetResolution(480, 270, false);

        DontDestroyOnLoad(gameObject);
        stressTest = GetComponent<StressTest>();
        try
        {
            server = new ServerImpl();
            bteLibrary = new BTESocketLibrary();
            progressWorker = new BackgroundWorker();
            progressWorker.WorkerSupportsCancellation = true;
            progressWorker.DoWork += DoProgressWorkEvent;
            bte = BlueTaleEngine.instance;
            server.sts_record_video_callback += RecordVideo;
            server.sts_returnstatus_callback += ReturnStatus;
            server.sts_server_info_callback += (data) => { myServerId = data.serverId; Console.WriteLine("server id: " + myServerId); CheckNeedLog(); };
            server.sts_stress_test_callback += () =>
                {
                    bstressTest = true;

                    if (stressTest != null)
                    {
                        stressTest.InitJson(myServerId);
                        StressTestFunc();
                    }
                    
                };
            bte.responseEvent += onBTEResponseEvent;
            if (connectToManager)
            {
                bteLibrary.Initialize(server, (string)jsonConfigFileContent["IPAddress"], (int)jsonConfigFileContent["Port"]);
            }

        }
        catch (System.Exception e)
        {
            LogHelper.WriteLog(typeof(JiaJun_test_UI), e);
        }
        Application.RegisterLogCallback(LogCallback);
    }
    /// <summary>
    /// 检测是否要通过日期名称更换log文件路径，避免log文件过大，需要myServerId来命名log文件
    /// </summary>
    void CheckNeedLog()
    {

        if (logWriter != null)
        {
            logWriter.Close();
        }

        DirectoryInfo di = new DirectoryInfo("Log");
        if (di.Exists == false)
        {
            di.Create();
        }
        logFileHandle = new FileInfo("Log/Unity_" + "(" + myServerId + ")" + "_" + DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + ".txt");
        if (logFileHandle.Exists)
        {
            logWriter = new StreamWriter(logFileHandle.Open(FileMode.Append, FileAccess.Write, FileShare.Read));
        }
        else
        {
            logWriter = new StreamWriter(logFileHandle.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read));
        }

    }
    void DoProgressWorkEvent(object sender, DoWorkEventArgs e)
    {
        while (progressWorker.CancellationPending == false)
        {
            try
            {
                GFS_MINSSION_WORK_PERCNET_Struct cmd = new GFS_MINSSION_WORK_PERCNET_Struct();
                cmd.percent = recordProgress;

                if (isRecordDone == false)
                {
                    bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_MINSSION_WORK_PERCNET, cmd);
                }

                Thread.Sleep(3000);
            }
            catch (Exception ex)
            {

                LogHelper.WriteLog(typeof(JiaJun_test_UI), ex);
            }

        }
    }
#if UNITY_EDITOR
    string jobid = "10782";
    string jobJson = "D:\\UserJsons\\replace.json";
    void OnGUI()
    {
        if (bte.isRunning == false)
        {
            jobid = GUILayout.TextField(jobid);
            if (GUILayout.Button("jobid get") || (Event.current.type == EventType.keyDown && Event.current.keyCode == KeyCode.Return))
            {
                StartCoroutine(ServerTestGUICmd());
            }
            jobJson = GUILayout.TextField(jobJson);
            if (GUILayout.Button("open json"))
            {
                FileInfo jsonFile = new FileInfo(jobJson);
                using (StreamReader sr = new StreamReader(jsonFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    string json = sr.ReadToEnd();
                    LitJson.JsonData js = LitJson.JsonMapper.ToObject(json);
                    js["templateNameURL"] = "file:///" + "D:/Bundles/" + (string)js["templateName"] + ".unity3d";
                    js["id"] = (System.Environment.TickCount * myServerId).ToString();
                    Debug.Log(js.ToJson());
                    STS_RECORD_VIDEO_Struct data = new STS_RECORD_VIDEO_Struct()
                    {
                        hasContent = true,
                        jasonContent = js.ToJson()
                    };
                    loadBundleData = data;
                    StartCoroutine("loadBundle");
                    recordProgress = 0;
                }
            }
        }

    }
    IEnumerator ServerTestGUICmd()
    {
        string checkJsonAddress = "http://s1/bteapp/server/Video/checkjson?id=";
        string url = checkJsonAddress + jobid.ToString();
        WWW www = new WWW(url);
        yield return www;
        //Debug.Log(www.text);

        LitJson.JsonData json;
        json = LitJson.JsonMapper.ToObject(www.text);
        json["id"] = jobid.ToString();
        STS_RECORD_VIDEO_Struct data = new STS_RECORD_VIDEO_Struct()
        {
            hasContent = true,
            jasonContent = json.ToJson()
        };
        loadBundleData = data;
        StartCoroutine("loadBundle");
        recordProgress = 0;

    }
#endif
    /// <summary>
    /// 更新是查看队列任务并执行
    /// </summary>
    void Update()
    {
        if (bteLibrary != null)
        {
            bteLibrary.ProcessEvents();
        }
        if (loadBundleStarted == true)
        {
            if ((DateTime.Now - startLoadBundleTime).TotalSeconds > 30)
            {
                Console.WriteLine("StopIt " + (DateTime.Now - startLoadBundleTime).TotalSeconds);
                StopCoroutine("loadBundle");
                if (updateMutexStream != null)
                {
                    updateMutexStream.Close();
                    updateMutexStream = null;
                }
                loadBundleStarted = false;
                Done();
            }
        }
    }
    /// <summary>
    /// bte core回复事件
    /// </summary>
    /// <param name="msg"></param>
    void onBTEResponseEvent(string msg)
    {

        LitJson.JsonData arguments = LitJson.JsonMapper.ToObject(msg);

        recordProgress = ((float)(double)arguments["progress"]) * 0.8f;

        if ((int)arguments["responseCode"] == (int)BlueTaleEngine.ResponseCode.generateVideoRequestSucceeded)
        {
            bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_GENERATEVIDEOREQUESTSUCCEEDED);
            isRecordDone = false;

            try
            {
                Debug.Log("workingjson" + " " + workingjson);
                LitJson.JsonData json = LitJson.JsonMapper.ToObject(workingjson);
                Debug.Log("(string)json[\"templateName\"]" + " " + (string)json["templateName"]);

                doneFileDetal.templateName = (string)json["templateName"];
                doneFileDetal.startTime = DateTime.Now;

                if (stressTest != null)
                {
                    stressTest.StartRecord();
                }
                
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }

        }
        if ((int)arguments["responseCode"] == (int)BlueTaleEngine.ResponseCode.renderingDone)
        {
            doneFileDetal.renderDoneTime = DateTime.Now;
        }

        if ((int)arguments["responseCode"] == (int)BlueTaleEngine.ResponseCode.exception)
        {

            LogHelper.WriteLog(typeof(JiaJun_test_UI), "BlueTaleEngine.ResponseCode.exception：" + "Id= " + processVideoId + " responseCode = " + (int)arguments["responseCode"] + " " + arguments["message"]);

            Done();
            bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_EXCEPTION, new GFS_EXCEPTION_Struct() { reason = "responseCode = " + (int)arguments["responseCode"] + " " + arguments["message"] });


            if (stressTest != null)
            {
                GFS_SERVER_STRESS_TEST_REPORT_Struct reportData = new GFS_SERVER_STRESS_TEST_REPORT_Struct();
                bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_SERVER_STRESS_TEST_REPORT, reportData);
                stressTest.EndRecord();
            }

        }


        if ((int)arguments["responseCode"] == (int)BlueTaleEngine.ResponseCode.generateVideoDone)
        {

            GFS_GENERATEVIDEODONE_Struct doneData = new GFS_GENERATEVIDEODONE_Struct();

            doneData.mp4Path = (string)arguments["filePath"];
            string convert_cmd = FFmpeg.getConvertVideoToWebmCmd(
                doneData.mp4Path,
                "libvpx",
                "1m",
                5,
                0,
                50,
                "libvorbis",
                Path.GetFileNameWithoutExtension(doneData.mp4Path) + ".webm");
            Debug.Log(convert_cmd);
            Cmd.execute(convert_cmd);

            Console.WriteLine("BlueTaleEngine.ResponseCode.generateVideoDone");
            Console.WriteLine("Done Path " + doneData.mp4Path);
            doneData.serverID = myServerId;
            doneData.jobID = processVideoId;
            doneData.templateName = doneFileDetal.templateName;
            doneData.startTime = doneFileDetal.startTime;
            doneData.renderDoneTime = doneFileDetal.renderDoneTime;            
            doneData.endTime = DateTime.Now;
  
            doneData.videoDuration = doneFileDetal.GetVideoDuration(doneData.mp4Path);
            FileInfo doneFileInfo = new FileInfo(doneData.mp4Path);
            doneData.fileSize = doneFileInfo.Length;

            Done();

            GFS_MINSSION_WORK_PERCNET_Struct cmd = new GFS_MINSSION_WORK_PERCNET_Struct();
            cmd.percent = 1;
            bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_MINSSION_WORK_PERCNET, cmd);
            bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_GENERATEVIDEODONE, doneData);

            if (stressTest != null)
            {
                GFS_SERVER_STRESS_TEST_REPORT_Struct reportData = new GFS_SERVER_STRESS_TEST_REPORT_Struct();
                reportData.serverID = myServerId;
                reportData.templateName = doneData.templateName;
                reportData.peakMemory = stressTest.peakMemory;
                reportData.startTime = doneData.startTime;
                reportData.renderDoneTime = doneData.renderDoneTime;
                reportData.endTime = doneData.endTime;
                reportData.fileSize = (int)doneFileInfo.Length;
                reportData.fileName = Path.GetFileName(doneData.mp4Path);
                reportData.videoDuration = doneData.videoDuration;
                bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_SERVER_STRESS_TEST_REPORT, reportData);
                stressTest.EndRecord();
            }

            
        }
    }
    void Done()
    {
        CheckNeedLog();

        //停止发送progress
        if (progressWorker.IsBusy)
        {
            progressWorker.CancelAsync();
        }

        localAudioFileMapper.Clear();
        //任务完成需要删除文件
        DirectoryInfo dir = new DirectoryInfo(System.Environment.CurrentDirectory);
        foreach (var item in downloadFile)
        {
            FileInfo f = new FileInfo(item);
            f.Delete();
        }
        downloadFile.Clear();
        DirectoryInfo tmpfileDir = new DirectoryInfo(System.Environment.CurrentDirectory + "/tempfiles" + processVideoId.ToString());
        if (tmpfileDir.Exists)
        {
            tmpfileDir.Delete(true);
        }
#if false
        foreach (FileInfo f in dir.GetFiles("*.wav"))
        {
            f.Delete();
        }
        foreach (FileInfo f in dir.GetFiles("*.WAV"))
        {
            f.Delete();
        }
        foreach (FileInfo f in dir.GetFiles("*.mp3"))
        {
            f.Delete();
        }
        foreach (FileInfo f in dir.GetFiles("*.MP3"))
        {
            f.Delete();
        }
#endif
        if (assetBundle)
        {
            assetBundle.Unload(true);
            assetBundle = null;
            Console.WriteLine("Unload bundle");
        }
        //加载空场景
        Application.LoadLevel("BTE_Empty");
        //重置标志位
        isRecordDone = true;
        StressTestFunc();
    }

    private void StressTestFunc()
    {
        if (bstressTest)
        {

            if (stressTest != null)
            {
                string sz = stressTest.GetJson();
                Debug.Log("GetJson:" + sz);
                if (sz == "")
                {
                    Console.WriteLine("send GFS_SERVER_STRESS_TEST");
                    bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_SERVER_STRESS_TEST_DONE);
                    stressTest.ResetJson();
                }
                else
                {
                    STS_RECORD_VIDEO_Struct data = new STS_RECORD_VIDEO_Struct() { hasContent = true, jasonContent = sz };
                    RecordVideo(data);
                }
            }

        }
    }
    /// <summary>
    /// manager告诉录制视频(线程)
    /// </summary>
    /// <param name="param"></param>

    void RecordVideo(STS_RECORD_VIDEO_Struct data)
    {
        if (isRecordDone)
        {
            Console.WriteLine("111111");
            LitJson.JsonData json;
            json = LitJson.JsonMapper.ToObject(data.jasonContent);
            if (((IDictionary)json).Contains("resolutionForce"))
            {
                Debug.Log(json["resolutionForce"][0] + " " + json["resolutionForce"][1]);
                Screen.SetResolution((int)json["resolutionForce"][0], (int)json["resolutionForce"][1], false);
            }
            else
            {
                Screen.SetResolution(480, 270, false);
            }
            startLoadBundleTime = DateTime.Now;
            loadBundleData = data;
            Console.WriteLine("121212121221");
            StartCoroutine("loadBundle");
            recordProgress = 0;
            Console.WriteLine("22222222");
        }
        else
        {
            Console.WriteLine("bte isRunning, command deny");
            bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_EXCEPTION, new GFS_EXCEPTION_Struct() { reason = "bte isRunning, command deny" });
        }
    }
    /// <summary>
    /// manager告诉返回状态(线程)
    /// </summary>

    void ReturnStatus()
    {
        bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_MINSSION_WORK_PERCNET, new GFS_MINSSION_WORK_PERCNET_Struct() { percent = recordProgress });
    }



    void CorrectDownloadedFile(LitJson.JsonData data, LitJson.JsonData change_data)
    {
        try
        {
            if (data.GetJsonType() == LitJson.JsonType.Object)
            {
                foreach (string key in ((IDictionary)data).Keys)
                {
                    if (data[key].IsArray)
                    {

                        LitJson.JsonData json_audio_array = data[key];
                        LitJson.JsonData change_json_audio_array = change_data[key];
                        for (int i = 0; i < json_audio_array.Count; i++)
                        {
                            CorrectDownloadedFile(json_audio_array[i], change_json_audio_array[i]);
                        }
                    }
                    if (data[key].IsString)
                    {
                        string http_file = (string)(data[key]);
                        if (http_file.StartsWith("http:") || http_file.StartsWith("file:"))
                        {
                            foreach (var item in soundExtentionSport)
                            {
                                string extention_low = "." + item.ToLower();
                                string extention_up = "." + item.ToUpper();

                                if (http_file.EndsWith(extention_low) || http_file.EndsWith(extention_up))
                                {
                                    string localfileExtention = Path.GetExtension(http_file);
                                    if (localfileExtention != "wav" || localfileExtention != "WAV")
                                    {
                                        change_data[key] = "file:///" + Path.Combine(System.Environment.CurrentDirectory + "/" + downloadFileTempDirectory, Path.GetFileNameWithoutExtension(http_file) + ".wav");
                                    }
                                    else
                                    {
                                        change_data[key] = "file:///" + Path.Combine(System.Environment.CurrentDirectory + "/" + downloadFileTempDirectory, Path.GetFileName(http_file));
                                    }

                                    Debug.Log(change_data[key]);
                                    //localAudioFileMapper记录文件路径，递归后进行下载
                                    localAudioFileMapper.Add((string)(data[key]), downloadFileTempDirectory + "/" + Path.GetFileName(http_file));//服务器文件，本地文件名绑定
                                }
                            }
                        }

                    }
                }
            }

        }
        catch (Exception ex)
        {
            throw ex;
        }


    }

    /// <summary>
    /// loadBundle是加载资源，所以处理的事情比较多
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    IEnumerator loadBundle()
    {
        loadBundleStarted = true;

        LitJson.JsonData json = null;
        if (loadBundleData.hasContent)//如果是以内容的形式发送过来
        {
            json = LitJson.JsonMapper.ToObject(loadBundleData.jasonContent);
            Debug.Log(loadBundleData.jasonContent);
        }
        Console.WriteLine("qqqqqqqqq");
        //json文件阅读成功
        if (json != null)
        {
            LitJson.JsonData btejson = LitJson.JsonMapper.ToObject(json.ToJson());//将网络json转化为本地json


            if (((IDictionary)btejson).Contains("id"))
            {
                processVideoId = int.Parse(((string)btejson["id"]));
                Console.WriteLine("job id: " + processVideoId);
            }
            downloadFileTempDirectory = "tempfiles" + processVideoId.ToString();

            DirectoryInfo tmpfileDir = new DirectoryInfo(System.Environment.CurrentDirectory + "/" + downloadFileTempDirectory);
            if (tmpfileDir.Exists == false)
            {
                tmpfileDir.Create();

            }

            try
            {

                CorrectDownloadedFile(json, btejson);
            }
            catch (Exception ex)
            {
                bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_EXCEPTION, new GFS_EXCEPTION_Struct() { reason = "json cant be load" });
                Done();
                LogHelper.WriteLog(typeof(JiaJun_test_UI), ex);
                loadBundleStarted = false;
                yield break;
            }

            Debug.Log(btejson.ToJson());
            Console.WriteLine("ddddddddddddddddd");
            //下载音频文件到本地
            foreach (KeyValuePair<string, string> pair in localAudioFileMapper)
            {
                Console.WriteLine("load " + pair.Key);//key is the file in upload directory
                WWW downloadAudio = new WWW(pair.Key);
                yield return downloadAudio;
                if (downloadAudio.size > 0)
                {
                    Console.WriteLine(pair.Key + " download size " + downloadAudio.size);
                    FileInfo fi = new FileInfo(pair.Value);
                    BinaryWriter bw = new BinaryWriter(fi.Open(FileMode.Create));
                    bw.Write(downloadAudio.bytes);
                    bw.Close();

                    string localfile = pair.Value;
                    downloadFile.Add(localfile);
                    string localWavFile = localfile;
                    string localfileExtention = Path.GetExtension(pair.Key);
                    Console.WriteLine("localfileExtention " + localfileExtention);
                    if (localfileExtention != ".wav" && localfileExtention != ".WAV")//check extention
                    {
                        localWavFile = downloadFileTempDirectory + "/" + Path.GetFileNameWithoutExtension(localfile) + ".wav";
                        string cmd = string.Format("ffmpeg -y -i \"{0}\" -acodec pcm_u8 -ar 44100 \"{1}\"",
                                                    localfile,
                                                    localWavFile);
                        Console.WriteLine(cmd);
                        Cmd.execute(cmd);
                        downloadFile.Add(localWavFile);
                    }
                }
                else
                {
                    LogHelper.WriteLog(typeof(JiaJun_test_UI), "download audio fail ,size is 0. " + pair.Key);
                    bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_EXCEPTION, new GFS_EXCEPTION_Struct() { reason = "audio file cant be load" });
                    Done();
                    loadBundleStarted = false;
                    yield break;
                }
            }


            Console.WriteLine("8888888888888888888888");
            string bundleFile = "";
            if (((IDictionary)btejson).Contains("templateNameURL"))
            {
                bundleFile = (string)btejson["templateNameURL"];

                DirectoryInfo bundleDir = new DirectoryInfo(System.Environment.CurrentDirectory + "/bundles");
                if (bundleDir.Exists == false)
                {
                    bundleDir.Create();
                }
                string saveBundlePath = System.Environment.CurrentDirectory + "/bundles/" + Path.GetFileName(bundleFile);


                bool needLoadBundle = true;

                //下载Bundle
                //第一步，检测md5码，本地md5是否与json对应，如果对应则不需要下载
                string md5 = GetBundleMd5(saveBundlePath);
                if (string.IsNullOrEmpty(md5) == false)
                {
                    if (((IDictionary)btejson).Contains("bundleMD5"))
                    {
                        if (md5 == (string)btejson["bundleMD5"])
                        {
                            needLoadBundle = false;
                        }
                    }
                }
                FileInfo updateMutexFileInfo = new FileInfo(System.Environment.CurrentDirectory + "/bundles/" + "update" + Path.GetFileNameWithoutExtension(bundleFile));
               
                //第二步，下载bundke
                if (needLoadBundle)
                {
                    //如果此时updateBundle文件打不开，就不停访问此文件，直到能访问打开为止，这步是防止多个实例同时下载同一Bundle,所造成的文件冲突异常
                    while (true)
                    {
                        try
                        {
                            
                            updateMutexStream = updateMutexFileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                            break;
                        }
                        catch (Exception)
                        {
                            //出现异常说明文件被另外的实例下载，则不需要下载bundle
                            needLoadBundle = false;
                            Thread.Sleep(1000);
                        }
                    }
                }
                if (needLoadBundle)
                {
                    Debug.Log("load bundle " + bundleFile);
                    Console.WriteLine("load bundle " + bundleFile);

                   
                    

                    WWW www = new WWW(bundleFile);
                    //todo 没有bundle
                    yield return www;
                    Console.WriteLine("load bundle finish");
                    assetBundle = www.assetBundle;

                    //2015-3-2
                    //FileInfo fi = new FileInfo(System.Environment.CurrentDirectory + "/bundles/" + "lock" + Path.GetFileNameWithoutExtension(bundleFile));
                    try
                    {
                        //2015-3-2
                        //FileStream fs = fi.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        BinaryWriter bw = new BinaryWriter(new FileInfo(saveBundlePath).OpenWrite());
                        bw.Write(www.bytes);
                        bw.Close();
                        CreateBundleMd5(saveBundlePath);
                        //2015-3-2
                        //fs.Close();

                    }
                    catch (Exception)
                    {
                        Console.WriteLine("lock bundle");
                    }
                    
                    www.Dispose();
                    www = null;
                }

                if (updateMutexStream != null)
                {
                    updateMutexStream.Close();
                    updateMutexStream = null;
                }


                if (assetBundle == null)
                {

                    bundleFile = "file:///" + System.Environment.CurrentDirectory + "/bundles/" + Path.GetFileName(bundleFile);
                    Console.WriteLine("try load bundle " + bundleFile);
                    Debug.Log("try load bundle " + bundleFile);
                    WWW www2 = new WWW(bundleFile);
                    //todo 没有bundle
                    yield return www2;

                    assetBundle = www2.assetBundle;
                    www2.Dispose();
                    www2 = null;
                    if (assetBundle == null)
                    {
                        bteLibrary.SendWithLength((int)BlueTaleManager.BTEGFSCommand.GFS_EXCEPTION, new GFS_EXCEPTION_Struct() { reason = "assetBundle cant be load" });

                        LogHelper.WriteLog(typeof(JiaJun_test_UI), "download bundle error:" + bundleFile);
                        Done();
                        loadBundleStarted = false;
                        yield break;
                    }
                    else
                    {
                        progressWorker.RunWorkerAsync();
                    }
                }
                else
                {
                    progressWorker.RunWorkerAsync();
                }
            }
            workingjson = btejson.ToJson();
            btejson["requestCode"] = (int)BlueTaleEngine.RequestCode.generateVideo;

            bte.request(btejson.ToJson());
            loadBundleStarted = false;

        }
    }
    string GetBundleMd5(string bundle)
    {
        string md5 = "";

        FileInfo fi = new FileInfo(bundle.Replace(".unity3d", ".md5txt"));
        if (fi.Exists)
        {
            StreamReader sr = new StreamReader(fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            md5 = sr.ReadToEnd();
            sr.Close();
        }

        return md5;
    }
    void CreateBundleMd5(string bundle)
    {

        FileInfo fi = new FileInfo(bundle.Replace(".unity3d", ".md5txt"));
        using (StreamWriter sw = new StreamWriter(fi.Open(FileMode.OpenOrCreate)))
        {
            sw.WriteLine(GetMD5HashFromFile(bundle));
        }

    }
    private string GetMD5HashFromFile(string fileName)
    {
        try
        {
            FileStream file = new FileStream(fileName, FileMode.Open);
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(file);
            file.Close();

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            LogHelper.WriteLog(typeof(JiaJun_test_UI), ex);
            return "";
        }
    }

    void OnApplicationQuit()
    {
        if (progressWorker != null)
        {
            if (progressWorker.CancellationPending == false)
            {
                progressWorker.CancelAsync();
            }
        }
        
        if (bteLibrary != null)
        {
            bteLibrary.Exit();
        }
        DirectoryInfo dir = new DirectoryInfo(System.Environment.CurrentDirectory);

        Debug.Log("Quit");
        logWriter.Close();
    }
}

