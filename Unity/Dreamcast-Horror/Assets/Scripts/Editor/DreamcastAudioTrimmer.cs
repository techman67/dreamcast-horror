using System;
using System.IO;

// Editor preparation only: preserve the original PCM format and first complete frames.
public static class DreamcastAudioTrimmer
{
    public static void WriteTestCopy(string source, string destination)
    {
        var report = DreamcastAudioValidator.Analyze(source);
        if (!report.CanTrimForTesting)
            throw new InvalidDataException("Only otherwise supported WAVs whose sole problem is duration can be shortened.\n" + report.Describe());
        int frames = (int)(65534L * report.rate / 11025);
        int alignment = report.channels * report.bits / 8;
        int length = frames * alignment;
        byte[] samples = new byte[length];
        using (var input = File.OpenRead(source)) {
            input.Position = report.dataOffset;
            int read = 0;
            while (read < length) {
                int count = input.Read(samples, read, length - read);
                if (count == 0) throw new InvalidDataException("Source changed or ended while reading the test excerpt.");
                read += count;
            }
        }
        // CreateNew also prevents overwriting the source or an existing test copy.
        using var output = new BinaryWriter(new FileStream(destination, FileMode.CreateNew, FileAccess.Write));
        output.Write(0x46464952u); output.Write(36 + length + (length & 1)); output.Write(0x45564157u);
        output.Write(0x20746d66u); output.Write(16); output.Write((ushort)1);
        output.Write((ushort)report.channels); output.Write(report.rate); output.Write(report.rate * alignment);
        output.Write((ushort)alignment); output.Write((ushort)report.bits);
        output.Write(0x61746164u); output.Write(length); output.Write(samples);
        if ((length & 1) != 0) output.Write((byte)0);
    }
}
