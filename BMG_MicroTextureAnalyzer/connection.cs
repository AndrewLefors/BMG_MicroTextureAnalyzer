using System.IO.Ports;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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
            try
            {
                // Use standard API to list serial ports instead of probing every COMx
                var ports = SerialPort.GetPortNames() ?? new string[0];
                var portList = ports.OrderBy(p =>
                {
                    // Order numerically if possible (COM1, COM2, COM10)
                    var digits = System.Text.RegularExpressions.Regex.Replace(p, "[^0-9]", "");
                    if (int.TryParse(digits, out int n)) return n;
                    return int.MaxValue;
                }).Select(p => p).ToList();

                if (portList.Count == 0)
                {
                    portList.Add("No Devices Available");
                }

                this.AvailableDevices = portList;
            }
            catch
            {
                // best-effort: if querying ports fails, surface a friendly message
                this.AvailableDevices = new List<string> { "No Devices Available" };
            }
        }

    }
}
