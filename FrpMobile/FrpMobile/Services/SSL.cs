using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace FrpMobile.Services
{
    public delegate Task<bool> Alert(string message);
    public class SSL:IDisposable
    {
        public Action<bool> SetActive;
        private string IPAddress;
        private int Port;
        private TcpClient client;
        private SslStream sslstream;
        private object sslLock;
        private Alert Alert;
        private bool Status = true;
        private Regex QueryPattern = new Regex(@"ID:(\d+)\r\nIP:(\d+\.\d+\.\d+\.\d+)\r\nProxy:(\w+).",RegexOptions.Compiled);
        public SSL(string address, int port, Action<bool> setActive, ref Alert alert)
        {
            IPAddress = address;
            Port = port;
            SetActive = setActive;
            sslLock = new object();
            Alert = alert;
        }
        public bool Init()
        {
            try
            {
                client = new TcpClient();
                if(!client.ConnectAsync(IPAddress, Port).Wait(1000))
                {
                    Alert("Connect false");
                    return false;
                }
            }
            catch
            {
                Alert("Connect false");
                return false;
            }
            if (Authenticate())
            {
                ClientRun();
                return true;
            }
            else
            {
                Alert("Authenticate false");
                sslstream?.Close();
                client?.Close();
                return false;
            }
        }
        public void Dispose()
        {
            client?.Close();
            sslstream?.Close();
            SetActive(false);
        }
        public bool Authenticate()
        {
            sslstream = new SslStream(client.GetStream(), false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            try
            {
                if(!Regex.IsMatch(IPAddress, @"\d+\.\d+\.\d+\.\d+", RegexOptions.Compiled))
                    sslstream.AuthenticateAsClient(IPAddress);
            }
            catch (AuthenticationException e)
            {
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception : {0}", e.InnerException.Message);
                }
                sslstream.Close();
                client.Close();
                return false;
            }
            byte[] pwd = Encoding.UTF8.GetBytes(CurServer.CurToken+".<EOF>");
            sslstream.Write(pwd);
            sslstream.Flush();
            if (!ReadMessage().Contains("Authenticate:Pass.<EOF>"))
            {
                sslstream.Close();
                client.Close();
                return false;
            }
            SetActive(true);
            return true;
        }
        public void ClientRun()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    if(!client.Connected)
                    {
                        SetActive(false);
                        sslstream.Close();
                        client.Close();
                        break;
                    }
                    string message = ReadMessage();
                    foreach (var query in message.Split("<EOF>".ToCharArray()))
                    {
                        if (query.Length == 0) continue;
                        if (query == "Hello.")
                        {
                            Task.Run(() =>
                            {
                                lock (sslLock)
                                {
                                    sslstream.Write(Encoding.UTF8.GetBytes("Hello.<EOF>"));
                                    sslstream.Flush();
                                }
                            });
                            continue;
                        };
                        if (QueryPattern.IsMatch(query))
                        {
                            GroupCollection paramters = QueryPattern.Match(query).Groups;
                            int id = int.Parse(paramters[1].Value);
                            string IP = paramters[2].Value;
                            string proxyType = paramters[3].Value;
                            if (QueryUser("IP:" + IP + "\tProxy:" + proxyType))
                                Send("Result:Pass\r\nID:" + id + ".<EOF>");
                            else
                                Send("Result:Reject\r\nID:" + id + ".<EOF>");
                        }
                    }
                }
            });
        }
        private void Send(string message)
        {
            Task.Run(() =>
            {
                lock (sslLock)
                {
                    try
                    {
                        sslstream.Write(Encoding.UTF8.GetBytes(message));
                        sslstream.Flush();
                    }
                    catch
                    {
                        //
                    }
                }
            });
        }
        private string ReadMessage()
        {
            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();
            int bytes = -1;
            while (bytes != 0)
            {
                try
                {
                    bytes = sslstream.Read(buffer, 0, buffer.Length);
                }catch(Exception e)
                {
                    if (e.GetType() == typeof(IOException))
                        return messageData.ToString();
                    return "Error";
                }
                messageData.Append(Encoding.UTF8.GetString(buffer.Take(bytes).ToArray()));
                if (messageData.ToString().Contains("<EOF>")) return messageData.ToString();
            }
            return messageData.ToString();
        }
        private bool ValidateServerCertificate(object sender,
                            X509Certificate certificate,
                            X509Chain chain,
                            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
            return false;
        }
        private bool QueryUser(string message)
        {
            Task<bool> task = Device.InvokeOnMainThreadAsync<bool>(()=>Alert(message));
            if (!task.Wait(10000))
                return false;
            else
                return task.Result;
        }
    }
    public static class CurServer{
        public static Action<string> setAddress;
        public static string _Address;
        public static string CurAddress { get { return _Address; } 
            set {
                _Address = value;
                setAddress?.Invoke(value);
            }
        }
        public static string CurToken { get; set; }
    }
}
