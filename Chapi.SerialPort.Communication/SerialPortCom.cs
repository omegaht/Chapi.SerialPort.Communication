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
        /// <summary>
        /// Sets the serial port options.
        /// </summary>
        /// <param name="portname">Portname.</param>
        /// <param name="baudrate">Baudrate.</param>
        /// <param name="stopbits">Stopbits.</param>
        /// <param name="parity">Parity.</param>
        public void SetPort(string portname, int baudrate = 115200, StopBits stopbits = StopBits.One, Parity parity = Parity.None)
        {
            if (_portName != portname)
            {
                // set to error so that the connection watcher will reconnect
                // using the new port
                hasReadWriteError = true;
            }
            _portName = portname;
            _baudRate = baudrate;
            _stopBits = stopbits;
            _parity = parity;
        }

        /// <summary>
        /// Sends the message.
        /// </summary>
        /// <returns><c>true</c>, if message was sent, <c>false</c> otherwise.</returns>
        /// <param name="message">Message.</param>
        public bool SendMessage(byte[] message)
        {
            bool success = false;
            if (IsConnected)
            {
                try
                {
                    _serialPort.Write(message, 0, message.Length);
                    success = true;
                    logger.Debug(BitConverter.ToString(message));
                }
                catch (Exception e)
                {
                    logger.Error(e);
                }
            }
            return success;
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

        #region Background Tasks

        private void ReaderTask()
        {
            while (IsConnected)
            {
                int messageLenght = 0;
                try
                {
                    messageLenght = _serialPort.BytesToRead;
                    if (messageLenght > 0)
                    {
                        byte[] message = new byte[messageLenght];
                        int readBytes = 0;
                        while (_serialPort.Read(message, readBytes, messageLenght - readBytes) <= 0)
                            ;// no
                        if (MessageReceived != null)
                        {
                            OnMessageReceived(new MessageReceivedEventArgs(message));
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    hasReadWriteError = true;
                    Thread.Sleep(1000);
                    throw;
                }
            }
        }

        private void ConnectionWatcherTask()
        {
            // Reconnects if the connection is drop or if an I/O error occurs.
            while (!disconnectRequested)
            {
                if (hasReadWriteError)
                {
                    try
                    {
                        Close();
                        //wait for 1 second before reconnecting
                        Thread.Sleep(1000);
                        if (!disconnectRequested)
                        {
                            try
                            {
                                Open();    
                            }
                            catch (Exception e)
                            {
                                logger.Error(e);
                                throw;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e);
                    }
                }
                if (!disconnectRequested)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        #endregion Background Tasks

        #region Events Raising

        /// <summary>
        /// Raises the connected state change event.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnConnectionStatusChanged(ConnectionStatusChangedEventArgs args)
        {
            logger.Debug(args.Connected);
            if (ConnectionStatusChanged != null)
            {
                ConnectionStatusChanged(this, args);
            }
        }

        protected virtual void OnMessageReceived(MessageReceivedEventArgs args)
        {
            logger.Debug(BitConverter.ToString(args.Data));
            if (MessageReceived != null)
            {
                MessageReceived(this, args);
            }
        }

        #endregion Events Raising
    }
}
