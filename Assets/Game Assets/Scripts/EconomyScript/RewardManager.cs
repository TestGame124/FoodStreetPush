using System.Collections.Generic;

public class RewardManager
{
    private List<IReward> rewards;

    public static System.Action<IReward> onAddReward;
    public RewardManager()
    {
        rewards = new List<IReward>();
    }

    public void AddReward(IReward reward)
    {
        rewards.Add(reward);
        onAddReward?.Invoke(reward);
    }

    public void GiveAllRewards()
    {
        foreach (var reward in rewards)
        {
            reward.GiveReward();
        }
        onAddReward= null;
        rewards.Clear();
    }
}