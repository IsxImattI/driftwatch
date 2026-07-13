using DriftWatch.Cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("driftwatch");
    config.AddCommand<CompareCommand>("compare")
        .WithDescription("Compare two schema sources and report drift.")
        .WithExample("compare", "--source", "./scripts",
            "--target", "Server=localhost;Database=AppDb;Integrated Security=True");
});

var exitCode = await app.RunAsync(args);

// Spectre returns its own negative codes for usage/parse errors; the CLI
// contract is 0 = no drift, 1 = drift, 2 = error.
return exitCode is ExitCodes.NoDrift or ExitCodes.Drift ? exitCode : ExitCodes.Error;
