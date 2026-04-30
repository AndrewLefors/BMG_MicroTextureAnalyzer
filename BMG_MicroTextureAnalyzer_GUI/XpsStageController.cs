using System;
using System.Threading;
using System.Threading.Tasks;
using CommandInterfaceXPS;
using Newport.Communication.TCPIP;

/// <summary>
/// Newport XPS Stage Controller — M-IMS300V
/// Three dedicated sockets: command, poll, abort.
/// Adaptive polling: fast during motion, slow when idle.
/// Abort stops the stage in place, no re-homing.
/// </summary>
public class XpsStageController : IDisposable
{
    // =========================================================================
    // CONFIG
    // =========================================================================

    public string IpAddress      { get; set; } = "192.168.254.254";
    public string PositionerName { get; set; } = "Group1.Pos";
    public int    Port           { get; set; } = 5001;
    public int    TimeoutMs      { get; set; } = 10000;

    /// <summary>Polling interval while stage is MOVING (ms). Default 50ms = 20Hz.</summary>
    public int ActivePollingIntervalMs { get; set; } = 50;

    /// <summary>Polling interval while stage is IDLE (ms). Default 500ms.</summary>
    public int IdlePollingIntervalMs { get; set; } = 500;

    // =========================================================================
    // MOTION PARAMETERS
    // =========================================================================

    private double _velocity     = 5.0;
    private double _acceleration = 10.0;
    private double _minJerkTime  = 0.0;
    private double _maxJerkTime  = 0.0;

    public double Velocity     => _velocity;
    public double Acceleration => _acceleration;

    // Firmware-enforced motion limits, populated at InitializeAsync via
    // PositionerMaximumVelocityAndAccelerationGet. NaN until init completes.
    private double _maxVelocityFw     = double.NaN;
    private double _maxAccelerationFw = double.NaN;
    public double MaxVelocity     => _maxVelocityFw;
    public double MaxAcceleration => _maxAccelerationFw;

    // =========================================================================
    // SOCKETS
    // _cmdXps   — blocked during moves, used for all motion commands
    // _pollXps  — dedicated position polling, never blocked
    // _abortXps — dedicated abort, always idle until needed
    // =========================================================================

    private readonly XPS _cmdXps   = new XPS();
    private readonly XPS _pollXps  = new XPS();
    private readonly XPS _abortXps = new XPS();

    private CancellationTokenSource _pollCts;
    private Task                    _pollTask;

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

    /// <summary>Fires on every position poll. Background thread — Invoke() on WinForms.</summary>
    public event Action<double>              PositionChanged;
    public event Action<MoveResult>          MoveCompleted;
    public event Action<string>              ErrorOccurred;
    public event Action<StageState>          StateChanged;
    public event Action<double, double>      MotionParametersChanged;

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

    public async Task ConnectAsync()
    {
        await Task.Run(() =>
        {
            int r;

            r = _cmdXps.OpenInstrument(IpAddress, Port, TimeoutMs);
            if (r != 0) throw new InvalidOperationException(
                $"XPS: command socket failed (code {r}) — check IP {IpAddress}:{Port}");

            r = _pollXps.OpenInstrument(IpAddress, Port, TimeoutMs);
            if (r != 0) throw new InvalidOperationException(
                $"XPS: poll socket failed (code {r})");

            r = _abortXps.OpenInstrument(IpAddress, Port, TimeoutMs);
            if (r != 0) throw new InvalidOperationException(
                $"XPS: abort socket failed (code {r})");

            _isConnected = true;
            RaiseState(StageState.Connected);
        });
    }

    public void Disconnect()
    {
        StopPolling();
        try { _cmdXps.CloseInstrument();  } catch { }
        try { _pollXps.CloseInstrument(); } catch { }
        try { _abortXps.CloseInstrument();} catch { }
        _isConnected   = false;
        _isInitialized = false;
        RaiseState(StageState.Disconnected);
    }

    // =========================================================================
    // INITIALIZATION  (Kill → Initialize → HomeSearch)
    // =========================================================================

    public async Task InitializeAsync()
    {
        EnsureConnected();
        RaiseState(StageState.Initializing);

        await Task.Run(() =>
        {
            string err;
            _cmdXps.GroupKill(GroupName, out err);

            CheckResult(_cmdXps.GroupInitialize(GroupName, out err),
                err, "GroupInitialize");

            CheckResult(_cmdXps.GroupHomeSearch(GroupName, out err),
                err, "GroupHomeSearch");

            // Query firmware-enforced motion limits for this positioner so the
            // UI can validate user-entered velocity/acceleration before sending.
            try
            {
                double maxVel, maxAccel;
                int code = _cmdXps.PositionerMaximumVelocityAndAccelerationGet(
                    PositionerName, out maxVel, out maxAccel, out err);
                if (code == 0)
                {
                    _maxVelocityFw = maxVel;
                    _maxAccelerationFw = maxAccel;
                }
                // If the call fails, leave _maxVelocityFw/_maxAccelerationFw as NaN
                // so the UI can fall back to a permissive validation path.
            }
            catch { }

            _isInitialized = true;
            RaiseState(StageState.Ready);
        });

        StartPolling();
    }

