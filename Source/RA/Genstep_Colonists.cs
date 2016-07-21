﻿using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RA
{
    public class Genstep_Colonists : Genstep
    {
        public const int NumStartingMealsPerColonist = 10;
        public const int NumStartingMedPacksPerColonist = 6;

        public const int CrushingDropPodsCount = 2;

        public override void Generate()
        {
            // TODO: make new game start event sequence
            //foreach (var colonist in Find.GameInitData.startingPawns)
            //{
            //    colonist.SetFactionDirect(Faction.OfPlayer);
            //    PawnComponentsUtility.AddAndRemoveDynamicComponents(colonist);
            //    colonist.needs.mood.thoughts.TryGainThought(ThoughtDefOf.NewColonyOptimism);
            //    foreach (var pawn in Find.GameInitData.startingPawns)
            //    {
            //        if (pawn != colonist)
            //        {
            //            var thought_SocialMemory = (Thought_SocialMemory)ThoughtMaker.MakeThought(ThoughtDefOf.CrashedTogether);
            //            thought_SocialMemory.SetOtherPawn(pawn);
            //            colonist.needs.mood.thoughts.TryGainThought(thought_SocialMemory);
            //        }
            //    }
            //    // damage colonists due to falling in ship wreck
            //    ApplyMinorInjuries(colonist);
            //}
            //CreateInitialWorkSettings();
            //var startedDirectInEditor = Find.GameInitData.StartedDirectInEditor;

            // list of lists to generate after ship crash
            var listsToGenerate = new List<List<Thing>>();

            // colonists list + pet added to that list
            var colonists = Find.GameInitData.startingPawns.Cast<Thing>().ToList();
            colonists.Add(RandomPet());
            listsToGenerate.Add(colonists);

            //// Create the ship impactResultThing part
            //SkyfallerUtil.MakeShipWreckCrashingAt(MapGenerator.PlayerStartSpot, listsToGenerate, 110,
            //    startedDirectInEditor);

            IntVec3 dropCell;
            // Create damaged drop pods with dead pawns
            for (var i = 0; i < CrushingDropPodsCount; i++)
            {
                // Generate slaver corpse
                var pawn = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("SpaceSlaverDead"),
                    FactionUtility.DefaultFactionFrom(
                        DefDatabase<PawnKindDef>.GetNamed("SpaceSlaverDead").defaultFactionType));

                // Find a location to drop
                 dropCell = CellFinder.RandomClosewalkCellNear(MapGenerator.PlayerStartSpot, 10);
                // Drop a drop pod containg our pawn
                SkyfallerUtil.MakeDropPodCrashingAt(dropCell, new DropPodInfo {SingleContainedThing = pawn});
            }

            // Create metal debris
            for (var i = 0; i < Rand.RangeInclusive(30, 40); i++)
            {
                // Find a location to drop
                dropCell = CellFinder.RandomClosewalkCellNear(MapGenerator.PlayerStartSpot, 40);
                SkyfallerUtil.MakeDebrisCrashingAt(dropCell);
            }

            // Create meteorites
            for (var i = 0; i < 10; i++)
            {
                // Find a location to drop
                dropCell = CellFinder.RandomClosewalkCellNear(MapGenerator.PlayerStartSpot, 20);
                SkyfallerUtil.MakeMeteoriteCrashingAt(dropCell);
            }
        }

        public static Thing RandomPet()
        {
            var kindDef = (from td in DefDatabase<PawnKindDef>.AllDefs
                where td.race.category == ThingCategory.Pawn && td.RaceProps.petness > 0f
                select td).RandomElementByWeight(td => td.RaceProps.petness);
            var pawn = PawnGenerator.GeneratePawn(kindDef, Faction.OfPlayer);
            if (pawn.Name == null || pawn.Name.Numerical)
            {
                pawn.Name = NameGenerator.GeneratePawnName(pawn);
            }
            var pawn2 = Find.GameInitData.startingPawns.RandomElement();
            pawn2.relations.AddDirectRelation(PawnRelationDefOf.Bond, pawn);
            return pawn;
        }

        public static void CreateInitialWorkSettings()
        {
            // disable all worktypes
            foreach (var pawn in Find.GameInitData.startingPawns)
            {
                pawn.workSettings.DisableAll();
            }

            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefs)
            {
                // if worktype is enabled in any case (like firefighting)
                if (workType.alwaysStartActive)
                {
                    // set priority 3 to all allowed work types
                    foreach (
                        var pawn in
                            Find.GameInitData.startingPawns.Where(colonist => !colonist.story.WorkTypeIsDisabled(workType)))
                    {
                        pawn.workSettings.SetPriority(workType, 3);
                    }
                }
                else
                {
                    var currentWorkTypeCapableColonistGenerated = false;
                    foreach (var colonist in Find.GameInitData.startingPawns)
                    {
                        if (!colonist.story.WorkTypeIsDisabled(workType) &&
                            colonist.skills.AverageOfRelevantSkillsFor(workType) >= 6f)
                        {
                            colonist.workSettings.SetPriority(workType, 3);
                            currentWorkTypeCapableColonistGenerated = true;
                        }
                    }
                    if (!currentWorkTypeCapableColonistGenerated)
                    {
                        var pawns = Find.GameInitData.startingPawns.Where(colonist => !colonist.story.WorkTypeIsDisabled(workType));

                        if (pawns.Any())
                        {
                            var pawn =
                                pawns.InRandomOrder().MaxBy(pawn1 => pawn1.skills.AverageOfRelevantSkillsFor(workType));
                            pawn.workSettings.SetPriority(workType, 3);
                        }
                        else if (workType.requireCapableColonist)
                        {
                            Log.Error("No colonist could do requireCapableColonist work type " + workType);
                        }
                    }
                }
            }
        }

        public static void ApplyMinorInjuries(Pawn pawn)
        {
            var hediffSet = pawn.health.hediffSet;
            for (var i = 0; i < 5; i++)
            {
                var bodyPartRecord = HittablePartsViolence(hediffSet).RandomElementByWeight(x => x.absoluteFleshCoverage);
                var amount = Rand.RangeInclusive(1, 5);
                var dinfo = new DamageInfo(DamageDefOf.Blunt, amount, null,
                    new BodyPartDamageInfo(bodyPartRecord, false, (HediffDef)null));
                pawn.TakeDamage(dinfo);
            }
        }

        public static IEnumerable<BodyPartRecord> HittablePartsViolence(HediffSet bodyModel)
        {
            return from x in bodyModel.GetNotMissingParts(null, null)
                where
                    x.depth == BodyPartDepth.Outside ||
                    (x.depth == BodyPartDepth.Inside && x.def.IsSolid(x, bodyModel.hediffs))
                select x;
        }
    }
}