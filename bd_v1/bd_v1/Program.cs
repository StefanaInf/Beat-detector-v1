using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;

public class BeatDetector
{
    private readonly int sampleRate;
    private readonly int sampleSize;
    private readonly Complex32[] previousSpectrum;
    private readonly List<float> spectralFluxes;
    private double elapsedTimeInSeconds;
    private double lastBeatTimeInSeconds;  // Added this line

    public BeatDetector(int sampleRate, int sampleSize)
    {
        this.sampleRate = sampleRate;
        this.sampleSize = sampleSize;
        this.previousSpectrum = new Complex32[sampleSize];
        this.spectralFluxes = new List<float>();
        this.elapsedTimeInSeconds = 0.0;
        this.lastBeatTimeInSeconds = 0.0;  // Added this line
    }

    private void NormalizeSpectrum(Complex32[] spectrum)
    {
        float maxMagnitude = spectrum.Max(c => c.Magnitude);

        if (maxMagnitude > 1.0f)
        {
            float normalizationFactor = 1.0f / maxMagnitude;

            for (int i = 0; i < spectrum.Length; i++)
            {
                spectrum[i] *= normalizationFactor;
            }
        }
    }

    private float CalculateSpectralFlux(Complex32[] currentSpectrum)
    {
        float flux = 0.0f;

        for (int i = 0; i < sampleSize; i++)
        {
            float difference = Math.Max(0, currentSpectrum[i].Magnitude - previousSpectrum[i].Magnitude);
            flux += difference;
        }

        spectralFluxes.Add(flux);

        if (spectralFluxes.Count > 10)
        {
            spectralFluxes.RemoveAt(0);
        }

        float median = spectralFluxes.Skip(Math.Max(0, spectralFluxes.Count - 10)).Take(10).Average();

        return median;
    }

    public void ProcessFrame(float[] samples)
    {
        Complex32[] spectrum = new Complex32[this.sampleSize];
        for (int i = 0; i < this.sampleSize; i++)
        {
            spectrum[i] = new Complex32(samples[i], 0);
        }

        Fourier.Forward(spectrum, FourierOptions.NoScaling);

        NormalizeSpectrum(spectrum);

        float flux = CalculateSpectralFlux(spectrum);

        float threshold = 7.0f;

        // Minimum time gap between successive beats (adjust as needed)
        double minTimeGapInSeconds = 0.2;

        // Check if enough time has passed since the last beat
        if (flux > threshold && elapsedTimeInSeconds - lastBeatTimeInSeconds > minTimeGapInSeconds)
        {
            Console.WriteLine($"Beat detected at {elapsedTimeInSeconds} seconds");
            lastBeatTimeInSeconds = elapsedTimeInSeconds;
        }

        Array.Copy(spectrum, previousSpectrum, this.sampleSize);

        // Update elapsed time
        elapsedTimeInSeconds += (double)this.sampleSize / this.sampleRate;
    }
}

public class SampleProvider : IWaveProvider
{
    private readonly BeatDetector beatDetector;
    private readonly AudioFileReader audioFile;

    public SampleProvider(BeatDetector beatDetector, AudioFileReader audioFile)
    {
        this.beatDetector = beatDetector;
        this.audioFile = audioFile;
    }

    public WaveFormat WaveFormat => audioFile.WaveFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = audioFile.Read(buffer, offset, count);

        if (bytesRead > 0)
        {
            float[] floatBuffer = new float[bytesRead / 4];
            Buffer.BlockCopy(buffer, offset, floatBuffer, 0, bytesRead);
            beatDetector.ProcessFrame(floatBuffer);
            Buffer.BlockCopy(floatBuffer, 0, buffer, offset, bytesRead);
        }

        return bytesRead;
    }
}

class Program
{
    static void Main()
    {
        string audioFilePath = @"C:\Users\Stefana\Music\If youre so simple (chill lofi beat).wav";

        var beatDetector = new BeatDetector(44100, 1024);
        using (var audioFile = new AudioFileReader(audioFilePath))
        {
            var sampleProvider = new SampleProvider(beatDetector, audioFile);

            using (var waveOut = new WaveOutEvent())
            {
                waveOut.Init(sampleProvider);
                waveOut.Play();

                Console.WriteLine("Playing audio...");
                System.Threading.Thread.Sleep(Timeout.Infinite);
            }
        }
    }
}
