using System;
using System.Threading;
using System.Threading.Tasks;
using CommandInterfaceXPS;
using Newport.Communication.TCPIP;

/// <summary>
/// Newport XPS Stage Controller — M-IMS300V
/// Verified against actual Newport.XPS.CommandInterface.dll method signatures.
///
/// SETUP:
///   1. Add Newport.XPS.CommandInterface.dll as a reference in your project
///   2. Set PositionerName to match your System.ini (XPS web GUI → System → System.ini)
///   3. Set IpAddress if different from default 192.168.0.254
///
/// USAGE:
///   var stage = new XpsStageController();
///   stage.PositionChanged += pos => lblPosition.Invoke(() => lblPosition.Text = $"{pos:F4} mm");
///   stage.StateChanged    += state => lblStatus.Invoke(() => lblStatus.Text = state.ToString());
///   stage.ErrorOccurred   += msg => MessageBox.Show(msg);
///   await stage.ConnectAsync();
///   await stage.InitializeAsync();
///   stage.SetVelocity(5.0);
///   await stage.MoveAbsoluteAsync(25.0);
/// </summary>
public class XpsStageController : IDisposable
{
    // =========================================================================
    // CONFIG
    // =========================================================================

    /// <summary>XPS controller IP address. Default: 192.168.0.254</summary>
    public string IpAddress { get; set; } = "192.168.0.254";

    /// <summary>
    /// Full positioner name from System.ini.
    /// Check via XPS web GUI → System → System.ini
    /// Example: "Group1.Pos"
    /// </summary>
    public string PositionerName { get; set; } = "Group1.Pos";

    /// <summary>TCP port. Default 5001.</summary>
    public int Port { get; set; } = 5001;

    /// <summary>Socket timeout in ms.</summary>
    public int TimeoutMs { get; set; } = 10000;

    /// <summary>Position polling interval in ms. 50ms = 20Hz.</summary>
    public int PollingIntervalMs { get; set; } = 50;

    // =========================================================================
    // MOTION PARAMETERS — safe defaults for M-IMS300V (max speed 20 mm/s)
    // =========================================================================

    private double _velocity     = 5.0;   // mm/s
    private double _acceleration = 10.0;  // mm/s²
    private double _minJerkTime  = 0.0;
    private double _maxJerkTime  = 0.0;

    public double Velocity     => _velocity;
    public double Acceleration => _acceleration;

    // =========================================================================
    // INTERNAL — two XPS instances replace the two-socket pattern
    //   _cmdXps  — command socket (moves, parameter sets)
    //   _pollXps — dedicated polling socket (never blocked by moves)
    // =========================================================================

    private XPS _cmdXps  = new XPS();
    private XPS _pollXps = new XPS();

    private CancellationTokenSource _pollCts;
    private Task                    _pollTask;

    // Group name derived from positioner name (everything before last '.')
    private string GroupName => PositionerName.Contains(".")
        ? PositionerName.Substring(0, PositionerName.LastIndexOf('.'))
        : PositionerName;

    // =========================================================================
    // STATE
    // =========================================================================

    private double _currentPosition = double.NaN;
    private bool   _isConnected     = false;
    private bool   _isInitialized   = false;
    private bool   _isMoving        = false;

    public double CurrentPosition => _currentPosition;
    public bool   IsConnected     => _isConnected;
    public bool   IsInitialized   => _isInitialized;
    public bool   IsMoving        => _isMoving;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// <summary>
    /// Fires on every position poll. Background thread — use Invoke() on WinForms.
    /// </summary>
    public event Action<double> PositionChanged;

    /// <summary>Fires when a move completes, is cancelled, or errors.</summary>
    public event Action<MoveResult> MoveCompleted;

    /// <summary>Fires on any controller or communication error.</summary>
    public event Action<string> ErrorOccurred;

    /// <summary>Fires on stage state changes.</summary>
    public event Action<StageState> StateChanged;

