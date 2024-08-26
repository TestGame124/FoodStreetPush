using System;

[Serializable]
public class ExperienceData 
{
    public int Level = 1;
    public int CurrentXP { get; internal set; }
    public int XPToNextLevel { get; internal set; }

}
