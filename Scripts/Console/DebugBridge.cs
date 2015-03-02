using UnityEngine;
using System.Collections;
using System.Net.Sockets;
using System.Net;
using System;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

public class DebugBridge : MonoBehaviour
{
    Socket socketServer;
    Socket socketClient;
    int managerPort = 6666;
    byte[] tmpData = new byte[8191];

    public static DebugBridge instance;
	void Awake()
    {
        while (true)
        {
            try
            {
                socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socketServer.Bind(new IPEndPoint(IPAddress.Any, managerPort));
                socketServer.Listen(1);
                break;
            }
            catch (Exception ex)
            {
                managerPort++;
                Console.WriteLine("prot " + managerPort +" "+ ex);
            }
        }
        instance = this;
 
        socketServer.BeginAccept((ar) =>
        {
            AcceptCallback(ar);

        }, socketServer);

    }

    private void AcceptCallback(IAsyncResult ar)
    {
        try
        {

            socketClient = socketServer.EndAccept(ar);
            ar.AsyncWaitHandle.Close();
            Console.WriteLine("btedb已连接");

            socketClient.BeginReceive(tmpData, 0, tmpData.Length, SocketFlags.None, new AsyncCallback(ReceiveDataLengthCallback), socketClient);     
        }
        catch (Exception e)
        {

            Console.WriteLine(e);
        }

    }
    private  void ServerDisconnect(Socket ts)
    {
        try
        {
            //ts.Shutdown(SocketShutdown.Both);
            ts.Disconnect(false);
            Console.WriteLine("btedb已断开连接");
            socketServer.BeginAccept((ar) =>
            {
                AcceptCallback(ar);

            }, socketServer);
        }
        catch (Exception ex)
        {

            Console.WriteLine(ex);
        }

    }


     void ReceiveDataLengthCallback(IAsyncResult result)
    {
        Socket ts = (Socket)result.AsyncState;
        try
        {
            int c = ts.EndReceive(result);
            result.AsyncWaitHandle.Close();

            if (c == 0)
            {
                ServerDisconnect(ts);
                return;
            }
            byte[] packageHeader = new byte[4];

            packageHeader[0] = tmpData[0];
            packageHeader[1] = tmpData[1];
            packageHeader[2] = tmpData[2];
            packageHeader[3] = tmpData[3];

            int bodyLength = BitConverter.ToInt32(packageHeader, 0);
            byte []bodyData = new byte[bodyLength];

            Buffer.BlockCopy(tmpData, 4, bodyData, 0, c - 4);
            int bitWritted = c - 4;
            while (c > 0)
            {
                if (bitWritted == bodyLength)
                    break;

                c = ts.Receive(tmpData, 0, tmpData.Length, SocketFlags.None);

                Buffer.BlockCopy(tmpData, 0, bodyData, bitWritted, c);

                bitWritted += c;
            }
            string str = System.Text.Encoding.GetEncoding(936).GetString(bodyData);

            switch (str)
            {
                case "logcat":
                    using (StreamReader sr = new StreamReader(JiaJun_test_UI.logFileHandle.Open(FileMode.Open, FileAccess.Read,FileShare.Write)))
                    {
                        Send(sr.ReadToEnd());
                    }                  
                    
                    break;
                default:
                    break;
            }
            ts.BeginReceive(tmpData, 0, tmpData.Length, SocketFlags.None, new AsyncCallback(ReceiveDataLengthCallback), ts);


        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            ServerDisconnect(ts);

        }
    }
     private void Send(string cmd)
     {
         if (socketClient!=null)
         {
             if (socketClient.Connected)
             {
                 
                 byte[] cmd_byte = System.Text.Encoding.GetEncoding(936).GetBytes(cmd);
                 byte[] len = BitConverter.GetBytes(cmd_byte.Length);

                 byte[] c = new byte[4 + cmd_byte.Length];
                 Buffer.BlockCopy(len, 0, c, 0, len.Length);
                 Buffer.BlockCopy(cmd_byte, 0, c, len.Length, cmd_byte.Length);

                 //Console.WriteLine("content length: "+cmd_byte.Length);
                 //Console.WriteLine("data len "+c.Length);
                 socketClient.Send(c, 0, c.Length, SocketFlags.None);                
             }
         }
         
     }

    public void Logcat(string log)
     {
         Send(log);
     }
}