    /// <summary>
    /// Fires when velocity or acceleration changes.
    /// Wire to GUI sliders/fields to keep them in sync.
    /// Args: (velocity mm/s, acceleration mm/s²)
    /// </summary>
    public event Action<double, double> MotionParametersChanged;

    // =========================================================================
    // CONSTRUCTORS
    // =========================================================================

    public XpsStageController() { }

    public XpsStageController(string ipAddress, string positionerName,
                               double velocityMmPerSec      = 5.0,
                               double accelerationMmPerSec2 = 10.0)
    {
        IpAddress      = ipAddress;
        PositionerName = positionerName;
        _velocity      = velocityMmPerSec;
        _acceleration  = accelerationMmPerSec2;
    }

    // =========================================================================
    // CONNECTION
    // =========================================================================

    /// <summary>
    /// Opens two independent connections to the XPS controller.
    /// _cmdXps handles all motion commands.
    /// _pollXps is dedicated to position polling so reads never block on moves.
    /// </summary>
    public async Task ConnectAsync()
    {
        await Task.Run(() =>
        {
            // OpenInstrument(string ip, int port, int timeoutMs)
            int result = _cmdXps.OpenInstrument(IpAddress, Port, TimeoutMs);
            if (result != 0)
                throw new InvalidOperationException(
                    $"XPS: Failed to open command connection to {IpAddress}:{Port} (code {result}). " +
                    $"Check IP address and that controller is powered on.");

            result = _pollXps.OpenInstrument(IpAddress, Port, TimeoutMs);
            if (result != 0)
                throw new InvalidOperationException(
                    $"XPS: Failed to open polling connection (code {result}).");

            _isConnected = true;
            RaiseState(StageState.Connected);
        });
    }

    public void Disconnect()
    {
        StopPolling();
        _cmdXps?.CloseInstrument();
        _pollXps?.CloseInstrument();
        _isConnected   = false;
        _isInitialized = false;
        RaiseState(StageState.Disconnected);
    }

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    /// <summary>
    /// Kill → Initialize → Home Search → Apply motion parameters.
    /// Must complete before any move commands.
    /// Stage will physically move to find the home reference position.
    /// </summary>
    public async Task InitializeAsync()
    {
        EnsureConnected();
        RaiseState(StageState.Initializing);

        await Task.Run(() =>
        {
            string err;

            // Kill clears any latched errors — ignore result
            _cmdXps.GroupKill(GroupName, out err);

            // GroupInitialize(string groupName, out string errorString)
            CheckResult(
                _cmdXps.GroupInitialize(GroupName, out err),
                err, "GroupInitialize");

            // GroupHomeSearch(string groupName, out string errorString)
            CheckResult(
                _cmdXps.GroupHomeSearch(GroupName, out err),
                err, "GroupHomeSearch");

            // Push velocity/acceleration to controller
            //ApplyMotionParameters();

            _isInitialized = true;
            RaiseState(StageState.Ready);
        });

        StartPolling();
    }

    // =========================================================================
    // DYNAMIC MOTION PARAMETER CONTROL
    // =========================================================================

    /// <summary>
    /// Set velocity in mm/s. Takes effect immediately — safe to call at runtime.
    /// M-IMS300V maximum: 20 mm/s.
    /// </summary>
    public void SetVelocity(double velocityMmPerSec)
    {
        if (velocityMmPerSec <= 0)
            throw new ArgumentOutOfRangeException(nameof(velocityMmPerSec),
                "Velocity must be greater than 0.");
        if (velocityMmPerSec > 20.0)
            throw new ArgumentOutOfRangeException(nameof(velocityMmPerSec),
                "M-IMS300V maximum velocity is 20 mm/s.");

        _velocity = velocityMmPerSec;
        if (_isInitialized) ApplyMotionParameters();
        MotionParametersChanged?.Invoke(_velocity, _acceleration);
    }

