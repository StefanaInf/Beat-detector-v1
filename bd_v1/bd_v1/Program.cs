using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using Microsoft.VisualBasic;
using NAudio.Wave;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

public class BeatDetector
{
    private readonly int sampleRate;
    private readonly int sampleSize;
    private double subBassMax, bassMax, lowMidrangeMax, midrangeMax, upperMidrangeMax, presenceMax, brillianceMax;
    private bool subBassBeat, bassBeat, lowMidrangeBeat, midrangeBeat, upperMidrangeBeat, presenceBeat, brillianceBeat;

    public BeatDetector(int sampleRate, int sampleSize)
    {
        this.sampleRate = sampleRate;
        this.sampleSize = sampleSize;
        subBassMax = bassMax = lowMidrangeMax = midrangeMax = upperMidrangeMax = presenceMax = brillianceMax = 10.0;
        subBassBeat = bassBeat = lowMidrangeBeat = midrangeBeat = upperMidrangeBeat = presenceBeat = brillianceBeat = false;
    }

    public void ProcessFrame(float[] samples)
    {
        var audioFFT = new Complex32[sampleSize];
        for (int i = 0; i < sampleSize; i++)
        {
            audioFFT[i] = new Complex32(samples[i], 0);
        }

        Fourier.Forward(audioFFT, FourierOptions.NoScaling);

        double[] audioMagnitudes = audioFFT.Select(c => (double)c.Magnitude).ToArray();
        double[] freqs = Enumerable.Range(0, sampleSize / 2)
                                    .Select(i => (double)i * sampleRate / sampleSize)
                                    .ToArray();

        int[] subBassIndices = freqs.Select((val, idx) => val >= 20 && val <= 60 ? idx : -1).Where(idx => idx != -1).ToArray();
        int[] bassIndices = freqs.Select((val, idx) => val >= 60 && val <= 250 ? idx : -1).Where(idx => idx != -1).ToArray();
        int[] lowMidrangeIndices = freqs.Select((val, idx) => val >= 250 && val <= 500 ? idx : -1).Where(idx => idx != -1).ToArray();
        int[] midrangeIndices = freqs.Select((val, idx) => val >= 500 && val <= 2000 ? idx : -1).Where(idx => idx != -1).ToArray();
        int[] upperMidrangeIndices = freqs.Select((val, idx) => val >= 2000 && val <= 4000 ? idx : -1).Where(idx => idx != -1).ToArray();
        int[] presenceIndices = freqs.Select((val, idx) => val >= 4000 && val <= 6000 ? idx : -1).Where(idx => idx != -1).ToArray();
        int[] brillianceIndices = freqs.Select((val, idx) => val >= 6000 && val <= 20000 ? idx : -1).Where(idx => idx != -1).ToArray();

        double subBass = subBassIndices.Max(idx => audioMagnitudes[idx]);
        double bass = bassIndices.Max(idx => audioMagnitudes[idx]);
        double lowMidrange = lowMidrangeIndices.Max(idx => audioMagnitudes[idx]);
        double midrange = midrangeIndices.Max(idx => audioMagnitudes[idx]);
        double upperMidrange = upperMidrangeIndices.Max(idx => audioMagnitudes[idx]);
        double presence = presenceIndices.Max(idx => audioMagnitudes[idx]);
        double brilliance = brillianceIndices.Max(idx => audioMagnitudes[idx]);

        /*double subBass = subBassIndices != null && subBassIndices.Count() > 0 ? subBassIndices.Max(idx => audioMagnitudes[idx]) : 0;
        double bass = bassIndices != null && bassIndices.Count() > 0 ? bassIndices.Max(idx => audioMagnitudes[idx]) : 0;
        double lowMidrange = lowMidrangeIndices != null && lowMidrangeIndices.Count() > 0 ? lowMidrangeIndices.Max(idx => audioMagnitudes[idx]) : 0;
        double midrange = midrangeIndices != null && midrangeIndices.Count() > 0 ? midrangeIndices.Max(idx => audioMagnitudes[idx]) : 0;
        double upperMidrange = upperMidrangeIndices != null && upperMidrangeIndices.Count() > 0 ? upperMidrangeIndices.Max(idx => audioMagnitudes[idx]) : 0;
        double presence = presenceIndices != null && presenceIndices.Count() > 0 ? presenceIndices.Max(idx => audioMagnitudes[idx]) : 0;
        double brilliance = brillianceIndices != null && brillianceIndices.Count() > 0 ? brillianceIndices.Max(idx => audioMagnitudes[idx]) : 0;*/

        subBassMax = Math.Max(subBassMax, subBass);
        bassMax = Math.Max(bassMax, bass);
        lowMidrangeMax = Math.Max(lowMidrangeMax, lowMidrange);
        midrangeMax = Math.Max(midrangeMax, midrange);
        upperMidrangeMax = Math.Max(upperMidrangeMax, upperMidrange);
        presenceMax = Math.Max(presenceMax, presence);
        brillianceMax = Math.Max(brillianceMax, brilliance);

        CheckBeat(subBass, ref subBassBeat, subBassMax, 0.9, 0.3, ">");
        CheckBeat(bass, ref bassBeat, bassMax, 0.9, 0.3, ">>");
        CheckBeat(lowMidrange, ref lowMidrangeBeat, lowMidrangeMax, 0.9, 0.3, ">>>");
        CheckBeat(midrange, ref midrangeBeat, midrangeMax, 0.9, 0.3, ">>>>");
        CheckBeat(upperMidrange, ref upperMidrangeBeat, upperMidrangeMax, 0.9, 0.3, ">>>>>");
        CheckBeat(presence, ref presenceBeat, presenceMax, 0.9, 0.3, ">>>>>>");
        CheckBeat(brilliance, ref brillianceBeat, brillianceMax, 0.9, 0.3, ">>>>>>>");

        double primaryFreq = freqs[Array.IndexOf(audioMagnitudes, audioMagnitudes.Max())];
        // Console.WriteLine($"Primary Frequency: {primaryFreq}");
    }

    private void CheckBeat(double value, ref bool beatFlag, double max, double beatThreshold, double resetThreshold, string beatType)
    {
        if (value >= max * beatThreshold && !beatFlag)
        {
            beatFlag = true;
            Console.WriteLine($"{beatType}");
        }
        else if (value < max * resetThreshold)
        {
            beatFlag = false;
        }
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

        var beatDetector = new BeatDetector(44100, 2048);
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