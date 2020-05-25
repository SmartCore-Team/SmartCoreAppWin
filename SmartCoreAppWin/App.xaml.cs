using Hardcodet.Wpf.TaskbarNotification;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace SmartCoreAppWin
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private Dictionary<string, object> mqttCfg = new Dictionary<string, object>();
        private List<string> apiUrls = new List<string>();
        private List<string> deviceIdList = null;
        private List<string> notifyDeviceIdList = null;

        private SynchronizationContext mSyncContext;
        private MqttClient mqttClient;

        private Dictionary<string, string> deviceFriendlyName = new Dictionary<string, string>();
        private Dictionary<string, string> propertyFriendlyName = new Dictionary<string, string>();
        private Dictionary<string, Dictionary<string, string>> propertyValueFriendlyName = new Dictionary<string, Dictionary<string, string>>();

        private TaskbarIcon _taskbar = new TaskbarIcon();
        private ContextMenu deviceContextMenu = new ContextMenu();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            mSyncContext = SynchronizationContext.Current;

            _taskbar.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); // new Icon("favicon.ico");
            _taskbar.ContextMenu = initMainContextMenu();
            _taskbar.TrayLeftMouseUp += (s, e2) => {
                deviceContextMenu.IsOpen = !deviceContextMenu.IsOpen;
            };

            init();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _taskbar.Dispose();
            base.OnExit(e);
        }

        private async void init()
        {
            await loadCfgFileAsync();
            if(null == this.deviceIdList)
            {
                this.deviceIdList = await deviceListAsync();
            }
            initMqtt();
            initDeviceContextMenuAsync();
        }

        private void initMqtt()
        {
            mqttClient = new MqttClient(this.mqttCfg["host"].ToString(), int.Parse(this.mqttCfg["port"].ToString()), false, null, null, MqttSslProtocols.None);
            mqttClient.MqttMsgPublishReceived += (s, e) =>
            {
                try
                {
                    string payload = System.Text.Encoding.UTF8.GetString(e.Message);
                    JObject payloadObj = (JObject)JsonConvert.DeserializeObject(payload);
                    string deviceId = payloadObj["deviceId"].ToString();
                    string propertyId = payloadObj["propertyId"].ToString();
                    string newValue = payloadObj["newValue"].ToString();
                    string oldValue = payloadObj["oldValue"].ToString();

                    mSyncContext.Post(updateValue, new string[] { deviceId, propertyId, newValue, oldValue });

                    if (this.notifyDeviceIdList != null && this.notifyDeviceIdList.Contains(deviceId))
                    {
                        string deviceFriendlyName = this.deviceFriendlyName.ContainsKey(deviceId) ? this.deviceFriendlyName[deviceId] : deviceId;
                        string dpKey = deviceId + "_" + propertyId;
                        string propertyFriendlyName = this.propertyFriendlyName.ContainsKey(dpKey) ? this.propertyFriendlyName[dpKey] : propertyId;
                        string newValueFriendlyName = this.propertyValueFriendlyName.ContainsKey(dpKey) ? this.propertyValueFriendlyName[dpKey].ContainsKey(newValue.ToUpper()) ? this.propertyValueFriendlyName[dpKey][newValue.ToUpper()] : newValue : newValue;
                        string oldValueFriendlyName = this.propertyValueFriendlyName.ContainsKey(dpKey) ? this.propertyValueFriendlyName[dpKey].ContainsKey(oldValue.ToUpper()) ? this.propertyValueFriendlyName[dpKey][oldValue.ToUpper()] : oldValue : oldValue;
                        _taskbar.ShowBalloonTip(deviceFriendlyName, propertyFriendlyName + ": " + oldValueFriendlyName + " -> " + newValueFriendlyName, Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location), true);
                    }
                }
                catch(Exception e2)
                {

                }
            };

            string username = this.mqttCfg["username"].ToString();
            string password = this.mqttCfg["password"].ToString();
            string clientId = this.mqttCfg["clientId"].ToString();
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                mqttClient.Connect(clientId);
            }
            else
            {
                mqttClient.Connect(clientId, username, password);
            }

            string topic = this.mqttCfg["topic"].ToString();
            mqttClient.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
        }

        private void updateValue(object param)
        {
            string[] paramArr = (string[])param;
            string deviceId = paramArr[0];
            string propertyId = paramArr[1];
            string newValue = paramArr[2];
            string oldValue = paramArr[3];

            for (int i = 0; i < deviceContextMenu.Items.Count; i++)
            {
                if(!(deviceContextMenu.Items[i] is MenuItem))
                {
                    continue;
                }

                MenuItem dMenuItem = (MenuItem)deviceContextMenu.Items[i];
                if (deviceId.Equals(dMenuItem.Tag))
                {
                    for (int j = 0; j < dMenuItem.Items.Count; j++)
                    {
                        if (!(dMenuItem.Items[j] is MenuItem))
                        {
                            continue;
                        }

                        MenuItem pMenuItem = (MenuItem)dMenuItem.Items[j];
                        if (propertyId.Equals(pMenuItem.Tag))
                        {
                            if (null == newValue)
                            {
                                pMenuItem.IsEnabled = false;
                            }
                            else
                            {
                                StackPanel panel = (StackPanel)pMenuItem.Header;
                                bool readOnly = (bool) panel.Tag;
                                if(readOnly)
                                {
                                    pMenuItem.IsEnabled = false;
                                }
                                else
                                {
                                    pMenuItem.IsEnabled = true;
                                }

                                for (int n = 0; n < panel.Children.Count; n++)
                                {
                                    if (panel.Children[n] is Label) { }
                                    else if (panel.Children[n] is Slider)
                                    {
                                        ((Slider)panel.Children[n]).Tag = "mqtt";
                                        ((Slider)panel.Children[n]).Value = int.Parse(newValue);
                                    }
                                    else if (panel.Children[n] is CheckBox)
                                    {
                                        ((CheckBox)panel.Children[n]).IsChecked = "TRUE".Equals(newValue.ToUpper()) ? true : false;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private async Task loadCfgFileAsync()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(System.AppDomain.CurrentDomain.BaseDirectory + "\\config.xml");
            XmlNode root = xmlDoc.SelectSingleNode("info");

            XmlNodeList apiUrlXmlNodeList = root.SelectNodes("apiUrl");
            for (int i = 0; i < apiUrlXmlNodeList.Count; i++) {
                this.apiUrls.Add(apiUrlXmlNodeList.Item(i).InnerText);
            }

            XmlNode deviceIdXmlNode = root.SelectSingleNode("deviceList");
            if (null != deviceIdXmlNode && !string.IsNullOrEmpty(deviceIdXmlNode.InnerText))
            {
                string deviceIdStr = deviceIdXmlNode.InnerText;
                this.deviceIdList = new List<string>(deviceIdStr.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
            }

            XmlNode notifyDeviceIdXmlNode = root.SelectSingleNode("notifyDeviceList");
            if (null != notifyDeviceIdXmlNode && !string.IsNullOrEmpty(notifyDeviceIdXmlNode.InnerText))
            {
                string notifyDeviceIdStr = notifyDeviceIdXmlNode.InnerText;
                this.notifyDeviceIdList = new List<string>(notifyDeviceIdStr.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
            }

            XmlNode mqttNode = root.SelectSingleNode("mqtt");
            mqttCfg.Add("host", mqttNode.SelectSingleNode("host").InnerText);
            mqttCfg.Add("port", mqttNode.SelectSingleNode("port").InnerText);
            mqttCfg.Add("username", mqttNode.SelectSingleNode("username").InnerText);
            mqttCfg.Add("password", mqttNode.SelectSingleNode("password").InnerText);
            mqttCfg.Add("topic", mqttNode.SelectSingleNode("topic").InnerText);
            mqttCfg.Add("clientId", mqttNode.SelectSingleNode("clientId").InnerText);
        }

        private async void initDeviceContextMenuAsync()
        {
            deviceContextMenu.Items.Clear();

            if(deviceIdList.Count <= 0)
            {
                return;
            }

            string deviceIdListStr = "\"" + string.Join("\", \"", deviceIdList.ToArray()) + "\"";
            string responseContent = await PostHttps(@"{""type"": ""device"", ""method"": ""getDevice"", ""deviceIdList"": [" + deviceIdListStr + "] }");
            JObject rObj = (JObject)JsonConvert.DeserializeObject(responseContent);
            Boolean success = Boolean.Parse(rObj["success"].ToString());
            if (success)
            {
                JObject devices = (JObject)rObj["data"];
                foreach (JObject device in devices.Values())
                {
                    string deviceId = device["deviceId"].ToString();
                    
                    JObject properties = (JObject)device["properties"];
                    JObject dExtra = (JObject)device["extra"];

                    MenuItem deviceMenuItem = new MenuItem();
                    deviceMenuItem.Tag = deviceId;

                    // set name
                    string deviceFriendlyNameStr = device["friendlyName"].ToString();
                    string deviceFriendlyName = string.IsNullOrEmpty(deviceFriendlyNameStr) ? deviceId : deviceFriendlyNameStr;
                    this.deviceFriendlyName.Add(deviceId, deviceFriendlyName);
                    deviceMenuItem.Header = deviceFriendlyName;

                    // set icon
                    string type = device["type"].ToString();
                    if(System.IO.File.Exists("images/device_" + deviceId + ".png"))
                    {
                        deviceMenuItem.Icon = new System.Windows.Controls.Image
                        {
                            Source = new BitmapImage(new Uri("images/device_" + deviceId + ".png", UriKind.Relative))
                        };
                    }
                    else
                    {
                        deviceMenuItem.Icon = new System.Windows.Controls.Image
                        {
                            Source = new BitmapImage(new Uri("images/deviceType_" + type + ".png", UriKind.Relative))
                        };
                    }

                    // set operation
                    JObject operations = (JObject)dExtra["operation"];
                    if (null != operations)
                    {
                        foreach (JProperty operation in operations.Properties())
                        {
                            MenuItem operationMenuItem = new MenuItem();
                            string operationFriendlyName = operation.Value["friendlyName"].ToString();
                            operationMenuItem.Header = string.IsNullOrEmpty(operationFriendlyName) ? operation.Name : operationFriendlyName;
                            // operationMenuItem.Tag = operation;
                            operationMenuItem.Click += async (s, e) => {
                                // String op = ((JProperty)GetTag(s)).Name;
                                await PostHttps("{\"type\": \"device\", \"method\": \"operation\", \"operation\": \"" + operation.Name + "\", \"deviceIdList\": [\"" + deviceId + "\"]}");
                            };
                            deviceMenuItem.Items.Add(operationMenuItem);
                        }

                        if(operations.Count > 0)
                        {
                            deviceMenuItem.Items.Add(new Separator());
                        }
                    }

                    // set properties
                    foreach (JProperty property in properties.Properties())
                    {
                        string propertyId = property.Name;
                        MenuItem propertyMenuItem = new MenuItem();
                        propertyMenuItem.Tag = propertyId;
                        propertyMenuItem.StaysOpenOnClick = true;
                        string propertyFriendlyNameStr = property.Value["friendlyName"].ToString();
                        string propertyFriendlyName = string.IsNullOrEmpty(propertyFriendlyNameStr) ? property.Name : propertyFriendlyNameStr;
                        this.propertyFriendlyName.Add(deviceId + "_" + propertyId, propertyFriendlyName);
                        JToken value = property.Value["value"];
                        JObject pExtra = (JObject)property.Value["extra"];
                        if(pExtra.ContainsKey("valueFriendlyName"))
                        {
                            Dictionary<string, string> valueFriendlyName = new Dictionary<string, string>();
                            foreach (JProperty rawValue in ((JObject) pExtra["valueFriendlyName"]).Properties())
                            {
                                valueFriendlyName.Add(rawValue.Name.ToUpper(), rawValue.Value.ToString());
                            }
                            this.propertyValueFriendlyName.Add(deviceId + "_" + propertyId, valueFriendlyName);
                        }

                        StackPanel panel = new StackPanel();
                        panel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                        panel.VerticalAlignment = VerticalAlignment.Center;
                        bool readOnly = property.Value.SelectToken("readOnly").Value<bool>();
                        panel.Tag = readOnly;
                        if (readOnly)
                        {
                            propertyMenuItem.IsEnabled = false;
                        }
                        else
                        {
                            propertyMenuItem.IsEnabled = true;
                        }
                        Label titleLabel = new Label();
                        titleLabel.VerticalAlignment = VerticalAlignment.Center;
                        titleLabel.Content = propertyFriendlyName;
                        panel.Children.Add(titleLabel);
                        string propertyValueType = property.Value["valueType"].ToString();
                        if(propertyValueType.Equals("java.lang.Integer"))
                        {
                            int valueMin = pExtra.ContainsKey("valueMin") ? int.Parse(pExtra["valueMin"].ToString()) : int.MinValue;
                            int valueMax = pExtra.ContainsKey("valueMax") ? int.Parse(pExtra["valueMax"].ToString()) : int.MaxValue;
                            int valueStep = pExtra.ContainsKey("valueStep") ? int.Parse(pExtra["valueStep"].ToString()) : 1;

                            Slider slider = new Slider();
                            Label valueLabel = new Label();
                            slider.Width = 200;
                            slider.Margin = new System.Windows.Thickness(2, 0, 0, 0);
                            slider.VerticalAlignment = VerticalAlignment.Center;
                            slider.Minimum = valueMin;
                            slider.Maximum = valueMax;
                            slider.IsSnapToTickEnabled = true;
                            slider.TickFrequency = valueStep;
                            slider.IsMoveToPointEnabled = true;
                            slider.Value = int.Parse(value.ToString());
                            valueLabel.Content = value.ToString();
                            if (!readOnly)
                            {
                                slider.ValueChanged += async (s, e) =>
                                {
                                    if (null == slider.Tag)
                                    {
                                        await PostHttps("{\"type\": \"device\", \"method\": \"setPropertyValue\", \"propertyIdList\": [\"" + property.Name + "\"], \"deviceIdList\": [\"" + deviceId + "\"], \"value\": " + slider.Value + "}");
                                    }
                                    valueLabel.Content = slider.Value;
                                    slider.Tag = null;
                                };
                            }
                            valueLabel.VerticalAlignment = VerticalAlignment.Center;
                            valueLabel.Width = 50;

                            if (JTokenType.Null == value.Type)
                            {
                                propertyMenuItem.IsEnabled = false;
                            }
                            panel.Children.Add(slider);
                            panel.Children.Add(valueLabel);
                        }
                        else if(propertyValueType.Equals("java.lang.Boolean"))
                        {
                            CheckBox checkbox = new CheckBox();
                            checkbox.VerticalAlignment = VerticalAlignment.Center;
                            checkbox.Margin = new System.Windows.Thickness(2, 0, 0, 0);
                            if (!readOnly)
                            {
                                checkbox.Click += async (s, e) =>
                                {
                                    await PostHttps("{\"type\": \"device\", \"method\": \"setPropertyValue\", \"propertyIdList\": [\"" + property.Name + "\"], \"deviceIdList\": [\"" + deviceId + "\"], \"value\": " + ((bool)checkbox.IsChecked ? "true" : "false") + "}");
                                };
                            }

                            if (JTokenType.Null == value.Type)
                            {
                                propertyMenuItem.IsEnabled = false;
                            }
                            panel.Children.Add(checkbox);
                        }
                        propertyMenuItem.Header = panel;
                        deviceMenuItem.Items.Add(propertyMenuItem);
                    }

                    deviceContextMenu.Items.Add(deviceMenuItem);
                }


                // return true;
            }
            else
            {
                // return false;
            }
        }

        internal static object GetTag(object sender)
        {
            Button button = sender as Button;
            MenuItem menuItem = sender as MenuItem;

            if (button != null)
                return button.Tag;
            if (menuItem != null)
                return menuItem.Tag;

            throw new ArgumentException("Unexpected sender");
        }
        private ContextMenu initMainContextMenu()
        {
            ContextMenu mainContextMenu = new ContextMenu();

            MenuItem versionMenuItem = new MenuItem();
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Label label = new Label();
            label.Content = "SmartCoreAppWin v" + version;
            versionMenuItem.Header = label;
            versionMenuItem.IsEnabled = false;
            mainContextMenu.Items.Add(versionMenuItem);

            mainContextMenu.Items.Add(new Separator());

            MenuItem exitMenuItem = new MenuItem();
            exitMenuItem.Header = "退出";
            exitMenuItem.Click += MenuItem_Exit_Click;
            mainContextMenu.Items.Add(exitMenuItem);

            return mainContextMenu;
        }

        private void MenuItem_Exit_Click(Object sender, System.EventArgs e)
        {
            mqttClient.Disconnect();
            Application.Current.Shutdown();
        }

        private async Task<string> PostHttps(string body)
        {
            string result = null;
            while (true)
            {
                int i = 0;
                for (; i< apiUrls.Count; i++)
                {
                    try
                    {
                        string apiUrl = apiUrls[i];
                        result = PostHttp(apiUrl, body);
                        break;
                    }
                    catch (Exception e)
                    { }
                }

                if (result == null)
                {
                    _taskbar.ShowBalloonTip("服务器连接失败", "12秒后重试", Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location), true);
                    await Task.Run(() =>
                    {
                        Thread.Sleep(12 * 1000);
                    });
                }
                else
                {
                    if(i != 0)
                    {
                        for(int j = 0; j < i; j++)
                        {
                            apiUrls.Add(apiUrls[0]);
                            apiUrls.RemoveAt(0);
                        }
                    }

                    return result;
                }
            }
        }

        private string PostHttp(string url, string body)
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);

            httpWebRequest.ContentType = "application/json; charset=UTF-8";
            httpWebRequest.Method = "POST";
            httpWebRequest.Timeout = 20000;

            byte[] btBodys = Encoding.UTF8.GetBytes(body);
            httpWebRequest.ContentLength = btBodys.Length;
            httpWebRequest.GetRequestStream().Write(btBodys, 0, btBodys.Length);

            HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream());
            string responseContent = streamReader.ReadToEnd();

            httpWebResponse.Close();
            streamReader.Close();
            httpWebRequest.Abort();
            httpWebResponse.Close();

            return responseContent;
        }

        public async Task<List<string>> deviceListAsync()
        {
            string responseContent = await PostHttps(@"{""type"": ""device"", ""method"": ""list""}");
            JObject rObj = (JObject) JsonConvert.DeserializeObject(responseContent);
            Boolean success = Boolean.Parse(rObj["success"].ToString());
            if (success)
            {
                List<string> r = new List<string>();
                JToken[] deviceIdArr = rObj["data"].ToArray();
                foreach (JToken deviceId in deviceIdArr)
                {
                    r.Add(deviceId.ToString());
                }
                return r;
            }
            else
            {
                return null;
            }
        }
    }
}
