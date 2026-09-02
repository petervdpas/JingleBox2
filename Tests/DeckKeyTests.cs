using Avalonia.Input;
using JingleBox2.Views;
using JingleBox2.Views.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a keystroke asks the transport for, without a window or a keyboard.
/// </summary>
/// <remarks>
/// The rule was inside the main window's own handler, which is why the transport stopped working
/// the moment anything else was in front of you: a machine's panel, an effect off a chain, a
/// plugin's window. It is out here now, and this is it.
/// </remarks>
public class DeckKeyTests
{
    /// <summary>Space is the one every tracker and every desk puts it on.</summary>
    [Fact]
    public void Space_works_the_transport()
    {
        Assert.Equal(DeckWant.Toggle, DeckKeys.Wants(Key.Space, KeyModifiers.None, busy: false));
    }

    /// <summary>And Ctrl+R records, which is the other half of the same bar.</summary>
    [Fact]
    public void Control_r_records()
    {
        Assert.Equal(DeckWant.Record, DeckKeys.Wants(Key.R, KeyModifiers.Control, busy: false));
    }

    /// <summary>
    /// Neither, while the keyboard is somewhere a key means something else.
    /// </summary>
    /// <remarks>
    /// A space in a name is a space, and Ctrl+R in one is somebody typing. This is the whole of
    /// what <c>busy</c> is for, and it is the reason a rule with no window in it can still be
    /// right about a window.
    /// </remarks>
    [Fact]
    public void Nothing_while_something_else_wants_the_key()
    {
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.Space, KeyModifiers.None, busy: true));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.R, KeyModifiers.Control, busy: true));
    }

    /// <summary>A space with anything held is not the transport's.</summary>
    /// <remarks>
    /// Ctrl+Space and Shift+Space belong to whatever else wants them, and a transport that took
    /// every space bar whatever was held with it would be taking keys nobody offered it.
    /// </remarks>
    [Fact]
    public void A_space_with_a_modifier_is_somebody_elses()
    {
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.Space, KeyModifiers.Control, busy: false));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.Space, KeyModifiers.Shift, busy: false));
    }

    /// <summary>And R on its own is a note, a letter, or nothing: it is not record.</summary>
    [Fact]
    public void An_r_on_its_own_is_not_record()
    {
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.R, KeyModifiers.None, busy: false));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.R, KeyModifiers.Shift, busy: false));
    }

    /// <summary>Every other key is nobody's business here.</summary>
    [Fact]
    public void Anything_else_is_left_alone()
    {
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.Enter, KeyModifiers.None, busy: false));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.A, KeyModifiers.None, busy: false));
        Assert.Equal(DeckWant.None, DeckKeys.Wants(Key.S, KeyModifiers.Control, busy: false));
    }
}
