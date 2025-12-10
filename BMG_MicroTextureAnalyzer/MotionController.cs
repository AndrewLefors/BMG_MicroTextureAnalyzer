using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Concurrent;

namespace BMG_MicroTextureAnalyzer
{
    public class MotionController : INotifyPropertyChanged, IDisposable
    {
        private bool _connected; // connection status
        private SerialPort? _serial;
        private string _recvBuffer = string.Empty;
        private readonly object _recvLock = new object();

        // Command request for async queue
        private class CommandRequest
        {
            public string Command { get; }
            public TaskCompletionSource<string> Tcs { get; }
            public int TimeoutMs { get; }
            public CommandRequest(string cmd, int timeoutMs)
            {
                Command = cmd;
                Tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                TimeoutMs = timeoutMs;
            }
        }

        private BlockingCollection<CommandRequest> _cmdQueue = new BlockingCollection<CommandRequest>();
        private CancellationTokenSource? _cmdCts;
        private Task? _cmdWorker;

        // Polling
        private CancellationTokenSource? _pollingCts;
        private Task? _pollingTask;

        // motion parameters
        private double DblMotorDegree = 1.8;
        private double DblLeadScrewPitch = 1.0;
        private double DblSubDivision = 1.0;
        private double DblPulseEqui = 1600.0; // default

        // reported values
        private long _currentYStep;
        private double _currentYPosition;

        // last polled cached position and timestamp (Unix seconds)
        private double _lastPolledPositionMm = double.NaN;
        private double _lastPolledPositionTimestamp = 0.0;
        private readonly object _lastPosLock = new object();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MotionController()
        {
        }

