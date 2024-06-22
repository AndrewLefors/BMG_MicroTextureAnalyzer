using MccDaq;
using MicroneedleAPI;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.IO.Ports;

namespace BMG_MicroTextureAnalyzer
{
    public class Engine : INotifyPropertyChanged
    {
        private MccBoard _board;
        private bool _isMonitoring;
        private MotionController _stage;
        private Connection _connection;
        private string _errorString;
        private readonly ConcurrentQueue<RawDataChangedEventArgs> _dataQueue = new ConcurrentQueue<RawDataChangedEventArgs>();
        private readonly List<ProcessedDataChangedEventArgs> _processedDataList = new List<ProcessedDataChangedEventArgs>();
        private readonly object _dataLock = new object();
        private BackgroundWorker _dataCollectorWorker;
        private BackgroundWorker _dataProcessorWorker;
        private BackgroundWorker _stageWorker;
        private BackgroundWorker _stageStarter;
        private bool _isRunning;

        private bool thresholdMet = false;
        private bool _isStageMoving;

        private double _voltage;


        public event EventHandler<ProcessedDataChangedEventArgs> DataChanged;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };


        public void StartMonitor()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;

            _dataCollectorWorker = new BackgroundWorker();
            _dataCollectorWorker.DoWork += DataCollectorWorker_DoWork;
            _dataCollectorWorker.WorkerSupportsCancellation = true;
            _dataCollectorWorker.RunWorkerAsync();

