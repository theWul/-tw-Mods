using System;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;

namespace tw_YAAM
     {
     [DefOf]
     public static class JobDefOf
          {
          public static JobDef FillComposter;
          public static JobDef EmptyComposter;
          }
     public class CompComposter : ThingComp
          {
          public CompProperties_Composter Props { get { return (CompProperties_Composter)this.props; } }
          }

     public class CompProperties_Composter : CompProperties
          {
          public int capacity = 75;
          public int processingTicks = 1200; // 6 days
          public int rawCompostPerCompost = 15; 
          public CompProperties_Composter ()
               {
               this.compClass = typeof (CompComposter);
               }
          }
     [StaticConstructorOnStartup]
     public class Building_Composter : Building
          {
          private CompComposter compComposter;
          private CompPowerTrader compPowerTrader;
          public int RawCompostCount;
          public int Capacity { get { return compComposter.Props.capacity; } }
          private int processingTicks { get { return this.compComposter.Props.processingTicks; } }
          private int rawCompostPerCompost { get { return this.compComposter.Props.rawCompostPerCompost; } }
          private static readonly Vector2 BarSize = new Vector2 (0.55f, 0.1f);
          private static readonly Color BarZeroProgressColor = new Color (0.6f, 0.3f, 0.3f);
          private static readonly Color BarFullProgressColor = new Color (1f, 1f, 0.3f);
          private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial (new Color (0.3f, 0.3f, 0.3f));
          public static readonly string twComposter_Incomplete = "{0}: can't take out product when not complete ({1})";
          private static readonly string twComposter_Inspect_Resources = "{0} / {1} raw compost";
          private static readonly string twComposter_Inspect_Processed = "{0} processed";
          private static readonly string twComposter_Inspect_Powered = "200% efficiency (powered)";
          private static readonly string twComposter_Inspect_Temperature = "{0} efficiency (temperature {1}°)";
          private static readonly string twComposter_Inspect_Produced = "{0} compost will be produced";

          public override void SpawnSetup (Map map, bool respawningAfterLoad)
               {
               base.SpawnSetup (map, respawningAfterLoad);
               this.compComposter = base.GetComp<CompComposter> ();
               this.compPowerTrader = base.GetComp<CompPowerTrader> ();
               }
          private float progress;
          private Material barFilledCachedMat;
          public float Progress
               {
               get  {  return this.progress; }
               set
                    {
                    if (value == this.progress) return;
                    this.progress = value;
                    this.barFilledCachedMat = null;
                    }
               }
          public Material BarFilledMat
               {
               get
                    {
                    if (this.barFilledCachedMat == null) this.barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial (Color.Lerp (Building_Composter.BarZeroProgressColor, Building_Composter.BarFullProgressColor, this.Progress));
                    return this.barFilledCachedMat;
                    }
               }
          public bool Empty { get  { return this.RawCompostCount <= 0; }  }
          public bool Full
               {
               get
                    {
                    return this.RawCompostCount >= Capacity;
                    }
               }
          public bool ProcessComplete
               {
               get
                    {
                    if (Empty) return false;
                    return this.Progress >= 1f;
                    }
               }
          public void AddResources (Thing raw_resource)
               {
               int count = raw_resource.stackCount;
               if (RawCompostCount + count > Capacity) count = Capacity - RawCompostCount;
               this.Progress = GenMath.WeightedAverage (0f, (float)count, this.Progress, (float)this.RawCompostCount);
               this.RawCompostCount += count;
               raw_resource.Destroy (DestroyMode.Vanish);
               }
          private float Temperature
               {
               get
                    {
                    if (base.MapHeld == null) return 0f;
                    return base.PositionHeld.GetTemperature (base.MapHeld);
                    }
               }
          private float TemperatureFactor
               {
               get
                    {
                    const float minTemp = 4f;
                    const float maxTemp = 36f;
                    if (Temperature < minTemp) return 0f;
                    if (Temperature > maxTemp) return 2f;
                    return 2f * (Temperature - minTemp) / (maxTemp - minTemp);
                    }
               }

          public override void TickRare ()
               {
               base.TickRare ();
               if (this.Empty) return;
               float advance = (1f / (float)this.processingTicks);
               if (this.compPowerTrader != null && this.compPowerTrader.PowerOn)
                    advance *= 2f;
               else advance *= TemperatureFactor;
               this.Progress = Mathf.Min (this.Progress + advance, 1f);
               }
          public Thing TakeOutProduct ()
               {
               if (!this.ProcessComplete)
                    {
                    Log.Warning (string.Format(twComposter_Incomplete, this.Label, Progress.ToStringPercent("0")));
                    return null;
                    }
               Thing thing = ThingMaker.MakeThing (ThingDefOf.twCompost, null);
               thing.stackCount = RawCompostCount / rawCompostPerCompost;
               RawCompostCount = 0;
               Progress = 0f;
               return thing;
               }
          public override void ExposeData ()
               {
               base.ExposeData ();
               Scribe_Values.Look<int> (ref this.RawCompostCount, "resources", 0, false);
               Scribe_Values.Look<float> (ref this.progress, "progress", 0f, false);
               }

          public override string GetInspectString ()
               {
               StringBuilder stringBuilder = new StringBuilder ();
               stringBuilder.Append (base.GetInspectString ());
               if (stringBuilder.Length != 0) stringBuilder.AppendLine ();
               stringBuilder.AppendLine (string.Format(twComposter_Inspect_Resources, RawCompostCount.ToString(), Capacity.ToString ()));
               if (!Empty)
                    {
                    stringBuilder.AppendLine (string.Format(twComposter_Inspect_Processed, progress.ToStringPercent ("0")));
                    if (this.compPowerTrader != null && this.compPowerTrader.PowerOn)
                         stringBuilder.AppendLine (twComposter_Inspect_Powered);
                    else stringBuilder.AppendLine (string.Format(twComposter_Inspect_Temperature, TemperatureFactor.ToStringPercent ("0"), Temperature.ToString ("#.0")));
                    stringBuilder.AppendLine (string.Format(twComposter_Inspect_Produced, (RawCompostCount / rawCompostPerCompost).ToString()));
                    }
               return stringBuilder.ToString ().TrimEndNewlines ();
               }

          public override void Draw ()
               {
               base.Draw ();
               if (!this.Empty)
                    {
                    Vector3 drawPos = this.DrawPos;
                    drawPos.y += 0.05f;
                    drawPos.z += 0.25f;
                    GenDraw.DrawFillableBar (new GenDraw.FillableBarRequest
                         {
                         center = drawPos,
                         size = Building_Composter.BarSize,
                         fillPercent = (float)this.RawCompostCount / (float) this.Capacity,
                         filledMat = this.BarFilledMat,
                         unfilledMat = Building_Composter.BarUnfilledMat,
                         margin = 0.1f,
                         rotation = Rot4.North
                         });
                    }
               }
          [DebuggerHidden]
          public override IEnumerable<Gizmo> GetGizmos ()
               {
               IEnumerator<Gizmo> enumerator = base.GetGizmos ().GetEnumerator ();
               while (enumerator.MoveNext ())
                    {
                    Gizmo current = enumerator.Current;
                    yield return current;
                    }
               if (Prefs.DevMode && !this.Empty)
                    {
                    yield return new Command_Action
                         {
                         defaultLabel = "Debug: Set progress to 1",
                         action = delegate { this.Progress = 1f; }
                         };
                    }
               yield break;
               }
          }

     public class JobDriver_FillComposter : JobDriver
          {
          protected Building_Composter Composter
               {
               get { return (Building_Composter)this.job.GetTarget (TargetIndex.A).Thing; }
               }
          protected Thing RawCompost
               {
               get { return this.job.GetTarget (TargetIndex.B).Thing; }
               }
          public override bool TryMakePreToilReservations ()
               {
               return this.pawn.Reserve (this.Composter, this.job, 1, -1, null) && this.pawn.Reserve (this.RawCompost, this.job, 1, -1, null);
               }
          [DebuggerHidden]
          protected override IEnumerable<Toil> MakeNewToils ()
               {
               this.FailOnDespawnedNullOrForbidden (TargetIndex.A);
               this.FailOnBurningImmobile (TargetIndex.A);
               base.AddEndCondition (() => (this.Composter.Full) ?  JobCondition.Succeeded : JobCondition.Ongoing);
               yield return Toils_General.DoAtomic (delegate { this.job.count = this.Composter.Capacity - this.Composter.RawCompostCount; });
               Toil reserveResources = Toils_Reserve.Reserve (TargetIndex.B, 1, -1, null);
               yield return reserveResources;
               yield return Toils_Goto.GotoThing (TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden (TargetIndex.B).FailOnSomeonePhysicallyInteracting (TargetIndex.B);
               yield return Toils_Haul.StartCarryThing (TargetIndex.B, false, true, false).FailOnDestroyedNullOrForbidden (TargetIndex.B);
               yield return Toils_Haul.CheckForGetOpportunityDuplicate (reserveResources, TargetIndex.B, TargetIndex.None, true, null);
               yield return Toils_Goto.GotoThing (TargetIndex.A, PathEndMode.Touch);
               yield return Toils_General.Wait (200).FailOnDestroyedNullOrForbidden (TargetIndex.B).FailOnDestroyedNullOrForbidden (TargetIndex.A).FailOnCannotTouch (TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay (TargetIndex.A, false, -0.5f);
               yield return new Toil
                    {
                    initAction = delegate
                         {
                              this.Composter.AddResources(this.RawCompost);
                              },
                    defaultCompleteMode = ToilCompleteMode.Instant
                    };
               }
          }
     public class JobDriver_EmptyComposter : JobDriver
          {
          protected Building_Composter Composter
               {
               get { return (Building_Composter)this.job.GetTarget (TargetIndex.A).Thing; }
               }
          protected Thing Compost
               {
               get { return this.job.GetTarget (TargetIndex.B).Thing; }
               }
          public override bool TryMakePreToilReservations ()
               {
               return this.pawn.Reserve (this.Composter, this.job, 1, -1, null);
               }
          [DebuggerHidden]
          protected override IEnumerable<Toil> MakeNewToils ()
               {
               this.FailOnDespawnedNullOrForbidden (TargetIndex.A);
               this.FailOnBurningImmobile (TargetIndex.A);
               yield return Toils_Goto.GotoThing (TargetIndex.A, PathEndMode.Touch);
               yield return Toils_General.Wait (200).FailOnDestroyedNullOrForbidden (TargetIndex.A).FailOnCannotTouch (TargetIndex.A, PathEndMode.Touch).FailOn (() => !this.Composter.ProcessComplete).WithProgressBarToilDelay (TargetIndex.A, false, -0.5f);
               yield return new Toil
                    {
                    initAction = delegate
                         {
                              Thing thing = this.Composter.TakeOutProduct ();
                              GenPlace.TryPlaceThing (thing, this.pawn.Position, this.Map, ThingPlaceMode.Near, null);
                              StoragePriority currentPriority = HaulAIUtility.StoragePriorityAtFor (thing.Position, thing);
                              IntVec3 c;
                              if (StoreUtility.TryFindBestBetterStoreCellFor (thing, this.pawn, this.Map, currentPriority, this.pawn.Faction, out c, true))
                                   {
                                   this.job.SetTarget (TargetIndex.C, c);
                                   this.job.SetTarget (TargetIndex.B, thing);
                                   this.job.count = thing.stackCount;
                                   }
                              else {
                                   this.EndJobWith (JobCondition.Incompletable);
                                   }
                              },
                    defaultCompleteMode = ToilCompleteMode.Instant
                    };
               yield return Toils_Reserve.Reserve (TargetIndex.B, 1, -1, null);
               yield return Toils_Reserve.Reserve (TargetIndex.C, 1, -1, null);
               yield return Toils_Goto.GotoThing (TargetIndex.B, PathEndMode.ClosestTouch);
               yield return Toils_Haul.StartCarryThing (TargetIndex.B, false, false, false);
               Toil carryToCell = Toils_Haul.CarryHauledThingToCell (TargetIndex.C);
               yield return carryToCell;
               yield return Toils_Haul.PlaceHauledThingInCell (TargetIndex.C, carryToCell, true);
               }
          }



     public class WorkGiver_FillComposter : WorkGiver_Scanner
          {
          public override PathEndMode PathEndMode
               {
               get { return PathEndMode.Touch; }
               }
          private Thing FindRawResources (Pawn pawn, Building_Composter composter)
               {
               Predicate<Thing> predicate = (Thing x) => !x.IsForbidden (pawn) && pawn.CanReserve (x, 1);
               return GenClosest.ClosestThingReachable (pawn.Position, pawn.Map, ThingRequest.ForDef (ThingDefOf.twRawCompost), PathEndMode.ClosestTouch, TraverseParms.For (pawn, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, predicate, null, 0, -1, false);
               }
          public override bool HasJobOnThing (Pawn pawn, Thing t, bool forced = false)
               {
               Building_Composter composter = t as Building_Composter;
               if (t == null) return false;
               if (t.IsForbidden (pawn)) return false;
               if (t.IsBurning ()) return false;
               if (pawn.Map.designationManager.DesignationOn (t, DesignationDefOf.Deconstruct) != null) return false;
               if (composter.Full)
                    {
                    JobFailReason.Is (t.Label + " is full.");
                    return false;
                    }
               if (!pawn.CanReserveAndReach (t, PathEndMode.Touch, pawn.NormalMaxDanger (), 1)) return false;
               if (this.FindRawResources (pawn, composter) == null)
                    {
                    JobFailReason.Is ("No accessable raw resources for " + t.Label);
                    return false;
                    }
               return true;
               }
          public override Job JobOnThing (Pawn pawn, Thing t, bool forced = false)
               {
               Building_Composter composter = t as Building_Composter;
               Thing t2 = this.FindRawResources (pawn, composter);
               return new Job (JobDefOf.FillComposter, t, t2)
                    {
                    count = composter.Capacity - composter.RawCompostCount
                    };
               }
          }

     public class WorkGiver_EmptyComposter : WorkGiver_Scanner
          {
          public override PathEndMode PathEndMode
               {
               get  { return PathEndMode.Touch; }
               }
          public override bool HasJobOnThing (Pawn pawn, Thing t, bool forced = false)
               {
               Building_Composter composter = t as Building_Composter;
               if (t == null) return false;
               if (t.IsForbidden (pawn)) return false;
               if (t.IsBurning ()) return false;
               if (pawn.Map.designationManager.DesignationOn (t, DesignationDefOf.Deconstruct) != null) return false;
               if (!composter.ProcessComplete)
                    {
                    JobFailReason.Is (string.Format(Building_Composter.twComposter_Incomplete, t.Label, composter.Progress.ToStringPercent ("0")));
                    return false;
                    }
               return pawn.CanReserveAndReach (t, PathEndMode.Touch, pawn.NormalMaxDanger (), 1);
               }
          public override Job JobOnThing (Pawn pawn, Thing t, bool forced = false)
               {
               return new Job (JobDefOf.EmptyComposter, t);
               }
          }

     public class WorkGiver_FillCompostBin : WorkGiver_FillComposter
          {
          public override ThingRequest PotentialWorkThingRequest
               {
               get  { return ThingRequest.ForDef (ThingDefOf.twCompostBin); }
               }
          }

     public class WorkGiver_EmptyCompostBin : WorkGiver_EmptyComposter
          {
          public override ThingRequest PotentialWorkThingRequest
               {
               get  { return ThingRequest.ForDef (ThingDefOf.twCompostBin); }
               }
          }
     public class WorkGiver_FillIndustrialComposter : WorkGiver_FillComposter
          {
          public override ThingRequest PotentialWorkThingRequest
               {
               get  { return ThingRequest.ForDef (ThingDefOf.twIndustrialComposter); }
               }
          }

     public class WorkGiver_EmptyIndustrialComposter : WorkGiver_EmptyComposter
          {
          public override ThingRequest PotentialWorkThingRequest
               {
               get  { return ThingRequest.ForDef (ThingDefOf.twIndustrialComposter); }
               }
          }
     }
