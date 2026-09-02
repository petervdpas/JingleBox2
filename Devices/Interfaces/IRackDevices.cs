using System.Collections.Generic;
using JingleBox2.Rack.Faces;

namespace JingleBox2.Devices.Interfaces;

/// <summary>
/// What devices of one kind this installation has, held for the run.
/// </summary>
/// <remarks>
/// The registry reads the folders off the disc, at startup and again whenever the list is
/// rebuilt, and hands what it found here. Everything that shows a device, draws one, or asks
/// whether a song can have one comes through this rather than reading the disc again.
///
/// One contract for both worlds because it is one question asked twice: what is there, is this id
/// one of them, which one is it, and what does its face look like. It was two interfaces with the
/// same four members, and where a question is asked twice it eventually gets two answers.
///
/// What each world adds to it is its own: a machine has an engine kind a song writes down, and a
/// rack a machine can be taken off. An effect has neither, and asks nothing here that a machine
/// does not.
/// </remarks>
/// <typeparam name="T">The manifest a device of this kind is read into.</typeparam>
public interface IRackDevices<T> where T : class, IRackProject
{
    /// <summary>Takes what the registry read, replacing whatever was known before.</summary>
    /// <remarks>
    /// Everything read last time is forgotten. A device thrown out in SETTINGS has to be gone the
    /// moment the list is rebuilt, not at the next start.
    /// </remarks>
    /// <param name="found">What was read and had an engine behind it.</param>
    void Keep(IEnumerable<T> found);

    /// <summary>The device with that id, or nothing when this installation has not got it.</summary>
    /// <param name="id">The id a song, a chain or a link wrote down.</param>
    T? For(string? id);

    /// <summary>
    /// True when this installation has that device.
    /// </summary>
    /// <remarks>
    /// Asked before anything sounds. A song naming a device that is not here is silent and says
    /// so; what it must not do is quietly play nothing as though nothing were missing.
    /// </remarks>
    /// <param name="id">The id a song, a chain or a link wrote down.</param>
    bool Has(string? id);

    /// <summary>Every device of this kind the installation has, in the order they were read.</summary>
    IReadOnlyList<T> All { get; }

    /// <summary>
    /// That device's face, or nothing when there is none worth drawing.
    /// </summary>
    /// <remarks>
    /// A panel with nothing on it is nothing rather than an empty frame: a device whose face has
    /// never been laid out draws as the host's own plain panel instead, which is what a plugin
    /// gets.
    /// </remarks>
    /// <param name="id">The device to draw.</param>
    Panel? PanelFor(string? id);
}
