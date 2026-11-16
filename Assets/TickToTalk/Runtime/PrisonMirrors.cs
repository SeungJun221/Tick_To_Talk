using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TickToTalk
{
    // Local visual effect only; never changes network state or player visibility outside rendering.
    [DefaultExecutionOrder(1000)]
    public sealed class PrisonMirrors : MonoBehaviour
    {
        PrisonPresentation presentation;
        Camera reflection;
        readonly Renderer[] surfaces = new Renderer[8];
        readonly RenderTexture[] textures = new RenderTexture[8];
        Material frameMaterial;
        Mesh[] mirrorMeshes;
        void Start()
        {
            presentation = GetComponent<PrisonPresentation>();
            if (BindBakedMirrors()) return;
            frameMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            frameMaterial.color = new Color(.15f,.2f,.22f);
            frameMaterial.SetFloat("_Metallic", .85f);
            frameMaterial.SetFloat("_Smoothness", .7f);
            mirrorMeshes = new Mesh[3];
            for (int sector=0;sector<4;sector++) for (int room=0;room<2;room++)
            {
                int i=sector*2+room;
                float x = room==0 ? -6f : 6f;
                var root = new GameObject("Sector " + (sector+1) + " Cell " + (room==0?"A":"B") + " / Door mirror");
                root.transform.SetParent(transform,false);
                float wallZ = -4f+sector*9;
                root.transform.position = new Vector3(x,1.55f,wallZ+.24f);
                root.transform.rotation = Quaternion.Euler(0, sector == 0 ? 180f : 0f, 0);
                var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                face.name = "Reflective glass"; face.transform.SetParent(root.transform,false);
                face.transform.localRotation = Quaternion.identity;
                face.transform.localScale = new Vector3(1.3f,2.4f,1);
                Destroy(face.GetComponent<Collider>());
                if (sector > 0)
                {
                    int shape=sector-1;
                    if (!mirrorMeshes[shape]) mirrorMeshes[shape]=CreateMirrorMesh(shape);
                    face.GetComponent<MeshFilter>().sharedMesh=mirrorMeshes[shape];
                }
                surfaces[i] = face.GetComponent<Renderer>();
                surfaces[i].sharedMaterial = new Material(Resources.Load<Shader>("MirrorSurface"));
                surfaces[i].shadowCastingMode = ShadowCastingMode.Off;
                surfaces[i].receiveShadows = false;
                if (sector==0) BuildRectangularFrame(root.transform);
                else BuildShapedFrame(root.transform,sector-1);
            }
            reflection = new GameObject("Mirror reflection camera").AddComponent<Camera>();
            reflection.transform.SetParent(transform,false); reflection.enabled=false;
            var data=reflection.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=false; data.renderShadows=true;
        }

bool BindBakedMirrors()
{
    // Mirror root names contain '/', so Transform.Find would interpret them as paths.
    // Compare direct-child names instead and bind the editable scene objects in place.
    for(int sector=0;sector<4;sector++)for(int room=0;room<2;room++)
    {
        int i=sector*2+room;
        string mirrorName="Sector "+(sector+1)+" Cell "+(room==0?"A":"B")+" / Door mirror";
        Transform mirrorRoot=null;
        foreach(Transform child in transform)
            if(child.name==mirrorName){mirrorRoot=child;break;}
        if(!mirrorRoot)return false;

        var face=mirrorRoot.Find("Reflective glass");
        if(!face||!face.TryGetComponent<Renderer>(out surfaces[i]))return false;
        surfaces[i].shadowCastingMode=ShadowCastingMode.Off;
        surfaces[i].receiveShadows=false;
    }

    reflection=transform.Find("Mirror reflection camera")?.GetComponent<Camera>();
    if(!reflection)reflection=new GameObject("Mirror reflection camera").AddComponent<Camera>();
    reflection.transform.SetParent(transform,false);reflection.enabled=false;
    var data=reflection.GetUniversalAdditionalCameraData();
    data.renderPostProcessing=false;data.renderShadows=true;
    return true;
}

        static Vector2 Outline(int shape, float angle)
        {
            float x=Mathf.Cos(angle), y=Mathf.Sin(angle);
            if (shape==0) return new Vector2(x,y);
            if (shape==1) return new Vector2(x*.86f,y*(y>0?1.08f:.85f));
            float octagon=Mathf.Max(Mathf.Abs(x),Mathf.Abs(y));
            return new Vector2(x/octagon*.87f,y/octagon*.87f);
        }
        static Mesh CreateMirrorMesh(int shape)
        {
            const int count=64;
            var vertices=new Vector3[count+1];var uv=new Vector2[count+1];var triangles=new int[count*3];
            uv[0]=new Vector2(.5f,.5f);
            for(int n=0;n<count;n++)
            {
                float a=2*Mathf.PI*n/count;
                var point=Outline(shape,a);
                vertices[n+1]=new Vector3(point.x*.5f,point.y*.5f,0);
                uv[n+1]=new Vector2(.5f+point.x*.5f,.5f+point.y*.5f);
                triangles[n*3]=0;triangles[n*3+1]=n+1;triangles[n*3+2]=(n+1)%count+1;
            }
            var mesh=new Mesh{name="Sector mirror shape " + shape,vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateNormals();return mesh;
        }
        void BuildRectangularFrame(Transform root)
        {
            for(int k=0;k<4;k++)
            {
                var rail=GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name="Brushed steel frame"; rail.transform.SetParent(root,false);
                rail.transform.localPosition=k<2?new Vector3(k==0?-.69f:.69f,0,0):new Vector3(0,k==2?-1.24f:1.24f,0);
                rail.transform.localScale=k<2?new Vector3(.08f,2.56f,.09f):new Vector3(1.3f,.08f,.09f);
                rail.GetComponent<Renderer>().sharedMaterial=frameMaterial;
                Destroy(rail.GetComponent<Collider>());
            }
        }
        void BuildShapedFrame(Transform root,int shape)
        {
            const int segments=64;
            for(int n=0;n<segments;n++)
            {
                float a=2*Mathf.PI*n/segments,b=2*Mathf.PI*(n+1)/segments;
                Vector2 p=Outline(shape,a),q=Outline(shape,b);
                Vector3 from=new Vector3(p.x*.65f,p.y*1.2f,0),to=new Vector3(q.x*.65f,q.y*1.2f,0);
                var rail=GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name="Shaped mirror frame";rail.transform.SetParent(root,false);
                rail.transform.localPosition=(from+to)*.5f;
                rail.transform.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);
                rail.transform.localScale=new Vector3(.045f,(to-from).magnitude+.008f,.08f);
                rail.GetComponent<Renderer>().sharedMaterial=frameMaterial;
                Destroy(rail.GetComponent<Collider>());
            }
        }
        void LateUpdate()
        {
            var session=NetworkSession.Instance;
            if (!reflection || !session || session.View==null || session.Dedicated) return;
            var eye=presentation.eye;
            var planes=GeometryUtility.CalculateFrustumPlanes(eye);
            for(int i=0;i<surfaces.Length;i++)
            {
                var surface=surfaces[i];if(!surface)continue;
                if (!GeometryUtility.TestPlanesAABB(planes,surface.bounds) || Mathf.Abs(eye.transform.position.z-surface.transform.position.z)>8f) continue;
                int width=Mathf.Clamp(eye.pixelWidth/2,512,1024), height=Mathf.Max(256,Mathf.RoundToInt(width/eye.aspect));
                if(!textures[i] || textures[i].width!=width || textures[i].height!=height)
                {
                    if(textures[i]) {textures[i].Release();Destroy(textures[i]);}
                    var descriptor=new RenderTextureDescriptor(width,height,RenderTextureFormat.ARGB32,24){msaaSamples=4};
                    descriptor.msaaSamples=SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
                    textures[i]=new RenderTexture(descriptor){name="Cell mirror "+i,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                    textures[i].Create();surface.sharedMaterial.SetTexture("_ReflectionTex",textures[i]);
                }
                reflection.CopyFrom(eye); reflection.enabled=false; reflection.allowMSAA=true; reflection.allowDynamicResolution=false;
                reflection.cameraType=CameraType.Reflection;
                reflection.targetTexture=textures[i]; reflection.cullingMask=(eye.cullingMask | (1<<30)) & ~((1<<31)|(1<<29));
                Vector3 planeNormal=surface.transform.forward;
                if(Vector3.Dot(eye.transform.position-surface.transform.position,planeNormal)<0)planeNormal=-planeNormal;
                float planeDistance=Vector3.Dot(planeNormal,surface.transform.position);
                var reflect=Matrix4x4.identity;
                for(int row=0;row<3;row++)
                {
                    for(int col=0;col<3;col++) reflect[row,col]-=2f*planeNormal[row]*planeNormal[col];
                    reflect[row,3]=2f*planeDistance*planeNormal[row];
                }
                reflection.transform.position=reflect.MultiplyPoint(eye.transform.position);
                reflection.transform.rotation=Quaternion.LookRotation(reflect.MultiplyVector(eye.transform.forward),reflect.MultiplyVector(eye.transform.up));
                reflection.worldToCameraMatrix=eye.worldToCameraMatrix*reflect;
                Vector3 point=reflection.worldToCameraMatrix.MultiplyPoint(surface.transform.position+planeNormal*.015f);
                Vector3 normal=reflection.worldToCameraMatrix.MultiplyVector(planeNormal).normalized;
                reflection.projectionMatrix=reflection.CalculateObliqueMatrix(new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal)));
                surface.sharedMaterial.SetMatrix("_ReflectionVP", GL.GetGPUProjectionMatrix(reflection.projectionMatrix,true)*reflection.worldToCameraMatrix);
                var actor=session.View.slot==0?presentation.playerA:presentation.playerB;
                var renderers=actor.GetComponentsInChildren<Renderer>();
                var states=new bool[renderers.Length];
                bool oldCulling=GL.invertCulling;
                try
                {
                    for(int r=0;r<renderers.Length;r++){states[r]=renderers[r].enabled;renderers[r].enabled=true;}
                    foreach(var mirror in surfaces) mirror.enabled=false;
                    GL.invertCulling=!oldCulling;
                    RenderPipeline.SubmitRenderRequest(reflection,new UniversalRenderPipeline.SingleCameraRequest { destination=textures[i] });
                }
                finally
                {
                    GL.invertCulling=oldCulling;
                    for(int r=0;r<renderers.Length;r++)renderers[r].enabled=states[r];
                    foreach(var mirror in surfaces)mirror.enabled=true;
                }
            }
        }
        void OnDestroy()
        {
            foreach(var texture in textures) if(texture){texture.Release();Destroy(texture);}
            foreach(var surface in surfaces) if(surface)Destroy(surface.sharedMaterial);
            if(frameMaterial)Destroy(frameMaterial);
            if(mirrorMeshes!=null)foreach(var mesh in mirrorMeshes)if(mesh)Destroy(mesh);
        }
    }
}
