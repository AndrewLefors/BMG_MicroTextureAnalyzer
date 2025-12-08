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

namespace BMG_MicroTextureAnalyzer
{
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

        IntPtr memHandle = // allocate memory for data buffer
            MccDaq.MccService.WinBufAlloc32Ex(10000); //set for 10000 data points, so 10000/1000 = 10 seconds of data @ 1000Hz

        public event EventHandler<ProcessedDataChangedEventArgs> DataChanged;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };


        public void StartMonitor()
        {
            if (_isRunning)
            {
                return;
            }

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

            // Free the buffer and allocate a new one to Memhandle
            try
            {
                _numPoints = (int)(DataCollectionTime * Rate);
                MccDaq.MccService.WinBufFreeEx(MemHandle);
                MemHandle = MccDaq.MccService.WinBufAlloc32Ex(NumPoints);
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
                System.Diagnostics.Debug.WriteLine("Error: " + this.ErrorString);
            }
            ThresholdMet = false;
            _isRunning = true;
            _isMonitoring = true;
            _isStageMoving = true;
            _dataQueue.Clear();
            _processedDataList.Clear();

            _dataCollectorWorker = new BackgroundWorker();
            _dataCollectorWorker.DoWork += DataCollectorWorker_ContinuousScan;
            _dataCollectorWorker.WorkerSupportsCancellation = true;
            TranslateYStage(-100); // Move the stage 100mm down to get the stage on the sample
            _dataCollectorWorker.RunWorkerAsync();
        
            _dataCollectorWorker2 = new BackgroundWorker();
            // reuse the continuous-scan reader implementation for fracture test
            _dataCollectorWorker2.DoWork += DataReaderWorker_ContinuousScan;
            _dataCollectorWorker2.WorkerSupportsCancellation = true;
            _dataCollectorWorker2.RunWorkerAsync();

            _dataProcessorWorker = new BackgroundWorker();
            _dataProcessorWorker.DoWork += DataProcessorWorker_FindPlane;
            _dataProcessorWorker.WorkerSupportsCancellation = true;
            _dataProcessorWorker.RunWorkerAsync();

            ////Adding stage worker for y stage location
            //_stageWorker = new BackgroundWorker();
            //_stageWorker.DoWork += StageWorker_GetStagePosition;
            //_stageWorker.WorkerSupportsCancellation = true;
            //_stageWorker.RunWorkerAsync();

            _dataProcessorWorker.RunWorkerCompleted += (sender, e) =>
            {
                
                

                //this.StopAsync();
            };
            _dataCollectorWorker.RunWorkerCompleted += (sender, e) =>
            {
                //_dataCollectorWorker.CancelAsync();
                //_dataCollectorWorker.Dispose();
                //this.IsMonitoring = false;
                //this._isStageMoving = false;
            };




        }

        public void FractureTest()
        {
            if (_isRunning)
            {
                return;
            }
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
                MccDaq.MccService.WinBufFreeEx(MemHandle);
                MemHandle = MccDaq.MccService.WinBufAlloc32Ex(NumPoints);
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
              MccDaq.MccService.WinBufFreeEx(MemHandle);
              MemHandle = MccDaq.MccService.WinBufAlloc32Ex(NumPoints);
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
                    _rate = value;
                    _numPoints = (int)(DataCollectionTime * Rate);
                    OnPropertyChanged(nameof(Rate));
                }
            }
        }

        /// <summary>
        /// Safely change the sampling rate at runtime. If a continuous scan is active this will stop it,
        /// reallocate the DAQ buffer for the new rate, then restart the continuous-scan workers.
        /// </summary>
        public void SetSamplingRate(int newRate)
        {
            if (newRate <= 0) return;

            if (newRate > _maxSampleRate)
            {
                this.ErrorString = $"Requested rate {newRate} > max {_maxSampleRate}. Clamping to {_maxSampleRate}.";
                newRate = _maxSampleRate;
            }

            bool wasMonitoring = this.IsMonitoring;
            bool wasRunning = this.IsRunning;

            // Stop background acquisition if running
            if (wasMonitoring)
            {
                try
                {
                    // signal background scan to stop
                    this.StopBackgroundCollection();
                    // also request workers cancellation if present
                    if (_dataCollectorWorker != null && _dataCollectorWorker.IsBusy) _dataCollectorWorker.CancelAsync();
                    if (_dataCollectorWorker2 != null && _dataCollectorWorker2.IsBusy) _dataCollectorWorker2.CancelAsync();
                    if (_dataProcessorWorker != null && _dataProcessorWorker.IsBusy) _dataProcessorWorker.CancelAsync();
                }
                catch (Exception ex)
                {
                    this.ErrorString = "Error stopping existing scan: " + ex.Message;
                }

                // Give workers a short time to stop
                int wait = 0;
                while (((_dataCollectorWorker != null && _dataCollectorWorker.IsBusy) || (_dataCollectorWorker2 != null && _dataCollectorWorker2.IsBusy) || (_dataProcessorWorker != null && _dataProcessorWorker.IsBusy)) && wait < 2000)
                {
                    Thread.Sleep(50);
                    wait += 50;
                }
            }

            // Set new rate and recompute buffer size
            this.Rate = newRate;
            _numPoints = (int)(DataCollectionTime * Rate);

            // Reallocate DAQ buffer
            try
            {
                if (MemHandle != IntPtr.Zero)
                {
                    MccDaq.MccService.WinBufFreeEx(MemHandle);
                    MemHandle = IntPtr.Zero;
                }
                MemHandle = MccDaq.MccService.WinBufAlloc32Ex(NumPoints);
                if (MemHandle == IntPtr.Zero)
                {
                    this.ErrorString = "Failed to allocate DAQ buffer for NumPoints=" + NumPoints;
                    return;
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = "Buffer allocation error: " + ex.Message;
                return;
            }

            // If we were monitoring, restart continuous-scan workers to use the new rate/buffer
            if (wasMonitoring || wasRunning)
            {
                try
                {
                    // recreate workers similar to ContinuousScanTest
                    _dataQueue.Clear();
                    _processedDataList.Clear();

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

                    this.IsMonitoring = true;
                    this._isRunning = true;
                }
                catch (Exception ex)
                {
                    this.ErrorString = "Failed to restart acquisition after rate change: " + ex.Message;
                }
            }
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
            //cancel the continuous scan
            //check if invoke required
            ThresholdMet = false;

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
        }

        private void DataCollectorWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (this._board == null)
            {
                this._board = new MccBoard(1);
            }
            int channel = 7;
            MccDaq.Range range = MccDaq.Range.Bip10Volts;
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
                Thread.Sleep(2); // Adjust sampling rate as necessary
            }
        }
        private void DataCollectorWorker_ContinuousScan(object sender, DoWorkEventArgs e)
        {
            if (this._board == null)
            {
                this._board = new MccBoard(1);
            }
            int channel = 7;
            
            short status;
            MccDaq.Range iaa300 = MccDaq.Range.Bip10Volts;
            int rate = this.Rate;

            MccDaq.ErrorInfo ulStat = this._board.AInScan(channel, channel, NumPoints, ref rate, iaa300, MemHandle, ScanOptions.Background);
            if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
            {
                // record error and stop gracefully instead of throwing
                this.ErrorString = "AInScan failed: " + ulStat.Message;
                return;
            }
            // UL may adjust the requested rate and return the actual rate via the ref parameter
            this.ActualRate = rate;

            while (!_dataCollectorWorker.CancellationPending && !ThresholdMet)
            {
                // If an external immediate stop was requested, break
                if (!this._isRunning || !this.IsMonitoring) break;
                Thread.Sleep(1);
            }
            //cancel the background worker
            try { _dataCollectorWorker.CancelAsync(); } catch { }
            try { _board?.StopBackground(FunctionType.AiFunction); } catch { }
        }

        private void DataReaderWorker_ContinuousScan(object sender, DoWorkEventArgs e)
        {
            int lastIndex = 0;
            int[] dataBuffer = new int[Math.Max(1, this.NumPoints)];
            MccDaq.Range iaa300 = MccDaq.Range.Bip10Volts;

            while (!_dataCollectorWorker2.CancellationPending && !ThresholdMet)
            {
                if (!this._isRunning || !this.IsMonitoring) break;

                try
                {
                    this._board.GetStatus(out short status, out int curCount, out int currentIndex, FunctionType.AiFunction);

                    if (MemHandle == IntPtr.Zero)
                    {
                        // buffer not allocated; wait a bit and continue
                        Thread.Sleep(1);
                        continue;
                    }

                    if (currentIndex != lastIndex)
                    {
                        if (currentIndex > lastIndex)
                        {
                            int pointsToRead = currentIndex - lastIndex;
                            MccDaq.ErrorInfo ulStat = MccDaq.MccService.WinBufToArray32(MemHandle, dataBuffer, lastIndex, pointsToRead);
                            if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                            {
                                this.ErrorString = "WinBufToArray32 failed: " + ulStat.Message;
                                // stop background acquisition and exit loop
                                try { _board.StopBackground(FunctionType.AiFunction); } catch { }
                                break;
                            }

                            for (int i = 0; i < pointsToRead; i++)
                            {
                                RawDataChangedEventArgs dataChangedEventArgs = new RawDataChangedEventArgs(dataBuffer[i]);
                                _dataQueue.Enqueue(dataChangedEventArgs);
                            }
                        }
                        else // wrap-around case: read [lastIndex..NumPoints) then [0..currentIndex)
                        {
                            int firstChunk = NumPoints - lastIndex;
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
                                    RawDataChangedEventArgs dataChangedEventArgs = new RawDataChangedEventArgs(dataBuffer[i]);
                                    _dataQueue.Enqueue(dataChangedEventArgs);
                                }
                            }
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
                                    RawDataChangedEventArgs dataChangedEventArgs = new RawDataChangedEventArgs(dataBuffer[i]);
                                    _dataQueue.Enqueue(dataChangedEventArgs);
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


            //TranslateYStage(-100); // Move the stage 100mm down to get the stage on the sample


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
                Thread.Sleep(1); // Adjust sampling rate as necessary

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

            short status;
            MccDaq.Range range = MccDaq.Range.Bip10Volts;
            int rate = this.Rate;
            //TranslateYStage(this.FractureDistance); // Move the stage 100mm down to get the stage on the sample
            MccDaq.ErrorInfo ulStat = this._board.AInScan(channel, channel, NumPoints, ref rate, range, MemHandle, ScanOptions.Background);
            if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
            {
                throw new Exception("Error reading analog input: " + ulStat.Message);
            }
            // capture actual rate returned by UL
            this.ActualRate = rate;
             while (!_dataCollectorWorker.CancellationPending && !ThresholdMet)
             {

                //Thread.Sleep(1);
             }
             //cancel the background worker
             _dataCollectorWorker.CancelAsync();
        }

        private void DataProcessorWorker_FractureTest(object sender, DoWorkEventArgs e)
        {
            //this.Stage.MoveYAbsolute(100);
            MccDaq.Range range = MccDaq.Range.Bip10Volts;
            while (!((BackgroundWorker)sender).CancellationPending && !ThresholdMet)
            {
                if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                {
                    MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(range, args.RawData, out double voltage);
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        throw new Exception("Error converting raw data to : " + ulStat.Message);
                    }

                    ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion);

                    // record force (Newtons) before offset so we can compute a robust zero in force units
                    AddRecentForceSample(processedData.Newtons);

                    // apply engine-side force offset so downstream consumers receive zeroed forces
                    processedData.Newtons = processedData.Newtons - this.ForceOffset;

                    //Run an async task to check the data if it meets or exceeds the threshold
                    if (processedData.Newtons >= this.FindPlaneThreshold)
                    {
                        this.ThresholdMet = true;
                        this.StopBackgroundCollection();
                    }

                    lock (_dataLock)
                    {
                        _processedDataList.Add(processedData);
                    }
                    OnDataChanged(processedData);
                }
                //Thread.Sleep(2);// Adjust processing rate as necessary
            }
            e.Cancel = true;

        }

        private void DataProcessorWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!((BackgroundWorker)sender).CancellationPending)
            {
                if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                {
                    MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(MccDaq.Range.Bip10Volts, args.RawData, out double voltage);
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        throw new Exception("Error converting raw data to voltage: " + ulStat.Message);
                    }
                    ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion);
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

            e.Cancel = true;
        }

        private void DataProcessorWorker_ContinuousScanInput(object sender, DoWorkEventArgs e)
        {
            //this.Stage.MoveYAbsolute(100);
            MccDaq.Range iaa300 = MccDaq.Range.Bip10Volts;
            MccDaq.Range range = MccDaq.Range.Bip10Volts;
            while (!((BackgroundWorker)sender).CancellationPending && !ThresholdMet)
            {
               // this.GetYLocation();
                if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                {
                    MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(iaa300, args.RawData, out double voltage); //Changed from Bip10Volts to Bip10Volts on Feb 17 2025
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        throw new Exception("Error converting raw data to : " + ulStat.Message);
                    }
                    ProcessedDataChangedEventArgs processedData = null;
                     //if (this.Stage.ConnectionStatus && this.IsStageRunning)
                     //{
                         //this.GetYLocation();
                     processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion, this.YStagePosition);
                     //}
                     //else
                     //{
                     //    processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion);
                     //}
                     AddRecentForceSample(processedData.Newtons);
                     processedData.Newtons = processedData.Newtons - this.ForceOffset;
                     //Run an async task to check the data if it meets or exceeds the threshold
                     Task.Run(() =>
                     {
                         if (processedData.Newtons >= this.FindPlaneThreshold + this.VoltageOffset)
                         {
                             this.ThresholdMet = true;
                             //this.Stage.Stop();
                             this.StopMotionController();
                             this.IsStageRunning = false;
                             this._board.StopBackground(FunctionType.AiFunction);
                             this.IsMonitoring = false;
                             //this._stageWorker.CancelAsync();
                         }
                     });
                     lock (_dataLock)
                     {
                         _processedDataList.Add(processedData);
                     }
                     OnDataChanged(processedData);
                 }
                 //Thread.Sleep(2);// Adjust processing rate as necessary
             }
             e.Cancel = true;
         }

        private void DataProcessorWorker_FindPlane(object sender, DoWorkEventArgs e)
        {
            MccDaq.Range range = MccDaq.Range.Bip10Volts;

            while (!((BackgroundWorker)sender).CancellationPending && !ThresholdMet)
            {
                if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                {
                    MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(range, args.RawData, out double voltage);
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        throw new Exception("Error converting raw data to voltage: " + ulStat.Message);
                    }
                    ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion, this.YStagePosition);
                    AddRecentForceSample(processedData.Newtons);
                    processedData.Newtons = processedData.Newtons - this.ForceOffset;
                    if (processedData.Newtons > this.FindPlaneThreshold)
                     {
                         ThresholdMet = true;
                         //this.Stage.Stop();
                         //this._board.StopBackground(FunctionType.AiFunction);
                         this.StopBackgroundCollection();
                         this.IsMonitoring = false;
                         this.IsStageRunning = false;

                         //this._stageWorker.CancelAsync();
                     }
                     //Task.Run(() =>this.GetYLocation());
                     lock (_dataLock)
                     {
                         _processedDataList.Add(processedData);
                     }
                     OnDataChanged(processedData);
                 }
                 //Thread.Sleep(1);// Adjust processing rate as necessary
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
                if (_dataQueue.TryDequeue(out RawDataChangedEventArgs args))
                {
                    MccDaq.ErrorInfo ulStat = _board.ToEngUnits32(MccDaq.Range.Bip10Volts, args.RawData, out double voltage);
                    if (ulStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
                    {
                        throw new Exception("Error converting raw data to voltage: " + ulStat.Message);
                    }
                    ProcessedDataChangedEventArgs processedData = new ProcessedDataChangedEventArgs(voltage, args.TimeStamp, this.VoltageConversion, this.NewtonConversion);
                    AddRecentForceSample(processedData.Newtons);
                    processedData.Newtons = processedData.Newtons - this.ForceOffset;
                    if (processedData.Newtons >= this.PunctureThreshold)
                     {
                         ThresholdMet = true;
                         this.Stage.Stop();
                         //Task.Run(() => this.StopAsync());
                     }   
                     lock (_dataLock)
                     {
                         _processedDataList.Add(processedData);
                     }
                     OnDataChanged(processedData);
                 }
                 Thread.Sleep(1);// Adjust processing rate as necessary
            }
             this.SetStageSpeed(100);
             Thread.Sleep(1000);
             TranslateYStage(3);
             e.Cancel = true;

             //((BackgroundWorker)sender).CancelAsync();

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

        //create class for event args that has timestamp and voltage
        public class ProcessedDataChangedEventArgs : EventArgs
        {
            public double TimeStamp { get; }
            public double Voltage { get; }
            public double? Step { get; }

            public double Pounds { get; }

            public double Newtons { get; set; }

            public ProcessedDataChangedEventArgs(double voltage, double TimeStamp_seconds, double voltageConversion, double newtonConversion, double? step = null)
            {
                //Get the current time in total seconds
                TimeStamp = TimeStamp_seconds;
                Voltage = voltage;
                Pounds = voltage * voltageConversion;
                Newtons = Pounds * newtonConversion;
                Step = step;
            }
        }

        public class RawDataChangedEventArgs : EventArgs
        {
            public double TimeStamp { get; }
            public int RawData { get; }

            public double? Step { get; }

            public RawDataChangedEventArgs(int rawData, double? step = null)
            {
                TimeStamp = DateTime.Now.TimeOfDay.TotalSeconds;
                RawData = rawData;
                Step = step;
            }


        }
    }
}
