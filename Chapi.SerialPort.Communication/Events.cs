using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chapi.SerialPortLib.Communication
{
    public class ConnectionStatusChangedEventArgs
    {
        /// <summary>
        /// State of the connection.
        /// </summary>
        public readonly bool Connected;
        /// <summary>
        /// Initializes an instance <see cref="SerialPortLib.ConnectionStatusChangedEventArgs"/> class.
        /// </summary>
        /// <param name="state">State of the connection (true = connected, false = not connected)</param>
        public ConnectionStatusChangedEventArgs(bool state)
        {
            Connected = state;
        }
    }

    public class MessageRecivedEventArgs
    {
        /// <summary>
        /// The recived data.
        /// </summary>
        public readonly byte[] Data;
        /// <summary>
        /// Initializes an instance of the <see cref="SerialPortLib.MessageReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="data"></param>
        public MessageRecivedEventArgs(byte[] data)
        {
            Data = data;
        }
    }
}
