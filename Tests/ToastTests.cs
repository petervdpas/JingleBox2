using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using JingleBox2.Config;
using JingleBox2.UI;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which messages become toasts, how long they stand, and what the bus marks as one.
/// </summary>
public sealed class ToastTests
{
    /// <summary>A fixed moment, so standing is a sum and not a wait.</summary>
    private static readonly DateTime Noon = new(2026, 9, 15, 12, 0, 0);

    /// <summary>How long a toast stands in these tests.</summary>
    private static readonly TimeSpan Lasts = TimeSpan.FromSeconds(6);

    /// <summary>A toast said at a given moment.</summary>
    private static StatusMessage Toast(string text, StatusKind kind = StatusKind.Plain, double secondsIn = 0) =>
        new(text, kind, "", Noon.AddSeconds(secondsIn), Toast: true);

    /// <summary>Most of what is said is the bar's alone.</summary>
    [Fact]
    public void A_message_that_is_not_a_toast_is_left_to_the_bar()
    {
        var shelf = new ToastShelf();

        Assert.False(shelf.Take(new StatusMessage("Saved", StatusKind.Done, "", Noon)));
        Assert.Empty(shelf.Showing);
    }

    /// <summary>Nothing is not a toast.</summary>
    [Fact]
    public void A_null_message_is_not_taken()
    {
        var shelf = new ToastShelf();

        Assert.False(shelf.Take(null!));
        Assert.Empty(shelf.Showing);
    }

    /// <summary>A toast goes exactly when its time is up.</summary>
    [Fact]
    public void A_toast_stands_until_its_time_is_up_and_not_a_moment_longer()
    {
        var shelf = new ToastShelf();
        shelf.Take(Toast("Hello"));

        Assert.True(shelf.Settle(Noon.AddSeconds(5.9), Lasts));
        Assert.Single(shelf.Showing);

        Assert.False(shelf.Settle(Noon.AddSeconds(6), Lasts));
        Assert.Empty(shelf.Showing);
    }

    /// <summary>A fault outlives any time and goes only when clicked.</summary>
    [Fact]
    public void A_fault_stands_until_it_is_clicked()
    {
        var shelf = new ToastShelf();
        var fault = Toast("It broke", StatusKind.Fault);
        shelf.Take(fault);

        Assert.False(shelf.Settle(Noon.AddHours(1), Lasts));
        Assert.Single(shelf.Showing);

        shelf.Dismiss(fault);
        Assert.Empty(shelf.Showing);
    }

    /// <summary>The clock stops once only faults are left, and not before.</summary>
    [Fact]
    public void The_clock_keeps_going_while_an_ordinary_toast_stands_beside_a_fault()
    {
        var shelf = new ToastShelf();
        shelf.Take(Toast("It broke", StatusKind.Fault));
        shelf.Take(Toast("News", secondsIn: 1));

        Assert.True(shelf.Settle(Noon.AddSeconds(2), Lasts));
        Assert.False(shelf.Settle(Noon.AddSeconds(7), Lasts));
        Assert.Equal(new[] { "It broke" }, shelf.Showing.Select(m => m.Text));
    }

    /// <summary>Clicking one toast takes down that toast and no other.</summary>
    [Fact]
    public void A_clicked_toast_goes_and_the_others_stay()
    {
        var shelf = new ToastShelf();
        var first = Toast("One");
        shelf.Take(first);
        shelf.Take(Toast("Two"));

        shelf.Dismiss(first);

        Assert.Equal(new[] { "Two" }, shelf.Showing.Select(m => m.Text));
    }

    /// <summary>A toast that is not up cannot be taken down.</summary>
    [Fact]
    public void Dismissing_a_toast_that_is_not_showing_changes_nothing()
    {
        var shelf = new ToastShelf();
        shelf.Take(Toast("One"));

        shelf.Dismiss(Toast("Never shown"));

        Assert.Single(shelf.Showing);
    }

