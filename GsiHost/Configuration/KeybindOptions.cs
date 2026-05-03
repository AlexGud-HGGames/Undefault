namespace GsiHost.Configuration;

public sealed class KeybindOptions
{
    public const string SectionName = "Keybinds";

    public bool? Enabled { get; set; }

    public List<KeybindBindingOptions> Bindings { get; set; } = new();

    public bool IsEnabled(RuntimeOptions runtime)
    {
        return Enabled ?? runtime.IsIntentCapture;
    }
}

public sealed class KeybindBindingOptions
{
    public string? Key { get; set; }

    public string? EventKey { get; set; }

    public string? Action { get; set; }

    public string? Detail { get; set; }
}
