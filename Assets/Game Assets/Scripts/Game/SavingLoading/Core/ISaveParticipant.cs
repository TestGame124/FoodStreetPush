namespace FoodStreet.Saving
{
    /// <summary>
    /// Implemented by anything whose state lives in the scene rather than in a
    /// plain field - pots, cooking stations, the inventory, the build grid.
    ///
    /// <see cref="CaptureState"/> always runs on the main thread, immediately
    /// before a write, so implementations may touch Unity objects freely.
    /// <see cref="RestoreState"/> is called once during boot.
    ///
    /// Systems that own nothing but plain values (coins, XP, tutorial stage)
    /// do not need this - they write straight into <see cref="SaveSystem.Data"/>
    /// and call <see cref="SaveSystem.MarkDirty"/>.
    /// </summary>
    public interface ISaveParticipant
    {
        /// <summary>Lower runs first. Use it when one participant reads another's output.</summary>
        int SaveOrder { get; }

        void CaptureState(GameSaveData data);

        void RestoreState(GameSaveData data);
    }
}
