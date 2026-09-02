using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JingleBox2.SoundDevices.SoundMachines.Records;

namespace JingleBox2.Views;

/// <summary>
/// The one dialog about machines this installation is not offering.
/// </summary>
/// <remarks>
/// One, and it takes what it is about. There were two, one raised on opening a song and one when
/// an instrument on such a machine was asked for, and each wrote out its own version of the same
/// two sentences: what is absent, and what that costs. Two spellings of one message eventually
/// disagree, and the way that fails here is one page calling a machine absent while the other
/// calls it shelved.
///
/// It says nothing about how to put it right. Somebody with a song full of machines knows this
/// application, and a sentence sending them to a page they already know is a sentence they read
/// every time to learn nothing.
/// </remarks>
public static class MissingSoundMachineDialog
{
    /// <summary>Says which machines are not being offered and what that costs.</summary>
    /// <remarks>
    /// The heading names them and why, since with one machine that is the whole message and the
    /// body is then the consequence alone. With several the heading cannot carry a reason each,
    /// so it says they are not being offered and the body gives one line apiece.
    /// </remarks>
    /// <param name="machines">What is not being offered. Nothing to say for an empty list.</param>
    public static Task ShowAsync(IReadOnlyList<MissingSoundMachine> machines)
    {
        if (machines is not { Count: > 0 }) return Task.CompletedTask;

        var wanted = machines.Where(machine => !machine.Registered).ToList();
        var shelved = machines.Where(machine => machine.Registered).ToList();

        string heading = wanted.Count > 0
            ? "This song needs a machine you have not got"
            : "This song uses a machine that is not on the rack";

        var said = new StringBuilder();

        foreach (var lot in new[] { wanted, shelved })
        {
            if (lot.Count == 0) continue;

            if (said.Length > 0) said.Append("\n\n");

            said.Append(lot[0].Label)
                .Append(": ")
                .Append(Listed(lot.Select(machine => machine.Name).ToList()))
                .Append(". The instruments on ")
                .Append(lot.Count == 1 ? "it make" : "them make")
                .Append(" no sound and have no panel.");
        }

        return ConfirmDialog.ErrorAsync("Machines", heading, said.ToString());
    }

    /// <summary>The same about one instrument, which names the instrument as well.</summary>
    /// <remarks>
    /// The machine is labelled in the heading because an instrument takes its machine's name
    /// unless somebody renames it, so the two are the same word more often than not and the
    /// heading alone would leave somebody wondering which of them is meant.
    /// </remarks>
    /// <param name="machine">What the instrument is on, or nothing to say nothing.</param>
    /// <param name="instrument">What the instrument is called.</param>
    public static Task ShowAsync(MissingSoundMachine? machine, string instrument)
    {
        if (machine is null) return Task.CompletedTask;

        return ConfirmDialog.ErrorAsync(
            "Machines",
            machine.Name + "(machine) " + machine.Because,
            "'" + instrument + "' is on it, so it has no panel and makes no sound.");
    }

    /// <summary>Names in a row, the way anybody would say them out loud.</summary>
    /// <param name="names">What to run together.</param>
    private static string Listed(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "",
        1 => names[0],
        _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1],
    };
}
