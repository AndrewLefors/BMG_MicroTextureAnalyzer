using BMG_MicroTextureAnalyzer;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Timers;
using System.ComponentModel;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace BMG_MicroTextureAnalyzer_GUI
{
    public partial class Form1 : Form
    {
        public Engine MTAengine;
        public MccDaq.MccBoard board;
        public Int32 monitorData;
        public Double monitorVoltage;
        public Double monitorPounds;
        public Double monitorNewtons;
        public Stopwatch stopwatch;
        public Double newtonThreshold = 1.5;
        public double relativeStartTime = double.NaN;

        private double _fractureTestPoundConversion = 2141.878;
        private double _fractureTestNewtonConversion = 4.44822;
        private double _punctureTestKilogramConversion = 39.6844;
        private double _punctureTestNewtonConversion = 9.81;

        private double _voltageConversion = 2141.878;
        private double _newtonConversion = 4.44822;

        private BackgroundWorker graphUpdaterBackgroundWorker = new BackgroundWorker();
        private double batchIntervalMs = 50;
        private ConcurrentQueue<Engine.ProcessedDataChangedEventArgs> dataQueue = new ConcurrentQueue<Engine.ProcessedDataChangedEventArgs>();

        private Thread chartUpdateThread;
        private bool chartUpdateThreadRunning = false;
        private Thread positionUpdateThread;
        private bool positionUpdateThreadRunning = false;

        private List<int> stageSpeedConversionSpeedList = new List<int> { 0, 4, 7, 9, 12, 15, 20, 25 }; //default of 19.1um/s
        private List<int> averageWindowList = new List<int> { 0, 10, 25, 50, 100, 150, 200, 250, 500, 1000 };

        private double voltageOffset = 0;

        private bool chartSaveToFile = false;
        private string fileSavePath = null;
        private int displayWindowSeconds = 30; // seconds of data to keep in circular buffer
        private int displayWindowSecondsContinuous = 10; // 10-second sliding window for live display
        private int maxDisplayPoints = 1200; // maximum points to display (approx pixels)
        private CircularBuffer<(double X, double Y)> displayBuffer;
        private BlockingCollection<string> fileWriteQueue;
        private Task fileWriterTask;
        private CancellationTokenSource chartCancellationTokenSource;
        private Logger logger;

        // fracture test file writer fields
        private bool fractureSaving = false;
        private string fractureFileSavePath = null;
        private BlockingCollection<string> fractureFileWriteQueue;
        private Task fractureFileWriterTask;

        // circular buffer implementation
        private class CircularBuffer<T>
        {
            private readonly T[] _buf;
            private int _start;
            private int _count;
            public CircularBuffer(int capacity) { _buf = new T[capacity]; _start = 0; _count = 0; }
            public int Count => _count;
            public void Add(T item)
            {
                if (_count < _buf.Length)
                {
                    _buf[(_start + _count) % _buf.Length] = item;
                    _count++;
                }
                else
                {
                    _buf[_start] = item;
                    _start = (_start + 1) % _buf.Length;
                }
            }
            public T[] ToArray()
            {
                T[] outArr = new T[_count];
                for (int i = 0; i < _count; i++) outArr[i] = _buf[(_start + i) % _buf.Length];
                return outArr;
            }
            public void Clear() { _start = 0; _count = 0; }
        }

        public Form1()
        {
            BMG_MicroTextureAnalyzer.Engine engine = new BMG_MicroTextureAnalyzer.Engine();
            MTAengine = engine;
            // Subscribe GUI to engine events
            MTAengine.DataChanged += MTAengine_DataChanged;
            board = new MccDaq.MccBoard(1);

            engine.xpsPositionChanged += pos => lblPosition.Invoke(() => lblPosition.Text = $"{pos:F4} mm");
            engine.StateChanged += state => lblStatus.Invoke(() => lblStatus.Text = state.ToString());
            engine.ErrorOccurred += msg => logger?.Log(msg, LogLevel.Error, "Engine");
            MTAengine = engine;
            MTAengine.PropertyChanged += MTAengine_PropertyChanged;
            // logger initialization deferred until form is shown to ensure control handles are created

            var subdivisionList = new List<int> { 1, 2, 4, 8 };
            var stageSpeedList = new List<double> { 19.1, 95.5, 152.8, 190.1, 248.3, 305.6, 401.0, 496.5 }; //unts in um/s
            var averageWindowList = new List<int> { 0, 10, 25, 50, 100, 150, 200, 250, 500, 1000 };




            InitializeComponent();

            // Initialize logger when form is first shown (ensures LogTextBox handle exists)
            this.Shown += Form1_Shown;

            // Attempt to load a logo image placed next to the executable named 'mme_logo.png' (user-provided).
            try
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var logoPath = Path.Combine(exeDir, "mme_logo.png");
                if (File.Exists(logoPath))
                {
                    var img = Image.FromFile(logoPath);
                    MMELogoPictureBox.Image = img;
                }
            }
            catch { /* don't fail startup if image can't be loaded */ }
            this.MotionControllerSubdivisionComboBox.DataSource = subdivisionList;
            this.MotionControllerSubdivisionComboBox.SelectedIndex = 0;
            this.DAQ_StageSpeedComboBox.DataSource = stageSpeedList;
            //this.AverageWindowComboBox.DataSource = averageWindowList;
            //this.AverageWindowComboBox.SelectedIndexChanged += AverageWindowComboBox_SelectedIndexChanged;
            //DAQDataGridView.Columns.Add("Time", "Time");
            //DAQDataGridView.Columns.Add("Voltage", "Voltage");
            //DAQDataGridView.Columns.Add("Pounds", "Pounds");
            //DAQDataGridView.Columns.Add("Newtons", "Newtons");
            //DAQDataGridView.Columns.Add("Step", "Step");
            //MonitorResponseChart = new Chart();

            //StartChartUpdateThread();

        }

        private void Form1_Shown(object? sender, EventArgs e)
        {
            try
            {
                // Make log textbox read-only so user cannot edit entries
                try { LogTextBox.ReadOnly = true; } catch { }

                logger = new Logger(LogTextBox, batchMs: 200, maxLines: 4000);
                logger.MinimumLevel = LogLevel.Info;
                logger.Start();

                // Wire up Engine logging callbacks
                MTAengine.SetLoggers(
                    (msg, src) => logger?.Log(msg, LogLevel.Info, src),
                    (msg, src) => logger?.Log(msg, LogLevel.Warning, src),
                    (msg, src) => logger?.Log(msg, LogLevel.Error, src)
                );

                // Wire up MotionController logging callbacks
                if (MTAengine.Stage != null)
                {
                    MTAengine.Stage.SetLoggers(
                        (msg, src) => logger?.Log(msg, LogLevel.Info, src),
                        (msg, src) => logger?.Log(msg, LogLevel.Warning, src),
                        (msg, src) => logger?.Log(msg, LogLevel.Error, src)
                    );
                }

                logger?.Log("Application started", LogLevel.Info, "System");
            }
            catch
            {
                // swallow - logging is best-effort
            }

            // unsubscribe, only initialize once
            this.Shown -= Form1_Shown;
        }

        //Convert this to event driven so that the data is updated when the event is thrown
        private async void StartChartUpdateThread()
        {
            // Stop any previous chart thread FIRST before checking if we should start
            try
            {
                chartCancellationTokenSource?.Cancel();
            }
            catch { }

            // Wait for previous thread to exit with increased timeout
            if (chartUpdateThread != null && chartUpdateThread.IsAlive)
            {
                try
                {
                    if (!chartUpdateThread.Join(500))
                    {
                        try { logger?.Log("Previous chart thread did not exit in time", LogLevel.Warning, "Chart"); } catch { }
                    }
                }
                catch { }
            }

            // Now check if we should start - after old thread is stopped
            if (chartUpdateThread != null && chartUpdateThread.IsAlive)
            {
                try { logger?.Log("Chart update thread already running, skipping start", LogLevel.Warning, "Chart"); } catch { }
                return;
            }

            // prepare circular buffer: use ActualRate for 10 seconds of data
            // Wait briefly for ActualRate to be set if engine is starting
            int waitedMs = 0;
            while (MTAengine.ActualRate <= 0 && waitedMs < 500)
            {
                await Task.Delay(50);
                waitedMs += 50;
            }

            int rate = Math.Max(1, MTAengine.ActualRate > 0 ? MTAengine.ActualRate : MTAengine.Rate);
            int windowSec = 10; // Always 10 seconds of data
            int capacity = Math.Max(1000, rate * windowSec);

            // Create new buffer with lock to prevent access during creation
            lock (_displayLock)
            {
                displayBuffer = new CircularBuffer<(double X, double Y)>(capacity);
            }

            // reset relative start time so chart X axis will show time since this run started
            relativeStartTime = double.NaN;

            // prepare file writer if saving mode and path provided (for non-fracture tests)
            if (chartSaveToFile && !string.IsNullOrEmpty(fileSavePath))
            {
                try
                {
                    fileWriteQueue = new BlockingCollection<string>(new ConcurrentQueue<string>());
                    var path = fileSavePath;
                    fileWriterTask = Task.Run(() =>
                    {
                        try
                        {
                            using (var sw = new StreamWriter(path, false))
                            {
                                sw.WriteLine("Time,Newtons");
                                foreach (var line in fileWriteQueue.GetConsumingEnumerable())
                                {
                                    sw.WriteLine(line);
                                    if (fileWriteQueue.Count == 0) sw.Flush();
                                }
                            }
                        }
                        catch { }
                    });
                }
                catch { }
            }

            // prepare chart series on UI thread
            try
            {
                if (MonitorResponseChart != null && !MonitorResponseChart.IsDisposed)
                {
                    MonitorResponseChart.Invoke(new Action(() =>
                    {
                        try
                        {
                            MonitorResponseChart.SuspendLayout();
                            MonitorResponseChart.Series.Clear();
                            var s = new Series { ChartType = SeriesChartType.FastLine, XValueType = ChartValueType.Double, YValueType = ChartValueType.Double };
                            MonitorResponseChart.Series.Add(s);
                            MonitorResponseChart.ResumeLayout();
                            try { typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(MonitorResponseChart, true, null); } catch { }
                        }
                        catch { }
                    }));
                }
            }
            catch { }

            // Create new cancellation token
            chartCancellationTokenSource = new CancellationTokenSource();
            chartUpdateThread = new Thread(ProcessDataQueue) { IsBackground = true };
            chartUpdateThreadRunning = true;
            chartUpdateThread.Start();

            try { logger?.Log("Chart update thread started", LogLevel.Info, "Chart"); } catch { }
        }

        //This function is under-cooked and should not be used until heavy revisions
        private async void StartPositionUpdateThread()
        {
            if (!MTAengine.IsStageRunning)
            {
                return;
            }
            await Task.Run(() => YPosLabel.Invoke(() =>
            {
                YPosLabel.Text = MTAengine.YStagePosition.ToString();
                positionUpdateThread = new Thread(() =>
                {
                    while (MTAengine.IsStageRunning)
                    {
                        YPosLabel.Invoke(new Action(() =>
                        {
                            MTAengine.GetYLocation();
                            YPosLabel.Text = MTAengine.YStagePosition.ToString();
                        }));
                    }
                });
            }));

        }
        private void ProcessDataQueue()
        {
            var token = chartCancellationTokenSource?.Token ?? CancellationToken.None;
            var sw = Stopwatch.StartNew();

            while (!token.IsCancellationRequested)
            {
                if (dataQueue.IsEmpty)
                {
                    Thread.Sleep(10);
                    continue;
                }

                var batch = new List<Engine.ProcessedDataChangedEventArgs>();
                var batchTimer = Stopwatch.StartNew();

                while (batchTimer.ElapsedMilliseconds < batchIntervalMs && !token.IsCancellationRequested)
                {
                    if (dataQueue.TryDequeue(out var item))
                    {
                        batch.Add(item);
                    }
                    else
                    {
                        Thread.Sleep(0);
                    }
                }

                if (batch.Count == 0) continue;

                if (chartSaveToFile && fileWriteQueue != null)
                {
                    // Write absolute timestamps to file
                    foreach (var d in batch)
                    {
                        try { fileWriteQueue.Add($"{d.TimeStamp:F6},{d.Newtons:F6}"); } catch { }
                    }
                }
                else
                {
                    // Update force reading label with most recent sample in batch
                    try { UpdateForceReadingLabel(batch.Last().Newtons); } catch { }

                    // Add samples to displayBuffer and compute relative time for chart display
                    lock (_displayLock)
                    {
                        foreach (var d in batch)
                        {
                            if (double.IsNaN(relativeStartTime)) relativeStartTime = d.TimeStamp;
                            double t = d.TimeStamp - relativeStartTime;
                            displayBuffer.Add((t, d.Newtons));
                        }
                    }

                    // Prepare downsampled arrays from circular buffer
                    double[][] snapshot;
                    lock (_displayLock)
                    {
                        var arr = displayBuffer.ToArray();
                        snapshot = new double[2][] { arr.Select(p => p.X).ToArray(), arr.Select(p => p.Y).ToArray() };
                    }
                    var raw = snapshot[0].Select((x, i) => (X: x, Y: snapshot[1][i])).ToArray();
                    int total = raw.Length;
                    if (total == 0) continue;

                    // Compute sliding window bounds based on latest time and 10-second window
                    double rightAll = raw[total - 1].X;
                    double leftWindow = Math.Max(0, rightAll - 10.0);

                    // Only keep points inside the visible 10-second window
                    var windowed = raw.Where(p => p.X >= leftWindow).ToArray();
                    if (windowed.Length == 0) continue;

                    raw = windowed;
                    total = raw.Length;

                    // Target points: use chart width or a reasonable default, ensuring we can handle high sample counts
                    int chartWidth = 1200;
                    try { if (MonitorResponseChart != null && MonitorResponseChart.Width > 0) chartWidth = MonitorResponseChart.Width; } catch { }
                    int target = Math.Min(Math.Max(chartWidth, 1000), 2000);

                    double[] xs, ys;
                    if (total <= target)
                    {
                        xs = new double[total];
                        ys = new double[total];
                        for (int i = 0; i < total; i++) { xs[i] = raw[i].X; ys[i] = raw[i].Y; }
                    }
                    else
                    {
                        // Downsample using min/max binning to preserve peaks/valleys
                        int bins = Math.Max(1, target / 2);
                        int pointsPerBin = (int)Math.Ceiling((double)total / bins);
                        var xsList = new List<double>(bins * 2);
                        var ysList = new List<double>(bins * 2);

                        for (int b = 0; b < bins; b++)
                        {
                            int start = b * pointsPerBin;
                            int end = Math.Min(total, start + pointsPerBin);
                            if (start >= end) break;
                            double minY = double.MaxValue, maxY = double.MinValue;
                            double minX = 0, maxX = 0;
                            for (int i = start; i < end; i++)
                            {
                                var p = raw[i];
                                if (p.Y < minY) { minY = p.Y; minX = p.X; }
                                if (p.Y > maxY) { maxY = p.Y; maxX = p.X; }
                            }
                            xsList.Add(minX); ysList.Add(minY);
                            if (maxY != minY) { xsList.Add(maxX); ysList.Add(maxY); }
                        }
                        xs = xsList.ToArray();
                        ys = ysList.ToArray();
                    }

                    try
                    {
                        this.BeginInvoke(new Action<double[], double[], double>((a, b, right) => UpdateChartWithArrays(a, b, right)), xs, ys, rightAll);
                        try { this.BeginInvoke(new Action(() => UpdateTimeReadingLabel(rightAll))); } catch { }
                    }
                    catch { }
                }
            }

            // finalize file writer
            try
            {
                if (fileWriteQueue != null) { fileWriteQueue.CompleteAdding(); fileWriterTask?.Wait(500); fileWriteQueue = null; }
            }
            catch { }
        }

        private void UpdateChartWithArrays(double[] xs, double[] ys)
        {
            try
            {
                if (MonitorResponseChart == null) return;
                if (MonitorResponseChart.Series.Count == 0)
                {
                    MonitorResponseChart.Series.Add(new Series { ChartType = SeriesChartType.FastLine, XValueType = ChartValueType.Double, YValueType = ChartValueType.Double });
                }
                var s = MonitorResponseChart.Series[0];
                s.Points.DataBindXY(xs, ys);

                // This overload has been replaced; call the other UpdateChartWithArrays that receives 'right' bound.
            }
            catch { }
        }

        private void UpdateChartWithArrays(double[] xs, double[] ys, double right)
        {
            try
            {
                // Safety check: ensure cancellation hasn't been requested
                if (chartCancellationTokenSource?.IsCancellationRequested == true)
                {
                    return;
                }

                if (MonitorResponseChart == null || MonitorResponseChart.IsDisposed) return;
                if (MonitorResponseChart.Series.Count == 0)
                {
                    MonitorResponseChart.Series.Add(new Series { ChartType = SeriesChartType.FastLine, XValueType = ChartValueType.Double, YValueType = ChartValueType.Double });
                }

                var s = MonitorResponseChart.Series[0];

                // Verify series is still valid
                if (s == null || s.Points == null) return;

                s.Points.DataBindXY(xs, ys);

                if (xs.Length > 0 && MonitorResponseChart.ChartAreas.Count > 0)
                {
                    var area = MonitorResponseChart.ChartAreas[0];
                    double rightBound = right;
                    // Slide behavior: start sliding once we've accumulated at least 'slideStartSeconds'.
                    double slideStartSeconds = Math.Min(5.0, displayWindowSecondsContinuous);
                    if (rightBound < slideStartSeconds)
                    {
                        // Before sliding starts, keep a constant window size so plotting area doesn't resize.
                        area.AxisX.Minimum = 0.0;
                        area.AxisX.Maximum = displayWindowSecondsContinuous;
                    }
                    else
                    {
                        // Use the continuous window size for sliding
                        double windowSec = Math.Max(1, displayWindowSecondsContinuous);
                        double leftBound = Math.Max(0.0, rightBound - windowSec);  // ? Clamp to 0 - never negative

                        // Directly set axis min/max so it always follows the newest entry.
                        area.AxisX.Minimum = leftBound;
                        area.AxisX.Maximum = rightBound;
                    }
                    area.AxisX.IsMarginVisible = false;

                    // Format X axis labels to 4 decimal places
                    try { area.AxisX.LabelStyle.Format = "F4"; } catch { }
                    try { area.AxisX.IntervalAutoMode = IntervalAutoMode.FixedCount; } catch { }
                    try { area.RecalculateAxesScale(); } catch { }
                }

            }
            catch { }
        }

        /// <summary>
        /// Update a UI label with the latest force reading (Newtons).
        /// This method locates a control named "forceReadingLabel" (if present) and updates it.
        /// If that control is not found, it falls back to updating the existing
        /// "voltageOffsetReadingLabel" so callers don't have to know which label exists.
        /// The update is performed via BeginInvoke to avoid blocking data threads.
        /// </summary>
        /// <param name="newtons">Force value in Newtons to display.</param>
        private void UpdateForceReadingLabel(double newtons)
        {
            // Display force in millinewtons with 5 decimal places
            string text = (newtons * 1000.0).ToString("F5");

            Action setLabel = () =>
            {
                // Try to find a dedicated forceReadingLabel control by name (designer may or may not have added it).
                var found = this.Controls.Find("forceReadingLabel", true);
                if (found.Length > 0 && found[0] is Label lbl)
                {
                    lbl.Text = text;
                    return;
                }

                // Fallback: update voltageOffsetReadingLabel if present
                try
                {
                    if (forceOffsetReadingLabel != null)
                    {
                        // Show offset label also in millinewtons with 5 decimal places
                        forceOffsetReadingLabel.Text = text;
                    }
                }
                catch
                {
                    // swallow any exceptions to avoid interrupting data processing
                }
            };

            if (this.IsHandleCreated && this.InvokeRequired)
                this.BeginInvoke(setLabel);
            else
                setLabel();
        }

        // Update the TimeReadingLabel with relative time in seconds (uses same time as chart X-axis)
        private void UpdateTimeReadingLabel(double seconds)
        {
            string txt = seconds.ToString("F4") + " s"; // 4 decimal places to match chart

            Action set = () =>
            {
                try
                {
                    var found = this.Controls.Find("TimeReadingLabel", true);
                    if (found.Length > 0 && found[0] is Label lbl)
                    {
                        lbl.Text = txt;
                        return;
                    }

                    // Fallback: if no specific TimeReadingLabel, try a label named "timeReadingLabel" (case)
                    var found2 = this.Controls.Find("timeReadingLabel", true);
                    if (found2.Length > 0 && found2[0] is Label lbl2)
                    {
                        lbl2.Text = txt;
                    }
                }
                catch { }
            };

            if (this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(set); else set();
        }

        private void MTAengine_DataChanged(object? sender, Engine.ProcessedDataChangedEventArgs e)
        {
            dataQueue.Enqueue(e);

            // Track last processed sample for logging
            try
            {
                _lastProcessedSample = e;
            }
            catch { }

            // If a fracture test file writer is active, write absolute timestamp (not relative)
            if (fractureSaving && fractureFileWriteQueue != null)
            {
                try
                {
                    double pos = double.IsNaN(e.PositionMm) ? double.NaN : e.PositionMm;
                    // Write absolute timestamp from engine (seconds since start of acquisition)
                    fractureFileWriteQueue.Add($"{e.TimeStamp:F6},{e.Newtons:F6},{pos:F6}");
                }
                catch { }
            }
        }
        private void PromptUserToSave()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FilterIndex = 2,
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                using (var writer = new StreamWriter(saveFileDialog.FileName))
                {
                    // Write headers
                    writer.WriteLine("Time,Newtons");

                    // Write data entries
                    foreach (var entry in MonitorResponseChart.Series[0].Points)
                    {
                        writer.WriteLine($"{entry.XValue},{entry.YValues[0]}");
                    }
                }
            }
        }
        private void MTAengine_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // AverageWindowComboBox handled by SelectedIndexChanged handler

            if (e.PropertyName == nameof(Engine.FractureTestComplete))
            {
                if (MTAengine.FractureTestComplete)
                {
                    //Take the data from the chart and add it to the datagrid

                    // finalize fracture file writer if active
                    if (fractureSaving && fractureFileWriteQueue != null)
                    {
                        try
                        {
                            var q = fractureFileWriteQueue;
                            fractureSaving = false;
                            fractureFileWriteQueue = null;
                            Task.Run(() =>
                            {
                                try
                                {
                                    q.CompleteAdding();
                                    fractureFileWriterTask?.Wait(2000);
                                }
                                catch { }
                            });
                        }
                        catch { }
                    }


                }
            }

            // Update ForceOffset label when engine ForceOffset property changes.
            // Accept both the direct Engine property name ("ForceOffset") and the prefixed form ("Engine.ForceOffset").
            if (e.PropertyName == nameof(Engine.ForceOffset) || e.PropertyName == "Engine.ForceOffset")
            {
                Action set = () =>
                {
                    try
                    {
                        string txt = (MTAengine.ForceOffset * 1000.0).ToString("F5"); // millinewtons, 5 decimals
                        var found = this.Controls.Find("forceOffsetReadingLabel", true);
                        if (found.Length > 0 && found[0] is Label lbl)
                        {
                            lbl.Text = txt;
                            return;
                        }

                        // Fallback to designer field if present
                        try
                        {
                            if (forceOffsetReadingLabel != null)
                            {
                                forceOffsetReadingLabel.Text = txt;
                            }
                        }
                        catch { }
                    }
                    catch { }
                };

                if (this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(set); else set();
            }

            //EHandle Connection event to populate combox with available devices after scan for devies button has been pressed
            if (e.PropertyName == "Connection.AvailableDevices")
            {
                // Always marshal to UI thread when updating controls
                Action setList = () =>
                {
                    try
                    {
                        AvailableDevicesComboBox.DataSource = MTAengine.Connection.AvailableDevices;
                        if (AvailableDevicesComboBox.Items.Count > 0) AvailableDevicesComboBox.SelectedIndex = 0;
                    }
                    catch { }
                };
                if (this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(setList); else setList();
            }
            //Check if the stage position has changed and update the label
            //  if (e.PropertyName = )
            if (e.PropertyName == nameof(MTAengine.Stage.WarningMessage))
            {
                MessageBox.Show(MTAengine.Stage.WarningMessage);
            }
            //Handle event when connection is successful to change connection status label text to connect in green text
            //Why is this not working? 
            if (e.PropertyName == "MotionController.ConnectionStatus")
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        ConnectionStatusResponseLabel.Text = MTAengine.Stage.ConnectionStatus ? "Connected" : "Not Connected";
                        ConnectionStatusResponseLabel.ForeColor = MTAengine.Stage.ConnectionStatus ? Color.Green : Color.Red;
                    }));
                }
                else
                {
                    ConnectionStatusResponseLabel.Text = MTAengine.Stage.ConnectionStatus ? "Connected" : "Not Connected";
                    ConnectionStatusResponseLabel.ForeColor = MTAengine.Stage.ConnectionStatus ? Color.Green : Color.Red;
                }
            }
            if (e.PropertyName == "MotionController.Busy")
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        MotionControllerStatusResponseLabel.Text = MTAengine.Stage.Busy ? "Busy" : "Not Busy";
                        MotionControllerStatusResponseLabel.ForeColor = MTAengine.Stage.Busy ? Color.Red : Color.Green;
                    }));
                }
                else
                {
                    MotionControllerStatusResponseLabel.Text = MTAengine.Stage.Busy ? "Busy" : "Not Busy";
                    MotionControllerStatusResponseLabel.ForeColor = MTAengine.Stage.Busy ? Color.Red : Color.Green;
                }
            }
            // Forward Engine.ErrorString to log (both top-level and prefixed from Engine.PropertyChanged wrapper)
            if (e.PropertyName == "ErrorString" || e.PropertyName == "Engine.ErrorString")
            {
                try { logger?.Log(MTAengine.ErrorString, LogLevel.Error, "Engine"); } catch { }
            }

            // Forward MotionController messages
            if (e.PropertyName == "MotionController.ErrorMessage")
            {
                try { logger?.Log(MTAengine.Stage.ErrorMessage, LogLevel.Error, "MotionController"); } catch { }
            }
            if (e.PropertyName == "MotionController.WarningMessage")
            {
                try { logger?.Log(MTAengine.Stage.WarningMessage, LogLevel.Warning, "MotionController"); } catch { }
            }

            // Log MotionController configuration changes
            if (e.PropertyName == "MotionController.PulseEquivalent")
            {
                try
                {
                    logger?.Log($"Pulse equivalent changed: {MTAengine.Stage.PulseEquivalent:F6}", LogLevel.Info, "Stage.Config");
                }
                catch { }
            }

            if (e.PropertyName == "MotionController.MotorDegree" ||
                e.PropertyName == "MotionController.LeadScrewPitch" ||
                e.PropertyName == "MotionController.Subdivision")
            {
                try
                {
                    logger?.Log($"Stage config changed: Motor={MTAengine.Stage.MotorDegree}°, Pitch={MTAengine.Stage.LeadScrewPitch}mm, Subdiv={MTAengine.Stage.Subdivision}", LogLevel.Info, "Stage.Config");
                }
                catch { }
            }

            // Log start/stop and state transitions for user visibility
            if (e.PropertyName == "IsRunning")
            {
                try
                {
                    if (MTAengine.IsRunning)
                    {
                        _thresholdEventLogged = false; // reset per-run
                        logger?.Log("Engine started running", LogLevel.Info, "Engine.Event");
                    }
                    else
                    {
                        logger?.Log("Engine stopped", LogLevel.Info, "Engine.Event");
                    }
                }
                catch { }
            }

            if (e.PropertyName == "IsMonitoring")
            {
                try
                {
                    logger?.Log($"Monitoring: {(MTAengine.IsMonitoring ? "Started" : "Stopped")}", LogLevel.Info, "Engine.Event");
                }
                catch { }
            }

            if (e.PropertyName == "ThresholdMet")
            {
                try
                {
                    if (MTAengine.ThresholdMet)
                    {
                        // use last processed sample if available
                        if (_lastProcessedSample != null)
                        {
                            logger?.Log($"Threshold met: {_lastProcessedSample.Newtons:F6} N at t={_lastProcessedSample.TimeStamp:F6} s", LogLevel.Info, "Stage.Event");
                        }
                        else
                        {
                            logger?.Log("Threshold met", LogLevel.Info, "Stage.Event");
                        }
                    }
                    else
                    {
                        logger?.Log("Threshold cleared", LogLevel.Info, "Stage.Event");
                    }
                }
                catch { }
            }

            if (e.PropertyName == "IsStageRunning")
            {
                try
                {
                    logger?.Log($"Stage moving: {(MTAengine.IsStageRunning ? "Yes" : "No")}", LogLevel.Info, "Stage.Event");
                }
                catch { }
            }

            // Handle ActualRate changes to update the rate display label
            // Match both unprefixed "ActualRate" (raised directly by Engine) and prefixed "Engine.ActualRate"
            if (e.PropertyName == nameof(Engine.ActualRate) || e.PropertyName == "ActualRate" || e.PropertyName == "Engine.ActualRate")
            {
                Action set = () =>
                {
                    try
                    {
                        string txt = $"{MTAengine.ActualRate} Hz";
                        // Try to find RateReadingLabel by name
                        var found = this.Controls.Find("RateReadingLabel", true);
                        if (found.Length > 0 && found[0] is Label lbl)
                        {
                            lbl.Text = txt;
                            return;
                        }
                        // Fallback to designer field if present
                        try
                        {
                            if (RateReadingLabel != null)
                            {
                                RateReadingLabel.Text = txt;
                            }
                        }
                        catch { }
                    }
                    catch { }
                };
                if (this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(set); else set();
                try { logger?.Log($"Actual sample rate: {MTAengine.ActualRate} Hz", LogLevel.Info, "DAQ"); } catch { }
            }

        }

        private async void ScanAvailableMotionControllerDevicesButton_Click(object sender, EventArgs e)
        {
            ScanAvailableMotionControllerDevicesButton.Enabled = false;
            await Task.Run(() => MTAengine.GetAvailableDevices());
            // Ensure combo updated (GetAvailableDevices triggers PropertyChanged)
            try
            {
                Action setList = () =>
                {
                    try { AvailableDevicesComboBox.DataSource = MTAengine.Connection.AvailableDevices; if (AvailableDevicesComboBox.Items.Count > 0) AvailableDevicesComboBox.SelectedIndex = 0; } catch { }
                };
                if (this.IsHandleCreated && this.InvokeRequired) this.BeginInvoke(setList); else setList();
            }
            catch { }
            ScanAvailableMotionControllerDevicesButton.Enabled = true;

        }

        private async void ConnectToMotionControllerButton_Click(object sender, EventArgs e)
        {
            ConnectToMotionControllerButton.Enabled = false;
            if (AvailableDevicesComboBox.SelectedItem != null)
            {
                var selection = AvailableDevicesComboBox.SelectedItem.ToString();
                if (selection == "No Devices Available")
                {
                    ConnectToMotionControllerButton.Enabled = true;
                    MessageBox.Show("No motion controller devices available.", "Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Extract COM port name (e.g., "COM3") robustly
                string portName = selection;
                try
                {
                    // If item contains additional text, take first token that starts with COM
                    var toks = selection.Split(new[] { ' ', '-' }, System.StringSplitOptions.RemoveEmptyEntries);
                    var comTok = toks.FirstOrDefault(t => t.StartsWith("COM", System.StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(comTok)) portName = comTok;

                    // parse digits
                    var digits = System.Text.RegularExpressions.Regex.Replace(portName, "[^0-9]", "");
                    if (!short.TryParse(digits, out short prt))
                    {
                        MessageBox.Show("Unable to parse selected COM port.", "Connection", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ConnectToMotionControllerButton.Enabled = true;
                        return;
                    }

                    // perform connect on background thread to avoid blocking UI
                    bool ok = await MTAengine.ConnectToMotionControllerAsync(prt);

                    // Wire up logging for the newly connected stage
                    if (ok && MTAengine.Stage != null)
                    {
                        try
                        {
                            MTAengine.Stage.SetLoggers(
                                (msg, src) => logger?.Log(msg, LogLevel.Info, src),
                                (msg, src) => logger?.Log(msg, LogLevel.Warning, src),
                                (msg, src) => logger?.Log(msg, LogLevel.Error, src)
                            );
                        }
                        catch { }
                    }

                    // verify connection status after attempting connect
                    try
                    {
                        if (!ok || MTAengine.Stage == null || !MTAengine.Stage.ConnectionStatus)
                        {
                            // Show diagnostic details from Engine/Stage to help debug why connect failed
                            string details = string.Empty;
                            try { if (MTAengine != null && !string.IsNullOrEmpty(MTAengine.ErrorString)) details += "Engine: " + MTAengine.ErrorString + "\n"; } catch { }
                            try { if (MTAengine?.Stage != null && !string.IsNullOrEmpty(MTAengine.Stage.ErrorMessage)) details += "Stage: " + MTAengine.Stage.ErrorMessage + "\n"; } catch { }
                            if (string.IsNullOrEmpty(details)) details = "No additional error information available.";

                            MessageBox.Show("Failed to connect to motion controller. Check the serial port and try again.\n\nDetails:\n" + details, "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {
                            MessageBox.Show("Motion controller connected.", "Connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error connecting to motion controller: " + ex.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            ConnectToMotionControllerButton.Enabled = true;
        }

        private async void SendYToHomeButton_Click(object sender, EventArgs e)
        {
            //Check if the motion controller is succesffuly connected; if it is issue the command to send the Y axis to the home position
            SendYToHomeButton.Enabled = false;
            if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
            {
                //Make this a task to run a seperate thread to leave ui response
                await Task.Run(() => MTAengine.HomeYStage());

            }
            else
            {
                MessageBox.Show("Motion controller is not connected. Cannot home Y-stage.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            SendYToHomeButton.Enabled = true;
        }

        private async void StopMotionControllerButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
            {
                await Task.Run(() => MTAengine.StopMotionController());
                try { logger?.Log("Stop button pressed by user", LogLevel.Info, "Stage.Event"); } catch { }
            }
        }

        private void StepperMotorAngle09RadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (StepperMotorAngle09RadioButton.Checked)
            {
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetMotorDegree(0.9));
                }
            }
        }

        private void StepperMotorAngle18RadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (StepperMotorAngle18RadioButton.Checked)
            {
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetMotorDegree(1.8));
                }
            }
        }

        private void LeadScrewPitch05mmRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (LeadScrewPitch05mmRadioButton.Checked)
            {
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetScrewLeadPitch(0.5));
                }
            }
        }

        private void LeadScrewPitch1mmRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (LeadScrewPitch1mmRadioButton.Checked)
            {
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetScrewLeadPitch(1));
                }
            }
        }

        private void LeadScrewPitch2mmRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (LeadScrewPitch2mmRadioButton.Checked)
            {
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetScrewLeadPitch(2));
                }
            }
        }

        private void LeadScrewPitch25mmRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (LeadScrewPitch25mmRadioButton.Checked)
            {
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetScrewLeadPitch(2.5));
                }
            }
        }

        private void CalculatePulseEquivalentButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    //TODO: Figure out what the hell is going on with pulse equivalent calculation. Seems like a division where there should be a multiplication
                    Task.Run(() => MTAengine.CalculatePulseEquivalent());

                    MessageBox.Show("Pulse Equivalent Calculated", "Pulse Equivalent", MessageBoxButtons.OK);
                    DialogResult result = MessageBox.Show("Would you like to set the pulse equivalent to standard for the motion controller?", "Set Pulse Equivalent to 1600", MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes)
                    {
                        MTAengine.Stage.PulseEquivalent = 1600;
                    }
                }
                else
                {
                    MessageBox.Show("Motion controller is not connected. Cannot calculate pulse equivalent.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                //TODO: Add a log label or text box for visual log of errors during operation
                MessageBox.Show("An error occurred while calculating the pulse equivalent: " + ex.Message);


            }
        }

        private async void MotionControllerSubdivisionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    //How do I marshall this?

                    MTAengine.SetSubdivision((int)MotionControllerSubdivisionComboBox.SelectedIndex);

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void ChangeMotionControllerSpeedButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
            {
                if (short.TryParse(MotionControllerSpeedTextBox.Text, out short outdata))
                {
                    await Task.Run(() => MTAengine.SetStageSpeed(outdata));
                }
            }
            else
            {
                MessageBox.Show("Motion controller is not connected. Cannot change speed.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void MoveYAxisButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
            {
                if (double.TryParse(this.YAxisDisplacementTextBox.Text, out double distance))
                {
                    await Task.Run(() => MTAengine.TranslateYStage(distance));
                }
            }
            else
            {
                MessageBox.Show("Motion controller is not connected. Cannot move Y-stage.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }



        private void DAQStopMonitoringButton_Click(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    if (MTAengine.IsStageRunning && MTAengine.Stage.ConnectionStatus)
                    {
                        MTAengine.Stage.Stop();
                        MTAengine.IsStageRunning = false;
                    }
                    if (MTAengine.IsMonitoring)
                    {
                        MTAengine.StopBackgroundCollection();
                        MTAengine.IsMonitoring = false;
                    }

                }));

            }
            else
            {
                if (MTAengine.Stage.ConnectionStatus)
                {
                    MTAengine.StopMotionController();
                    MTAengine.IsStageRunning = false;
                }
                if (MTAengine.IsMonitoring)
                {
                    MTAengine.StopBackgroundCollection();
                    MTAengine.IsMonitoring = false;
                }

            }

        }

        private async void StartConstantMonitorButton_Click(object sender, EventArgs e)
        {
            // Stop any existing test FIRST
            await MTAengine.StopAsync();
            await Task.Delay(100);

            // Clear chart and buffers
            ClearChartAndBuffers();

            chartSaveToFile = false;
            fileSavePath = null;

            // Chart already cleared by ClearChartAndBuffers, but create new series
            MonitorResponseChart.Series.Clear();
            this.relativeStartTime = double.NaN;
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);

            MTAengine.DataCollectionTime = 600;
            if (double.TryParse(PlaneDetectionThresholdTextBox.Text, out var planeThresh))
            {
                // SET THRESHOLD DIRECTLY - do NOT add ForceOffset here
                // The ForceOffset is already applied to the measured force in the Engine,
                // so the threshold should be the raw user-entered value
                MTAengine.FindPlaneThreshold = planeThresh;

                // START CHART THREAD FIRST (before engine starts collecting data)
                StartChartUpdateThread();

                // WAIT for chart thread to be ready
                await Task.Delay(200);  // Give chart thread time to initialize

                // NOW start the engine
                MTAengine.FindPlane();

                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    MTAengine.TranslateYStage(-1000);
                }
                else
                {
                    MessageBox.Show("Motion controller is not connected. Skipping stage move.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid threshold value");
            }
        }

        private void ZeroVoltageButton_Click(object sender, EventArgs e)
        {
            if (MonitorResponseChart.Series[0].Points.Count > 0)
            {

                voltageOffset = MonitorResponseChart.Series[0].Points.Average(point => point.YValues[0]);
            }
        }


        private void ReturnProbeToMaxHeightButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
            {
                MTAengine.SetStageSpeed(100);
                Thread.Sleep(100);
                MTAengine.TranslateYStage(10);
            }
            else
            {
                MessageBox.Show("Motion controller is not connected. Cannot move stage.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        async private void backgroundWorkerStartButton_Click(object sender, EventArgs e)
        {
            // Stop any existing test FIRST
            await MTAengine.StopAsync();
            await Task.Delay(100);

            // Clear chart and buffers
            ClearChartAndBuffers();

            MonitorResponseChart.Series.Clear();
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            MTAengine.StartMonitor();
        }

        private void stopBackgroundWorkerButton_Click(object sender, EventArgs e)
        {
            // schedule StopAsync on background thread to avoid blocking UI
            Task.Run(async () => await MTAengine.StopAsync());
        }

        private async void FractureTestStartButton_Click(object sender, EventArgs e)
        {
            // Stop any existing test FIRST
            await MTAengine.StopAsync();
            await Task.Delay(100);

            // Clear chart and buffers
            ClearChartAndBuffers();

            MonitorResponseChart.Series.Clear();
            this.relativeStartTime = double.NaN;
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);

            double.TryParse(CollectionTimeSecondsTextBox.Text, out double result);
            if (result == 0)
            {
                await Task.Run(() => MessageBox.Show("Please enter a valid collection time"));
                return;
            }
            else if (result < 60)
            {
                result = 60;
                Task.Run(() => MessageBox.Show("Minimum Fracture Collection Time is 60 seconds, setting to minumum"));
            }
            MTAengine.DataCollectionTime = result;
            MTAengine.FindPlaneThreshold = 100 + voltageOffset;

            if (double.TryParse(FractureDepthTextBox.Text, out var depth))
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "CSV files (*.csv)|*.csv";
                    sfd.FileName = $"fracture_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    if (sfd.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    // Set up ONLY the fracture file writer - do NOT use chartSaveToFile
                    chartSaveToFile = false;
                    fileSavePath = null;

                    fractureFileSavePath = sfd.FileName;
                    fractureSaving = true;
                    fractureFileWriteQueue = new BlockingCollection<string>(new ConcurrentQueue<string>());
                    var path = fractureFileSavePath;

                    try { logger?.Log($"Fracture data file created: {System.IO.Path.GetFileName(path)}", LogLevel.Info, "File.Event"); } catch { }

                    fractureFileWriterTask = Task.Run(() =>
                    {
                        int linesWritten = 0;
                        try
                        {
                            using (var sw = new StreamWriter(path, false))
                            {
                                sw.WriteLine("Time,Newtons,Position_mm");
                                linesWritten++;
                                foreach (var line in fractureFileWriteQueue.GetConsumingEnumerable())
                                {
                                    sw.WriteLine(line);
                                    linesWritten++;
                                    if (fractureFileWriteQueue.Count == 0) sw.Flush();
                                }
                            }
                            try { logger?.Log($"Fracture file closed: {linesWritten} lines written", LogLevel.Info, "File.Event"); } catch { }
                        }
                        catch (Exception ex)
                        {
                            try { logger?.Log($"File write error: {ex.Message}", LogLevel.Error, "File.Event"); } catch { }
                        }
                    });
                }

                MTAengine.FractureDistance = depth;
                DialogResult dresult = MessageBox.Show("Current Fracture Depth:" + MTAengine.FractureDistance);

                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    MTAengine.FractureTest();
                    StartChartUpdateThread();
                    Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        try { MTAengine.TranslateYStage(MTAengine.FractureDistance); } catch { }
                    });
                }
                else
                {
                    MTAengine.FractureTest();
                    StartChartUpdateThread();
                    MessageBox.Show("Motion controller is not connected. Starting fracture test without movement.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MTAengine.FractureDistance = 0;
                MessageBox.Show("Please enter a valid depth value");
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            //Check if this one is selected, if it is then set the conversion factor to 2141.878
            // And set the newton conversion factor to 4.44822
            if (radioButton1.Checked)
            {
                if (double.TryParse(FractureDepthTextBox.Text, out double thresh))
                {
                    //MTAengine.FractureThreshold = thresh;
                }
                else
                {
                    //MTAengine.FractureThreshold = 1.5;
                }
                //this.PunctureTestStartButton.Enabled = false;
                this.FractureTestStartButton.Enabled = true;
                MTAengine.VoltageConversion = this.MTAengine.FractureVoltageConversion;
                MTAengine.NewtonConversion = this.MTAengine.FractureNewtonConversion;
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                //if (double.TryParse(PunctureMaxDepthTextBox.Text, out double thresh))
                //{
                //    //MTAengine.PunctureThreshold = thresh;
                //}
                //else
                //{
                //    // MTAengine.PunctureThreshold = 0.05;
                //}
                //this.PunctureTestStartButton.Enabled = false;
                this.FractureTestStartButton.Enabled = true;
                MTAengine.VoltageConversion = this.MTAengine.PunctureVoltageConversion;
                MTAengine.NewtonConversion = this.MTAengine.PunctureNewtonConversion;
            }
        }

        private async void PunctureTestStartButton_Click(object sender, EventArgs e)
        {

            //await Task.Run(() => MTAengine.StopAsync());
            //await Task.Run(() => MTAengine.StopMotionController());
            //await Task.Run(() => MTAengine.GetYLocation());
            //MonitorResponseChart.Series.Clear();
            //Series series = new Series
            //{
            //    ChartType = SeriesChartType.Line
            //};
            //if (double.TryParse(PunctureMaxDepthTextBox.Text, out double depth))
            //{

            //    MTAengine.PunctureDistance = depth;
            //    MonitorResponseChart.Series.Add(series);
            //  //  DAQDataGridView.Rows.Clear();
            //    //MTAengine.SetStageSpeed(1);
            //    this._voltageConversion = MTAengine.PunctureVoltageConversion;
            //    this._newtonConversion = MTAengine.PunctureNewtonConversion;
            //    Thread.Sleep(10);

            //    MTAengine.PunctureTest();
            //}
            //else
            //{
            //    MessageBox.Show("Please enter a valid depth value");
            //}
        }

        //private void SetPunctureOffsetButton_Click(object sender, EventArgs e)
        //{
        //    //Take the average of the first 1000 data points in the voltage column and set that as the offset
        //    double offset = 0;
        //    for (int i = 0; i < DAQDataGridView.RowCount; i++)
        //    {
        //        offset += Convert.ToDouble(DAQDataGridView.Rows[i].Cells[1].Value);
        //    }
        //    offset = offset / DAQDataGridView.RowCount;
        //    MTAengine.ForceOffset = offset;
        //}

        private void MicroTextureAnalyzerTabPage_Click(object sender, EventArgs e)
        {

        }

        private async void button1_Click(object sender, EventArgs e)
        {
            // Stop any existing test FIRST
            await MTAengine.StopAsync();
            await Task.Delay(100);

            // Clear chart and buffers
            ClearChartAndBuffers();

            chartSaveToFile = false;
            fileSavePath = null;

            MonitorResponseChart.Series.Clear();
            this.relativeStartTime = double.NaN;
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);

            if (PlaneDetectionThresholdTextBox.Text != "")
            {
                MTAengine.FindPlaneThreshold = double.Parse(PlaneDetectionThresholdTextBox.Text);
            }
            double.TryParse(CollectionTimeSecondsTextBox.Text, out double result);
            if (result == 0)
            {
                MessageBox.Show("Please enter a valid collection time");
                return;
            }

            if (double.TryParse(FractureDepthTextBox.Text, out var depth))
            {
                MTAengine.FractureDistance = depth;
            }
            else
            {
                MTAengine.FractureDistance = 0;
            }

            MTAengine.DataCollectionTime = result;

            MTAengine.ContinuousScanTest();
            StartChartUpdateThread();
        }

        private void resetEngineButton_Click(object sender, EventArgs e)
        {
            // Reset functionality disabled — keep UI stable
            MessageBox.Show("Reset is disabled. Use Stop or reconnect devices if needed.", "Reset Disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private bool _thresholdEventLogged = false;
        private Engine.ProcessedDataChangedEventArgs _lastProcessedSample = null;
        private readonly object _displayLock = new object();

        /// <summary>
        /// Comprehensive cleanup of chart, buffers, and threads before starting a new test.
        /// Ensures no residual data or threads interfere with the new test.
        /// </summary>
        private void ClearChartAndBuffers()
        {
            try
            {
                // Step 1: Cancel and wait for chart update thread to fully exit
                try
                {
                    chartCancellationTokenSource?.Cancel();
                }
                catch { }

                // Wait for thread to exit (increased timeout to 500ms for reliability)
                if (chartUpdateThread != null && chartUpdateThread.IsAlive)
                {
                    try
                    {
                        if (!chartUpdateThread.Join(500))
                        {
                            // Thread didn't exit gracefully, log warning
                            try { logger?.Log("Chart update thread did not exit in time", LogLevel.Warning, "Chart.Cleanup"); } catch { }
                        }
                    }
                    catch { }
                }

                // Step 2: Clear data queue
                while (dataQueue.TryDequeue(out _)) { }

                // Step 3: Clear display buffer with lock
                lock (_displayLock)
                {
                    displayBuffer?.Clear();
                }

                // Step 4: Reset relative start time
                relativeStartTime = double.NaN;

                // Step 5: Clear chart series on UI thread with suspend/resume layout
                try
                {
                    if (MonitorResponseChart != null && !MonitorResponseChart.IsDisposed)
                    {
                        if (MonitorResponseChart.InvokeRequired)
                        {
                            MonitorResponseChart.Invoke(new Action(() =>
                            {
                                try
                                {
                                    MonitorResponseChart.SuspendLayout();
                                    MonitorResponseChart.Series.Clear();
                                    MonitorResponseChart.ResumeLayout();
                                    MonitorResponseChart.Invalidate();
                                }
                                catch { }
                            }));
                        }
                        else
                        {
                            MonitorResponseChart.SuspendLayout();
                            MonitorResponseChart.Series.Clear();
                            MonitorResponseChart.ResumeLayout();
                            MonitorResponseChart.Invalidate();
                        }
                    }
                }
                catch { }

                // Step 6: Dispose old cancellation token source
                try
                {
                    chartCancellationTokenSource?.Dispose();
                    chartCancellationTokenSource = null;
                }
                catch { }

                // Small delay to ensure UI updates complete
                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                try { logger?.Log($"Error during chart cleanup: {ex.Message}", LogLevel.Error, "Chart.Cleanup"); } catch { }
            }
        }


        private void voltageOffsetReadingLabel_Click(object sender, EventArgs e)
        {

        }

        private void zero_voltage_button_Click(object sender, EventArgs e)
        {
            this.MTAengine.ComputeAndSetForceOffset();
        }

        private void clear_zero_button_Click(object sender, EventArgs e)
        {
            this.MTAengine.SetForceOffset(0);
        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void DAQ_StageSpeedComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            //change the selected index of the combo box to the selected index of the list
            int selectedIndex = DAQ_StageSpeedComboBox.SelectedIndex;
            // Validate the index is within bounds
            if (selectedIndex >= 0 && selectedIndex < stageSpeedConversionSpeedList.Count)
            {
                short controllerSpeedValue = (short)stageSpeedConversionSpeedList[selectedIndex];
                MTAengine.SetStageSpeed(controllerSpeedValue);
                MessageBox.Show("Stage Speed Set to: " + controllerSpeedValue.ToString() + ": " + DAQ_StageSpeedComboBox.SelectedItem?.ToString() + "um/s");
            }
        }

        private void saveFileButton_Click(object sender, EventArgs e)
        {
            PromptUserToSave();
        }

        private void ThousandHertzRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (ThousandHertzRadioButton.Checked)
            {
                MTAengine.SetSamplingRate(1000);
            }
        }

        private void TwoThousandHertzRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (TwoThousandHertzRadioButton.Checked)
            {
                MTAengine.SetSamplingRate(1500);
            }
        }

        private void radioButton6_CheckedChanged(object sender, EventArgs e)
        {
            if (ThreeThousandHertzRadioButton.Checked)
            {
                MTAengine.SetSamplingRate(3000);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {

            if (MTAengine.Stage.ConnectionStatus)
            {
                MTAengine.StopMotionController();
            }
            //Stop the engine and wait for it to finish if it's running.
            if (MTAengine.IsMonitoring)
            {
                // If StopAsync returns a Task, wait for its completion.
                MTAengine.StopBackgroundCollection();
            }

            // finalize fracture writer if active
            try
            {
                if (fractureSaving && fractureFileWriteQueue != null)
                {
                    var q = fractureFileWriteQueue;
                    fractureSaving = false;
                    fractureFileWriteQueue = null;
                    q.CompleteAdding();
                    fractureFileWriterTask?.Wait(500);
                }
            }
            catch { }

            base.OnFormClosing(e);
            Environment.Exit(0);
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private async void XPSTestMove_Click(object sender, EventArgs e)
        {

            await MTAengine.MoveRelativeAsync(1.0);

        }

        private async void button3_Click(object sender, EventArgs e)
        {
            await MTAengine.ConnectAsync();
            await MTAengine.InitializeAsync();
        }
    }
}

