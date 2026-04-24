namespace GsiHost.Configuration;

/// <summary>
/// Binds the <c>Gsi</c> section of <c>appsettings.json</c>. Extra keys in JSON
/// (Method, Path, Url) are ignored here but remain for CS2 setup / config UI.
/// </summary>
public sealed class GsiOptions
{
    public const string SectionName = "Gsi";

    /// <summary>
    /// When false, <c>POST /gsi/reset</c> returns 403. Default true for local dev.
    /// </summary>
    public bool AllowReset { get; set; } = true;
}
