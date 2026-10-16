
using Roentgen.Audio.MonoGame;
namespace Roentgen.Audio.MonoGame.Tests;
public sealed class Phase18PcmTests
{
    [Fact] public void Converter_clamps_and_reports_invalid_samples()
    {
        float[] input=[0f,1f,-1f,2f,float.NaN]; byte[] output=new byte[input.Length*2];
        Pcm16Converter.Convert(input,output,out int nonfinite,out int clipped);
        Assert.Equal(1,nonfinite); Assert.Equal(1,clipped);
    }
    [Fact] public void Ring_is_bounded_fifo()
    {
        var ring=new PcmBlockRingBuffer(3,4); Assert.True(ring.TryGetWriteSpan(out Span<byte> first,out int token)); first[0]=7; ring.CommitWrite(token);
        Assert.True(ring.TryRead(out byte[] read)); Assert.Equal(7,read[0]); Assert.False(ring.TryRead(out _));
    }
}
