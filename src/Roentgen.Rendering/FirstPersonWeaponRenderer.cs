using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roentgen.Rendering;

/// <summary>Camera-space weapon presentation. State comes from accepted combat feedback, never input guesses.</summary>
public sealed class FirstPersonWeaponRenderer : IDisposable
{
    private readonly GraphicsDevice device;
    private readonly BasicEffect effect;
    private readonly Dictionary<string,GpuMesh> meshes=[];
    private readonly RenderThreadGuard guard;
    private readonly GpuMesh flash;
    public FirstPersonWeaponRenderer(GraphicsDevice device,RenderThreadGuard guard)
    {
        this.device=device;this.guard=guard;
        effect=new BasicEffect(device){VertexColorEnabled=true,LightingEnabled=true,PreferPerPixelLighting=true};
        effect.EnableDefaultLighting();effect.AmbientLightColor=new Vector3(0.4f);
        meshes["sidearm"]=Build("sidearm");meshes["rifle"]=Build("rifle");meshes["melee"]=Build("melee");
        flash=GpuMesh.Upload(device,guard,ProceduralMeshFactory.CreateBox("muzzle-flash",new Vector3(.04f,.04f,.06f),new Color(255,218,139)));
    }
    private GpuMesh Build(string kind)
    {
        var vertices=new List<RoentgenVertex>();var indices=new List<int>();
        Color metal=new(91,105,109),dark=new(39,45,47),wood=new(130,82,47),skin=new(132,97,76);
        if(kind=="melee")
        {
            Part(new(.045f,.045f,.50f),new(0,0,-.15f),wood);
            Part(new(.23f,.11f,.11f),new(0,.03f,-.42f),metal);
        }
        else
        {
            bool rifle=kind=="rifle";
            Part(new(.09f,.10f,rifle?.38f:.18f),new(0,0,-.09f),metal);
            Part(new(.035f,.035f,rifle?.34f:.12f),new(0,.025f,rifle?-.41f:-.22f),dark);
            Part(new(.07f,.15f,.07f),new(0,-.11f,.015f),wood);
            Part(new(.018f,.026f,.025f),new(0,.067f,-.12f),dark);
            Part(new(.015f,.025f,.02f),new(0,.05f,rifle?-.56f:-.27f),dark);
            if(rifle){Part(new(.065f,.15f,.09f),new(0,-.11f,-.10f),dark);Part(new(.08f,.075f,.22f),new(0,-.02f,.18f),wood);}
        }
        Part(new(.085f,.10f,.12f),new(.025f,-.13f,.04f),skin);
        Part(new(.085f,.09f,.26f),new(.08f,-.18f,.20f),new Color(69,82,66));
        return GpuMesh.Upload(device,guard,new MeshData("view-"+kind,vertices,indices));
        void Part(Vector3 dimensions,Vector3 center,Color color)
        {
            var mesh=ProceduralMeshFactory.CreateBox("part",dimensions,color);int offset=vertices.Count;
            vertices.AddRange(mesh.Vertices.Select(v=>new RoentgenVertex(v.Position+center,v.Normal,v.TextureCoordinate,v.Color)));
            indices.AddRange(mesh.Indices.Select(i=>i+offset));
        }
    }
    public void Draw(string kind,float recoil,float reload,float sway,bool aiming,bool muzzleFlash)
    {
        effect.View=Matrix.Identity;effect.Projection=Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(65),device.Viewport.AspectRatio,.02f,5f);
        effect.World=Matrix.CreateRotationX(-recoil*.12f+reload*.65f)*Matrix.CreateRotationZ(reload*-.45f)*
            Matrix.CreateTranslation(aiming?.02f:.24f,(aiming?-.10f:-.23f)-reload*.12f+sway,-.48f+recoil*.08f);
        device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;device.BlendState=BlendState.Opaque;
        effect.LightingEnabled=true;effect.DiffuseColor=Vector3.One;
        meshes.TryGetValue(kind,out var mesh);mesh??=meshes["sidearm"];mesh.Bind(device);
        foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawIndexedPrimitives(PrimitiveType.TriangleList,0,0,mesh.PrimitiveCount);}
        if(muzzleFlash&&kind!="melee")
        {
            effect.World=Matrix.CreateTranslation(0,.025f,kind=="rifle"?-.61f:-.3f)*effect.World;
            effect.LightingEnabled=false;flash.Bind(device);
            foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawIndexedPrimitives(PrimitiveType.TriangleList,0,0,flash.PrimitiveCount);}
        }
        device.DepthStencilState=DepthStencilState.Default;
    }
    public void Dispose(){foreach(var mesh in meshes.Values)mesh.Dispose();flash.Dispose();effect.Dispose();}
}
