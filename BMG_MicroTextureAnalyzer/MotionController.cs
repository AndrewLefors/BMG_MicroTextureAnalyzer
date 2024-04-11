﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using System.IO.Ports;

namespace BMG_MicroTextureAnalyzer
{
    public class MotionController : INotifyPropertyChanged
    {

        private bool BlnConnect;                                 //Connection Status
        public static SerialPort SCPort = null;                 //Define serial port
        public string StrReceiver;                              //Receive the string from controller
        private bool BlnBusy;                                   //If controller is busy
        private bool BlnReadCom;                                 //If reading is finished, return TRUE
        private bool BlnStopCommand;                             //Stop waiting
        public short ShrPort;                                   //The serial port number
        private bool BlnSet;                                    //If the command sent is a set command or an inquiry command. TRUE is a set command
        private double DblMotorDegree;                          //Motor degree
        private double DblTransmissionRatio;                    //Transmission ratio
        private double DblSubDivision;                          //Subdivision
        private double DblPulseEqui;                            //Pulse equivalent
        private string _errMessage;                              //Error message
        private string _warningMessage;                          //Warning message
        short sSpeed;                                           //Current speed
        long lCurrStep;                                         //Current steps
        double dCurrPosi;                                       //Current position

        public event PropertyChangedEventHandler? PropertyChanged;

