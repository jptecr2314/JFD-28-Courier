using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Japensou.NuclearOption.AirframeData;
using Object=UnityEngine.Object;

namespace Japensou.NuclearOption.CurrentDamage
{
    internal static class CurrentAirframeCompatibility
    {
        internal const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static Type Family(string name)=>AccessTools.TypeByName("Japensou.NuclearOption.Superheavy."+name);
        internal static void Install(Harmony h)
        {
            AccessTools.Field(Family("SuperheavyArmamentCatalog"),"AuthoredNames").SetValue(null,(Func<MeshFilter,string[]>)Names);
            AccessTools.Field(Family("SuperheavyArmamentCatalog"),"AuthoredMesh").SetValue(null,(Func<MeshFilter,Mesh>)OriginalMesh);
            h.CreateClassProcessor(typeof(StopOldPaint)).Patch();h.CreateClassProcessor(typeof(Paint)).Patch();
            h.CreateClassProcessor(typeof(Cargo)).Patch();h.CreateClassProcessor(typeof(CargoLookup)).Patch();
            h.CreateClassProcessor(typeof(KeepEditorCargo)).Patch();
            h.CreateClassProcessor(typeof(PreviewLoadoutContacts)).Patch();
            h.CreateClassProcessor(typeof(PreviewWeaponContacts)).Patch();h.CreateClassProcessor(typeof(PreviewGearContacts)).Patch();
            h.CreateClassProcessor(typeof(CourierGearDebris)).Patch();
        }
        [HarmonyPatch(typeof(LandingGear),"BreakWheel")]
        private static class CourierGearDebris
        {
            private static void Prefix(LandingGear __instance,AeroPart ___attachedPart,Collider ___gearCollider)
            {if(___attachedPart&&___attachedPart.parentUnit)___attachedPart.parentUnit.GetComponent<CourierAssemblyOwnership>()?.AlignGearContact(__instance,___gearCollider);}
            private static void Postfix(LandingGear __instance,AeroPart ___attachedPart,Collider ___gearCollider)
            {
                var graph=___attachedPart&&___attachedPart.parentUnit?___attachedPart.parentUnit.GetComponent<NativePartitionController>():null;
                var ownership=graph?graph.GetComponent<CourierAssemblyOwnership>():null;
                if(graph&&graph.CourierRefit&&ownership&&___gearCollider)
                    graph.RegisterGearDebris(___gearCollider,ownership.GearSupport(__instance)??___attachedPart);
            }
        }
        [HarmonyPatch]
        private static class PreviewLoadoutContacts
        {
            private static IEnumerable<MethodBase> TargetMethods(){yield return AccessTools.Method(typeof(LoadoutSelector),"AssignAircraft");yield return AccessTools.Method(typeof(LoadoutSelector),"UpdateWeapons");}
            private static void Postfix(Aircraft ___aircraft)
            {
                PrunePreviewContacts(___aircraft);
            }
        }
        private static void PrunePreviewContacts(Aircraft a){if(!a||(a.networked&&GameManager.gameState!=GameState.Editor)||!a.GetComponent<BakedAirframe>())return;foreach(var collider in a.GetComponentsInChildren<Collider>(true))collider.enabled=collider.name=="CURRENT_EDITOR_SELECTION_CONTACT";}
        [HarmonyPatch(typeof(WeaponManager),nameof(WeaponManager.SpawnWeapons))]
        private static class PreviewWeaponContacts{private static void Postfix(Aircraft ___aircraft)=>PrunePreviewContacts(___aircraft);}
        [HarmonyPatch(typeof(Aircraft),nameof(Aircraft.SetGear),new[]{typeof(LandingGear.GearState)})]
        private static class PreviewGearContacts{private static void Postfix(Aircraft __instance)=>PrunePreviewContacts(__instance);}
        private static string[] Names(MeshFilter f)
        {
            var s=f.GetComponent<NativePartitionSurface>();if(s)return SurfaceNames(s.SourceOriginal,s.PartId,s.Fresh);
            var data=f.GetComponentInParent<BakedAirframe>(true);int i=data&&data.SurfaceFilters!=null?Array.IndexOf(data.SurfaceFilters,f):-1;
            return i<0?new[]{f.name}:SurfaceNames(data.SurfaceSources[i],data.SurfaceParts[i],data.SurfaceFresh[i]);
        }
        private static string[] SurfaceNames(string original,string part,bool fresh)=>((fresh&&!original.StartsWith("SHGT_FuelGeometry__",StringComparison.Ordinal)?"SHGT_Structure_"+part+"\n":"")+original).Split('\n');
        private static Mesh OriginalMesh(MeshFilter f)
        {
            var s=f.GetComponent<NativePartitionSurface>();if(s)return s.PlanningOriginal?s.PlanningOriginal:s.Original;
            var data=f.GetComponentInParent<BakedAirframe>(true);var presentation=data?data.GetComponent<CourierPresentationData>():null;int row=presentation&&presentation.Filters!=null?Array.IndexOf(presentation.Filters,f):-1;
            if(row>=0&&presentation.PlanningSources!=null&&presentation.PlanningSources.Length==presentation.Filters.Length&&presentation.PlanningSources[row])return presentation.PlanningSources[row];
            int i=data&&data.SurfaceFilters!=null?Array.IndexOf(data.SurfaceFilters,f):-1;return i<0?null:data.SurfaceOriginals[i];
        }
        [HarmonyPatch]
        private static class StopOldPaint
        {
            private static MethodBase TargetMethod()=>Family("SuperheavyGroundLiveries").GetNestedType("NativeLiveryPatch",All).GetMethod("Postfix",All);
            private static bool Prefix(Aircraft __0)=>!__0||!__0.GetComponent<NativePartitionController>();
        }
        [HarmonyPatch(typeof(LiveryBehaviour),"SetLivery")]
        private static class Paint
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Aircraft ___aircraft,LiveryData livery)
            {
                if(!___aircraft||!___aircraft.GetComponent<NativePartitionController>()||!livery||!livery.Texture)return;
                var own=___aircraft.GetComponent<OwnedAirframePaint>()??___aircraft.gameObject.AddComponent<OwnedAirframePaint>();own.Apply(livery);
            }
        }
        private sealed class CargoState {internal readonly Dictionary<int,object> Loads=new Dictionary<int,object>();internal string Signature;}
        [HarmonyPatch]
        private static class KeepEditorCargo
        {
            private static MethodBase TargetMethod()=>Family("SuperheavySpatialCargoRuntime").GetNestedType("RestorePreviewPatch",All).GetMethod("Postfix",All);
            private static bool Prefix(Aircraft __1)=>GameManager.gameState!=GameState.Editor||!__1||__1.definition?.jsonKey!="jp_superheavy_ground_test";
        }
        private static readonly ConditionalWeakTable<Aircraft,CargoState> Recovered=new ConditionalWeakTable<Aircraft,CargoState>();
        private static object WeakGet(object table,object key)
        {var args=new[]{key,null};return (bool)table.GetType().GetMethod("TryGetValue").Invoke(table,args)?args[1]:null;}
        private static object Call(Type t,string method,params object[] args)=>AccessTools.Method(t,method).Invoke(null,args);
        [HarmonyPatch]
        private static class Cargo
        {
            private static MethodBase TargetMethod()=>Family("SuperheavySpatialCargoRuntime").GetNestedType("ApplyAnchorsPatch",All).GetMethod("Prefix",All);
            private static bool Prefix(WeaponManager __0,Aircraft __1)
            {
                if(!__1||__1.definition?.jsonKey!="jp_superheavy_ground_test"||(GameManager.gameState!=GameState.SinglePlayer&&GameManager.gameState!=GameState.Editor))return true;
                var runtime=Family("SuperheavySpatialCargoRuntime");var args=new object[]{__0,null};if(!(bool)Call(runtime,"TryGetAnchors",args))return true;var anchors=args[1];
                var loadout=__1.loadout;if(loadout?.weapons==null)return false;
                var layout=WeakGet(AccessTools.Field(runtime,"AircraftLayouts").GetValue(null),__1)??WeakGet(AccessTools.Field(runtime,"LoadoutLayouts").GetValue(null),loadout);
                if(layout==null||!(bool)Call(runtime,"Matches",loadout,layout))
                {var restore=new object[]{__1,null};layout=(bool)Call(runtime,"TryRestore",restore)&& (bool)Call(runtime,"Matches",loadout,restore[1])?restore[1]:null;}
                if(layout!=null)
                {
                    Call(runtime,"Bind",__1,layout);anchors.GetType().GetMethod("Apply",All).Invoke(anchors,new[]{layout});Recovered.Remove(__1);return false;
                }
                Call(runtime,"Bind",__1,null);anchors.GetType().GetMethod("Apply",All).Invoke(anchors,new object[]{null});
                var signature=string.Join("|",loadout.weapons.Select(w=>w?w.jsonKey:""));
                if(Recovered.TryGetValue(__1,out var previous)&&previous.Signature==signature){ApplyRecovered(anchors,previous);return false;}
                Recovered.Remove(__1);var state=new CargoState{Signature=signature};
                var indices=Enumerable.Range(0,Math.Min(16,loadout.weapons.Count)).Where(i=>(i<3||i>=7)&&loadout.weapons[i]&&loadout.weapons[i].Cargo).ToArray();
                if(indices.Length==0)return false;
                var plan=Family("SuperheavySpatialCargoPlan");var aryx=(string[])AccessTools.Field(runtime,"AryxCargoKeys").GetValue(null);
                // Ordinary native bay stores keep their established discrete
                // anchors, including mounts which are not spatial singletons.
                if(indices.All(i=>i<3&&!(bool)Call(runtime,"IsFrontOnly",loadout.weapons[i])&&!aryx.Contains((string)Call(plan,"SourceCargoKey",loadout.weapons[i].jsonKey))&&!(bool)Call(Family("SuperheavyQolStores"),"IsOptionalCargo",(string)Call(plan,"SourceCargoKey",loadout.weapons[i].jsonKey))))return true;
                var itemType=Family("CargoItem");var items=Array.CreateInstance(itemType,indices.Length);
                for(int n=0;n<indices.Length;n++){var measure=new object[]{loadout.weapons[indices[n]],null};if(!(bool)Call(plan,"TryMeasure",measure))throw new InvalidOperationException("Selected Courier cargo cannot be measured: "+loadout.weapons[indices[n]].jsonKey);items.SetValue(measure[1],n);}
                var pack=new object[]{items,null};if(!(bool)Call(Family("SuperheavyCargoPacking"),"TryPack",pack))throw new InvalidOperationException("Selected Courier cargo does not fit the hold; selections were preserved.");
                var loadType=Family("SpatialCargoLoad");var loads=Array.CreateInstance(loadType,indices.Length);var ctor=loadType.GetConstructors(All).Single();int at=0;
                foreach(var placement in(Array)pack[1])
                {
                    var t=placement.GetType();int n=(int)t.GetField("ItemIndex").GetValue(placement);var mount=loadout.weapons[indices[n]];
                    var load=ctor.Invoke(new[]{(object)mount,items.GetValue(n),t.GetField("X").GetValue(placement),t.GetField("Y").GetValue(placement)});
                    loads.SetValue(load,at++);state.Loads.Add(indices[n],load);
                }
                var validate=new object[]{loads,null};if(!(bool)Call(plan,"TryValidate",validate))throw new InvalidOperationException("Selected Courier cargo cannot be released safely: "+validate[1]+" Selections were preserved.");
                Recovered.Add(__1,state);ApplyRecovered(anchors,state);
                CurrentAirframeDamagePlugin.Log.LogInfo("OWN_CARGO_RECOVERED "+indices.Length+" selected mounts; original slot IDs preserved.");return false;
            }
        }
        private static void ApplyRecovered(object anchors,CargoState state)
        {
            var points=(Transform[])AccessTools.Field(anchors.GetType(),"Anchors").GetValue(anchors);var plan=Family("SuperheavySpatialCargoPlan");
            foreach(var pair in state.Loads)points[pair.Key<3?pair.Key:pair.Key-4].localPosition=(Vector3)Call(plan,"GetAnchor",pair.Value);
        }
        [HarmonyPatch]
        private static class CargoLookup
        {
            private static MethodBase TargetMethod()=>AccessTools.Method(Family("SuperheavySpatialCargoRuntime"),"TryGetLoad");
            private static bool Prefix(object[] __args,ref bool __result)
            {
                if(!(__args[0]is Aircraft aircraft)||!Recovered.TryGetValue(aircraft,out var state))return true;
                var cargo=__args[1];var hardpoint=AccessTools.Field(typeof(Weapon),"hardpoint").GetValue(cargo);int index=(int)AccessTools.Field(hardpoint.GetType(),"HardpointIndex").GetValue(hardpoint);
                __result=state.Loads.TryGetValue(index,out var load);__args[2]=load;return false;
            }
        }
    }
    public sealed class OwnedAirframePaint:MonoBehaviour
    {
        private NativePartitionSurface[] surfaces;
        private BakedAirframe paintData;
        public LiveryData Current;public int Channel;
        public void Apply(LiveryData livery)
        {
            Current=livery;Channel=livery.name=="SHGT_Livery_bdf-matte"||livery.name=="SHGT_Livery_bdf-digital"?1:livery.name=="SHGT_Livery_bdf-forest"?2:0;
            Refresh();var damage=GetComponent<FreshStructureDamage>();if(damage)damage.ApplyPaint(Channel);
            var family=CurrentAirframeCompatibility.Family("SuperheavyGroundLiveries");var state=GetComponent(family)??gameObject.AddComponent(family);
            AccessTools.Property(family,"CurrentLivery").SetValue(state,livery);AccessTools.Property(family,"CurrentChannel").SetValue(state,Channel);
            var defense=CurrentAirframeCompatibility.Family("SuperheavyGroundDefense");var aircraft=GetComponent<Aircraft>();
            foreach(var turret in GetComponentsInChildren(defense,true).Concat(aircraft.partLookup.OfType<AeroPart>().SelectMany(p=>p.GetComponentsInChildren(defense,true))).Distinct())AccessTools.Method(turret.GetType(),"ApplyLivery").Invoke(turret,new object[]{livery,Channel});
            CurrentAirframeDamagePlugin.Log.LogInfo("OWN_PAINT_APPLIED "+livery.name+" UV"+Channel+" surfaces="+surfaces.Length);
        }
        public void Refresh()
        {
            if(!Current)return;if(surfaces==null){var aircraft=GetComponent<Aircraft>();surfaces=GetComponentsInChildren<NativePartitionSurface>(true).Concat(aircraft.partLookup.OfType<AeroPart>().SelectMany(p=>p.GetComponentsInChildren<NativePartitionSurface>(true))).Where(s=>s.Painted).Distinct().ToArray();}
            var block=new MaterialPropertyBlock();
            foreach(var surface in surfaces)
            {
                if(!surface)continue;var filter=surface.GetComponent<MeshFilter>();var renderer=surface.GetComponent<MeshRenderer>();
                if(!filter.sharedMesh.name.StartsWith("OWN "))
                {
                    filter.sharedMesh=Channel==0?surface.Original:Channel==1?surface.Paint1:surface.Paint2;
                    if(!filter.sharedMesh)throw new InvalidOperationException("Baked livery chart missing: "+surface.SourceOriginal);
                }
                renderer.GetPropertyBlock(block);block.SetTexture("_BaseMap",Current.Texture);block.SetColor("_BaseColor",Color.white);block.SetFloat("_Smoothness",Current.Glossiness);renderer.SetPropertyBlock(block);
            }
            if(!paintData)paintData=GetComponent<BakedAirframe>();
            var filters=paintData?paintData.DetailPaintFilters:null;
            if(filters==null||filters.Length==0)return;
            if(paintData.DetailPaintOriginals?.Length!=filters.Length||paintData.DetailPaint1?.Length!=filters.Length||paintData.DetailPaint2?.Length!=filters.Length||paintData.DetailPaintSlotMasks?.Length!=filters.Length)throw new InvalidOperationException("Baked detail paint bindings differ.");
            for(int i=0;i<filters.Length;i++)
            {
                var filter=filters[i];if(!filter)continue;
                filter.sharedMesh=Channel==0?paintData.DetailPaintOriginals[i]:Channel==1?paintData.DetailPaint1[i]:paintData.DetailPaint2[i];
                if(!filter.sharedMesh)throw new InvalidOperationException("Baked detail paint chart missing.");
                var renderer=filter.GetComponent<MeshRenderer>();int mask=paintData.DetailPaintSlotMasks[i];
                for(int slot=0;slot<renderer.sharedMaterials.Length;slot++)if((mask&(1<<slot))!=0)
                {block.Clear();renderer.GetPropertyBlock(block,slot);block.SetTexture("_BaseMap",Current.Texture);block.SetColor("_BaseColor",Color.white);block.SetFloat("_Smoothness",Current.Glossiness);renderer.SetPropertyBlock(block,slot);}
            }
        }
    }
}
