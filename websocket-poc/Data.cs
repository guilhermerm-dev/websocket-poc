using System;
namespace websocket_poc
{
    public class Data
    {

        public Data(string destination, string name, string message) : this()
        {
            Destination = destination;
            Name = name;
            Message = message;
        }

        public Data()
        {
        }

        public string Destination { get; private set; }

        public string Name { get; private set; }

        public string Message { get; private set; }
    }
}
