using System.ComponentModel;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DriftWatch.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DriftWatch.Cli;

public sealed class CompareCommand : AsyncCommand<CompareCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--source <SOURCE>")]
        [Description("Folder of .sql scripts or a SQL Server connection string.")]
        public string? Source { get; init; }

        [CommandOption("--target <TARGET>")]
        [Description("Folder of .sql scripts or a SQL Server connection string.")]
        public string? Target { get; init; }

        [CommandOption("--ignore-case")]
        [Description("Compare definitions case-insensitively.")]
        public bool IgnoreCase { get; init; }

        [CommandOption("--format <FORMAT>")]
        [Description("Output format: table (default) or json.")]
        [DefaultValue(OutputFormat.Table)]
        public OutputFormat Format { get; init; }

        [CommandOption("--show-diff")]
        [Description("Show a line diff of the normalized definitions for each different object.")]
        public bool ShowDiff { get; init; }

        public override ValidationResult Validate() =>
            string.IsNullOrWhiteSpace(Source) ? ValidationResult.Error("--source is required.")
            : string.IsNullOrWhiteSpace(Target) ? ValidationResult.Error("--target is required.")
            : ValidationResult.Success();
    }

    private sealed record SourceData(
        IReadOnlyList<SchemaObject> Objects,
        IReadOnlyList<string> SkippedFiles,
        IReadOnlyList<EncryptedObjectInfo> EncryptedObjects);

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var source = SchemaSourceFactory.Create(settings.Source!);
            var target = SchemaSourceFactory.Create(settings.Target!);

            var sourceData = await ReadAsync(source, cancellationToken);
            var targetData = await ReadAsync(target, cancellationToken);

            var options = new NormalizeOptions(IgnoreCase: settings.IgnoreCase);
            var report = DriftComparer.Compare(sourceData.Objects, targetData.Objects, options);

            var skippedFiles = sourceData.SkippedFiles.Concat(targetData.SkippedFiles).ToList();
            var encryptedObjects = sourceData.EncryptedObjects.Concat(targetData.EncryptedObjects).ToList();

            if (settings.Format == OutputFormat.Json)
            {
                // Stdout must stay pure JSON; warnings go to stderr.
                WriteWarningsToStdErr(skippedFiles, encryptedObjects);
                Console.WriteLine(JsonReport.Serialize(
                    report, sourceData.Objects.Count, targetData.Objects.Count,
                    skippedFiles, encryptedObjects));
            }
            else
            {
                RenderTable(source, target, sourceData, targetData, report,
                    skippedFiles, encryptedObjects, settings, options);
            }

            return ExitCodes.FromReport(report);
        }
        catch (Exception ex)
        {
            // Expected failures (bad source, connection failure, duplicate
            // objects) get a clean message, not a stack trace.
            Console.Error.WriteLine($"error: {ex.Message}");
            return ExitCodes.Error;
        }
    }

    private static async Task<SourceData> ReadAsync(ISchemaSource source, CancellationToken ct)
    {
        switch (source)
        {
            case ScriptFolderSource folder:
            {
                var result = await folder.ReadDetailedAsync(ct);
                return new SourceData(result.Objects, result.SkippedFiles, []);
            }

            case SqlServerSource server:
            {
                var result = await server.ReadDetailedAsync(ct);
                return new SourceData(result.Objects, [], result.EncryptedObjects);
            }

            default:
                return new SourceData(await source.ReadAsync(ct), [], []);
        }
    }

    private static void WriteWarningsToStdErr(
        IReadOnlyList<string> skippedFiles,
        IReadOnlyList<EncryptedObjectInfo> encryptedObjects)
    {
        foreach (var file in skippedFiles)
        {
            Console.Error.WriteLine($"warning: skipped file (no CREATE statement found): {file}");
        }

        foreach (var encrypted in encryptedObjects)
        {
            Console.Error.WriteLine(
                $"warning: skipped encrypted object: {encrypted.FullName} ({encrypted.Type})");
        }
    }

    private static void RenderTable(
        ISchemaSource source,
        ISchemaSource target,
        SourceData sourceData,
        SourceData targetData,
        DriftReport report,
        IReadOnlyList<string> skippedFiles,
        IReadOnlyList<EncryptedObjectInfo> encryptedObjects,
        Settings settings,
        NormalizeOptions options)
    {
        var console = AnsiConsole.Console;

        console.MarkupLine($"[bold]Comparing[/] {Markup.Escape(source.Description)}");
        console.MarkupLine($"[bold]     with[/] {Markup.Escape(target.Description)}");
        console.WriteLine();

        RenderSection(console, "Only in source", "yellow",
            report.OnlyInSource.Select(o => (o.Type, o.FullName)));
        RenderSection(console, "Only in target", "yellow",
            report.OnlyInTarget.Select(o => (o.Type, o.FullName)));
        RenderSection(console, "Different", "red",
            report.Different.Select(p => (p.Source.Type, p.Source.FullName)));

        if (settings.ShowDiff)
        {
            foreach (var pair in report.Different)
            {
                RenderDiff(console, pair, options);
            }
        }

        console.MarkupLine(
            $"Summary: {sourceData.Objects.Count} objects in source, {targetData.Objects.Count} in target — " +
            $"[yellow]{report.OnlyInSource.Count} only in source[/], " +
            $"[yellow]{report.OnlyInTarget.Count} only in target[/], " +
            $"[red]{report.Different.Count} different[/].");
        console.MarkupLine(report.HasDrift
            ? "[red bold]Drift detected.[/]"
            : "[green bold]No drift detected.[/]");

        foreach (var file in skippedFiles)
        {
            console.MarkupLine(
                $"[grey]warning: skipped file (no CREATE statement found): {Markup.Escape(file)}[/]");
        }

        foreach (var encrypted in encryptedObjects)
        {
            console.MarkupLine(
                $"[grey]warning: skipped encrypted object: {Markup.Escape(encrypted.FullName)} ({encrypted.Type})[/]");
        }
    }

    private static void RenderSection(
        IAnsiConsole console,
        string title,
        string color,
        IEnumerable<(SchemaObjectType Type, string FullName)> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title(new TableTitle($"[{color} bold]{title} ({list.Count})[/]"))
            .AddColumn("Type")
            .AddColumn("Object");

        foreach (var (type, fullName) in list)
        {
            table.AddRow($"[{color}]{type}[/]", $"[{color}]{Markup.Escape(fullName)}[/]");
        }

        console.Write(table);
        console.WriteLine();
    }

    private static void RenderDiff(IAnsiConsole console, DriftPair pair, NormalizeOptions options)
    {
        console.MarkupLine(
            $"[red bold]{Markup.Escape(pair.Source.FullName)}[/] [grey]({pair.Source.Type}, -source/+target, normalized)[/]");

        var diff = InlineDiffBuilder.Diff(
            SqlNormalizer.Normalize(pair.Source.Definition, options),
            SqlNormalizer.Normalize(pair.Target.Definition, options));

        foreach (var line in diff.Lines)
        {
            var text = Markup.Escape(line.Text);
            switch (line.Type)
            {
                case ChangeType.Inserted:
                    console.MarkupLine($"[green]+ {text}[/]");
                    break;
                case ChangeType.Deleted:
                    console.MarkupLine($"[red]- {text}[/]");
                    break;
                default:
                    console.MarkupLine($"[grey]  {text}[/]");
                    break;
            }
        }

        console.WriteLine();
    }
}
