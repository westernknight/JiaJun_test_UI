using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Web;
using log4net;
using LitJson;

// 本组件用于跟 BlueTale Manager 交互
// 将本组件放在 FirstScene 中，以后将一直存在
//[assembly: log4net.Config.XmlConfigurator(ConfigFile=Application.dataPath + "/Log/BTEServer.config", Watch = true)]
public class BTEWorkstation : MonoBehaviour
{
	public static BTEWorkstation instance { private set; get; }

	//4-1，log4net配置文件的路径
	//ILog log = log4net.LogManager.GetLogger(typeof(Program));
	private static string _fileName =
	#if UNITY_ANDROID
		"Config/log4net";
	#elif UNITY_STANDALONE_WIN
		Application.streamingAssetsPath + "/BTEServer.config";
	#endif

	// 当前 workstation 是否闲置中
	public bool isStandby { private set; get; }

	private Socket socket;

	private BlueTaleEngine bte;

	private double bteProgress = 0.0f;

	private void initialize()
	{
		socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

		// byte[] inValue = new byte[] { 1, 0, 0, 0, 0x88, 0x13, 0, 0, 0x88, 0x13, 0, 0 };
		// socket.IOControl(IOControlCode.KeepAliveValues, inValue, null);
	}

	public bool isConnected
	{
		get
		{
			return socket == null ? false : socket.Connected;
		}
	}

	private System.IAsyncResult connectAsyncResult = null;

