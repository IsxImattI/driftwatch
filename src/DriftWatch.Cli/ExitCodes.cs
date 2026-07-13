using DriftWatch.Core;

namespace DriftWatch.Cli;

/// <summary>
/// Exit codes are a contract (CI gate): 0 = no drift, 1 = drift, 2 = error.
/// </summary>
public static class ExitCodes
{
    public const int NoDrift = 0;
    public const int Drift = 1;
    public const int Error = 2;

    public static int FromReport(DriftReport report) =>
        report.HasDrift ? Drift : NoDrift;
}
