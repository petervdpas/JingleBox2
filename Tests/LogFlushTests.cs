using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// That a line written into the log is a line in the file, however many threads are emptying it.
/// </summary>
/// <remarks>
/// **This is the one part of the log the doctrine leaves untestable, and it is where the fault
/// was.** The rules came out into <c>ILogAreas</c>, <c>ILogLine</c> and <c>ILogFile</c>, each of
/// which can be asked without a process; what the door was left holding is a queue, a thread and
/// a file, and nothing stood in front of the handover between them.
///
/// There is always more than one flusher: the writing thread runs on its own clock and anything
/// may flush by hand, which the way out of the process does and clearing the log does. Two of
/// them inside at once each took a share of the queue and then opened the same file, and the one
/// that lost the open had its share swallowed along with the exception, because a log may not
/// throw in the thing it is a log of.
///
/// So the symptom was a line that was written and is not in the file, at random and under load,
/// which is the worst thing to be chasing with a log. It was found by a test of something else
/// entirely, which wrote two lines and read back one.
/// </remarks>
public class LogFlushTests
{
    /// <summary>How many lines are written, which has to be more than one batch's worth.</summary>
    private const int Lines = 2000;

    /// <summary>
    /// Nothing is lost when the queue is being emptied from several threads at once.
    /// </summary>
    /// <remarks>
    /// The hand-flushing is what a process on its way out does, and it runs beside the log's own
    /// thread rather than instead of it. Every line is numbered so a missing one can be named
    /// rather than counted, since a count that is short says nothing about which.
    /// </remarks>
    [Fact]
    public async Task Every_line_written_is_a_line_in_the_file()
    {
        string folder = Path.Combine(Path.GetTempPath(), "jb-log-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        try
        {
            Log.Open(folder, true, LogArea.Audio);

            var flushing = new CancellationTokenSource();

            var hands = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            {
                while (!flushing.IsCancellationRequested) Log.Flush();
            })).ToArray();

            for (int at = 0; at < Lines; at++) Log.Write(LogArea.Audio, "line " + at.ToString("0000"));

            Log.Flush();

            flushing.Cancel();
            await Task.WhenAll(hands);

            Log.Close();

            string written = File.ReadAllText(Path.Combine(folder, Log.FileName));

            var missing = Enumerable.Range(0, Lines)
                .Where(at => !written.Contains("line " + at.ToString("0000")))
                .Take(5)
                .ToArray();

            Assert.True(missing.Length == 0, "lost: " + string.Join(", ", missing));
        }
        finally
        {
            Log.Close();
            Directory.Delete(folder, true);
        }
    }
}
