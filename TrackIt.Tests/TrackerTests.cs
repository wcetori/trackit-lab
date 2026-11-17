using TrackIt;
using Xunit;

namespace TrackIt.Tests;

public sealed class TrackerTests
{
    [Fact] public void Goal_ClampsProgress_AndCompletesAtTarget()
    {
        var goal=new Goal(Guid.NewGuid(),"Read","","mental",DateTime.UtcNow,null,ItemStatus.Planned,10,0,"books","weekly");
        goal.SetProgress(14);
        Assert.Equal(10,goal.CurrentValue); Assert.Equal(ItemStatus.Completed,goal.Status);
    }
    [Theory] [InlineData(39,0,"red")] [InlineData(40,20,"yellow")] [InlineData(100,100,"green")]
    public void Quest_CompletionAwardsExpectedXp(int progress,int xp,string result)
    {
        var quest=new Quest(Guid.NewGuid(),"Quest","","career",DateTime.UtcNow,null,ItemStatus.Planned,100);
        quest.SetProgress(progress);
        Assert.Equal(xp,quest.Complete(DateTime.UtcNow)); Assert.Equal(result,quest.FinalResult);
    }
    [Fact] public void Profile_RecalculatesLevelFromExperience()
    {
        var profile=new UserProfile(Guid.NewGuid(),"A","a@b.test",DateTime.UtcNow);
        profile.AddXp(350);
        Assert.Equal(3,profile.CurrentLevel);
    }
    [Fact] public void InvalidGoalTarget_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Goal(Guid.NewGuid(),"x","","c",DateTime.UtcNow,null,ItemStatus.Planned,0,0,"u","once"));
    }
    [Fact] public void JsonRepository_RoundTripsState()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        var logger=new TestLogger(); var repo=new JsonTrackerRepository(path,logger);
        var data=new TrackerData(); data.Quests.Add(new(Guid.NewGuid(),"Test","","mental",DateTime.UtcNow,null,ItemStatus.Planned,30));
        repo.Save(data); var loaded=repo.Load();
        Assert.Single(loaded.Quests); Assert.Equal("Test",loaded.Quests[0].Title); File.Delete(path);
    }
    private sealed class TestLogger : IAppLogger { public void Info(string message) { } public void Error(string message,Exception exception) { } }
}
