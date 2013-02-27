using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net;

namespace RPiAsyncServer
{
    class Program
    {
        private static USBWorker mUSBWorker = null;
        private static Thread USBThread = null;

        static void Main(string[] args)
        {
            IPAddress ipAddress = new IPAddress(new byte[] {127, 0, 0, 1});
            TcpListener serverSocket = new TcpListener(ipAddress, 1508);
            //TcpListener serverSocket = new TcpListener(1508);
            TcpClient clientSocket = default(TcpClient);

            // USB reader
            mUSBWorker = new USBWorker();
            USBThread = new Thread(mUSBWorker.doWork);
            USBThread.Start();
            Console.WriteLine(" >> USB Worker thread started");

            int counter = 0;
            serverSocket.Start();
            Console.WriteLine(" >> Server Started");
            counter = 0;

            while (true)
            {
                counter += 1;
                clientSocket = serverSocket.AcceptTcpClient();
                Console.WriteLine(" >> " + "Client No:" + Convert.ToString(counter) + " started!");
                handleClinet client = new handleClinet();
                client.startClient(clientSocket, Convert.ToString(counter));
                mUSBWorker.AddClient(clientSocket);
            }
            //clientSocket.Close();
            mUSBWorker.RequestToStop();
            USBThread.Join();
            serverSocket.Stop();
            Console.WriteLine(" >> " + "exit");
            Console.ReadLine();
        }
    }

    //Class to handle each client request
    public class handleClinet
    {
        TcpClient clientSocket;
        string clNo;

        public void startClient(TcpClient inClientSocket, string clineNo)
        {
            this.clientSocket = inClientSocket;
            this.clNo = clineNo;
            Thread ctThread = new Thread(doChat);
            ctThread.Start();
        }

        private void doChat()
        {
            int requestCount = 0;
            byte[] bytesFrom = new byte[10025];
            string dataFromClient = null;
            Byte[] sendBytes = null;
            string serverResponse = null;
            string rCount = null;
            requestCount = 0;

            while (true)
            {
                try
                {
                    requestCount = requestCount + 1;
                    NetworkStream networkStream = clientSocket.GetStream();
                    if (networkStream.DataAvailable)
                    {
                        networkStream.Read(bytesFrom, 0, (int)clientSocket.ReceiveBufferSize);
                        dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                        dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
                        Console.WriteLine(" >> " + "From client-" + clNo + " " + dataFromClient);
                        rCount = Convert.ToString(requestCount);
                        serverResponse = "Server to clinet(" + clNo + ") " + rCount;
                        sendBytes = Encoding.ASCII.GetBytes(serverResponse);
                        networkStream.Write(sendBytes, 0, sendBytes.Length);
                        networkStream.Flush();
                        Console.WriteLine(" >> " + serverResponse);
                    }
                }

                catch (Exception ex)
                {
                    //Console.WriteLine(" >> " + ex.ToString());
                    Console.WriteLine(" >> Closing socket!");
                    clientSocket.Close();
                    break;
                }
            }
        }
    } // handle client


    class USBWorker
    {
        private List<TcpClient> Clients = null;
        private string USBData = "USB here!";
        private bool running = true;
        byte[] sendBytes = null;

        public USBWorker()
        {
            Clients = new List<TcpClient>();
        }

        public void AddClient(TcpClient client)
        {
            if (client != null)
            {
                Clients.Add(client);
                Console.WriteLine(" >> Client added");
            }
        }

        public void doWork()
        {
            Console.WriteLine("USB working here");
            while (running)
            {
                foreach (TcpClient client in Clients)
                {
                    if (client != null)
                    {
                        if (client.Connected)
                        {
                            try
                            {
                                Console.WriteLine("Sent USB data to client");
                                NetworkStream networkStream = client.GetStream();
                                sendBytes = Encoding.ASCII.GetBytes(USBData);
                                networkStream.Write(sendBytes, 0, sendBytes.Length);
                                networkStream.Flush();
                            }
                            catch (Exception ex)
                            {
                                //Clients.Remove(client);
                                client.Close();

                            }
                        } // connected
                    } // !null
                    else
                    {
                        Clients.Remove(client);
                    } // if
                } // foreach
                Thread.Sleep(500);
            } // Loop
        } // doWork

        public void RequestToStop()
        {
            running = false;
        }

    } // class USBWorker

} // namespace
