using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

/// <summary>Small reusable deterministic tile textures; works with the default BasicEffect path.</summary>
public sealed class SurfaceTextureLibrary : IDisposable
{
    private readonly GraphicsDevice device;
    private readonly Dictionary<string,Texture2D> textures=new(StringComparer.Ordinal);
    public SurfaceTextureLibrary(GraphicsDevice device)=>this.device=device;
    public Texture2D? Resolve(string id)
    {
        if(!id.StartsWith("structure-",StringComparison.Ordinal)&&!id.Contains("terrain",StringComparison.Ordinal))return null;
        if(textures.TryGetValue(id,out var existing))return existing;
        const int size=128;var pixels=new Color[size*size];
        uint seed=2166136261;foreach(char c in id)seed=(seed^c)*16777619;
        bool metal=id.Contains("metal",StringComparison.Ordinal)||id.Contains("steel",StringComparison.Ordinal)||id.Contains("roof",StringComparison.Ordinal);
        bool masonry=id.Contains("masonry",StringComparison.Ordinal);bool timber=id.Contains("timber",StringComparison.Ordinal);
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            uint n=seed^(uint)(x*374761393)^(uint)(y*668265263);n=(n^(n>>13))*1274126177;n^=n>>16;
            float noise=(n&255)/255f;
            float v=0.80f+noise*0.17f;
            if(masonry&&(y%32<3||(x+(y/32%2)*32)%64<3))v=0.43f;
            if(metal){v-=x%32<2?0.22f:0;v+=MathF.Sin(x*MathF.PI/8)*0.06f;}
            if(timber){v-=y%32<2?0.24f:0;v+=MathF.Sin(x*0.2f+MathF.Sin(y*0.16f))*0.06f;}
            if(noise<0.025f)v*=0.6f;
            pixels[y*size+x]=new Color(v,v*(metal?0.97f:1f),v*(metal?0.91f:1f));
        }
        var texture=new Texture2D(device,size,size,false,SurfaceFormat.Color);texture.SetData(pixels);textures.Add(id,texture);return texture;
    }
    public void Dispose(){foreach(var texture in textures.Values)texture.Dispose();textures.Clear();}
}