    /// <summary>
    /// Set acceleration in mm/s². Takes effect immediately — safe to call at runtime.
    /// </summary>
    public void SetAcceleration(double accelerationMmPerSec2)
    {
        if (accelerationMmPerSec2 <= 0)
            throw new ArgumentOutOfRangeException(nameof(accelerationMmPerSec2),
                "Acceleration must be greater than 0.");

        _acceleration = accelerationMmPerSec2;
        if (_isInitialized) ApplyMotionParameters();
        MotionParametersChanged?.Invoke(_velocity, _acceleration);
    }

    /// <summary>
    /// Set velocity and acceleration in one call — preferred when changing both.
    /// </summary>
    public void SetMotionParameters(double velocityMmPerSec, double accelerationMmPerSec2)
    {
        if (velocityMmPerSec      <= 0) throw new ArgumentOutOfRangeException(nameof(velocityMmPerSec));
        if (accelerationMmPerSec2 <= 0) throw new ArgumentOutOfRangeException(nameof(accelerationMmPerSec2));
        if (velocityMmPerSec > 20.0)
            throw new ArgumentOutOfRangeException(nameof(velocityMmPerSec),
                "M-IMS300V maximum velocity is 20 mm/s.");

        _velocity     = velocityMmPerSec;
        _acceleration = accelerationMmPerSec2;
        if (_isInitialized) ApplyMotionParameters();
        MotionParametersChanged?.Invoke(_velocity, _acceleration);
    }

    /// <summary>
    /// Read velocity and acceleration directly from the controller.
    /// Call after InitializeAsync() to sync GUI fields to controller state.
    /// </summary>
    public (double velocity, double acceleration) GetMotionParametersFromController()
    {
        EnsureInitialized();
        string err;
        double vel, accel, minJerk, maxJerk;

        // PositionerSGammaParametersGet(string positioner,
        //   out double vel, out double accel, out double minJerk, out double maxJerk,
        //   out string err)
        CheckResult(
            _cmdXps.PositionerSGammaParametersGet(
                PositionerName, out vel, out accel, out minJerk, out maxJerk, out err),
            err, "PositionerSGammaParametersGet");

        _velocity     = vel;
        _acceleration = accel;
        return (vel, accel);
    }

    /// <summary>Pushes current _velocity/_acceleration to the controller.</summary>
    private void ApplyMotionParameters()
    {
        string err;

        // PositionerSGammaParametersSet(string positioner,
        //   double vel, double accel, double minJerk, double maxJerk,
        //   out string err)
        CheckResult(
            _cmdXps.PositionerSGammaParametersSet(
                PositionerName,
                _velocity, _acceleration,
                _minJerkTime, _maxJerkTime, out err),
            err, "PositionerSGammaParametersSet");
    }

    // =========================================================================
    // MOTION COMMANDS
    // =========================================================================

    /// <summary>
    /// Move to absolute position in mm. Awaitable and cancellable.
    /// Cancelling immediately sends a hardware abort.
    /// </summary>
    public async Task MoveAbsoluteAsync(double positionMm,
                                        CancellationToken ct = default)
    {
        EnsureInitialized();
        _isMoving = true;
        RaiseState(StageState.Moving);

        try
        {
            await Task.Run(() =>
            {
                string err;
                // GroupMoveAbsolute(string group, double[] positions, int nbElements, out string err)
                CheckResult(
                    _cmdXps.GroupMoveAbsolute(
                        GroupName, new double[] { positionMm }, 1, out err),
                    err, "GroupMoveAbsolute");
            }, ct);

            MoveCompleted?.Invoke(MoveResult.Completed);
        }
        catch (OperationCanceledException)
        {
            await AbortAsync();
            MoveCompleted?.Invoke(MoveResult.Cancelled);
        }
        catch (Exception ex)
        {
            RaiseError($"MoveAbsolute failed: {ex.Message}");
            MoveCompleted?.Invoke(MoveResult.Error);
        }
        finally
        {
            _isMoving = false;
            RaiseState(StageState.Ready);
        }
    }

