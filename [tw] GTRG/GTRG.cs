using System.Reflection;
using Verse;
using Verse.AI;
using RimWorld;
namespace tw_GTRG 
     {
     public class GTRG 
          {
          public class Mod : Verse.Mod 
               {
               public Mod (ModContentPack content) : base (content)
                    {
                    Verse.Log.Message("GTRG loaded");
                    }
               }
          }
     }
