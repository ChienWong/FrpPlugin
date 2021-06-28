# FrpPlugin
A frp Plugin for Authentication
## Before running the Server
- You need download the SSL Certificate from your Domain Server provider,If just test you can generate it by openssl
- Install .Net5 SDK,Detailly see [Microsoft Doc](https://docs.microsoft.com/zh-cn/dotnet/core/install/)
## Run Servers
- Using your ssl certificate replace DomainName.pfx and modify  `<EmbeddedResource Include="DomainName.pfx">` in FrpPluginServer.csproj
- Modify App.config
    * `<param name="File" value=".\Logs\"/>` The location of log file,In windows,\ should be /
    * `<CertificatePassword>` Your password of Certificate
    * `<ClientPassword>` Your password fo mobile phone
    * `<HostAddress>` Server IP for listening
    * `<RequestPort>` Socket for Litening Frp request
    * `<ClientPort>` Socket for Listening result of mobile Phone
- Run commond `dotnet run`,you can run it,And if you want to depoly it, you can run command `dotnet publish -r linux-64 -c release`,For more information about how build or depoly,you can see Micorsoft Document
- Of course,you need add Frp releate configuration in frps.ini
## Run Mobile Phone
- You generate .Apk for Android
     * Add Server
     * ![](https://github.com/ChienWong/FrpPlugin/blob/main/ForREADME/1.jpg)
     * Select Server 
     * ![](https://github.com/ChienWong/FrpPlugin/blob/main/ForREADME/3.jpg)
     * Connect Server
     * ![](https://github.com/ChienWong/FrpPlugin/blob/main/ForREADME/2.jpg)
     * Wait frp request
     * ![](https://github.com/ChienWong/FrpPlugin/blob/main/ForREADME/4.jpg)
