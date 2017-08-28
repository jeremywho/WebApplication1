using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.WebSockets;
using SimpleTCP;

namespace WebApplication1.Controllers
{
    public class WebsockifyController : Controller
    {
        public void Index()
        {
            if (HttpContext.IsWebSocketRequest)
            {
                HttpContext.AcceptWebSocketRequest(ProcessRequestInternalAsync, new AspNetWebSocketOptions{SubProtocol = "binary"});
            }
        }

        private async Task ProcessRequestInternalAsync(AspNetWebSocketContext arg)
        {
            try
            {
                var webSocket = arg.WebSocket;
                var client = new SimpleTcpClient().Connect("192.168.1.9", 5900);
                client.DataReceived += async (sender, message) =>
                {
                    try
                    {

                        Debug.WriteLine($"**** RECV [{message.Data.Length}]");
                        await webSocket.SendAsync(new ArraySegment<byte>(message.Data), WebSocketMessageType.Binary, true, CancellationToken.None);

                    }
                    catch (Exception ex)
                    {
                        
                    }
                };

                const int maxMessageSize = 5000;
                var receivedDataBuffer = new ArraySegment<Byte>(new Byte[maxMessageSize]);

                var cancellationToken = new CancellationToken();


                while (arg.WebSocket.State == WebSocketState.Open)
                {
                    //Reads data. 
                    WebSocketReceiveResult webSocketReceiveResult =
                        await webSocket.ReceiveAsync(receivedDataBuffer, cancellationToken);

                    //If input frame is cancelation frame, send close command. 
                    if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            String.Empty, cancellationToken);
                    }
                    else if (webSocketReceiveResult.MessageType == WebSocketMessageType.Binary)
                    {
                        byte[] payloadData = receivedDataBuffer.Array.Where(b => b != 0).ToArray();
                        Debug.WriteLine($"****WS RECV [{payloadData.Length}]");// [{Encoding.ASCII.GetString(payloadData, 0, payloadData.Length)}]");
                        client.Write(payloadData);
                        //await stream.WriteAsync(payloadData, 0, payloadData.Length, ct);
                        //Because we know that is a string, we convert it. 
                        //string receiveString =System.Text.Encoding.UTF8.GetString(payloadData, 0, payloadData.Length);

                        //Converts string to byte array. 
                        //var newString = String.Format("Hello, " + receiveString + " ! Time {0}", DateTime.Now.ToString());
                        //Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(newString);

                        //Sends data back. 
                        //await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
                    }
                    else
                    {
                        Debug.WriteLine("**** Got TEXT");
                    }
                }
            }
            catch (Exception ex)
            {
                var m = ex.Message;
            }
        }
    }



    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (HttpContext.IsWebSocketRequest)
            {
                HttpContext.AcceptWebSocketRequest(ProcessRequestInternalAsync);
            }
            return View();
        }


        private async Task ProcessRequestInternalAsync(AspNetWebSocketContext arg)
        {
            const int maxMessageSize = 1024;

            //Buffer for received bits. 
            var receivedDataBuffer = new ArraySegment<Byte>(new Byte[maxMessageSize]);

            var cancellationToken = new CancellationToken();

            var webSocket = arg.WebSocket;
            while (arg.WebSocket.State == WebSocketState.Open)
            {
                //Reads data. 
                WebSocketReceiveResult webSocketReceiveResult =
                    await webSocket.ReceiveAsync(receivedDataBuffer, cancellationToken);

                //If input frame is cancelation frame, send close command. 
                if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                        String.Empty, cancellationToken);
                }
                else
                {
                    byte[] payloadData = receivedDataBuffer.Array.Where(b => b != 0).ToArray();

                    //Because we know that is a string, we convert it. 
                    string receiveString =
                        System.Text.Encoding.UTF8.GetString(payloadData, 0, payloadData.Length);

                    //Converts string to byte array. 
                    var newString =
                        String.Format("Hello, " + receiveString + " ! Time {0}", DateTime.Now.ToString());
                    Byte[] bytes = System.Text.Encoding.UTF8.GetBytes(newString);

                    //Sends data back. 
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text, true, cancellationToken);
                }
            }
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }


    public class StateObject
    {
        // Client socket.
        public Socket WorkSocket;
        // Size of receive buffer.
        public const int BufferSize = 5000;
        // Receive buffer.
        public byte[] Buffer = new byte[BufferSize];
    }

    public class AsynchronousClient
    {
        private readonly WebSocket _webSocket;
        private Socket _client = null;

        public AsynchronousClient(WebSocket webSocket)
        {
            _webSocket = webSocket;
            StartClient();
        }
        // The port number for the remote device.
        private const int port = 6900;

        // ManualResetEvent instances signal completion.
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);

        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);

        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        // The response from the remote device.
        private static String response = String.Empty;

        private void StartClient()
        {
            // Connect to a remote device.
            try
            {
                // Establish the remote endpoint for the socket.
                // The name of the 
                // remote device is "host.contoso.com".
                //IPHostEntry ipHostInfo = Dns.Resolve("host.contoso.com");
                //IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("192.168.1.9"), port);

                // Create a TCP/IP socket.
                _client = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.
                _client.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), _client);
                connectDone.WaitOne();

                // Send test data to the remote device.
                //Send(client, "This is a test<EOF>");
                //sendDone.WaitOne();

                // Receive the response from the remote device.
                Receive();
                //receiveDone.WaitOne();

                // Write the response to the console.
                //Console.WriteLine("Response received : {0}", response);

                // Release the socket.
                //client.Shutdown(SocketShutdown.Both);
                //client.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket) ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}",
                    client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void Receive()
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.WorkSocket = _client;

                // Begin receiving the data from the remote device.
                _client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject) ar.AsyncState;
                Socket client = state.WorkSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    //state.sb.Append(Encoding.ASCII.GetString(state.Buffer, 0, bytesRead));

                    // Get the rest of the data.
                    //client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0,
                     //   new AsyncCallback(ReceiveCallback), state);
                    _webSocket.SendAsync(new ArraySegment<byte>(state.Buffer), WebSocketMessageType.Binary, true, CancellationToken.None);
                }
                else
                {
                    // All the data has arrived; put it in response.
                    //if (state.sb.Length > 1)
                    //{
                    //    response = state.sb.ToString();
                    //}
                    // Signal that all bytes have been received.
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            Receive();
        }

        public void Send(byte[] data)
        {

            // Begin sending the data to the remote device.
            _client.BeginSend(data, 0, data.Length, 0,
                new AsyncCallback(SendCallback), _client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket) ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                // Signal that all bytes have been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}