        // Backwards-compatible flags used by existing UI/Engine code
        public bool Busy { get; private set; }
        public bool ReadCom { get; private set; }
        public bool SetCommand { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;
        public string WarningMessage { get; private set; } = string.Empty;

        // Logging callbacks
        private Action<string, string> _logInfo;
        private Action<string, string> _logWarning;
        private Action<string, string> _logError;

        public void SetLoggers(Action<string, string> logInfo, Action<string, string> logWarning, Action<string, string> logError)
        {
            _logInfo = logInfo;
            _logWarning = logWarning;
            _logError = logError;
        }

        private void LogInfo(string message, string source) => _logInfo?.Invoke(message, source);
        private void LogWarning(string message, string source) => _logWarning?.Invoke(message, source);
        private void LogError(string message, string source) => _logError?.Invoke(message, source);

        // Synchronous wrapper for legacy connect
        public void ConnectPort(short sPort)
        {
            try
            {
                // fire-and-wait for a short timeout to provide compatibility
                var ok = ConnectAsync(sPort, 5000).GetAwaiter().GetResult();
                ConnectionStatus = ok;
            }
            catch (Exception ex)
            {
                ConnectionStatus = false;
                ErrorMessage = ex.Message;
            }
        }

        // Legacy SendCommand synchronous facade - posts command and waits briefly for response
        public void SendCommand(string cmd)
        {
            try
            {
                // best-effort: fire and forget but attempt to wait shortly
                var t = SendCommandAsync(cmd, 1000);
                try { t.Wait(800); } catch { }
            }
            catch { }
        }

        // Legacy SetSpeed implementation
        public void SetSpeed(double speed)
        {
            try
            {
                LogInfo($"Stage speed set: {speed}", "Stage.Config");
                // send set speed command and optionally query
                var t = SendCommandAsync("V" + ((int)speed).ToString() + "\r", 500);
                try { t.Wait(500); } catch { }
            }
            catch (Exception ex)
            {
                WarningMessage = ex.Message;
            }
        }

        // Legacy CalculatePulseEquiv wrapper
        // Keep name used by Engine
        public void CalculatePulseEquivLegacy()
        {
            CalculatePulseEquiv();
        }

        // Provide a Delay method expected by Engine
        public void Delay(long milliSecond = 500)
        {
            try { Thread.Sleep((int)milliSecond); } catch { }
        }

        internal void ConvertDistanceToSteps(double distance, int axis)
        {
            try
            {
                if (this.PulseEquivalent != 0)
                {
                    if (axis == 0)
                    {
                        this.CurrentXStep = Convert.ToInt64(distance * this.PulseEquivalent);
                    }
                    else if (axis == 1)
                    {
                        this.CurrentYStep = Convert.ToInt64(distance * this.PulseEquivalent);
                    }
                    else
                    {
                        this.CurrentYStep = Convert.ToInt64(distance * this.PulseEquivalent);
                    }

                }
                else
                {
                    if (axis == 0)
                    {
                        this.CurrentXStep = Convert.ToInt64(distance * 0.005);
                    }
                    else
                    {
                        this.CurrentYStep = Convert.ToInt64(distance * 0.005);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        // Public calculation method matching original API name
        public void CalculatePulseEquiv()
        {
            try
            {
                if (this.MotorDegree == 0)
                {
                    this.WarningMessage = "Stepper Angle is 0";
                    return;
                }
                if (this.LeadScrewPitch == 0)
                {
                    this.WarningMessage = "Lead Screw Pitch is 0";
                    return;
                }
                if (this.Subdivision == 0)
                {
                    this.WarningMessage = "Subdivision is 0";
                    return;
                }
                double stepsPerRevolution = 360 / MotorDegree;
                double microsteppedSteps = stepsPerRevolution * Subdivision;
                this.PulseEquivalent = microsteppedSteps / LeadScrewPitch;
                this.WarningMessage = "Pulse Equivalent:" + this.PulseEquivalent.ToString();
                LogInfo($"Pulse equivalent calculated: {this.PulseEquivalent:F6}", "Stage.Config");
            }
            catch (Exception ex)
            {
                this.ErrorMessage = ex.Message;
            }
        }

        // Compat helpers for CurrentXStep used elsewhere
        public long CurrentXStep { get; private set; }

        public bool ConnectionStatus
        {
            get => _connected;
            private set
            {
                if (_connected != value)
                {
                    _connected = value;
                    OnPropertyChanged(nameof(ConnectionStatus));
                }
            }
        }

        public long CurrentYStep
        {
            get => _currentYStep;
            private set
            {
                if (_currentYStep != value)
                {
                    _currentYStep = value;
                    OnPropertyChanged(nameof(CurrentYStep));
                }
            }
        }

        public double CurrentYPosition
        {
            get => _currentYPosition;
            private set
            {
                if (Math.Abs(_currentYPosition - value) > 1e-9)
                {
                    _currentYPosition = value;
                    OnPropertyChanged(nameof(CurrentYPosition));
                }
            }
        }

        public double MotorDegree
        {
            get => DblMotorDegree;
            internal set { DblMotorDegree = value; OnPropertyChanged(nameof(MotorDegree)); }
        }
        public double LeadScrewPitch
        {
            get => DblLeadScrewPitch;
            internal set { DblLeadScrewPitch = value; OnPropertyChanged(nameof(LeadScrewPitch)); }
        }
        public double Subdivision
        {
            get => DblSubDivision;
            internal set { DblSubDivision = value; OnPropertyChanged(nameof(Subdivision)); }
        }
        public double PulseEquivalent
        {
            get => DblPulseEqui;
            set 
            { 
                if (Math.Abs(DblPulseEqui - value) > 1e-9)
                {
                    DblPulseEqui = value;
                    OnPropertyChanged(nameof(PulseEquivalent));
                    LogInfo($"Pulse equivalent set: {DblPulseEqui:F6}", "Stage.Config");
                }
            }
        }

        // Open serial port and start worker; returns true if controller responded OK
        public async Task<bool> ConnectAsync(short sPort, int timeoutMs = 5000)
        {
            LogInfo($"Connecting to motion controller: COM{sPort}...", "Connection.Event");

            try
            {
                // Close existing
                ClosePort();

                _serial = new SerialPort();
                _serial.PortName = "COM" + sPort.ToString();
                _serial.BaudRate = 9600;
                _serial.DataBits = 8;
                _serial.StopBits = StopBits.One;
                _serial.Parity = Parity.None;
                _serial.ReadBufferSize = 4096;
                _serial.WriteBufferSize = 2048;
                _serial.DtrEnable = true;
                _serial.Handshake = Handshake.None;
                _serial.RtsEnable = false;

                _serial.DataReceived += Serial_DataReceived;

                // Open with a small retry in case port is transiently busy
                try
                {
                    _serial.Open();
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    ErrorMessage = "Access denied opening serial port: " + uaEx.Message;
                    ConnectionStatus = false;
                    LogError($"Connection failed: Access denied - {uaEx.Message}", "Connection.Event");
                    return false;
                }
                catch (IOException ioEx)
                {
                    ErrorMessage = "IO error opening serial port: " + ioEx.Message;
                    ConnectionStatus = false;
                    LogError($"Connection failed: IO error - {ioEx.Message}", "Connection.Event");
                    return false;
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Unexpected error opening serial port: " + ex.Message;
                    ConnectionStatus = false;
                    LogError($"Connection failed: {ex.Message}", "Connection.Event");
                    return false;
                }

                // start command worker
                _cmdCts = new CancellationTokenSource();
                _cmdWorker = Task.Run(() => CommandWorkerLoop(_cmdCts.Token));

                // small delay for port to settle
                await Task.Delay(50);

                // flush any pending input
                try { _serial.DiscardInBuffer(); _serial.DiscardOutBuffer(); } catch { }

                // Try a few handshake variants and timeouts to accommodate different firmware
                string[] candidates = new[] { "?R\r", "?R\n", "?R", "ID\r", "*IDN?\r" };
                List<string> attemptErrors = new List<string>();
                bool handshakeOk = false;
                foreach (var cmd in candidates)
                {
                    try
                    {
                        var resp = await SendCommandAsync(cmd, timeoutMs);
                        if (!string.IsNullOrEmpty(resp) && (resp.Contains("OK") || resp.Trim().Length > 0))
                        {
                            handshakeOk = true;
                            break;
                        }
                        else
                        {
                            attemptErrors.Add($"No response for '{cmd}'");
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        attemptErrors.Add($"Timeout waiting for response to '{cmd}'");
                    }
                    catch (Exception ex)
                    {
                        attemptErrors.Add($"Error for '{cmd}': {ex.Message}");
                    }
                }

                if (handshakeOk)
                {
                    ConnectionStatus = true;
                    StartPositionPolling();
                    LogInfo("Motion controller connected successfully", "Connection.Event");
                    return true;
                }
                else
                {
                    ErrorMessage = "Handshake failed: " + string.Join("; ", attemptErrors);
                    ConnectionStatus = false;
                    LogError($"Connection failed: Handshake failed", "Connection.Event");
                    try { _serial?.Close(); } catch { }
                    return false;
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = false;
                ErrorMessage = ex.Message;
                LogError($"Connection error: {ex.Message}", "Connection.Event");
                try { _serial?.Close(); } catch { }
                return false;
            }
        }

        // Get position synchronously/async with fallback to last polled cache
        public async Task<(double positionMm, double timestampSec)> GetPositionAsync(int timeoutMs = 30)
        {
            // If serial not available return last cached
            if (_serial == null || !_serial.IsOpen)
            {
                lock (_lastPosLock) { return (_lastPolledPositionMm, _lastPolledPositionTimestamp); }
            }

            try
            {
                var resp = await SendCommandAsync("?Y\r", timeoutMs).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(resp))
                {
                    var digits = Regex.Replace(resp, "[^0-9-]", "");
                    if (long.TryParse(digits, out long steps))
                    {
                        double pos = PulseEquivalent != 0 ? (double)steps / PulseEquivalent : (double)steps * 0.005;
                        double ts = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
                        // update cached values
                        lock (_lastPosLock)
                        {
                            _lastPolledPositionMm = pos;
                            _lastPolledPositionTimestamp = ts;
                        }
                        // also update public properties
                        CurrentYStep = steps;
                        CurrentYPosition = pos;
                        return (pos, ts);
                    }
                }
            }
            catch { /* ignore and fall back to cached value */ }

            lock (_lastPosLock) { return (_lastPolledPositionMm, _lastPolledPositionTimestamp); }
        }

        private void StartPositionPolling(int intervalMs = 100)
        {
            StopPositionPolling();
            _pollingCts = new CancellationTokenSource();
            var token = _pollingCts.Token;
            _pollingTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && ConnectionStatus)
                {
                    try
                    {
                        var resp = await SendCommandAsync("?Y\r", 500);
                        if (!string.IsNullOrEmpty(resp))
                        {
                            // parse numeric steps
                            var digits = Regex.Replace(resp, "[^0-9-]", "");
                            if (long.TryParse(digits, out long steps))
                            {
                                double pos = PulseEquivalent != 0 ? (double)steps / PulseEquivalent : (double)steps * 0.005;
                                double ts = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
                                lock (_lastPosLock)
                                {
                                    _lastPolledPositionMm = pos;
                                    _lastPolledPositionTimestamp = ts;
                                }
                                CurrentYStep = steps;
                                CurrentYPosition = pos;
                            }
                        }
                    }
                    catch { /* swallow transient errors */ }
                    await Task.Delay(intervalMs, token).ContinueWith(t => { });
                }
            }, token);
        }

        private void StopPositionPolling()
        {
            try
            {
                _pollingCts?.Cancel();
                _pollingTask = null;
                _pollingCts = null;
            }
            catch { }
        }

        private async Task CommandWorkerLoop(CancellationToken token)
        {
            try
            {
                foreach (var req in _cmdQueue.GetConsumingEnumerable(token))
                {
                    if (token.IsCancellationRequested) break;
                    if (_serial == null || !_serial.IsOpen)
                    {
                        req.Tcs.TrySetException(new InvalidOperationException("Serial port closed"));
                        continue;
                    }

                    // clear receive buffer
                    lock (_recvLock) { _recvBuffer = string.Empty; }

                    try
                    {
                        // track current request so Serial_DataReceived can complete it
                        _currentRequest = req;
                        _serial.Write(req.Command);
                        _serial.BaseStream.Flush();
                    }
                    catch (Exception ex)
                    {
                        req.Tcs.TrySetException(ex);
                        _currentRequest = null;
                        continue;
                    }

                    // wait for response or timeout
                    using var cts = new CancellationTokenSource(req.TimeoutMs);
                    using (cts.Token.Register(() => req.Tcs.TrySetCanceled()))
                    {
                        try
                        {
                            // Keep a longer delay to avoid hammering device between commands
                            await Task.Delay(200, token).ConfigureAwait(false);
                            var resp = await req.Tcs.Task.ConfigureAwait(false);
                            // response delivered via Serial_DataReceived; nothing more to do here
                        }
                        catch (TaskCanceledException)
                        {
                            req.Tcs.TrySetCanceled();
                        }
                        catch (Exception ex)
                        {
                            req.Tcs.TrySetException(ex);
                        }
                        finally
                        {
                            _currentRequest = null;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private CommandRequest? _currentRequest = null;

        private void Serial_DataReceived(object? sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serial == null) return;
                string chunk = _serial.ReadExisting();
                if (string.IsNullOrEmpty(chunk)) return;

                lock (_recvLock)
                {
                    _recvBuffer += chunk;
                    // if we have newline or OK or ERR, consider complete
                    if (_recvBuffer.Contains("\n") || _recvBuffer.Contains("OK") || _recvBuffer.Contains("ERR"))
                    {
                        var resp = _recvBuffer;
                        _recvBuffer = string.Empty;

                        // complete the current request if present
                        if (_currentRequest != null && !_currentRequest.Tcs.Task.IsCompleted)
                        {
                            _currentRequest.Tcs.TrySetResult(resp);
                        }
                        else
                        {
                            // Try to find a pending request to complete
                            CommandRequest? pending = null;
                            foreach (var q in _cmdQueue)
                            {
                                if (!q.Tcs.Task.IsCompleted) { pending = q; break; }
                            }
                            if (pending != null)
                            {
                                pending.Tcs.TrySetResult(resp);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public Task<string> SendCommandAsync(string command, int timeoutMs = 2000)
        {
            if (_serial == null || !_serial.IsOpen) return Task.FromException<string>(new InvalidOperationException("Serial port not open"));
            var req = new CommandRequest(command, timeoutMs);
            try
            {
                _cmdQueue.Add(req);
            }
            catch (Exception ex)
            {
                return Task.FromException<string>(ex);
            }
            return req.Tcs.Task;
        }

        /// <summary>
        /// Send stop command immediately by writing directly to serial port, bypassing the command queue.
        /// Minimal and synchronous to ensure lowest latency.
        /// </summary>
        public void ForceStop()
        {
            try
            {
                if (_serial != null && _serial.IsOpen)
                {
                    lock (_recvLock)
                    {
                        try
                        {
                            _serial.Write("S\r");
                            try { _serial.BaseStream.Flush(); } catch { }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        // Synchronous wrappers (blocking) for existing callers - these should be called off UI thread
        public void MoveYAbsolute(double yPos, bool flag = true)
        {
            try
            {
                if (flag) ConvertDistanceToSteps(yPos, 1);
                string commandString = (CurrentYStep > 0) ? "+" + CurrentYStep.ToString() : CurrentYStep.ToString();
                LogInfo($"Stage move commanded: {yPos:F3} mm ({CurrentYStep} steps)", "Stage.Event");
                // send command and do not wait long
                var task = SendCommandAsync("Y" + commandString + "\r", 2000);
                // don't block forever - wait briefly for acknowledgement
                try { task.Wait(1500); }
                catch { }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public void Stop()
        {
            try
            {
                LogInfo("Stage stop commanded", "Stage.Event");
                // Use immediate stop path instead of queuing to avoid delays
                ForceStop();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        public void ReturnYToOrigin()
        {
            try
            {
                LogInfo("Stage homing commanded", "Stage.Event");
                var t = SendCommandAsync("HY0\r", 2000);
                try { t.Wait(1000); } catch { }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        // Get Y position synchronously (legacy) - calls async and waits briefly
        private void GetYPosition()
        {
            try
            {
                var task = SendCommandAsync("?Y\r", 500);
                if (task.Wait(600))
                {
                    var resp = task.Result;
                    var digits = Regex.Replace(resp, "[^0-9-]", "");
                    if (long.TryParse(digits, out long steps))
                    {
                        CurrentYStep = steps;
                        CurrentYPosition = PulseEquivalent != 0 ? (double)steps / PulseEquivalent : (double)steps * 0.005;
                    }
                }
            }
            catch { }
        }

        public void GetYLocation()
        {
            GetYPosition();
        }

        public void HomeYStage()
        {
            try { ReturnYToOrigin(); } catch { }
        }

        public void ClosePort()
        {
            try
            {
                StopPositionPolling();
                if (_cmdCts != null)
                {
                    _cmdCts.Cancel();
                    _cmdCts = null;
                }
                if (_cmdWorker != null)
                {
                    _cmdWorker = null;
                }
                try { _serial?.Close(); } catch { }
                try { _serial?.Dispose(); } catch { }
                _serial = null;
                ConnectionStatus = false;
                // clear queue
                while (_cmdQueue.TryTake(out var _)) { }
            }
            catch { }
        }

        public void Dispose()
        {
            ClosePort();
        }

        /// <summary>
        /// Legacy immediate stop removed — use Stop() which posts via command queue to keep behavior stable.
        /// </summary>
    }
}
