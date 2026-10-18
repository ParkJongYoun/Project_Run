using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIItem_ComicBurst : MaskableGraphic
    {
        public float Phase;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;Vector2 center=new Vector2(Mathf.Sin(Phase)*40,Mathf.Cos(Phase)*24);
            for(int i=0;i<76;i++)
            {
                float angle=(i*4.7368f+Phase*.8f)*Mathf.Deg2Rad;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var side=new Vector2(-direction.y,direction.x);
                float inner=140+(i%7)*32+Mathf.Sin(Phase*7+i)*14;float outer=Mathf.Max(r.width,r.height);
                int start=mesh.currentVertCount;
                var v=UIVertex.simpleVert;v.color=color;
                v.position=center+direction*inner;mesh.AddVert(v);
                v.position=center+direction*outer+side*(i%3==0?9:3);mesh.AddVert(v);
                v.position=center+direction*outer-side*(i%3==0?9:3);mesh.AddVert(v);
                mesh.AddTriangle(start,start+1,start+2);
            }
        }
        public void Animate(float value){Phase=value;SetVerticesDirty();}
    }
}
