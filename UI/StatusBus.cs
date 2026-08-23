using JingleBox2.Diagnostics;
using System;
using System.Collections.Generic;

namespace JingleBox2.UI;

/// <summary>How a message wants to be read.</summary>
public enum StatusKind
{
    /// <summary>Where you are and what is selected. The resting state of the bar.</summary>
    Context,

    /// <summary>Something happened. Most messages.</summary>
    Plain,

    /// <summary>Something worked, and saying so is worth a moment of green.</summary>
    Done,

    /// <summary>Something is not as expected, but nothing has broken.</summary>
    Warning,

    /// <summary>Something failed. These stay until the next thing, and go in the log as well.</summary>
    Fault
}

/// <summary>One thing that happened, with who said it and when.</summary>
public sealed record StatusMessage(string Text, StatusKind Kind, string From, DateTime At)
{
    public override string ToString() =>
        At.ToString("HH:mm:ss") + "  " + (From.Length > 0 ? From + ": " : "") + Text;
}

/// <summary>
/// Where everything in the app says where you are and what it has just done.
/// </summary>
/// <remarks>
/// Two different things share one bar, and the difference between them is the whole design.
///
/// The context is where you are: which song, which pattern, which line, which instrument. It is
/// true for as long as you are there and it changes as you move. It is what the bar says when
/// nothing has just happened, which is nearly always.
///
/// A message is something that has just happened, and it stands in front of the context for a
/// few seconds before the context comes back. A fault stands there until the next thing is
/// said, because a fault you had to catch inside four seconds is a fault you missed.
///
/// A bus rather than a property on a page, so anything can speak: an audio device that will not
/// open, a plugin that has died, a recording that could not be read. Those happen a long way
/// from any page, and several of them happen off the UI thread, which is what the lock is for.
/// </remarks>
public sealed class StatusBus
{
    /// <summary>How much is remembered. Enough to see what led to a fault, not a log file.</summary>
    public const int Remembered = 50;

    /// <summary>How long an ordinary message stands in front of the context.</summary>
    public static readonly TimeSpan Holds = TimeSpan.FromSeconds(4);

    private readonly object _lock = new();
    private readonly List<StatusMessage> _said = new();

    private string _context = "";

    /// <summary>Raised for every message, on whatever thread posted it.</summary>
    public event EventHandler<StatusMessage>? Posted;

    /// <summary>Raised when where you are changes.</summary>
    public event EventHandler<string>? ContextChanged;

    /// <summary>
    /// Where you are, now: whichever page is on screen keeps this true.
    /// </summary>
    public string Context
    {
        get { lock (_lock) return _context; }
        set
        {
            string wanted = (value ?? "").Trim();

            lock (_lock)
            {
                if (_context == wanted) return;

                _context = wanted;
            }

            ContextChanged?.Invoke(this, wanted);
        }
    }

    /// <summary>The last thing said, or null before anything has been.</summary>
    public StatusMessage? Last
    {
        get { lock (_lock) return _said.Count == 0 ? null : _said[^1]; }
    }

    /// <summary>What has been said lately, oldest first.</summary>
    public IReadOnlyList<StatusMessage> Recent
    {
        get { lock (_lock) return _said.ToArray(); }
    }

    public void Say(string text, string from = "") => Post(text, StatusKind.Plain, from);

    public void Done(string text, string from = "") => Post(text, StatusKind.Done, from);

    public void Warn(string text, string from = "") => Post(text, StatusKind.Warning, from);

    /// <summary>
    /// Something failed. Said out loud and written down, because a fault the user saw for four
    /// seconds and a fault anybody can go back and read are different things.
    /// </summary>
    public void Fault(string text, string from = "")
    {
        Post(text, StatusKind.Fault, from);

        Log.Write(LogArea.App, () => "status fault: " + (from.Length > 0 ? from + ": " : "") + text);
    }

    /// <summary>True while that message is still standing in front of the context.</summary>
    public static bool Holding(StatusMessage? message, DateTime now) =>
        message != null && (message.Kind == StatusKind.Fault || now - message.At < Holds);

    private void Post(string text, StatusKind kind, string from)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var message = new StatusMessage(text.Trim(), kind, from, DateTime.Now);

        lock (_lock)
        {
            // Saying the same thing twice running is one thing happening, not two. Replaced
            // rather than dropped, so its four seconds start again.
            if (_said.Count > 0 && _said[^1].Text == message.Text && _said[^1].Kind == message.Kind)
                _said.RemoveAt(_said.Count - 1);

            _said.Add(message);

            if (_said.Count > Remembered) _said.RemoveRange(0, _said.Count - Remembered);
        }

        Posted?.Invoke(this, message);
    }

    public void Clear()
    {
        lock (_lock) _said.Clear();
    }
}
