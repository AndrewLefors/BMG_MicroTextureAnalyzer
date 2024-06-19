using System.IO.Ports;
using System.Collections.Generic;
using System.ComponentModel;

namespace MicroneedleAPI
{

    public class Connection : INotifyPropertyChanged
    {
        private List<string>? _availableDevices;

        public event PropertyChangedEventHandler? PropertyChanged = delegate { };
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public List<string>? AvailableDevices
        {
            get { return _availableDevices; }
            private set 
            {      
                    if (_availableDevices != value)
                    {
                        _availableDevices = value;
                        OnPropertyChanged(nameof(AvailableDevices));
                    }   
            }
        }

        internal void GetOpenPorts()
        {
            List<string> result = new List<string>();
            for (int i = 1; i <= 256; i++)
            {
                string port = "COM" + i.ToString();
                SerialPort sp = new SerialPort(port);
                try
                {
                    sp.Open();
                    if (sp.IsOpen)
                    {
                        sp.Close();
                        result.Add(port);
                    }
                }
                catch (IOException) { }
            }
            ParsePortName(result);
        }

        private void ParsePortName(List<string> portInfo)
        {
            List<string> portList = new List<string>();
            foreach (var port in portInfo)
            {
                portList.Add(port.Split('-').Last());
            }
            if (portList.Count == 0) 
            {
                portList.Add("No Devices Available");
            }
            this.AvailableDevices = portList;
        }

    }
}
