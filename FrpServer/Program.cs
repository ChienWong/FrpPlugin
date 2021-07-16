using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Xml;

namespace FrpPluginServer
{
    class Program
    {
        public static X509Certificate serverCertificate{get;set;}
        public static void Main(string[] args)
        {
            string path= System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "DomainName.pfx";
            XmlDocument configXml= new XmlDocument();
            configXml.Load(System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "App.config");
            XmlNode configNode = configXml.SelectSingleNode("configuration").SelectSingleNode("ServerConfig");
            char[] chars= configNode.SelectSingleNode("CertificatePassword").InnerText.ToArray();
            string ClientPassword = configNode.SelectSingleNode("ClientPassword").InnerText;
            string hostIP=configNode.SelectSingleNode("HostAddress").InnerText;
            int requestPort = int.Parse(configNode.SelectSingleNode("RequestPort").InnerText);
            int ClientPort = int.Parse(configNode.SelectSingleNode("ClientPort").InnerText);
            SecureString password = new SecureString();
            foreach(char c in chars)
                password.AppendChar(c);
            serverCertificate=new X509Certificate(path,password);
            FrpPulginServer.Server s = new FrpPulginServer.Server(path,chars,ClientPassword);
            s.Listen(requestPort,ClientPort,hostIP);
        }
    }
}