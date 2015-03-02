using BlueTaleManager;
using BTEServer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// bte server底层通信实现
/// </summary>
public class UBinder : SerializationBinder
{
    public override Type BindToType(string assemblyName, string typeName)
    {
        Assembly ass = Assembly.GetExecutingAssembly();
        return ass.GetType(typeName);
    }
}
class BTESocketLibrary
{
    byte[] tmpData = new byte[100 * 1024];//100k临时data
    BTEData dataSave = new BTEData();
    IServerCapable server;
    Socket socketClient;
    string ip;
    int port;
    bool netExit = false;//manager不在线是否重拨
    int recvLength = 0;//当前需要就收多少数据记录，网络差的情况有可能一次接收不完所有数据
    Thread heartBreathThread;//心跳线程
    //初始化网络，若发现丢失连接，可以跑这个函数尝试重新连接，但重新连接后，不会发送上次发送不成功的数据
    struct Mission
    {
        public BTESTSCommand cmd;
        public object data;
    }
    Queue<Mission> dataQueue = new Queue<Mission>();

    public void Initialize(IServerCapable server, string ip, int port)
    {

        this.server = server;
        this.ip = ip;
        this.port = port;

        socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(ip), port);

        Console.WriteLine("连接BTEManager [{0}:{1}]", ip, port);



