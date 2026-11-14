using System.Text.Json;
using System.Text.Json.Serialization;
using Color = System.Drawing.Color;

namespace MediocreSoundDesigner;

public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ColorTranslator.FromHtml(reader.GetString());
    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) => writer.WriteStringValue(("#" + value.R.ToString("X2") + value.G.ToString("X2") + value.B.ToString("X2")).ToLower());
}

public static class Settings
{
    // Bump this when the default palette changes, so older settings files switch to the new look once
    private const int CURRENT_PALETTE_VERSION = 2;

    public class Colors
    {
        public Color WinBg { get; set; }
        public Color WinFg { get; set; }
        public Color TrackSamples_Enabled { get; set; }
        public Color TrackSamples_Disabled { get; set; }
        public Color TrackBg_Unselected { get; set; }
        public Color TrackBg_Selected { get; set; }
        public Color Playline { get; set; }

        // Added with the refined palette
        public Color TrackArea { get; set; }     // background behind the tracks
        public Color Toolbar { get; set; }       // toolbar background
        public Color TextDim { get; set; }       // secondary text (labels, status)
        public Color Accent { get; set; }        // play button, sliders, checkmarks

        // Which palette this file was saved with (old files don't have it, so it reads as 0)
        public int PaletteVersion { get; set; }

        // Refined dark palette (soft charcoal greys instead of pure black)
        public Colors()
        {
            WinBg = ColorTranslator.FromHtml("#1E1E1E");
            WinFg = ColorTranslator.FromHtml("#E0E0E0");
            TrackSamples_Enabled = ColorTranslator.FromHtml("#4FC3F7");
            TrackSamples_Disabled = ColorTranslator.FromHtml("#5A5A5A");
            TrackBg_Unselected = ColorTranslator.FromHtml("#2D2D30");
            TrackBg_Selected = ColorTranslator.FromHtml("#37373D");
            Playline = ColorTranslator.FromHtml("#FFFFFF");
            TrackArea = ColorTranslator.FromHtml("#252526");
            Toolbar = ColorTranslator.FromHtml("#2D2D30");
            TextDim = ColorTranslator.FromHtml("#A0A0A0");
            Accent = ColorTranslator.FromHtml("#4FC3F7");
        }
    }

    static Settings()
    {
        LoadFromFile();
    }

    public static Colors Color { get; private set; }

    private const string FILENAME = "settings.json";

    public static void SaveToFile()
    {
        JsonSerializerOptions options = new() { Converters = { new ColorJsonConverter() }, WriteIndented = true };
        File.WriteAllText(FILENAME, JsonSerializer.Serialize(Color, options));
    }

    public static void LoadFromFile()
    {
        if (File.Exists(FILENAME))
        {
            JsonSerializerOptions options = new() { Converters = { new ColorJsonConverter() } };
            Color = JsonSerializer.Deserialize<Colors>(File.ReadAllText(FILENAME), options);

            // Saved with an older palette: switch to the new one (once) and save it
            if (Color is null || Color.PaletteVersion < CURRENT_PALETTE_VERSION)
            {
                Color = CreateDefaults();
                SaveToFile();
            }
        }
        else
        {
            Color = CreateDefaults();
        }
    }

    public static void RevertToDefault()
    {
        Color = CreateDefaults();
    }

    private static Colors CreateDefaults()
    {
        return new Colors { PaletteVersion = CURRENT_PALETTE_VERSION };
    }
}