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
        private int displayWindowSecondsContinuous = 5; // small sliding window for live display (seconds)
        private int maxDisplayPoints = 1200; // maximum points to display (approx pixels)
        private CircularBuffer<(double X, double Y)> displayBuffer;
        private BlockingCollection<string> fileWriteQueue;
        private Task fileWriterTask;
        private CancellationTokenSource chartCancellationTokenSource;
        
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


            MTAengine = engine;
            MTAengine.PropertyChanged += MTAengine_PropertyChanged;
            var subdivisionList = new List<int> { 1, 2, 4, 8 };
            var stageSpeedList = new List<double> { 19.1, 95.5, 152.8, 190.1, 248.3, 305.6, 401.0, 496.5 }; //unts in um/s
            var averageWindowList = new List<int> { 0, 10, 25, 50, 100, 150, 200, 250, 500, 1000 };





            InitializeComponent();
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
        //Convert this to event driven so that the data is updated when the event is thrown
        private async void StartChartUpdateThread()
        {
            if (!MTAengine.IsRunning)
            {
                return;
            }

            // stop any previous chart thread
            try { chartCancellationTokenSource?.Cancel(); } catch { }
            if (chartUpdateThread != null && chartUpdateThread.IsAlive)
            {
                try { chartUpdateThread.Join(200); } catch { }
            }

            // prepare circular buffer: always keep only a small recent window for live display
            int rate = Math.Max(1, MTAengine.Rate);
            int windowSec = Math.Max(1, displayWindowSecondsContinuous);
            int capacity = Math.Max(1000, rate * windowSec);
            displayBuffer = new CircularBuffer<(double X, double Y)>(capacity);

            // reset relative start time so chart X axis will show time since this run started
            relativeStartTime = double.NaN;

            // prepare file writer if saving mode and path provided
            if (chartSaveToFile && !string.IsNullOrEmpty(fileSavePath))
            {
                try
                {
                    fileWriteQueue = new BlockingCollection<string>(new ConcurrentQueue<string>());
                    var path = fileSavePath; // capture
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
                        MonitorResponseChart.Series.Clear();
                        var s = new Series { ChartType = SeriesChartType.FastLine, XValueType = ChartValueType.Double, YValueType = ChartValueType.Double };
                        MonitorResponseChart.Series.Add(s);
                        try { typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(MonitorResponseChart, true, null); } catch { }
                    }));
                }
            }
            catch { }

            chartCancellationTokenSource = new CancellationTokenSource();
            chartUpdateThread = new Thread(ProcessDataQueue) { IsBackground = true };
            chartUpdateThread.Start();
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

            while (!token.IsCancellationRequested && MTAengine.IsRunning)
            {
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
                    foreach (var d in batch)
                    {
                        try { fileWriteQueue.Add($"{d.TimeStamp:F6},{d.Newtons:F6}"); } catch { }
                    }
                }
                else
                {
                    // Update force reading label with most recent sample in batch
                    try { UpdateForceReadingLabel(batch.Last().Newtons); } catch { }

                    // Add samples to displayBuffer (bounded by circular buffer) and optionally queue to file
                    foreach (var d in batch)
                    {
                        if (double.IsNaN(relativeStartTime)) relativeStartTime = d.TimeStamp;
                        double t = d.TimeStamp - relativeStartTime;
                        displayBuffer.Add((t, d.Newtons));

                        if (chartSaveToFile && fileWriteQueue != null)
                        {
                            try { fileWriteQueue.Add($"{t:F6},{d.Newtons:F6}"); } catch { }
                        }
                    }

                    // prepare downsampled arrays from circular buffer
                    var raw = displayBuffer.ToArray();
                    int total = raw.Length;
                    if (total == 0) continue;

                    // Compute sliding window bounds based on latest time and desired window size (use continuous window)
                    int plotWindowSec = Math.Max(1, displayWindowSecondsContinuous);
                    double rightAll = raw[total - 1].X;
                    double leftWindow = rightAll - plotWindowSec;

                    // Only keep points inside the visible window to avoid plotting older points.
                    var windowed = raw.Where(p => p.X >= leftWindow).ToArray();
                    if (windowed.Length == 0) continue;

                    // Use windowed data for downsampling / plotting
                    raw = windowed;
                    total = raw.Length;

                    int target = Math.Min(maxDisplayPoints, MonitorResponseChart?.Width > 0 ? MonitorResponseChart.Width : maxDisplayPoints);
                    if (target <= 0) target = Math.Min(maxDisplayPoints, 1000);

                    double[] xs, ys;
                    if (total <= target)
                    {
                        xs = new double[total]; ys = new double[total];
                        for (int i = 0; i < total; i++) { xs[i] = raw[i].X; ys[i] = raw[i].Y; }
                    }
                    else
                    {
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
                        xs = xsList.ToArray(); ys = ysList.ToArray();
                    }

                    // marshal update to UI
                    try
                    {
                        // Pass the desired visible window bounds so UpdateChartWithArrays can keep fixed width
                        this.BeginInvoke(new Action<double[], double[], double>((a, b, right) => UpdateChartWithArrays(a, b, right)), xs, ys, rightAll);
                        // Also update time label with most recent relative time
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
                if (MonitorResponseChart == null) return;
                if (MonitorResponseChart.Series.Count == 0)
                {
                    MonitorResponseChart.Series.Add(new Series { ChartType = SeriesChartType.FastLine, XValueType = ChartValueType.Double, YValueType = ChartValueType.Double });
                }
                var s = MonitorResponseChart.Series[0];
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
                        double leftBound = rightBound - windowSec;

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

            // If a fracture test file writer is active, enqueue a CSV line: Time,Newtons,Position_mm
            if (fractureSaving && fractureFileWriteQueue != null)
            {
                try
                {
                    // Use per-sample aligned position provided by the engine in the event args
                    double pos = double.IsNaN(e.PositionMm) ? double.NaN : e.PositionMm;
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
            //if (e.PropertyName == nameof(Engine.PunctureTestComplete))
            //{
            //    if (MTAengine.PunctureTestComplete)
            //    {
            //        //Take the data from the chart and add it to the datagrid
            //        Task.Run(() => Invoke((MethodInvoker)(delegate
            //        {
            //            //Prompt a messagebox that asks the user if they want to save the file
            //            SaveFileDialog saveFileDialog = new SaveFileDialog();
            //            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            //            saveFileDialog.FilterIndex = 2;
            //            saveFileDialog.RestoreDirectory = true;
            //            //Use data from datagrid to save to a file
            //            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            //            {
            //                using (var writer = new StreamWriter(saveFileDialog.FileName))
            //                {
            //                    // Write headers
            //                    for (int i = 0; i < DAQDataGridView.Columns.Count; i++)
            //                    {
            //                        writer.Write(DAQDataGridView.Columns[i].HeaderText);
            //                        if (i < DAQDataGridView.Columns.Count - 1)
            //                        {
            //                            writer.Write(",");
            //                        }
            //                    }
            //                    writer.WriteLine();

            //                    // Write rows
            //                    for (int i = 0; i < DAQDataGridView.Rows.Count; i++)
            //                    {
            //                        for (int j = 0; j < DAQDataGridView.Columns.Count; j++)
            //                        {
            //                            writer.Write(DAQDataGridView.Rows[i].Cells[j].Value?.ToString());
            //                            if (j < DAQDataGridView.Columns.Count - 1)
            //                            {
            //                                writer.Write(",");
            //                            }
            //                        }
            //                        writer.WriteLine();
            //                    }
            //                }
            //                //dataListBox.Items.Add("Results saved.");
            //            }
            //            else
            //            {
            //                // dataListBox.Items.Add("Save canceled.");
            //            }

            //        })));
            //    }
            //}

            //EHandle Connection event to populate combox with available devices after scan for devies button has been pressed
            if (e.PropertyName == "Connection.AvailableDevices")
            {
                AvailableDevicesComboBox.DataSource = MTAengine.Connection.AvailableDevices;
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
            if (e.PropertyName == "MotionController.ErrorMessage")
            {
                MessageBox.Show(MTAengine.Stage.ErrorMessage);
            }
            if (e.PropertyName == "MotionController.PulseEquivalent")
            {
                MessageBox.Show(MTAengine.Stage.PulseEquivalent.ToString());
                PulseEquivalentResponseLabel.Text = MTAengine.Stage.PulseEquivalent.ToString();
            }

            // Update position reading when the MotionController reports a Y position change
            if (e.PropertyName == "MotionController.CurrentYPosition")
            {
                Action updatePos = () =>
                {
                    try
                    {
                        if (MTAengine?.Stage != null)
                        {
                            // display in mm with 3 decimal places
                            PositionReadingLabel.Text = MTAengine.Stage.CurrentYPosition.ToString("F3") + " mm";
                        }
                    }
                    catch { }
                };

                if (InvokeRequired) BeginInvoke(updatePos); else updatePos();
            }

            if (e.PropertyName == nameof(Engine.ThresholdMet))
            {


                if (MTAengine.ThresholdMet)
                {
                    //MTAengine.StopMotionController();
                    //MessageBox.Show("Threshold Met");
                }
            }

            // Update UI when engine reports actual DAQ sampling rate
            if (e.PropertyName == nameof(Engine.ActualRate))
            {
                Action update = () =>
                {
                    try
                    {
                        if (RateReadingLabel != null)
                        {
                            RateReadingLabel.Text = MTAengine.ActualRate.ToString() + " Hz";
                            var orig = RateReadingLabel.BackColor;
                            RateReadingLabel.BackColor = Color.LightBlue;
                            Task.Run(async () =>
                            {
                                await Task.Delay(400);
                                if (!RateReadingLabel.IsDisposed && RateReadingLabel.IsHandleCreated)
                                {
                                    try { RateReadingLabel.BeginInvoke(new Action(() => RateReadingLabel.BackColor = orig)); } catch { }
                                }
                            });
                        }
                    }
                    catch { }
                };
                if (InvokeRequired) BeginInvoke(update); else update();
            }

            // Update UI when engine force offset changes so user always sees current zero
            if (e.PropertyName == nameof(Engine.ForceOffset))
            {
                // Use BeginInvoke/Invoke to marshal to UI thread if needed
                Action update = () =>
                {
                    try
                    {
                        if (forceOffsetReadingLabel != null)
                        {
                            // Display force offset in millinewtons with 5 decimal places
                            forceOffsetReadingLabel.Text = (MTAengine.ForceOffset * 1000.0).ToString("F5");

                            // Visual confirmation when resetting to zero (briefly flash background)
                            if (Math.Abs(MTAengine.ForceOffset) < 1e-6)
                            {
                                var orig = forceOffsetReadingLabel.BackColor;
                                forceOffsetReadingLabel.BackColor = Color.LightGreen;
                                // restore color after short delay without blocking UI thread
                                Task.Run(async () =>
                                {
                                    await Task.Delay(500);
                                    if (!forceOffsetReadingLabel.IsDisposed && forceOffsetReadingLabel.IsHandleCreated)
                                    {
                                        try { forceOffsetReadingLabel.BeginInvoke(new Action(() => forceOffsetReadingLabel.BackColor = orig)); } catch { }
                                    }
                                });
                            }
                        }
                    }
                    catch { }
                };

                if (InvokeRequired) BeginInvoke(update); else update();
            }

        }

        private async void ScanAvailableMotionControllerDevicesButton_Click(object sender, EventArgs e)
        {
            ScanAvailableMotionControllerDevicesButton.Enabled = false;
            await Task.Run(() => MTAengine.GetAvailableDevices());
            //AvailableDevicesComboBox.DataSource = MTAengine.Connection.AvailableDevices;
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
                var prt = short.Parse(selection.Substring(selection.Length - 1));
                // perform connect on background thread to avoid blocking UI
                bool ok = await MTAengine.ConnectToMotionControllerAsync(prt);
                // verify connection status after attempting connect
                try
                {
                    if (!ok || MTAengine.Stage == null || !MTAengine.Stage.ConnectionStatus)
                    {
                        MessageBox.Show("Failed to connect to motion controller. Check the serial port and try again.", "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        MessageBox.Show("Motion controller connected.", "Connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch { }
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
            }
            else
            {
                MessageBox.Show("Motion controller is not connected.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            chartSaveToFile = false;
            fileSavePath = null;
            await Task.Run(() => MTAengine.StopAsync());
            MonitorResponseChart.Series.Clear();
            this.relativeStartTime = double.NaN;
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            // DAQDataGridView.Rows.Clear();
            //MTAengine.SetStageSpeed(0);
            //double.TryParse(CollectionTimeSecondsTextBox.Text, out double result);
            //if (result == 0)
            //{
            //    MessageBox.Show("Please enter a valid collection time");
            //    return;
            //}
            MTAengine.DataCollectionTime = 600;
            if (double.TryParse(PlaneDetectionThresholdTextBox.Text, out var planeThresh))
            {
                MTAengine.FindPlaneThreshold = planeThresh + this.MTAengine.ForceOffset;
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    MTAengine.TranslateYStage(-1000);
                }
                else
                {
                    MessageBox.Show("Motion controller is not connected. Skipping stage move.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                MTAengine.FindPlane();
                StartChartUpdateThread();
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
            if (MTAengine.IsRunning || MTAengine.IsMonitoring)
            {
                // If engine busy, stop current operation then proceed to start a new monitor
                await Task.Run(() => MTAengine.StopAsync());
                // give workers a moment to unwind
                await Task.Delay(50);
            }
            MonitorResponseChart.Series.Clear();
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            // DAQDataGridView.Rows.Clear();
            MTAengine.StartMonitor();

        }

        private void stopBackgroundWorkerButton_Click(object sender, EventArgs e)
        {
            // schedule StopAsync on background thread to avoid blocking UI
            Task.Run(async () => await MTAengine.StopAsync());
        }

        private async void FractureTestStartButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.IsRunning || MTAengine.IsMonitoring)
            {
                // If engine busy, stop current operation and then continue to start fracture test
                await Task.Run(() => MTAengine.StopAsync());
                await Task.Delay(50);
            }
            // previously awaited StopAsync here; continue with setup
            MonitorResponseChart.Series.Clear();
            this.relativeStartTime = double.NaN;
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            //MTAengine.SetStageSpeed(0);
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
                // Prompt user for file path before starting fracture test
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "CSV files (*.csv)|*.csv";
                    sfd.FileName = $"fracture_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    if (sfd.ShowDialog() != DialogResult.OK)
                    {
                        // user cancelled - do not start test
                        return;
                    }
                    fileSavePath = sfd.FileName;
                    chartSaveToFile = true;

                    // Start fracture CSV writer (separate from chart writer)
                    try
                    {
                        fractureFileSavePath = fileSavePath;
                        fractureSaving = true;
                        fractureFileWriteQueue = new BlockingCollection<string>(new ConcurrentQueue<string>());
                        var path = fractureFileSavePath; // capture
                        fractureFileWriterTask = Task.Run(() =>
                        {
                            try
                            {
                                using (var sw = new StreamWriter(path, false))
                                {
                                    sw.WriteLine("Time,Newtons,Position_mm");
                                    foreach (var line in fractureFileWriteQueue.GetConsumingEnumerable())
                                    {
                                        sw.WriteLine(line);
                                        if (fractureFileWriteQueue.Count == 0) sw.Flush();
                                    }
                                }
                            }
                            catch { }
                        });
                    }
                    catch { }
                }

                MTAengine.FractureDistance = depth;
                DialogResult dresult = MessageBox.Show("Current Fracture Depth:" + MTAengine.FractureDistance);
                //MTAengine.SetStageSpeed(1);
                //MTAengine.TranslateYStage(MTAengine.FractureDistance);
                // verify stage connection before starting fracture
                if (MTAengine.Stage != null && MTAengine.Stage.ConnectionStatus)
                {
                    MTAengine.FractureTest();
                    Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        try { MTAengine.TranslateYStage(MTAengine.FractureDistance); } catch { }
                    });
                }
                else
                {
                    MessageBox.Show("Motion controller is not connected. Cannot start fracture test.", "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                StartChartUpdateThread();

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
            if (MTAengine.IsRunning || MTAengine.IsMonitoring)
            {
                // If engine busy, stop current operation and then continue to start a new continuous scan
                await Task.Run(() => MTAengine.StopAsync());
                await Task.Delay(50);
            }
            chartSaveToFile = false;
            fileSavePath = null;
            await Task.Run(() => MTAengine.StopAsync());
            Thread.Sleep(10);
            //await Task.Run(() => MTAengine.Stage.Stop());
            MonitorResponseChart.Series.Clear();
            this.relativeStartTime = double.NaN;
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            // MTAengine.SetStageSpeed(1);
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
            //StartPositionUpdateThread();


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

            //// Ensure chartUpdateThread exists and is alive before joining.
            //if (chartUpdateThread != null && chartUpdateThread.IsAlive)
            //{
            //    chartUpdateThread.Join();
            //}

            base.OnFormClosing(e);
            Environment.Exit(0);
            // Optionally force exit if necessary.
            // Environment.Exit(0);
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
                MTAengine.SetSamplingRate(2000);
            }
        }

        private void radioButton6_CheckedChanged(object sender, EventArgs e)
        {
            if (ThreeThousandHertzRadioButton.Checked)
            {
                MTAengine.SetSamplingRate(3000);
            }
        }

        private void saveFileButton_Click(object sender, EventArgs e)
        {
            PromptUserToSave();
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

        private void AverageWindowComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            //int selectedIndex = AverageWindowComboBox.SelectedIndex;
            //if (selectedIndex >= 0 && selectedIndex < averageWindowList.Count)
            //{
            //    int averageWindowValue = averageWindowList[selectedIndex];
            //   // MTAengine.AverageWindow = averageWindowValue;
            //    MessageBox.Show("Average Window Set to: " + averageWindowValue.ToString() + " samples");
            //}
        }

        private void voltageOffsetReadingLabel_Click(object sender, EventArgs e)
        {

        }

        private void resetEngineButton_Click(object sender, EventArgs e)
        {
            // Reset functionality disabled — keep UI stable
            MessageBox.Show("Reset is disabled. Use Stop or reconnect devices if needed.", "Reset Disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

