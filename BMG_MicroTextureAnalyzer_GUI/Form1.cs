using BMG_MicroTextureAnalyzer;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Timers;
using System.ComponentModel;
using System.Collections.Concurrent;
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
        public double relativeStartTime;

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

        private double voltageOffset = 0;

        public Form1()
        {
            BMG_MicroTextureAnalyzer.Engine engine = new BMG_MicroTextureAnalyzer.Engine();
            MTAengine = engine;
            MTAengine.DataChanged += MTAengine_DataChanged;
            board = new MccDaq.MccBoard(1);


            MTAengine = engine;
            MTAengine.PropertyChanged += MTAengine_PropertyChanged;
            var subdivisionList = new List<int> { 1, 2, 4, 8 };



            InitializeComponent();
            this.MotionControllerSubdivisionComboBox.DataSource = subdivisionList;
            this.MotionControllerSubdivisionComboBox.SelectedIndex = 0;

            DAQDataGridView.Columns.Add("Time", "Time");
            DAQDataGridView.Columns.Add("Voltage", "Voltage");
            DAQDataGridView.Columns.Add("Pounds", "Pounds");
            DAQDataGridView.Columns.Add("Newtons", "Newtons");
            DAQDataGridView.Columns.Add("Step", "Step");
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
            //MonitorResponseChart = new Chart();

            await Task.Run(() => MonitorResponseChart.Invoke(() =>
            {
                MonitorResponseChart.Series.Clear();
                Series series = new Series
                {
                    ChartType = SeriesChartType.Line
                };
                MonitorResponseChart.Series.Add(series);
                chartUpdateThread = new Thread(ProcessDataQueue);
                chartUpdateThread.IsBackground = true;
                chartUpdateThread.Start();
            }));
            //Get YPosition
            //chartUpdateThread.Join();



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
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            while (MTAengine.IsRunning)
            {
                List<Engine.ProcessedDataChangedEventArgs> batch = new List<Engine.ProcessedDataChangedEventArgs>();
                while (stopwatch.ElapsedMilliseconds < batchIntervalMs)
                {
                    if (dataQueue.TryDequeue(out Engine.ProcessedDataChangedEventArgs data))
                    {
                        data.Newtons = data.Newtons - voltageOffset;
                        batch.Add(data);
                    }
                }
                if (batch.Count > 0)
                {
                    UpdateChart(batch);

                }

                stopwatch.Restart();
            }

        }

        private void UpdateChart(List<Engine.ProcessedDataChangedEventArgs> data)
        {
            if (MonitorResponseChart.InvokeRequired)
            {
                MonitorResponseChart.Invoke(new Action<List<Engine.ProcessedDataChangedEventArgs>>(UpdateChart), data);
            }
            else
            {

                foreach (var d in data)
                {
                    if (this.relativeStartTime != -1)
                    {
                        var time = d.TimeStamp - this.relativeStartTime;
                        MonitorResponseChart.Series[0].Points.AddXY(time, d.Newtons);
                       // YPosLabel.Text = MTAengine.YStagePosition.ToString();
                        //YPosLabel.ForeColor = Color.Green;

                    }
                    else
                    {
                        this.relativeStartTime = d.TimeStamp;
                        Series series = new Series
                        {
                            ChartType = SeriesChartType.Line
                        };

                        MonitorResponseChart.Series[0] = series;
                        MonitorResponseChart.Series[0].Points.AddXY(0, 0);
                        //YPosLabel.Text = MTAengine.Stage.CurrentYPosition.ToString();
                        //DAQDataGridView.Rows.Add(0, 0, d.Step);
                    }


                };
            }
        }
        private void MTAengine_DataChanged(object? sender, Engine.ProcessedDataChangedEventArgs e)
        {
            dataQueue.Enqueue(e);
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

            if (e.PropertyName == nameof(Engine.FractureTestComplete))
            {
                if (MTAengine.FractureTestComplete)
                {
                    //Take the data from the chart and add it to the datagrid




                }
            }
            if (e.PropertyName == nameof(Engine.PunctureTestComplete))
            {
                if (MTAengine.PunctureTestComplete)
                {
                    //Take the data from the chart and add it to the datagrid
                    Task.Run(() => Invoke((MethodInvoker)(delegate
                    {
                        //Prompt a messagebox that asks the user if they want to save the file
                        SaveFileDialog saveFileDialog = new SaveFileDialog();
                        saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
                        saveFileDialog.FilterIndex = 2;
                        saveFileDialog.RestoreDirectory = true;
                        //Use data from datagrid to save to a file
                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            using (var writer = new StreamWriter(saveFileDialog.FileName))
                            {
                                // Write headers
                                for (int i = 0; i < DAQDataGridView.Columns.Count; i++)
                                {
                                    writer.Write(DAQDataGridView.Columns[i].HeaderText);
                                    if (i < DAQDataGridView.Columns.Count - 1)
                                    {
                                        writer.Write(",");
                                    }
                                }
                                writer.WriteLine();

                                // Write rows
                                for (int i = 0; i < DAQDataGridView.Rows.Count; i++)
                                {
                                    for (int j = 0; j < DAQDataGridView.Columns.Count; j++)
                                    {
                                        writer.Write(DAQDataGridView.Rows[i].Cells[j].Value?.ToString());
                                        if (j < DAQDataGridView.Columns.Count - 1)
                                        {
                                            writer.Write(",");
                                        }
                                    }
                                    writer.WriteLine();
                                }
                            }
                            //dataListBox.Items.Add("Results saved.");
                        }
                        else
                        {
                            // dataListBox.Items.Add("Save canceled.");
                        }

                    })));
                }
            }

            //EHandle Connection event to populate combox with available devices after scan for devies button has been pressed
            if (e.PropertyName == "Connection.AvailableDevices")
            {
                AvailableDevicesComboBox.DataSource = MTAengine.Connection.AvailableDevices;
            }
            //Check if the stage position has changed and update the label

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
            if (e.PropertyName == nameof(Engine.ThresholdMet))
            {


                if (MTAengine.ThresholdMet)
                {
                    //MTAengine.StopMotionController();
                    //MessageBox.Show("Threshold Met");
                }
            }

        }

        private async void ScanAvailableMotionControllerDevicesButton_Click(object sender, EventArgs e)
        {
            ScanAvailableMotionControllerDevicesButton.Enabled = false;
            MTAengine.GetAvailableDevices();
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
                    return;
                }
                var prt = short.Parse(selection.Substring(selection.Length - 1));
                MTAengine.ConnectToMotionController(prt);
            }
            ConnectToMotionControllerButton.Enabled = true;
        }

        private async void SendYToHomeButton_Click(object sender, EventArgs e)
        {
            //Check if the motion controller is succesffuly connected; if it is issue the command to send the Y axis to the home position
            SendYToHomeButton.Enabled = false;
            if (MTAengine.Stage.ConnectionStatus)
            {
                //Make this a task to run a sepearte thread to leave ui response
                await Task.Run(() => MTAengine.HomeYStage());

            }
            SendYToHomeButton.Enabled = true;
        }

        private async void StopMotionControllerButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.Stage.ConnectionStatus)
            {
                await Task.Run(() => MTAengine.StopMotionController());
            }
        }

        private void StepperMotorAngle09RadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (StepperMotorAngle09RadioButton.Checked)
            {
                if (MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetMotorDegree(0.9));
                }
            }
        }

        private void StepperMotorAngle18RadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (StepperMotorAngle18RadioButton.Checked)
            {
                if (MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetMotorDegree(1.8));
                }
            }
        }

        private void LeadScrewPitch05mmRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (LeadScrewPitch05mmRadioButton.Checked)
            {
                if (MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetScrewLeadPitch(0.5));
                }
            }
        }

        private void LeadScrewPitch1mmRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (LeadScrewPitch1mmRadioButton.Checked)
            {
                if (MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetScrewLeadPitch(1));
                }
            }
        }

        private void LeadScrewPitch2mmRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (LeadScrewPitch2mmRadioButton.Checked)
            {
                if (MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetScrewLeadPitch(2));
                }
            }
        }

        private void LeadScrewPitch25mmRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (LeadScrewPitch25mmRadioButton.Checked)
            {
                if (MTAengine.Stage.ConnectionStatus)
                {
                    Task.Run(() => MTAengine.SetScrewLeadPitch(2.5));
                }
            }
        }

        private void CalculatePulseEquivalentButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (MTAengine.Stage != null)
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
                if (MTAengine.Stage.ConnectionStatus)
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
            if (MTAengine.Stage != null)
            {
                if (short.TryParse(MotionControllerSpeedTextBox.Text, out short outdata))
                {
                    await Task.Run(() => MTAengine.SetStageSpeed(outdata));
                }


            }
        }

        private async void MoveYAxisButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.Stage != null)
            {
                if (double.TryParse(this.YAxisDisplacementTextBox.Text, out double distance))
                {
                    await Task.Run(() => MTAengine.TranslateYStage(distance));
                }
            }
        }



        private void DAQStopMonitoringButton_Click(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    if (MTAengine.IsStageRunning)
                    {
                        MTAengine.StopMotionController();
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
            await Task.Run(() => MTAengine.StopAsync());
            MonitorResponseChart.Series.Clear();
            this.relativeStartTime = -1;
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            DAQDataGridView.Rows.Clear();
            MTAengine.SetStageSpeed(0);
            //double.TryParse(CollectionTimeSecondsTextBox.Text, out double result);
            //if (result == 0)
            //{
            //    MessageBox.Show("Please enter a valid collection time");
            //    return;
            //}
            MTAengine.DataCollectionTime = 600;
            if (double.TryParse(PlaneDetectionThresholdTextBox.Text, out var planeThresh))
            {
                MTAengine.FindPlaneThreshold = planeThresh + voltageOffset;
                MTAengine.TranslateYStage(-1000);
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
            MTAengine.SetStageSpeed(100);
            Thread.Sleep(100);
            MTAengine.TranslateYStage(10);
        }

        private void backgroundWorkerStartButton_Click(object sender, EventArgs e)
        {
            MonitorResponseChart.Series.Clear();
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            DAQDataGridView.Rows.Clear();
            MTAengine.StartMonitor();

        }

        private void stopBackgroundWorkerButton_Click(object sender, EventArgs e)
        {
            Task.Run(() => MTAengine.StopAsync());
        }

        private async void FractureTestStartButton_Click(object sender, EventArgs e)
        {
            await Task.Run(() => MTAengine.StopAsync());
            // Thread.Sleep(10);
            //await Task.Run(() => MTAengine.Stage.Stop());
            //Thread.Sleep(10);
            MonitorResponseChart.Series.Clear();
            this.relativeStartTime = -1;
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            MTAengine.SetStageSpeed(0);
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
                MTAengine.FractureDistance = depth;
                DialogResult dresult = MessageBox.Show("Current Fracture Depth:" + MTAengine.FractureDistance);
                //MTAengine.SetStageSpeed(1);
                MTAengine.TranslateYStage(MTAengine.FractureDistance);
                MTAengine.FractureTest();
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
                this.PunctureTestStartButton.Enabled = false;
                this.FractureTestStartButton.Enabled = true;
                MTAengine.VoltageConversion = this.MTAengine.FractureVoltageConversion;
                MTAengine.NewtonConversion = this.MTAengine.FractureNewtonConversion;
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                if (double.TryParse(PunctureMaxDepthTextBox.Text, out double thresh))
                {
                    //MTAengine.PunctureThreshold = thresh;
                }
                else
                {
                    // MTAengine.PunctureThreshold = 0.05;
                }
                this.PunctureTestStartButton.Enabled = false;
                this.FractureTestStartButton.Enabled = true;
                MTAengine.VoltageConversion = this.MTAengine.PunctureVoltageConversion;
                MTAengine.NewtonConversion = this.MTAengine.PunctureNewtonConversion;
            }
        }

        private async void PunctureTestStartButton_Click(object sender, EventArgs e)
        {

            await Task.Run(() => MTAengine.StopAsync());
            await Task.Run(() => MTAengine.StopMotionController());
            await Task.Run(() => MTAengine.GetYLocation());
            MonitorResponseChart.Series.Clear();
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            if (double.TryParse(PunctureMaxDepthTextBox.Text, out double depth))
            {

                MTAengine.PunctureDistance = depth;
                MonitorResponseChart.Series.Add(series);
                DAQDataGridView.Rows.Clear();
                MTAengine.SetStageSpeed(1);
                this._voltageConversion = MTAengine.PunctureVoltageConversion;
                this._newtonConversion = MTAengine.PunctureNewtonConversion;
                Thread.Sleep(10);

                MTAengine.PunctureTest();
            }
            else
            {
                MessageBox.Show("Please enter a valid depth value");
            }
        }

        private void SetPunctureOffsetButton_Click(object sender, EventArgs e)
        {
            //Take the average of the first 1000 data points in the voltage column and set that as the offset
            double offset = 0;
            for (int i = 0; i < DAQDataGridView.RowCount; i++)
            {
                offset += Convert.ToDouble(DAQDataGridView.Rows[i].Cells[1].Value);
            }
            offset = offset / DAQDataGridView.RowCount;
            MTAengine.VoltageOffset = offset;
        }

        private void MicroTextureAnalyzerTabPage_Click(object sender, EventArgs e)
        {

        }

        private async void button1_Click(object sender, EventArgs e)
        {
            await Task.Run(() => MTAengine.StopAsync());
            Thread.Sleep(10);
            //await Task.Run(() => MTAengine.Stage.Stop());
            MonitorResponseChart.Series.Clear();
            this.relativeStartTime = -1;
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            MTAengine.SetStageSpeed(1);
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
                MTAengine.Rate = 1000;
            }
        }

        private void TwoThousandHertzRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (TwoThousandHertzRadioButton.Checked)
            {
                MTAengine.Rate = 1500;
            }
        }

        private void radioButton6_CheckedChanged(object sender, EventArgs e)
        {
            if (ThreeThousandHertzRadioButton.Checked)
            {
                MTAengine.Rate = 2999;
            }
        }

        private void saveFileButton_Click(object sender, EventArgs e)
        {
            PromptUserToSave();
        }

        private void zero_voltage_button_Click(object sender, EventArgs e)
        {
            if (MonitorResponseChart.Series[0].Points.Count > 0)
            {
                voltageOffset = MonitorResponseChart.Series[0].Points.Average(point => point.YValues[0]);
            }
        }

        private void clear_zero_button_Click(object sender, EventArgs e)
        {
            voltageOffset = 0.0;
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
    }
}

