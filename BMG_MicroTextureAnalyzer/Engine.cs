using MccDaq;
using MicroneedleAPI;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.IO.Ports;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.InteropServices;
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace BMG_MicroTextureAnalyzer
{
    public class UserNotificationEventArgs : EventArgs
    {
        public string Message { get; }
        public string Caption { get; }
        public bool IsError { get; }
        public UserNotificationEventArgs(string message, string caption = null, bool isError = false)
        {
            Message = message; Caption = caption; IsError = isError;
        }
    }

    public class Engine : INotifyPropertyChanged
    {
        private MccBoard _board;
        private bool _isMonitoring;
        private MccDaq.Range _range = MccDaq.Range.Bip10Volts;
        private MotionController _stage;
        private double _yStagePosition;
        private double _stageSpeed = 0; //default speed of 19.1um/s //Stage uses 0-255 as speed values corresponding to the following equation: Actual speed(mm/s) = (speed value+1) * 22000 * pulse equivalent / 720
                                        // The speed value is this stage speed, and pulse equivalent for the lab setup is 1/1600 -> pitch of the lead screw (mm) * stepper angle / (360 *subdivision) = (1*1.8(360*8)) -> 0.000625 
        private Connection _connection;
        private string _errorString;
        private readonly ConcurrentQueue<RawDataChangedEventArgs> _dataQueue = new ConcurrentQueue<RawDataChangedEventArgs>();
        private readonly List<ProcessedDataChangedEventArgs> _processedDataList = new List<ProcessedDataChangedEventArgs>();
        private readonly object _dataLock = new object();
        private BackgroundWorker _dataCollectorWorker;
        private BackgroundWorker _dataProcessorWorker;
        private BackgroundWorker _dataCollectorWorker2;
        private BackgroundWorker _stageWorker;
        private BackgroundWorker _stageStarter;
        private Thread _stagePositionThread;
        private bool _isRunning;

        private bool _fractureTestComplete;
        private bool _punctureTestComplete;

        private bool _isPunctureTest;
        private bool _isFractureTest;

        private bool thresholdMet = false;
        private bool _isStageMoving;

        private double _fractureDistance;
        private double _punctureDistance;
        private double _punctureThreshold = 0.098; // 98mN threshold for puncture test == 10g force
        private double _voltage;

        private double _fractureTestPoundConversion = 2141.878;
        private double _fractureTestNewtonConversion = 4.44822;
        private double _punctureTestKilogramConversion = 1e-3;//7.93688e-4;//7.93688;//3.893e-4;//3.96844;
        private double _punctureTestNewtonConversion = 9.81;// 1kg = 9.81N

        private double _findPlaneThreshold = 5;

        private double _voltageConversion;
        private double _newtonConversion;

        private double _voltageOffset = 0.0;

        // Force offset (Newtons) - engine-side zeroing target
        private double _forceOffset = 0.0;
        public double ForceOffset
        {
            get => _forceOffset;
            set
            {
                if (_forceOffset != value)
                {
                    _forceOffset = value;
                    OnPropertyChanged(nameof(ForceOffset));
                }
            }
        }

        // Recent force buffer for engine-side zeroing (values in Newtons).
        private readonly object _recentForceLock = new object();
        private readonly Queue<double> _recentForces = new Queue<double>();
        private int _recentForceBufferSize = 200; // number of recent samples to keep

        private int _rate = 1000; //default of 1kHz
        private int _maxSampleRate = 3000; // maximum supported sample rate (adjust per hardware)
        private int _actualRate = 1000;

        public int ActualRate
        {
            get => _actualRate;
            private set
            {
                if (_actualRate != value)
                {
                    _actualRate = value;
                    OnPropertyChanged(nameof(ActualRate));
                }
            }
        }

        private double _dataCollectionTime = 10; //default of 10 seconds
        private int _numPoints = 10000; //default of 10000 points (10 seconds @ 1kHz)

        // Use a modest default circular DAQ buffer to avoid huge allocations
        private const int DefaultWinBufSize = 8192;
        private int _winBufSize = DefaultWinBufSize;

        IntPtr memHandle = // allocate memory for data buffer
            MccDaq.MccService.WinBufAlloc32Ex(DefaultWinBufSize); // default small buffer

        // Stage poller and lock-free ring buffer for cached positions
        private const int StagePollBufferSize = 4096; // power of two for fast mask
        private readonly double[] _stagePosBuffer = new double[StagePollBufferSize];
        private readonly double[] _stagePosTsBuffer = new double[StagePollBufferSize];
        private long _stagePosWriteIndex = 0; // incrementing counter
        private double _latestStagePosMm = double.NaN; // cached latest
        private double _latestStagePosTs = 0.0;
        private CancellationTokenSource _stagePollCts;
        private Task _stagePollTask;

        public event EventHandler<ProcessedDataChangedEventArgs> DataChanged;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        // Event UI can subscribe to in order to show MessageBox or other user-facing messages
        // public event EventHandler<UserNotificationEventArgs> UserNotification;

        // NotifyUser removed per user request. Engine will not raise UI notifications automatically.
        

        private void NotifyUser(string message, string caption = null, bool isError = false)
        {
            try
            {
                this.ErrorString = message;
                // UserNotification?.Invoke(this, new UserNotificationEventArgs(message, caption, isError));
            }
            catch { }

            if (isError)
            {
                // Ensure engine is moved to a safe idle state so UI can start new operations.
                try
                {
                    // Stop acquisition and stage immediately and clear running flags
                    StopAllImmediate();

                    // Also set logical flags in case StopAllImmediate didn't clear them
                    _isMonitoring = false;
                    _isRunning = false;
                    _isStageMoving = false;
                    ThresholdMet = false;

                    // Do NOT attempt an automatic reset here - automatic resets caused instability.
                }
                catch
                {
                    // swallow - best-effort cleanup
                }
            }
        }

        public void StartMonitor()
        {
            if (_isRunning)
            {
                return;
            }

            LogInfo("Basic monitoring started", "Engine.Event");

            _isRunning = true;
            ThresholdMet = false;
            _processedDataList.Clear();
            _dataCollectorWorker = new BackgroundWorker();
            _dataCollectorWorker.DoWork += DataCollectorWorker_DoWork;
            _dataCollectorWorker.WorkerSupportsCancellation = true;
            _dataCollectorWorker.RunWorkerAsync();

            _dataProcessorWorker = new BackgroundWorker();
            _dataProcessorWorker.DoWork += DataProcessorWorker_DoWork;
            _dataProcessorWorker.WorkerSupportsCancellation = true;
            _dataProcessorWorker.RunWorkerAsync();

            _dataProcessorWorker.RunWorkerCompleted += (sender, e) =>
            {
                //this.StopAsync();
            };
        }

        public void FindPlane()
        {
            LogInfo($"Find plane started: threshold={FindPlaneThreshold:F6} N, time={DataCollectionTime:F1} s", "Engine.Event");

            // Use same continuous-acquisition pipeline as ContinuousScanTest,
            // but start stage motion only after acquisition is running to avoid missed samples.
            this.GetYLocation();
            if (_isRunning)
            {
                return;
            }
            if (this._isFractureTest)
            {
                _voltageConversion = _fractureTestPoundConversion;
                _newtonConversion = _fractureTestNewtonConversion;
            }
            else if (this._isPunctureTest)
            {
                _voltageConversion = _punctureTestKilogramConversion;
                _newtonConversion = _punctureTestNewtonConversion;
            }

            // allocate DAQ buffer
            try
            {
                _numPoints = (int)(DataCollectionTime * Rate);
                int allocSize = Math.Min(_numPoints, DefaultWinBufSize);
                if (MemHandle != IntPtr.Zero) MccDaq.MccService.WinBufFreeEx(MemHandle);
                MemHandle = MccDaq.MccService.WinBufAlloc32Ex(allocSize);
                _winBufSize = allocSize;
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }

            if (MemHandle == IntPtr.Zero)
            {
                this.ErrorString = "Error allocating memory for data buffer";
                return;
            }

            ThresholdMet = false;
            _isRunning = true;
            _isMonitoring = true;
            _isStageMoving = false; // will set true when motion actually starts
            _dataQueue.Clear();
            _processedDataList.Clear();

            // Start continuous acquisition workers (same as ContinuousScanTest)
            _dataCollectorWorker = new BackgroundWorker();
            _dataCollectorWorker.DoWork += DataCollectorWorker_ContinuousScan;
            _dataCollectorWorker.WorkerSupportsCancellation = true;
            _dataCollectorWorker.RunWorkerAsync();

            _dataCollectorWorker2 = new BackgroundWorker();
            _dataCollectorWorker2.DoWork += DataReaderWorker_ContinuousScan;
            _dataCollectorWorker2.WorkerSupportsCancellation = true;
            _dataCollectorWorker2.RunWorkerAsync();

            _dataProcessorWorker = new BackgroundWorker();
            _dataProcessorWorker.DoWork += DataProcessorWorker_ContinuousScanInput;
            _dataProcessorWorker.WorkerSupportsCancellation = true;
            _dataProcessorWorker.RunWorkerAsync();

            // After acquisition has started, move the stage so no data is missed.
            Task.Run(() =>
            {
                try
                {
                    // Wait briefly for acquisition to settle (ActualRate set by DataCollectorWorker)
                    int waited = 0;
                    while (this.ActualRate <= 0 && waited < 2000)
                    {
                        Thread.Sleep(20);
                        waited += 20;
                    }
                    // perform stage move if available
                    try
                    {
                        TranslateYStage(-100); // move toward sample
                        _isStageMoving = true;
                    }
                    catch (Exception ex)
                    {
                        this.ErrorString = "Stage move failed: " + ex.Message;
                    }
                }
                catch { }
            });

            // Ensure workers clear state on completion
            _dataProcessorWorker.RunWorkerCompleted += (s, e) =>
            {
                // Final cleanup if threshold reached or worker finished
                try { StopBackgroundCollection(); } catch { }
                _isStageMoving = false;
                _isMonitoring = false;
                _isRunning = false;
                ThresholdMet = false;
            };
            _dataCollectorWorker.RunWorkerCompleted += (s, e) => { try { _dataCollectorWorker.CancelAsync(); } catch { } };
        }

        public void FractureTest()
        {
            if (_isRunning)
            {
                return;
            }

            LogInfo($"Fracture test started: depth={FractureDistance:F3} mm, time={DataCollectionTime:F1} s, threshold={FindPlaneThreshold:F3} N", "Engine.Event");

            _fractureTestComplete = false;
            ThresholdMet = false;
            _isRunning = true;
            _isMonitoring = true;
             // Move the stage 100mm down to get the stage on the sample
            _isStageMoving = true;
            _isFractureTest = true;
            _isPunctureTest = false;
            _dataQueue.Clear();
            _processedDataList.Clear();
            try
            {
                _numPoints = (int)(DataCollectionTime * Rate);
                int allocSize = Math.Min(_numPoints, DefaultWinBufSize);
                MccDaq.MccService.WinBufFreeEx(MemHandle);
                MemHandle = MccDaq.MccService.WinBufAlloc32Ex(allocSize);
                _winBufSize = allocSize;
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
                System.Diagnostics.Debug.WriteLine(this.ErrorString);
            }

            _dataCollectorWorker = new BackgroundWorker();
            _dataCollectorWorker.DoWork += DataCollectorWorker_FractureTest;
            _dataCollectorWorker.WorkerSupportsCancellation = true;
            _dataCollectorWorker.RunWorkerAsync();
            //this.TranslateYStage(FractureDistance);
            _dataCollectorWorker2 = new BackgroundWorker();
            // reuse the continuous-scan reader implementation for fracture test
            _dataCollectorWorker2.DoWork += DataReaderWorker_ContinuousScan;
             _dataCollectorWorker2.WorkerSupportsCancellation = true;
             _dataCollectorWorker2.RunWorkerAsync();

            //Task.Run(async () =>
            //{
            //    await Task.Delay(TimeSpan.FromSeconds(5));
            //    this.TranslateYStage(FractureDistance);
            //});


            _dataProcessorWorker = new BackgroundWorker();
            _dataProcessorWorker.DoWork += DataProcessorWorker_FractureTest;
            _dataProcessorWorker.WorkerSupportsCancellation = true;
            _dataProcessorWorker.RunWorkerAsync();
            _dataProcessorWorker.RunWorkerCompleted += (sender, e) =>
            {
                //Add save file dialoge for saving the data for fracture test to csv. This should pop up a confirmation box asking if they want to save, then go through the save file dialoge

                //Save the data to a csv file
                //Create a new save file dialoge

                //FractureTestComplete = true;


                //Re-enabled on 12/31/2024
                //IF AN ERROR OCCURS, COMMENT THIS OUT
                // this.StopAsync();

            };
            //_stageWorker = new BackgroundWorker();
            //_stageWorker.DoWork += StageWorker_ReportStageLocation;
            //_stageWorker.WorkerSupportsCancellation = true;
            //_stageWorker.RunWorkerAsync();
        }

        private void _dataCollectorWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void PunctureTest()
        {
            if (_isRunning)
            {
                return;
            }
            _punctureTestComplete = false;
            ThresholdMet = false;
            _isRunning = true;
            _isPunctureTest = true;
            _isFractureTest = false;
            _dataQueue.Clear();
            _processedDataList.Clear();


            _dataCollectorWorker = new BackgroundWorker();
            //_dataCollectorWorker.DoWork += DataCollectorWorker_PunctureTest;
            _dataCollectorWorker.WorkerSupportsCancellation = true;
            _dataCollectorWorker.RunWorkerAsync();

            _dataProcessorWorker = new BackgroundWorker();
            _dataProcessorWorker.DoWork += DataProcessorWorker_PunctureTest;
            _dataProcessorWorker.WorkerSupportsCancellation = true;
            _dataProcessorWorker.RunWorkerAsync();
            _dataProcessorWorker.RunWorkerCompleted += (sender, e) =>
            {
                //Add save file dialoge for saving the data for fracture test to csv. This should pop up a confirmation box asking if they want to save, then go through the save file dialoge

                //Save the data to a csv file
                //Create a new save file dialoge

                PunctureTestComplete = true;

               // this.StopAsync();
            };
            //_stageWorker = new BackgroundWorker();
            //_stageWorker.DoWork += StageWorker_ReportStageLocation;
            //_stageWorker.WorkerSupportsCancellation = true;
            //_stageWorker.RunWorkerAsync();
        }

        public void ContinuousScanTest()
        {
            if (_isRunning)
            {
                return;
            }

            LogInfo($"Continuous scan started: time={DataCollectionTime:F1} s, rate={Rate} Hz", "Engine.Event");

            if (this._isFractureTest)
            {
                _voltageConversion = _fractureTestPoundConversion;
                _newtonConversion = _fractureTestNewtonConversion;
            }
            else if (this._isPunctureTest)
            {
                _voltageConversion = _punctureTestKilogramConversion;
                _newtonConversion = _punctureTestNewtonConversion;
            }

            // Free the buffer and allocate a new one to Memhandle
            try
            {
              _numPoints = (int)(DataCollectionTime * Rate);
              int allocSize = Math.Min(_numPoints, DefaultWinBufSize);
              MccDaq.MccService.WinBufFreeEx(MemHandle);
              MemHandle = MccDaq.MccService.WinBufAlloc32Ex(allocSize);
              _winBufSize = allocSize;
            }
            catch(Exception ex)
            {
                this.ErrorString = ex.Message;
            }
            


            if (MemHandle == 0)
            {
                this.ErrorString = "Error allocating memory for data buffer";
                return;
            }
            _isMonitoring = true;
            _isRunning = true;
            _isStageMoving = false;
            _isFractureTest = false;
            _isPunctureTest = false;
            ThresholdMet = false;
            _dataQueue.Clear();
            _processedDataList.Clear();

            

            //Worker 1
            _dataCollectorWorker = new BackgroundWorker();
            _dataCollectorWorker.DoWork += DataCollectorWorker_ContinuousScan;
            _dataCollectorWorker.WorkerSupportsCancellation = true;
            _dataCollectorWorker.RunWorkerAsync();
            //Worker 2
            _dataCollectorWorker2 = new BackgroundWorker();
            _dataCollectorWorker2.DoWork += DataReaderWorker_ContinuousScan;
            _dataCollectorWorker2.WorkerSupportsCancellation = true;
            _dataCollectorWorker2.RunWorkerAsync();
            //Processor1
            _dataProcessorWorker = new BackgroundWorker();
            _dataProcessorWorker.DoWork += DataProcessorWorker_ContinuousScanInput;
            _dataProcessorWorker.WorkerSupportsCancellation = true;
            _dataProcessorWorker.RunWorkerAsync();
            


            _dataProcessorWorker.RunWorkerCompleted += (sender, e) =>
            {
               // _dataProcessorWorker.CancelAsync();
                //_dataProcessorWorkerDispose();  
            };
            _dataCollectorWorker.RunWorkerCompleted -= (sender, e) =>
            {
                //_dataCollectorWorker.CancelAsync();
               // _dataCollectorWorker.Dispose();
               //this.IsMonitoring = false;
               //this._isStageMoving = false;

            };
            _dataCollectorWorker2.RunWorkerCompleted -= (sender, e) =>
            {
              //_dataCollectorWorker2.CancelAsync();
              //_dataCollectorWorker2.Dispose();
               
            };
          
            ThresholdMet = false;

        }

        private void _dataCollectorWorker_ContinuousScan_(object? sender, DoWorkEventArgs e)
        {
            throw new NotImplementedException();
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

        public double YStagePosition
        {
            get { return _yStagePosition; }
            set
            {
                if (_yStagePosition != value)
                {
                    _yStagePosition = value;
                    OnPropertyChanged(nameof(YStagePosition));
                }
            }
        }

        public double FindPlaneThreshold
        {
            get { return _findPlaneThreshold; }
            set
            {
                if (_findPlaneThreshold != value)
                {
                    _findPlaneThreshold = value;
                    OnPropertyChanged(nameof(FindPlaneThreshold));
                    LogInfo($"Threshold changed: {_findPlaneThreshold:F6} N", "Engine.Config");
                }
            }
        }

        public int Rate
        {
            get { return _rate; }
            set
            {
                if (_rate != value)
                {
                    int oldRate = _rate;
                    _rate = value;
                    _numPoints = (int)(DataCollectionTime * Rate);
                    OnPropertyChanged(nameof(Rate));
                    LogInfo($"Sampling rate changed: {oldRate} Hz → {_rate} Hz", "DAQ.Config");
                }
            }
        }

        /// <summary>
        /// Safely change the sampling rate. Only updates the Rate property and reallocates
        /// the DAQ buffer. Does NOT start or restart any acquisition - user must explicitly
        /// start a test routine by pressing a button.
        /// </summary>
        public void SetSamplingRate(int newRate)
        {
            if (newRate <= 0) return;

            if (newRate > _maxSampleRate)
            {
                this.ErrorString = $"Requested rate {newRate} Hz exceeds max {_maxSampleRate} Hz. Clamping.";
                newRate = _maxSampleRate;
            }

            LogInfo($"Changing sampling rate to {newRate} Hz...", "DAQ.Config");

            // If currently monitoring, stop first (but do NOT auto-restart)
            if (this.IsMonitoring)
            {
                try
                {
                    LogWarning("Stopping active acquisition to change sampling rate", "DAQ.Config");
                    this.StopBackgroundCollection();
                    if (_dataCollectorWorker != null && _dataCollectorWorker.IsBusy) _dataCollectorWorker.CancelAsync();
                    if (_dataCollectorWorker2 != null && _dataCollectorWorker2.IsBusy) _dataCollectorWorker2.CancelAsync();
                    if (_dataProcessorWorker != null && _dataProcessorWorker.IsBusy) _dataProcessorWorker.CancelAsync();
                }
                catch (Exception ex)
                {
                    this.ErrorString = "Error stopping existing scan: " + ex.Message;
                }

                // Wait for workers to stop
                int wait = 0;
                while (((_dataCollectorWorker != null && _dataCollectorWorker.IsBusy) || 
                        (_dataCollectorWorker2 != null && _dataCollectorWorker2.IsBusy) || 
                        (_dataProcessorWorker != null && _dataProcessorWorker.IsBusy)) && wait < 2000)
                {
                    Thread.Sleep(50);
                    wait += 50;
                }
                
                // Clear running flags - user must explicitly start a new test
                _isMonitoring = false;
                _isRunning = false;
            }

            // Set new rate
            this.Rate = newRate;
            _numPoints = (int)(DataCollectionTime * Rate);

            // Reallocate DAQ buffer with appropriate size for the rate
            try
            {
                if (MemHandle != IntPtr.Zero)
                {
                    MccDaq.MccService.WinBufFreeEx(MemHandle);
                    MemHandle = IntPtr.Zero;
                }
                int bufSize = Math.Min(Math.Max(newRate, DefaultWinBufSize), 65536);
                MemHandle = MccDaq.MccService.WinBufAlloc32Ex(bufSize);
                _winBufSize = bufSize;
                if (MemHandle == IntPtr.Zero)
                {
                    this.ErrorString = $"Failed to allocate DAQ buffer for rate={newRate} Hz, bufSize={bufSize}";
                    return;
                }
                LogInfo($"DAQ buffer allocated: size={bufSize} samples", "DAQ.Config");
                System.Diagnostics.Debug.WriteLine($"[RATE] SetSamplingRate: newRate={newRate}, bufSize={bufSize}");
            }
            catch (Exception ex)
            {
                this.ErrorString = "Buffer allocation error: " + ex.Message;
                return;
            }

            // DO NOT restart workers - user must explicitly start a test routine
        }

        public int NumPoints
        {
            get { return _numPoints; }
           
        }

        public double DataCollectionTime
        {
            get { return _dataCollectionTime; }
            set
            {
                if (_dataCollectionTime != value)
                {
                    _dataCollectionTime = value;
                    _numPoints = (int)(DataCollectionTime * Rate);
                    OnPropertyChanged(nameof(DataCollectionTime));
                    LogInfo($"Collection time set: {_dataCollectionTime:F2} s", "Engine.Config");
                }
            }
        }

        //public int AverageWindow
           // { get { return _averageWindow; }

        public bool IsStageRunning
        {         
            get { return _isStageMoving; }
                   set
            {
                if (_isStageMoving != value)
                {
                    _isStageMoving = value;
                    OnPropertyChanged(nameof(IsStageRunning));
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

        public double VoltageOffset
        {
            get { return _voltageOffset; }
            set
            {
                if (_voltageOffset != value)
                {
                    _voltageOffset = value;
                    OnPropertyChanged(nameof(VoltageOffset));
                }
            }
        }

        /// <summary>
        /// Adds a processed force sample (Newtons) to the engine's recent-sample buffer.
        /// Call this after creating a ProcessedDataChangedEventArgs so the buffer contains
        /// the engine's processed force samples (before offset application).
        /// </summary>
        private void AddRecentForceSample(double newtons)
        {
            lock (_recentForceLock)
            {
                _recentForces.Enqueue(newtons);
                while (_recentForces.Count > _recentForceBufferSize)
                    _recentForces.Dequeue();
            }
        }

        /// <summary>
        /// Compute a robust zero (force offset in Newtons) from the recent processed-force samples and set ForceOffset.
        /// Uses median by default which is robust to spikes.
        /// </summary>
        public void ComputeAndSetForceOffset(int sampleCount = 100, bool useMedian = true)
        {
            double[] snap;
            lock (_recentForceLock)
            {
                if (_recentForces.Count == 0) return;
                int take = Math.Min(sampleCount, _recentForces.Count);
                snap = _recentForces.Skip(Math.Max(0, _recentForces.Count - take)).Take(take).ToArray();
            }

            if (snap.Length == 0) return;

            double offset;
            if (useMedian)
            {
                Array.Sort(snap);
                int m = snap.Length / 2;
                offset = (snap.Length % 2 == 1) ? snap[m] : ((snap[m - 1] + snap[m]) / 2.0);
            }
            else
            {
                offset = snap.Average();
            }

            this.ForceOffset = offset;
        }

        public void SetForceOffset(double newtons)
        {
            this.ForceOffset = newtons;
        }

        public double VoltConversion
        {
            get { return _voltageConversion; }
            set
            {
                if (_voltageConversion != value)
                {
                    _voltageConversion = value;
                    OnPropertyChanged(nameof(VoltConversion));
                }
            }
        }

        public double NewtConversion
        {
            get { return _newtonConversion; }
            set
            {
                if (_newtonConversion != value)
                {
                    _newtonConversion = value;
                    OnPropertyChanged(nameof(NewtConversion));
                }
            }
        }

        public bool FractureTestComplete
        {
            get { return _fractureTestComplete; }
            set
            {
                if (_fractureTestComplete != value)
                {
                    _fractureTestComplete = value;
                    OnPropertyChanged(nameof(FractureTestComplete));
                }
            }
        }

        public bool PunctureTestComplete
        {
            get { return _punctureTestComplete; }
            set
            {
                if (_punctureTestComplete != value)
                {
                    _punctureTestComplete = value;
                    OnPropertyChanged(nameof(PunctureTestComplete));
                }
            }
        }

        public double FractureDistance
        {
            get { return _fractureDistance; }
            set
            {
                if (_fractureDistance != value)
                {
                    _fractureDistance = value;
                    OnPropertyChanged(nameof(FractureDistance));
                }
            }
        }

        public double PunctureDistance
        {
            get { return _punctureDistance; }
            set
            {
                if (_punctureDistance != value)
                {
                    _punctureDistance = value;
                    OnPropertyChanged(nameof(PunctureDistance));
                }
            }
        }

        public double PunctureThreshold
        {
            get { return _punctureThreshold; }
            set
            {
                if (_punctureThreshold != value)
                {
                    _punctureThreshold = value;
                    OnPropertyChanged(nameof(PunctureThreshold));
                }
            }
        }

        public double VoltageConversion
        {
            get { return _voltageConversion; }
            set
            {
                if (_voltageConversion != value)
                {
                    _voltageConversion = value;
                    OnPropertyChanged(nameof(VoltageConversion));
                }
            }
        }

        public double FractureVoltageConversion
        {
            get { return _fractureTestPoundConversion; }
            set
            {
                if (_fractureTestPoundConversion != value)
                {
                    _fractureTestPoundConversion = value;
                    OnPropertyChanged(nameof(FractureVoltageConversion));
                }
            }
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
        public double NewtonConversion
        {
            get { return _newtonConversion; }
            set
            {
                if (_newtonConversion != value)
                {
                    _newtonConversion = value;
                    OnPropertyChanged(nameof(NewtonConversion));
                }
            }
        }

        public double FractureNewtonConversion
        {
            get { return _fractureTestNewtonConversion; }
            set
            {
                if (_fractureTestNewtonConversion != value)
                {
                    _fractureTestNewtonConversion = value;
                    OnPropertyChanged(nameof(FractureNewtonConversion));
                }
            }
        }

        public double PunctureVoltageConversion
        {
            get { return _punctureTestKilogramConversion; }
            set
            {
                if (_punctureTestKilogramConversion != value)
                {
                    _punctureTestKilogramConversion = value;
                    OnPropertyChanged(nameof(PunctureVoltageConversion));
                }
            }
        }

        //How do I access the memory location at a IntPtr?
          public IntPtr MemHandle
        {
                get { return memHandle; }
                set
            {
                 if (memHandle != value)
                {
                      memHandle = value;
                      OnPropertyChanged(nameof(MemHandle));
                 }
                }
          }
        public double PunctureNewtonConversion
        {
            get { return _punctureTestNewtonConversion; }
            set
            {
                if (_punctureTestNewtonConversion != value)
                {
                    _punctureTestNewtonConversion = value;
                    OnPropertyChanged(nameof(PunctureNewtonConversion));
                }
            }
        }

        public bool IsRunning
        {
            get { return _isRunning; }
        }
        public async Task StopAsync()
        {
            if (!_isRunning) return;

            LogInfo("Engine stopping...", "Engine.Event");

            if (IsStageRunning)
            {
                _stage.Stop();
                _isStageMoving = false;
            }
            if (IsMonitoring)
            {
                _board.StopBackground(FunctionType.AiFunction);
                _isMonitoring = false;
            }
            if (_dataCollectorWorker != null && _dataCollectorWorker.IsBusy)
            {
                _dataCollectorWorker.CancelAsync();
            }
            if (_dataProcessorWorker != null && _dataProcessorWorker.IsBusy)
            {
                _dataProcessorWorker.CancelAsync();
            }
            if (_dataCollectorWorker2 != null && _dataCollectorWorker2.IsBusy)
            {
                _dataCollectorWorker2.CancelAsync();
            }
            if (_stageWorker != null && _stageWorker.IsBusy)
            {
                _stageWorker.CancelAsync();
            }

            _dataQueue.Clear();
            _isRunning = false;
            _isMonitoring = false;
            _isStageMoving = false;
            _isFractureTest = false;
            _isPunctureTest = false;
            ThresholdMet = false;

            LogInfo("Engine stopped", "Engine.Event");
         }

         /// <summary>
         /// Stop background data collection gracefully (stops DAQ background and cancels workers).
         /// This matches earlier usage from UI and other processors.
         /// </summary>
         public void StopBackgroundCollection()
        {
            try
            {
                if (this._board != null)
                {
                    try { this._board.StopBackground(FunctionType.AiFunction); } catch { }
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = "Error stopping DAQ background: " + ex.Message;
            }

            try
            {
                _dataCollectorWorker?.CancelAsync();
                _dataCollectorWorker2?.CancelAsync();
                _dataProcessorWorker?.CancelAsync();
            }
            catch (Exception ex)
            {
                this.ErrorString = "Error cancelling workers: " + ex.Message;
            }

            _isMonitoring = false;
            // Clear any transient mode flags so conversions are not left pointing at fracture/puncture
            _isFractureTest = false;
            _isPunctureTest = false;
         }

         public void StopAllImmediate()
         {
            // Immediate stop: stop stage and DAQ background, cancel workers and wait briefly for them to exit.
            try
            {
                if (this.Stage != null)
                {
                    try { this.Stage.Stop(); } catch { }
                    this._isStageMoving = false;
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = "Error stopping stage: " + ex.Message;
            }

            try
            {
                if (this._board != null)
                {
                    try { this._board.StopBackground(FunctionType.AiFunction); } catch { }
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = "Error stopping DAQ background: " + ex.Message;
            }

            // Request cancellation for workers
            try
            {
                _dataCollectorWorker?.CancelAsync();
                _dataCollectorWorker2?.CancelAsync();
                _dataProcessorWorker?.CancelAsync();
                _stageWorker?.CancelAsync();
            }
            catch (Exception ex)
            {
                this.ErrorString = "Error cancelling workers: " + ex.Message;
            }

            // Wait shortly for workers to stop
            int waited = 0;
            while (((_dataCollectorWorker != null && _dataCollectorWorker.IsBusy) || (_dataCollectorWorker2 != null && _dataCollectorWorker2.IsBusy) || (_dataProcessorWorker != null && _dataProcessorWorker.IsBusy)) && waited < 3000)
            {
                Thread.Sleep(50);
                waited += 50;
            }

            // Mark engine as not monitoring/running
            this._isMonitoring = false;
            this._isRunning = false;
            this.ThresholdMet = false;
            // Clear test mode flags to avoid stale conversion selection on next run
            _isFractureTest = false;
            _isPunctureTest = false;
         }

         private void DataCollectorWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (this._board == null)
            {
                this._board = new MccBoard(1);
            }
            int channel = 7;
            MccDaq.Range range = MccDaq.Range.Bip10Volts;

            // Use high-resolution stopwatch for real timestamps
            var stopwatch = Stopwatch.StartNew();

            while (!_dataCollectorWorker.CancellationPending)
            {
                try
                {
                    MccDaq.ErrorInfo ulStat = this._board.AIn32(channel, range, out int rawData, 0);
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        var msg = "AIn32 failed: " + ulStat.Message;
                        this.ErrorString = msg;
                        try { this._board?.StopBackground(FunctionType.AiFunction); } catch { }
                        break;
                    }
                    // Use real wall-clock timestamp
                    double timestamp = stopwatch.Elapsed.TotalSeconds;
                    RawDataChangedEventArgs dataChangedEventArgs = new RawDataChangedEventArgs(rawData, timestamp);
                    _dataQueue.Enqueue(dataChangedEventArgs);
                    Thread.Sleep(2); // Adjust sampling rate as necessary
                }
                catch (Exception ex)
                {
                    var msg = "DataCollectorWorker error: " + ex.Message;
                    this.ErrorString = msg;
                    try { this._board?.StopBackground(FunctionType.AiFunction); } catch { }
                    break;
                }
            }
        }

        private void DataCollectorWorker_ContinuousScan(object sender, DoWorkEventArgs e)
        {
            if (this._board == null)
            {
                this._board = new MccBoard(1);
            }
            int channel = 7;

            MccDaq.Range iaa300 = MccDaq.Range.Bip10Volts;
            int rate = this.Rate;

            System.Diagnostics.Debug.WriteLine($"[RATE] DataCollectorWorker_ContinuousScan starting. Requested rate={rate} Hz, bufSize={_winBufSize}");
            LogInfo($"DAQ acquisition starting: requested rate={rate} Hz, buffer={_winBufSize} samples", "DAQ.Event");

            // Use Background + Continuous for true circular buffer operation
            MccDaq.ErrorInfo ulStat = this._board.AInScan(channel, channel, _winBufSize, ref rate, iaa300, MemHandle,
                 ScanOptions.Background | ScanOptions.Continuous);
            if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
            {
                var msg = "AInScan failed: " + ulStat.Message;
                this.ErrorString = msg;
                return;
            }

            // Store actual rate negotiated by hardware
            this.ActualRate = rate;
            System.Diagnostics.Debug.WriteLine($"[RATE] AInScan returned ActualRate={rate} Hz (requested={this.Rate} Hz)");
            LogInfo($"DAQ acquisition started: actual rate={rate} Hz", "DAQ.Event");

            // Log warning if hardware rate differs significantly from requested
            if (Math.Abs(rate - this.Rate) > 10)
            {
                this.ErrorString = $"Warning: Requested {this.Rate} Hz but hardware returned {rate} Hz";
            }

            while (!_dataCollectorWorker.CancellationPending && !ThresholdMet)
            {
                if (!this._isRunning || !this.IsMonitoring) break;
                Thread.Sleep(1);
            }
            try { _dataCollectorWorker.CancelAsync(); } catch { }
            try { _board?.StopBackground(FunctionType.AiFunction); } catch { }
        }

        // Diagnostic: track buffer wrap events for debugging drift issues
        private long _bufferWrapCount = 0;
        public long BufferWrapCount => _bufferWrapCount;

        // High-resolution stopwatch for real timestamps (shared across workers for a single acquisition run)
        private Stopwatch _acquisitionStopwatch;
        private double _acquisitionStartTimeSec; // wall-clock offset when stopwatch started

        private void DataReaderWorker_ContinuousScan(object sender, DoWorkEventArgs e)
        {
            int lastIndex = 0;
            int[] dataBuffer = new int[Math.Max(1, this._winBufSize)];

            // Wait for ActualRate to be set by the collector worker before proceeding
            int waitedForRate = 0;
            while (this.ActualRate <= 0 && waitedForRate < 2000 && !_dataCollectorWorker2.CancellationPending)
            {
                Thread.Sleep(10);
                waitedForRate += 10;
            }

            if (this.ActualRate <= 0)
            {
                this.ErrorString = "ActualRate not set - cannot compute timestamps";
                return;
            }

            double actualRate = (double)this.ActualRate;
            long totalSamplesRead = 0;

            // Start stopwatch to provide absolute start time
            var stopwatch = Stopwatch.StartNew();
            double startTime = stopwatch.Elapsed.TotalSeconds;

            // Reset wrap counter at start
            _bufferWrapCount = 0;

            System.Diagnostics.Debug.WriteLine($"[TIMESTAMP] DataReaderWorker starting. ActualRate={actualRate} Hz");

            while (!_dataCollectorWorker2.CancellationPending && !ThresholdMet)
            {
                if (!this._isRunning || !this.IsMonitoring) break;

                try
                {
                    this._board.GetStatus(out short status, out int curCount, out int currentIndex, FunctionType.AiFunction);

                    if (MemHandle == IntPtr.Zero)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (currentIndex != lastIndex)
                    {
                        int pointsToRead;

                        if (currentIndex > lastIndex)
                        {
                            pointsToRead = currentIndex - lastIndex;
                            MccDaq.ErrorInfo ulStat = MccDaq.MccService.WinBufToArray32(MemHandle, dataBuffer, lastIndex, pointsToRead);
                            if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                            {
                                this.ErrorString = "WinBufToArray32 failed: " + ulStat.Message;
                                try { _board.StopBackground(FunctionType.AiFunction); } catch { }
                                break;
                            }

                            // Compute timestamp for each sample based on sample index and actual rate
                            for (int i = 0; i < pointsToRead; i++)
                            {
                                double t = startTime + ((double)totalSamplesRead / actualRate);
                                var dataChangedEventArgs = new RawDataChangedEventArgs(dataBuffer[i], t);
                                _dataQueue.Enqueue(dataChangedEventArgs);
                                totalSamplesRead++;
                            }
                        }
                        else
                        {
                            // Buffer wrapped
                            _bufferWrapCount++;

                            // First chunk (end of buffer)
                            int firstChunk = _winBufSize - lastIndex;

                            if (firstChunk > 0)
                            {
                                MccDaq.ErrorInfo ulStat = MccDaq.MccService.WinBufToArray32(MemHandle, dataBuffer, lastIndex, firstChunk);
                                if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                                {
                                    this.ErrorString = "WinBufToArray32 failed (first chunk): " + ulStat.Message;
                                    try { _board.StopBackground(FunctionType.AiFunction); } catch { }
                                    break;
                                }
                                for (int i = 0; i < firstChunk; i++)
                                {
                                    double t = startTime + ((double)totalSamplesRead / actualRate);
                                    var dataChangedEventArgs = new RawDataChangedEventArgs(dataBuffer[i], t);
                                    _dataQueue.Enqueue(dataChangedEventArgs);
                                    totalSamplesRead++;
                                }
                            }

                            // Second chunk (beginning of buffer)
                            if (currentIndex > 0)
                            {
                                MccDaq.ErrorInfo ulStat2 = MccDaq.MccService.WinBufToArray32(MemHandle, dataBuffer, 0, currentIndex);
                                if (ulStat2.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                                {
                                    this.ErrorString = "WinBufToArray32 failed (second chunk): " + ulStat2.Message;
                                    try { _board.StopBackground(FunctionType.AiFunction); } catch { }
                                    break;
                                }
                                for (int i = 0; i < currentIndex; i++)
                                {
                                    double t = startTime + ((double)totalSamplesRead / actualRate);
                                    var dataChangedEventArgs = new RawDataChangedEventArgs(dataBuffer[i], t);
                                    _dataQueue.Enqueue(dataChangedEventArgs);
                                    totalSamplesRead++;
                                }
                            }
                        }

                        lastIndex = currentIndex;
                    }
                }
                catch (Exception ex)
                {
                    this.ErrorString = "DataReaderWorker error: " + ex.Message;
                    try { _board.StopBackground(FunctionType.AiFunction); } catch { }
                    break;
                }

                Thread.Sleep(1);
            }

            System.Diagnostics.Debug.WriteLine($"[TIMESTAMP] Reader exiting. TotalSamples={totalSamplesRead}, Duration={stopwatch.Elapsed.TotalSeconds:F3}s, Expected={(double)totalSamplesRead/actualRate:F3}s, Wraps={_bufferWrapCount}");
            LogInfo($"DAQ acquisition complete: {totalSamplesRead} samples, {stopwatch.Elapsed.TotalSeconds:F3} s, {_bufferWrapCount} buffer wraps", "DAQ.Data");
            try { _dataCollectorWorker2.CancelAsync(); } catch { }
        }

        private void DataCollectorWorker_FindPlane(object sender, DoWorkEventArgs e)
        {
            if (this._board == null)
            {
                this._board = new MccBoard(1);
            }

            int channel = 7;
            MccDaq.Range range = MccDaq.Range.Bip10Volts;

            // Use high-resolution stopwatch for real timestamps
            var stopwatch = Stopwatch.StartNew();

            while (!_dataCollectorWorker.CancellationPending && !ThresholdMet)
            {
                try
                {
                    MccDaq.ErrorInfo ulStat = this._board.AIn32(channel, range, out int rawData, 0);
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        var msg = "AIn32 failed: " + ulStat.Message;
                        this.ErrorString = msg;
                        break;
                    }
                    // Use real wall-clock timestamp
                    double timestamp = stopwatch.Elapsed.TotalSeconds;
                    RawDataChangedEventArgs dataChangedEventArgs = new RawDataChangedEventArgs(rawData, timestamp);
                    _dataQueue.Enqueue(dataChangedEventArgs);
                    Thread.Sleep(1); // Adjust sampling rate as necessary
                }
                catch (Exception ex)
                {
                    var msg = "DataCollectorWorker_FindPlane error: " + ex.Message;
                    this.ErrorString = msg;
                    break;
                }
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

            MccDaq.Range range = MccDaq.Range.Bip10Volts;
            int rate = this.Rate;

            System.Diagnostics.Debug.WriteLine($"[RATE] DataCollectorWorker_FractureTest starting. Requested rate={rate} Hz, bufSize={_winBufSize}");

            try
            {
                // Use Background + Continuous for true circular buffer operation
                MccDaq.ErrorInfo ulStat = this._board.AInScan(channel, channel, _winBufSize, ref rate, range, MemHandle,
                    ScanOptions.Background | ScanOptions.Continuous);
                if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                {
                    var msg = "AInScan failed: " + ulStat.Message;
                    this.ErrorString = msg;
                    return;
                }

                // Store actual rate negotiated by hardware
                this.ActualRate = rate;
                System.Diagnostics.Debug.WriteLine($"[RATE] FractureTest AInScan returned ActualRate={rate} Hz");

                while (!_dataCollectorWorker.CancellationPending && !ThresholdMet)
                {
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                var msg = "DataCollectorWorker_FractureTest error: " + ex.Message;
                this.ErrorString = msg;
            }
            finally
            {
                try { _dataCollectorWorker.CancelAsync(); } catch { }
            }
        }

        private void DataProcessorWorker_FractureTest(object sender, DoWorkEventArgs e)
        {
            MccDaq.Range range = MccDaq.Range.Bip10Volts;
            while (!((BackgroundWorker)sender).CancellationPending && !ThresholdMet)
            {
                try
                {
                    if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                    {
                        MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(range, args.RawData, out double voltage);
                        if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                        {
                            this.ErrorString = "ToEngUnits32 failed: " + ulStat.Message;
                            this.StopBackgroundCollection();
                            break;
                        }

                        // use cached stage position lookup
                        var pos = GetClosestCachedStagePosition(args.TimeStamp);
                        ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion, null, pos.positionMm, pos.timestampSec);

                        AddRecentForceSample(processedData.Newtons);

                        processedData.Newtons = processedData.Newtons - this.ForceOffset;

                        if (processedData.Newtons >= this.FindPlaneThreshold)
                        {
                            this.ThresholdMet = true;
                            // Immediately stop stage and acquisition to prevent further motion/samples
                            try { StopAllImmediate(); } catch { }
                        }
                        lock (_dataLock)
                        {
                            _processedDataList.Add(processedData);
                        }
                        OnDataChanged(processedData);
                    }
                }
                catch (Exception ex)
                {
                    var msg = "DataProcessorWorker_FractureTest error: " + ex.Message;
                    this.ErrorString = msg;
                    break;
                }
            }
            // Mark fracture test finished and clear fracture mode so future scans pick correct conversion
            try
            {
                FractureTestComplete = true;
            }
            catch { }
            _isFractureTest = false;
            _isMonitoring = false;
            _isRunning = false;
            e.Cancel = true;

         }

        private void DataProcessorWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!((BackgroundWorker)sender).CancellationPending)
            {
                try
                {
                    if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                    {
                        MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(MccDaq.Range.Bip10Volts, args.RawData, out double voltage);
                        if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                        {
                            this.ErrorString = "ToEngUnits32 failed: " + ulStat.Message;
                            continue;
                        }
                        var pos = GetClosestCachedStagePosition(args.TimeStamp);
                        ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion, null, pos.positionMm, pos.timestampSec);
                        AddRecentForceSample(processedData.Newtons);
                        processedData.Newtons = processedData.Newtons - this.ForceOffset;
                        lock (_dataLock)
                        {
                            _processedDataList.Add(processedData);
                        }
                        OnDataChanged(processedData);
                    }
                    Thread.Sleep(2);// Adjust processing rate as necessary
                }
                catch (Exception ex)
                {
                    var msg = "DataProcessorWorker error: " + ex.Message;
                    this.ErrorString = msg;
                    break;
                }
            }

            e.Cancel = true;
        }

        private void DataProcessorWorker_ContinuousScanInput(object sender, DoWorkEventArgs e)
        {
            MccDaq.Range iaa300 = MccDaq.Range.Bip10Volts;
            MccDaq.Range range = MccDaq.Range.Bip10Volts;
            while (!((BackgroundWorker)sender).CancellationPending && !ThresholdMet)
            {
                try
                {
                    if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                    {
                        MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(iaa300, args.RawData, out double voltage);
                        if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                        {
                            this.ErrorString = "ToEngUnits32 failed: " + ulStat.Message;
                            this.StopBackgroundCollection();
                            break;
                        }

                        var pos = GetClosestCachedStagePosition(args.TimeStamp);
                        ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion, this.YStagePosition, pos.positionMm, pos.timestampSec);
                        AddRecentForceSample(processedData.Newtons);
                        processedData.Newtons = processedData.Newtons - this.ForceOffset;
                        Task.Run(() =>
                        {
                            if (processedData.Newtons >= this.FindPlaneThreshold + this.VoltageOffset)
                            {
                                this.ThresholdMet = true;
                                // Stop immediately on engine side to ensure stage halts without delay
                                try { StopAllImmediate(); } catch { }
                                this.IsStageRunning = false;
                                this._board.StopBackground(FunctionType.AiFunction);
                                this.IsMonitoring = false;
                            }
                        });
                        lock (_dataLock)
                        {
                            _processedDataList.Add(processedData);
                        }
                        OnDataChanged(processedData);
                    }
                }
                catch (Exception ex)
                {
                    var msg = "DataProcessorWorker_ContinuousScanInput error: " + ex.Message;
                    this.ErrorString = msg;
                    break;
                }
            }
            e.Cancel = true;
        }

        private void DataProcessorWorker_FindPlane(object sender, DoWorkEventArgs e)
        {
            MccDaq.Range range = MccDaq.Range.Bip10Volts;

            while (!((BackgroundWorker)sender).CancellationPending && !ThresholdMet)
            {
                try
                {
                    if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                    {
                        MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(range, args.RawData, out double voltage);
                        if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                        {
                            this.ErrorString = "ToEngUnits32 failed: " + ulStat.Message;
                            this.StopBackgroundCollection();
                            this.IsMonitoring = false;
                            this.IsStageRunning = false;
                            break;
                        }

                        var pos = GetClosestCachedStagePosition(args.TimeStamp);
                        ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion, this.YStagePosition, pos.positionMm, pos.timestampSec);
                        AddRecentForceSample(processedData.Newtons);
                        processedData.Newtons = processedData.Newtons - this.ForceOffset;
                        if (processedData.Newtons > this.FindPlaneThreshold)
                        {
                            ThresholdMet = true;
                            // Immediately stop stage and DAQ
                            try { StopAllImmediate(); } catch { }
                            this.IsMonitoring = false;
                            this.IsStageRunning = false;
                        }
                        lock (_dataLock)
                        {
                            _processedDataList.Add(processedData);
                        }
                        OnDataChanged(processedData);
                    }
                }
                catch (Exception ex)
                {
                    var msg = "DataProcessorWorker_FindPlane error: " + ex.Message;
                    this.ErrorString = msg;
                    break;
                }
            }

            //Thread.Sleep(1000);
            this.GetYLocation();
            this.Stage.Delay();
            TranslateYStage(0.5);
            e.Cancel = true;
             
            // ((BackgroundWorker)sender).CancelAsync();

         }

        private void DataProcessorWorker_PunctureTest(object sender, DoWorkEventArgs e)
        {

            while (!((BackgroundWorker)sender).CancellationPending && !ThresholdMet)
            {
                try
                {
                    if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                    {
                        MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(MccDaq.Range.Bip10Volts, args.RawData, out double voltage);
                        if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                        {
                            this.ErrorString = "ToEngUnits32 failed: " + ulStat.Message;
                            continue;
                        }

                        var pos = GetClosestCachedStagePosition(args.TimeStamp);
                        ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion, null, pos.positionMm, pos.timestampSec);
                        AddRecentForceSample(processedData.Newtons);
                        processedData.Newtons = processedData.Newtons - this.ForceOffset;
                        if (processedData.Newtons >= this.PunctureThreshold)
                        {
                            ThresholdMet = true;
                            // stop stage immediately
                            try { StopAllImmediate(); } catch { }
                        }
                        lock (_dataLock)
                        {
                            _processedDataList.Add(processedData);
                        }
                        OnDataChanged(processedData);
                    }
                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    var msg = "DataProcessorWorker_PunctureTest error: " + ex.Message;
                    this.ErrorString = msg;
                    break;
                }
            }
             this.SetStageSpeed(100);
             Thread.Sleep(1000);
             TranslateYStage(3);
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
                    // stop poller for previous stage instance
                    try { StopStagePoller(); } catch { }

                    this._stage = value;
                    this.OnPropertyChanged(nameof(Stage));

                    // start poller if newly assigned stage is connected
                    try
                    {
                        if (this._stage != null && this._stage.ConnectionStatus)
                        {
                            StartStagePoller(1, 10);
                        }
                    }
                    catch { }
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
            get { return _errorString; }
            set
            {
                // Always update and raise notification so UI/loggers see every occurrence,
                // even if the text is identical to the previous value.
                _errorString = value;
                OnPropertyChanged(nameof(ErrorString));
            }
        }

        // Logging callbacks - injected by UI layer to avoid direct dependency
        private Action<string, string> _logInfo;    // (message, source)
        private Action<string, string> _logWarning; // (message, source)
        private Action<string, string> _logError;   // (message, source)

        /// <summary>
        /// Set logging callbacks so Engine can log without direct UI dependency.
        /// </summary>
        public void SetLoggers(Action<string, string> logInfo, Action<string, string> logWarning, Action<string, string> logError)
        {
            _logInfo = logInfo;
            _logWarning = logWarning;
            _logError = logError;
        }

        private void LogInfo(string message, string source) => _logInfo?.Invoke(message, source);
        private void LogWarning(string message, string source) => _logWarning?.Invoke(message, source);
        private void LogError(string message, string source) => _logError?.Invoke(message, source);

        public Engine()
        {
            this.Connection = new Connection();
            this.Stage = new MotionController();
            this.Connection.PropertyChanged += Engine_PropertyChanged;
            this.Stage.PropertyChanged += Engine_PropertyChanged;
            _dataQueue = new ConcurrentQueue<RawDataChangedEventArgs>();
            _processedDataList = new List<ProcessedDataChangedEventArgs>();
            _isRunning = false;
            _isMonitoring = false;
            _isStageMoving = false;
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
                //Trying out async method -> causes stage to not respond to stop call GOING TO STOP TRYING TO FIX CONNECTION LABEL FOR NOW
                //this.Stage.ConnectAsync(port);
                // synchronous connect (legacy) - attempt quick open
                try { this.Stage.ConnectPort(port); } catch { }
                 this.Stage.PropertyChanged += Engine_PropertyChanged;

                 // start poller if connected
                 try { if (this.Stage != null && this.Stage.ConnectionStatus) StartStagePoller(1, 10); } catch { }

             }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }

        }

        // Async connect helper that uses the MotionController async connect and starts polling
        public async Task<bool> ConnectToMotionControllerAsync(short port)
        {
            try
            {
                if (this.Stage != null)
                {
                    try { this.Stage.ClosePort(); } catch { }
                }
                var newStage = new MotionController();
                this.Stage = newStage;
                this.Stage.PropertyChanged += Engine_PropertyChanged;
                // MotionController.ConnectAsync returns a Task<bool>
                var result = await this.Stage.ConnectAsync(port);
                // if connected start poller
                try { if (result && this.Stage.ConnectionStatus) StartStagePoller(1, 10); } catch { }
                return result;
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
                return false;
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
                    try { this.Stage.Stop(); } catch { }
                    this._isStageMoving = false;
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
                    LogInfo($"Setting stage speed: {speed}", "Stage.Config");
                    this.Stage.SetSpeed(speed);
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        public void GetYLocation()
        {
            try
            {
                if (this.Stage != null)
                {
                    this.Stage.GetYLocation();
                    //this.YStagePosition = this.Stage.CurrentYStep;
                   // Console.WriteLine(this.Stage.CurrentYPosition.ToString());
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        // Start/stop poller and lookup methods
        private void StartStagePoller(int pollIntervalMs = 1, int positionTimeoutMs = 10)
        {
            if (_stagePollCts != null) return;
            if (this.Stage == null) return;
            if (!this.Stage.ConnectionStatus) return;

            _stagePollCts = new CancellationTokenSource();
            var token = _stagePollCts.Token;
            _stagePollTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && this.Stage != null && this.Stage.ConnectionStatus)
                {
                    try
                    {
                        var t = await this.Stage.GetPositionAsync(positionTimeoutMs).ConfigureAwait(false);
                        if (!double.IsNaN(t.positionMm))
                        {
                            long idx = Interlocked.Increment(ref _stagePosWriteIndex);
                            int slot = (int)(idx & (StagePollBufferSize - 1));
                            _stagePosBuffer[slot] = t.positionMm;
                            _stagePosTsBuffer[slot] = t.timestampSec;
                            _latestStagePosMm = t.positionMm;
                            _latestStagePosTs = t.timestampSec;
                            try { this.YStagePosition = t.positionMm; } catch { }
                        }
                    }
                    catch { }
                    try { await Task.Delay(pollIntervalMs, token).ConfigureAwait(false); } catch { break; }
                }
            }, token);
        }

        private void StopStagePoller()
        {
            try
            {
                if (_stagePollCts != null)
                {
                    try { _stagePollCts.Cancel(); } catch { }
                    try { _stagePollTask?.Wait(200); } catch { }
                    _stagePollTask = null;
                    _stagePollCts.Dispose();
                    _stagePollCts = null;
                }
            }
            catch { }
        }

        public (double positionMm, double timestampSec) GetLatestCachedStagePosition() => (_latestStagePosMm, _latestStagePosTs);

        public (double positionMm, double timestampSec) GetClosestCachedStagePosition(double sampleTimestamp)
        {
            long writeIdx = Interlocked.Read(ref _stagePosWriteIndex);
            if (writeIdx == 0) return (_latestStagePosMm, _latestStagePosTs);
            int entries = (int)Math.Min(writeIdx, StagePollBufferSize);
            double bestPos = double.NaN; double bestTs = 0.0; double bestDiff = double.MaxValue;
            for (int i = 0; i < entries; i++)
            {
                int slot = (int)((writeIdx - i) & (StagePollBufferSize - 1));
                double ts = _stagePosTsBuffer[slot];
                if (ts <= 0) continue;
                double diff = Math.Abs(ts - sampleTimestamp);
                if (diff < bestDiff) { bestDiff = diff; bestPos = _stagePosBuffer[slot]; bestTs = ts; if (bestDiff == 0.0) break; }
            }
            if (double.IsNaN(bestPos)) return (_latestStagePosMm, _latestStagePosTs);
            return (bestPos, bestTs);
        }

        // Original synchronous fallback (kept for compatibility but not used by processors)
        private (double positionMm, double timestampSec) GetStagePositionForSample(int timeoutMs = 30)
        {
            try
            {
                if (this.Stage != null && this.Stage.ConnectionStatus)
                {
                    try
                    {
                        var t = this.Stage.GetPositionAsync(timeoutMs).GetAwaiter().GetResult();
                        return (t.positionMm, t.timestampSec);
                    }
                    catch { }

                    try
                    {
                        return (this.Stage.CurrentYPosition, DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds);
                    }
                    catch { }
                }
            }
            catch { }
            return (double.NaN, 0.0);
        }

        public class ProcessedDataChangedEventArgs : EventArgs
        {
            public double TimeStamp { get; }
            public double Voltage { get; }
            public double? Step { get; }

            public double Pounds { get; }

            public double Newtons { get; set; }

            // new fields for position
            public double PositionMm { get; }
            public double PositionTimestamp { get; }

            public ProcessedDataChangedEventArgs(double voltage, double TimeStamp_seconds, double voltageConversion, double newtonConversion, double? step = null, double positionMm = double.NaN, double positionTimestamp = 0.0)
            {
                //Get the current time in total seconds
                TimeStamp = TimeStamp_seconds;
                Voltage = voltage;
                Pounds = voltage * voltageConversion;
                Newtons = Pounds * newtonConversion;
                Step = step;
                PositionMm = positionMm;
                PositionTimestamp = positionTimestamp;
            }
        }

        public class RawDataChangedEventArgs : EventArgs
        {
            public double TimeStamp { get; }
            public int RawData { get; }

            public double? Step { get; }

            public RawDataChangedEventArgs(int rawData, double timeStamp, double? step = null)
            {
                TimeStamp = timeStamp;
                RawData = rawData;
                Step = step;
            }
        }
    }
}
