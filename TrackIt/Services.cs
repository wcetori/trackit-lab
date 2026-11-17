using System.Text;
using System.Text.Json;

using System.Text.Json.Serialization;

namespace TrackIt;

public sealed record TrackerCommand(CommandType Type, string? Title = null, string? Description = null, string? Category = null, decimal? Target = null, string? Unit = null, int? Xp = null, Guid? ItemId = null, int? Progress = null, string? Name = null);
public sealed class TrackerData
{
    public List<Goal> Goals { get; set; } = []; public List<Quest> Quests { get; set; } = [];
    public List<TrackerCategory> Categories { get; set; } = []; public UserProfile Profile { get; set; } = new(Guid.NewGuid(),"Player","player@local",DateTime.UtcNow);
    public List<ProgressRecord> ProgressRecords { get; set; } = []; public List<Reward> Rewards { get; set; } = [];
}
public sealed record OperationResult(int Processed, int Goals, int Quests, int TotalXp);
public sealed record AppPaths(string DataDirectory,string InputDirectory,string OutputDirectory,string DataFile,string LogFile,string ReportFile)
{
    public static AppPaths Create(string root) { var basePath=Path.Combine(root,"TrackItData"); var data=Path.Combine(basePath,"data"); var input=Path.Combine(basePath,"input"); var output=Path.Combine(basePath,"output"); return new(data,input,output,Path.Combine(data,"tracker.json"),Path.Combine(output,"app.log"),Path.Combine(output,"report.txt")); }
}
public static class JsonOptions { public static readonly JsonSerializerOptions Default = Create(); private static JsonSerializerOptions Create() { var options=new JsonSerializerOptions { WriteIndented=true, PropertyNameCaseInsensitive=true }; options.Converters.Add(new JsonStringEnumConverter()); return options; } }
public static class SampleFiles
{
    public const string Commands = """
    [
      { "type": "AddCategory", "name": "Career", "description": "Work and learning", "category": "career" },
      { "type": "AddGoal", "title": "Read 12 books", "description": "Reading goal", "category": "mental", "target": 12, "unit": "books" },
      { "type": "AddQuest", "title": "Finish the first assignment", "description": "Submit coursework", "category": "career", "xp": 80 },
      { "type": "AddQuest", "title": "Walk 5 km", "description": "Physical activity", "category": "physical", "xp": 50 }
    ]
    """;
}

// Контракти ізолюють файлові деталі від логіки програми.
public interface ITrackerRepository { TrackerData Load(); void Save(TrackerData data); }
public interface IAppLogger { void Info(string message); void Error(string message, Exception exception); }
public interface IReportWriter { void Write(TrackerData data); }

public sealed class FileLogger(string filePath) : IAppLogger
{
    private readonly object _gate = new();
    public void Info(string message) => Append($"INFO  {DateTime.UtcNow:O} {message}");
    public void Error(string message, Exception exception) => Append($"ERROR {DateTime.UtcNow:O} {message}: {exception}");
    private void Append(string line) { lock(_gate) { Directory.CreateDirectory(Path.GetDirectoryName(filePath)!); File.AppendAllText(filePath,line+Environment.NewLine,Encoding.UTF8); } }
}

public sealed class JsonTrackerRepository(string filePath,IAppLogger logger) : ITrackerRepository
{
    public TrackerData Load()
    {
        if(!File.Exists(filePath)) return new TrackerData();
        try { return JsonSerializer.Deserialize<TrackerData>(File.ReadAllText(filePath),JsonOptions.Default) ?? new TrackerData(); }
        catch(JsonException ex) { logger.Error("Invalid tracker JSON; starting with empty data",ex); throw new InvalidDataException("Файл tracker.json пошкоджений. Виправ його або перемісти в резервну копію.",ex); }
    }
    public void Save(TrackerData data) { Directory.CreateDirectory(Path.GetDirectoryName(filePath)!); var temp=filePath+".tmp"; File.WriteAllText(temp,JsonSerializer.Serialize(data,JsonOptions.Default),Encoding.UTF8); File.Move(temp,filePath,true); }
}

