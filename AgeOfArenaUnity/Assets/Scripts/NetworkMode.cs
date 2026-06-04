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
/// ⚠️ DETERMINISM STATUS (2026-06, audit): the lockstep scaffolding (LockstepSystem,
/// FixedPoint, GridPathfinder, ChecksumSystem, CommandRecorder, DesyncHandler, TransportLayer)
/// is CHECKED IN BUT NOT INTEGRATED into the live simulation:
///   • LockstepSystem.StartLockstep() is never called → IsActive stays false → OnSimTick is inert.
///   • GameManager.FixedStepEnabled stays false → the sim runs on Time.deltaTime, not FIXED_DT.
///   • FixedPoint has zero callers; GridPathfinder.FindPath is never queried (units use NavMeshAgent).
///   • ChecksumSystem hashes float NavMesh positions, so it is not a true determinism guarantee.
/// In other words the game is SINGLE-PLAYER and NOT yet deterministic. MP2 (the determinism
/// pre-req below) is the work that wires this up. Don't treat the scaffolding as "MP done".
///
/// This file is a design decision record. IsMultiplayerEnabled stays false until MP2 is green.
/// </summary>
public static class NetworkMode
{
    public const string ChosenArchitecture = "Lockstep";
    public const bool IsMultiplayerEnabled = false; // flip when MP2 (determinism) is actually integrated
}