        socketClient.BeginConnect(
            ipep,
            new System.AsyncCallback(ConnectCallback),
             socketClient);


    }
    /// <summary>
    /// 程序退出需要把网络线程关掉
    /// </summary>
    public void Exit()
    {
        netExit = true;
        if (socketClient != null)
        {
            if (socketClient.Connected)
            {
                //socketClient.Shutdown(SocketShutdown.Both);

                socketClient.Disconnect(false);
            }
        }
        else
        {
            Console.WriteLine("socketClient == null");
        }
    }
    /// <summary>
    /// 5秒检测连接状态
    /// </summary>
    void HeartBreath()
    {

        while (socketClient.Connected)
        {
            Thread.Sleep(5000);
        }
        lock (this)
        {
            if (netExit == false)
            {
                socketClient.Close();
                Initialize(server, ip, port);
            }

        }
    }
    /// <summary>
    /// 不断链接BTEmanager
    /// </summary>
    /// <param name="ar"></param>
    private void ConnectCallback(System.IAsyncResult ar)
    {
        try
        {
            socketClient.EndConnect(ar);
            ar.AsyncWaitHandle.Close();

            socketClient.BeginReceive(tmpData, 0, tmpData.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socketClient);
            Console.WriteLine("连接BTEManager [{0}:{1}]成功", ip, port);

            if (heartBreathThread != null)
            {
                heartBreathThread.Abort();
                heartBreathThread = new Thread(new ThreadStart(HeartBreath));
                heartBreathThread.IsBackground = true;
                heartBreathThread.Start();
            }
            else
            {
                heartBreathThread = new Thread(new ThreadStart(HeartBreath));
                heartBreathThread.IsBackground = true;
                heartBreathThread.Start();
            }

            //byte[] inValue = new byte[] { 1, 0, 0, 0, 0x88, 0x13, 0, 0, 0x88, 0x13, 0, 0 };
            //socketClient.IOControl(IOControlCode.KeepAliveValues, inValue, null);
        }
        catch (System.Exception ex)
        {

            LogHelper.WriteLog(typeof(BTESocketLibrary), ex);
            if (netExit == false)
            {
                Thread.Sleep(1000);//sleep 1 second
                IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(ip), port);

                socketClient.BeginConnect(
                ipep,
                new System.AsyncCallback(ConnectCallback),
                socketClient);
            }

        }
    }

    private void SendCallback(System.IAsyncResult ar)
    {
        try
        {
            socketClient.EndSend(ar);
            ar.AsyncWaitHandle.Close();
        }
        catch (System.Exception ex)
        {
            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(ip), port);
            socketClient.BeginConnect(
                ipep,
                new System.AsyncCallback(ConnectCallback),
                socketClient);

            LogHelper.WriteLog(typeof(BTESocketLibrary), ex);
        }
    }
    /// <summary>
    /// body_data只有数据包，不含包头4字节的描述数据包长度的int
    /// </summary>
    /// <param name="body_data"></param>
    void DealPackage(byte[] body_data)
    {
        //string str = System.Text.Encoding.GetEncoding(936).GetString(bodyData);
        //Console.WriteLine(str);
        BTEData data = new BTEData() { bodyData = body_data, bodyLength = body_data.Length };
        IFormatter formatter = new BinaryFormatter();
        formatter.Binder = new UBinder();
        if (UnPack(data.bodyData).Length != 0)
        {
            MemoryStream ms = new MemoryStream(UnPack(data.bodyData));
            Console.WriteLine(ParseHeader(data.bodyData));
            dataQueue.Enqueue(new Mission()
            {
                cmd = ParseHeader(data.bodyData),
                data = formatter.Deserialize(ms)
            });
        }
        else
        {
            Console.WriteLine(ParseHeader(data.bodyData));
            dataQueue.Enqueue(new Mission()
            {
                cmd = ParseHeader(data.bodyData),
                data = null
            });

        }
    }
    void ReceivePackage(Socket ts, int receiveLength, byte[] receiveBuffer, int needBodyLength, byte[] allocBuffer)
    {
        if (needBodyLength > receiveLength - 4)//不完整包
        {
            Buffer.BlockCopy(receiveBuffer, 4, allocBuffer, 0, receiveLength - 4);//把所有数据放进Buffer,不包含包的前4字节
            int fillLength = receiveLength - 4;

            while (true)
            {
                //收到包截断的情况，继续接收
                if (fillLength != needBodyLength)
                {
                    int body_part = ts.Receive(tmpData, 0, tmpData.Length, SocketFlags.None);

                    if (fillLength + body_part > needBodyLength)
                    {
                        //粘包
                        int visioLength = (fillLength + body_part) - needBodyLength;//粘包长度

                        Buffer.BlockCopy(tmpData, 0, allocBuffer, fillLength, body_part - visioLength);
                        DealPackage(allocBuffer);
                        byte[] visioBuffer = new byte[visioLength];
                        Buffer.BlockCopy(tmpData, needBodyLength - fillLength, visioBuffer, 0, visioLength);
                        VisioPackage(ts, visioBuffer);
                        break;
                    }
                    Buffer.BlockCopy(tmpData, 0, allocBuffer, fillLength, body_part);

                    fillLength += body_part;
                }
                else
                {
                    DealPackage(allocBuffer);
                    break;
                }
            }

        }
        else if (needBodyLength == receiveLength - 4)
        {
            Buffer.BlockCopy(receiveBuffer, 4, allocBuffer, 0, needBodyLength);
            DealPackage(allocBuffer);
        }
        else//粘包
        {
            Buffer.BlockCopy(receiveBuffer, 4, allocBuffer, 0, needBodyLength);
            DealPackage(allocBuffer);
            int visioLength = receiveLength - (needBodyLength + 4);//粘包长度
            byte[] visioBuffer = new byte[visioLength];

            Buffer.BlockCopy(receiveBuffer, 4 + needBodyLength, visioBuffer, 0, visioLength);
            VisioPackage(ts, visioBuffer);
        }
    }

    void VisioPackage(Socket ts, byte[] visioBuffer)
    {
        if (visioBuffer.Length>=4)//can read package size
        {
            byte[] packageHeader = new byte[4];
            packageHeader[0] = visioBuffer[0];
            packageHeader[1] = visioBuffer[1];
            packageHeader[2] = visioBuffer[2];
            packageHeader[3] = visioBuffer[3];
            int bodyLength = BitConverter.ToInt32(packageHeader, 0);
            byte[] bodyData = new byte[bodyLength];

            if (visioBuffer.Length>=bodyLength+4)
            {
                ReceivePackage(ts, visioBuffer.Length, visioBuffer, bodyLength, bodyData);
            }
            else
            {
                int body_part = ts.Receive(tmpData, 0, tmpData.Length, SocketFlags.None);
                byte[] receiveBuffer = new byte[visioBuffer.Length + body_part];

                Buffer.BlockCopy(visioBuffer, 0, receiveBuffer, 0, visioBuffer.Length);
                Buffer.BlockCopy(tmpData, 0, receiveBuffer, visioBuffer.Length, body_part);

                ReceivePackage(ts, receiveBuffer.Length, receiveBuffer, bodyLength, bodyData);
            }

        }
        else//cant read package size,must read next pocket
        {
            int body_part = ts.Receive(tmpData, 0, tmpData.Length, SocketFlags.None);
            byte[] receiveBuffer = new byte[visioBuffer.Length + body_part];

            Buffer.BlockCopy(visioBuffer, 0, receiveBuffer, 0, visioBuffer.Length);
            Buffer.BlockCopy(tmpData, 0, receiveBuffer, visioBuffer.Length, body_part);

            byte[] packageHeader = new byte[4];
            packageHeader[0] = receiveBuffer[0];
            packageHeader[1] = receiveBuffer[1];
            packageHeader[2] = receiveBuffer[2];
            packageHeader[3] = receiveBuffer[3];
            int bodyLength = BitConverter.ToInt32(packageHeader, 0);
            byte[] bodyData = new byte[bodyLength];

            ReceivePackage(ts, receiveBuffer.Length, receiveBuffer, bodyLength, bodyData);
        }
        
    }
    void ReceiveCallback(IAsyncResult result)
    {

        try
        {
            Socket ts = (Socket)result.AsyncState;
            int c = ts.EndReceive(result);

            result.AsyncWaitHandle.Close();
            if (c == 0)
            {
                ts.Disconnect(false);
                Initialize(this.server, this.ip, this.port);
            }
            else
            {
                byte[] packageHeader = new byte[4];
                packageHeader[0] = tmpData[0];
                packageHeader[1] = tmpData[1];
                packageHeader[2] = tmpData[2];
                packageHeader[3] = tmpData[3];               

                int bodyLength = BitConverter.ToInt32(packageHeader, 0);
                byte[] bodyData = new byte[bodyLength];

                ReceivePackage(ts, c, tmpData, bodyLength, bodyData);
                ts.BeginReceive(tmpData, 0, tmpData.Length, SocketFlags.None, ReceiveCallback, ts);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }


    /// <summary>
    /// 包的内容是  (数据长度(4bit)|数据)
    /// </summary>
    /// <param name="result"></param>
    void ReceiveDataLengthCallback(IAsyncResult result)
    {

        try
        {
            Socket ts = (Socket)result.AsyncState;
            int c = ts.EndReceive(result);
            result.AsyncWaitHandle.Close();


            if (c == 0)
            {
                //接收数据失败，尝试重新连接bte manager
                socketClient.Close();
                Initialize(this.server, this.ip, this.port);
            }
            else
            {
                byte[] packageHeader = new byte[4];

                packageHeader[0] = tmpData[0];
                packageHeader[1] = tmpData[1];
                packageHeader[2] = tmpData[2];
                packageHeader[3] = tmpData[3];

                dataSave.bodyLength = BitConverter.ToInt32(packageHeader, 0);
                dataSave.bodyData = new byte[dataSave.bodyLength];

                Buffer.BlockCopy(tmpData, 4, dataSave.bodyData, 0, c - 4);
                int bitWritted = c - 4;
                while (c > 0)
                {
                    if (bitWritted == dataSave.bodyLength )
                        break;

                    c = ts.Receive(tmpData, 0, tmpData.Length, SocketFlags.None);

                    Buffer.BlockCopy(tmpData, 0, dataSave.bodyData, bitWritted, c);

                    bitWritted += c;
                }

             
                DealData(dataSave);
                ts.BeginReceive(tmpData, 0, tmpData.Length, SocketFlags.None, new AsyncCallback(ReceiveDataLengthCallback), ts);
                
#if false


                //清空数据，重新开始异步接收
                Debug.Log("数据包长度 " + dataSave.bodyLength);
                if (recvLength == dataSave.bodyLength + 4)
                {
                    Debug.Log("接收数据完全 in first time");
                    //偏移包头4个字节
                    //1 接收数据完全的时候，将临时包tmpData的数据赋值给data，goto 2

                    Buffer.BlockCopy(tmpData, 4, dataSave.bodyData, 0, dataSave.bodyLength);
                    ts.BeginReceive(tmpData, 0, tmpData.Length, SocketFlags.None, new AsyncCallback(ReceiveDataLengthCallback), ts);

                    DealData(dataSave);


                }
                else
                {
                    //2 若接收数据不完全，继续侦听receive收数据给dataSave.bodyData
                    ts.BeginReceive(dataSave.bodyData, recvLength, dataSave.bodyData.Length - recvLength, SocketFlags.None, new AsyncCallback(ReceiveDataCallback), ts);
                }
#endif
            }

        }
        catch (Exception ex)
        {
            LogHelper.WriteLog(typeof(BTESocketLibrary), ex);
        }
    }
    //可能因为网络阻塞，循环接收没有接收的包
    void ReceiveDataCallback(IAsyncResult result)
    {
        try
        {
            Socket ts = (Socket)result.AsyncState;
            int recv_count = ts.EndReceive(result);
            result.AsyncWaitHandle.Close();

            recvLength += recv_count;


            if (recvLength == dataSave.bodyLength + 4)
            {
                Debug.Log("接收数据完全");
                ts.BeginReceive(tmpData, 0, tmpData.Length, SocketFlags.None, new AsyncCallback(ReceiveDataLengthCallback), ts);
                DealData(dataSave);
            }
            else if (recv_count != 0)
            {

                Debug.Log("接收数据不完全," + "已经接收recvLength" + recvLength + "dataSave.bodyLength" + dataSave.bodyLength);
                ts.BeginReceive(dataSave.bodyData, recvLength, dataSave.bodyData.Length - recvLength, SocketFlags.None, new AsyncCallback(ReceiveDataCallback), ts);
            }
            else
            {
                Debug.Log("接收数据失败(size==0)，丢弃数据包");
                ts.BeginReceive(tmpData, 0, tmpData.Length, SocketFlags.None, new AsyncCallback(ReceiveDataLengthCallback), ts);
            }

        }
        catch (Exception ex)
        {
            LogHelper.WriteLog(typeof(BTESocketLibrary), ex);
        }



    }
    //打包两个数据包
    byte[] BuildPack(byte[] a, byte[] b)
    {
        byte[] c = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, c, 0, a.Length);
        Buffer.BlockCopy(b, 0, c, a.Length, b.Length);
        return c;
    }
    /// <summary>
    /// 发送接口.包头4字节记录内容大小，
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="cmd"></param>
    /// <param name="data"></param>
    public void SendWithLength(int cmd, object data = null)
    {
        try
        {
            if (socketClient!=null)
            {
                if (socketClient.Connected)
                {
                    if (data == null)
                    {
                        if (socketClient.Connected)
                        {
                            byte[] send_data_with_length = BuildPack(BitConverter.GetBytes(BitConverter.GetBytes(cmd).Length), BitConverter.GetBytes(cmd));
                            //socketClient.BeginSend(send_data_with_length, 0, send_data_with_length.Length, SocketFlags.None, new AsyncCallback(SendCallback), socketClient);
                            socketClient.Send(send_data_with_length);
                        }

                    }
                    else
                    {
                        MemoryStream stream = new MemoryStream();
                        IFormatter formatter = new BinaryFormatter();
                        formatter.Serialize(stream, data);

                        byte[] send_data = BuildPack(BitConverter.GetBytes(cmd), stream.ToArray());//带命令数据包
                        byte[] send_data_with_length = BuildPack(BitConverter.GetBytes(send_data.Length), send_data);
                       // socketClient.BeginSend(send_data_with_length, 0, send_data_with_length.Length, SocketFlags.None, new AsyncCallback(SendCallback), socketClient);
                        socketClient.Send(send_data_with_length);
                    }
                }
                else
                {
                    Console.WriteLine("与BTE失去连接，发送不了数据包");
                    socketClient.Close();
                    Initialize(this.server, this.ip, this.port);
                }                
            }
            


        }
        catch (Exception ex)
        {
            LogHelper.WriteLog(typeof(BTESocketLibrary), ex);
        }

    }

    /// <summary>
    /// 解释包头
    /// </summary>
    /// <param name="raw"></param>
    /// <returns></returns>
    BTESTSCommand ParseHeader(byte[] raw)
    {
        byte[] packageHeader = new byte[4];

        packageHeader[0] = raw[0];
        packageHeader[1] = raw[1];
        packageHeader[2] = raw[2];
        packageHeader[3] = raw[3];
        return (BTESTSCommand)BitConverter.ToInt32(packageHeader, 0);
    }
    /// <summary>
    /// 分离包头
    /// </summary>
    /// <param name="raw"></param>
    /// <returns></returns>
    byte[] UnPack(byte[] raw)
    {
        return raw.Skip(4).ToArray();

    }
    /// <summary>
    /// 根据包头，处理任务
    /// </summary>
    /// <param name="data"></param>
    void DealData(BTEData data)
    {

       
        

//         switch (ParseHeader(data.bodyData))
//         {
//             case BTESTSCommand.STS_RECORD_VIDEO:
//                 Console.WriteLine("BTESTSCommand.STS_RECORD_VIDEO");
//                 IFormatter formatter = new BinaryFormatter();
//                 formatter.Binder = new UBinder();
//                 MemoryStream ms = new MemoryStream(UnPack(data.bodyData));
//                 STS_RECORD_VIDEO_Struct obj = (STS_RECORD_VIDEO_Struct)formatter.Deserialize(ms);
//                 dataQueue.Enqueue(new Mission()
//                 {
//                     cmd = BTESTSCommand.STS_RECORD_VIDEO,
//                     data = obj
//                 });
//                 server.sts_record_video(obj);
//                 break;
//             case BTESTSCommand.STS_RETURNSTATUS:
//                 Console.WriteLine("BTESTSCommand.STS_RETURNSTATUS");
//                 dataQueue.Enqueue(new Mission()
//                 {
//                     cmd = BTESTSCommand.STS_RETURNSTATUS,
//                     data = null
//                 });
//                 server.sts_returnstatus();
//                 break;
// 
//             default:
//                 LogHelper.WriteLog(typeof(BTESocketLibrary), "unexpected manager command");
//                 break;
        //}
    }
    public void ProcessEvents()
    {
        while (dataQueue.Count > 0)
        {
            Mission mission = dataQueue.Dequeue();
            switch (mission.cmd)
            {
                case BTESTSCommand.STS_RECORD_VIDEO:
                    server.sts_record_video((STS_RECORD_VIDEO_Struct)mission.data);
                    break;
                case BTESTSCommand.STS_RETURNSTATUS:
                    server.sts_returnstatus();
                    break;
                case BTESTSCommand.STS_SERVER_INFO:
                    server.sts_server_info( (STS_SERVER_INFO_Struct)mission.data);
                    break;
                case BTESTSCommand.STS_SERVER_STRESS_TEST:
                    server.sts_stress_test();
                    break;
                default:
                    break;
            }
        }
    }
}

