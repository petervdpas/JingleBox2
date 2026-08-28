using JingleBox2.Diagnostics;
using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

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

    /// <summary>Held while what has been said is read or written, since several threads speak.</summary>
    private readonly object _lock = new();

    /// <summary>What has been said lately, oldest first.</summary>
    private readonly List<StatusMessage> _said = new();

    /// <summary>Where you are, which is what the bar says when nothing has just happened.</summary>
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

    /// <summary>Something happened, which is most of what is ever said.</summary>
    /// <param name="text">What to say. A blank one says nothing.</param>
    /// <param name="from">Who is saying it, shown before the message.</param>
    public void Say(string text, string from = "") => Post(text, StatusKind.Plain, from);

    /// <summary>Something worked, and saying so is worth a moment of green.</summary>
    /// <param name="text">What to say. A blank one says nothing.</param>
    /// <param name="from">Who is saying it.</param>
    public void Done(string text, string from = "") => Post(text, StatusKind.Done, from);

    /// <summary>Something is not as expected, but nothing has broken.</summary>
    /// <param name="text">What to say. A blank one says nothing.</param>
    /// <param name="from">Who is saying it.</param>
    public void Warn(string text, string from = "") => Post(text, StatusKind.Warning, from);

    /// <summary>
    /// Something failed. Said out loud and written down, because a fault the user saw for four
    /// seconds and a fault anybody can go back and read are different things.
    /// </summary>
    /// <param name="text">What to say. A blank one says nothing.</param>
    /// <param name="from">Who is saying it.</param>
    public void Fault(string text, string from = "")
    {
        Post(text, StatusKind.Fault, from);

        Log.Write(LogArea.App, () => "status fault: " + (from.Length > 0 ? from + ": " : "") + text);
    }

    /// <summary>True while that message is still standing in front of the context.</summary>
    /// <remarks>
    /// A fault stands until the next thing is said, because a fault you had to catch inside four
    /// seconds is a fault you missed. Told the time rather than reading the clock, so it can be
    /// put a question to without waiting.
    /// </remarks>
    /// <param name="message">The message, or null when nothing has been said.</param>
    /// <param name="now">The moment being asked about.</param>
    public static bool Holding(StatusMessage? message, DateTime now) =>
        message != null && (message.Kind == StatusKind.Fault || now - message.At < Holds);

    /// <summary>Writes one message down and tells whoever is listening.</summary>
    /// <remarks>
    /// Saying the same thing twice running is one thing happening rather than two. The first is
    /// replaced rather than the second dropped, so its four seconds start again.
    /// </remarks>
    private void Post(string text, StatusKind kind, string from)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var message = new StatusMessage(text.Trim(), kind, from, DateTime.Now);

        lock (_lock)
        {
            if (_said.Count > 0 && _said[^1].Text == message.Text && _said[^1].Kind == message.Kind)
                _said.RemoveAt(_said.Count - 1);

            _said.Add(message);

            if (_said.Count > Remembered) _said.RemoveRange(0, _said.Count - Remembered);
        }

        Posted?.Invoke(this, message);
    }

    /// <summary>Forgets what has been said. Where you are is untouched.</summary>
    public void Clear()
    {
        lock (_lock) _said.Clear();
    }
}
