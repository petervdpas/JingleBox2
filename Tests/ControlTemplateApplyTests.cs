using System.Linq;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Laying a template down again, which is what the surfaces line on a face does.
/// </summary>
/// <remarks>
/// The links a template is made of come straight off the desk, so laying it down hands
/// <see cref="ControlLink.Take"/> the very objects that are already in the list it is writing
/// into. Every one of them displaces whatever holds its control or points at its target, and it
/// itself is one of those, so the act is a removal followed by an addition of the same thing.
/// That has to leave the desk exactly as it was and has never been checked.
/// </remarks>
public class ControlTemplateApplyTests
{
    /// <summary>What a template on one effect looks like: four knobs, four parameters.</summary>
    private static ControlLink Desk()
    {
        var link = new ControlLink(new(), () => { });

        for (int knob = 0; knob < 4; knob++)
            link.Take(new[]
            {
                new ControlMapping
                {
                    Kind = ControlKind.SoundDevice,
                    Scope = ControlScope.Focused,
                    Machine = "effect.echobox",
                    Key = new[] { "time", "feedback", "damp", "mix" }[knob],
                    Device = "nanoKONTROL2",
                    Channel = 1,
                    Cc = 16 + knob
                }
            });

        return link;
    }

    /// <summary>A template taken off the desk and laid down again is still there.</summary>
    [Fact]
    public void Laying_a_template_down_again_leaves_it_where_it_was()
    {
        var link = Desk();

        var template = link.Desk.ToList();

        Assert.Equal(4, template.Count);

        Assert.Equal(4, link.Take(template));

        Assert.Equal(4, link.Desk.Count);
        Assert.Equal(
            template.Select(one => one.Key).OrderBy(one => one),
            link.Desk.Select(one => one.Key).OrderBy(one => one));
    }

    /// <summary>And twice more, since idempotence that holds once may not hold again.</summary>
    [Fact]
    public void Laying_it_down_three_times_changes_nothing()
    {
        var link = Desk();

        for (int again = 0; again < 3; again++) link.Take(link.Desk.ToList());

        Assert.Equal(4, link.Desk.Count);
        Assert.Equal(4, link.Desk.Select(one => one.Cc).Distinct().Count());
    }

    /// <summary>
    /// What the router reads is up to date the moment a template is laid down.
    /// </summary>
    /// <remarks>
    /// The merged list of the desk's links and the song's is kept until one of them moves, and a
    /// template arriving is one of the ways they move. Read before and after, so a stale merged
    /// list would show as a knob that does nothing until something else happens to touch the
    /// links.
    /// </remarks>
    [Fact]
    public void The_router_sees_a_template_the_moment_it_arrives()
    {
        var link = new ControlLink(new(), () => { });

        Assert.Empty(link.Mappings);

        link.Take(new[]
        {
            new ControlMapping
            {
                Kind = ControlKind.SoundDevice,
                Scope = ControlScope.Focused,
                Machine = "effect.echobox",
                Key = "time",
                Device = "nanoKONTROL2",
                Channel = 1,
                Cc = 16
            }
        });

        Assert.Single(link.Mappings);
        Assert.Equal("time", link.Mappings[0].Key);
    }

    /// <summary>One knob pointed somewhere else since is taken back by the template.</summary>
    [Fact]
    public void A_knob_pointed_elsewhere_since_is_taken_back()
    {
        var link = Desk();

        var template = link.Desk.ToList();

        link.Take(new[]
        {
            new ControlMapping
            {
                Kind = ControlKind.SoundDevice,
                Scope = ControlScope.Focused,
                Machine = "effect.echobox",
                Key = "mix",
                Device = "nanoKONTROL2",
                Channel = 1,
                Cc = 16
            }
        });

        Assert.Equal(4, link.Take(template));

        Assert.Equal("time", link.Desk.Single(one => one.Cc == 16).Key);
        Assert.Equal(4, link.Desk.Count);
    }
}
