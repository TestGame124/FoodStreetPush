#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using LastOrders.Audio;
using LastOrders.DayCycle;
using LastOrders.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A cue never playing the same take twice running, a button saying what it does, the ears landing
/// where the camera looks, the phase mask, the catalog's takes still being in the pack, and every
/// sound id having something to play.
/// </summary>
public class SoundTests
{
    const string LibraryPath = "Assets/Game Assets/Content/LastOrders/Audio/SoundLibrary.asset";

    [Test]
    public void ATakeIsNeverTheOnePlayedLast()
    {
        for (int count = 2; count <= 6; count++)
        for (int previous = 0; previous < count; previous++)
        {
            var hit = new HashSet<int>();
            for (float roll = 0f; roll < 1f; roll += 0.01f)
            {
                int index = SoundCue.PickIndex(count, previous, roll);
                Assert.That(index, Is.Not.EqualTo(previous), $"{count} takes, {previous} last");
                Assert.That(index, Is.InRange(0, count - 1));
                hit.Add(index);
            }

            Assert.That(hit.Count, Is.EqualTo(count - 1), "every other take comes up");
        }
    }

    [Test]
    public void OneTakeOrNoHistoryStillPicksSomething()
    {
        Assert.That(SoundCue.PickIndex(1, 0, 0.7f), Is.EqualTo(0));
        Assert.That(SoundCue.PickIndex(4, -1, 0.99f), Is.EqualTo(3));
        Assert.That(SoundCue.PickIndex(4, -1, 1f), Is.EqualTo(3), "a roll of exactly 1 stays in range");
        Assert.That(SoundCue.PickIndex(3, 7, 0f), Is.EqualTo(0), "a stale history is ignored");
    }

    [Test]
    public void ButtonsSoundLikeWhatTheyDo()
    {
        Assert.That(UiClickSounds.ForName("CloseBtn"), Is.EqualTo(SoundId.UiBack));
        Assert.That(UiClickSounds.ForName("Close Button"), Is.EqualTo(SoundId.UiBack));
        Assert.That(UiClickSounds.ForName("Cancel"), Is.EqualTo(SoundId.UiBack));
        Assert.That(UiClickSounds.ForName("ExpandButton"), Is.EqualTo(SoundId.UiConfirm));
        Assert.That(UiClickSounds.ForName("WorldUI_RepairPrompt"), Is.EqualTo(SoundId.UiConfirm));
        Assert.That(UiClickSounds.ForName("TakeawayTab"), Is.EqualTo(SoundId.UiTap), "'away' is not a way back");
        Assert.That(UiClickSounds.ForName("Crisis_Rally"), Is.EqualTo(SoundId.UiTap));
        Assert.That(UiClickSounds.ForName(null), Is.EqualTo(SoundId.UiTap));
    }

    [Test]
    public void TheEarsLandWhereTheCameraLooks()
    {
        Vector3 eye = new Vector3(10f, 30f, -5f);
        Vector3 down45 = new Vector3(0f, -1f, 1f).normalized;

        Vector3 focus = SoundListener.Focus(eye, down45, 0f);
        Assert.That(focus.y, Is.EqualTo(0f).Within(1e-4f));
        Assert.That(focus.x, Is.EqualTo(10f).Within(1e-4f));
        Assert.That(focus.z, Is.EqualTo(25f).Within(1e-3f), "30 down at 45 degrees is 30 along");

        Vector3 level = SoundListener.Focus(eye, Vector3.forward, 0f);
        Assert.That(level, Is.EqualTo(new Vector3(10f, 0f, -5f)), "looking level, the point under the camera stands in");
    }

    [Test]
    public void ThePhaseMaskHoldsOnlyItsPhases()
    {
        Assert.That(SoundPhases.Day.Includes(DayPhase.Day), Is.True);
        Assert.That(SoundPhases.Day.Includes(DayPhase.Dusk), Is.False);
        Assert.That((SoundPhases.Dusk | SoundPhases.Night).Includes(DayPhase.Night), Is.True);

        foreach (DayPhase phase in (DayPhase[])Enum.GetValues(typeof(DayPhase)))
        {
            Assert.That(SoundPhases.All.Includes(phase), Is.True, $"All includes {phase}");
            Assert.That(SoundPhases.None.Includes(phase), Is.False, $"None excludes {phase}");
        }
    }

