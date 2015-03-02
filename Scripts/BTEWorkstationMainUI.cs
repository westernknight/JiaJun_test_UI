using UnityEngine;
using System.Collections;

public class BTEWorkstationMainUI : MonoBehaviour
{
	private BTEWorkstation ws;

	public UIInput ipInput;
	public UIInput portInput;
	public UIEventListener connectButton;

	public UILabel standbyLabel;

	private const string managerIPKey = "ManagerIP";
	private const string managerPortKey = "ManagerPort";

	private void onConnectButtonClick(GameObject sender)
	{
		int port = 0;
		try
		{
			port = System.Convert.ToInt32(portInput.label.text);
		}
		catch (System.Exception ex)
		{
			Debug.LogError(ex.Message);
			BTEWorkstation.log.Error(ex.Message,new System.Exception("发生了一个异常"));
			return;
		}
		PlayerPrefs.SetString(managerIPKey, ipInput.label.text);
		PlayerPrefs.SetString(managerPortKey, portInput.label.text);
		ws.connectToManager(ipInput.label.text, port);
	}

	public void setConnectUIActive(bool active)
	{
		ipInput.gameObject.SetActive(active);
		portInput.gameObject.SetActive(active);
		connectButton.gameObject.SetActive(active);
	}

	public void setStandbyUIActive(bool active)
	{
		standbyLabel.gameObject.SetActive(active);
	}
	void InvokeOnConnectButtonClick(){
		if(!ws.isConnected)
		onConnectButtonClick (gameObject);
	}
	// TEST CODE
	public UIEventListener testButton;

	void Start()
	{
		ws = BTEWorkstation.instance;
		ws.Awake ();
		connectButton.onClick = onConnectButtonClick;
		// TEST CODE BEGIN
		ipInput.label.text = PlayerPrefs.GetString(managerIPKey, "172.16.0.165");
		portInput.label.text = PlayerPrefs.GetString(managerPortKey, "5555");
		//if(!ws.isConnected){InvokeRepeating("InvokeOnConnectButtonClick",60f,60f);}
		CancelInvoke ("InvokeOnConnectButtonClick");
		InvokeRepeating("InvokeOnConnectButtonClick",6f,6f);
		testButton.onClick = (GameObject sender) =>

		{
			ws.disconnectFromManager();
			 //Hashtable table = new Hashtable();
			// table.Add("id", string.Format("output_{0:yyyy-MM-dd_hh-mm-ss_tt}_{1}", System.DateTime.Now, Random.Range(0, 999999)));
			// table.Add("templateName", "GreetingCard_1");
			// table.Add("title", "Blue Arc");
			// table.Add("subTitle", "The Company");
			// table.Add("brideName", "Animations");
			// table.Add("groomName", "Games");
			// table.Add("title1", "菠萝吹雪");
			// table.Add("subTitle1", "是一种叫菠萝的水果");
			// table.Add("theEnd", "Good Bye");
			// table.Add("subTheEnd", "Thanks");
			// table.Add("texture1URL", "file:///D:\\workspace\\BlueArcDemo\\HeKaDemo\\TestImages\\BoLuoChuiXue.png");
			// BlueTaleEngine.instance.run(Json.objectToString(table));
		};

		// TEST CODE END
	}

	void Update()
	{
		if (ws == null)
		{
			Debug.LogError("ws == null");
			//ws = BTEWorkstation.instance;
			return;
		}
		if (!ws.isConnected)
		{
			setConnectUIActive(true);
			setStandbyUIActive(false);
		}
		else
		{
			setConnectUIActive(false);
			setStandbyUIActive(true);
		}
	}
}