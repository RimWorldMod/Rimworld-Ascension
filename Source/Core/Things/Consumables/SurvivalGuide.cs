﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;
using Verse.AI;

namespace RA
{
    public class SurvivalGuide : ThingWithComps, IUsable
    {
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight))
            {
                yield return new FloatMenuOption("Cannot use: incapable of reading", null);
            }
            else
            {
                Action action = () =>
                {
                    Job job = new Job(DefDatabase<JobDef>.GetNamed("UseSurvivalGuide"), this);
                    pawn.drafter.TakeOrderedJob(job);
                };
                yield return new FloatMenuOption("Examine " + this.LabelCap, action);
            }
        }

        public void UsedBy(Pawn pawn)
        {
            Find.ResearchManager.currentProj = ResearchProjectDef.Named("SurvivalGuideI");
            Find.ResearchManager.InstantFinish(ResearchProjectDef.Named("SurvivalGuideI"));

            this.Destroy(DestroyMode.Vanish);
        }
    }
}
