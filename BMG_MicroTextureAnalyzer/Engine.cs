using MicroneedleAPI;
using System.ComponentModel;
using System.IO.Ports;

namespace BMG_MicroTextureAnalyzer
{
    public class Engine : INotifyPropertyChanged
    {
        private MotionController _stage;
        private MccDaqWrapper _dataDev;
        private Connection _connection;
        private string _errorString;

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
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

        public MccDaqWrapper DataDev
        {
            get { return this._dataDev; }
            set
            {
                if (this._dataDev != value)
                {
                    this._dataDev = value;
                    this.OnPropertyChanged(nameof(DataDev));
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
            this.DataDev = new MccDaqWrapper();
            this.Connection.PropertyChanged +=  Engine_PropertyChanged;
            this.Stage.PropertyChanged += Engine_PropertyChanged;   
            this.DataDev.PropertyChanged += Engine_PropertyChanged;
            this.DataDev.DataChanged += DataDev.OnDataChanged;
            this.Stage.SCPort.DataReceived += Stage.SCPort_DataReceived;
            
            
            //TODO: Add INotifyPropertyChanged event handlers for DataDev
           // this.DataDev.PropertyChanged += Engine_PropertyChanged;

        }

        public void Initialize()
        {
            if (this.Stage != null)
            {
                this.Stage.ClosePort();
            }

            this.Stage = new MotionController();
            this.Stage.PropertyChanged += Engine_PropertyChanged;
            
            this.DataDev = new MccDaqWrapper();
            
        }
        //TODO: Add an event to handle all Errors
        //TODO: Add an event to handle all warnings
        //TODO: Add an event to handle all messages
        //TODO: Add an event to handle all DAQ events
        //TODO: Add an event to handle all stage events
        //TODO: Add an event to handle all connection events

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
            catch(Exception ex)
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

        //Methods below relate to the DAQ device
        public void StartMonitoring(int channel, MccDaq.Range range, int intervalMs)
        {
            try
            {
                if (this.DataDev != null)
                {
                    this.DataDev.StartMonitoring(channel, range, intervalMs);
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }

        public void StopMonitoring()
        {
            try
            {
                if (this.DataDev != null)
                {
                    this.DataDev.StopMonitoring();
                }
            }
            catch (Exception ex)
            {
                this.ErrorString = ex.Message;
            }
        }
    }
}
