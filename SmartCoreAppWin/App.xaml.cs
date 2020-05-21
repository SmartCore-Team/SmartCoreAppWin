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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;

namespace SmartCoreAppWin
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private TaskbarIcon _taskbar;
        private ContextMenu deviceContextMenu;

        private string apiUrl;
        private List<string> deviceIdList;

        protected override void OnStartup(StartupEventArgs e)
        {
            loadCfgFile();

            _taskbar = new TaskbarIcon();
            _taskbar.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); // new Icon("favicon.ico");
            _taskbar.ContextMenu = initMainContextMenu();
            deviceContextMenu = initDeviceContextMenu();
            _taskbar.TrayLeftMouseUp += TaskbarIcon_Left_Click;

            base.OnStartup(e);
            // _taskbar.ShowBalloonTip("Title", "Message", Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location), largeIcon: true);
        }

        private void loadCfgFile()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(System.AppDomain.CurrentDomain.BaseDirectory + "\\config.xml");
            XmlNode root = xmlDoc.SelectSingleNode("info");
            XmlNode apiUrlXmlNode = root.SelectSingleNode("apiUrl");
            this.apiUrl = apiUrlXmlNode.InnerText;
            XmlNode deviceIdXmlNode = root.SelectSingleNode("deviceId");
            if (null == deviceIdXmlNode)
            {
                this.deviceIdList = deviceList();
            }
            else
            {
                string deviceIdStr = deviceIdXmlNode.InnerText;
                this.deviceIdList = new List<string>(deviceIdStr.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        private void TaskbarIcon_Left_Click(Object sender, System.EventArgs e)
        {
            deviceContextMenu.IsOpen = !deviceContextMenu.IsOpen;
        }

        private ContextMenu initDeviceContextMenu()
        {
            ContextMenu deviceContextMenu = new ContextMenu();
            
            string deviceIdListStr = "\"" + string.Join("\", \"", deviceIdList.ToArray()) + "\"";
            string responseContent = PostHttp(apiUrl, @"{""type"": ""device"", ""method"": ""getDevice"", ""deviceIdList"": [" + deviceIdListStr + "] }");
            JObject rObj = (JObject)JsonConvert.DeserializeObject(responseContent);
            Boolean success = Boolean.Parse(rObj["success"].ToString());
            if (success)
            {
                JObject devices = (JObject)rObj["data"];
                foreach (JObject device in devices.Values())
                {
                    string deviceId = device["deviceId"].ToString();
                    
                    JObject properties = (JObject)device["properties"];
                    JObject extra = (JObject)device["extra"];

                    MenuItem deviceMenuItem = new MenuItem();

                    // set name
                    string deviceFriendlyName = device["friendlyName"].ToString();
                    deviceMenuItem.Header = string.IsNullOrEmpty(deviceFriendlyName) ? deviceId : deviceFriendlyName;

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
                    JObject operations = (JObject)extra["operation"];
                    if (null != operations)
                    {
                        foreach (JProperty operation in operations.Properties())
                        {
                            MenuItem operationMenuItem = new MenuItem();
                            string operationFriendlyName = operation.Value["friendlyName"].ToString();
                            operationMenuItem.Header = string.IsNullOrEmpty(operationFriendlyName) ? operation.Name : operationFriendlyName;
                            // operationMenuItem.Tag = operation;
                            operationMenuItem.Click += (s, e) => {
                                // String op = ((JProperty)GetTag(s)).Name;
                                PostHttp(apiUrl, "{\"type\": \"device\", \"method\": \"operation\", \"operation\": \"" + operation.Name + "\", \"deviceIdList\": [\"" + deviceId + "\"]}");
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
                        RichTextBox propertyMenuItem = new RichTextBox();
                        string propertyFriendlyName = property.Value["friendlyName"].ToString();
                        // propertyMenuItem.Text = string.IsNullOrEmpty(propertyFriendlyName) ? property.Name : propertyFriendlyName;

                        string propertyValueType = property.Value["valueType"].ToString();
                        if(propertyValueType.Equals("java.lang.Integer"))
                        {

                        }
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

            return deviceContextMenu;
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

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Label label = new Label();
            label.Content = "SmartCoreAppWin v" + version;
            mainContextMenu.Items.Add(label);
            mainContextMenu.Items.Add(new Separator());
            MenuItem exitMenuItem = new MenuItem();
            exitMenuItem.Header = "退出";
            exitMenuItem.Click += MenuItem_Exit_Click;
            mainContextMenu.Items.Add(exitMenuItem);

            return mainContextMenu;
        }

        private void MenuItem_Exit_Click(Object sender, System.EventArgs e)
        {
            Application.Current.Shutdown();
        }

        public string PostHttp(string url, string body)
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

        public List<string> deviceList()
        {
            string responseContent = PostHttp(apiUrl, @"{""type"": ""device"", ""method"": ""list""}");
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
