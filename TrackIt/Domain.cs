namespace TrackIt;

public enum ItemStatus { Planned, Active, Completed, Failed }
public enum CommandType { AddGoal, AddQuest, UpdateProgress, Complete, AddCategory }

// Абстракція спільної поведінки цілей і квестів; інкапсуляція через private set.
public abstract class TrackItem
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string Category { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? Deadline { get; private set; }
    public ItemStatus Status { get; private set; }

    protected TrackItem(Guid id, string title, string description, string category, DateTime createdAt, DateTime? deadline, ItemStatus status)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Title = Required(title, nameof(title)); Description = description ?? "";
        Category = Required(category, nameof(category)); CreatedAt = createdAt; Deadline = deadline; Status = status;
    }
    public void Rename(string title) => Title = Required(title, nameof(title));
    public void ChangeStatus(ItemStatus status) => Status = status;
    public bool IsOverdue(DateTime now) => Deadline.HasValue && Deadline.Value < now && Status != ItemStatus.Completed;
    public abstract string GetSummary();
    public static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Поле обов’язкове.", name) : value.Trim();
}

public sealed class Goal : TrackItem
{
    public decimal TargetValue { get; private set; }
    public decimal CurrentValue { get; private set; }
    public string Unit { get; private set; }
    public string Frequency { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Goal(Guid id, string title, string description, string category, DateTime createdAt, DateTime? deadline, ItemStatus status, decimal targetValue, decimal currentValue, string unit, string frequency, bool isArchived = false, DateTime? completedAt = null)
      : base(id,title,description,category,createdAt,deadline,status) { if(targetValue<=0) throw new ArgumentOutOfRangeException(nameof(targetValue)); TargetValue=targetValue; CurrentValue=Math.Clamp(currentValue,0,targetValue); Unit=Required(unit,nameof(unit)); Frequency=Required(frequency,nameof(frequency)); IsArchived=isArchived; CompletedAt=completedAt; }
    public void SetProgress(decimal value) { CurrentValue=Math.Clamp(value,0,TargetValue); if(CurrentValue>=TargetValue) { ChangeStatus(ItemStatus.Completed); CompletedAt=DateTime.UtcNow; } }
    public override string GetSummary() => $"Ціль: {Title} — {CurrentValue}/{TargetValue} {Unit}";
}

public sealed class Quest : TrackItem
{
    public int XpReward { get; private set; }
    public int ProgressPercent { get; private set; }
    public string FinalResult { get; private set; }
    public bool IsRepeatable { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public int Attempts { get; private set; }
    public Quest(Guid id,string title,string description,string category,DateTime createdAt,DateTime? deadline,ItemStatus status,int xpReward,int progressPercent=0,string finalResult="",bool isRepeatable=false,DateTime? finishedAt=null,int attempts=0)
      : base(id,title,description,category,createdAt,deadline,status) { if(xpReward<0) throw new ArgumentOutOfRangeException(nameof(xpReward)); XpReward=xpReward; ProgressPercent=Math.Clamp(progressPercent,0,100); FinalResult=finalResult; IsRepeatable=isRepeatable; FinishedAt=finishedAt; Attempts=attempts; }
    public void SetProgress(int value) { if(Status==ItemStatus.Completed||Status==ItemStatus.Failed) throw new InvalidOperationException("Завершений квест не можна змінити."); ProgressPercent=Math.Clamp(value,0,100); if(ProgressPercent>0) ChangeStatus(ItemStatus.Active); }
    public int Complete(DateTime now) { if(Status==ItemStatus.Completed||Status==ItemStatus.Failed) throw new InvalidOperationException("Квест уже завершено."); Attempts++; FinishedAt=now; FinalResult=ProgressPercent>=61?"green":ProgressPercent>=40?"yellow":"red"; ChangeStatus(ProgressPercent>=40?ItemStatus.Completed:ItemStatus.Failed); return ProgressPercent>=61?XpReward*ProgressPercent/100:ProgressPercent>=40?XpReward*ProgressPercent/200:0; }
    public override string GetSummary() => $"Квест: {Title} — {ProgressPercent}%, {XpReward} XP, статус {Status}";
}

public sealed class TrackerCategory
{
    public Guid Id { get; private set; } public string Name { get; private set; } public string Description { get; private set; }
    public string Color { get; private set; } public bool IsActive { get; private set; } public int SortOrder { get; private set; }
    public TrackerCategory(Guid id,string name,string description,string color,bool isActive,int sortOrder) { Id=id==Guid.Empty?Guid.NewGuid():id; Name=TrackItem.Required(name,"name"); Description=description; Color=color; IsActive=isActive; SortOrder=sortOrder; }
    public void Rename(string name) => Name=TrackItem.Required(name,"name"); public void Deactivate() => IsActive=false;
}

public sealed class UserProfile
{
    public Guid Id { get; private set; } public string Name { get; private set; } public string Email { get; private set; }
    public DateTime CreatedAt { get; private set; } public int TotalXp { get; private set; } public int CurrentLevel { get; private set; }
    public UserProfile(Guid id,string name,string email,DateTime createdAt,int totalXp=0,int currentLevel=1) { Id=id==Guid.Empty?Guid.NewGuid():id; Name=TrackItem.Required(name,"name"); Email=TrackItem.Required(email,"email"); CreatedAt=createdAt; TotalXp=Math.Max(0,totalXp); CurrentLevel=Math.Max(1,currentLevel); RecalculateLevel(); }
    public void AddXp(int amount) { if(amount<0) throw new ArgumentOutOfRangeException(nameof(amount)); TotalXp+=amount; RecalculateLevel(); }
    public void RecalculateLevel() { var xp=TotalXp; CurrentLevel=1; while(xp>=CurrentLevel*100) { xp-=CurrentLevel*100; CurrentLevel++; } }
}

public sealed class ProgressRecord
{
    public Guid Id { get; private set; } public Guid ItemId { get; private set; } public DateTime RecordedAt { get; private set; }
    public decimal PreviousValue { get; private set; } public decimal NewValue { get; private set; } public string Note { get; private set; }
    public ProgressRecord(Guid id,Guid itemId,DateTime at,decimal previousValue,decimal newValue,string note) { Id=id==Guid.Empty?Guid.NewGuid():id; ItemId=itemId; RecordedAt=at; PreviousValue=previousValue; NewValue=newValue; Note=note; }
    public decimal GetChange() => NewValue-PreviousValue; public void UpdateNote(string note) => Note=note;
}

public sealed class Reward
{
    public Guid Id { get; private set; } public string Name { get; private set; } public string Description { get; private set; }
    public int XpCost { get; private set; } public bool IsUnlocked { get; private set; } public DateTime? UnlockedAt { get; private set; }
    public Reward(Guid id,string name,string description,int xpCost,bool isUnlocked=false,DateTime? unlockedAt=null) { Id=id==Guid.Empty?Guid.NewGuid():id; Name=TrackItem.Required(name,"name"); Description=description; if(xpCost<0) throw new ArgumentOutOfRangeException(nameof(xpCost)); XpCost=xpCost; IsUnlocked=isUnlocked; UnlockedAt=unlockedAt; }
    public void Unlock(DateTime now) { IsUnlocked=true; UnlockedAt=now; } public void Lock() { IsUnlocked=false; UnlockedAt=null; }
}
