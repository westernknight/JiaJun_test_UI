// using UnityEngine;
// using System.Collections;
// using System;
// using System.Diagnostics;
// using System.IO;
// using GpuzDemo;
// 
// 
// /*
//  *  在本地生成ComputerStatus+serverid的记事本，记录生成视频数据
//  * 
//  * 
//  * 
//  * 
//  * 
//  * */
// public class ComputerStatus : MonoBehaviour
// {
//     GpuzWrapper gpuz = new GpuzWrapper();
//     void Awake()
//     {
//        
//     }
// 
//     int peakMemory;
//     DateTime startTime;
//     DateTime renderDoneTime;
//     DateTime endTime;
//     string templateName;
//     string _lastInfo;
//     public bool useGpuCatch = false;
//     double graphicsMemUse;
//     double gpuLoad;
//     public string LastInfo
//     {
//         get { return _lastInfo; }
//     }
// 
// 
//     IEnumerator Checking()
//     {
//         while (true)
//         {
//             yield return new WaitForSeconds(1);
//             Process p = new Process();
//             ProcessStartInfo ps = new ProcessStartInfo();
//             ps.FileName = "cmd";
//             ps.Arguments = string.Format(@"/c @echo off && for /f ""tokens=5"" %i in ('tasklist /NH /FI ""PID eq {0}""') do echo %i ", Process.GetCurrentProcess().Id);
//             ps.UseShellExecute = false;
//             ps.RedirectStandardOutput = true;
//             ps.CreateNoWindow = true;
//             p.StartInfo = ps;
//             p.OutputDataReceived += (sender, e) =>
//             {
//                 if (e.Data != null)
//                 {
//                     int catchMem = int.Parse(e.Data.Replace(",", ""));
//                     if (catchMem > peakMemory)
//                     {
//                         peakMemory = catchMem;
//                     }
//                 }
// 
//             };
//             p.Start();
//             p.BeginOutputReadLine();
//             p.WaitForExit();
// 
//             if (useGpuCatch)
//             {
//                 double catchGraphicsMemUse = gpuz.SensorValue(6);
//                 if (catchGraphicsMemUse > graphicsMemUse)
//                 {
//                     graphicsMemUse = catchGraphicsMemUse;
//                 }
// 
//                 double catchgpuLoad = gpuz.SensorValue(7);
//                 if (catchgpuLoad > gpuLoad)
//                 {
//                     gpuLoad = catchgpuLoad;
//                 }
//             }
//             
//             
//         }
//     }
//     static string GetVideoDuration(string sourceFile)
//     {
//         string result = "";
//         Process p = new Process();
//         ProcessStartInfo ps = new ProcessStartInfo();
//         ps.FileName = "ffmpeg.exe";
//         ps.Arguments = string.Format("-i {0}", sourceFile);
//         ps.UseShellExecute = false;
//         ps.RedirectStandardError = true;
//         ps.CreateNoWindow = true;
//         p.StartInfo = ps;
//         p.ErrorDataReceived += (sender, e) =>
//         {
//             if (e.Data != null)
//             {
//                 result += e.Data;
//             }
// 
//         };
//         p.Start();
//         p.BeginErrorReadLine();
//         p.WaitForExit();
// 
//         string duration = result.Substring(result.IndexOf("Duration: ") + ("Duration: ").Length, ("00:00:00").Length);
//         return duration;
//     }
//     public void StartRecord(string temName)
//     {
//         if (useGpuCatch)
//         {
//             gpuz.Open();
//         }
//         
//         StartCoroutine("Checking");
//         templateName = temName;
//         startTime = DateTime.Now;
//         peakMemory = 0;
//         graphicsMemUse = 0;
//         gpuLoad = 0;
//     }
//     public void RenderingDoneAndStartFFmpeg()
//     {
//         renderDoneTime = DateTime.Now;
//     }
//     public BlueTaleManager.GFS_SERVER_STRESS_TEST_REPORT_Struct EndRecord(int serverID, bool nowrong, int filesize, string filename)
//     {
//        
//         StopCoroutine("Checking");
//         if (useGpuCatch)
//         {
//             gpuz.Close();
//         }
// 
//         BlueTaleManager.GFS_SERVER_STRESS_TEST_REPORT_Struct data = new BlueTaleManager.GFS_SERVER_STRESS_TEST_REPORT_Struct();
//         endTime = DateTime.Now;
//         try
//         {
//             if (serverID!=0)
//             {
//                 FileInfo fi = new FileInfo("ComputerStatus" + serverID + ".txt");
//                 _lastInfo = string.Format("{0},{1},(startTime){2},(renderDoneTime){3},(endTime){4},(generateDeltaTime){5},(renderDeltaTime){6},(ffmpegDeltaTime){7},(filesize){8:N2},(vidwoDuration){9},(peakMemory){10:N2},{11}", serverID, templateName, startTime, renderDoneTime, endTime, endTime - startTime, renderDoneTime - startTime, endTime - renderDoneTime, (float)filesize / 1024 / 1024, GetVideoDuration(filename), (float)peakMemory / 1024, nowrong);
//                 UnityEngine.Debug.Log(_lastInfo);
// 
//                 if (fi.Exists == false)
//                 {
//                     using (StreamWriter sw = new StreamWriter(fi.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read)))
//                     {
//                         sw.WriteLine(_lastInfo);
//                     }
//                 }
//                 else
//                 {
//                     using (StreamWriter sw = new StreamWriter(fi.Open(FileMode.Append, FileAccess.Write, FileShare.Read)))
//                     {
//                         sw.WriteLine(_lastInfo);
//                     }
//                 }       
//             }
// 
//            
//             data.serverID = serverID;
//             data.templateName = templateName;
//             data.startTime = startTime;
//             data.renderDoneTime = renderDoneTime;
//             data.endTime = endTime;
//             data.fileName = filename;
//             data.fileSize = filesize;
//             data.peakMemory = (float)peakMemory / 1024;
//             data.graphicsMemUse = graphicsMemUse;
//             data.noWrong = nowrong;
//             data.gpuLoad = gpuLoad;
//             return data;
//             
//         }
//         catch (Exception ex)
//         {
// 
//             UnityEngine.Debug.Log(ex);
//         }
// 
//         return data;
//         //Console.WriteLine(info);
//         
//     }
// }
