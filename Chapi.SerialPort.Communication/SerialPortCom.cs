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
    }
}
