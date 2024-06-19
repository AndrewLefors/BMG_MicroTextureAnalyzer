namespace BMG_MicroTextureAnalyzer_GUI
{
    using BMG_MicroTextureAnalyzer;
    public partial class Form1 : Form
    {
        public Engine MTAengine;
        public Form1()
        {
            BMG_MicroTextureAnalyzer.Engine engine = new BMG_MicroTextureAnalyzer.Engine();
            MTAengine = engine;
            MTAengine.PropertyChanged += MTAengine_PropertyChanged;
            var subdivisionList = new List<int> { 1, 2, 4, 8 };


            InitializeComponent();
            this.MotionControllerSubdivisionComboBox.DataSource = subdivisionList;
            this.MotionControllerSubdivisionComboBox.SelectedIndex = 0;
        }

        private void MTAengine_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //EHandle Connection event to populate combox with available devices after scan for devies button has been pressed
            if (e.PropertyName == "Connection.AvailableDevices")
            {
                AvailableDevicesComboBox.DataSource = MTAengine.Connection.AvailableDevices;
            }
            //Handle event when connection is successful to change connection status label text to connect in green text
            //Why is this not working? 
            if (e.PropertyName == "MotionController.ReadCom")
            {
                ConnectionStatusResponseLabel.Text = MTAengine.Stage.ConnectionStatus ? "Connected" : "Not Connected";
                ConnectionStatusResponseLabel.ForeColor = MTAengine.Stage.ConnectionStatus ? Color.Green : Color.Red;
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
            //FTODO: Fix this to properly respond to the events thrown by the DataChanged event handler from DAQ
            if (e.PropertyName == "DAQ.ReadAnalogInput")
            {
                DAQDataResponseLabel.Text = MTAengine.DataDev.ReadAnalogInput(0, MccDaq.Range.BipPt005Volts).ToString();
            }
            if (e.PropertyName == "DAQ.PropertyChanged")
            {
                DAQMonitoringStatusResponseLabel.Text = MTAengine.DataDev.IsMonitoring ? "Monitoring" : "Not Monitoring";
                DAQMonitoringStatusResponseLabel.ForeColor = MTAengine.DataDev.IsMonitoring ? Color.Green : Color.Red;
            }
        }

        private void ScanAvailableMotionControllerDevicesButton_Click(object sender, EventArgs e)
        {
            MTAengine.GetAvailableDevices();
            //AvailableDevicesComboBox.DataSource = MTAengine.Connection.AvailableDevices;

        }

        private void ConnectToMotionControllerButton_Click(object sender, EventArgs e)
        {
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
        }

        private async void SendYToHomeButton_Click(object sender, EventArgs e)
        {
            //Check if the motion controller is succesffuly connected; if it is issue the command to send the Y axis to the home position
            if (MTAengine.Stage.ConnectionStatus)
            {
                //Make this a task to run a sepearte thread to leave ui response
                Task.Run(() => MTAengine.HomeYStage());

            }
        }

        private void StopMotionControllerButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.Stage.ConnectionStatus)
            {
                Task.Run(() => MTAengine.StopMotionController());
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
                if (short.TryParse(this.MotionControllerSpeedTextBox.Text, out short sspeed))
                {
                    Task.Run(() => MTAengine.SetStageSpeed(sspeed));
                }

            }
        }

        private async void MoveYAxisButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.Stage != null)
            {
                if (MTAengine.Stage.Busy)
                {
                    MessageBox.Show("The stage is currently busy, please wait for the current operation to complete");
                    return;
                }
                if (double.TryParse(this.YAxisDisplacementTextBox.Text, out double distance))
                {
                    await Task.Run(() => MTAengine.TranslateYStage(distance));
                }
            }
        }

        private void DAQStartMonitoringButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.DataDev != null)
            {
                if (MTAengine.DataDev.IsMonitoring)
                {
                    MessageBox.Show("Monitoring is already in progress.");
                    return;
                }
                MTAengine.StartMonitoring(7, MccDaq.Range.BipPt005Volts, 100);

            }
        }

        private void DAQStopMonitoringButton_Click(object sender, EventArgs e)
        {
            if (MTAengine.DataDev != null)
            {
                if (!MTAengine.DataDev.IsMonitoring)
                {
                    MessageBox.Show("Monitoring is not in progress.");
                    return;
                }
                MTAengine.StopMonitoring();
            }
        }
    }
}
