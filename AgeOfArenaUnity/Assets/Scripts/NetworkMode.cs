/// <summary>
/// MP1: Multiplayer architecture decision — Lockstep vs Client-Server.
///
/// Decision: LOCKSTEP (deterministic simulation).
///
/// Rationale:
///   • AoE2 and most classic RTS use lockstep because it is bandwidth-efficient
///     (only inputs are sent, not state) and avoids authoritative-server cost.
///   • This codebase already uses random seeds (WorldRoot.mapSeed) and can be made
///     fully deterministic by: (a) replacing all Random.Range calls with a seeded
///     PRNG, (b) fixing NavMesh agent order, (c) using FixedUpdate instead of Update
///     for all gameplay ticks.
///   • MP2 (determinism pre-requisite) must be completed before any networking code
///     is added. See docs/10-multiplayer.md for the full checklist.
///   • MP3 (transport + lobby) requires a transport layer; Unity Netcode for GameObjects
///     or Mirror are the leading candidates.
///
/// This file is a design decision record — no runtime code until MP2 is green.
/// </summary>
public static class NetworkMode
{
    public const string ChosenArchitecture = "Lockstep";
    public const bool IsMultiplayerEnabled = false; // flip when MP2 is done
}
