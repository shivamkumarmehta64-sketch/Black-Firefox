using System;
using System.IO;
using System.Linq;
using System.Text;

static class GenRes
{
    static void Main(string[] args)
    {
        string icoPath = args.Length > 0 ? args[0] : @"C:\Users\shiva\projects\Black-Firefox\icon.ico";
        string outPath = args.Length > 1 ? args[1] : @"C:\Users\shiva\projects\Black-Firefox\black.res";

        using (FileStream fs = File.Create(outPath))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            WriteNullResource(bw); // mandatory empty resource #0 at offset 0
            byte[] icoData = File.ReadAllBytes(icoPath);
            WriteIcons(bw, icoData);
            WriteVersion(bw);
            WriteManifest(bw);
        }
        Console.WriteLine("Wrote " + outPath + " (" + new FileInfo(outPath).Length + " bytes)");
    }

    // Empty resource #0: DataSize=0, HeaderSize=0x20, type=0, name=0, rest zeroed = 32 bytes
    static void WriteNullResource(BinaryWriter bw)
    {
        bw.Write((uint)0);       // DataSize
        bw.Write((uint)0x20);    // HeaderSize (includes these two fields)
        bw.Write((ushort)0xFFFF); bw.Write((ushort)0); // TYPE ordinal 0
        bw.Write((ushort)0xFFFF); bw.Write((ushort)0); // NAME ordinal 0
        bw.Write((uint)0);       // DataVersion
        bw.Write((ushort)0);     // MemoryFlags
        bw.Write((ushort)0);     // LanguageId
        bw.Write((uint)0);       // Version
        bw.Write((uint)0);       // Characteristics
    }

    static void WriteResEntry(BinaryWriter bw, ushort type, ushort name, ushort lang, byte[] data)
    {
        bw.Write((uint)data.Length);
        bw.Write((uint)0x20);
        bw.Write((ushort)0xFFFF); bw.Write((ushort)type); // TYPE ordinal
        bw.Write((ushort)0xFFFF); bw.Write((ushort)name); // NAME ordinal
        bw.Write((uint)0);      // data version
        bw.Write((ushort)0x1030);
        bw.Write((ushort)lang);
        bw.Write((uint)0);      // version
        bw.Write((uint)0);      // characteristics
        bw.Write(data);
        while ((bw.BaseStream.Position % 4) != 0) bw.Write((byte)0);
    }

    static void WriteIcons(BinaryWriter bw, byte[] ico)
    {
        // ICO: WORD reserved, WORD type, WORD count, then ICONDIRENTRY[], then image data
        int count = ReadU16(ico, 2);
        for (int i = 0; i < count; i++)
        {
            int off = 6 + i * 16;
            uint bytesInRes = ReadU32(ico, off + 8);
            uint imageOff = ReadU32(ico, off + 12);
            byte[] img = new byte[bytesInRes];
            Array.Copy(ico, imageOff, img, 0, bytesInRes);
            WriteResEntry(bw, 3, (ushort)(i + 1), 0, img); // RT_ICON
        }
        WriteResEntry(bw, 14, 1, 0, BuildGroupIcon(ico)); // RT_GROUP_ICON
    }

    static byte[] BuildGroupIcon(byte[] ico)
    {
        int count = ReadU16(ico, 2);
        MemoryStream m = new MemoryStream();
        BinaryWriter g = new BinaryWriter(m);
        g.Write((ushort)0); g.Write((ushort)1); g.Write((ushort)count);
        for (int i = 0; i < count; i++)
        {
            int off = 6 + i * 16;
            g.Write(ico[off]);            // width
            g.Write(ico[off + 1]);        // height
            g.Write(ico[off + 2]);        // colors
            g.Write((byte)0);             // reserved
            g.Write(ReadU16(ico, off + 4)); // planes
            g.Write(ReadU16(ico, off + 6)); // bitcount
            g.Write(ReadU32(ico, off + 8)); // bytes in res
            g.Write((ushort)(i + 1));     // id
        }
        return m.ToArray();
    }

    static ushort ReadU16(byte[] b, int off) { return (ushort)(b[off] | (b[off + 1] << 8)); }
    static uint ReadU32(byte[] b, int off) { return (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24)); }

    static void WriteVersion(BinaryWriter bw)
    {
        byte[] root = BuildVersionBlock();
        WriteResEntry(bw, 16, 1, 0x0409, root);
    }

    static void Align4(MemoryStream m)
    {
        while ((m.Position % 4) != 0) m.WriteByte(0);
    }

    static byte[] BuildVersionBlock()
    {
        // StringTable "040904B0": a String entry per key.
        string[] keys = { "CompanyName", "FileDescription", "FileVersion", "InternalName",
                          "LegalCopyright", "OriginalFilename", "ProductName", "ProductVersion" };
        string[] vals = { "Black", "Black Browser", "8.9.0.0", "Black",
                          "Black Browser", "Black.exe", "Black Browser", "8.9.0.0" };

        // Build StringFileInfo (wType=1)
        MemoryStream sfi = new MemoryStream();
        BinaryWriter s = new BinaryWriter(sfi);
        s.Write((ushort)0); s.Write((ushort)0); s.Write((ushort)1); // len filled later
        s.Write(Enc16("StringFileInfo\0")); Align4(sfi);

        // StringTable
        MemoryStream st = new MemoryStream();
        BinaryWriter t = new BinaryWriter(st);
        t.Write((ushort)0); t.Write((ushort)0); t.Write((ushort)1); // len filled later
        t.Write(Enc16("040904B0\0")); Align4(st);
        for (int i = 0; i < keys.Length; i++)
        {
            long entryStart = st.Position;
            byte[] keyB = Enc16(keys[i] + "\0");
            byte[] valB = Enc16(vals[i] + "\0"); // value INCLUDES null terminator
            t.Write((ushort)0);                                // wLength patched later
            t.Write((ushort)(valB.Length / 2));                // wValueLength = WCHAR count incl null
            t.Write((ushort)1);                                // wType
            t.Write(keyB);
            Align4(st);                                        // pad key to DWORD boundary
            t.Write(valB);
            Align4(st);                                        // pad value to DWORD boundary
            long entryEnd = st.Position;
            long saved = st.Position;
            st.Position = entryStart;
            t.Write((ushort)(entryEnd - entryStart));          // wLength incl padding
            st.Position = saved;
        }
        byte[] stBytes = st.ToArray();
        int stLen = stBytes.Length;
        stBytes[0] = (byte)(stLen & 0xFF); stBytes[1] = (byte)((stLen >> 8) & 0xFF);

        s.Write(stBytes);
        byte[] sfiBytes = sfi.ToArray();
        int sfiLen = sfiBytes.Length;
        sfiBytes[0] = (byte)(sfiLen & 0xFF); sfiBytes[1] = (byte)((sfiLen >> 8) & 0xFF);

        // Build VarFileInfo (wType=1)
        MemoryStream vfi = new MemoryStream();
        BinaryWriter v = new BinaryWriter(vfi);
        v.Write((ushort)0); v.Write((ushort)0); v.Write((ushort)1); // len filled later
        v.Write(Enc16("VarFileInfo\0")); Align4(vfi);
        // Var: Translation, wType=0
        MemoryStream var = new MemoryStream();
        BinaryWriter vr = new BinaryWriter(var);
        int varLen = 6 + Enc16("Translation\0").Length + 4;
        if ((varLen % 4) != 0) varLen += 4 - (varLen % 4);
        vr.Write((ushort)varLen); // wLength
        vr.Write((ushort)4);      // wValueLength
        vr.Write((ushort)0);      // wType
        vr.Write(Enc16("Translation\0"));
        Align4(var);
        vr.Write((uint)0x04B00409); // lang 0x0409, codepage 1200
        v.Write(var.ToArray());
        byte[] vfiBytes = vfi.ToArray();
        int vfiLen = vfiBytes.Length;
        vfiBytes[0] = (byte)(vfiLen & 0xFF); vfiBytes[1] = (byte)((vfiLen >> 8) & 0xFF);

        // Root VS_VERSION_INFO (wType=0)
        MemoryStream root = new MemoryStream();
        BinaryWriter r = new BinaryWriter(root);
        r.Write((ushort)0); r.Write((ushort)52); r.Write((ushort)0); // len filled later
        r.Write(Enc16("VS_VERSION_INFO\0")); Align4(root);
        r.Write((uint)0xFEEF04BD);
        r.Write((uint)0x00010000);
        r.Write((uint)0x00080009); // FileVersion MS 8.9
        r.Write((uint)0x00000000); // FileVersion LS
        r.Write((uint)0x00080009); // ProductVersion MS
        r.Write((uint)0x00000000); // ProductVersion LS
        r.Write((uint)0x3F);       // FileFlagsMask
        r.Write((uint)0);          // FileFlags
        r.Write((uint)0x00040004); // FileOS
        r.Write((uint)0x1);        // FileType (APP)
        r.Write((uint)0);          // FileSubtype
        r.Write((uint)0);          // FileDateMS
        r.Write((uint)0);          // FileDateLS
        r.Write(sfiBytes);
        r.Write(vfiBytes);
        byte[] rootBytes = root.ToArray();
        int rootLen = rootBytes.Length;
        rootBytes[0] = (byte)(rootLen & 0xFF); rootBytes[1] = (byte)((rootLen >> 8) & 0xFF);
        return rootBytes;
    }

    static byte[] Enc16(string s)
    {
        return Encoding.Unicode.GetBytes(s);
    }

    static void WriteManifest(BinaryWriter bw)
    {
        string manifest = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<assembly manifestVersion=\"1.0\" xmlns=\"urn:schemas-microsoft-com:asm.v1\">\r\n" +
        "  <trustInfo xmlns=\"urn:schemas-microsoft-com:asm.v3\">\r\n" +
        "    <security>\r\n" +
        "      <requestedPrivileges>\r\n" +
        "        <requestedExecutionLevel level=\"asInvoker\" uiAccess=\"false\" />\r\n" +
        "      </requestedPrivileges>\r\n" +
        "    </security>\r\n" +
        "  </trustInfo>\r\n" +
        "  <compatibility xmlns=\"urn:schemas-microsoft-com:compatibility.v1\">\r\n" +
        "    <application>\r\n" +
        "      <supportedOS Id=\"{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}\" />\r\n" +
        "    </application>\r\n" +
        "  </compatibility>\r\n" +
        "  <dependency>\r\n" +
        "    <dependentAssembly>\r\n" +
        "      <assemblyIdentity type=\"win32\" name=\"Microsoft.Windows.Common-Controls\" version=\"6.0.0.0\" processorArchitecture=\"*\" publicKeyToken=\"6595b64144ccf1df\" language=\"*\" />\r\n" +
        "    </dependentAssembly>\r\n" +
        "  </dependency>\r\n" +
        "</assembly>\r\n";
        WriteResEntry(bw, 24, 1, 0, Encoding.UTF8.GetBytes(manifest));
    }
}