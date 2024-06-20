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
        public Double newtonThreshold = 1;


        public Form1()
        {
            BMG_MicroTextureAnalyzer.Engine engine = new BMG_MicroTextureAnalyzer.Engine();
            board = new MccDaq.MccBoard(1);

            MTAengine = engine;
            MTAengine.PropertyChanged += MTAengine_PropertyChanged;
            var subdivisionList = new List<int> { 1, 2, 4, 8 };



            InitializeComponent();
            this.MotionControllerSubdivisionComboBox.DataSource = subdivisionList;
            this.MotionControllerSubdivisionComboBox.SelectedIndex = 0;
            MonitorTimer.Tick += MonitorTimer_Tick;
            DAQDataGridView.Columns.Add("Time", "Time");
            DAQDataGridView.Columns.Add("Voltage", "Voltage");
            DAQDataGridView.Columns.Add("Pounds", "Pounds");
            DAQDataGridView.Columns.Add("Newtons", "Newtons");

        }
        //Convert this to event driven so that the data is updated when the event is thrown

        private void MTAengine_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
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
                MotionControllerStatusResponseLabel.Text = MTAengine.Stage.Busy ? "Busy" : "Not Busy";
                MotionControllerStatusResponseLabel.ForeColor = MTAengine.Stage.Busy ? Color.Red : Color.Green;
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
               if(short.TryParse(MotionControllerSpeedTextBox.Text, out short outdata))
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
            MTAengine.StopMotionController();
            MonitorTimer.Stop();
        }

        private async void StartConstantMonitorButton_Click(object sender, EventArgs e)
        {
            MonitorResponseChart.Series.Clear();
            DAQDataGridView.Rows.Clear();
            stopwatch = new Stopwatch();
            stopwatch.Start();
            MonitorTimer.Enabled = true;
            MonitorTimer.Start();
            MTAengine.SetStageSpeed(5);
            Thread.Sleep(1000);
            MonitorTimer.Interval = 1;
            
            MTAengine.TranslateYStage(-100);

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

                DAQDataGridView.Rows.Add(stopwatch.Elapsed.TotalSeconds, monitorVoltage, monitorPounds, monitorNewtons);
                UpdateChartData();
                
            
            //export datagrid to a file csv format
            //Will do this in a sepearte button or file-dialouge
            //Thread.Sleep(100);
            //Check if Threshold has been reached
            //How can I completely stop these insteado f just throwing a message box

            if (monitorNewtons > newtonThreshold)
            {
                //MessageBox.Show("Threshold reached");
                MTAengine.StopMotionController();
                MonitorTimer.Stop();
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

        private void ReturnProbeToMaxHeightButton_Click(object sender, EventArgs e)
        {
            MTAengine.SetStageSpeed(100);
            Thread.Sleep(100);
            MTAengine.TranslateYStage(100);
        }
    }
}
