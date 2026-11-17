using System.Text.Json;
using TrackIt;

var paths = AppPaths.Create(AppContext.BaseDirectory);
Directory.CreateDirectory(paths.DataDirectory);
Directory.CreateDirectory(paths.InputDirectory);
Directory.CreateDirectory(paths.OutputDirectory);

var logger = new FileLogger(paths.LogFile);
ITrackerRepository repository = new JsonTrackerRepository(paths.DataFile, logger);
IReportWriter reports = new TextReportWriter(paths.ReportFile);
var service = new TrackerService(repository, reports, logger);

try
{
    var commandFile = Path.Combine(paths.InputDirectory, "commands.json");
    if (!File.Exists(commandFile))
    {
        File.WriteAllText(commandFile, SampleFiles.Commands);
        Console.WriteLine($"Створено приклад вхідного файлу: {commandFile}");
    }
    var commands = JsonSerializer.Deserialize<List<TrackerCommand>>(File.ReadAllText(commandFile), JsonOptions.Default)
                   ?? throw new InvalidDataException("Файл commands.json містить порожній список.");
    var result = service.Execute(commands);
    // Treat the JSON file as an inbox so the same commands are not applied twice.
    File.WriteAllText(commandFile, "[]");
    Console.WriteLine($"Оброблено команд: {result.Processed}. Дані: {paths.DataFile}. Звіт: {paths.ReportFile}. Лог: {paths.LogFile}");
}
catch (Exception ex)
{
    logger.Error("Application failed", ex);
    Console.Error.WriteLine($"Помилка: {ex.Message}. Подробиці записані у файл логу.");
    Environment.ExitCode = 1;
}
