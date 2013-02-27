using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace RPiServer
{
    // State object for reading client data asynchronously
    public class StateObject
    {
        // Client  socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 1024;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }

    public class AsyncServer
    {
        // Thread signal.
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private UsbReader mUsbreader = null;
        private Thread workerThread = null;
        private bool serverRunning = true;
        private IPAddress ipAddress = null;

        public AsyncServer()
        {
            mUsbreader = new UsbReader();
            workerThread = new Thread(mUsbreader.Proccess);
            workerThread.Start();
        }

        public void StartListening()
        {
            // Data buffer for incoming data.
            byte[] bytes = new Byte[1024];

            // Establish the local endpoint for the socket.
            // The DNS name of the computer
            // running the listener is "host.contoso.com".
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            //IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            foreach (IPAddress ipaddr in ipHostInfo.AddressList)
            {
                if (ipaddr.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ipaddr;  
                }
            }

            ipAddress = (ipAddress == null) ? new IPAddress(new byte[] { 127, 0, 0, 1 }) : ipAddress;

            //ipAddress = ipHostInfo.AddressList[0];
            ipAddress = new IPAddress(new byte[] { 192, 168, 10, 8 });
            
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 1508);

            // Create a TCP/IP socket.
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine("Starting [{0}:{1}]", ipAddress.ToString(), localEndPoint.Port);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (serverRunning)
                {
                    // Set the event to nonsignaled state.
                    allDone.Reset();
                    // Start an asynchronous socket to listen for connections.
                    Console.WriteLine("Server ready and waiting for a connection...");
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    // Wait until a connection is made before continuing.
                    allDone.WaitOne();
                }

                Console.WriteLine("Server shutting down ...");
                mUsbreader.RequestStop();
                workerThread.Join();
                listener.Shutdown(SocketShutdown.Both);
                listener.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            allDone.Set();
            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);
            // Create the state object.
            StateObject state = new StateObject();
            state.workSocket = handler;
            mUsbreader.AddListener(handler);
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            Console.WriteLine("Client added!");
        }

        public void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket. 
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read 
                // more data.
                content = state.sb.ToString();
                if (content.IndexOf("BYE!") > -1)
                {
                    // All the data has been read from the 
                    // client. Display it on the console.
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);
                    // Echo the data back to the client.
                    Send(handler, content);
                }
                else if (content.IndexOf("HALT!") > -1)
                {
                    Send(handler, "Shuting down the server ...");
                    serverRunning = false;
                    handler.EndReceive(ar);
                    //handler.EndAccept(ar);
                }
                else
                {
                    // Not all data received. Get more.
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
            }
        }

        private void Send(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


    }// AsyncServer

    class UsbReader
    {
        private String data = "Pong!\r\n";
        private List<Socket> Sockets = null;
        private bool running = true;

        public UsbReader()
        {
            Sockets = new List<Socket>();
        }

        public void AddListener(Socket handler)
        {
            if (handler != null)
            {
                Sockets.Add(handler);
                Console.WriteLine("Listener added!");
            }
        }

        public void Proccess()
        {
            Console.WriteLine("Proccessing...");

            byte[] byteData = Encoding.ASCII.GetBytes(data);

            while (running)
            {
                foreach (Socket mHandler in Sockets)
                {
                    if (mHandler != null)
                    {
                        if (mHandler.Connected)
                        {
                            try
                            {
                                // Begin sending the data to the remote device.
                                mHandler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), mHandler);
                            }
                            catch (Exception ex)
                            {
                                mHandler.Shutdown(SocketShutdown.Both);
                                mHandler.Close();
                            }
                        }
                    }
                    else
                    {
                        Sockets.Remove(mHandler);
                    }
                }
                Thread.Sleep(500);
            }
        }

        public void RequestStop()
        {
            running = false;
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                //handler.Shutdown(SocketShutdown.Both);
                //handler.Close();                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    } // UsbReader
}