    // =========================================================================
    // MOTION PARAMETER CONTROL
    // =========================================================================

    public void SetVelocity(double v)
    {
        double vMax = double.IsNaN(_maxVelocityFw) ? 20.0 : _maxVelocityFw;
        if (v <= 0 || v > vMax) throw new ArgumentOutOfRangeException(nameof(v),
            $"Velocity must be 0 < v ≤ {vMax:F3} mm/s.");
        _velocity = v;
        if (_isInitialized) ApplyMotionParameters();
        MotionParametersChanged?.Invoke(_velocity, _acceleration);
    }

    public void SetAcceleration(double a)
    {
        double aMax = double.IsNaN(_maxAccelerationFw) ? double.MaxValue : _maxAccelerationFw;
        if (a <= 0 || a > aMax) throw new ArgumentOutOfRangeException(nameof(a),
            $"Acceleration must be 0 < a ≤ {aMax:F3} mm/s².");
        _acceleration = a;
        if (_isInitialized) ApplyMotionParameters();
        MotionParametersChanged?.Invoke(_velocity, _acceleration);
    }

    public void SetMotionParameters(double v, double a)
    {
        double vMax = double.IsNaN(_maxVelocityFw) ? 20.0 : _maxVelocityFw;
        double aMax = double.IsNaN(_maxAccelerationFw) ? double.MaxValue : _maxAccelerationFw;
        if (v <= 0 || v > vMax) throw new ArgumentOutOfRangeException(nameof(v),
            $"Velocity must be 0 < v ≤ {vMax:F3} mm/s.");
        if (a <= 0 || a > aMax) throw new ArgumentOutOfRangeException(nameof(a),
            $"Acceleration must be 0 < a ≤ {aMax:F3} mm/s².");
        _velocity     = v;
        _acceleration = a;
        if (_isInitialized) ApplyMotionParameters();
        MotionParametersChanged?.Invoke(_velocity, _acceleration);
    }

    public (double velocity, double acceleration) GetMotionParametersFromController()
    {
        EnsureInitialized();
        string err;
        double vel, accel, minJerk, maxJerk;
        CheckResult(
            _cmdXps.PositionerSGammaParametersGet(
                PositionerName, out vel, out accel, out minJerk, out maxJerk, out err),
            err, "PositionerSGammaParametersGet");
        _velocity     = vel;
        _acceleration = accel;
        return (vel, accel);
    }

    private void ApplyMotionParameters()
    {
        string err;
        CheckResult(
            _cmdXps.PositionerSGammaParametersSet(
                PositionerName, _velocity, _acceleration,
                _minJerkTime, _maxJerkTime, out err),
            err, "PositionerSGammaParametersSet");
    }

    // =========================================================================
    // MOTION COMMANDS
    // =========================================================================

    public async Task MoveAbsoluteAsync(double positionMm,
                                        CancellationToken ct = default)
    {
        EnsureInitialized();
        _isMoving = true;
        RaiseState(StageState.Moving);

        try
        {
            int code = 0;
            string err = null;

            await Task.Run(() =>
            {
                code = _cmdXps.GroupMoveAbsolute(
                    GroupName, new double[] { positionMm }, 1, out err);
            }, ct);

            if (code == 0)
                MoveCompleted?.Invoke(MoveResult.Completed);
            else if (IsAbortCode(code))
                MoveCompleted?.Invoke(MoveResult.Cancelled);
            else
            {
                RaiseError($"MoveAbsolute failed (code {code}): {err}");
                MoveCompleted?.Invoke(MoveResult.Error);
            }
        }
        catch (OperationCanceledException)
        {
            await AbortAsync();
            MoveCompleted?.Invoke(MoveResult.Cancelled);
        }
        catch (Exception ex)
        {
            RaiseError($"MoveAbsolute exception: {ex.Message}");
            MoveCompleted?.Invoke(MoveResult.Error);
        }
        finally
        {
            _isMoving = false;
            RaiseState(StageState.Ready);
        }
    }

