using System.Text;

namespace MusicTagger.IntegrationTests.Support;

/// <summary>
/// Generates small but structurally valid audio files for every supported container, so the
/// write path can be tested on real bytes instead of mocked tag libraries. Each fixture carries
/// <see cref="Payload"/> as its audio data, which lets a test prove that the audio bytes are
/// still exactly where they were after the metadata was written.
/// </summary>
public static class AudioFixtures
{
    /// <summary>Distinctive audio payload; no tag library ever generates this sequence.</summary>
    public static byte[] Payload { get; } =
        Enumerable.Range(0, 512).Select(i => (byte)(0x40 + (i * 37 % 191))).ToArray();

    public static byte[] Wav()
    {
        const int sampleRate = 8000;

        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + Payload.Length);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);
        w.Write((short)1);          // PCM
        w.Write((short)1);          // mono
        w.Write(sampleRate);
        w.Write(sampleRate * 2);
        w.Write((short)2);          // block align
        w.Write((short)16);         // bits per sample
        w.Write("data"u8.ToArray());
        w.Write(Payload.Length);
        w.Write(Payload);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// A WAV file tagged the way other tools do it: a LIST/INFO chunk (ASCII values) instead of
    /// an ID3 tag, so the migration path in the writer can be exercised.
    /// </summary>
    public static byte[] WavWithRiffInfo()
    {
        var list = new MemoryStream();
        var lw = new BinaryWriter(list);
        lw.Write("INFO"u8.ToArray());
        WriteInfoChunk(lw, "INAM", "Old title");
        WriteInfoChunk(lw, "IART", "Old artist");
        lw.Flush();
        var listData = list.ToArray();

        const int sampleRate = 8000;

        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray());
        w.Write(4 + 8 + 16 + 8 + Payload.Length + 8 + listData.Length);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray());
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(sampleRate);
        w.Write(sampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8.ToArray());
        w.Write(Payload.Length);
        w.Write(Payload);
        w.Write("LIST"u8.ToArray());
        w.Write(listData.Length);
        w.Write(listData);
        w.Flush();
        return ms.ToArray();
    }

    private static void WriteInfoChunk(BinaryWriter writer, string id, string value)
    {
        var data = Encoding.ASCII.GetBytes(value + "\0");
        writer.Write(Encoding.ASCII.GetBytes(id));
        writer.Write(data.Length);
        writer.Write(data);

        if (data.Length % 2 != 0)
        {
            writer.Write((byte)0); // chunks are word aligned
        }
    }

    public static byte[] Aiff()
    {
        var frames = Payload.Length / 2;

        var comm = new MemoryStream();
        var cw = new BinaryWriter(comm);
        cw.Write(BigEndian(1, 2));           // channels
        cw.Write(BigEndian(frames, 4));      // sample frames
        cw.Write(BigEndian(16, 2));          // sample size
        cw.Write([0x40, 0x0E, 0xAC, 0x44, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]); // 44100 Hz
        cw.Flush();
        var commData = comm.ToArray();

        var ssnd = new MemoryStream();
        var sw = new BinaryWriter(ssnd);
        sw.Write(BigEndian(0, 4));           // offset
        sw.Write(BigEndian(0, 4));           // block size
        sw.Write(Payload);
        sw.Flush();
        var ssndData = ssnd.ToArray();

        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write("FORM"u8.ToArray());
        w.Write(BigEndian(4 + 8 + commData.Length + 8 + ssndData.Length, 4));
        w.Write("AIFF"u8.ToArray());
        w.Write("COMM"u8.ToArray());
        w.Write(BigEndian(commData.Length, 4));
        w.Write(commData);
        w.Write("SSND"u8.ToArray());
        w.Write(BigEndian(ssndData.Length, 4));
        w.Write(ssndData);
        w.Flush();
        return ms.ToArray();
    }

    public static byte[] Flac()
    {
        const int sampleRate = 44100;
        const int totalSamples = 4410;

        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write("fLaC"u8.ToArray());

        // STREAMINFO (34 bytes) is the only mandatory metadata block.
        w.Write((byte)0x80);              // last block + type 0
        w.Write((byte)0x00);
        w.Write((byte)0x00);
        w.Write((byte)0x22);
        w.Write((ushort)4096);            // min block size
        w.Write((ushort)4096);            // max block size
        w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); // min frame size
        w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); // max frame size

        var packed = ((ulong)sampleRate << 44) | (15UL << 36) | (ulong)totalSamples;
        for (var i = 7; i >= 0; i--)
        {
            w.Write((byte)((packed >> (i * 8)) & 0xFF));
        }

        w.Write(new byte[16]);            // md5 of the unencoded audio (unknown)
        w.Write(Payload);                 // stands in for the audio frames
        w.Flush();
        return ms.ToArray();
    }

    public static byte[] M4a()
    {
        var mvhd = Atom("mvhd", box =>
        {
            box.Write(BigEndian(0, 4));           // version + flags
            box.Write(BigEndian(0, 4));           // creation
            box.Write(BigEndian(0, 4));           // modification
            box.Write(BigEndian(1000, 4));        // timescale
            box.Write(BigEndian(1000, 4));        // duration
            box.Write(BigEndian(0x00010000, 4));  // rate
            box.Write(BigEndian(0x0100, 2));      // volume
            box.Write(new byte[10]);              // reserved
            box.Write(Matrix());
            box.Write(new byte[24]);              // pre_defined
            box.Write(BigEndian(2, 4));           // next track id
        });

        var tkhd = Atom("tkhd", box =>
        {
            box.Write(BigEndian(7, 4));
            box.Write(BigEndian(0, 4));
            box.Write(BigEndian(0, 4));
            box.Write(BigEndian(1, 4));           // track id
            box.Write(BigEndian(0, 4));
            box.Write(BigEndian(1000, 4));        // duration
            box.Write(new byte[8]);               // reserved
            box.Write(BigEndian(0, 2));           // layer
            box.Write(BigEndian(0, 2));           // alternate group
            box.Write(BigEndian(0x0100, 2));      // volume
            box.Write(BigEndian(0, 2));           // reserved
            box.Write(Matrix());
            box.Write(BigEndian(0, 4));           // width
            box.Write(BigEndian(0, 4));           // height
        });

        var mdhd = Atom("mdhd", box =>
        {
            box.Write(BigEndian(0, 4));
            box.Write(BigEndian(0, 4));
            box.Write(BigEndian(0, 4));
            box.Write(BigEndian(44100, 4));       // timescale
            box.Write(BigEndian(44100, 4));       // duration
            box.Write(BigEndian(0x55C4, 2));      // language 'und'
            box.Write(BigEndian(0, 2));
        });

        var hdlr = Atom("hdlr", box =>
        {
            box.Write(BigEndian(0, 4));
            box.Write(BigEndian(0, 4));
            box.Write("soun"u8.ToArray());
            box.Write(new byte[12]);
            box.Write(new byte[] { 0 });
        });

        var mp4a = Atom("mp4a", box =>
        {
            box.Write(new byte[6]);               // reserved
            box.Write(BigEndian(1, 2));           // data reference index
            box.Write(new byte[8]);               // version, revision, vendor
            box.Write(BigEndian(2, 2));           // channels
            box.Write(BigEndian(16, 2));          // sample size
            box.Write(BigEndian(0, 2));           // compression id
            box.Write(BigEndian(0, 2));           // packet size
            box.Write(BigEndian(44100 << 16, 4)); // sample rate (16.16)
        });

        var stsd = Atom("stsd", box =>
        {
            box.Write(BigEndian(0, 4));
            box.Write(BigEndian(1, 4));           // entry count
            box.Write(mp4a);
        });

        var stbl = Atom("stbl", box => box.Write(stsd));
        var smhd = Atom("smhd", box => { box.Write(BigEndian(0, 4)); box.Write(new byte[4]); });
        var dinf = Atom("dinf", _ => { });
        var minf = Atom("minf", box => { box.Write(smhd); box.Write(dinf); box.Write(stbl); });
        var mdia = Atom("mdia", box => { box.Write(mdhd); box.Write(hdlr); box.Write(minf); });
        var trak = Atom("trak", box => { box.Write(tkhd); box.Write(mdia); });

        var ftyp = Atom("ftyp", box =>
        {
            box.Write("M4A "u8.ToArray());
            box.Write(BigEndian(0, 4));
            box.Write("M4A "u8.ToArray());
            box.Write("mp42"u8.ToArray());
            box.Write("isom"u8.ToArray());
        });

        var moov = Atom("moov", box => { box.Write(mvhd); box.Write(trak); });
        var mdat = Atom("mdat", box => box.Write(Payload));

        return ftyp.Concat(moov).Concat(mdat).ToArray();
    }

    /// <summary>ADTS AAC stream: MPEG-4 LC, 44.1 kHz, stereo, four frames of <see cref="Payload"/>.</summary>
    public static byte[] Aac()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        for (var i = 0; i < 4; i++)
        {
            var frame = Payload.Skip(i * 128).Take(128).ToArray();
            var frameLength = 7 + frame.Length;

            w.Write((byte)0xFF);
            w.Write((byte)0xF1);
            w.Write((byte)(0x50 | ((frameLength >> 11) & 0x03)));
            w.Write((byte)((frameLength >> 3) & 0xFF));
            w.Write((byte)(((frameLength & 0x07) << 5) | 0x1F));
            w.Write((byte)0xFC);
            w.Write(frame);
        }

        w.Flush();
        return ms.ToArray();
    }

    public static byte[] Ogg()
    {
        var id = new List<byte> { 0x01 };
        id.AddRange("vorbis"u8.ToArray());
        id.AddRange(new byte[4]);
        id.Add(1);                       // channels
        id.AddRange(BigEndian(44100, 4)); // sample rate
        id.AddRange(new byte[12]);
        id.Add(0xB8);                    // blocksizes
        id.Add(0x01);                    // framing flag

        var comment = new List<byte> { 0x03 };
        comment.AddRange("vorbis"u8.ToArray());
        comment.AddRange(BigEndian(0, 4)); // vendor length
        comment.AddRange(BigEndian(0, 4)); // comment count
        comment.Add(0x01);                 // framing bit

        var setup = new List<byte> { 0x05 };
        setup.AddRange("vorbis"u8.ToArray());
        setup.AddRange(new byte[16]);

        return OggPage(0, 0x02, id.ToArray())
            .Concat(OggPage(1, 0x00, comment.ToArray()))
            .Concat(OggPage(2, 0x00, setup.ToArray()))
            .Concat(OggPage(3, 0x04, Payload))
            .ToArray();
    }

    public static byte[] Opus()
    {
        var id = new List<byte>();
        id.AddRange("OpusHead"u8.ToArray());
        id.Add(1);                        // version
        id.Add(1);                        // channels
        id.AddRange(BigEndian(312, 2));    // pre-skip
        id.AddRange(BigEndian(48000, 4));  // input sample rate
        id.AddRange(BigEndian(0, 2));      // output gain
        id.Add(0);                         // channel mapping family

        var comment = new List<byte>();
        comment.AddRange("OpusTags"u8.ToArray());
        comment.AddRange(BigEndian(0, 4));
        comment.AddRange(BigEndian(0, 4));

        return OggPage(0, 0x02, id.ToArray())
            .Concat(OggPage(1, 0x00, comment.ToArray()))
            .Concat(OggPage(2, 0x04, Payload))
            .ToArray();
    }

    private static byte[] OggPage(uint sequence, byte headerType, byte[] payload)
    {
        var lacing = new List<byte>();
        var remaining = payload.Length;
        while (remaining >= 255)
        {
            lacing.Add(255);
            remaining -= 255;
        }

        lacing.Add((byte)remaining);

        var page = new List<byte>();
        page.AddRange("OggS"u8.ToArray());
        page.Add(0);                      // version
        page.Add(headerType);
        page.AddRange(new byte[8]);       // granule position
        page.AddRange(BigEndian(101, 4)); // stream serial
        page.AddRange(BigEndian((int)sequence, 4));
        page.AddRange(new byte[4]);       // crc placeholder
        page.Add((byte)lacing.Count);
        page.AddRange(lacing);
        page.AddRange(payload);

        var bytes = page.ToArray();
        var crc = Crc32(bytes);
        BitConverter.GetBytes(crc).CopyTo(bytes, 22);
        return bytes;
    }

    private static byte[] Atom(string name, Action<BinaryWriter> body)
    {
        var inner = new MemoryStream();
        var w = new BinaryWriter(inner);
        body(w);
        w.Flush();
        var payload = inner.ToArray();

        var result = new MemoryStream();
        var rw = new BinaryWriter(result);
        rw.Write(BigEndian(payload.Length + 8, 4));
        rw.Write(Encoding.ASCII.GetBytes(name));
        rw.Write(payload);
        rw.Flush();
        return result.ToArray();
    }

    private static byte[] Matrix() =>
        BigEndian(0x00010000, 4).Concat(new byte[4]).Concat(new byte[4])
            .Concat(new byte[4]).Concat(BigEndian(0x00010000, 4)).Concat(new byte[4])
            .Concat(new byte[4]).Concat(new byte[4]).Concat(BigEndian(0x40000000, 4))
            .ToArray();

    private static byte[] BigEndian(int value, int bytes) =>
        Enumerable.Range(0, bytes).Reverse().Select(i => (byte)((value >> (i * 8)) & 0xFF)).ToArray();

    private static uint Crc32(byte[] data)
    {
        uint crc = 0;
        foreach (var b in data)
        {
            crc ^= (uint)b << 24;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
            }
        }

        return crc;
    }
}
