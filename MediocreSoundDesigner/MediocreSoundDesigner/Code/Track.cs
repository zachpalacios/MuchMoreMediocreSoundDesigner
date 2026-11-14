using NAudio.Wave;
using System.Security.Permissions;

namespace MediocreSoundDesigner;

public enum SampleRegion
{
    BEGINNING_ONLY,
    END_ONLY,
    BEGINNING_AND_END,
    ANYWHERE,
}

public class Track : IDisposable
{
    // Fields
    private string file;
    private TimeSpan duration;
    private float offset;
    private float timeScale;
    private int sampleRate;
    private float[] samples;
    public float[] GetSamplesCopy() => (float[])samples.Clone();
    private float pitch;
    private int bits;
    private int channels;
    private AudioFileReader fileReader;
    private WaveOut outputDevice;
    private Panel panContainer;
    private int drawTimescale;

    // properties
    public int DrawOffset { get; set; }
    public bool IsDisabled { get; private set; }
    public Panel PanDrawSamples { get; private set; }
    public float Volume { get; private set; }
    public Color TrackColor { get; set; } = Color.DeepSkyBlue;  // waveform color
    public string Name { get; set; } = "Track";                 // label shown on the track
    public int SampleCount => samples.Length;                    // used for zoom-to-fit
    public int SampleRate => sampleRate;                         // e.g. 44100
    public int Channels => channels;                             // 1 = mono, 2 = stereo
    public double DurationSeconds => samples.Length / (double)Math.Max(1, sampleRate * channels);
    private static readonly Font LabelFont = new("Segoe UI", 9f, FontStyle.Bold);
    private string tempFilePath => Path.Combine(Path.GetTempPath(), "MediocreSoundDesigner", "temp.wav");
    public PlaybackState State => outputDevice?.PlaybackState ?? PlaybackState.Stopped;

    // ctor
    public Track(string file)
    {
        fileReader = new AudioFileReader(file);
        int sampleCount = (int)(fileReader.Length / 4);
        this.samples = new float[sampleCount];
        int samplesRead = ((ISampleProvider)fileReader).Read(samples);
        if (samplesRead < sampleCount)
        {
            Array.Resize(ref samples, samplesRead);
        }

        this.file = file;
        this.Name = Path.GetFileNameWithoutExtension(file);
        this.duration = fileReader.TotalTime;
        this.offset = 0;
        this.timeScale = 1.0f;
        this.Volume = 1.0f;
        this.pitch = 1.0f;
        this.bits = fileReader.WaveFormat.BitsPerSample;
        this.channels = fileReader.WaveFormat.Channels;
        this.sampleRate = fileReader.WaveFormat.SampleRate;
        IsDisabled = false;
    }
    public Track(float[] samples)
    {
        this.samples = samples;

        this.file = "";
        this.duration = TimeSpan.Zero;
        this.offset = 0;
        this.timeScale = 1.0f;
        this.Volume = 1.0f;
        this.pitch = 1.0f;
        this.bits = 32;
        this.channels = 1;
        this.sampleRate = 44100;
        IsDisabled = false;
    }
    // Same as above, but with a specific format (used for the combined playback mix)
    public Track(float[] samples, int sampleRate, int channels) : this(samples)
    {
        this.sampleRate = sampleRate;
        this.channels = channels;
    }
    public void LinkPanels(Panel panDrawSamples, Panel panContainer)
    {
        this.PanDrawSamples = panDrawSamples;
        this.panContainer = panContainer;
    }

