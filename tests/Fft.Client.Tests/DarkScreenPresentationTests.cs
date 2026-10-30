using System.Collections.Immutable;
using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Tests.Support;
using Zodiac.Content;
using Zodiac.Framework.Events;
using Xunit;

namespace Fft.Client.Tests;

public sealed class DarkScreenPresentationTests
{
    private static string Temp() => Path.Combine(Path.GetTempPath(), "fft-dark-client-" + Guid.NewGuid().ToString("N"));
    private static DarkScreenQuad Rect(int x,int y,int w,int h,int color=48) => new(new(x,y),new(x+w,y),new(x,y+h),new(x+w,y+h),color,color,color);
    private static byte[] Background() { var data = new byte[256*240*4]; for(int i=0;i<data.Length;i+=4) { data[i]=data[i+1]=data[i+2]=128;data[i+3]=255; } return data; }
    [Fact]
    public void Rectangle_diagonal_and_shared_edges_are_blended_once()
    {
        var result = DarkScreenPresentation.Compose([Rect(0,0,16,16),Rect(16,0,16,16)],Background());
        for(int y=0;y<16;y++) for(int x=0;x<32;x++) Assert.Equal(88,result[(y*256+x)*4]);
        Assert.Equal(128,result[32*4]);
    }
    [Fact]
    public void Overlaps_are_separate_passes_and_preserve_order()
    {
        var a = Rect(0,0,8,8,0); var b = Rect(0,0,8,8,248);
        Assert.Equal(152,DarkScreenPresentation.Compose([a,b],Background())[0]);
        Assert.Equal(88,DarkScreenPresentation.Compose([b,a],Background())[0]);
    }
    [Fact]
    public void Signed_clipping_and_degenerate_quads_preserve_background()
    {
        var background=Background(); var copy=background.ToArray();
        var result=DarkScreenPresentation.Compose([Rect(-8,-8,16,16),Rect(0,0,0,0),Rect(1000,1000,8,8)],background);
        Assert.Equal(88,result[0]);Assert.Equal(128,result[(8*256+8)*4]);Assert.Equal(copy,background);
        Assert.Equal(copy,DarkScreenPresentation.Compose([],background));
    }
    [Fact]
    public void Rotated_diamond_edges_are_half_open_with_single_center_coverage()
    {
        var quad=new DarkScreenQuad(new(12,0),new(24,12),new(0,12),new(12,24));
        var result=DarkScreenPresentation.Compose([quad],Background());
        Assert.Equal(88,result[(12*256+12)*4]);Assert.Equal(128,result[0]);
        Assert.All(Enumerable.Range(0,256*240), i=>Assert.Contains(result[i*4],new byte[]{88,128}));
    }
    [Theory]
    [InlineData("alpha")] [InlineData("precision")] [InlineData("length")] [InlineData("draws")] [InlineData("bounds")] [InlineData("color")]
    public void Invalid_explicit_state_rejects_before_composition(string kind)
    {
        var bg=Background(); ImmutableArray<DarkScreenQuad> draws=[];
        switch(kind)
        {
            case "alpha":bg[3]=0;break; case "precision":bg[0]=129;break;case "length":bg=new byte[1];break;
            case "draws":draws=default;break;case "bounds":draws=[Rect(-1025,0,1,1)];break;case "color":draws=[Rect(0,0,1,1,256)];break;
        }
        Assert.Throws<ArgumentException>(()=>DarkScreenPresentation.Compose(draws,bg));
    }
    [Theory]
    [InlineData(0)] [InlineData(44)] [InlineData(255)]
    public void Actual_prepared_session_replays_independently_of_composition(int hold)
    {
        var root=AuthoredDarkScreen.Write(Temp(),AuthoredDarkScreen.Definition(hold));
        var session=DarkScreenSession.Load(LooseAssetStore.Load(root),AuthoredDarkScreen.Id);
        var a=session.Start().State;var b=session.Start().State;
        while(!a.Event.Exited)
        {
            if(a.Event.Tick is 0 or 5 or 55 or 56 || a.Worker!.IsTerminal)
            {
                var before=JsonSerializer.Serialize(a);
                var quads=DarkScreenPresentation.Project(session.Definition,a.Worker);
                var first=DarkScreenPresentation.Compose(quads,DarkScreenPresentation.CheckerBackground());
                Assert.Equal(first,DarkScreenPresentation.Compose(quads,DarkScreenPresentation.CheckerBackground()));
                Assert.Equal(before,JsonSerializer.Serialize(a));
                if(a.Event.Tick==55) Assert.Equal(288,quads.Length);
            }
            var ar=DarkScreenExecutor.AdvanceTick(a,[],8);var br=DarkScreenExecutor.AdvanceTick(b,[],8);
            Assert.True(ar.Accepted,ar.Diagnostic);Assert.True(br.Accepted,br.Diagnostic);
            Assert.Equal(JsonSerializer.Serialize(ar),JsonSerializer.Serialize(br));a=ar.State;b=br.State;
        }
        Assert.Equal(113+hold,a.Event.Tick);
    }
    [Fact]
    public void Projection_is_reversed_and_uses_signed_truncation_with_no_textures()
    {
        var definition=AuthoredDarkScreen.Definition();var root=AuthoredDarkScreen.Write(Temp(),definition);
        var session=DarkScreenSession.Load(LooseAssetStore.Load(root),AuthoredDarkScreen.Id);var state=session.Start().State;
        while(state.Event.Tick<55) state=DarkScreenExecutor.AdvanceTick(state,[],8).State;
        var quads=DarkScreenPresentation.Project(definition,state.Worker);
        Assert.Equal(DarkScreenPresentation.Quad(definition,new(287,512,48)),quads[0]);
        definition=definition with{ Rotations=definition.Rotations.SetItem(0,new(0,1,4096,-4096,-1))};
        var q=DarkScreenPresentation.Quad(definition,new(0,0,4));
        Assert.Equal(new DarkScreenPoint(-12,-11),q.A);Assert.Equal(new DarkScreenPoint(-13,-12),q.B);
        Assert.Throws<ArgumentException>(()=>DarkScreenPresentation.Project(definition with {Id="sample:wrong/id"},state.Worker));
    }
    [Theory]
    [InlineData("--dark-screen")] [InlineData("--ready")] [InlineData("bogus")]
    public void Preview_options_reject_missing_or_cross_profile_arguments(string first)
        =>Assert.Throws<ArgumentException>(()=>DarkScreenPreviewOptions.Parse([first]));
    [Fact]
    public void Readiness_rejects_before_a_scene_exists()
    {
        var root=AuthoredDarkScreen.Write(Temp());File.Delete(Path.Combine(root,"dark-screen.json"));
        Assert.Throws<FileNotFoundException>(()=>DarkScreenSession.Load(LooseAssetStore.Load(root),AuthoredDarkScreen.Id));
        Assert.Throws<ArgumentException>(()=>DarkScreenPreviewOptions.Parse(["--dark-screen","--content-root","x","--id",AuthoredDarkScreen.Id.Value,"--circle-tick","1"]));
    }
}
