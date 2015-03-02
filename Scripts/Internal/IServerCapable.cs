using BlueTaleManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BTEServer
{
    /// <summary>
    /// 
    /// server 回调事件定义接口
    /// </summary>
    interface IServerCapable
    {
        void sts_record_video(STS_RECORD_VIDEO_Struct data);
        void sts_returnstatus();

        void sts_server_info(STS_SERVER_INFO_Struct data);

        void sts_stress_test();
    }

}