    // edits
    public float[] CloneSamples()
    {
        return (float[])samples.Clone();
    }
    public void ReplaceSamples(float[] samples)
    {
        this.samples = samples;
    }
    public void Reverse()
    {
        for (int i = 0; i < samples.Length / 2; i++)
        {
            var temp = samples[i];
            samples[i] = samples[samples.Length - 1 - i];
            samples[samples.Length - 1 - i] = temp;
        }
    }
    public void Flip()
    {
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] *= -1;
        }
    }

    // Normalize audio method
    public void Normalize()
    {
        // Find the highest amplitude in the track
        float maxAmplitude = 0;

        for (int i = 0; i < samples.Length; i++)
        {
            float amplitude = Math.Abs(samples[i]);

            if (amplitude > maxAmplitude)
            {
                maxAmplitude = amplitude;
            }
        }

        // Do nothing if the track has no sound at all
        if (maxAmplitude == 0)
        {
            return;
        }

        // Calculate the multiplier needed to reach peak amplitude
        float multiplier = 1.0f / maxAmplitude;

        // Apply the multiplier to every sample
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] *= multiplier;
        }
    }

    // Fade in method
    public void FadeIn(float durationSeconds)
    {
        if (durationSeconds <= 0 || sampleRate <= 0 || samples.Length == 0)
            return;

        // Calculate how many audio frames the fade should cover
        int totalFrames = samples.Length / channels;
        int fadeFrames = Math.Min(totalFrames, (int)(durationSeconds * sampleRate));

        if (fadeFrames <= 1)
            return;

        // Gradually increase volume from 0% to 100%
        for (int frame = 0; frame < fadeFrames; frame++)
        {
            float multiplier = (float)frame / (fadeFrames - 1);

            for (int channel = 0; channel < channels; channel++)
            {
                int index = frame * channels + channel;
                samples[index] *= multiplier;
            }
        }
    }
    // Fade out method
    public void FadeOut(float durationSeconds)
    {
        if (durationSeconds <= 0 || sampleRate <= 0 || samples.Length == 0)
            return;

        int totalFrames = samples.Length / channels;
        int fadeFrames = Math.Min(totalFrames, (int)(durationSeconds * sampleRate));

        if (fadeFrames <= 1)
            return;

        // Start the fade near the end of the track
        int startFrame = totalFrames - fadeFrames;

        // Gradually decrease volume from 100% to 0%
        for (int frame = 0; frame < fadeFrames; frame++)
        {
            float multiplier = 1.0f - (float)frame / (fadeFrames - 1);

            for (int channel = 0; channel < channels; channel++)
            {
                int index = (startFrame + frame) * channels + channel;
                samples[index] *= multiplier;
            }
        }
    }
    public void Trim()
    {
    }
    public void AlterTimeScale(float scale)
    {
    }
    public void SetVolume(float newVolume)
    {
        Volume = newVolume;
    }
    public void EliminateSilence(SampleRegion region, float minDuration, float thres)
    {
        float[] newSamples = CloneSamples();
        if (region is SampleRegion.BEGINNING_ONLY or SampleRegion.BEGINNING_AND_END)
        {
            int silentDurationInNumSamples = 0;
            for (int i = 0; i < newSamples.Length; i++)
            {
                if (Math.Abs(newSamples[i]) <= thres)
                {
                    silentDurationInNumSamples++;
                }
                else
                {
                    break;
                }
            }
            if ((float)silentDurationInNumSamples / sampleRate >= minDuration)
            {
                newSamples = newSamples[silentDurationInNumSamples..];
            }
        }
        if (region is SampleRegion.END_ONLY or SampleRegion.BEGINNING_AND_END)
        {
            int silentDurationInNumSamples = 0;
            for (int i = newSamples.Length - 1; i >= 0; i--)
            {
                if (Math.Abs(newSamples[i]) <= thres)
                {
                    silentDurationInNumSamples++;
                }
                else
                {
                    break;
                }
            }
            if ((float)silentDurationInNumSamples / sampleRate >= minDuration)
            {
                newSamples = newSamples[0..^silentDurationInNumSamples];
            }
        }
        if (region is SampleRegion.ANYWHERE)
        {
            int silentDurationInNumSamples = 0;
            int silentRegionStart = 0;
            int i;
            for (i = 0; i < newSamples.Length; i++)
            {
                if (Math.Abs(newSamples[i]) <= thres)
                {
                    silentDurationInNumSamples++;
                }
                else
                {
                    if ((float)silentDurationInNumSamples / sampleRate >= minDuration)
                    {
                        newSamples = [.. newSamples[0..silentRegionStart], .. newSamples[i..]];
                        i = 0;
                    }
                    silentRegionStart = i + 1;
                    silentDurationInNumSamples = 0;
                }
            }
            if ((float)silentDurationInNumSamples / sampleRate >= minDuration)
            {
                newSamples = [.. newSamples[0..silentRegionStart], .. newSamples[i..]];
            }
        }
        samples = newSamples;
    }

    // play
    public void Play()
    {
        outputDevice?.Dispose();
        fileReader?.Dispose();
        SaveToTempFile();
        fileReader = new AudioFileReader(tempFilePath);
        outputDevice = new WaveOut();
        outputDevice.Init(fileReader);
        outputDevice.Play();
    }
    public void Pause()
    {
        outputDevice?.Pause();
    }
    // Continue playing after a pause
    public void Resume()
    {
        outputDevice?.Play();
    }
    // Stop playing and rewind to the start
    public void Stop()
    {
        if (outputDevice != null)
        {
            outputDevice.Stop();
            outputDevice.Dispose();
            outputDevice = null;
        }
    }
    public void Reset()
    {
    }
    public int GetPlayheadPos()
    {
        return (int)((outputDevice?.GetPosition() ?? 0) / 4);
    }
    public void ToggleDisabled()
    {
        IsDisabled = !IsDisabled;
    }
    // file i/o

    // The audio format to write: the original file's format if we have one,
    // otherwise the track's own format (generated/recorded tracks are mono 44.1 kHz)
    private WaveFormat GetWaveFormat()
    {
        return fileReader?.WaveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
    }

    private void SaveToTempFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath));
        using var writer = new WaveFileWriter(tempFilePath, GetWaveFormat());
        float[] adjusted = GetAdjustedSamples();
        writer.WriteSamples(adjusted, 0, adjusted.Length);
    }
    public void SaveToFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var writer = new WaveFileWriter(path, GetWaveFormat());
        float[] adjusted = GetAdjustedSamples();
        writer.WriteSamples(adjusted, 0, adjusted.Length);
    }
    public float[] GetAdjustedSamples()
    {
        float[] adjSamples = new float[samples.Length];
        for (int s = 0; s < samples.Length; s++)
        {
            adjSamples[s] = samples[s] * Volume;
        }
        int deadLength = DrawOffset * drawTimescale;
        deadLength -= deadLength % channels;
        float[] deadSpace = new float[deadLength];

        return [.. deadSpace, .. adjSamples];
    }

    // render
    public void ShiftSampleOver(int amount)
    {
        DrawOffset += amount;
        DrawOffset = Math.Max(0, DrawOffset);
    }
    public void DrawSamples(Graphics g, int drawTimescale)
    {
        if (PanDrawSamples is null)
        {
            return;
        }
        drawTimescale = Math.Max(1, drawTimescale);   // never divide the track into 0-sample columns
        this.drawTimescale = drawTimescale;

        int width = PanDrawSamples.Width;
        int mid = PanDrawSamples.Height / 2;
        float halfHeight = PanDrawSamples.Height / 2f - 6;   // small margin top and bottom

        // Only draw the strip of the panel that actually needs repainting (much faster)
        RectangleF clip = g.ClipBounds;
        int firstX = Math.Max(DrawOffset, (int)Math.Floor(clip.Left));
        int lastX = Math.Min(width, (int)Math.Ceiling(clip.Right));

        // center line
        using (Pen centerPen = new(Color.FromArgb(50, Color.White)))
        {
            g.DrawLine(centerPen, (int)clip.Left, mid, lastX, mid);
        }

        if (samples != null && samples.Length > 0)
        {
            Color color = IsDisabled ? Settings.Color.TrackSamples_Disabled : TrackColor;
            using Pen pen = new(color);

            // One vertical line per pixel, from the lowest to the highest sample in that pixel's slice.
            for (int x = firstX; x < lastX; x++)
            {
                long start = (long)(x - DrawOffset) * drawTimescale;
                if (start >= samples.Length) break;
                int end = (int)Math.Min(start + drawTimescale, samples.Length);
                float min = 0f, max = 0f;
                for (int i = (int)start; i < end; i++)
                {
                    float v = samples[i] * Volume;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
                int yTop = mid - (int)(Math.Clamp(max, -1f, 1f) * halfHeight);
                int yBottom = mid - (int)(Math.Clamp(min, -1f, 1f) * halfHeight);
                g.DrawLine(pen, x, yTop, x, Math.Max(yBottom, yTop + 1));
            }

            // track name in the top-left corner
            TextRenderer.DrawText(g, Name, LabelFont, new Point(8, 6), color);
        }
    }

    // dispose
    public void Dispose()
    {
        outputDevice?.Dispose();
        fileReader?.Dispose();
    }
}