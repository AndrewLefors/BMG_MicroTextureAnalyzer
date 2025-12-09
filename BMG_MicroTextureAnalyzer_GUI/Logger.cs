using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BMG_MicroTextureAnalyzer_GUI
{
    public enum LogLevel { Debug = 0, Info = 1, Warning = 2, Error = 3 }

    internal class LogEntry
    {
        public DateTime Time { get; set; }
        public LogLevel Level { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
    }

    public class Logger : IDisposable
    {
        private readonly ConcurrentQueue<LogEntry> _queue = new ConcurrentQueue<LogEntry>();
        private readonly TextBox _target;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly int _batchMs;
        private readonly int _maxLines;
        private Task _worker;

        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        public Logger(TextBox target, int batchMs = 200, int maxLines = 2000)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _batchMs = batchMs;
            _maxLines = maxLines;
        }

        public void Start()
        {
            if (_worker != null) return;
            _worker = Task.Run(() => WorkerLoop(_cts.Token));
        }

        public void Log(string message, LogLevel level = LogLevel.Info, string source = null)
        {
            if (message == null) return;
            if (level < MinimumLevel) return;
            _queue.Enqueue(new LogEntry { Time = DateTime.UtcNow, Level = level, Source = source ?? string.Empty, Message = message });
        }

        private async Task WorkerLoop(CancellationToken token)
        {
            var sb = new StringBuilder();
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // collect available items
                    bool any = false;
                    while (_queue.TryDequeue(out var e))
                    {
                        any = true;
                        sb.Append('[');
                        sb.Append(e.Time.ToString("HH:mm:ss.fff"));
                        sb.Append(']');
                        sb.Append(' ');
                        sb.Append(e.Level.ToString().ToUpper());
                        if (!string.IsNullOrEmpty(e.Source)) { sb.Append(' '); sb.Append('('); sb.Append(e.Source); sb.Append(')'); }
                        sb.Append(" : ");
                        sb.AppendLine(e.Message);
                    }

                    if (any)
                    {
                        var outStr = sb.ToString();
                        sb.Clear();
                        try
                        {
                            if (!_target.IsDisposed && _target.IsHandleCreated)
                            {
                                _target.BeginInvoke(new Action(() =>
                                {
                                    try
                                    {
                                        // Prepend new text so newest entries appear at the top
                                        try
                                        {
                                            // Efficient prepend: set Text to new + old
                                            string current = _target.Text ?? string.Empty;
                                            _target.Text = outStr + current;

                                            // Trim lines if too many (keep newest at top)
                                            try
                                            {
                                                var lines = _target.Lines ?? Array.Empty<string>();
                                                if (lines.Length > _maxLines)
                                                {
                                                    int keep = _maxLines;
                                                    var newLines = new string[keep];
                                                    Array.Copy(lines, 0, newLines, 0, keep);
                                                    _target.Lines = newLines;
                                                  }
                                            }
                                            catch { }

                                            // Move caret to start so top (newest) is visible
                                            try
                                            {
                                                _target.SelectionStart = 0;
                                                _target.ScrollToCaret();
                                            }
                                            catch { }
                                        }
                                        catch
                                        {
                                            // fallback to append if prepend fails for any reason
                                            _target.AppendText(outStr);
                                        }
                                    }
                                    catch { }
                                }));
                            }
                        }
                        catch { }
                    }

                    await Task.Delay(_batchMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(_batchMs, token).ConfigureAwait(false); }
            }
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                _worker?.Wait(500);
            }
            catch { }
            finally { _cts.Dispose(); }
        }
    }
}
