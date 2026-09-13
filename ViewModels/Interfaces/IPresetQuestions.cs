using System.Threading.Tasks;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// What has to be asked of somebody keeping or taking off a preset of their own.
/// </summary>
/// <remarks>
/// Four questions and no window in any of them, so the Menu lines that ask them can be put a
/// question to by a test that answers for the person.
/// </remarks>
public interface IPresetQuestions
{
    /// <summary>What to call the preset, or nothing when it was cancelled.</summary>
    /// <param name="machine">The machine it is a preset of.</param>
    /// <param name="suggested">The name the box opens with.</param>
    /// <param name="why">
    /// Why a preset is being kept, said above the question, where keeping one was not what was
    /// pressed. Nothing for Save as preset, which explains itself.
    /// </param>
    Task<string?> Name(string machine, string suggested, string why = "");

    /// <summary>Whether to replace a preset of yours already called that.</summary>
    /// <param name="name">The name that is taken.</param>
    Task<bool> Replace(string name);

    /// <summary>Whether to take a preset of yours off for good.</summary>
    /// <param name="name">The preset.</param>
    Task<bool> Delete(string name);

    /// <summary>Says why something could not be done.</summary>
    /// <param name="why">The sentence to say.</param>
    Task Refused(string why);
}
