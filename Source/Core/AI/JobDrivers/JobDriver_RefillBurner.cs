﻿using System.Collections.Generic;
using System.Linq;

using Verse;
using Verse.AI;

namespace RA
{
    public class JobDriver_RefillBurner : JobDriver
    {
        public const TargetIndex FuelIndex = TargetIndex.A;
        public const TargetIndex BurnerIndex = TargetIndex.B;

        // job decriptioption when pawn performing it is selected
        public override string GetReport()
        {
            string report;

            if (pawn.carrier.CarriedThing != null)
                report = string.Format("Carry {0} to {1}", pawn.carrier.CarriedThing.Label, CurJob.GetTarget(BurnerIndex).Thing.Label);
            else
                report = "Gathering fuel for " + CurJob.GetTarget(BurnerIndex).Thing.Label;

            return report;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrForbidden(BurnerIndex);
            this.FailOnBurningImmobile(BurnerIndex);

            FindFuelToGather();

            yield return Toils_Reserve.Reserve(BurnerIndex);
            yield return Toils_Reserve.ReserveQueue(FuelIndex);

            //Gather fuel loop
            {
                Toil extractTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(FuelIndex);
                yield return extractTarget;
                yield return Toils_Goto.GotoThing(FuelIndex, PathEndMode.ClosestTouch)
                    .FailOnDespawnedOrForbidden(FuelIndex)
                    .FailOnBurningImmobile(FuelIndex);
                yield return Toils_Haul.StartCarryThing(FuelIndex);
                yield return Toils_Jump.JumpIfHaveTargetInQueue(FuelIndex, extractTarget);
            }

            yield return Toils_Haul.CarryHauledThingToContainer();
            yield return DepositFuelIntoBurner(BurnerIndex);
        }

        public void FindFuelToGather()
        {
            Building_WorkTable_Fueled burner = CurJob.GetTarget(BurnerIndex).Thing as Building_WorkTable_Fueled;
            int requiredFuelAmount = CurJob.maxNumToCarry;

            IntVec3 searchCenter = CurJob.GetTarget(FuelIndex).Thing.Position;
            IEnumerable<Thing> searchSet = Find.ListerThings.ThingsOfDef(CurJob.GetTarget(FuelIndex).Thing.def);

            CurJob.targetQueueA = new List<TargetInfo>();
            CurJob.numToBringList = new List<int>();

            int closestFuelStackCount = 0;

            do
            {
                // max search radius - 10 cells around first or last picked up fuel resource
                CurJob.targetQueueA.Add(GenClosest.ClosestThing_Global_Reachable(searchCenter, searchSet, PathEndMode.ClosestTouch, TraverseParms.For(pawn, DangerUtility.NormalMaxDanger(pawn)), 10));
                closestFuelStackCount += CurJob.targetQueueA.Last().Thing.stackCount;

                if (requiredFuelAmount >= closestFuelStackCount)
                    CurJob.numToBringList.Add(closestFuelStackCount);
                else
                    CurJob.numToBringList.Add(requiredFuelAmount);

                // NOTE: check if works properly
                searchSet = searchSet.Where(fuel => !CurJob.targetQueueA.Contains(fuel));
                searchCenter = CurJob.targetQueueA.Last().Thing.Position;

                requiredFuelAmount -= closestFuelStackCount;
            } while (requiredFuelAmount > 0 && searchSet.Count() > 0);
        }

        public Toil DepositFuelIntoBurner(TargetIndex burnerIndex)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                if (actor.carrier.CarriedThing == null)
                {
                    Log.Error(actor + " tried to place hauled thing in container but is not hauling anything.");
                    return;
                }
                IThingContainerOwner thingContainerOwner = curJob.GetTarget(burnerIndex).Thing as IThingContainerOwner;
                if (thingContainerOwner != null)
                {
                    int num = actor.carrier.CarriedThing.stackCount;
                    actor.carrier.container.TransferToContainer(actor.carrier.CarriedThing, thingContainerOwner.GetContainer(), num);
                }
                else if (curJob.GetTarget(burnerIndex).Thing.def.Minifiable)
                {
                    actor.carrier.container.Clear();
                }
                else
                {
                    Log.Error("Could not deposit hauled thing in container: " + curJob.GetTarget(burnerIndex).Thing);
                }
            };
            return toil;
        }
    }
}

