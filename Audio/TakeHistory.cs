using System.Collections.Generic;
using System.Linq;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class TakeHistory : ITakeHistory
{
    /// <summary>Backs <see cref="Steps"/>.</summary>
    private readonly List<TakeStep> _steps = new();

    /// <inheritdoc/>
    public IReadOnlyList<TakeStep> Steps => _steps;

    /// <inheritdoc/>
    public int Done { get; private set; }

    /// <inheritdoc/>
    public IEnumerable<TakeStep> Applied => _steps.Take(Done);

    /// <inheritdoc/>
    public bool CanUndo => Done > 0;

    /// <inheritdoc/>
    public bool CanRedo => Done < _steps.Count;

    /// <inheritdoc/>
    public void Add(TakeStep step)
    {
        if (Done < _steps.Count) _steps.RemoveRange(Done, _steps.Count - Done);

        _steps.Add(step);
        Done = _steps.Count;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (CanUndo) Done--;
    }

    /// <inheritdoc/>
    public void Redo()
    {
        if (CanRedo) Done++;
    }

    /// <inheritdoc/>
    public void GoTo(int done) => Done = Held(done);

    /// <inheritdoc/>
    public TakeWalk Toward(int done)
    {
        done = Held(done);

        if (done == Done) return TakeWalk.Nowhere;

        return done == Done + 1
            ? new TakeWalk(false, new[] { _steps[Done] })
            : new TakeWalk(true, _steps.Take(done).ToArray());
    }

    /// <summary>A place brought inside the list, since both ends of it are somewhere to stand.</summary>
    /// <param name="done">How many steps somebody asked for.</param>
    /// <returns>How many there really are to that point.</returns>
    private int Held(int done) => done < 0 ? 0 : done > _steps.Count ? _steps.Count : done;

    /// <inheritdoc/>
    public void Clear()
    {
        _steps.Clear();
        Done = 0;
    }
}
