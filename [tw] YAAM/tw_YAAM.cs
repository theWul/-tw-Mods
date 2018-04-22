using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using Harmony;
using HugsLib;
using HugsLib.Settings;


namespace tw_YAAM
     {
     /*
     [StaticConstructorOnStartup]
     internal static class tw_YAAM_Initializer
          {
          static tw_YAAM_Initializer ()
               {
               HarmonyInstance harmony = HarmonyInstance.Create ("thewul.rimworld.twYAAM");
               harmony.PatchAll (Assembly.GetExecutingAssembly ());
               }
          }
     public class tw_YAAM
          {
          public class Mod : Verse.Mod
               {
               public Mod (ModContentPack content) : base (content)
                    {
                    // Verse.Log.Message ("tw_YAAM 1.0");
                    }
               }
          }
     */

     public class tw_YAAM : ModBase
          {
          public static SettingHandle<bool> ConvertSoilAfterHarvest;
          public static SettingHandle<bool> PlaceBlueprint;
          public override string ModIdentifier
               {
               get { return "tw_YAAM"; }
               }
          public override void DefsLoaded ()
               {
               base.DefsLoaded ();
               ConvertSoilAfterHarvest = Settings.GetHandle<bool> ("ConvertSoilAfterHarvest", "twYAAM_ConvertSoilAfterHarvest_Title".Translate(), "twYAAM_ConvertSoilAfterHarvest_Description".Translate(), true);
               PlaceBlueprint = Settings.GetHandle<bool> ("PlaceBlueprint", "twYAAM_PlaceBlueprint_Title".Translate(), "twYAAM_PlaceBlueprint_Description".Translate(), true);
               Verse.Log.Message ("tw_YAAM 1.0, ConvertSoilAfterHarvest=" + ConvertSoilAfterHarvest.ToString() + ", PlaceBlueprint=" + PlaceBlueprint.ToString());
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

     [HarmonyPatch (typeof (JobDriver_PlantHarvest), "PlantWorkDoneToil")]
     class JobDriver_PlantHarvest_PlantWorkDoneToil {
          static void Postfix (JobDriver_PlantHarvest __instance, Toil __result)
               {
               __result.initAction = delegate
			     {
                    Thing _thing = __result.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing;
                    Map _map = __result.actor.Map;
				_map.designationManager.RemoveAllDesignationsOn(_thing, false);
                    if (!tw_YAAM.ConvertSoilAfterHarvest) return;
                    IntVec3 _pos = _thing.Position;
                    TerrainDef _terrainDef =  _map.terrainGrid.TerrainAt(_pos);
                    if (!_terrainDef.defName.StartsWith("twSoil")) return;
                    _map.terrainGrid.SetTerrain(__result.actor.jobs.curJob.targetA.Cell, TerrainDefOf.twExtraSoil);
                    if ( _map.terrainGrid.TerrainAt(_pos).defName != TerrainDefOf.twExtraSoil.defName) Verse.Log.Warning("tw_YAMM.ConvertSoilAfterHarvest failed at " + _pos.ToString());
				if (!tw_YAAM.PlaceBlueprint) return;
                    Blueprint_Build blueprint_Build = GenConstruct.PlaceBlueprintForBuild(_terrainDef, _pos, _map, Rot4.North, Faction.OfPlayer, null);
                    if (blueprint_Build == null) Verse.Log.Warning("tw_YAMM.PlaceBlueprint failed for " + _terrainDef.defName);
			     };
               }
          }
     }