            _dataProcessorWorker = new BackgroundWorker();
            _dataProcessorWorker.DoWork += DataProcessorWorker_DoWork;
            _dataProcessorWorker.WorkerSupportsCancellation = true;
            _dataProcessorWorker.RunWorkerAsync();
        }

        public void FindPlane()
        {
            if (_isRunning)
            {
                return;
            }
            ThresholdMet = false;
            _isRunning = true;
            _isStageMoving = false;
            _dataQueue.Clear();

            _dataCollectorWorker = new BackgroundWorker();
            _dataCollectorWorker.DoWork += DataCollectorWorker_FindPlane;
            _dataCollectorWorker.WorkerSupportsCancellation = true;
            _dataCollectorWorker.RunWorkerAsync();

            _dataProcessorWorker = new BackgroundWorker();
            _dataProcessorWorker.DoWork += DataProcessorWorker_FindPlane;
            _dataProcessorWorker.WorkerSupportsCancellation = true;
            _dataProcessorWorker.RunWorkerAsync();


        }

        public void FractureTest()
        {
            if (_isRunning)
            {
                return;
            }
            ThresholdMet = false;
            _isRunning = true;
            _dataQueue.Clear();

            _dataCollectorWorker = new BackgroundWorker();
            _dataCollectorWorker.DoWork += DataCollectorWorker_FractureTest;
            _dataCollectorWorker.WorkerSupportsCancellation = true;
            _dataCollectorWorker.RunWorkerAsync();

            _dataProcessorWorker = new BackgroundWorker();
            _dataProcessorWorker.DoWork += DataProcessorWorker_FractureTest;
            _dataProcessorWorker.WorkerSupportsCancellation = true;
            _dataProcessorWorker.RunWorkerAsync();
        }

        public bool ThresholdMet
        {
            get { return thresholdMet; }
            set
            {
                if (thresholdMet != value)
                {
                    thresholdMet = value;
                    OnPropertyChanged(nameof(ThresholdMet));
                }
            }
        }

        public double Voltage
        {
            get { return _voltage; }
            set
            {
                if (_voltage != value)
                {
                    _voltage = value;
                    OnPropertyChanged(nameof(Voltage));
                }
            }
        }
        public async Task StopAsync()
        {
            if (!_isRunning) return;

            _dataCollectorWorker.CancelAsync();
            _dataProcessorWorker.CancelAsync();
            //_stageWorker.CancelAsync();

            while (_dataCollectorWorker.IsBusy || _dataProcessorWorker.IsBusy)
            {
                await Task.Delay(100);
            }
            _dataQueue.Clear();
            _isRunning = false;
            ThresholdMet = false;
        }

        public List<ProcessedDataChangedEventArgs> GetProcessedData()
        {
            lock (_dataLock)
            {
                return new List<ProcessedDataChangedEventArgs>(_processedDataList);
            }
        }

        private void DataCollectorWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (this._board == null)
            {
                this._board = new MccBoard(1);
            }
            int channel = 7;
            MccDaq.Range range = MccDaq.Range.BipPt078Volts;
            while (!_dataCollectorWorker.CancellationPending)
            {
                MccDaq.ErrorInfo ulStat = this._board.AIn32(channel, range, out int rawData, 0);
                if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                {
                    throw new Exception("Error reading analog input: " + ulStat.Message);
                }
                //ulStat = daqBoard.ToEngUnits32(range, rawData, out double voltage);
                //Create new datachangedevent args to store the timestamp and voltage
                RawDataChangedEventArgs dataChangedEventArgs = new RawDataChangedEventArgs(rawData);
                _dataQueue.Enqueue(dataChangedEventArgs);
                Thread.Sleep(1); // Adjust sampling rate as necessary
            }
        }

        private void DataCollectorWorker_FindPlane(object sender, DoWorkEventArgs e)
        {
            if (this._board == null)
            {
                this._board = new MccBoard(1);
            }
           
            int channel = 7;
            MccDaq.Range range = MccDaq.Range.BipPt078Volts;
            TranslateYStage(-100); // Move the stage 100mm down to get the stage on the sample


            while (!_dataCollectorWorker.CancellationPending && !ThresholdMet)
            {
                MccDaq.ErrorInfo ulStat = this._board.AIn32(channel, range, out int rawData, 0);
                if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                {
                    throw new Exception("Error reading analog input: " + ulStat.Message);
                }
                //ulStat = daqBoard.ToEngUnits32(range, rawData, out double voltage);
                //Create new datachangedevent args to store the timestamp and voltage
                RawDataChangedEventArgs dataChangedEventArgs = new RawDataChangedEventArgs(rawData);
                _dataQueue.Enqueue(dataChangedEventArgs);
                Thread.Sleep(5); // Adjust sampling rate as necessary

            }
            
            
            e.Cancel = true;
        }

        private void DataCollectorWorker_FractureTest(object sender, DoWorkEventArgs e)
        {
            if (this._board == null)
            {
                this._board = new MccBoard(1);
            }

            int channel = 7;
            MccDaq.Range range = MccDaq.Range.BipPt078Volts;
            TranslateYStage(-1); // Move the stage 100mm down to get the stage on the sample

            while (!_dataCollectorWorker.CancellationPending && !ThresholdMet)
            {
                MccDaq.ErrorInfo ulStat = this._board.AIn32(channel, range, out int rawData, 0);
                if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                {
                    throw new Exception("Error reading analog input: " + ulStat.Message);
                }
                //ulStat = daqBoard.ToEngUnits32(range, rawData, out double voltage);
                //Create new datachangedevent args to store the timestamp and voltage
                RawDataChangedEventArgs dataChangedEventArgs = new RawDataChangedEventArgs(rawData);
                _dataQueue.Enqueue(dataChangedEventArgs);
                Thread.Sleep(5); // Adjust sampling rate as necessary

            }


            e.Cancel = true;
        }

        private void DataProcessorWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!((BackgroundWorker)sender).CancellationPending)
            {
                if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                {
                    MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(MccDaq.Range.BipPt078Volts, args.RawData, out double voltage);
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        throw new Exception("Error converting raw data to voltage: " + ulStat.Message);
                    }
                    ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp);
                    lock (_dataLock)
                    {
                        _processedDataList.Add(processedData);
                    }
                    OnDataChanged(processedData);
                }
               // Thread.Sleep(1);// Adjust processing rate as necessary
            }

            e.Cancel = true;
        }

        private void DataProcessorWorker_FindPlane(object sender, DoWorkEventArgs e)
        {
            
            while (!((BackgroundWorker)sender).CancellationPending && !ThresholdMet)
            {
                if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                {
                    MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(MccDaq.Range.BipPt078Volts, args.RawData, out double voltage);
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        throw new Exception("Error converting raw data to voltage: " + ulStat.Message);
                    }
                    if ((voltage * 2141.878 * 4.4488) > 1.1)
                    {
                        ThresholdMet = true;
                        this.Stage.Stop();
                    }
                    ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp);
                    lock (_dataLock)
                    {
                        _processedDataList.Add(processedData);
                    }
                    OnDataChanged(processedData);
                }
                //Thread.Sleep(1);// Adjust processing rate as necessary
            }
            Thread.Sleep(1000);
            TranslateYStage(0.25);
            e.Cancel = true;
            
            //((BackgroundWorker)sender).CancelAsync();

        }

        private void DataProcessorWorker_FractureTest(object sender, DoWorkEventArgs e)
        {

            while (!((BackgroundWorker)sender).CancellationPending && !ThresholdMet)
            {
                if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                {
                    MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(MccDaq.Range.BipPt078Volts, args.RawData, out double voltage);
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        throw new Exception("Error converting raw data to voltage: " + ulStat.Message);
                    }
                    if ((voltage * 2141.878) >= 25)
                    {
                        ThresholdMet = true;
                        this.Stage.Stop();
                    }
                    ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp);
                    lock (_dataLock)
                    {
                        _processedDataList.Add(processedData);
                    }
                    OnDataChanged(processedData);
                }
                //Thread.Sleep(1);// Adjust processing rate as necessary
            }
            Thread.Sleep(1000);
            TranslateYStage(10);
            e.Cancel = true;

            //((BackgroundWorker)sender).CancelAsync();

        }

        private void StageWorker_StartStage(object sender, DoWorkEventArgs e)
        {
            _isStageMoving = true;
            // Start stage movement
            TranslateYStage(100);
        }

        private void StageWorker_FindPlane(object sender, DoWorkEventArgs e)
        {
       

            // Monitor for threshold signal
            while (_isStageMoving && !((BackgroundWorker)sender).CancellationPending)
            {
                // Check threshold from the latest processed data
                //double latestVoltage = GetLatestProcessedVoltage();
                if (ThresholdMet)
                {
                    // Stop stage movement if threshold is met
                    StopMotionController(); 
                    _isStageMoving = false;
                    
                }

                //Thread.Sleep(100); // Adjust control rate as necessary
            }

            e.Cancel = true;
        }

        

        protected virtual void OnDataChanged(ProcessedDataChangedEventArgs e)
        {
            DataChanged?.Invoke(this, e);
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Engine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Use the class name as a prefix for the property name
            string propertyName = sender.GetType().Name + "." + e.PropertyName;

            // Invoke the Engine's PropertyChanged event
            OnPropertyChanged(propertyName);
        }


        public MotionController Stage
        {
            get { return this._stage; }
            set
            {
                if (this._stage != value)
                {
                    this._stage = value;
                    this.OnPropertyChanged(nameof(Stage));
                }
            }
        }

        public Connection Connection
        {
            get { return this._connection; }
            set
            {
                if (this._connection != value)
                {
                    this._connection = value;
                    this.OnPropertyChanged(nameof(Connection));
                }
            }
        }

        public string ErrorString
        {
            get { return this._errorString; }
            set
            {
                if (this._errorString != value)
                {
                    this._errorString = value;
                    this.OnPropertyChanged(nameof(ErrorString));
                }
            }
        }

        public Engine()
        {
            this.Connection = new Connection();
            this.Stage = new MotionController();
            this.Connection.PropertyChanged += Engine_PropertyChanged;
            this.Stage.PropertyChanged += Engine_PropertyChanged;
            _dataQueue = new ConcurrentQueue<RawDataChangedEventArgs>();
            _processedDataList = new List<ProcessedDataChangedEventArgs>();
            _isRunning = false;
            _dataLock = new object();
            this._board = new MccBoard(1);


        }


        public void GetAvailableDevices()
        {
            try
            {
                this.Connection.GetOpenPorts();
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;

            }
        }


        public void ConnectToMotionController(short port)
        {
            try
            {
                if (this.Stage != null)
                {
                    this.Stage.ClosePort();
                }
                MotionController newStage = new MotionController();
                this.Stage = newStage;
                this.Stage.ConnectPort(port);
                this.Stage.PropertyChanged += Engine_PropertyChanged;

            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }

        }

        public void HomeYStage()
        {
            try
            {
                if (this.Stage != null)
                {
                    //Make this run on a seperate thread so the ui is responsive

                    this.Stage.ReturnYToOrigin();
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }

        }

        public void TranslateYStage(double distance)
        {
            try
            {
                if (this.Stage != null)
                {
                    this.Stage.MoveYAbsolute(distance);
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        public void StopMotionController()
        {
            try
            {
                if (this.Stage != null)
                {
                    this.Stage.Stop();
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        public void SetMotorDegree(double motorDeg)
        {
            try
            {

                this.Stage.MotorDegree = motorDeg;

            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        public void SetScrewLeadPitch(double pitch)
        {
            try
            {
                this.Stage.LeadScrewPitch = pitch;
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        public void SetSubdivision(int subDiv)
        {
            try
            {
                this.Stage.Subdivision = subDiv;
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        public void CalculatePulseEquivalent()
        {
            try
            {
                if (this.Stage != null)
                {
                    this.Stage.CalculatePulseEquiv();

                }
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        public void SetStageSpeed(short speed)
        {
            try
            {
                if (this.Stage != null)
                {
                    this.Stage.SetSpeed(speed);
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        //create class for event args that has timestamp and voltage
        public class ProcessedDataChangedEventArgs : EventArgs
        {
            public double TimeStamp { get; }
            public double Voltage { get; }

            public ProcessedDataChangedEventArgs(double voltage, double TimeStamp_seconds)
            {
                //Get the current time in total seconds
                TimeStamp = TimeStamp_seconds;
                Voltage = voltage;
            }
        }

        public class RawDataChangedEventArgs : EventArgs
        {
            public double TimeStamp { get; }
            public int RawData { get; }

            public RawDataChangedEventArgs(int rawData)
            {
                TimeStamp = DateTime.Now.TimeOfDay.TotalSeconds;
                RawData = rawData;
            }


        }
    }
}
