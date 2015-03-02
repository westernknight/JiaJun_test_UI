using BlueTaleManager;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
namespace BTEServer
{
    class ServerImpl : IServerCapable
    {
        public System.Action<STS_RECORD_VIDEO_Struct> sts_record_video_callback;
        public System.Action sts_returnstatus_callback;
        public System.Action<STS_SERVER_INFO_Struct> sts_server_info_callback;
        public System.Action sts_stress_test_callback;
        public void sts_record_video(STS_RECORD_VIDEO_Struct data)
        {
            if (sts_record_video_callback!=null)
            {
                sts_record_video_callback(data);
            }            
        }
        public void sts_returnstatus()
        {
            if (sts_returnstatus_callback != null)
            {
                sts_returnstatus_callback();
            } 
        }
        public void sts_server_info(STS_SERVER_INFO_Struct data)
        {
            if (sts_server_info_callback != null)
            {
                sts_server_info_callback(data);
            }
        }
        public void sts_stress_test()
        {
            if (sts_stress_test_callback != null)
            {
                sts_stress_test_callback();
            }
        }
    }
}
