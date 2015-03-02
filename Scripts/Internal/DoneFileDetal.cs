using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BlueTaleManager
{
    class DoneFileDetal
    {
        public DateTime startTime;
        public DateTime renderDoneTime;
        public string templateName;
        public string GetVideoDuration(string sourceFile)
        {
            string result = "";
            Process p = new Process();
            ProcessStartInfo ps = new ProcessStartInfo();
            ps.FileName = "ffmpeg.exe";
            ps.Arguments = string.Format("-i {0}", sourceFile);
            ps.UseShellExecute = false;
            ps.RedirectStandardError = true;
            ps.CreateNoWindow = true;
            p.StartInfo = ps;
            p.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    result += e.Data;
                }

            };
            p.Start();
            p.BeginErrorReadLine();
            p.WaitForExit();

            string duration = result.Substring(result.IndexOf("Duration: ") + ("Duration: ").Length, ("00:00:00").Length);
            return duration;
        }
    }
}
