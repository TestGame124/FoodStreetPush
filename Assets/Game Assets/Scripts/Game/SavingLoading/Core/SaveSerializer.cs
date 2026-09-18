using System;
using Newtonsoft.Json;
using UnityEngine;

namespace FoodStreet.Saving
{
    internal static class SaveSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,

            // Never resolve types named in the file. Even though the payload is
            // MAC-verified, a save format that can name .NET types to construct
            // is a liability that buys us nothing.
            TypeNameHandling = TypeNameHandling.None,

            // Fields added since a save was written just keep their defaults;
            // fields removed since are ignored rather than throwing.
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,

            // The model initialises its lists inline. Without Replace, Newtonsoft
            // appends the file's entries onto those instances instead of
            // swapping them, which silently duplicates everything on load.
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        private static readonly JsonSerializerSettings PrettySettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        internal static string Serialize(GameSaveData data, bool pretty = false)
        {
            return JsonConvert.SerializeObject(data, pretty ? PrettySettings : Settings);
        }

        internal static GameSaveData Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                GameSaveData data = JsonConvert.DeserializeObject<GameSaveData>(json, Settings);
                data?.EnsureComplete();
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveSystem] Save payload could not be parsed: {exception.Message}");
                return null;
            }
        }
    }
}