    /// <summary>
    /// Move relative to current position in mm.
    /// Positive = downward on M-IMS300V (toward sample).
    /// </summary>
    public async Task MoveRelativeAsync(double displacementMm,
                                        CancellationToken ct = default)
    {
        EnsureInitialized();
        _isMoving = true;
        RaiseState(StageState.Moving);

        try
        {
            await Task.Run(() =>
            {
                string err;
                // GroupMoveRelative(string group, double[] displacements, int nbElements, out string err)
                CheckResult(
                    _cmdXps.GroupMoveRelative(
                        GroupName, new double[] { displacementMm }, 1, out err),
                    err, "GroupMoveRelative");
            }, ct);

            MoveCompleted?.Invoke(MoveResult.Completed);
        }
        catch (OperationCanceledException)
        {
            await AbortAsync();
            MoveCompleted?.Invoke(MoveResult.Cancelled);
        }
        catch (Exception ex)
        {
            RaiseError($"MoveRelative failed: {ex.Message}");
            MoveCompleted?.Invoke(MoveResult.Error);
        }
        finally
        {
            _isMoving = false;
            RaiseState(StageState.Ready);
        }
    }

    /// <summary>
    /// Immediate hardware stop. Safe to call at any time.
    /// Wire to your STOP button.
    /// </summary>
    public async Task AbortAsync()
    {
        if (!_isConnected) return;

        await Task.Run(() =>
        {
            string err;
            // GroupMoveAbort(string groupName, out string errorString)
            int result = _cmdXps.GroupMoveAbort(GroupName, out err);
            if (result != 0)
                RaiseError($"GroupMoveAbort returned {result}: {err}");
        });

        _isMoving = false;
        RaiseState(StageState.Ready);
    }

    /// <summary>
    /// Abort current motion then move to safe retract position.
    /// Default retract is 0.0 mm (home). Override as needed.
    /// </summary>
    public async Task RetractAsync(double retractPositionMm = 0.0)
    {
        await AbortAsync();
        await Task.Delay(100); // let controller process abort
        await MoveAbsoluteAsync(retractPositionMm);
    }

    // =========================================================================
    // POSITION POLLING
    // =========================================================================

    private void StartPolling()
    {
        StopPolling();
        _pollCts  = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoop(_pollCts.Token));
    }

    private void StopPolling()
    {
        _pollCts?.Cancel();
        try { _pollTask?.Wait(500); } catch { }
        _pollCts?.Dispose();
        _pollCts  = null;
        _pollTask = null;
    }

    private void PollLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isConnected)
        {
            try
            {
                double[] pos = new double[1];
                string   err;

                // GroupPositionCurrentGet(string group, out double[] pos, int nbElements, out string err)
                int result = _pollXps.GroupPositionCurrentGet(
                    GroupName, out pos, 1, out err);

                if (result == 0)
                {
                    _currentPosition = pos[0];
                    PositionChanged?.Invoke(pos[0]);
                }
                else
                {
                    RaiseError($"Position poll error {result}: {err}");
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Polling exception: {ex.Message}");
            }

            Thread.Sleep(PollingIntervalMs);
        }
    }

    // =========================================================================
    // STATUS
    // =========================================================================

    /// <summary>Returns group status code and human-readable description.</summary>
    public (int code, string description) GetGroupStatus()
    {
        EnsureConnected();
        int    status;
        string err, desc;

        // GroupStatusGet(string groupName, out int status, out string err)
        CheckResult(
            _cmdXps.GroupStatusGet(GroupName, out status, out err),
            err, "GroupStatusGet");

        // GroupStatusStringGet(int status, out string description, out string err)
        _cmdXps.GroupStatusStringGet(status, out desc, out err);
        return (status, desc);
    }

    /// <summary>Returns controller firmware version string.</summary>
    public string GetFirmwareVersion()
    {
        EnsureConnected();
        string version, err;

        // FirmwareVersionGet(out string version, out string err)
        CheckResult(
            _cmdXps.FirmwareVersionGet(out version, out err),
            err, "FirmwareVersionGet");

        return version;
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private void EnsureConnected()
    {
        if (!_isConnected)
            throw new InvalidOperationException(
                "XPS not connected. Call ConnectAsync() first.");
    }

    private void EnsureInitialized()
    {
        EnsureConnected();
        if (!_isInitialized)
            throw new InvalidOperationException(
                "XPS not initialized. Call InitializeAsync() first.");
    }

    private void CheckResult(int result, string errString, string functionName)
    {
        if (result != 0)
        {
            string msg = $"XPS {functionName} failed (code {result}): {errString}";
            RaiseError(msg);
            throw new InvalidOperationException(msg);
        }
    }

    private void RaiseError(string message) => ErrorOccurred?.Invoke(message);
    private void RaiseState(StageState state) => StateChanged?.Invoke(state);

    // =========================================================================
    // IDisposable
    // =========================================================================

    public void Dispose() => Disconnect();

    // =========================================================================
    // ENUMS
    // =========================================================================

    public enum StageState
    {
        Disconnected,
        Connected,
        Initializing,
        Ready,
        Moving
    }

    public enum MoveResult
    {
        Completed,
        Cancelled,
        Error
    }
}


