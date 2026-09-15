namespace Reach.Reporting;

/// <summary>
/// The one axis a notice is classified on. A <c>warning</c>/<c>info</c> severity alongside
/// this could only ever disagree with it, and a pipeline that wants to gate gates on
/// <see cref="BlindSpot"/>.
/// </summary>
internal enum NoticeKind
{
    /// <summary>May under-select. Every code of this kind has an entry in the limitations register.</summary>
    BlindSpot,

    /// <summary>May over-select — a deliberate widening, disclosed so its cost can be measured.</summary>
    Widening,

    /// <summary>Something fell outside the analysis scope.</summary>
    Scope,

    /// <summary>A fact about the environment the run happened in.</summary>
    Environment,
}