        public MotionController()
        {
            SCPort = new SerialPort();
            sSpeed = 200;
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool ConnectionStatus
        {
            get
            {
                return BlnConnect;
            }
            private set
            {
                if (BlnConnect != value)
                {
                    BlnConnect = value;
                    OnPropertyChanged(nameof(ConnectionStatus));
                }
            }
        }

        public bool ReadCom
        {
            get
            {
                return BlnReadCom;
            }
            private set
            {
                if (BlnReadCom != value)
                {
                    BlnReadCom = value;
                    OnPropertyChanged(nameof(ReadCom));
                }
            }
        }
        public bool Busy
        {
            get
            {
                return BlnBusy;
            }
            private set
            {
                if (BlnBusy != value)
                {
                    BlnBusy = value;
                    OnPropertyChanged(nameof(Busy));
                }
            }
        }
        public bool SetCommand
        {
            get
            {
                return BlnSet;
            }
            private set
            {
                if (BlnSet != value)
                {
                    BlnSet = value;
                    OnPropertyChanged(nameof(SetCommand));
                }
            }
        }

        public double MotorDegree
        {
            get
            {
                return DblMotorDegree;
            }
            private set
            {
                if (DblMotorDegree != value)
                {
                    DblMotorDegree = value;
                    OnPropertyChanged(nameof(MotorDegree));
                }
            }
        }
        
        public double TransmissionRatio
        {
            get
            {
                return DblTransmissionRatio;
            }
            private set
            {
                if (DblTransmissionRatio != value)
                {
                    DblTransmissionRatio = value;
                    OnPropertyChanged(nameof(TransmissionRatio));
                }
            }
        }

        public double SubDivision
        {
            get
            {
                return DblSubDivision;
            }
            private set
            {
                if (DblSubDivision != value)
                {
                    DblSubDivision = value;
                    OnPropertyChanged(nameof(SubDivision));
                }
            }
        }

        public double PulseEquivalent
        {
            get
            {
                return DblPulseEqui;
            }
            private set
            {
                if (DblPulseEqui != value)
                {
                    DblPulseEqui = value;
                    OnPropertyChanged(nameof(PulseEquivalent));
                }
            }
        }

        public double CurrentPosition
        {
            get
            {
                return dCurrPosi;
            }
            private set
            {
                if (dCurrPosi != value)
                {
                    dCurrPosi = value;
                    OnPropertyChanged(nameof(CurrentPosition));
                }
            }
        }

        public long CurrentStep
        {
            get
            {
                return lCurrStep;
            }
            private set
            {
                if (lCurrStep != value)
                {
                    lCurrStep = value;
                    OnPropertyChanged(nameof(CurrentStep));
                }
            }
        }

        public short Speed
        {
            get
            {
                return sSpeed;
            }
            private set
            {
                if (sSpeed != value)
                {
                    sSpeed = value;
                    OnPropertyChanged(nameof(Speed));
                }
            }
        }

        public string ErrorMessage
        {
            get
            {
                return _errMessage;
            }
            private set
            {
                if (_errMessage != value)
                {
                    _errMessage = value;
                    OnPropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        public string WarningMessage
        {
            get
            {
                return _warningMessage;
            }
            private set
            {
                if (_warningMessage != value)
                {
                    _warningMessage = value;
                    OnPropertyChanged(nameof(WarningMessage));
                }
            }
        }

        private void ConnectPort(short sPort)
            {
                if (SCPort.IsOpen == true) SCPort.Close();
                SCPort.PortName = "COM" + sPort.ToString();            //Set the serial port number
                SCPort.BaudRate = 9600;                                //Set the bit rate
                SCPort.DataBits = 8;                                   //Set the data bits
                SCPort.StopBits = StopBits.One;                        //Set the stop bit
                SCPort.Parity = Parity.None;                           //Set the Parity
                SCPort.ReadBufferSize = 2048;
                SCPort.WriteBufferSize = 1024;
                SCPort.DtrEnable = true;
                SCPort.Handshake = Handshake.None;
                SCPort.ReceivedBytesThreshold = 1;
                SCPort.RtsEnable = false;

                //This delegate should be a trigger event for fetching data asynchronously, it will be triggered when there is data passed from serial port.
                SCPort.DataReceived += new SerialDataReceivedEventHandler(SCPort_DataReceived);     //DataReceivedEvent delegate
                try
                {
                    SCPort.Open();                                     //Open serial port
                    if (SCPort.IsOpen)
                    {
                        StrReceiver = "";
                        Busy = true;
                        SetCommand = false;
                        SendCommand("?R\r");                           //Connect to the controller
                        Delay(10000);
                        Busy = false;

                        if (StrReceiver == "?R\rOK\n")
                        {
                            ConnectionStatus = true;  //Connected successfully
                            ShrPort = sPort;                          //Setial port number
                            
                        }
                        else
                        {
                            Busy = false;
                            ConnectionStatus = false;
                            WarningMessage = "Failed to connect";
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
            }

            private void SCPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
            {
                //****************************************************************
                //Function: SCPort_DataReceived
                //Parameters: 
                //Description: receive the data sent from serial port and handle
                //Return:
                //****************************************************************
                try
                {
                    string sCurString = "";
                    sCurString = SCPort.ReadExisting();
                    if (sCurString != "")
                        StrReceiver = StrReceiver + sCurString;
                    if (SetCommand == true)
                    {
                        if (StrReceiver.Length == 3)
                        {
                            if (StrReceiver.Substring(StrReceiver.Length - 3) == "OK\n")
                                ReadCom = true;
                        }
                        else if (StrReceiver.Length == 4)
                        {
                            if (StrReceiver.Substring(StrReceiver.Length - 3) == "OK\n" || StrReceiver.Substring(StrReceiver.Length - 4) == "OK\nS")
                                ReadCom = true;
                        }
                        else if (StrReceiver.Length > 4)
                        {
                            if (StrReceiver.Substring(StrReceiver.Length - 3) == "OK\n" || StrReceiver.Substring(StrReceiver.Length - 4) == "OK\nS" ||
                                StrReceiver.Substring(StrReceiver.Length - 5) == "ERR1\n" || StrReceiver.Substring(StrReceiver.Length - 5) == "ERR5\n")
                                ReadCom = true;
                        }
                    }
                    else
                    {
                        if (StrReceiver.Length > 1)
                        {
                            if (StrReceiver.Substring(StrReceiver.Length - 1, 1) == "\n")
                                ReadCom = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
            }
            public void ClosePort()
            {
                //****************************************************************
                //Function: ClosePort
                //Parameters: 
                //Description: close the connection
                //Return:
                //****************************************************************
                if (SCPort.IsOpen) SCPort.Close();
            }

            public void SendCommand(string CommandString)
            {
                //****************************************************************
                //Function: SendCommand
                //Parameters: CommandString: the command string
                //Description: send the command to controller
                //Return:
                //****************************************************************
                if (SCPort.IsOpen)
                {
                    SCPort.Write(CommandString);
                    SCPort.DiscardOutBuffer();
                }
            }

            public void Delay(long milliSecond = 500)
            {
                //****************************************************************
                //Function: Delay
                //Parameters: milliSecond:the waiting time, unit is millsecond
                //Description: appoint the waiting time and exit waiting until the data reading is finished or clicking the stop button or close the window or the waiting time is over.
                //Return:
                //****************************************************************
                int start = Environment.TickCount;

                ReadCom = false;
                BlnStopCommand = false;
                while (Math.Abs(Environment.TickCount - start) < milliSecond)
                {
                    if (ReadCom == true)
                    {
                        ReadCom = false;
                        return;
                    }
                    if (BlnStopCommand == true) return;
                    //Application.DoEvents();
                }
            }

            private void ConnectToPort(short port)      //Connect to appointed serial port
            {
                ConnectPort(port);
            }

            private void SetNewSpeed(short speed)      //Set new speed and get the current speed
            {
                
                if (speed < 0 || speed > 255)
                {
                    WarningMessage = "The speed value must be an integer between 0 and 255.";
                    return;
                }
                if (Busy == true)
                {
                    WarningMessage = "The connection is busy, please wait.";
                    return;
                }
                StrReceiver = "";
                Busy = true;
                SetCommand = true;
                SendCommand("V" + sSpeed.ToString() + "\r");            //Set speed
                Delay(100000);
                Busy = false;

                StrReceiver = "";
                Busy = true;
                SetCommand = false;
                SendCommand("?V\r");                                    //Inquiry speed
                Delay(100000);
                Busy = false;

                if (StrReceiver != "")
                {
                    Speed = Convert.ToInt16(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                    //label3.Text = "The speed is " + sSpeed.ToString();
                }
            }
            // TODO: Fix equation for calculating pulse equivalent
            private void CalculatePulseEquiv()      //Get the pulse equivalent
            {
                PulseEquivalent = Math.Round(MotorDegree / (TransmissionRatio * SubDivision), 5);
                //label8.Text = "The pulse equivalent is " + DblPulseEqui.ToString();
            }

            private void MoveYAbsolute(double yPos)      //Move Y axis to the appointed position and get the current position
            {
                long lStep;
                string s;
               // button7.Focus();
                //button6_Click(sender, e);
                CurrentStep = Convert.ToInt64((yPos - CurrentPosition) / PulseEquivalent);
                if (CurrentStep > 0)
                    s = "+" + CurrentStep.ToString();
                else
                    s = CurrentStep.ToString();
                StrReceiver = "";
                Busy = true;
                SetCommand = true;
                SendCommand("Y" + s + "\r");   //Move Y axis to the appointed position.

                //textBox7.Text = "...... ";
                //timer1.Interval = 310 - sSpeed;
                //timer1.Enabled = true;
                Delay(100000000);
                Busy = false;
                //timer1.Enabled = false;

                //button6_Click(sender, e);
                GetYPosition();
            }


            private void timer1_Tick(object sender, EventArgs e)
            {
                string s;
                if (BlnReadCom == true)
                {
                    //timer1.Enabled = false;
                    return;
                }
                //s = textBox7.Text;
                //textBox7.Text = s.Substring(s.Length - 1, 1) + s.Substring(0, s.Length - 1);
            }

            private void GetYPosition()      //Get the current position of Y axis
            {
                StrReceiver = "";
                Busy = true;
                SetCommand = false;
                SendCommand("?Y\r");            //Inquiry the current position of Y axis
                Delay(100000);
                Busy = false;

                if (StrReceiver != "")
                {
                    if (StrReceiver.Substring(5, 1) == "-")
                        CurrentStep = -Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                    else
                        CurrentStep = Convert.ToInt64(System.Text.RegularExpressions.Regex.Replace(StrReceiver, @"[^0-9]+", ""));
                }
                else
                    return;
                CurrentPosition = CurrentStep * PulseEquivalent;
                //textBox7.Text = dCurrPosi.ToString();
            }

            private void ReturnYToOrigin()      //Return Y to origin
            {
                //button7.Focus();
                StrReceiver = "";
                Busy = true;
                SetCommand = true;
                SendCommand("HY0\r");   //Home Y axis

                //textBox7.Text = "...... ";
                //timer1.Interval = 310 - sSpeed;
                //timer1.Enabled = true;
                Delay(1000000);
                //timer1.Enabled = false;
                Busy = false;
                //button6_Click(sender, e);
                GetYPosition();
            }

            private void Stop()      //Stop moving
            {
                StrReceiver = "";
                Busy = true;
                SetCommand = true;
                SendCommand("S\r");   //Stop moving
                Delay(100000000);
                //timer1.Enabled = false;
                BlnStopCommand = true;
                //DelayWait(500);
                Busy = false;

            }
    }
}