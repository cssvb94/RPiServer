
namespace RPiServer
{
    class Program
    {
        static AsyncServer server;

        static void Main(string[] args)
        {
            server = new AsyncServer();
            server.StartListening();
        }
    }
}
