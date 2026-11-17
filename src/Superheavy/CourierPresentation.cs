using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Japensou.NuclearOption.AirframeData;

namespace Japensou.NuclearOption.CurrentDamage
{
    // Presentation only: no collider/body/HP/joint/fuel/control writes here.
    public sealed class CourierPresentation:MonoBehaviour
    {
        private static readonly HashSet<CourierPresentation> instances=new HashSet<CourierPresentation>();
        private sealed class Row {internal int Index,Triangles;internal MeshFilter Filter;internal Renderer Renderer;internal NativePartitionSurface Meta;internal Mesh Selected;internal UnityEngine.Rendering.ShadowCastingMode Shadow;}
        private readonly Dictionary<MeshFilter,Row> lookup=new Dictionary<MeshFilter,Row>();
        private readonly Dictionary<string,int> stages=new Dictionary<string,int>();
        private CourierPresentationData data;private Row[] rows;private NativePartitionController graph;private Camera camera;private float nextPoll;
        private int distanceLevel;private CourierQuality preset;
        private bool cameraInside,accessOpen;
        public int ActiveTriangles,QualityChanges;
#if COURIER_BALANCED_ONLY
        internal double WorkBudgetMs=>.65;
        internal int RimBudget=>512;
        internal int WorkBatch=>8;
        private int MeshLevel=>1;
#else
        internal double WorkBudgetMs=>preset==CourierQuality.Performance?.35:preset==CourierQuality.Balanced?.65:1.0;
        internal int RimBudget=>preset==CourierQuality.Performance?256:preset==CourierQuality.Balanced?512:1024;
        internal int WorkBatch=>preset==CourierQuality.Performance?4:preset==CourierQuality.Balanced?8:16;
        private int MeshLevel=>Math.Max(distanceLevel,preset==CourierQuality.Performance?2:preset==CourierQuality.Balanced?1:0);
#endif
        internal bool Available=>data&&rows!=null;
        internal void Bind(NativePartitionController owner)
        {
            graph=owner;data=GetComponent<CourierPresentationData>();if(!data)return;
            ActiveTriangles=0;QualityChanges=0;
            int n=data.Filters?.Length??0;
            if(n==0||data.Detailed.Length!=n||data.Balanced.Length!=n||data.Performance.Length!=n||data.Owners.Length!=n||data.Interior.Length!=n||data.BakedDamage.Length!=n)throw new InvalidOperationException("Courier presentation bindings differ.");
            rows=new Row[n];for(int i=0;i<n;i++)
            {
                var f=data.Filters[i];if(!f||!data.Detailed[i]||!data.Balanced[i]||!data.Performance[i])throw new InvalidOperationException("Courier presentation mesh missing.");
                var renderer=f.GetComponent<Renderer>();var meta=f.GetComponent<NativePartitionSurface>();if(meta){meta.Interior=data.Interior[i];meta.BakedInterior=data.BakedDamage[i];meta.InteriorFurniture=meta.SourceOriginal.StartsWith("SHGT_Cabin material ")||meta.SourceOriginal.StartsWith("SHGT_CrewCompartments material ");meta.PlanningOriginal=data.PlanningSources!=null&&data.PlanningSources.Length==n?data.PlanningSources[i]:null;}
                rows[i]=new Row{Index=i,Filter=f,Renderer=renderer,Meta=meta,Shadow=data.ShadowModes!=null&&data.ShadowModes.Length==n?(UnityEngine.Rendering.ShadowCastingMode)data.ShadowModes[i]:renderer.shadowCastingMode};
                lookup.Add(f,rows[i]);
            }
            instances.Add(this);Apply();PreviewVisibility();
        }
        internal static void ApplyToAll(){foreach(var instance in instances)if(instance&&instance.Available){instance.Apply();instance.graph.Damage?.QualityChanged();instance.graph.Refresh();}}
        private void Update()
        {
            if(!Available||Time.unscaledTime<nextPoll)return;nextPoll=Time.unscaledTime+.2f;
            if(!camera)camera=Camera.main;float distance=camera?(camera.transform.position-transform.position).magnitude:0;
            int level=distance>600?2:distance>250?1:0;
            if(level!=distanceLevel||preset!=CurrentAirframeDamagePlugin.SelectedQuality){distanceLevel=level;Apply();graph.Damage?.QualityChanged();}
            if(!graph.Ready)PreviewVisibility();
        }
        internal void Health(string id,int stage)
        {
            if(!Available)return;if(stages.TryGetValue(id,out var old)&&old==stage)return;stages[id]=stage;
            foreach(var row in rows)if(data.BakedDamage[row.Index]&&data.Owners[row.Index]==id)Apply(row);
        }
        private void Apply()
        {
            preset=CurrentAirframeDamagePlugin.SelectedQuality;
            foreach(var row in rows)Apply(row);QualityChanges++;
        }
        private void Apply(Row row)
        {
            int i=row.Index,level=MeshLevel;stages.TryGetValue(data.Owners[i],out var stage);
            Mesh mesh=level==2?data.Performance[i]:level==1?data.Balanced[i]:data.Detailed[i];
            if(data.BakedDamage[i]&&stage>0)
            {
                var damaged=level==2?(stage==1?data.PerformanceDamage1:data.PerformanceDamage2):level==1?(stage==1?data.BalancedDamage1:data.BalancedDamage2):(stage==1?data.DetailedDamage1:data.DetailedDamage2);
                if(damaged!=null&&i<damaged.Length&&damaged[i])mesh=damaged[i];
            }
            row.Selected=mesh;
            if(row.Meta==null||row.Meta.BakedInterior||!graph.Damage)row.Filter.sharedMesh=mesh;
            // Exterior self-shadowing is part of the authored finish. Drop
            // only interior casting in the light preset, preserving nacelles.
            row.Renderer.shadowCastingMode=level==2&&data.Interior[i]?UnityEngine.Rendering.ShadowCastingMode.Off:row.Shadow;
            int count=0;for(int slot=0;slot<mesh.subMeshCount;slot++)count+=(int)mesh.GetIndexCount(slot)/3;
            ActiveTriangles+=count-row.Triangles;row.Triangles=count;
        }
        internal Mesh SourceMesh(MeshFilter filter,Mesh fallback)=>lookup.TryGetValue(filter,out var row)?row.Selected:fallback;
        internal Mesh PaintMesh(MeshFilter filter,int channel,Mesh fallback)
        {
            if(channel==0)return SourceMesh(filter,fallback);if(!lookup.TryGetValue(filter,out var row))return fallback;
            int level=MeshLevel;if(level==0)return fallback;
            var array=level==2?(channel==1?data.PerformancePaint1:data.PerformancePaint2):(channel==1?data.BalancedPaint1:data.BalancedPaint2);
            return array!=null&&row.Index<array.Length&&array[row.Index]?array[row.Index]:fallback;
        }
        internal void PrepareVisibility()
        {
            if(!camera)camera=Camera.main;if(!camera){cameraInside=true;accessOpen=false;return;}
            // The camera inside the fuselage must retain a complete cockpit
            // and cargo interior, including intact close-up detail.
            Vector3 p=transform.InverseTransformPoint(camera.transform.position);
            cameraInside=Mathf.Abs(p.x)<5.5f&&p.y>2&&p.y<12&&p.z>-26&&p.z<34;
            // Open loading/bomb doors expose otherwise intact interiors.
            accessOpen=graph.InteriorAccessOpen;
        }
        internal bool InteriorVisible(NativePartitionSurface surface,bool exposed)=>exposed||surface.InteriorFurniture&&(cameraInside||accessOpen);
        private void PreviewVisibility(){if(graph.Ready)return;PrepareVisibility();foreach(var row in rows)if(data.Interior[row.Index])row.Renderer.enabled=row.Meta&&row.Meta.InteriorFurniture&&(cameraInside||accessOpen);}
        private void OnDestroy(){instances.Remove(this);}
    }
}