public sealed class TextReportWriter(string filePath) : IReportWriter
{
    public void Write(TrackerData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var lines=new List<string>{"TRACKIT — ЗВІТ",$"Створено UTC: {DateTime.UtcNow:O}",$"Користувач: {data.Profile.Name}",$"Рівень: {data.Profile.CurrentLevel} | XP: {data.Profile.TotalXp}","","ЦІЛІ:"};
        lines.AddRange(data.Goals.Select(x=>"- "+x.GetSummary())); lines.Add("КВЕСТИ:"); lines.AddRange(data.Quests.Select(x=>"- "+x.GetSummary()));
        File.WriteAllLines(filePath,lines,Encoding.UTF8);
    }
}

public sealed class TrackerService(ITrackerRepository repository,IReportWriter reportWriter,IAppLogger logger)
{
    public OperationResult Execute(IEnumerable<TrackerCommand> commands)
    {
        var data=repository.Load(); var processed=0;
        foreach(var command in commands) { try { Apply(data,command); processed++; } catch(ArgumentException ex) { logger.Error($"Invalid command {command.Type}",ex); } catch(InvalidOperationException ex) { logger.Error($"Rejected command {command.Type}",ex); } }
        repository.Save(data); reportWriter.Write(data); logger.Info($"Processed {processed} commands; goals={data.Goals.Count}; quests={data.Quests.Count}");
        return new(processed,data.Goals.Count,data.Quests.Count,data.Profile.TotalXp);
    }
    private static void Apply(TrackerData data,TrackerCommand c)
    {
        switch(c.Type)
        {
            case CommandType.AddCategory: data.Categories.Add(new(Guid.NewGuid(),Need(c.Name??c.Category,"name"),c.Description??"","#ff007f",true,data.Categories.Count)); break;
            case CommandType.AddGoal: data.Goals.Add(new(Guid.NewGuid(),Need(c.Title,"title"),c.Description??"",Need(c.Category,"category"),DateTime.UtcNow,null,ItemStatus.Planned,c.Target??1,0,c.Unit??"units","once")); break;
            case CommandType.AddQuest: data.Quests.Add(new(Guid.NewGuid(),Need(c.Title,"title"),c.Description??"",Need(c.Category,"category"),DateTime.UtcNow,null,ItemStatus.Planned,c.Xp??0)); break;
            case CommandType.UpdateProgress:
                var itemId=c.ItemId??throw new ArgumentException("ItemId обов’язковий."); var goal=data.Goals.FirstOrDefault(x=>x.Id==itemId); var quest=data.Quests.FirstOrDefault(x=>x.Id==itemId);
                if(goal is null&&quest is null) throw new ArgumentException($"Елемент {itemId} не знайдено.");
                if(goal is not null) { var old=goal.CurrentValue; goal.SetProgress(c.Progress??0); data.ProgressRecords.Add(new(Guid.NewGuid(),goal.Id,DateTime.UtcNow,old,goal.CurrentValue,"Input file update")); }
                if(quest is not null) { var old=quest.ProgressPercent; quest.SetProgress(c.Progress??0); data.ProgressRecords.Add(new(Guid.NewGuid(),quest.Id,DateTime.UtcNow,old,quest.ProgressPercent,"Input file update")); } break;
            case CommandType.Complete:
                var q=data.Quests.FirstOrDefault(x=>x.Id==c.ItemId)??throw new ArgumentException("Квест не знайдено."); data.Profile.AddXp(q.Complete(DateTime.UtcNow)); break;
            default: throw new ArgumentException("Невідомий тип команди.");
        }
    }
    private static string Need(string? value,string name)=>string.IsNullOrWhiteSpace(value)?throw new ArgumentException($"Поле {name} обов’язкове."):value.Trim();
}