    /// <summary>The corner holds a limited number, and the oldest makes room.</summary>
    [Fact]
    public void The_oldest_goes_to_make_room()
    {
        var shelf = new ToastShelf();

        for (int i = 0; i < ToastShelf.MostShown + 2; i++) shelf.Take(Toast("Toast " + i, secondsIn: i));

        Assert.Equal(ToastShelf.MostShown, shelf.Showing.Count);
        Assert.Equal("Toast 2", shelf.Showing[0].Text);
        Assert.Equal("Toast " + (ToastShelf.MostShown + 1), shelf.Showing[^1].Text);
    }

    /// <summary>The same words said twice are one toast, newest and with its time started again.</summary>
    [Fact]
    public void The_same_words_again_are_one_toast_with_its_time_started_again()
    {
        var shelf = new ToastShelf();
        shelf.Take(Toast("Again"));
        shelf.Take(Toast("Other", secondsIn: 1));
        shelf.Take(Toast("Again", secondsIn: 5));

        Assert.Equal(new[] { "Other", "Again" }, shelf.Showing.Select(m => m.Text));

        shelf.Settle(Noon.AddSeconds(8), Lasts);

        Assert.Equal(new[] { "Again" }, shelf.Showing.Select(m => m.Text));
    }

    /// <summary>The same words as a warning are a different message from the same words as news.</summary>
    [Fact]
    public void The_same_words_as_another_kind_are_a_second_toast()
    {
        var shelf = new ToastShelf();
        shelf.Take(Toast("Plugin"));
        shelf.Take(Toast("Plugin", StatusKind.Warning));

        Assert.Equal(2, shelf.Showing.Count);
    }

    /// <summary>A toast goes through the bus like anything else, marked, with its link.</summary>
    [Fact]
    public void The_bus_marks_a_toast_and_carries_its_link_to_whoever_listens()
    {
        var bus = new StatusBus();
        var heard = new List<StatusMessage>();
        bus.Posted += (_, m) => heard.Add(m);

        bus.Say("Just the bar");
        bus.Toast("News", StatusKind.Done, "Releases", "https://example.org/r");

        Assert.False(heard[0].Toast);
        Assert.True(heard[1].Toast);
        Assert.Equal("https://example.org/r", heard[1].Link);
        Assert.Equal("News", bus.Last!.Text);
    }

    /// <summary>A toast with no words is not said at all.</summary>
    [Fact]
    public void A_blank_toast_says_nothing()
    {
        var bus = new StatusBus();
        int heard = 0;
        bus.Posted += (_, _) => heard++;

        bus.Toast("   ");

        Assert.Equal(0, heard);
        Assert.Null(bus.Last);
    }

    /// <summary>Words said in the bar and then as a toast are not folded into the plain one.</summary>
    [Fact]
    public void A_toast_after_the_same_words_in_the_bar_is_still_a_toast()
    {
        var bus = new StatusBus();
        bus.Say("Same");
        bus.Toast("Same");

        Assert.Equal(2, bus.Recent.Count);
        Assert.True(bus.Last!.Toast);
    }

    /// <summary>How long a toast stands is written inside its two ends.</summary>
    [Theory]
    [InlineData(0.5, AppConfig.LeastToastSeconds)]
    [InlineData(600, AppConfig.MostToastSeconds)]
    [InlineData(5, 5)]
    public void How_long_a_toast_stands_is_held_inside_its_ends_when_written(double set, double written)
    {
        var cfg = new AppConfig { ToastSeconds = set };

        var json = new ConfigStore().Written(cfg);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(written, doc.RootElement.GetProperty(nameof(AppConfig.ToastSeconds)).GetDouble());
    }

    /// <summary>A fresh installation looks for releases and toasts for the default time.</summary>
    [Fact]
    public void A_fresh_installation_looks_for_releases_and_toasts_for_six_seconds()
    {
        var cfg = new AppConfig();

        Assert.True(cfg.CheckForReleases);
        Assert.Equal(AppConfig.DefaultToastSeconds, cfg.ToastSeconds);
    }
}
