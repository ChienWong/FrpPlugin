using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.IO;
using log4net;
using System.ComponentModel.Design;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "App.config", Watch = true)]
namespace FrpPulginServer
{
    public class Server
    {
        public string ServerIP;
        //Server Certificate for ssl
        private X509Certificate serverCertificate;
        //Listening Server
        private TcpListener RequestListener;
        //Listening Client
        private TcpListener ClientListener;
        //Listening message from Clients
        private object ClientsConnsLock = new object(); 
        private List<Client> ClientsConns = new List<Client>();
        //Listening request from Server
        private object RequestConnsLock = new object();
        private List<TcpClient> RequestConns = new List<TcpClient>();
        //Client Password
        private string ClientPassword = new String("1234");
        //client message pattern
        private Regex ResultPattern = new Regex(@"Result:(\w+)\r\nID:(\d+).",RegexOptions.Compiled);
        private Regex RequestPattern = new Regex(@"POST /handler?[\s\S]*op=(\w+)&[\s\S]*",RegexOptions.Compiled);
        private Queue<QueryResult> QueryResultQueue = new Queue<QueryResult>();
        private object IDRequestLock = new object();
        //Request ID to Request Socket 
        private Dictionary<int,Socket> IDRequest = new Dictionary<int, Socket>();
        //Synchronize  to send result
        private Semaphore Event = new Semaphore(0,10);
        private ILog Logger;
        private Socket IndicationRequest;
        private Socket TiggerRequest;
        private Socket IndicationClient;
        private Socket TiggerClient;
        struct Client
        {
            public TcpClient tcpclient;
            public SslStream sslStream;
            public object writingLock;
            public bool Active;
            public void setActive(bool value)
            {
                Active = value;
            }
        }
        struct QueryResult
        {
            public int ID;
            public bool Pass;
        }
        public Server(string cert,char[] password,string clientPassword)
        {
            SecureString pwd = new SecureString();
            foreach(char c in password)
                pwd.AppendChar(c);
            serverCertificate= new X509Certificate(cert,pwd);
            this.ClientPassword=clientPassword;
            Logger=LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        }
        public void Listen(int RequestPort,int ClientPort,string IP)
        {
            ServerIP = IP;
            ClientListener = new TcpListener(IPAddress.Parse(ServerIP),ClientPort);
            RequestListener = new  TcpListener(IPAddress.Parse("127.0.0.1"),RequestPort);
            Socket IndicatorServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IndicatorServer.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"),61221));
            IndicatorServer.Listen();
            TiggerRequest = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
            if(!TiggerRequest.ConnectAsync(IPAddress.Parse("127.0.0.1"),61221).Wait(1000)) throw new Exception("Can't Connect to IndicationRequest");
            IndicationRequest = IndicatorServer.Accept();
            TiggerClient = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
            if(!TiggerClient.ConnectAsync(IPAddress.Parse("127.0.0.1"),61221).Wait(1000)) throw new Exception("Can't Connect to IndicationClient");
            IndicationClient = IndicatorServer.Accept();
            Task BindClientListening = new Task(StartListening);
            BindClientListening.Start();
            Task ProcessClient = new Task(ProcessMessageFromClients);
            ProcessClient.Start();
            Task ProcessServer = new Task(ProcessMessageFromServer);
            ProcessServer.Start();
            Task Delivery = new Task(DeliveryResult);
            Delivery.Start();
            while(true)
            {
                int count = 0;
                IEnumerable<int> ids;
                lock(IDRequestLock)
                {
                    ids= IDRequest.Keys.ToList();
                }
                while(count<5)
                {
                    IEnumerable<Client> tempCopy;
                    lock(ClientsConnsLock)
                    {
                        tempCopy = ClientsConns.Reverse<Client>();
                    }
                    Thread.Sleep(2000);
                    foreach(var i in tempCopy)
                    {
                        if(!i.Active)
                        {
                            Logger.Info("Client disconnect:\tIP:"+((IPEndPoint)i.tcpclient.Client.RemoteEndPoint).Address.ToString());
                            lock(ClientsConnsLock)
                            {
                                ClientsConns.Remove(i);
                            }
                            continue;
                        }
                        SendAllClient("Hello.<EOF>");
                    }
                    count++;
                }
                foreach(int i in ids)
                {
                    if(!IDRequest.ContainsKey(i)) continue;
                    if(!IDRequest[i].Connected)
                    {
                        lock(IDRequestLock)
                        {
                            IDRequest.Remove(i);
                        }
                    }
                    string message = "HTTP/1.1 200 OK \r\n\r\n{\"reject\": true,\"reject_reason\": \"Malicious IP\"}";
                    byte[] buff = System.Text.Encoding.UTF8.GetBytes(message);
                    try 
                    {
                        IDRequest[i].Send(buff);
                        IDRequest[i].Shutdown(SocketShutdown.Both);
                    }
                    finally
                    {
                        IDRequest[i].Close();
                    }
                    Logger.Debug("QueryMissing:\tID:"+i);
                    lock(IDRequestLock)
                    {
                        IDRequest.Remove(i);
                    }
                }
            }
        }
        private void DeliveryResult()
        {
            while(true)
            {
                Event.WaitOne();
                QueryResult result =  QueryResultQueue.Dequeue();
                string message;
                if(result.Pass)
                    message = "HTTP/1.1 200 OK \r\n\r\n{\"reject\": false,\"unchange\": true}";
                else
                    message = "HTTP/1.1 200 OK \r\n\r\n{\"reject\": true,\"reject_reason\": \"Malicious IP\"}";
                byte[] buff = System.Text.Encoding.UTF8.GetBytes(message);
                if(!IDRequest.ContainsKey(result.ID))continue;
                try
                {
                    IDRequest[result.ID].Send(buff);
                    IDRequest[result.ID].Shutdown(SocketShutdown.Both);
                }catch(Exception e){
                    Logger.Debug("Send result failed,\tID:"+result.ID+"\tERROR:"+e.Message);
                }
                finally
                {
                    IDRequest[result.ID].Close();
                    lock(IDRequestLock)
                    {
                        IDRequest.Remove(result.ID);
                    }
                }
            }
        }
        private void ProcessMessageFromServer()
        {
            try{
            int RequestID = 100;
            while(true)
            {
                List<Socket> conns = new List<Socket>();
                IEnumerable<TcpClient> tempCopy;
                lock(RequestConnsLock)
                {
                    tempCopy = RequestConns.Reverse<TcpClient>();
                }
                foreach(var i in tempCopy)
                {
                    if(!i.Connected)
                    {
                        i.Close();
                        Logger.Debug("Request Remove");
                        lock(RequestConnsLock)
                        {
                            RequestConns.Remove(i);
                        }
                        if(RequestConns.Count==0) break;
                        continue;
                    }
                    conns.Add(i.Client);
                }
                //Avoid blocking when new socket coming, special socket
                conns.Add(IndicationRequest);
                Socket.Select(conns,null,null,-1);
                if(conns.Count==1 && conns[0]==IndicationRequest)
                {
                    IndicationRequest.Receive(new byte[1024]);
                    continue;
                }
                byte[] buffer= new byte[1024];
                foreach(var i in conns)
                {
                    if(i==IndicationRequest)
                    {
                        IndicationRequest.Receive(new byte[1024]);
                        continue;
                    }
                    int rc = 0;
                    try
                    {
                        rc = i.Receive(buffer);
                    }catch
                    {
                        Logger.Debug("Request read failed");
                        continue;
                    }
                    string str = System.Text.Encoding.UTF8.GetString(buffer, 0, rc);
                    string[] requests=str.Split("\r\n");
                    if(requests.Length==0) {
                        Logger.Debug("Damaged request : "+str);
                        continue;
                    }
                    //string op=RequestPattern.Matches(requests[0])[0].Groups[1].Value;
                    JObject json;
                    try
                    {
                        json=JObject.Parse(requests[requests.Length-1]);
                    }catch{
                        Logger.Debug("Damaged request : "+str);
                        continue;
                    }
                    string remoteIP;
                    string message;
                    try{
                        remoteIP=json["content"]["remote_addr"].ToString().Split(':')[0];
                        message = "ID:"+RequestID+"\r\nIP:"+remoteIP+"\r\nProxy:"+json["content"]["proxy_name"].ToString()+"<EOF>";
                    }catch{
                        Logger.Debug("Damaged request : "+str);
                        continue;
                    }
                    Logger.Info("Request:"+message.Replace("\r\n","\t"));
                    if(ClientsConns.Count==0)
                    {
                        message = "HTTP/1.1 200 OK \r\n\r\n{\"reject\": true,\"reject_reason\": \"Malicious IP\"}";
                        try
                        {
                            Logger.Info("Result:reject,No clients\tID:"+RequestID);
                            i.Send(System.Text.Encoding.UTF8.GetBytes(message));
                            try
                            {
                                i.Shutdown(SocketShutdown.Both);
                            }finally{
                                i.Close();
                            }
                        }catch(Exception e)
                        {
                            Logger.Debug("Send Result failed\tID:"+RequestID+"\t"+e.Message);
                        }
                        continue;
                    }
                    lock(IDRequestLock)
                    {
                        IDRequest.Add(RequestID,i);
                    }
                    foreach(var c in RequestConns){
                        if(c.Client==i){
                            RequestConns.Remove(c);
                            break;
                        }
                    }
                    SendAllClient(message);
                    RequestID=(RequestID+1)%1000;
                    if(RequestID<100)
                    {
                        Logger.Info("restart count ID");
                        RequestID=100;
                    }
                }
            }}catch(Exception e){
                Logger.Debug("ProcessMessageFromServer Error: "+e.Message+"\t"+e.StackTrace);
            }
        }
        private void SendAllClient(string message)
        {
            lock (ClientsConnsLock)
            {
                foreach(var c in ClientsConns)
                {
                    byte[] buf = System.Text.Encoding.UTF8.GetBytes(message);
                    lock(c.writingLock)
                    {
                        try
                        {
                            c.sslStream.Write(buf);
                            c.sslStream.Flush();
                        }catch(Exception e)
                        {
                            Logger.Debug("Send query failed,Message:"+message+"\t"+e.Message);
                        }
                    }
                }
            }
        }
        private void ProcessMessageFromClients()
        {
            while (true)
            {
                List<Socket> conns=new List<Socket>();
                Dictionary<Socket,Client> map=new Dictionary<Socket, Client>();
                IEnumerable<Client> tempCopy;
                lock(ClientsConnsLock)
                {
                    tempCopy = ClientsConns.Reverse<Client>();
                }
                foreach(var i in tempCopy)
                {
                    if (i.tcpclient.Connected==false)
                    {
                        Logger.Info("Client remove:\tIP:"+((IPEndPoint)i.tcpclient.Client.RemoteEndPoint).Address.ToString());
                        i.sslStream.Close();
                        i.tcpclient.Close();
                        lock(ClientsConnsLock)
                        {
                            try{
                                ClientsConns.Remove(i);
                            }catch{
                                Logger.Debug("clientConnection has been removed");
                            }
                        }
                        continue;
                    }
                    conns.Add(i.tcpclient.Client);
                    map.Add(i.tcpclient.Client,i);
                }
                //Avoid blocking when new socket coming, special socket
                conns.Add(IndicationClient);
                Socket.Select(conns,null,null,-1);
                if(conns.Count == 1 && conns[0]==IndicationClient)
                {
                    IndicationClient.Receive(new byte[1024]);
                    continue;
                }
                foreach(Socket i in conns)
                {
                    if(i==IndicationClient){
                        IndicationClient.Receive(new byte[1024]);
                        continue;
                    }
                    SslStream ssl = map[i].sslStream;
                    try
                    {
                        string message = ReadSslMessage(ssl);
                        if(message==""||message=="Error") continue;
                        foreach (var request in message.Split("<EOF>"))
                        {
                            if(request.Equals("Hello."))
                            {
                                map[i].setActive(true);
                                continue;
                            }
                            if(ResultPattern.IsMatch(request))
                            {
                                GroupCollection result = ResultPattern.Match(request).Groups;
                                int id = int.Parse(result[2].Value);
                                bool pass = result[1].Value=="Pass";
                                QueryResultQueue.Enqueue(new QueryResult(){ID=id,Pass=pass});
                                Logger.Info("Result:"+pass+"\tID"+id);
                                Event.Release(1);
                                continue;
                            }
                            Logger.Debug("Damaged client message:\t"+request);
                        }
                    }catch(Exception e)
                    {
                        Logger.Debug("Read ssl failed,"+e.Message);
                        continue;
                    }
                }
            }
        }
        private void StartListening()
        {
            ClientListener.Start();
            RequestListener.Start();
            while(true)
            {
                List<Socket> accept=new List<Socket>{ClientListener.Server,RequestListener.Server};
                Socket.Select(accept,null,null,-1);
                foreach(var i in accept)
                {
                    if(i == ClientListener.Server)
                    {
                        Task.Run(()=>Authenticate(ClientListener.AcceptTcpClient()));
                    }
                    else
                    {
                        TcpClient requestClient = RequestListener.AcceptTcpClient();
                        Logger.Info("New Request:"+((IPEndPoint)requestClient.Client.RemoteEndPoint).Address.ToString());
                        lock(RequestConnsLock)
                        {
                            RequestConns.Add(requestClient);
                        }
                        TiggerRequest.Send(System.Text.Encoding.UTF8.GetBytes("OK"));
                    }
                }
            }
        }
        private void Authenticate(TcpClient client)
        {
            Logger.Info("New client Login\tIP:"+((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());
            SslStream ssl=new SslStream(client.GetStream());
            try
            {
                ssl.AuthenticateAsServer(serverCertificate,false,SslProtocols.Tls,true);
                ssl.ReadTimeout = 500;
                ssl.WriteTimeout = 500;
            }catch(Exception e)
            {
                Logger.Debug("Authenticate failed,IP:"+((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()+"\t"+e.Message);
                ssl.Close();
                client.Close();
            }
            try
            {
                string message = ReadSslMessage(ssl);
                while(message.Equals(""))message = ReadSslMessage(ssl);
                if(ComparePassword(ClientPassword,message.Replace(".<EOF>","")))
                {
                    Logger.Info("New client\tIP:"+((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());
                    ssl.Write(System.Text.Encoding.UTF8.GetBytes("Authenticate:Pass.<EOF>"));
                    lock(ClientsConnsLock)
                    {
                        ClientsConns.Add(new Client(){tcpclient=client,sslStream=ssl,writingLock=new Object(),Active=true});
                    }
                    TiggerClient.Send(System.Text.Encoding.UTF8.GetBytes("OK"));
                }else
                {
                    Logger.Debug("Password error:"+message);
                    ssl.Write(System.Text.Encoding.UTF8.GetBytes("Authenticate:Reject.<EOF>"));
                    ssl.Close();
                    client.Close();
                }
            }catch(Exception e)
            {
                Logger.Debug("Authenticate,Password failed,\t"+e.Message);
                ssl.Close();
                client.Close();
            }
        }
        private string ReadSslMessage(SslStream sslStream)
        {
            byte[] buffer= new byte[2048];
            StringBuilder messageData= new StringBuilder();
            int bytes = -1;
            while(bytes!=0){
                try{
                    bytes= sslStream.Read(buffer,0,buffer.Length);
                }catch(Exception e)
                {
                    if(e.GetType()==typeof(IOException))
                        return messageData.ToString();
                    Logger.Debug("Read ssl failed and return error\t"+e.Message);
                    return "Error";
                }
                messageData.Append(Encoding.UTF8.GetString(buffer.Take(bytes).ToArray()));
            }
            return messageData.ToString();
        }
        private bool ComparePassword(string s1,string s2)
        {
            if(s1.Length!=s2.Length) {
                int magic = 1;
                foreach(var i in s1)  magic += i;
                return magic==0;
            }
            bool ans = true;
            for(int i =0;i<s1.Length;i++)
            {
                if(s1[i]!=s2[i]) ans = false;
            }
            return ans;
        }
    }
}
