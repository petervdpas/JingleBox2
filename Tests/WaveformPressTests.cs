using Avalonia.Input;
using JingleBox2.Rack.Controls;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which press moves a picture of a recording rather than touching what is drawn on it.
/// </summary>
/// <remarks>
/// One rule for every waveform in the application, which is the point of it: the left button on
/// its own already means something different in each of them, so the gesture that pans has to be
/// the same everywhere or it is a gesture nobody can keep in their head.
/// </remarks>
public class WaveformPressTests
{
    /// <summary>The rule under test.</summary>
    private readonly WaveformPress _press = new();

    /// <summary>The plain left button is whatever the picture itself does with it.</summary>
    [Fact]
    public void A_plain_press_is_not_a_pan()
    {
        Assert.False(_press.MeansPan(false, KeyModifiers.None));
    }

    /// <summary>The one that was asked for, and the one a two-button mouse can make.</summary>
    [Fact]
    public void Ctrl_pans()
    {
        Assert.True(_press.MeansPan(false, KeyModifiers.Control));
    }

    /// <summary>What the chop editor already answered, kept.</summary>
    [Fact]
    public void Shift_pans()
    {
        Assert.True(_press.MeansPan(false, KeyModifiers.Shift));
    }

    /// <summary>The button that needs no hand on the keyboard.</summary>
    [Fact]
    public void The_middle_button_pans()
    {
        Assert.True(_press.MeansPan(true, KeyModifiers.None));
    }

    /// <summary>A modifier this rule says nothing about leaves the press alone.</summary>
    [Fact]
    public void Alt_is_not_a_pan()
    {
        Assert.False(_press.MeansPan(false, KeyModifiers.Alt));
    }

    /// <summary>A second modifier held with one that pans still pans.</summary>
    [Fact]
    public void Ctrl_with_something_else_still_pans()
    {
        Assert.True(_press.MeansPan(false, KeyModifiers.Control | KeyModifiers.Alt));
    }
}
