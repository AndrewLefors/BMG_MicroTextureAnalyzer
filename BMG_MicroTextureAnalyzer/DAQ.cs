using System;
using System.ComponentModel;
using System.Threading;
using MccDaq;

namespace BMG_MicroTextureAnalyzer
{

    public class MccDaqWrapper : INotifyPropertyChanged
    {
        // Event to notify subscribers of data changes
        public event EventHandler<DataChangedEventArgs> DataChanged;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        // MCC DAQ Board instance
        private readonly MccBoard _board;
        private bool _isMonitoring;
        private Thread _monitorThread;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Constructor initializes the MCC DAQ board
        public MccDaqWrapper(int boardNumber = 0)
        {
            _board = new MccBoard(boardNumber);
            _board.FlashLED();
        }



        public bool IsMonitoring
        {
            get { return _isMonitoring; }
            set
            {
                if (_isMonitoring != value)
                {
                    _isMonitoring = value;
                    OnPropertyChanged(nameof(IsMonitoring));
                }
            }
        }


        // Reads an analog input from a specified channel and range
        public float ReadAnalogInput(int channel, MccDaq.Range range)
        {
            ErrorInfo ulStat = _board.AIn(channel, range, out ushort dataValue);
            if (ulStat.Value != ErrorInfo.ErrorCode.NoErrors)
            {
                throw new Exception("Error reading analog input: " + ulStat.Message);
            }

            float voltage = ConvertDataToVoltage(dataValue, range);
            OnDataChanged(new DataChangedEventArgs(channel, voltage));
            OnPropertyChanged(nameof(ReadAnalogInput));
            return voltage;
        }

        // Converts raw data value to voltage based on the range
        private float ConvertDataToVoltage(ushort dataValue, MccDaq.Range range)
        {
            // Assuming a 16-bit resolution DAQ device with ±10V range for simplicity
            // Adjust the conversion logic based on the actual device specs and range
            //_board.
            const float maxVoltage = 10f;
            const float minVoltage = -10.0f;
            float voltageRange = maxVoltage - minVoltage;
            float voltage = (dataValue / 65536.0f) * voltageRange + minVoltage;
            return voltage;
        }

        // Triggers the DataChanged event
        protected virtual void OnDataChanged(DataChangedEventArgs e)
        {
            DataChanged?.Invoke(this, e);
            OnPropertyChanged(nameof(DataChanged));
        }

        // Starts monitoring the specified channel at regular intervals
        public void StartMonitoring(int channel, MccDaq.Range range, int intervalMs)
        {
            if (_isMonitoring)
                throw new InvalidOperationException("Monitoring is already in progress.");

            _isMonitoring = true;
            _monitorThread = new Thread(() => MonitorChannel(channel, range, intervalMs))
            {
                IsBackground = true
            };
            _monitorThread.Start();
        }

        // Stops monitoring
        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitorThread?.Join();
        }

        // Monitors the specified channel and raises DataChanged events
        private void MonitorChannel(int channel, MccDaq.Range range, int intervalMs)
        {
            while (_isMonitoring)
            {
                try
                {
                    ReadAnalogInput(channel, range);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred while monitoring: " + ex.Message);
                    _isMonitoring = false;
                }
                Thread.Sleep(intervalMs);
            }
        }
    }

    // Event arguments for data changed event
    public class DataChangedEventArgs : EventArgs
    {
        public int Channel { get; }
        public float Voltage { get; }

        public DataChangedEventArgs(int channel, float voltage)
        {
            Channel = channel;
            Voltage = voltage;
        }
    }



}
