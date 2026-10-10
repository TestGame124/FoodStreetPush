namespace LastOrders.EditorTools
{
    /// <summary>
    /// Where the level's wildlife and weather are kept: the birds' and butterflies' meshes and prefabs,
    /// the cloud texture, the particles' materials. Built in the farm animals' way - vertex-painted on
    /// the buildings' LastOrders/Stylized material - and in the current look's own folder, as the rest
    /// of the generated art is.
    /// </summary>
    internal static class AmbienceKit
    {
        internal const string ContentFolder = "Assets/Game Assets/Content/LastOrders/Ambience";
        internal const string MeshFolder = ContentFolder + "/" + EnvKit.Style;
        internal const string BirdFolder = MeshFolder + "/Birds";
        internal const string InsectFolder = MeshFolder + "/Insects";
        internal const string EffectsFolder = MeshFolder + "/Fx";
        internal const string PrefabFolder = "Assets/Game Assets/Content/LastOrders/Prefabs/Ambience";

        internal const string RootName = "AmbientLife";
    }
}
