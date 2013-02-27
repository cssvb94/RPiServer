using System.Windows.Forms;
using System.Net.Sockets;
using System;
using System.Threading;
using System.Text;

namespace RPiAsyncClient
{
    public partial class ClientForm : Form
    {
        TcpClient clientSocket = new System.Net.Sockets.TcpClient();
        NetworkStream serverStream;
        USBDataReceiver mUSBDataReceiver = null;
        Thread USBDataReceiver = null;

        public ClientForm()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, System.EventArgs e)
        {
            msg("Client Started");
            
            clientSocket.Connect("127.0.0.1", 1508);
            label1.Text = "Client Socket Program - Server Connected ...";

            mUSBDataReceiver = new USBDataReceiver(clientSocket);
            mUSBDataReceiver.DataReceived += new RPiAsyncClient.USBDataReceiver.DataReceivedHandler(mUSBDataDataReceived);
            USBDataReceiver = new Thread(mUSBDataReceiver.doReceive);
            USBDataReceiver.Start();
        }

        void mUSBDataDataReceived(byte[] data, EventArgs e)
        {
            if (data != null)
            {
                string returndata = System.Text.Encoding.ASCII.GetString(data);
                msg("Data from Server : " + returndata);
            }
        }

        private void btnSend_Click(object sender, System.EventArgs e)
        {
            NetworkStream serverStream = clientSocket.GetStream();
            byte[] outStream = System.Text.Encoding.ASCII.GetBytes("Message from Client$");
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();
            byte[] inStream = new byte[10025];
            serverStream.Read(inStream, 0, (int)clientSocket.ReceiveBufferSize);
            string returndata = System.Text.Encoding.ASCII.GetString(inStream);
            msg("Data from Server : " + returndata);
        }

        public void msg(string mesg)
        {
            textBox1.Text = textBox1.Text + Environment.NewLine + " >> " + mesg;
        }

        private void ClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            mUSBDataReceiver.RequestStop();
            USBDataReceiver.Join();
            if (clientSocket != null)
            {
                clientSocket.Close();
            }
        }
    } // Form

    class USBDataReceiver
    {
        private TcpClient listener = null;
        private bool running = true;
        private NetworkStream serverStream = null;

        public EventArgs e = null;

        public event DataReceivedHandler DataReceived;
        public delegate void DataReceivedHandler(byte[] data, EventArgs e);

        public USBDataReceiver(TcpClient client)
        {
            listener = client;
            try
            {
                serverStream = listener.GetStream();                
            }
            catch (Exception ex)
            {
            }
        }

        public void doReceive()
        {
            byte[] inStream = new byte[10025];
            if (DataReceived != null)
            {
                DataReceived(Encoding.ASCII.GetBytes("Running USBData listener"), e);
            }

            while (running && (listener != null))
            {
                if (serverStream.DataAvailable)
                {
                    serverStream.Read(inStream, 0, (int)listener.ReceiveBufferSize);
                    string returndata = System.Text.Encoding.ASCII.GetString(inStream);
                    if (DataReceived != null)
                    {
                        DataReceived(inStream, e);
                    }
                    Console.WriteLine(returndata);
                }
            }
            listener.Close();
        } // doReceive

        public void RequestStop()
        {
            running = false;
        }
        
    } // DataReceiver
}