// =============================================================================
// FRONT-END EXAMPLE — WinForms
// =============================================================================
//
// private XpsStageController _stage;
// private CancellationTokenSource _moveCts;
//
// async void Form_Load(object sender, EventArgs e)
// {
//     _stage = new XpsStageController
//     {
//         IpAddress      = "192.168.0.254",
//         PositionerName = "Group1.Pos",   // ← from XPS web GUI → System → System.ini
//     };
//
//     _stage.PositionChanged += pos =>
//         lblPosition.Invoke(() => lblPosition.Text = $"{pos:F4} mm");
//
//     _stage.StateChanged += state =>
//         this.Invoke(() =>
//         {
//             lblStatus.Text     = state.ToString();
//             bool moving        = state == XpsStageController.StageState.Moving;
//             btnMove.Enabled    = !moving;
//             btnStop.Enabled    =  moving;
//             btnRetract.Enabled =  true;
//         });
//
//     _stage.MotionParametersChanged += (vel, acc) =>
//         this.Invoke(() =>
//         {
//             trackVelocity.Value     = (int)(vel * 10);
//             trackAcceleration.Value = (int)(acc * 10);
//             lblVelocity.Text        = $"{vel:F1} mm/s";
//             lblAcceleration.Text    = $"{acc:F1} mm/s²";
//         });
//
//     _stage.ErrorOccurred += msg =>
//         MessageBox.Show(msg, "Stage Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
//
//     await _stage.ConnectAsync();
//     await _stage.InitializeAsync();
//
//     var (vel, acc) = _stage.GetMotionParametersFromController();
//     trackVelocity.Value     = (int)(vel * 10);
//     trackAcceleration.Value = (int)(acc * 10);
// }
//
// void trackVelocity_ValueChanged(object sender, EventArgs e)
//     => _stage?.SetVelocity(trackVelocity.Value / 10.0);
//
// void trackAcceleration_ValueChanged(object sender, EventArgs e)
//     => _stage?.SetAcceleration(trackAcceleration.Value / 10.0);
//
// async void btnMove_Click(object sender, EventArgs e)
// {
//     if (!double.TryParse(txtTarget.Text, out double target)) return;
//     _moveCts = new CancellationTokenSource();
//     await _stage.MoveAbsoluteAsync(target, _moveCts.Token);
// }
//
// async void btnStop_Click(object sender, EventArgs e)
// {
//     _moveCts?.Cancel();
//     await _stage.AbortAsync();
// }
//
// async void btnRetract_Click(object sender, EventArgs e)
// {
//     _moveCts?.Cancel();
//     await _stage.RetractAsync(0.0);
// }
//
// void Form_FormClosing(object sender, FormClosingEventArgs e)
//     => _stage?.Dispose();
