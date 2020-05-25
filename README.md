# SmartCoreAppWin
SmartCore的一个Windows桌面应用UI。

## 界面预览
![](https://raw.githubusercontent.com/SmartCore-Team/SmartCoreAppWin/master/images/1.jpg)
![](https://raw.githubusercontent.com/SmartCore-Team/SmartCoreAppWin/master/images/2.jpg)
![](https://raw.githubusercontent.com/SmartCore-Team/SmartCoreAppWin/master/images/3.jpg)

## 配置说明
```
<?xml version="1.0" encoding="utf-8" ?>
<info>
  <!-- SmartCore api地址 -->
  <apiUrl>http://6.0.1.1:8666/v1/rest</apiUrl>
  
  <!-- 设备列表，多个设备用逗号分割，空则自动获取全部设备 -->
  <deviceList></deviceList>
  
  <!-- 通知设备列表，多个设备用逗号分割 -->
  <notifyDeviceList>6001943c7eaa</notifyDeviceList>
  
  <!-- mqtt设置，用于接收SmartCore系统的通知消息 -->
  <mqtt>
    <host>6.0.1.1</host>
    <port>1883</port>
    <username>mqtt</username>
    <password>mqtt</password>
    <topic>/SmartCore/notifyComplete</topic>
    <clientId>SmartCore_App_Win</clientId>
  </mqtt>
</info>
```

## 设备图标说明
按设备id使用图标，图标文件命名为：images/device_设备ID.png
   
按设备类型使用图标，图标文件命名为：images/deviceType_设备类型.png


