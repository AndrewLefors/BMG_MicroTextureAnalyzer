using BMG_MicroTextureAnalyzer;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Timers;
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



        }
        //Convert this to event driven so that the data is updated when the event is thrown

        private void MTAengine_DataChanged(object? sender, Engine.ProcessedDataChangedEventArgs e)
        {
            //Add to dataGrid
            var pounds = ((e.Voltage - MTAengine.VoltageOffset) * this._voltageConversion);
            var newtons = pounds * this._newtonConversion;

            //Update labels in real time

            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {

                    DAQMonitoringStatusResponseLabel.Text = MTAengine.Stage.CurrentYStep.ToString();
                    NewtonsResponseLabel.Text = newtons.ToString();
                    //Check if there is a time value in stored in the first part of the monitorresponsechart, if there is use that as a the start for the relative time
                    if (MonitorResponseChart.Series[0].Points.Count > 0)
                    {
                        var time = e.TimeStamp - this.relativeStartTime;

                        DAQDataGridView.Rows.Add(time, e.Voltage - MTAengine.VoltageOffset, pounds, newtons, MTAengine.Stage.CurrentYStep);
                        MonitorResponseChart.Series[0].Points.AddXY(time, newtons);

                    }
                    else
                    {
                        this.relativeStartTime = e.TimeStamp;
                        //Make the MonitorResponseChart a linechart 

                        MonitorResponseChart.Series.Clear();
                        Series series = new Series
                        {
                            ChartType = SeriesChartType.Line
                        };
                        MonitorResponseChart.Series.Add(series);
                        MonitorResponseChart.Series[0].Points.AddXY(0, newtons);
                        DAQDataGridView.Rows.Add(0, e.Voltage - MTAengine.VoltageOffset, pounds, newtons, e.Step);
                    }

                }));

            }
            else
            {

                if (MonitorResponseChart.Series[0].Points.Count > 0)
                {
                    var time = e.TimeStamp - this.relativeStartTime;
                    DAQDataGridView.Rows.Add(time, e.Voltage - MTAengine.VoltageOffset, pounds, newtons);
                    MonitorResponseChart.Series[0].Points.AddXY(time, newtons);
                }
                else
                {
                    this.relativeStartTime = e.TimeStamp;
                    MonitorResponseChart.Series[0].Points.AddXY(0, newtons);
                    DAQDataGridView.Rows.Add(0, e.Voltage - MTAengine.VoltageOffset, pounds, newtons);
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
            //Handle event when connection is successful to change connection status label text to connect in green text
            //Why is this not working? 
            if (e.PropertyName == "MotionController.ConnectionStatus")
            {
                ConnectionStatusLabel.Text = MTAengine.Stage.ConnectionStatus ? "Connected" : "Not Connected";
                ConnectionStatusLabel.ForeColor = MTAengine.Stage.ConnectionStatus ? Color.Green : Color.Red;
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
                    Task.Run(() => MTAengine.CalculatePulseEquivalent());
                }
            }
            catch (Exception ex)
            {
                //TODO: Add a log label or text box for visual log of errors during operation
                MessageBox.Show(ex.Message);
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

                    MTAengine.StopMotionController();
                    MTAengine.StopAsync();
                }));

            }
            else
            {
                MTAengine.StopMotionController();
                MTAengine.StopAsync();
            }

        }

        private async void StartConstantMonitorButton_Click(object sender, EventArgs e)
        {
            //MonitorResponseChart.Series.Clear();
            //Series series = new Series
            //{
            //    ChartType = SeriesChartType.Line
            //};
            //MonitorResponseChart.Series.Add(series);
            //DAQDataGridView.Rows.Clear();
            //stopwatch = new Stopwatch();
            //stopwatch.Start();
            //MonitorTimer.Enabled = true;
            //MonitorTimer.Start();
            //MTAengine.SetStageSpeed(5);
            //Thread.Sleep(10);
            //MonitorTimer.Interval = 1;

            //MTAengine.TranslateYStage(-100);

            await Task.Run(() => MTAengine.StopAsync());
            MonitorResponseChart.Series.Clear();
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);
            DAQDataGridView.Rows.Clear();
            MTAengine.SetStageSpeed(1);
            if (double.TryParse(PlaneDetectionThresholdTextBox.Text, out var planeThresh))
            {
                MTAengine.FindPlaneThreshold = planeThresh;
                Thread.Sleep(10);
                MTAengine.FindPlane();
            }
            else
            {
                MessageBox.Show("Please enter a valid threshold value");
            }



        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            MccDaq.ErrorInfo ULStat = this.board.AIn32(7, MccDaq.Range.BipPt078Volts, out monitorData, 1);
            if (ULStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
            {
                Task.Run(() => MessageBox.Show("Error reading data: " + ULStat.Message));
                MTAengine.StopMotionController();
                return;
            }
            ULStat = this.board.ToEngUnits32(MccDaq.Range.BipPt078Volts, monitorData, out monitorVoltage);
            if (ULStat.Value != MccDaq.ErrorInfo.ErrorCode.NoErrors)
            {
                Task.Run(() => MessageBox.Show("Error converting data: " + ULStat.Message));
                MTAengine.StopMotionController();
                return;
            }
            //Convert the voltage to pounds using factor of 2141.878 lbs/volt
            monitorPounds = monitorVoltage * 2141.878;
            DAQMonitoringStatusResponseLabel.Text = monitorVoltage.ToString();
            DAQDataResponseLabel.Text = monitorPounds.ToString();
            monitorNewtons = monitorPounds * 4.44822;
            NewtonsResponseLabel.Text = monitorNewtons.ToString();



            //Add the value to the datagrid with another colum for the time and another colum for the distance
            //First add coluns to datagrid


            //I want to have a seperate thread run the updatechart, but make sure it's the proper thread using invoke
            //this.
            Task.Run(() =>
            {
                //DAQDataGridView.Rows.Add(stopwatch.Elapsed.TotalSeconds, monitorVoltage, monitorPounds, monitorNewtons);
                UpdatePortionChartData(stopwatch.Elapsed.TotalSeconds, monitorNewtons);
            });


            //export datagrid to a file csv format
            //Will do this in a sepearte button or file-dialouge
            //Thread.Sleep(100);
            //Check if Threshold has been reached
            //How can I completely stop these insteado f just throwing a message box

            if (monitorNewtons > newtonThreshold)
            {
                //MessageBox.Show("Threshold reached");
                MTAengine.StopMotionController();

                //Take the data from the chart and add it to the datagrid
                Task.Run(() => Invoke((MethodInvoker)(delegate
                {
                    foreach (DataPoint point in MonitorResponseChart.Series[0].Points)
                    {
                        DAQDataGridView.Rows.Add(point.XValue, point.YValues[0], point.YValues[0] * 21, point.YValues[0] * 4.44822);
                    }
                })));
                // UpdateChartData();
                return;
            }

        }

        private void UpdateChartData()
        {
            MonitorResponseChart.Series.Clear();
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            MonitorResponseChart.Series.Add(series);

            foreach (DataGridViewRow row in DAQDataGridView.Rows)
            {
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    //Convert the datetime to relative time in seconds

                    double xValue = Convert.ToDouble(row.Cells[0].Value);
                    double yValue = Convert.ToDouble(row.Cells[3].Value);
                    series.Points.AddXY(xValue, yValue);
                }
            }
        }

        //Make updateportionchartdata function that takes the new time value, new voltage, new pound and new newton values and addits it to the chart
        //This will be called in the monitor timer tick event
        private void UpdatePortionChartData(double time, double newtons)
        {

            MonitorResponseChart.Series[0].Points.AddXY(time, newtons);
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
            MTAengine.StopAsync();
        }

        private async void FractureTestStartButton_Click(object sender, EventArgs e)
        {
            await Task.Run(() => MTAengine.StopAsync());
            await Task.Run(() => MTAengine.StopMotionController());
            await Task.Run(() => MTAengine.GetYLocation());
            MonitorResponseChart.Series.Clear();
            Series series = new Series
            {
                ChartType = SeriesChartType.Line
            };
            if (double.TryParse(FractureDepthTextBox.Text, out double depth))
            {

                MTAengine.FractureDistance = depth;
                MonitorResponseChart.Series.Add(series);
                DAQDataGridView.Rows.Clear();
                MTAengine.SetStageSpeed(1);
                MTAengine.VoltageConversion = MTAengine.FractureVoltageConversion;
                MTAengine.NewtonConversion = MTAengine.FractureNewtonConversion;
                Thread.Sleep(10);

                MTAengine.FractureTest();
            }
            else
            {
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
                    MTAengine.PunctureThreshold = thresh;
                }
                else
                {
                    MTAengine.PunctureThreshold = 0.05;
                }
                this.PunctureTestStartButton.Enabled = true;
                this.FractureTestStartButton.Enabled = false;
                MTAengine.VoltageConversion = MTAengine.PunctureVoltageConversion;
                MTAengine.NewtonConversion = MTAengine.PunctureNewtonConversion;
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
                MTAengine.SetStageSpeed(0);
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

        private void button1_Click(object sender, EventArgs e)
        {
            MTAengine.ContinuousScanTest();
        }
    }
}
