using System;
using System.Threading;
using System.Diagnostics;
using System.IO.Ports;
using NLog;

namespace Chapi.SerialPortLib.Communication
{
    /// <summary>
    /// Serial Port communication Input/Output
    /// </summary>
    public class SerialPortCom
    {
        #region Private Attributes

        internal static Logger logger = LogManager.GetCurrentClassLogger();

        private SerialPort _serialPort;
        private string _portName = "";
        private Parity _parity = Parity.None;
        private StopBits _stopBits = StopBits.One;
        private int _baudRate = 9600;

        // Error state variable for Read/write
        private bool hasReadWriteError = true;

        // Serial Port reader Task
        private Thread reader;
        // Serial Port connection watcher
        private Thread connectionWatcher;

        private object accessLock = new object();
        private bool disconnectRequested = false;

        #endregion

        #region Public Events

        /// <summary>
        /// Connected state change event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public delegate void ConnectionStatusChangedEventHandler(object sender, ConnectionStatusChangedEventArgs args);
        /// <summary>
        /// Event for connected state change.
        /// </summary>
        public event ConnectionStatusChangedEventHandler ConnectionStatusChanged;

        /// <summary>
        /// Message received event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public delegate void MessageReceivedEventHandler(object sender, MessageReceivedEventArgs args);
        /// <summary>
        /// Event for message received.
        /// </summary>
        public event MessageReceivedEventHandler MessageReceived;

        #endregion
    }
}
