namespace BMG_MicroTextureAnalyzer_GUI
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            MMELogoPictureBox = new PictureBox();
            tabControl1 = new TabControl();
            MicroTextureAnalyzerTabPage = new TabPage();
            MotionControllerTabPage = new TabPage();
            tableLayoutPanel6 = new TableLayoutPanel();
            YPositionLabel = new Label();
            YStepLabel = new Label();
            YPositionResponseLabel = new Label();
            YStepResponseLabel = new Label();
            tableLayoutPanel7 = new TableLayoutPanel();
            YHomeResponseLabel = new Label();
            SetYHomeButton = new Button();
            YHomeLabel = new Label();
            StepperMotorValuesGroupBox = new GroupBox();
            YAxisPropertiesGroupBox = new GroupBox();
            MotionControllerStatusResponseLabel = new Label();
            StopMotionControllerButton = new Button();
            SendYToHomeButton = new Button();
            StepperMotorSettingsGroupBox = new GroupBox();
            tableLayoutPanel2 = new TableLayoutPanel();
            StepperMotorAngleLabel = new Label();
            LeadScrewPitchLabel = new Label();
            StepperMotorSubdivisionLabel = new Label();
            PulseEquivalentLabel = new Label();
            tableLayoutPanel3 = new TableLayoutPanel();
            StepperMotorAngle18RadioButton = new RadioButton();
            StepperMotorAngle09RadioButton = new RadioButton();
            tableLayoutPanel4 = new TableLayoutPanel();
            LeadScrewPitch05mmRadioButton = new RadioButton();
            LeadScrewPitch1mmRadioButton = new RadioButton();
            LeadScrewPitch2mmRadioButton = new RadioButton();
            LeadScrewPitch25mmRadioButton = new RadioButton();
            MotionControllerSubdivisionComboBox = new ComboBox();
            tableLayoutPanel5 = new TableLayoutPanel();
            PulseEquivalentResponseLabel = new Label();
            CalculatePulseEquivalentButton = new Button();
            MotionControllerSettingsGroupBox = new GroupBox();
            tableLayoutPanel1 = new TableLayoutPanel();
            XAxisDisplacementLabel = new Label();
            YAxisDisplacementLabel = new Label();
            MotionControllerSpeedLabel = new Label();
            XAxisDisplacementTextBox = new TextBox();
            YAxisDisplacementTextBox = new TextBox();
            MotionControllerSpeedTextBox = new TextBox();
            MoveXAxisButton = new Button();
            MoveYAxisButton = new Button();
            ChangeMotionControllerSpeedButton = new Button();
            MotionControllerConnectionSettingsGroupBox = new GroupBox();
            MotionControllerConnectionTableLayout = new TableLayoutPanel();
            AvailableDevicesComboBox = new ComboBox();
            ScanAvailableMotionControllerDevicesButton = new Button();
            ConnectToMotionControllerButton = new Button();
            DisconnectMotionControllerButton = new Button();
            AvailableDevicesLabel = new Label();
            ConnectionStatusLabel = new Label();
            ConnectionStatusResponseLabel = new Label();
            DaqDeviceTabPage = new TabPage();
            zero_voltage_button = new Button();
            button2 = new Button();
            tableLayoutPanel12 = new TableLayoutPanel();
            PollingRateLabel = new Label();
            CollectionTimeLabel = new Label();
            tableLayoutPanel13 = new TableLayoutPanel();
            ThousandHertzRadioButton = new RadioButton();
            TwoThousandHertzRadioButton = new RadioButton();
            ThreeThousandHertzRadioButton = new RadioButton();
            CollectionTimeSecondsTextBox = new TextBox();
            button1 = new Button();
            label5 = new Label();
            tableLayoutPanel11 = new TableLayoutPanel();
            label4 = new Label();
            PlaneDetectionThresholdTextBox = new TextBox();
            tableLayoutPanel10 = new TableLayoutPanel();
            radioButton1 = new RadioButton();
            radioButton2 = new RadioButton();
            backgroundWorkerStartButton = new Button();
            SetPunctureOffsetButton = new Button();
            tableLayoutPanel9 = new TableLayoutPanel();
            FractureTestStartButton = new Button();
            PunctureTestStartButton = new Button();
            FractureDepthTextBox = new TextBox();
            PunctureMaxDepthTextBox = new TextBox();
            tableLayoutPanel8 = new TableLayoutPanel();
            label3 = new Label();
            DAQMonitoringStatusResponseLabel = new Label();
            StartConstantMonitorButton = new Button();
            label1 = new Label();
            DAQStopMonitoringButton = new Button();
            NewtonsResponseLabel = new Label();
            label2 = new Label();
            DAQDataResponseLabel = new Label();
            ReturnProbeToMaxHeightButton = new Button();
            MonitorResponseChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            DAQDataGridView = new DataGridView();
            clear_zero_button = new Button();
            ((System.ComponentModel.ISupportInitialize)MMELogoPictureBox).BeginInit();
            tabControl1.SuspendLayout();
            MotionControllerTabPage.SuspendLayout();
            tableLayoutPanel6.SuspendLayout();
            tableLayoutPanel7.SuspendLayout();
            StepperMotorValuesGroupBox.SuspendLayout();
            YAxisPropertiesGroupBox.SuspendLayout();
            StepperMotorSettingsGroupBox.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            tableLayoutPanel3.SuspendLayout();
            tableLayoutPanel4.SuspendLayout();
            tableLayoutPanel5.SuspendLayout();
            MotionControllerSettingsGroupBox.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            MotionControllerConnectionSettingsGroupBox.SuspendLayout();
            MotionControllerConnectionTableLayout.SuspendLayout();
            DaqDeviceTabPage.SuspendLayout();
            tableLayoutPanel12.SuspendLayout();
            tableLayoutPanel13.SuspendLayout();
            tableLayoutPanel11.SuspendLayout();
            tableLayoutPanel10.SuspendLayout();
            tableLayoutPanel9.SuspendLayout();
            tableLayoutPanel8.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)MonitorResponseChart).BeginInit();
            ((System.ComponentModel.ISupportInitialize)DAQDataGridView).BeginInit();
            SuspendLayout();
            // 
            // MMELogoPictureBox
            // 
            MMELogoPictureBox.Location = new Point(0, -1);
            MMELogoPictureBox.Name = "MMELogoPictureBox";
            MMELogoPictureBox.Size = new Size(1119, 68);
            MMELogoPictureBox.TabIndex = 0;
            MMELogoPictureBox.TabStop = false;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(MicroTextureAnalyzerTabPage);
            tabControl1.Controls.Add(MotionControllerTabPage);
            tabControl1.Controls.Add(DaqDeviceTabPage);
            tabControl1.Location = new Point(0, 73);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1119, 580);
            tabControl1.TabIndex = 1;
            // 
            // MicroTextureAnalyzerTabPage
            // 
            MicroTextureAnalyzerTabPage.Location = new Point(4, 24);
            MicroTextureAnalyzerTabPage.Name = "MicroTextureAnalyzerTabPage";
            MicroTextureAnalyzerTabPage.Padding = new Padding(3);
            MicroTextureAnalyzerTabPage.Size = new Size(1111, 552);
            MicroTextureAnalyzerTabPage.TabIndex = 2;
            MicroTextureAnalyzerTabPage.Text = "MicroTexture Analyzer";
            MicroTextureAnalyzerTabPage.UseVisualStyleBackColor = true;
            MicroTextureAnalyzerTabPage.Click += MicroTextureAnalyzerTabPage_Click;
            // 
            // MotionControllerTabPage
            // 
            MotionControllerTabPage.Controls.Add(tableLayoutPanel6);
            MotionControllerTabPage.Controls.Add(StepperMotorValuesGroupBox);
            MotionControllerTabPage.Controls.Add(StepperMotorSettingsGroupBox);
            MotionControllerTabPage.Controls.Add(MotionControllerSettingsGroupBox);
            MotionControllerTabPage.Controls.Add(MotionControllerConnectionSettingsGroupBox);
            MotionControllerTabPage.Location = new Point(4, 24);
            MotionControllerTabPage.Name = "MotionControllerTabPage";
            MotionControllerTabPage.Padding = new Padding(3);
            MotionControllerTabPage.Size = new Size(1111, 552);
            MotionControllerTabPage.TabIndex = 0;
            MotionControllerTabPage.Text = "Motion Controller";
            MotionControllerTabPage.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanel6
            // 
            tableLayoutPanel6.ColumnCount = 2;
            tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34.0694F));
            tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65.9306F));
            tableLayoutPanel6.Controls.Add(YPositionLabel, 0, 0);
            tableLayoutPanel6.Controls.Add(YStepLabel, 0, 1);
            tableLayoutPanel6.Controls.Add(YPositionResponseLabel, 1, 0);
            tableLayoutPanel6.Controls.Add(YStepResponseLabel, 1, 1);
            tableLayoutPanel6.Controls.Add(tableLayoutPanel7, 1, 2);
            tableLayoutPanel6.Controls.Add(YHomeLabel, 0, 2);
            tableLayoutPanel6.Location = new Point(389, 28);
            tableLayoutPanel6.Name = "tableLayoutPanel6";
            tableLayoutPanel6.RowCount = 3;
            tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Absolute, 43F));
            tableLayoutPanel6.Size = new Size(317, 155);
            tableLayoutPanel6.TabIndex = 0;
            // 
            // YPositionLabel
            // 
            YPositionLabel.Anchor = AnchorStyles.None;
            YPositionLabel.AutoSize = true;
            YPositionLabel.Location = new Point(21, 20);
            YPositionLabel.Name = "YPositionLabel";
            YPositionLabel.Size = new Size(65, 15);
            YPositionLabel.TabIndex = 0;
            YPositionLabel.Text = "Y-Position:";
            // 
            // YStepLabel
            // 
            YStepLabel.Anchor = AnchorStyles.None;
            YStepLabel.AutoSize = true;
            YStepLabel.Location = new Point(31, 76);
            YStepLabel.Name = "YStepLabel";
            YStepLabel.Size = new Size(45, 15);
            YStepLabel.TabIndex = 1;
            YStepLabel.Text = "Y-Step:";
            // 
            // YPositionResponseLabel
            // 
            YPositionResponseLabel.AutoSize = true;
            YPositionResponseLabel.Location = new Point(111, 0);
            YPositionResponseLabel.Name = "YPositionResponseLabel";
            YPositionResponseLabel.Size = new Size(0, 15);
            YPositionResponseLabel.TabIndex = 2;
            // 
            // YStepResponseLabel
            // 
            YStepResponseLabel.Anchor = AnchorStyles.None;
            YStepResponseLabel.AutoSize = true;
            YStepResponseLabel.Location = new Point(212, 76);
            YStepResponseLabel.Name = "YStepResponseLabel";
            YStepResponseLabel.Size = new Size(0, 15);
            YStepResponseLabel.TabIndex = 3;
            // 
            // tableLayoutPanel7
            // 
            tableLayoutPanel7.ColumnCount = 2;
            tableLayoutPanel7.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel7.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel7.Controls.Add(YHomeResponseLabel, 0, 0);
            tableLayoutPanel7.Controls.Add(SetYHomeButton, 1, 0);
            tableLayoutPanel7.Location = new Point(111, 115);
            tableLayoutPanel7.Name = "tableLayoutPanel7";
            tableLayoutPanel7.RowCount = 1;
            tableLayoutPanel7.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel7.Size = new Size(200, 37);
            tableLayoutPanel7.TabIndex = 5;
            // 
            // YHomeResponseLabel
            // 
            YHomeResponseLabel.Anchor = AnchorStyles.None;
            YHomeResponseLabel.AutoSize = true;
            YHomeResponseLabel.Location = new Point(50, 11);
            YHomeResponseLabel.Name = "YHomeResponseLabel";
            YHomeResponseLabel.Size = new Size(0, 15);
            YHomeResponseLabel.TabIndex = 5;
            // 
            // SetYHomeButton
            // 
            SetYHomeButton.Location = new Point(103, 3);
            SetYHomeButton.Name = "SetYHomeButton";
            SetYHomeButton.Size = new Size(94, 31);
            SetYHomeButton.TabIndex = 6;
            SetYHomeButton.Text = "Set Y-Home";
            SetYHomeButton.UseVisualStyleBackColor = true;
            // 
            // YHomeLabel
            // 
            YHomeLabel.Anchor = AnchorStyles.None;
            YHomeLabel.AutoSize = true;
            YHomeLabel.Location = new Point(26, 126);
            YHomeLabel.Name = "YHomeLabel";
            YHomeLabel.Size = new Size(55, 15);
            YHomeLabel.TabIndex = 4;
            YHomeLabel.Text = "Y-Home:";
            // 
            // StepperMotorValuesGroupBox
            // 
            StepperMotorValuesGroupBox.Controls.Add(YAxisPropertiesGroupBox);
            StepperMotorValuesGroupBox.Location = new Point(389, 3);
            StepperMotorValuesGroupBox.Name = "StepperMotorValuesGroupBox";
            StepperMotorValuesGroupBox.Size = new Size(404, 540);
            StepperMotorValuesGroupBox.TabIndex = 4;
            StepperMotorValuesGroupBox.TabStop = false;
            StepperMotorValuesGroupBox.Text = "Motion Controller Values";
            // 
            // YAxisPropertiesGroupBox
            // 
            YAxisPropertiesGroupBox.Controls.Add(MotionControllerStatusResponseLabel);
            YAxisPropertiesGroupBox.Controls.Add(StopMotionControllerButton);
            YAxisPropertiesGroupBox.Controls.Add(SendYToHomeButton);
            YAxisPropertiesGroupBox.Location = new Point(1, 22);
            YAxisPropertiesGroupBox.Name = "YAxisPropertiesGroupBox";
            YAxisPropertiesGroupBox.Size = new Size(316, 235);
            YAxisPropertiesGroupBox.TabIndex = 0;
            YAxisPropertiesGroupBox.TabStop = false;
            YAxisPropertiesGroupBox.Text = "Y-Stage Properties";
            // 
            // MotionControllerStatusResponseLabel
            // 
            MotionControllerStatusResponseLabel.AutoSize = true;
            MotionControllerStatusResponseLabel.Location = new Point(3, 164);
            MotionControllerStatusResponseLabel.Name = "MotionControllerStatusResponseLabel";
            MotionControllerStatusResponseLabel.Size = new Size(38, 15);
            MotionControllerStatusResponseLabel.TabIndex = 3;
            MotionControllerStatusResponseLabel.Text = "label1";
            // 
            // StopMotionControllerButton
            // 
            StopMotionControllerButton.Location = new Point(234, 195);
            StopMotionControllerButton.Name = "StopMotionControllerButton";
            StopMotionControllerButton.Size = new Size(75, 23);
            StopMotionControllerButton.TabIndex = 2;
            StopMotionControllerButton.Text = "Stop";
            StopMotionControllerButton.UseVisualStyleBackColor = true;
            StopMotionControllerButton.Click += StopMotionControllerButton_Click;
            // 
            // SendYToHomeButton
            // 
            SendYToHomeButton.Location = new Point(232, 164);
            SendYToHomeButton.Name = "SendYToHomeButton";
            SendYToHomeButton.Size = new Size(75, 23);
            SendYToHomeButton.TabIndex = 1;
            SendYToHomeButton.Text = "Send Y-Home";
            SendYToHomeButton.UseVisualStyleBackColor = true;
            SendYToHomeButton.Click += SendYToHomeButton_Click;
            // 
            // StepperMotorSettingsGroupBox
            // 
            StepperMotorSettingsGroupBox.Controls.Add(tableLayoutPanel2);
            StepperMotorSettingsGroupBox.Location = new Point(5, 311);
            StepperMotorSettingsGroupBox.Name = "StepperMotorSettingsGroupBox";
            StepperMotorSettingsGroupBox.Size = new Size(378, 232);
            StepperMotorSettingsGroupBox.TabIndex = 3;
            StepperMotorSettingsGroupBox.TabStop = false;
            StepperMotorSettingsGroupBox.Text = "Stepper Motor Settings";
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.ColumnCount = 2;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23.913044F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76.08696F));
            tableLayoutPanel2.Controls.Add(StepperMotorAngleLabel, 0, 0);
            tableLayoutPanel2.Controls.Add(LeadScrewPitchLabel, 0, 1);
            tableLayoutPanel2.Controls.Add(StepperMotorSubdivisionLabel, 0, 2);
            tableLayoutPanel2.Controls.Add(PulseEquivalentLabel, 0, 3);
            tableLayoutPanel2.Controls.Add(tableLayoutPanel3, 1, 0);
            tableLayoutPanel2.Controls.Add(tableLayoutPanel4, 1, 1);
            tableLayoutPanel2.Controls.Add(MotionControllerSubdivisionComboBox, 1, 2);
            tableLayoutPanel2.Controls.Add(tableLayoutPanel5, 1, 3);
            tableLayoutPanel2.Location = new Point(1, 13);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 4;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 42.953022F));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 57.046978F));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            tableLayoutPanel2.Size = new Size(368, 199);
            tableLayoutPanel2.TabIndex = 0;
            // 
            // StepperMotorAngleLabel
            // 
            StepperMotorAngleLabel.Anchor = AnchorStyles.None;
            StepperMotorAngleLabel.AutoSize = true;
            StepperMotorAngleLabel.Location = new Point(5, 9);
            StepperMotorAngleLabel.Name = "StepperMotorAngleLabel";
            StepperMotorAngleLabel.Size = new Size(77, 30);
            StepperMotorAngleLabel.TabIndex = 0;
            StepperMotorAngleLabel.Text = "Stepper Motor Angle:";
            // 
            // LeadScrewPitchLabel
            // 
            LeadScrewPitchLabel.AllowDrop = true;
            LeadScrewPitchLabel.Anchor = AnchorStyles.None;
            LeadScrewPitchLabel.AutoSize = true;
            LeadScrewPitchLabel.Location = new Point(9, 65);
            LeadScrewPitchLabel.Name = "LeadScrewPitchLabel";
            LeadScrewPitchLabel.Size = new Size(69, 30);
            LeadScrewPitchLabel.TabIndex = 1;
            LeadScrewPitchLabel.Text = "Screw Lead (Pitch):";
            // 
            // StepperMotorSubdivisionLabel
            // 
            StepperMotorSubdivisionLabel.Anchor = AnchorStyles.None;
            StepperMotorSubdivisionLabel.AutoSize = true;
            StepperMotorSubdivisionLabel.Location = new Point(8, 123);
            StepperMotorSubdivisionLabel.Name = "StepperMotorSubdivisionLabel";
            StepperMotorSubdivisionLabel.Size = new Size(71, 15);
            StepperMotorSubdivisionLabel.TabIndex = 2;
            StepperMotorSubdivisionLabel.Text = "Subdivision:";
            // 
            // PulseEquivalentLabel
            // 
            PulseEquivalentLabel.Anchor = AnchorStyles.None;
            PulseEquivalentLabel.Location = new Point(6, 158);
            PulseEquivalentLabel.Name = "PulseEquivalentLabel";
            PulseEquivalentLabel.Size = new Size(75, 32);
            PulseEquivalentLabel.TabIndex = 3;
            PulseEquivalentLabel.Text = "Pulse Equivalent:";
            // 
            // tableLayoutPanel3
            // 
            tableLayoutPanel3.Anchor = AnchorStyles.None;
            tableLayoutPanel3.ColumnCount = 2;
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 51.0791359F));
            tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48.9208641F));
            tableLayoutPanel3.Controls.Add(StepperMotorAngle18RadioButton, 0, 0);
            tableLayoutPanel3.Controls.Add(StepperMotorAngle09RadioButton, 1, 0);
            tableLayoutPanel3.Location = new Point(91, 3);
            tableLayoutPanel3.Name = "tableLayoutPanel3";
            tableLayoutPanel3.RowCount = 1;
            tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel3.Size = new Size(274, 42);
            tableLayoutPanel3.TabIndex = 4;
            // 
            // StepperMotorAngle18RadioButton
            // 
            StepperMotorAngle18RadioButton.AutoSize = true;
            StepperMotorAngle18RadioButton.Checked = true;
            StepperMotorAngle18RadioButton.Location = new Point(3, 3);
            StepperMotorAngle18RadioButton.Name = "StepperMotorAngle18RadioButton";
            StepperMotorAngle18RadioButton.Size = new Size(85, 19);
            StepperMotorAngle18RadioButton.TabIndex = 0;
            StepperMotorAngle18RadioButton.TabStop = true;
            StepperMotorAngle18RadioButton.Text = "1.8 Degrees";
            StepperMotorAngle18RadioButton.UseVisualStyleBackColor = true;
            StepperMotorAngle18RadioButton.CheckedChanged += StepperMotorAngle18RadioButton_CheckedChanged;
            // 
            // StepperMotorAngle09RadioButton
            // 
            StepperMotorAngle09RadioButton.AutoSize = true;
            StepperMotorAngle09RadioButton.Location = new Point(142, 3);
            StepperMotorAngle09RadioButton.Name = "StepperMotorAngle09RadioButton";
            StepperMotorAngle09RadioButton.Size = new Size(85, 19);
            StepperMotorAngle09RadioButton.TabIndex = 1;
            StepperMotorAngle09RadioButton.Text = "0.9 Degrees";
            StepperMotorAngle09RadioButton.UseVisualStyleBackColor = true;
            StepperMotorAngle09RadioButton.CheckedChanged += StepperMotorAngle09RadioButton_CheckedChanged;
            // 
            // tableLayoutPanel4
            // 
            tableLayoutPanel4.Anchor = AnchorStyles.None;
            tableLayoutPanel4.ColumnCount = 4;
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66F));
            tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 67F));
            tableLayoutPanel4.Controls.Add(LeadScrewPitch05mmRadioButton, 0, 0);
            tableLayoutPanel4.Controls.Add(LeadScrewPitch1mmRadioButton, 1, 0);
            tableLayoutPanel4.Controls.Add(LeadScrewPitch2mmRadioButton, 2, 0);
            tableLayoutPanel4.Controls.Add(LeadScrewPitch25mmRadioButton, 3, 0);
            tableLayoutPanel4.Location = new Point(91, 58);
            tableLayoutPanel4.Name = "tableLayoutPanel4";
            tableLayoutPanel4.RowCount = 1;
            tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel4.Size = new Size(274, 44);
            tableLayoutPanel4.TabIndex = 5;
            // 
            // LeadScrewPitch05mmRadioButton
            // 
            LeadScrewPitch05mmRadioButton.AutoSize = true;
            LeadScrewPitch05mmRadioButton.Location = new Point(3, 3);
            LeadScrewPitch05mmRadioButton.Name = "LeadScrewPitch05mmRadioButton";
            LeadScrewPitch05mmRadioButton.Size = new Size(62, 19);
            LeadScrewPitch05mmRadioButton.TabIndex = 0;
            LeadScrewPitch05mmRadioButton.TabStop = true;
            LeadScrewPitch05mmRadioButton.Text = "0.5mm";
            LeadScrewPitch05mmRadioButton.UseVisualStyleBackColor = true;
            LeadScrewPitch05mmRadioButton.CheckedChanged += LeadScrewPitch05mmRadioButton_CheckedChanged;
            // 
            // LeadScrewPitch1mmRadioButton
            // 
            LeadScrewPitch1mmRadioButton.AutoSize = true;
            LeadScrewPitch1mmRadioButton.Location = new Point(73, 3);
            LeadScrewPitch1mmRadioButton.Name = "LeadScrewPitch1mmRadioButton";
            LeadScrewPitch1mmRadioButton.Size = new Size(53, 19);
            LeadScrewPitch1mmRadioButton.TabIndex = 1;
            LeadScrewPitch1mmRadioButton.TabStop = true;
            LeadScrewPitch1mmRadioButton.Text = "1mm";
            LeadScrewPitch1mmRadioButton.UseVisualStyleBackColor = true;
            LeadScrewPitch1mmRadioButton.CheckedChanged += LeadScrewPitch1mmRadioButton_CheckedChanged;
            // 
            // LeadScrewPitch2mmRadioButton
            // 
            LeadScrewPitch2mmRadioButton.AutoSize = true;
            LeadScrewPitch2mmRadioButton.Location = new Point(143, 3);
            LeadScrewPitch2mmRadioButton.Name = "LeadScrewPitch2mmRadioButton";
            LeadScrewPitch2mmRadioButton.Size = new Size(53, 19);
            LeadScrewPitch2mmRadioButton.TabIndex = 2;
            LeadScrewPitch2mmRadioButton.TabStop = true;
            LeadScrewPitch2mmRadioButton.Text = "2mm";
            LeadScrewPitch2mmRadioButton.UseVisualStyleBackColor = true;
            LeadScrewPitch2mmRadioButton.CheckedChanged += LeadScrewPitch2mmRadioButton_CheckedChanged;
            // 
            // LeadScrewPitch25mmRadioButton
            // 
            LeadScrewPitch25mmRadioButton.AutoSize = true;
            LeadScrewPitch25mmRadioButton.Location = new Point(209, 3);
            LeadScrewPitch25mmRadioButton.Name = "LeadScrewPitch25mmRadioButton";
            LeadScrewPitch25mmRadioButton.Size = new Size(62, 19);
            LeadScrewPitch25mmRadioButton.TabIndex = 3;
            LeadScrewPitch25mmRadioButton.TabStop = true;
            LeadScrewPitch25mmRadioButton.Text = "2.5mm";
            LeadScrewPitch25mmRadioButton.UseVisualStyleBackColor = true;
            LeadScrewPitch25mmRadioButton.CheckedChanged += LeadScrewPitch25mmRadioButton_CheckedChanged;
            // 
            // MotionControllerSubdivisionComboBox
            // 
            MotionControllerSubdivisionComboBox.Anchor = AnchorStyles.Right;
            MotionControllerSubdivisionComboBox.FormattingEnabled = true;
            MotionControllerSubdivisionComboBox.Location = new Point(244, 119);
            MotionControllerSubdivisionComboBox.Name = "MotionControllerSubdivisionComboBox";
            MotionControllerSubdivisionComboBox.Size = new Size(121, 23);
            MotionControllerSubdivisionComboBox.TabIndex = 6;
            MotionControllerSubdivisionComboBox.SelectedIndexChanged += MotionControllerSubdivisionComboBox_SelectedIndexChanged;
            // 
            // tableLayoutPanel5
            // 
            tableLayoutPanel5.ColumnCount = 2;
            tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37.59124F));
            tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62.40876F));
            tableLayoutPanel5.Controls.Add(PulseEquivalentResponseLabel, 0, 0);
            tableLayoutPanel5.Controls.Add(CalculatePulseEquivalentButton, 1, 0);
            tableLayoutPanel5.Location = new Point(91, 153);
            tableLayoutPanel5.Name = "tableLayoutPanel5";
            tableLayoutPanel5.RowCount = 1;
            tableLayoutPanel5.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel5.Size = new Size(274, 43);
            tableLayoutPanel5.TabIndex = 7;
            // 
            // PulseEquivalentResponseLabel
            // 
            PulseEquivalentResponseLabel.AutoSize = true;
            PulseEquivalentResponseLabel.Location = new Point(3, 0);
            PulseEquivalentResponseLabel.Name = "PulseEquivalentResponseLabel";
            PulseEquivalentResponseLabel.Size = new Size(0, 15);
            PulseEquivalentResponseLabel.TabIndex = 0;
            // 
            // CalculatePulseEquivalentButton
            // 
            CalculatePulseEquivalentButton.Location = new Point(106, 3);
            CalculatePulseEquivalentButton.Name = "CalculatePulseEquivalentButton";
            CalculatePulseEquivalentButton.Size = new Size(165, 37);
            CalculatePulseEquivalentButton.TabIndex = 1;
            CalculatePulseEquivalentButton.Text = "Calculate Pulse Equivalent";
            CalculatePulseEquivalentButton.UseVisualStyleBackColor = true;
            CalculatePulseEquivalentButton.Click += CalculatePulseEquivalentButton_Click;
            // 
            // MotionControllerSettingsGroupBox
            // 
            MotionControllerSettingsGroupBox.Controls.Add(tableLayoutPanel1);
            MotionControllerSettingsGroupBox.Location = new Point(6, 126);
            MotionControllerSettingsGroupBox.Name = "MotionControllerSettingsGroupBox";
            MotionControllerSettingsGroupBox.Size = new Size(377, 181);
            MotionControllerSettingsGroupBox.TabIndex = 2;
            MotionControllerSettingsGroupBox.TabStop = false;
            MotionControllerSettingsGroupBox.Text = "Motion Controller Settings";
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 121F));
            tableLayoutPanel1.Controls.Add(XAxisDisplacementLabel, 0, 0);
            tableLayoutPanel1.Controls.Add(YAxisDisplacementLabel, 0, 1);
            tableLayoutPanel1.Controls.Add(MotionControllerSpeedLabel, 0, 2);
            tableLayoutPanel1.Controls.Add(XAxisDisplacementTextBox, 1, 0);
            tableLayoutPanel1.Controls.Add(YAxisDisplacementTextBox, 1, 1);
            tableLayoutPanel1.Controls.Add(MotionControllerSpeedTextBox, 1, 2);
            tableLayoutPanel1.Controls.Add(MoveXAxisButton, 2, 0);
            tableLayoutPanel1.Controls.Add(MoveYAxisButton, 2, 1);
            tableLayoutPanel1.Controls.Add(ChangeMotionControllerSpeedButton, 2, 2);
            tableLayoutPanel1.Location = new Point(0, 12);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            tableLayoutPanel1.Size = new Size(365, 151);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // XAxisDisplacementLabel
            // 
            XAxisDisplacementLabel.Anchor = AnchorStyles.None;
            XAxisDisplacementLabel.AutoSize = true;
            XAxisDisplacementLabel.Location = new Point(3, 18);
            XAxisDisplacementLabel.Name = "XAxisDisplacementLabel";
            XAxisDisplacementLabel.Size = new Size(115, 15);
            XAxisDisplacementLabel.TabIndex = 0;
            XAxisDisplacementLabel.Text = "X-Axis Displacement";
            // 
            // YAxisDisplacementLabel
            // 
            YAxisDisplacementLabel.Anchor = AnchorStyles.None;
            YAxisDisplacementLabel.AutoSize = true;
            YAxisDisplacementLabel.Location = new Point(3, 70);
            YAxisDisplacementLabel.Name = "YAxisDisplacementLabel";
            YAxisDisplacementLabel.Size = new Size(115, 15);
            YAxisDisplacementLabel.TabIndex = 1;
            YAxisDisplacementLabel.Text = "Y-Axis Displacement";
            // 
            // MotionControllerSpeedLabel
            // 
            MotionControllerSpeedLabel.Anchor = AnchorStyles.None;
            MotionControllerSpeedLabel.AutoSize = true;
            MotionControllerSpeedLabel.Location = new Point(21, 120);
            MotionControllerSpeedLabel.Name = "MotionControllerSpeedLabel";
            MotionControllerSpeedLabel.Size = new Size(79, 15);
            MotionControllerSpeedLabel.TabIndex = 2;
            MotionControllerSpeedLabel.Text = "Speed (1-255)";
            // 
            // XAxisDisplacementTextBox
            // 
            XAxisDisplacementTextBox.Anchor = AnchorStyles.None;
            XAxisDisplacementTextBox.Location = new Point(133, 14);
            XAxisDisplacementTextBox.Name = "XAxisDisplacementTextBox";
            XAxisDisplacementTextBox.Size = new Size(100, 23);
            XAxisDisplacementTextBox.TabIndex = 3;
            // 
            // YAxisDisplacementTextBox
            // 
            YAxisDisplacementTextBox.Anchor = AnchorStyles.None;
            YAxisDisplacementTextBox.Location = new Point(133, 66);
            YAxisDisplacementTextBox.Name = "YAxisDisplacementTextBox";
            YAxisDisplacementTextBox.Size = new Size(100, 23);
            YAxisDisplacementTextBox.TabIndex = 4;
            // 
            // MotionControllerSpeedTextBox
            // 
            MotionControllerSpeedTextBox.Anchor = AnchorStyles.None;
            MotionControllerSpeedTextBox.Location = new Point(133, 116);
            MotionControllerSpeedTextBox.Name = "MotionControllerSpeedTextBox";
            MotionControllerSpeedTextBox.Size = new Size(100, 23);
            MotionControllerSpeedTextBox.TabIndex = 5;
            // 
            // MoveXAxisButton
            // 
            MoveXAxisButton.Anchor = AnchorStyles.None;
            MoveXAxisButton.Location = new Point(247, 14);
            MoveXAxisButton.Name = "MoveXAxisButton";
            MoveXAxisButton.Size = new Size(115, 23);
            MoveXAxisButton.TabIndex = 6;
            MoveXAxisButton.Text = "Translate X-Axis";
            MoveXAxisButton.UseVisualStyleBackColor = true;
            // 
            // MoveYAxisButton
            // 
            MoveYAxisButton.Anchor = AnchorStyles.None;
            MoveYAxisButton.Location = new Point(247, 66);
            MoveYAxisButton.Name = "MoveYAxisButton";
            MoveYAxisButton.Size = new Size(115, 23);
            MoveYAxisButton.TabIndex = 7;
            MoveYAxisButton.Text = "Translate Y-Axis ";
            MoveYAxisButton.UseVisualStyleBackColor = true;
            MoveYAxisButton.Click += MoveYAxisButton_Click;
            // 
            // ChangeMotionControllerSpeedButton
            // 
            ChangeMotionControllerSpeedButton.Anchor = AnchorStyles.None;
            ChangeMotionControllerSpeedButton.Location = new Point(247, 116);
            ChangeMotionControllerSpeedButton.Name = "ChangeMotionControllerSpeedButton";
            ChangeMotionControllerSpeedButton.Size = new Size(115, 23);
            ChangeMotionControllerSpeedButton.TabIndex = 8;
            ChangeMotionControllerSpeedButton.Text = "Change Speed";
            ChangeMotionControllerSpeedButton.UseVisualStyleBackColor = true;
            ChangeMotionControllerSpeedButton.Click += ChangeMotionControllerSpeedButton_Click;
            // 
            // MotionControllerConnectionSettingsGroupBox
            // 
            MotionControllerConnectionSettingsGroupBox.Controls.Add(MotionControllerConnectionTableLayout);
            MotionControllerConnectionSettingsGroupBox.Location = new Point(0, 3);
            MotionControllerConnectionSettingsGroupBox.Name = "MotionControllerConnectionSettingsGroupBox";
            MotionControllerConnectionSettingsGroupBox.Size = new Size(383, 120);
            MotionControllerConnectionSettingsGroupBox.TabIndex = 1;
            MotionControllerConnectionSettingsGroupBox.TabStop = false;
            MotionControllerConnectionSettingsGroupBox.Text = "Connection Settings";
            // 
            // MotionControllerConnectionTableLayout
            // 
            MotionControllerConnectionTableLayout.ColumnCount = 3;
            MotionControllerConnectionTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48.8095245F));
            MotionControllerConnectionTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 51.1904755F));
            MotionControllerConnectionTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 121F));
            MotionControllerConnectionTableLayout.Controls.Add(AvailableDevicesComboBox, 1, 0);
            MotionControllerConnectionTableLayout.Controls.Add(ScanAvailableMotionControllerDevicesButton, 2, 0);
            MotionControllerConnectionTableLayout.Controls.Add(ConnectToMotionControllerButton, 2, 1);
            MotionControllerConnectionTableLayout.Controls.Add(DisconnectMotionControllerButton, 2, 2);
            MotionControllerConnectionTableLayout.Controls.Add(AvailableDevicesLabel, 0, 0);
            MotionControllerConnectionTableLayout.Controls.Add(ConnectionStatusLabel, 0, 1);
            MotionControllerConnectionTableLayout.Controls.Add(ConnectionStatusResponseLabel, 1, 1);
            MotionControllerConnectionTableLayout.Location = new Point(6, 22);
            MotionControllerConnectionTableLayout.Name = "MotionControllerConnectionTableLayout";
            MotionControllerConnectionTableLayout.RowCount = 3;
            MotionControllerConnectionTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 51.7241364F));
            MotionControllerConnectionTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 48.2758636F));
            MotionControllerConnectionTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            MotionControllerConnectionTableLayout.Size = new Size(368, 92);
            MotionControllerConnectionTableLayout.TabIndex = 0;
            // 
            // AvailableDevicesComboBox
            // 
            AvailableDevicesComboBox.FormattingEnabled = true;
            AvailableDevicesComboBox.Location = new Point(123, 3);
            AvailableDevicesComboBox.Name = "AvailableDevicesComboBox";
            AvailableDevicesComboBox.Size = new Size(115, 23);
            AvailableDevicesComboBox.TabIndex = 1;
            // 
            // ScanAvailableMotionControllerDevicesButton
            // 
            ScanAvailableMotionControllerDevicesButton.Location = new Point(249, 3);
            ScanAvailableMotionControllerDevicesButton.Name = "ScanAvailableMotionControllerDevicesButton";
            ScanAvailableMotionControllerDevicesButton.Size = new Size(110, 22);
            ScanAvailableMotionControllerDevicesButton.TabIndex = 2;
            ScanAvailableMotionControllerDevicesButton.Text = "Scan Devices";
            ScanAvailableMotionControllerDevicesButton.UseVisualStyleBackColor = true;
            ScanAvailableMotionControllerDevicesButton.Click += ScanAvailableMotionControllerDevicesButton_Click;
            // 
            // ConnectToMotionControllerButton
            // 
            ConnectToMotionControllerButton.Location = new Point(249, 34);
            ConnectToMotionControllerButton.Name = "ConnectToMotionControllerButton";
            ConnectToMotionControllerButton.Size = new Size(110, 23);
            ConnectToMotionControllerButton.TabIndex = 5;
            ConnectToMotionControllerButton.Text = "Connect Device";
            ConnectToMotionControllerButton.UseVisualStyleBackColor = true;
            ConnectToMotionControllerButton.Click += ConnectToMotionControllerButton_Click;
            // 
            // DisconnectMotionControllerButton
            // 
            DisconnectMotionControllerButton.Location = new Point(249, 63);
            DisconnectMotionControllerButton.Name = "DisconnectMotionControllerButton";
            DisconnectMotionControllerButton.Size = new Size(110, 23);
            DisconnectMotionControllerButton.TabIndex = 6;
            DisconnectMotionControllerButton.Text = "Disconnect";
            DisconnectMotionControllerButton.UseVisualStyleBackColor = true;
            // 
            // AvailableDevicesLabel
            // 
            AvailableDevicesLabel.Anchor = AnchorStyles.None;
            AvailableDevicesLabel.AutoSize = true;
            AvailableDevicesLabel.Location = new Point(11, 8);
            AvailableDevicesLabel.Name = "AvailableDevicesLabel";
            AvailableDevicesLabel.Size = new Size(98, 15);
            AvailableDevicesLabel.TabIndex = 7;
            AvailableDevicesLabel.Text = "Available Devices";
            // 
            // ConnectionStatusLabel
            // 
            ConnectionStatusLabel.Anchor = AnchorStyles.None;
            ConnectionStatusLabel.AutoSize = true;
            ConnectionStatusLabel.Location = new Point(8, 38);
            ConnectionStatusLabel.Name = "ConnectionStatusLabel";
            ConnectionStatusLabel.Size = new Size(104, 15);
            ConnectionStatusLabel.TabIndex = 8;
            ConnectionStatusLabel.Text = "Connection Status";
            // 
            // ConnectionStatusResponseLabel
            // 
            ConnectionStatusResponseLabel.Anchor = AnchorStyles.None;
            ConnectionStatusResponseLabel.AutoSize = true;
            ConnectionStatusResponseLabel.Location = new Point(143, 38);
            ConnectionStatusResponseLabel.Name = "ConnectionStatusResponseLabel";
            ConnectionStatusResponseLabel.Size = new Size(79, 15);
            ConnectionStatusResponseLabel.TabIndex = 9;
            ConnectionStatusResponseLabel.Text = "Disconnected";
            // 
            // DaqDeviceTabPage
            // 
            DaqDeviceTabPage.Controls.Add(clear_zero_button);
            DaqDeviceTabPage.Controls.Add(zero_voltage_button);
            DaqDeviceTabPage.Controls.Add(button2);
            DaqDeviceTabPage.Controls.Add(tableLayoutPanel12);
            DaqDeviceTabPage.Controls.Add(button1);
            DaqDeviceTabPage.Controls.Add(label5);
            DaqDeviceTabPage.Controls.Add(tableLayoutPanel11);
            DaqDeviceTabPage.Controls.Add(tableLayoutPanel10);
            DaqDeviceTabPage.Controls.Add(backgroundWorkerStartButton);
            DaqDeviceTabPage.Controls.Add(SetPunctureOffsetButton);
            DaqDeviceTabPage.Controls.Add(tableLayoutPanel9);
            DaqDeviceTabPage.Controls.Add(tableLayoutPanel8);
            DaqDeviceTabPage.Controls.Add(MonitorResponseChart);
            DaqDeviceTabPage.Controls.Add(DAQDataGridView);
            DaqDeviceTabPage.Location = new Point(4, 24);
            DaqDeviceTabPage.Name = "DaqDeviceTabPage";
            DaqDeviceTabPage.Padding = new Padding(3);
            DaqDeviceTabPage.Size = new Size(1111, 552);
            DaqDeviceTabPage.TabIndex = 1;
            DaqDeviceTabPage.Text = "DAQ Device";
            DaqDeviceTabPage.UseVisualStyleBackColor = true;
            // 
            // zero_voltage_button
            // 
            zero_voltage_button.Location = new Point(484, 209);
            zero_voltage_button.Name = "zero_voltage_button";
            zero_voltage_button.Size = new Size(75, 23);
            zero_voltage_button.TabIndex = 20;
            zero_voltage_button.Text = "Set Zero";
            zero_voltage_button.UseVisualStyleBackColor = true;
            zero_voltage_button.Click += zero_voltage_button_Click;
            // 
            // button2
            // 
            button2.Location = new Point(891, 355);
            button2.Name = "button2";
            button2.Size = new Size(75, 23);
            button2.TabIndex = 19;
            button2.Text = "Save Data";
            button2.UseVisualStyleBackColor = true;
            button2.Click += saveFileButton_Click;
            // 
            // tableLayoutPanel12
            // 
            tableLayoutPanel12.ColumnCount = 2;
            tableLayoutPanel12.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel12.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel12.Controls.Add(PollingRateLabel, 0, 0);
            tableLayoutPanel12.Controls.Add(CollectionTimeLabel, 1, 0);
            tableLayoutPanel12.Controls.Add(tableLayoutPanel13, 0, 1);
            tableLayoutPanel12.Controls.Add(CollectionTimeSecondsTextBox, 1, 1);
            tableLayoutPanel12.Location = new Point(771, 6);
            tableLayoutPanel12.Name = "tableLayoutPanel12";
            tableLayoutPanel12.RowCount = 3;
            tableLayoutPanel12.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel12.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel12.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tableLayoutPanel12.Size = new Size(344, 173);
            tableLayoutPanel12.TabIndex = 18;
            // 
            // PollingRateLabel
            // 
            PollingRateLabel.Anchor = AnchorStyles.None;
            PollingRateLabel.AutoSize = true;
            PollingRateLabel.Location = new Point(40, 27);
            PollingRateLabel.Name = "PollingRateLabel";
            PollingRateLabel.Size = new Size(92, 15);
            PollingRateLabel.TabIndex = 0;
            PollingRateLabel.Text = "Polling Rate(Hz)";
            // 
            // CollectionTimeLabel
            // 
            CollectionTimeLabel.Anchor = AnchorStyles.None;
            CollectionTimeLabel.AutoSize = true;
            CollectionTimeLabel.Location = new Point(204, 27);
            CollectionTimeLabel.Name = "CollectionTimeLabel";
            CollectionTimeLabel.Size = new Size(107, 15);
            CollectionTimeLabel.TabIndex = 1;
            CollectionTimeLabel.Text = "Collection Time (s)";
            // 
            // tableLayoutPanel13
            // 
            tableLayoutPanel13.ColumnCount = 3;
            tableLayoutPanel13.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel13.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel13.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 51F));
            tableLayoutPanel13.Controls.Add(ThousandHertzRadioButton, 0, 0);
            tableLayoutPanel13.Controls.Add(TwoThousandHertzRadioButton, 1, 0);
            tableLayoutPanel13.Controls.Add(ThreeThousandHertzRadioButton, 2, 0);
            tableLayoutPanel13.Location = new Point(3, 73);
            tableLayoutPanel13.Name = "tableLayoutPanel13";
            tableLayoutPanel13.RowCount = 1;
            tableLayoutPanel13.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel13.Size = new Size(166, 64);
            tableLayoutPanel13.TabIndex = 2;
            // 
            // ThousandHertzRadioButton
            // 
            ThousandHertzRadioButton.Anchor = AnchorStyles.None;
            ThousandHertzRadioButton.AutoSize = true;
            ThousandHertzRadioButton.Location = new Point(4, 22);
            ThousandHertzRadioButton.Name = "ThousandHertzRadioButton";
            ThousandHertzRadioButton.Size = new Size(49, 19);
            ThousandHertzRadioButton.TabIndex = 0;
            ThousandHertzRadioButton.TabStop = true;
            ThousandHertzRadioButton.Text = "1000";
            ThousandHertzRadioButton.UseVisualStyleBackColor = true;
            ThousandHertzRadioButton.CheckedChanged += ThousandHertzRadioButton_CheckedChanged;
            // 
            // TwoThousandHertzRadioButton
            // 
            TwoThousandHertzRadioButton.Anchor = AnchorStyles.None;
            TwoThousandHertzRadioButton.AutoSize = true;
            TwoThousandHertzRadioButton.Location = new Point(61, 22);
            TwoThousandHertzRadioButton.Name = "TwoThousandHertzRadioButton";
            TwoThousandHertzRadioButton.Size = new Size(49, 19);
            TwoThousandHertzRadioButton.TabIndex = 1;
            TwoThousandHertzRadioButton.TabStop = true;
            TwoThousandHertzRadioButton.Text = "2000";
            TwoThousandHertzRadioButton.UseVisualStyleBackColor = true;
            TwoThousandHertzRadioButton.CheckedChanged += TwoThousandHertzRadioButton_CheckedChanged;
            // 
            // ThreeThousandHertzRadioButton
            // 
            ThreeThousandHertzRadioButton.Anchor = AnchorStyles.None;
            ThreeThousandHertzRadioButton.AutoSize = true;
            ThreeThousandHertzRadioButton.Location = new Point(117, 22);
            ThreeThousandHertzRadioButton.Name = "ThreeThousandHertzRadioButton";
            ThreeThousandHertzRadioButton.Size = new Size(46, 19);
            ThreeThousandHertzRadioButton.TabIndex = 2;
            ThreeThousandHertzRadioButton.TabStop = true;
            ThreeThousandHertzRadioButton.Text = "3000";
            ThreeThousandHertzRadioButton.TextImageRelation = TextImageRelation.ImageAboveText;
            ThreeThousandHertzRadioButton.UseVisualStyleBackColor = true;
            ThreeThousandHertzRadioButton.CheckedChanged += radioButton6_CheckedChanged;
            // 
            // CollectionTimeSecondsTextBox
            // 
            CollectionTimeSecondsTextBox.Anchor = AnchorStyles.None;
            CollectionTimeSecondsTextBox.Location = new Point(208, 93);
            CollectionTimeSecondsTextBox.Name = "CollectionTimeSecondsTextBox";
            CollectionTimeSecondsTextBox.Size = new Size(100, 23);
            CollectionTimeSecondsTextBox.TabIndex = 3;
            // 
            // button1
            // 
            button1.Location = new Point(586, 506);
            button1.Name = "button1";
            button1.Size = new Size(179, 23);
            button1.TabIndex = 17;
            button1.Text = "Continuous Scan\r\n\r\n";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(521, 260);
            label5.Name = "label5";
            label5.Size = new Size(266, 15);
            label5.TabIndex = 16;
            label5.Text = "Loadcell Settings: ONLY USE Fracture with LSB-25";
            // 
            // tableLayoutPanel11
            // 
            tableLayoutPanel11.ColumnCount = 3;
            tableLayoutPanel11.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel11.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel11.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 61F));
            tableLayoutPanel11.Controls.Add(label4, 0, 0);
            tableLayoutPanel11.Controls.Add(PlaneDetectionThresholdTextBox, 1, 0);
            tableLayoutPanel11.Location = new Point(565, 131);
            tableLayoutPanel11.Name = "tableLayoutPanel11";
            tableLayoutPanel11.RowCount = 1;
            tableLayoutPanel11.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel11.Size = new Size(200, 73);
            tableLayoutPanel11.TabIndex = 15;
            // 
            // label4
            // 
            label4.Anchor = AnchorStyles.None;
            label4.AutoSize = true;
            label4.Location = new Point(4, 14);
            label4.Name = "label4";
            label4.Size = new Size(61, 45);
            label4.TabIndex = 0;
            label4.Text = "Plane Detection Threshold";
            // 
            // PlaneDetectionThresholdTextBox
            // 
            PlaneDetectionThresholdTextBox.Anchor = AnchorStyles.None;
            PlaneDetectionThresholdTextBox.Location = new Point(72, 25);
            PlaneDetectionThresholdTextBox.Name = "PlaneDetectionThresholdTextBox";
            PlaneDetectionThresholdTextBox.Size = new Size(63, 23);
            PlaneDetectionThresholdTextBox.TabIndex = 1;
            // 
            // tableLayoutPanel10
            // 
            tableLayoutPanel10.ColumnCount = 2;
            tableLayoutPanel10.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel10.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel10.Controls.Add(radioButton1, 0, 0);
            tableLayoutPanel10.Controls.Add(radioButton2, 1, 0);
            tableLayoutPanel10.Location = new Point(562, 278);
            tableLayoutPanel10.Name = "tableLayoutPanel10";
            tableLayoutPanel10.RowCount = 1;
            tableLayoutPanel10.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel10.Size = new Size(200, 100);
            tableLayoutPanel10.TabIndex = 14;
            // 
            // radioButton1
            // 
            radioButton1.Anchor = AnchorStyles.None;
            radioButton1.AutoSize = true;
            radioButton1.Location = new Point(3, 40);
            radioButton1.Name = "radioButton1";
            radioButton1.Size = new Size(94, 19);
            radioButton1.TabIndex = 0;
            radioButton1.TabStop = true;
            radioButton1.Text = "Fracture Settings";
            radioButton1.UseVisualStyleBackColor = true;
            radioButton1.CheckedChanged += radioButton1_CheckedChanged;
            // 
            // radioButton2
            // 
            radioButton2.Anchor = AnchorStyles.None;
            radioButton2.AutoSize = true;
            radioButton2.Location = new Point(113, 40);
            radioButton2.Name = "radioButton2";
            radioButton2.Size = new Size(73, 19);
            radioButton2.TabIndex = 1;
            radioButton2.TabStop = true;
            radioButton2.Text = "Puncture";
            radioButton2.UseVisualStyleBackColor = true;
            radioButton2.CheckedChanged += radioButton2_CheckedChanged;
            // 
            // backgroundWorkerStartButton
            // 
            backgroundWorkerStartButton.Location = new Point(484, 145);
            backgroundWorkerStartButton.Name = "backgroundWorkerStartButton";
            backgroundWorkerStartButton.Size = new Size(75, 23);
            backgroundWorkerStartButton.TabIndex = 12;
            backgroundWorkerStartButton.Text = "StartMonitoring";
            backgroundWorkerStartButton.UseVisualStyleBackColor = true;
            backgroundWorkerStartButton.Click += backgroundWorkerStartButton_Click;
            // 
            // SetPunctureOffsetButton
            // 
            SetPunctureOffsetButton.Anchor = AnchorStyles.None;
            SetPunctureOffsetButton.Location = new Point(643, 181);
            SetPunctureOffsetButton.Name = "SetPunctureOffsetButton";
            SetPunctureOffsetButton.Size = new Size(75, 23);
            SetPunctureOffsetButton.TabIndex = 2;
            SetPunctureOffsetButton.Text = "Set Zero Point";
            SetPunctureOffsetButton.UseVisualStyleBackColor = true;
            SetPunctureOffsetButton.Click += SetPunctureOffsetButton_Click;
            // 
            // tableLayoutPanel9
            // 
            tableLayoutPanel9.ColumnCount = 2;
            tableLayoutPanel9.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel9.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel9.Controls.Add(FractureTestStartButton, 0, 1);
            tableLayoutPanel9.Controls.Add(PunctureTestStartButton, 1, 1);
            tableLayoutPanel9.Controls.Add(FractureDepthTextBox, 0, 0);
            tableLayoutPanel9.Controls.Add(PunctureMaxDepthTextBox, 1, 0);
            tableLayoutPanel9.Location = new Point(562, 371);
            tableLayoutPanel9.Name = "tableLayoutPanel9";
            tableLayoutPanel9.RowCount = 2;
            tableLayoutPanel9.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel9.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel9.Size = new Size(200, 100);
            tableLayoutPanel9.TabIndex = 11;
            // 
            // FractureTestStartButton
            // 
            FractureTestStartButton.Anchor = AnchorStyles.None;
            FractureTestStartButton.Location = new Point(12, 63);
            FractureTestStartButton.Name = "FractureTestStartButton";
            FractureTestStartButton.Size = new Size(75, 23);
            FractureTestStartButton.TabIndex = 1;
            FractureTestStartButton.Text = "Fracture";
            FractureTestStartButton.UseVisualStyleBackColor = true;
            FractureTestStartButton.Click += FractureTestStartButton_Click;
            // 
            // PunctureTestStartButton
            // 
            PunctureTestStartButton.Anchor = AnchorStyles.None;
            PunctureTestStartButton.Location = new Point(112, 63);
            PunctureTestStartButton.Name = "PunctureTestStartButton";
            PunctureTestStartButton.Size = new Size(75, 23);
            PunctureTestStartButton.TabIndex = 0;
            PunctureTestStartButton.Text = "Puncture Test Start";
            PunctureTestStartButton.UseVisualStyleBackColor = true;
            PunctureTestStartButton.Click += PunctureTestStartButton_Click;
            // 
            // FractureDepthTextBox
            // 
            FractureDepthTextBox.Anchor = AnchorStyles.None;
            FractureDepthTextBox.Location = new Point(3, 13);
            FractureDepthTextBox.Name = "FractureDepthTextBox";
            FractureDepthTextBox.Size = new Size(94, 23);
            FractureDepthTextBox.TabIndex = 2;
            // 
            // PunctureMaxDepthTextBox
            // 
            PunctureMaxDepthTextBox.Anchor = AnchorStyles.None;
            PunctureMaxDepthTextBox.Location = new Point(103, 13);
            PunctureMaxDepthTextBox.Name = "PunctureMaxDepthTextBox";
            PunctureMaxDepthTextBox.Size = new Size(94, 23);
            PunctureMaxDepthTextBox.TabIndex = 3;
            // 
            // tableLayoutPanel8
            // 
            tableLayoutPanel8.ColumnCount = 3;
            tableLayoutPanel8.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 83.425415F));
            tableLayoutPanel8.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.574585F));
            tableLayoutPanel8.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66F));
            tableLayoutPanel8.Controls.Add(label3, 1, 2);
            tableLayoutPanel8.Controls.Add(DAQMonitoringStatusResponseLabel, 0, 0);
            tableLayoutPanel8.Controls.Add(StartConstantMonitorButton, 2, 0);
            tableLayoutPanel8.Controls.Add(label1, 1, 0);
            tableLayoutPanel8.Controls.Add(DAQStopMonitoringButton, 2, 1);
            tableLayoutPanel8.Controls.Add(NewtonsResponseLabel, 0, 2);
            tableLayoutPanel8.Controls.Add(label2, 1, 1);
            tableLayoutPanel8.Controls.Add(DAQDataResponseLabel, 0, 1);
            tableLayoutPanel8.Controls.Add(ReturnProbeToMaxHeightButton, 2, 2);
            tableLayoutPanel8.Location = new Point(490, 3);
            tableLayoutPanel8.Name = "tableLayoutPanel8";
            tableLayoutPanel8.RowCount = 3;
            tableLayoutPanel8.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel8.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel8.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            tableLayoutPanel8.Size = new Size(275, 122);
            tableLayoutPanel8.TabIndex = 10;
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Left;
            label3.AutoSize = true;
            label3.Location = new Point(177, 96);
            label3.Name = "label3";
            label3.Size = new Size(16, 15);
            label3.TabIndex = 11;
            label3.Text = "N";
            // 
            // DAQMonitoringStatusResponseLabel
            // 
            DAQMonitoringStatusResponseLabel.Anchor = AnchorStyles.Right;
            DAQMonitoringStatusResponseLabel.AutoSize = true;
            DAQMonitoringStatusResponseLabel.Location = new Point(125, 14);
            DAQMonitoringStatusResponseLabel.Name = "DAQMonitoringStatusResponseLabel";
            DAQMonitoringStatusResponseLabel.Size = new Size(46, 15);
            DAQMonitoringStatusResponseLabel.TabIndex = 2;
            DAQMonitoringStatusResponseLabel.Text = "Voltage";
            // 
            // StartConstantMonitorButton
            // 
            StartConstantMonitorButton.Location = new Point(211, 3);
            StartConstantMonitorButton.Name = "StartConstantMonitorButton";
            StartConstantMonitorButton.Size = new Size(61, 37);
            StartConstantMonitorButton.TabIndex = 5;
            StartConstantMonitorButton.Text = "Find Surface";
            StartConstantMonitorButton.UseVisualStyleBackColor = true;
            StartConstantMonitorButton.Click += StartConstantMonitorButton_Click;
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Left;
            label1.AutoSize = true;
            label1.Location = new Point(177, 14);
            label1.Name = "label1";
            label1.Size = new Size(14, 15);
            label1.TabIndex = 8;
            label1.Text = "V";
            // 
            // DAQStopMonitoringButton
            // 
            DAQStopMonitoringButton.Location = new Point(211, 46);
            DAQStopMonitoringButton.Name = "DAQStopMonitoringButton";
            DAQStopMonitoringButton.Size = new Size(61, 23);
            DAQStopMonitoringButton.TabIndex = 4;
            DAQStopMonitoringButton.Text = "Stop Monitoring";
            DAQStopMonitoringButton.UseVisualStyleBackColor = true;
            DAQStopMonitoringButton.Click += DAQStopMonitoringButton_Click;
            // 
            // NewtonsResponseLabel
            // 
            NewtonsResponseLabel.Anchor = AnchorStyles.Right;
            NewtonsResponseLabel.AutoSize = true;
            NewtonsResponseLabel.Location = new Point(117, 96);
            NewtonsResponseLabel.Name = "NewtonsResponseLabel";
            NewtonsResponseLabel.Size = new Size(54, 15);
            NewtonsResponseLabel.TabIndex = 6;
            NewtonsResponseLabel.Text = "Newtons";
            // 
            // label2
            // 
            label2.Anchor = AnchorStyles.Left;
            label2.AutoSize = true;
            label2.Location = new Point(177, 57);
            label2.Name = "label2";
            label2.Size = new Size(22, 15);
            label2.TabIndex = 9;
            label2.Text = "lbs";
            // 
            // DAQDataResponseLabel
            // 
            DAQDataResponseLabel.Anchor = AnchorStyles.Right;
            DAQDataResponseLabel.AutoSize = true;
            DAQDataResponseLabel.Location = new Point(124, 57);
            DAQDataResponseLabel.Name = "DAQDataResponseLabel";
            DAQDataResponseLabel.Size = new Size(47, 15);
            DAQDataResponseLabel.TabIndex = 3;
            DAQDataResponseLabel.Text = "Pounds";
            // 
            // ReturnProbeToMaxHeightButton
            // 
            ReturnProbeToMaxHeightButton.Location = new Point(211, 89);
            ReturnProbeToMaxHeightButton.Name = "ReturnProbeToMaxHeightButton";
            ReturnProbeToMaxHeightButton.Size = new Size(61, 23);
            ReturnProbeToMaxHeightButton.TabIndex = 12;
            ReturnProbeToMaxHeightButton.Text = "Return Probe to Top";
            ReturnProbeToMaxHeightButton.UseVisualStyleBackColor = true;
            ReturnProbeToMaxHeightButton.Click += ReturnProbeToMaxHeightButton_Click;
            // 
            // MonitorResponseChart
            // 
            chartArea1.Name = "ChartArea1";
            MonitorResponseChart.ChartAreas.Add(chartArea1);
            legend1.Name = "Legend1";
            MonitorResponseChart.Legends.Add(legend1);
            MonitorResponseChart.Location = new Point(8, 3);
            MonitorResponseChart.Name = "MonitorResponseChart";
            series1.ChartArea = "ChartArea1";
            series1.Legend = "Legend1";
            series1.Name = "Series1";
            MonitorResponseChart.Series.Add(series1);
            MonitorResponseChart.Size = new Size(476, 289);
            MonitorResponseChart.TabIndex = 7;
            MonitorResponseChart.Text = "chart1";
            // 
            // DAQDataGridView
            // 
            DAQDataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DAQDataGridView.Location = new Point(0, 295);
            DAQDataGridView.Name = "DAQDataGridView";
            DAQDataGridView.Size = new Size(495, 209);
            DAQDataGridView.TabIndex = 0;
            // 
            // clear_zero_button
            // 
            clear_zero_button.Location = new Point(562, 209);
            clear_zero_button.Name = "clear_zero_button";
            clear_zero_button.Size = new Size(75, 23);
            clear_zero_button.TabIndex = 21;
            clear_zero_button.Text = "Clear Zero";
            clear_zero_button.UseVisualStyleBackColor = true;
            clear_zero_button.Click += clear_zero_button_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1118, 647);
            Controls.Add(tabControl1);
            Controls.Add(MMELogoPictureBox);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)MMELogoPictureBox).EndInit();
            tabControl1.ResumeLayout(false);
            MotionControllerTabPage.ResumeLayout(false);
            tableLayoutPanel6.ResumeLayout(false);
            tableLayoutPanel6.PerformLayout();
            tableLayoutPanel7.ResumeLayout(false);
            tableLayoutPanel7.PerformLayout();
            StepperMotorValuesGroupBox.ResumeLayout(false);
            YAxisPropertiesGroupBox.ResumeLayout(false);
            YAxisPropertiesGroupBox.PerformLayout();
            StepperMotorSettingsGroupBox.ResumeLayout(false);
            tableLayoutPanel2.ResumeLayout(false);
            tableLayoutPanel2.PerformLayout();
            tableLayoutPanel3.ResumeLayout(false);
            tableLayoutPanel3.PerformLayout();
            tableLayoutPanel4.ResumeLayout(false);
            tableLayoutPanel4.PerformLayout();
            tableLayoutPanel5.ResumeLayout(false);
            tableLayoutPanel5.PerformLayout();
            MotionControllerSettingsGroupBox.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            MotionControllerConnectionSettingsGroupBox.ResumeLayout(false);
            MotionControllerConnectionTableLayout.ResumeLayout(false);
            MotionControllerConnectionTableLayout.PerformLayout();
            DaqDeviceTabPage.ResumeLayout(false);
            DaqDeviceTabPage.PerformLayout();
            tableLayoutPanel12.ResumeLayout(false);
            tableLayoutPanel12.PerformLayout();
            tableLayoutPanel13.ResumeLayout(false);
            tableLayoutPanel13.PerformLayout();
            tableLayoutPanel11.ResumeLayout(false);
            tableLayoutPanel11.PerformLayout();
            tableLayoutPanel10.ResumeLayout(false);
            tableLayoutPanel10.PerformLayout();
            tableLayoutPanel9.ResumeLayout(false);
            tableLayoutPanel9.PerformLayout();
            tableLayoutPanel8.ResumeLayout(false);
            tableLayoutPanel8.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)MonitorResponseChart).EndInit();
            ((System.ComponentModel.ISupportInitialize)DAQDataGridView).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private PictureBox MMELogoPictureBox;
        private TabControl tabControl1;
        private TabPage MotionControllerTabPage;
        private TabPage DaqDeviceTabPage;
        private TableLayoutPanel MotionControllerConnectionTableLayout;
        private GroupBox MotionControllerConnectionSettingsGroupBox;
        private ComboBox AvailableDevicesComboBox;
        private Button ScanAvailableMotionControllerDevicesButton;
        private Button ConnectToMotionControllerButton;
        private GroupBox MotionControllerSettingsGroupBox;
        private Button DisconnectMotionControllerButton;
        private TabPage MicroTextureAnalyzerTabPage;
        private GroupBox StepperMotorSettingsGroupBox;
        private TableLayoutPanel tableLayoutPanel1;
        private Label AvailableDevicesLabel;
        private Label ConnectionStatusLabel;
        private Label ConnectionStatusResponseLabel;
        private Label XAxisDisplacementLabel;
        private Label YAxisDisplacementLabel;
        private Label MotionControllerSpeedLabel;
        private TextBox XAxisDisplacementTextBox;
        private TextBox YAxisDisplacementTextBox;
        private TextBox MotionControllerSpeedTextBox;
        private Button MoveXAxisButton;
        private GroupBox StepperMotorValuesGroupBox;
        private TableLayoutPanel tableLayoutPanel2;
        private Button MoveYAxisButton;
        private Button ChangeMotionControllerSpeedButton;
        private Label StepperMotorAngleLabel;
        private Label LeadScrewPitchLabel;
        private Label StepperMotorSubdivisionLabel;
        private Label PulseEquivalentLabel;
        private TableLayoutPanel tableLayoutPanel3;
        private RadioButton StepperMotorAngle18RadioButton;
        private RadioButton StepperMotorAngle09RadioButton;
        private TableLayoutPanel tableLayoutPanel4;
        private RadioButton LeadScrewPitch05mmRadioButton;
        private RadioButton LeadScrewPitch1mmRadioButton;
        private RadioButton LeadScrewPitch2mmRadioButton;
        private RadioButton LeadScrewPitch25mmRadioButton;
        private ComboBox MotionControllerSubdivisionComboBox;
        private TableLayoutPanel tableLayoutPanel5;
        private Label PulseEquivalentResponseLabel;
        private Button CalculatePulseEquivalentButton;
        private GroupBox YAxisPropertiesGroupBox;
        private TableLayoutPanel tableLayoutPanel6;
        private Label YPositionLabel;
        private Label YStepLabel;
        private Label YPositionResponseLabel;
        private Label YStepResponseLabel;
        private Label YHomeLabel;
        private TableLayoutPanel tableLayoutPanel7;
        private Label YHomeResponseLabel;
        private Button SetYHomeButton;
        private Button SendYToHomeButton;
        private Button StopMotionControllerButton;
        private DataGridView DAQDataGridView;
        private Label DAQMonitoringStatusResponseLabel;
        private Label DAQDataResponseLabel;
        private Button DAQStopMonitoringButton;
        private Label MotionControllerStatusResponseLabel;
        private Button StartConstantMonitorButton;
        private Label NewtonsResponseLabel;
        private System.Windows.Forms.DataVisualization.Charting.Chart MonitorResponseChart;
        private TableLayoutPanel tableLayoutPanel8;
        private Label label2;
        private Label label1;
        private Label label3;
        private Button ReturnProbeToMaxHeightButton;
        private TableLayoutPanel tableLayoutPanel9;
        private Button FractureTestStartButton;
        private Button PunctureTestStartButton;
        private TextBox FractureDepthTextBox;
        private TextBox PunctureMaxDepthTextBox;
        private Button backgroundWorkerStartButton;
        private TableLayoutPanel tableLayoutPanel10;
        private RadioButton radioButton1;
        private RadioButton radioButton2;
        private Button SetPunctureOffsetButton;
        private TableLayoutPanel tableLayoutPanel11;
        private Label label4;
        private Label label5;
        private TextBox PlaneDetectionThresholdTextBox;
        private Button button1;
        private TableLayoutPanel tableLayoutPanel12;
        private Label PollingRateLabel;
        private Label CollectionTimeLabel;
        private TableLayoutPanel tableLayoutPanel13;
        private RadioButton ThousandHertzRadioButton;
        private RadioButton TwoThousandHertzRadioButton;
        private RadioButton ThreeThousandHertzRadioButton;
        private TextBox CollectionTimeSecondsTextBox;
        private Button button2;
        private Button zero_voltage_button;
        private Button clear_zero_button;
    }
}
