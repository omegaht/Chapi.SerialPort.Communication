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

        #region Public Members

        /// <summary>
        /// Value indicating if the serial port is connected.
        /// </summary>
        public bool IsConnected
        {
            get { return _serialPort != null && !hasReadWriteError && !disconnectRequested; }
        }
        /// <summary>
        /// Connect to the serial port.
        /// </summary>
        public bool Connect()
        {
            if (disconnectRequested)
            {
                return false;
            }
            lock (accessLock)
            {
                Disconnect();
                Open();
                connectionWatcher = new Thread(ConnectionWatcherTask);
                connectionWatcher.Start();
            }
            return IsConnected;
        }
        /// <summary>
        /// Disconnect the serial port.
        /// </summary>
        public void Disconnect()
        {
            if (disconnectRequested)
                return;
            disconnectRequested = true;
            Close();
            lock (accessLock)
            {
                if (connectionWatcher != null)
                {
                    if (connectionWatcher.Join(5000))
                        connectionWatcher.Abort();
                    connectionWatcher = null;
                }
                disconnectRequested = false;
            }
        }
        #endregion Public Members

        #region Serial Port handling

        /// <summary>
        /// Opens the port.
        /// </summary>
        /// <returns></returns>
        private bool Open()
        {
            bool success = false;
            lock (accessLock)
            {
                Close();
                try
                {
                    _serialPort = new SerialPort();
                    _serialPort.ErrorReceived += HandleErrorReceived;
                    _serialPort.PortName = _portName;
                    _serialPort.BaudRate = _baudRate;
                    _serialPort.StopBits = _stopBits;
                    _serialPort.Parity = _parity;

                    _serialPort.Open();
                    success = true;
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    Close();
                }
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    hasReadWriteError = false;
                    //Initiate reader thread/ task
                    reader = new Thread(ReaderTask);
                    reader.Start();
                    OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs(true));
                }
            }
            return success;
        }

        /// <summary>
        /// Close port.
        /// </summary>
        private void Close()
        {
            lock (accessLock)
            {
                // Stop reader thread
                if(reader != null)
                {
                    if (!reader.Join(5000))
                        reader.Abort();
                    reader = null;
                }
                if (_serialPort != null)
                {
                    _serialPort.ErrorReceived -= HandleErrorReceived;
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                        OnConnectionStatusChanged(new ConnectionStatusChangedEventArgs(false));
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
                hasReadWriteError = true;
            }
        }

        private void HandleErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            logger.Error(e.EventType);
        }

        #endregion Serial Port handling
    }
}