	public void connectToManager(string ip, int port)
	{
		try
		{
			IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(ip), port);
			connectAsyncResult = socket.BeginConnect(
				ipep,
				new System.AsyncCallback(connectCallback),
				socket);
		}
		catch (System.Exception ex)
		{
			Debug.LogError("connectToManager: " + ex.Message);
			BTEWorkstation.log.Error(ex.Message,new System.Exception("发生了一个异常"));
			//connectToManager(ip,port);
		}
	}

	private byte[] receiveBuffer = new byte[4096];

	private void connectCallback(System.IAsyncResult ar)
	{
		try
		{
			socket.EndConnect(ar);
			connectAsyncResult = null;

			socket.BeginReceive(
				receiveBuffer,
				0,
				receiveBuffer.Length,
				SocketFlags.None,
				new System.AsyncCallback(receiveCallback),
				socket);
		}
		catch (System.Exception ex)
		{
			Debug.LogError("connectCallback: " + ex.Message);
			BTEWorkstation.log.Error(ex.Message,new System.Exception("发生了一个异常"));
		}
	}

	private string receivedMessage = string.Empty;
	private bool received = false; // to do work

	private void receiveCallback(System.IAsyncResult ar)
	{
		try
		{
			int receivedLength = socket.EndReceive(ar);
			if (receivedLength <= 0)
			{
				disconnectFromManager();
				return;
			}

			if (!isStandby)
			{
				// TEST CODE: send not standby message to manager
				sendToManager("not standby");
			}
			else
			{
				// TODO : check json data valid

				byte[] data = new byte[receivedLength];
				//Debug.Log(data.Length);
				System.Array.Copy(receiveBuffer, 0, data, 0, receivedLength);

				receivedMessage = Encoding.UTF8.GetString(data);
				//Debug.Log("receivedMessage:"+receivedMessage);
				//receivedMessage=File.ReadAllText(@"D:\Scen.txt"); //{"id":"100020", "templateName": "GreetingCard_1","title": "xsf","subTitle": "dfgerg","brideName": "sdfd ","groomName": "sdfs","title1": "sdf","subTitle1": "fwerfd","theEnd": "fds ","subTheEnd": "dfvgerfg","texture1URL": "C:\\fakepath\\100015.mp4"};
				//receivedMessage=File.ReadAllText(_path+"/BTEServer/TestFile/Scen.txt");
				isStandby = false;
				received = true;
			}

			socket.BeginReceive(
				receiveBuffer,
				0,
				receiveBuffer.Length,
				SocketFlags.None,
				new System.AsyncCallback(receiveCallback),
				socket);
		}
		catch
		{
			disconnectFromManager();
		}
	}

	public void disconnectFromManager()
	{
		try
		{
			if (socket.Connected)
			{
				socket.Shutdown(SocketShutdown.Both);
				// socket.Dispose();
				// socket.Close();
				socket.Disconnect(false);

				if (disconnectFromManagerEvent != null)
				{
					disconnectFromManagerEvent();
				}
			}
			else
			{
				// socket.Dispose();
				socket.Close();
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogError("disconnectFromManager: " + ex.Message);
			BTEWorkstation.log.Error(ex.Message,new System.Exception("发生了一个异常"));
			Application.Quit();
		}
	}

	public event System.Action disconnectFromManagerEvent;

	public void sendToManager(string message)
	{
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(message);
			socket.BeginSend(
				bytes,
				0,
				bytes.Length,
				SocketFlags.None,
				new System.AsyncCallback(sendCallback),
				socket);
		}	
		catch (System.Exception ex)
		{
			disconnectFromManager();
			Debug.LogError("sendToManager: " + ex.Message);
			BTEWorkstation.log.Error(ex.Message,new System.Exception("发生了一个异常"));
		}
	}

	private void sendCallback(System.IAsyncResult ar)
	{
		try
		{
			((Socket)ar.AsyncState).EndSend(ar);
		}
		catch (System.Exception ex)
		{
			disconnectFromManager();
			Debug.LogError("sendCallback: " + ex.Message);
			BTEWorkstation.log.Error(ex.Message,new System.Exception("发生了一个异常"));
		}
	}

	private void onBTEResponse(string responseJson)
	{
		JsonData response;
		try
		{
			response = JsonMapper.ToObject(responseJson);
		}
		catch (System.Exception ex)
		{
			Debug.LogError("response json parsing failed!"); // bte core 有问题
			return;
		}

		if (!response.containsKey("responseCode"))
		{
			Debug.LogError("response json key \"responseCode\" has not found!");
			return;
		}
		if (!response.containsKey("message"))
		{
			Debug.LogError("response json key \"message\" has not found!");
			return;
		}

		int responseCode = (int)response["responseCode"];
		string message = (string)response["message"];
		Debug.Log(message);
		switch ((BlueTaleEngine.ResponseCode)responseCode)
		{
			case BlueTaleEngine.ResponseCode.invalidRequest:
				// request json 有问题
				break;

			case BlueTaleEngine.ResponseCode.isNotRunning:
				// 通过 returnStatus 查询得到的状态
				break;

			case BlueTaleEngine.ResponseCode.isRunning:
				// bte 正在运行，每隔若干秒（看 bte 设置）返回 progress 值

				// 发送 progress 给 manager
				if (!response.containsKey("progress"))
				{
					Debug.LogError("response json key \"progress\" has not found!");
					return;
				}
				bteProgress = (double)response["progress"];

				// TODO: 发送 progress 给 manager
				// 下面是演示
				JsonData json1 = new JsonData();
				json1["progress"] = bteProgress;
				json1["message"] = "进度";
				sendToManager(json1.ToJson());
				
				break;

			case BlueTaleEngine.ResponseCode.exception:
				// bte 发生了异常，存在较严重的问题
				break;

			case BlueTaleEngine.ResponseCode.generateVideoRequestSucceeded:
				// 发送 generate video 的请求成功，bte 开始工作
				break;

			case BlueTaleEngine.ResponseCode.generateVideoDone:
				// 视频生成完成

				if (!response.containsKey("filePath"))
				{
					Debug.LogError("response json key \"filePath\" has not found!");
					return;
				}

				Application.LoadLevel("BTE_WS_MainUI");
				isStandby = true;

				string filePath = (string)response["filePath"];

				// TODO：发送消息给 manager，表示视频生成完成，bte server 变为空闲状态
				// 下面的是旧的
				JsonData json2 = new JsonData();
				json2["filePath"] = filePath;
				json2["message"] = "done";
				sendToManager(json2.ToJson());

				break;

			default:
				Debug.LogError("not supported response code: " + responseCode);
				break;
		}
	}

	public void Awake()
	{
		Screen.SetResolution(480, 270, false);
		//log4net.Config.XmlConfigurator (ConfigFile = _fileName, Watch = true);

		instance = this;
		isStandby = true;
		DontDestroyOnLoad(gameObject);
		initialize();
	}
	string _path;//=Application.dataPath;
	public static ILog log;
	void Start()
	{   
		log4net.Config.XmlConfigurator.ConfigureAndWatch(new System.IO.FileInfo(_fileName));
		 log = log4net.LogManager.GetLogger(typeof(BTEWorkstation));
		//log.Error("fafsaa", new System.Exception("发生了一个异常"));
		//_path=Application.dataPath;
		//Debug.Log ("file:///" + Application.dataPath + "/BTEServer/TestFile/VideoHive 1887 My Love.unity3d");
		//string ss= File.ReadAllText(Application.dataPath+"/BTEServer/TestFile/Scen.txt");
	//	Debug.Log (ss);

		// 初始化 BTE
		BlueTaleEngine.create();
		bte = BlueTaleEngine.instance;
		bte.responseEvent += onBTEResponse;

		Application.LoadLevel("BTE_WS_MainUI");
	}

	private bool isConnectedLast = false;

	void Update()
	{
		if (received)
		{
			received = false;
			LitJson.JsonData SceneName=LitJson.JsonMapper.ToObject(receivedMessage);
			Debug.Log(SceneName["templateNameURL"]);
			string _SceneName=(string)SceneName["templateNameURL"];
			Debug.Log(SceneName.ToJson());
			//_SceneName="file:///" + Application.dataPath + "/BTEServer/TestFile/VideoHive 1887 My Love.unity3d";
			//_SceneName="file:///" +"172.16.0.43/bteweb/server/template/VideoHive_1322_Robots_3D_gifts_special.unity3d";
			StartCoroutine(loadBundle(_SceneName));
			//AssetBundle assetBundle = AssetBundle.CreateFromFile(_SceneName); 
			//bte.run(receivedMessage);
		}

		if (!isConnected && isConnectedLast)
		{
			disconnectFromManager();
			socket.Close();
			initialize();
			Debug.Log("重置 Socket");
		}
		isConnectedLast = isConnected;
	}

	void OnApplicationQuit()
	{
		disconnectFromManager();
	}

	//@小熊--------------------------------------------
	AssetBundle _Bundle;
	IEnumerator  loadBundle(string url)
	{
		//AssetBundle.Unload(false);
		string[] locaScene= url.Split('/');
		//string locaURL="file:///"+Application.dataPath+"/AssetBundle/"+locaScene[6];
		string locaURL="D:/Message/"+locaScene[locaScene.Length-1];
		_Bundle = AssetBundle.CreateFromFile(locaURL); 
		Debug.Log (locaURL);
		if (_Bundle == null) {
			Debug.Log("assetBundle == null");
						//BTEWorkstation.log.Info (url);
						WWW www = WWW.LoadFromCacheOrDownload (url, 1);
			//WWW www=new WWW(url);
						yield  return www;
			Debug.Log(www.error);
			if(www.error!=null){BTEWorkstation.log.Info (www.error);Application.Quit();}
			_Bundle = www.assetBundle;
				}
		if (_Bundle != null) {
			Debug.Log("bte.request");
			
			JsonData requestJson = JsonMapper.ToObject(receivedMessage);
			requestJson["requestCode"] = (int)BlueTaleEngine.RequestCode.generateVideo;
			bte.request(requestJson.ToJson());

			Invoke("unload",5f);
		}
		else
			Application.Quit ();
	}
	void unload(){
		_Bundle.Unload(false);
	}
	//@小熊--------------------------------------------
}