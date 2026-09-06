using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The input is open while any page showing its meter is on screen.
/// </summary>
/// <remarks>
/// **This exists because the mixer's IN strip drew a meter that never moved.** Its reading is the
/// recorder's own level, the input was only opened while RECORD was in front, and with the input
/// closed that reading really is nought: nothing was broken and the meter was telling the truth.
/// Two pages show it now, so whether it is open is a count rather than a flag, and a count
/// written as a flag is the page that left last closing the input under the page still up.
///
/// The counting is what is asked about here. The delay before the input really closes is a clock
/// and belongs to the page, so what is pinned is that the last departure is what starts it and
/// an arrival before it fires calls it off.
/// </remarks>
public class InputWatchTests
{
    /// <summary>Counts what it was asked to do, and answers nothing about audio.</summary>
    private sealed class Counting : IInputWatch
    {
        /// <summary>How many pages say they are looking.</summary>
        private int _watching;

        /// <summary>Whether the input is open.</summary>
        public bool Open { get; private set; }

        /// <summary>How many times it has been opened, so reopening shows up.</summary>
        public int Opened { get; private set; }

        /// <summary>How many times the clock to close it has been started.</summary>
        public int Closings { get; private set; }

        /// <inheritdoc/>
        /// <remarks>
        /// An input that is already open is left alone, which is what the recorder itself does:
        /// <c>StartMonitoring</c> answers at once where it is already listening. Without that
        /// here, a page coming straight back would reopen the capture and lose the routing, and
        /// the test for the delay would be measuring the wrong thing.
        /// </remarks>
        public void Watch()
        {
            _watching++;
            _pending = false;

            if (_watching > 1 || Open) return;

            Open = true;
            Opened++;
        }

        /// <inheritdoc/>
        public void LetGo()
        {
            if (_watching > 0) _watching--;
            if (_watching > 0) return;

            _pending = true;
            Closings++;
        }

        /// <summary>Whether a close is waiting on the clock.</summary>
        private bool _pending;

        /// <summary>What the clock does when it fires.</summary>
        public void Elapse()
        {
            if (!_pending) return;

            _pending = false;

            if (_watching == 0) Open = false;
        }
    }

    /// <summary>One page looking opens the input.</summary>
    [Fact]
    public void One_page_looking_opens_the_input()
    {
        var watch = new Counting();

        watch.Watch();

        Assert.True(watch.Open);
        Assert.Equal(1, watch.Opened);
    }

    /// <summary>
    /// The second page does not reopen it, which is not tidiness.
    /// </summary>
    /// <remarks>
    /// Closing and opening the capture is where the routing is lost: the system wires a new
    /// capture stream to whatever it thinks best, so an input reopened is an input pointed
    /// somewhere else. Walking onto the mixer with RECORD still up must not do that.
    /// </remarks>
    [Fact]
    public void A_second_page_does_not_reopen_the_input()
    {
        var watch = new Counting();

        watch.Watch();
        watch.Watch();

        Assert.Equal(1, watch.Opened);
        Assert.True(watch.Open);
    }

    /// <summary>
    /// One page leaving while another is still looking leaves the input open.
    /// </summary>
    /// <remarks>
    /// The whole of what the count buys, and what a flag would have got wrong.
    /// </remarks>
    [Fact]
    public void One_page_leaving_does_not_close_it_under_the_other()
    {
        var watch = new Counting();

        watch.Watch();
        watch.Watch();

        watch.LetGo();
        watch.Elapse();

        Assert.True(watch.Open);
    }

    /// <summary>The last page leaving closes it.</summary>
    [Fact]
    public void The_last_page_leaving_closes_it()
    {
        var watch = new Counting();

        watch.Watch();
        watch.LetGo();
        watch.Elapse();

        Assert.False(watch.Open);
    }

    /// <summary>
    /// A page that comes straight back keeps the input, which is what the delay is for.
    /// </summary>
    /// <remarks>
    /// A theme swap and other re-templating detach a page and put it straight back, so the count
    /// reaches nought for a moment in the ordinary course of things. Closing there and then would
    /// lose the routing every time somebody changed the theme.
    /// </remarks>
    [Fact]
    public void A_page_that_comes_straight_back_keeps_the_input()
    {
        var watch = new Counting();

        watch.Watch();
        watch.LetGo();
        watch.Watch();
        watch.Elapse();

        Assert.True(watch.Open);
        Assert.Equal(1, watch.Opened);
    }

    /// <summary>Letting go more often than taking hold does not take the count below nought.</summary>
    /// <remarks>
    /// Reachable: a page detached without ever having been attached, and a view model swapped
    /// under a page that had already let go. Unguarded, the count goes negative and the next
    /// arrival does not open the input, which is a meter that never moves again.
    /// </remarks>
    [Fact]
    public void Letting_go_too_often_does_not_go_below_nothing()
    {
        var watch = new Counting();

        watch.LetGo();
        watch.LetGo();
        watch.Elapse();

        watch.Watch();

        Assert.True(watch.Open);
        Assert.Equal(1, watch.Opened);
    }
}
