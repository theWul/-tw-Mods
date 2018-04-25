using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Harmony;
using HugsLib;
using HugsLib.Utils;
using HugsLib.Settings;
using UnityEngine;
using System.Xml;

namespace tw_YAAM
     {
     public class WorldObject : UtilityWorldObject, IExposable
          {
          public List<string> GrowingZonesPlaceBlueprint = new List<string> ();

          public override void ExposeData ()
               {
               base.ExposeData ();
               Scribe_Collections.Look (ref GrowingZonesPlaceBlueprint, "GrowingZonesPlaceBlueprint", LookMode.Value, LookMode.Deep);
               }

          }
     public class tw_YAAM : ModBase
          {
          public static SettingHandle<bool> ConvertSoilAfterHarvest;
          public static WorldObject WorldObject = null;
          public override string ModIdentifier
               {
               get { return "tw_YAAM"; }
               }
          public override void DefsLoaded ()
               {
               base.DefsLoaded ();
               ConvertSoilAfterHarvest = Settings.GetHandle<bool> ("ConvertSoilAfterHarvest", "twYAAM_ConvertSoilAfterHarvest_Title".Translate(), "twYAAM_ConvertSoilAfterHarvest_Description".Translate(), true);
               Verse.Log.Message ("tw_YAAM 1.0, ConvertSoilAfterHarvest=" + ConvertSoilAfterHarvest.ToString());
               }
          public override void WorldLoaded ()
               {
               WorldObject = UtilityWorldObjectManager.GetUtilityWorldObject<WorldObject> ();
               base.WorldLoaded ();
               }
          }



     [DefOf]
     public static class ThingDefOf
          {
          public static ThingDef twRawCompost;
          public static ThingDef twCompost;
          public static ThingDef twCompostBin;
          public static ThingDef twIndustrialComposter;
          }
	[DefOf]
	public static class TerrainDefOf 
          {
		public static TerrainDef twExtraSoil;
		public static TerrainDef twSoilTilled;
		public static TerrainDef twSoilEnriched;
		public static TerrainDef twSoilFertilized;
          }

     [HarmonyPatch (typeof (CompRottable), "CompTickRare")]
     public static class CompRottable_CompTickRare
          {
          struct State
               {
               public Map map;
               public int stackCount;
               public State (CompRottable instance)
                    {
                    map = instance.parent.Map;
                    stackCount = instance.parent.stackCount;
                    }
               }
          static void Prefix (CompRottable __instance, ref State __state)
               {
               __state = new State (__instance);
               }
          static void Postfix (CompRottable __instance, ref State __state)
               {
               if (__instance.parent.Destroyed)
                    {
                    if (__instance.parent.def.thingCategories == null) { return; }
                    if (__instance.parent.def.defName.Contains ("__Corpse")) { return; }
                    if (__state.map == null) { return; }
                    var twRawCompost = ThingMaker.MakeThing (ThingDefOf.twRawCompost, null);
                    twRawCompost.stackCount = __state.stackCount;
                    GenPlace.TryPlaceThing (twRawCompost, __instance.parent.Position, __state.map, ThingPlaceMode.Near, null);
                    }
               }
          }

     #region HarmonyPatch GrowingZone
     public class Terrain_Action : Command_Action
          {
          public Terrain_Action (Zone_Growing growingZone, TerrainDef terrainDef)
               {
               defaultLabel = terrainDef.label;
               defaultDesc = string.Format("twYAAM_Gizmo_Description".Translate(), defaultLabel);
               icon = ContentFinder<Texture2D>.Get(terrainDef.texturePath + "UI");
               action = delegate
                    {
                    Map map = growingZone.Map; 
                    foreach (IntVec3 cell in growingZone.Cells)
                              {
                              if (map.terrainGrid.TerrainAt(cell).fertility >= terrainDef.fertility) continue;
                              Blueprint_Build blueprint_Build = GenConstruct.PlaceBlueprintForBuild(terrainDef, cell, map, Rot4.North, Faction.OfPlayer, null);
                              if (blueprint_Build == null) Verse.Log.Warning("tw_YAMM.Terrain_Action failed for " + terrainDef.defName + " in " + growingZone.label);
                              }
                    };
               }
          }
     [HarmonyPatch (typeof (Zone_Growing), "GetGizmos")]
     public class Zone_Growing_GetGizmos
          {
          [HarmonyPostfix]
          public static void Postfix (Zone_Growing __instance, ref IEnumerable<Gizmo> __result)
               {
               __result = AddGizmos (__instance, __result);
               }

          private static IEnumerable<Gizmo> AddGizmos (Zone_Growing __instance, IEnumerable<Gizmo> __result)
               {
               foreach (var gizmo in __result) yield return gizmo;
               if (tw_YAAM.ConvertSoilAfterHarvest)
                    yield return new Command_Toggle
                         {
                         defaultLabel = "twYAAM_PlaceBlueprint_Title".Translate (),
                         defaultDesc = "twYAAM_PlaceBlueprint_Description".Translate (),
                         icon = ContentFinder<Texture2D>.Get("PlaceBlueprintUI"),
                         isActive = () => tw_YAAM.WorldObject.GrowingZonesPlaceBlueprint.Contains(__instance.label),
                         toggleAction = delegate  {
                                                  if (tw_YAAM.WorldObject.GrowingZonesPlaceBlueprint.Contains(__instance.label))
                                                       tw_YAAM.WorldObject.GrowingZonesPlaceBlueprint.Remove(__instance.label);
                                                  else tw_YAAM.WorldObject.GrowingZonesPlaceBlueprint.Add(__instance.label);
                                                  }
                         };
               yield return new Terrain_Action (__instance, TerrainDefOf.twSoilTilled);
               yield return new Terrain_Action (__instance, TerrainDefOf.twSoilEnriched);
               yield return new Terrain_Action (__instance, TerrainDefOf.twSoilFertilized);
               }
          }
     [HarmonyPatch (typeof (Zone_Growing), "Delete")]
     public class Zone_Growing_Delete
          {
          [HarmonyPrefix]
          static void Prefix (Zone_Growing __instance)
               {
               if   (tw_YAAM.WorldObject.GrowingZonesPlaceBlueprint.Contains(__instance.label))
                    tw_YAAM.WorldObject.GrowingZonesPlaceBlueprint.Remove(__instance.label);
               }
          }
     #endregion

     
     [HarmonyPatch (typeof (JobDriver_PlantHarvest), "PlantWorkDoneToil")]
     class JobDriver_PlantHarvest_PlantWorkDoneToil {
          static void Postfix (JobDriver_PlantHarvest __instance, Toil __result)
               {
               __result.initAction = delegate
			     {
                    Thing thing = __result.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing;
                    Map map = __result.actor.Map;
				map.designationManager.RemoveAllDesignationsOn(thing, false);
                    if (!tw_YAAM.ConvertSoilAfterHarvest) return;
                    IntVec3 pos = thing.Position;
                    TerrainDef terrainDef =  map.terrainGrid.TerrainAt(pos);
                    if (!terrainDef.defName.StartsWith("twSoil")) return;
                    map.terrainGrid.SetTerrain(__result.actor.jobs.curJob.targetA.Cell, RimWorld.TerrainDefOf.Soil);
                    if ( map.terrainGrid.TerrainAt(pos).defName != RimWorld.TerrainDefOf.Soil.defName) Verse.Log.Warning("tw_YAMM.ConvertSoilAfterHarvest failed at " + pos.ToString());
			     List<Zone> zonesList = map.zoneManager.AllZones;
			     for (int j = 0; j < zonesList.Count; j++)
			          {
				     Zone_Growing growingZone = zonesList[j] as Zone_Growing;
				     if (growingZone == null) continue;
					if (growingZone.cells.Count == 0) continue;
                         if (!growingZone.Cells.Contains(pos)) continue;
                         if (tw_YAAM.WorldObject.GrowingZonesPlaceBlueprint.Contains(growingZone.label))
                                   {
                                   Blueprint_Build blueprint_Build = GenConstruct.PlaceBlueprintForBuild(terrainDef, pos, map, Rot4.North, Faction.OfPlayer, null);
                                   if (blueprint_Build == null) Verse.Log.Warning("tw_YAMM.PlaceBlueprint failed for " + terrainDef.defName + " in " + growingZone.label);
                                   return;
                                   }
                         }
			     };
               }
          }
     }
