﻿using System.IO;
using SoulsFormats.AC4;

namespace WitchyBND.Parsers;

public class WZERO3 : WFolderParser
{

    public override string Name => "Zero3";
    public override bool Is(string path)
    {
        return Zero3.Is(path);
    }

    public override bool IsUnpacked(string path)
    {
        return false;
    }

    public override void Unpack(string srcPath)
    {
        var z3 = Zero3.Read(srcPath);
        var targetDir = GetUnpackDestDir(srcPath);
        foreach (Zero3.File file in z3.Files)
        {
            string outPath = $@"{targetDir}\{file.Name.Replace('/', '\\')}";
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, file.Bytes);
        }
    }

    public override void Repack(string srcPath)
    {
        // Do nothing.
    }
}