namespace WpfSettings.Settings;

public record ApplicationSettings
{
    public int Id { get; set; }
    public string ServerUrl { get; set; }
    public bool Mode { get; set; }
}
