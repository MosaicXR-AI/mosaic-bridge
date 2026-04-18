namespace Mosaic.Bridge.Core.Licensing
{
    public enum LicenseTier
    {
        Trial,
        Indie,
        Pro,
        Team,
        Pilot,
        Expired
    }

    public enum BlockReason
    {
        TrialExpired,
        QuotaExhausted,
        GraceExpired
    }
}
