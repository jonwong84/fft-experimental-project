using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Zodiac.AssetTool.Cli;
using Zodiac.Content.ContentPackages;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class OriginalThrowStoneImporterTests
{
    private static int Offset(uint address)=>checked((int)(address-0x80010000+0x800));
    [Fact]
    public void Synthetic_decode_preserves_historical_ID_all_source_records_and_metadata_without_certifying_edition()
    {
        var image=Image(); var before=image.ToArray(); var metadata=OriginalMetadataImporter.DecodeSelection(image).Package;
        string original=metadata.ToJson(); var selected=OriginalThrowStoneImporter.DecodeSelection(image,metadata);
        selected.Package.ValidateAgainst(metadata); Assert.Equal(original,metadata.ToJson()); Assert.Equal(before,image);
        Assert.Equal("fft.ps1:ability/source-0148",Assert.Single(selected.Package.Abilities).Id.Value);
        Assert.Equal(6,selected.Records.Length);
        foreach(var row in selected.Records)
        { Assert.Equal(Offset(row.VirtualAddress),row.FileOffset); Assert.Equal(Convert.ToHexStringLower(image.AsSpan(row.FileOffset,row.ByteLength)),row.RawHex); }
        string json=OriginalThrowStoneImporter.ToJson(selected);
        Assert.Equal(json,OriginalThrowStoneImporter.ToJson(OriginalThrowStoneImporter.DecodeSelection(image,metadata)));
        var parsed=JsonNode.Parse(json)!; Assert.Null(parsed["targetEdition"]); Assert.Null(parsed["input"]!["sha256"]);
        Assert.Equal("unverified-layout-decoding",parsed["confidence"]!.GetValue<string>());
        Assert.Throws<InvalidDataException>(()=>OriginalThrowStoneImporter.ImportVerified(image,metadata));
        Assert.Equal(selected.Package.ToJson(),TargetedAbilityContentPackage.ReadJson(selected.Package.ToJson()).ToJson());
        Array.Clear(image); Assert.Equal(json,OriginalThrowStoneImporter.ToJson(selected));
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    public void All_fourteen_secondary_fields_are_semantically_validated(int field)
    {
        var image=Image(); var metadata=OriginalMetadataImporter.DecodeSelection(image).Package;
        image[Offset(0x8005fbf0+148*14)+field]^=0xff; var before=image.ToArray();
        Assert.Throws<InvalidDataException>(()=>OriginalThrowStoneImporter.DecodeSelection(image,metadata)); Assert.Equal(before,image);
    }

    [Theory] [InlineData("job")] [InlineData("slot")] [InlineData("slot-high-bit")] [InlineData("menu")] [InlineData("status")]
    [InlineData("learning-jp")] [InlineData("learning-rate")] [InlineData("learning-type")] [InlineData("learning-ai")]
    [InlineData("header")] [InlineData("length")]
    public void Selected_records_and_learning_dependency_must_agree(string kind)
    {
        var image=Image(); var metadata=OriginalMetadataImporter.DecodeSelection(image).Package;
        switch(kind)
        {
            case "job": image[Offset(0x800610b8+74*48)]=6; break;
            case "slot": image[Offset(0x80064a94+5*25)+5]=147; break;
            case "slot-high-bit": image[Offset(0x80064a94+5*25)]|=0x20; break;
            case "menu": image[Offset(0x80065cb4+5)]=8; break;
            case "status": image[Offset(0x80063fc4)+1]=1; break;
            case "learning-jp": image[Offset(0x8005ebf0+148*8)]++; break;
            case "learning-rate": image[Offset(0x8005ebf0+148*8)+2]++; break;
            case "learning-type": image[Offset(0x8005ebf0+148*8)+3]++; break;
            case "learning-ai": image[Offset(0x8005ebf0+148*8)+7]++; break;
            case "header": image[0]=0; break;
            case "length": image=image[..0x900]; break;
        }
        Assert.Throws<InvalidDataException>(()=>OriginalThrowStoneImporter.DecodeSelection(image,metadata));
    }

    [Theory] [InlineData("move")] [InlineData("jump")] [InlineData("extra-slot")]
    public void Whole_canonical_metadata_dependency_must_match_without_being_rewritten(string kind)
    {
        var image=Image(); string source=OriginalMetadataImporter.DecodeSelection(image).Package.ToJson();
        var json=JsonNode.Parse(source)!;
        if(kind=="move") json["jobs"]![0]!["baseMove"]=9;
        else if(kind=="jump") json["jobs"]![0]!["baseJump"]=9;
        else json["skillsets"]![0]!["slots"]![0]!["abilityId"]=OriginalThrowStoneImporter.AbilityId;
        var metadata=ContentPackage.ReadJson(json.ToJsonString()); string before=metadata.ToJson();
        Assert.Throws<InvalidDataException>(()=>OriginalThrowStoneImporter.DecodeSelection(image,metadata));
        Assert.Equal(before,metadata.ToJson());
    }

    [Theory] [InlineData("missing")] [InlineData("outside")] [InlineData("hash")] [InlineData("traversal")]
    [InlineData("duplicate")] [InlineData("existing")]
    public void Command_preserves_outputs_on_missing_dependency_path_hash_and_option_failures(string kind)
    {
        string workspace=Path.Combine(Path.GetTempPath(),"fft-throw-import-"+Guid.NewGuid().ToString("N"));
        string scratch=Path.Combine(workspace,"scratch"); Directory.CreateDirectory(scratch);
        string exe=Path.Combine(scratch,"synthetic.exe"),metadata=Path.Combine(scratch,"metadata.json"),destination=Path.Combine(scratch,"output");
        File.WriteAllBytes(exe,Image()); File.WriteAllText(metadata,OriginalMetadataImporter.DecodeSelection(Image()).Package.ToJson());
        if(kind=="existing") Directory.CreateDirectory(destination);
        try
        {
            var args=new List<string>{"import-throw-stone","--exe",kind=="outside"?Path.Combine(workspace,"outside.exe"):exe,"--output",
                kind=="traversal"?Path.Combine(scratch,"..","outside"):destination};
            if(kind!="missing") args.AddRange(["--metadata",metadata]);
            if(kind=="duplicate") args.AddRange(["--exe",exe]);
            using var output=new StringWriter(); using var error=new StringWriter();
            Assert.NotEqual(0,new ImportMetadataCommand(workspace).Run(args.ToArray(),output,error));
            Assert.Empty(output.ToString()); Assert.NotEmpty(error.ToString());
            Assert.Equal(kind=="existing",Directory.Exists(destination));
            Assert.Equal(2,Directory.GetFiles(scratch).Length);
        }
        finally { File.Delete(exe); File.Delete(metadata); if(Directory.Exists(destination)) Directory.Delete(destination); Directory.Delete(scratch); Directory.Delete(workspace); }
    }

    private static byte[] Image()
    {
        var image=new byte[OriginalMetadataImporter.ExecutableLength]; "PS-X EXE"u8.CopyTo(image);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x18),0x80010000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c),(uint)image.Length-0x800);
        int job=Offset(0x800610b8+74*48); image[job]=5; image[job+0x17]=2; image[job+0x18]=2;
        image[Offset(0x80064a94+5*25)+5]=148;
        new byte[]{90,0,80,65,130,2,65,129}.CopyTo(image,Offset(0x8005ebf0+148*8));
        new byte[]{4,0,0,1,17,0,34,0,55,0,2,0,0,0}.CopyTo(image,Offset(0x8005fbf0+148*14));
        return image;
    }
}