    public async Task MoveRelativeAsync(double displacementMm,
                                        CancellationToken ct = default)
    {
        EnsureInitialized();
        _isMoving = true;
        RaiseState(StageState.Moving);

        try
        {
            int code = 0;
            string err = null;

            await Task.Run(() =>
            {
                code = _cmdXps.GroupMoveRelative(
                    GroupName, new double[] { displacementMm }, 1, out err);
            }, ct);

            if (code == 0)
                MoveCompleted?.Invoke(MoveResult.Completed);
            else if (IsAbortCode(code))
                MoveCompleted?.Invoke(MoveResult.Cancelled);
            else
            {
                RaiseError($"MoveRelative failed (code {code}): {err}");
                MoveCompleted?.Invoke(MoveResult.Error);
            }
        }
        catch (OperationCanceledException)
        {
            await AbortAsync();
            MoveCompleted?.Invoke(MoveResult.Cancelled);
        }
        catch (Exception ex)
        {
            RaiseError($"MoveRelative exception: {ex.Message}");
            MoveCompleted?.Invoke(MoveResult.Error);
        }
        finally
        {
            _isMoving = false;
            RaiseState(StageState.Ready);
        }
    }

    // =========================================================================
    // ABORT  — dedicated socket, always clear, never blocked
    // No RecoverAsync — GroupMoveAbort leaves group READY on M-IMS300V firmware.
    // Stage stops in place, encoder position preserved, next move fires immediately.
    // =========================================================================

    public async Task AbortAsync()
    {
        if (!_isConnected) return;

        await Task.Run(() =>
        {
            string err;
            int result;

            // 1. GroupMoveAbortFast — fastest stop
            result = _abortXps.GroupMoveAbortFast(GroupName, 10, out err);
            System.Diagnostics.Debug.WriteLine($"AbortFast: {result} {err}");
            if (result == 0 || result == -27) goto done;

            // 2. GroupMoveAbort — standard abort
            result = _abortXps.GroupMoveAbort(GroupName, out err);
            System.Diagnostics.Debug.WriteLine($"Abort: {result} {err}");
            if (result == 0 || result == -27) goto done;

            // 3. Kill — last resort, stage stops but needs re-init before next move
            result = _abortXps.GroupKill(GroupName, out err);
            System.Diagnostics.Debug.WriteLine($"Kill: {result} {err}");

        done:;
        });

        _isMoving = false;

        try
        {
            double[] pos;
            string err;
            if (_pollXps.GroupPositionCurrentGet(GroupName, out pos, 1, out err) == 0)
            {
                _currentPosition = pos[0];
                PositionChanged?.Invoke(pos[0]);
            }
        }
        catch { }

        RaiseState(StageState.Ready);
    }

    public async Task RetractAsync(double retractPositionMm = 0.0)
    {
        await AbortAsync();
        await Task.Delay(100);
        await MoveAbsoluteAsync(retractPositionMm);
    }

    // =========================================================================
    // ABORT CODE CHECK
    // These codes mean the move was intentionally interrupted — not a fault.
    // Do NOT call RecoverAsync for these.
    // =========================================================================

    private static bool IsAbortCode(int code) =>
        code == -27 ||   // ERR_GROUP_ABORT_MOTION
        code == -22 ||   // ERR_NOT_ALLOWED_ACTION (abort race)
        code == -1;      // generic interrupted

    // =========================================================================
    // ADAPTIVE POLLING LOOP
    // Fast (ActivePollingIntervalMs) while moving.
    // Slow (IdlePollingIntervalMs) while idle.
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
                double[] pos;
                string   err;

                int result = _pollXps.GroupPositionCurrentGet(
                    GroupName, out pos, 1, out err);

                if (result == 0)
                {
                    _currentPosition = pos[0];
                    PositionChanged?.Invoke(pos[0]);
                }
                else
                {
                    RaiseError($"Poll error {result}: {err}");
                }
            }
            catch (Exception ex)
            {
                RaiseError($"Poll exception: {ex.Message}");
            }

            // Adaptive interval — fast when moving, slow when idle
            Thread.Sleep(_isMoving ? ActivePollingIntervalMs : IdlePollingIntervalMs);
        }
    }

    // =========================================================================
    // STATUS
    // =========================================================================

    public (int code, string description) GetGroupStatus()
    {
        EnsureConnected();
        int    status;
        string err, desc;
        CheckResult(_cmdXps.GroupStatusGet(GroupName, out status, out err),
            err, "GroupStatusGet");
        _cmdXps.GroupStatusStringGet(status, out desc, out err);
        return (status, desc);
    }

    public string GetFirmwareVersion()
    {
        EnsureConnected();
        string version, err;
        CheckResult(_cmdXps.FirmwareVersionGet(out version, out err),
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

    private void CheckResult(int result, string errString, string fn)
    {
        if (result != 0)
        {
            string msg = $"XPS {fn} failed (code {result}): {errString}";
            RaiseError(msg);
            throw new InvalidOperationException(msg);
        }
    }

    private void RaiseError(string msg)  => ErrorOccurred?.Invoke(msg);
    private void RaiseState(StageState s) => StateChanged?.Invoke(s);

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