    [Test]
    public void APatternNamesTheEndOfATakesPath()
    {
        var keys = new[]
        {
            "Cooking_Actions/Open_Close/Oven_Close_1",
            "Cooking_Actions/Open_Close/Toaster_Oven_Close_Resonate_Rattle_1",
            "Dishes/General Utensils/Small_Metal_Tongs_Clink_2",
            "zombies/zombie-12",
        };

        Assert.That(SoundSetup.Match(keys, "Open_Close/Oven_Close_*"), Is.EqualTo(new[] { "Cooking_Actions/Open_Close/Oven_Close_1" }),
                    "the toaster oven is not the oven");
        Assert.That(SoundSetup.Match(keys, "General Utensils/Small_Metal_Tongs_Clink_*"), Has.Count.EqualTo(1));
        Assert.That(SoundSetup.Match(keys, "zombies/zombie-*"), Has.Count.EqualTo(1));
        Assert.That(SoundSetup.Match(keys, "Close_*"), Is.Empty, "a pattern starts at a folder or name boundary");
    }

    [Test]
    public void EverySoundIsInTheCatalogOnce()
    {
        var seen = new HashSet<SoundId>();
        foreach (SoundCatalog.Spec spec in SoundCatalog.All)
        {
            Assert.That(seen.Add(spec.Id), Is.True, $"{spec.Id} is in the catalog twice");
            Assert.That(spec.Takes, Is.Not.Empty, $"{spec.Id} names no takes");
            Assert.That(spec.MaxDistance, Is.GreaterThan(spec.MinDistance), $"{spec.Id} fades out before it starts");
        }

        foreach (SoundId id in (SoundId[])Enum.GetValues(typeof(SoundId)))
            if (id != SoundId.None) Assert.That(seen, Does.Contain(id), $"{id} has no entry in the catalog");
    }

    [Test]
    public void EveryCatalogPatternStillFindsATakeInThePack()
    {
        var keys = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Game Assets/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Substring("Assets/Game Assets/Audio/".Length);
            string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("Cooking_Game_")) name = name.Substring("Cooking_Game_".Length);
            keys.Add(folder + "/" + name);
        }

        if (keys.Count == 0) Assert.Ignore("The sound pack is not in this project.");

        foreach (SoundCatalog.Spec spec in SoundCatalog.All)
        foreach (string pattern in spec.Takes)
            Assert.That(SoundSetup.Match(keys, pattern), Is.Not.Empty, $"{spec.Id}: nothing in the pack matches '{pattern}'");
    }

    [Test]
    public void EverySoundIdHasACueWithTakesInTheLibrary()
    {
        var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (library == null) Assert.Ignore("No sound library yet: run 'Last Orders / Audio / Set Up Sound'.");

        foreach (SoundId id in (SoundId[])Enum.GetValues(typeof(SoundId)))
        {
            if (id == SoundId.None) continue;

            SoundCue cue = library.Get(id);
            Assert.That(cue, Is.Not.Null, $"{id} has no cue");
            Assert.That(cue.HasClips, Is.True, $"{id}'s cue has no takes");
            if (cue.spatial) Assert.That(cue.maxDistance, Is.GreaterThan(cue.minDistance), $"{id} fades out before it starts");
        }
    }

    [Test]
    public void ThreeDTakesAreMonoAndBedsStream()
    {
        var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (library == null) Assert.Ignore("No sound library yet: run 'Last Orders / Audio / Set Up Sound'.");

        foreach (SoundLibrary.Entry entry in library.Cues)
        {
            if (entry.cue == null) continue;

            foreach (AudioClip clip in entry.cue.clips)
            {
                if (!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is AudioImporter importer)) continue;

                // A stereo take on a 3D voice plays only its left channel.
                if (entry.cue.spatial) Assert.That(importer.forceToMono, Is.True, $"{clip.name} ({entry.id}) is heard in 3D but is stereo");

                if (entry.cue.bus == SoundBus.Ambience)
                    Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), $"{clip.name} is a bed held in memory");
            }
        }
    }

    [Test]
    public void AHandleThatNeverStartedIsHarmless()
    {
        SoundHandle nothing = default;
        Assert.That(nothing.IsPlaying, Is.False);
        Assert.DoesNotThrow(() => nothing.Stop(0.5f));
        Assert.DoesNotThrow(() => nothing.SetVolume(0.2f));

        // With no controller in the scene every call is silence, not an error.
        if (SoundController.Instance == null)
            Assert.That(SoundController.Play(SoundId.UiTap).IsPlaying, Is.False);
    }
}
#endif
