﻿using System;
using System.Collections.Generic;
using System.Reflection;
using RimworldAscension.Core.Detours.Jobs;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RA
{
    public class DetoursInitializer : ITab
    {
        public static List<string> sourceMethods = new List<string>();
        public static List<string> destMethods = new List<string>();

        // ITab requirement
        protected override void FillTab() { }

        public DetoursInitializer()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                // load assets from main thread.
                RA_Assets.Init();

                InitDetours();

                if (Prefs.DevMode)
                    Log.Message("Detours initialized");
            });
        }

        public static bool TryGetPrivateField(Type type, object instance, string fieldName, out object value, BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance)
        {
            var field = type.GetField(fieldName, flags);
            value = field?.GetValue(instance);
            return value != null;
        }

        //This is a basic first implementation of the IL method 'hooks' (detours) made possible by RawCode's work;
        //https://ludeon.com/forums/index.php?topic=17143.0
        //Performs detours, spits out basic logs and warns if a method is sourceMethods multiple times.
        public static unsafe bool TryDetourFromTo(MethodInfo source, MethodInfo destination)
        {
            // keep track of detours and spit out some messaging
            var sourceString = source.DeclaringType.FullName + "." + source.Name;
            var destinationString = destination.DeclaringType.FullName + "." + destination.Name;

            sourceMethods.Add(sourceString);
            destMethods.Add(destinationString);

            if (IntPtr.Size == sizeof(Int64))
            {
                // 64-bit systems use 64-bit absolute address and jumps
                // 12 byte destructive

                // Get function pointers
                var Source_Base = source.MethodHandle.GetFunctionPointer().ToInt64();
                var Destination_Base = destination.MethodHandle.GetFunctionPointer().ToInt64();

                // Native source address
                var Pointer_Raw_Source = (byte*)Source_Base;

                // Pointer to insert jump address into native code
                var Pointer_Raw_Address = (long*)(Pointer_Raw_Source + 0x02);

                // Insert 64-bit absolute jump into native code (address in rax)
                // mov rax, immediate64
                // jmp [rax]
                *(Pointer_Raw_Source + 0x00) = 0x48;
                *(Pointer_Raw_Source + 0x01) = 0xB8;
                *Pointer_Raw_Address = Destination_Base; // ( Pointer_Raw_Source + 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 )
                *(Pointer_Raw_Source + 0x0A) = 0xFF;
                *(Pointer_Raw_Source + 0x0B) = 0xE0;
            }
            else
            {
                // 32-bit systems use 32-bit relative offset and jump
                // 5 byte destructive

                // Get function pointers
                var Source_Base = source.MethodHandle.GetFunctionPointer().ToInt32();
                var Destination_Base = destination.MethodHandle.GetFunctionPointer().ToInt32();

                // Native source address
                var Pointer_Raw_Source = (byte*)Source_Base;

                // Pointer to insert jump address into native code
                var Pointer_Raw_Address = (int*)(Pointer_Raw_Source + 1);

                // Jump offset (less instruction size)
                var offset = Destination_Base - Source_Base - 5;

                // Insert 32-bit relative jump into native code
                *Pointer_Raw_Source = 0xE9;
                *Pointer_Raw_Address = offset;
            }

            // done!
            return true;
        }

        public static void InitDetours()
        {
            #region VARIOUS

            var vanillaSelectableNow = typeof(ThingSelectionUtility).GetMethod("SelectableNow", BindingFlags.Static | BindingFlags.Public);
            var newSelectableNow = typeof(RA_ThingSelectionUtility).GetMethod("SelectableNow", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaSelectableNow, newSelectableNow);

            // added rectangular field edges support for trading post
            // added Graphic_StuffBased support
            var vanillaSelectedUpdate = typeof(Designator_Place).GetMethod("SelectedUpdate", BindingFlags.Instance | BindingFlags.Public);
            var newSelectedUpdate = typeof(RA_Designator_Place).GetMethod("SelectedUpdate", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaSelectedUpdate, newSelectedUpdate);

            // changed text align to middle center
            var vanillaButtonSubtle = typeof(WidgetsSubtle).GetMethod("ButtonSubtle", BindingFlags.Static | BindingFlags.Public);
            var newButtonSubtle = typeof(RA_WidgetsSubtle).GetMethod("ButtonSubtle", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaButtonSubtle, newButtonSubtle);

            // delay CheckGameOver first call
            var vanillaCheckGameOver = typeof(GameEnder).GetMethod("CheckGameOver", BindingFlags.Instance | BindingFlags.Public);
            var newCheckGameOver = typeof(RA_GameEnder).GetMethod("CheckGameOver", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaCheckGameOver, newCheckGameOver);

            // delay GameEndTick first call
            var vanillaGameEndTick = typeof(GameEnder).GetMethod("GameEndTick", BindingFlags.Instance | BindingFlags.Public);
            var newGameEndTick = typeof(RA_GameEnder).GetMethod("GameEndTick", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaGameEndTick, newGameEndTick);

            // changed initial game start message
            var vanillaInitNewGeneratedMap = typeof(MapIniter_NewGame).GetMethod("InitNewGeneratedMap", BindingFlags.Static | BindingFlags.Public);
            var newInitNewGeneratedMap = typeof(RA_MapIniter_NewGame).GetMethod("InitNewGeneratedMap", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaInitNewGeneratedMap, newInitNewGeneratedMap);

            // changed initial colonists count
            var vanillaGenerateDefaultColonistsWithFaction = typeof(MapInitData).GetMethod("GenerateDefaultColonistsWithFaction", BindingFlags.Static | BindingFlags.Public);
            var newGenerateDefaultColonistsWithFaction = typeof(RA_MapInitData).GetMethod("GenerateDefaultColonistsWithFaction", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaGenerateDefaultColonistsWithFaction, newGenerateDefaultColonistsWithFaction);

            // make recipe decide what result stuff to make based on defaultIngredientFilter as blocking Stuff types one
            var vanillaGetDominantIngredient = typeof(Toils_Recipe).GetMethod("GetDominantIngredient", BindingFlags.Static | BindingFlags.NonPublic);
            var newGetDominantIngredient = typeof(RA_Toils_Recipe).GetMethod("GetDominantIngredient", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaGetDominantIngredient, newGetDominantIngredient);

            //// CR aim pie
            //var vanillaStanceDraw = typeof(Stance_Warmup).GetMethod("StanceDraw", BindingFlags.Instance | BindingFlags.Public);
            //var newStanceDraw = typeof(RA_Stance_Warmup).GetMethod("StanceDraw", BindingFlags.Instance | BindingFlags.Public);
            //TryDetourFromTo(vanillaStanceDraw, newStanceDraw);

            // added support for fuel burners
            var vanillaDoRecipeWork = typeof(Toils_Recipe).GetMethod("DoRecipeWork", BindingFlags.Static | BindingFlags.Public);
            var newDoRecipeWork = typeof(RA_Toils_Recipe).GetMethod("DoRecipeWork", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaDoRecipeWork, newDoRecipeWork);

            // change the way to determine the research bench availability
            var vanillaColonistsHaveResearchBench = typeof(ListerBuildings).GetMethod("ColonistsHaveResearchBench", BindingFlags.Instance | BindingFlags.Public);
            var newColonistsHaveResearchBench = typeof(RA_ListerBuildings).GetMethod("ColonistsHaveResearchBench", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaColonistsHaveResearchBench, newColonistsHaveResearchBench);

            // send Notify_PawnTookDamage signal to the LordToil
            var vanillaNotify_PawnTookDamage = typeof(Lord).GetMethod("Notify_PawnTookDamage", BindingFlags.Instance | BindingFlags.Public);
            var newNotify_PawnTookDamage = typeof(RA_Lord).GetMethod("Notify_PawnTookDamage", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaNotify_PawnTookDamage, newNotify_PawnTookDamage);

            // remove trader generation from visitor group
            var vanillaTryExecute = typeof(IncidentWorker_VisitorGroup).GetMethod("TryExecute", BindingFlags.Instance | BindingFlags.Public);
            var newTryExecute = typeof(RA_IncidentWorker_VisitorGroup).GetMethod("TryExecute", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaTryExecute, newTryExecute);

            // make effecter trigger deteriorate used tools
            var vanillaTrigger = typeof(Effecter).GetMethod("Trigger", BindingFlags.Instance | BindingFlags.Public);
            var newTrigger = typeof(RA_Effecter).GetMethod("Trigger", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaTrigger, newTrigger);
            
            //// change vanilla threat points generation
            //var vanillaDefaultParmsNow = typeof(IncidentMakerUtility).GetMethod("DefaultParmsNow", BindingFlags.Static | BindingFlags.Public);
            //var newDefaultParmsNow = typeof(RA_IncidentMakerUtility).GetMethod("DefaultParmsNow", BindingFlags.Static | BindingFlags.Public);
            //TryDetourFromTo(vanillaDefaultParmsNow, newDefaultParmsNow);

            // fixes bug for decreased carry capacity
            var vanillaJumpToCollectNextIntoHandsForBill = typeof(JobDriver_DoBill).GetMethod("JumpToCollectNextIntoHandsForBill", BindingFlags.Static | BindingFlags.NonPublic);
            var newJumpToCollectNextIntoHandsForBill = typeof(RA_JobDriver_DoBill).GetMethod("JumpToCollectNextIntoHandsForBill", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaJumpToCollectNextIntoHandsForBill, newJumpToCollectNextIntoHandsForBill);

            // skip message prompt for research recipes
            var vanillaNotify_IterationCompleted = typeof(Bill_Production).GetMethod("Notify_IterationCompleted", BindingFlags.Instance | BindingFlags.Public);
            var newNotify_IterationCompleted = typeof(RA_Bill_Production).GetMethod("Notify_IterationCompleted", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaNotify_IterationCompleted, newNotify_IterationCompleted);

            #endregion

            #region TRADE

            // made trade system accept items for trade in trade zones around trade centers
            var vanillaColonyThingsWillingToBuy = typeof(Pawn).GetProperty("ColonyThingsWillingToBuy", BindingFlags.Instance | BindingFlags.Public);
            var vanillaColonyThingsWillingToBuy_Getter = vanillaColonyThingsWillingToBuy.GetGetMethod();
            var newColonyThingsWillingToBuy = typeof(RA_Pawn).GetProperty("ColonyThingsWillingToBuy", BindingFlags.Instance | BindingFlags.Public);
            var newColonyThingsWillingToBuy_Getter = newColonyThingsWillingToBuy.GetGetMethod();
            TryDetourFromTo(vanillaColonyThingsWillingToBuy_Getter, newColonyThingsWillingToBuy_Getter);

            // made trade system offer items for trade from tradeStock in current trade center
            var vanillaGoods = typeof(Pawn).GetProperty("Goods", BindingFlags.Instance | BindingFlags.Public);
            var vanillaGoods_Getter = vanillaGoods.GetGetMethod();
            var newGoods = typeof(RA_Pawn).GetProperty("Goods", BindingFlags.Instance | BindingFlags.Public);
            var newGoods_Getter = newGoods.GetGetMethod();
            TryDetourFromTo(vanillaGoods_Getter, newGoods_Getter);

            // resolve trade by assigning hauling jobs
            var vanillaResolveTrade = typeof(Tradeable).GetMethod("ResolveTrade", BindingFlags.Instance | BindingFlags.Public);
            var newResolveTrade = typeof(RA_Tradeable).GetMethod("ResolveTrade", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaResolveTrade, newResolveTrade);

            // resolve trade for pawns
            var vanillaResolveTradePawn = typeof(Tradeable_Pawn).GetMethod("ResolveTrade", BindingFlags.Instance | BindingFlags.Public);
            var newResolveTradePawn = typeof(RA_Tradeable_Pawn).GetMethod("ResolveTrade", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaResolveTradePawn, newResolveTradePawn);

            // adjusted price calculation
            var vanillaPriceFor = typeof(Tradeable).GetMethod("PriceFor", BindingFlags.Instance | BindingFlags.Public);
            var newPriceFor = typeof(RA_Tradeable).GetMethod("PriceFor", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaPriceFor, newPriceFor);

            // make trade sessions individual per trade center
            var vanillaSetupWith = typeof(TradeSession).GetMethod("SetupWith", BindingFlags.Static | BindingFlags.Public);
            var newSetupWith = typeof(RA_TradeSession).GetMethod("SetupWith", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaSetupWith, newSetupWith);

            // assign Pawn_TraderTracker based on pawn caravan role, not mindState.wantsToTradeWithColony
            var vanillaAddAndRemoveDynamicComponents = typeof(PawnComponentsUtility).GetMethod("AddAndRemoveDynamicComponents", BindingFlags.Static | BindingFlags.Public);
            var newAddAndRemoveDynamicComponents = typeof(RA_PawnComponentsUtility).GetMethod("AddAndRemoveDynamicComponents", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaAddAndRemoveDynamicComponents, newAddAndRemoveDynamicComponents);

            #endregion

            #region CONTAINERS

            // allows to pass checks for item stacking in cells with containers
            var vanillaNoStorageBlockersIn = typeof(StoreUtility).GetMethod("NoStorageBlockersIn", BindingFlags.Static | BindingFlags.NonPublic);
            var newNoStorageBlockersIn = typeof(RA_StoreUtility).GetMethod("NoStorageBlockersIn", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaNoStorageBlockersIn, newNoStorageBlockersIn);

            // allows to haul items to container stacks
            var vanillaHaulMaxNumToCellJob = typeof(HaulAIUtility).GetMethod("HaulMaxNumToCellJob", BindingFlags.Static | BindingFlags.Public);
            var newHaulMaxNumToCellJob = typeof(RA_HaulAIUtility).GetMethod("HaulMaxNumToCellJob", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaHaulMaxNumToCellJob, newHaulMaxNumToCellJob);

            // allows to place items to container stacks
            var vanillaTryPlaceDirect = typeof(GenPlace).GetMethod("TryPlaceDirect", BindingFlags.Static | BindingFlags.NonPublic);
            var newTryPlaceDirect = typeof(RA_GenPlace).GetMethod("TryPlaceDirect", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaTryPlaceDirect, newTryPlaceDirect);

            // allows to place items to container stacks
            var vanillaCompTickRare = typeof(CompRottable).GetMethod("CompTickRare", BindingFlags.Instance | BindingFlags.Public);
            var newCompTickRare = typeof(RA_CompRottable).GetMethod("CompTickRare", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaCompTickRare, newCompTickRare);

            #endregion

            #region MAINMENU

            // detour RimWorld.MainMenuDrawer.MainMenuOnGUI
            var vanillaDoMainMenuButtons = typeof(MainMenuDrawer).GetMethod("MainMenuOnGUI", BindingFlags.Static | BindingFlags.Public);
            var newDoMainMenuButtons = typeof(RA_MainMenuDrawer).GetMethod("MainMenuOnGUI", BindingFlags.Static | BindingFlags.Public);
            TryDetourFromTo(vanillaDoMainMenuButtons, newDoMainMenuButtons);

            // detour RimWorld.UI_BackgroundMain.BackgroundOnGUI
            var vanillaBackgroundOnGUI = typeof(UI_BackgroundMain).GetMethod("BackgroundOnGUI", BindingFlags.Instance | BindingFlags.Public);
            var newBackgroundOnGUI = typeof(RA_UI_BackgroundMain).GetMethod("BackgroundOnGUI", BindingFlags.Instance | BindingFlags.Public);
            TryDetourFromTo(vanillaBackgroundOnGUI, newBackgroundOnGUI);

            #endregion

            #region DIPLOMACY

            // allow AffectGoodwillWith with hidden factions; make factions hostile when relations are negative
            // detour RimWorld.Faction.AffectGoodwillWith
            //var vanillaAffectGoodwillWith = typeof(Faction).GetMethod("AffectGoodwillWith", BindingFlags.Instance | BindingFlags.Public);
            //var newAffectGoodwillWith = typeof(RA_Faction).GetMethod("AffectGoodwillWith", BindingFlags.Instance | BindingFlags.Public);
            //TryDetourFromTo(vanillaAffectGoodwillWith, newAffectGoodwillWith);

            //// allow Notify_MemberCaptured with hidden factions
            //// detour RimWorld.Faction.Notify_MemberCaptured
            //var vanillaNotify_MemberCaptured = typeof(Faction).GetMethod("Notify_MemberCaptured", BindingFlags.Instance | BindingFlags.Public);
            //var newNotify_MemberCaptured = typeof(RA_Faction).GetMethod("Notify_MemberCaptured", BindingFlags.Instance | BindingFlags.Public);
            //TryDetourFromTo(vanillaNotify_MemberCaptured, newNotify_MemberCaptured);

            //// allow Notify_MemberTookDamage with hidden factions
            //// detour RimWorld.Faction.Notify_MemberTookDamage
            //var vanillaNotify_MemberTookDamage = typeof(Faction).GetMethod("Notify_MemberTookDamage", BindingFlags.Instance | BindingFlags.Public);
            //var newNotify_MemberTookDamage = typeof(RA_Faction).GetMethod("Notify_MemberTookDamage", BindingFlags.Instance | BindingFlags.Public);
            //TryDetourFromTo(vanillaNotify_MemberTookDamage, newNotify_MemberTookDamage);

            //// allow Notify_FactionTick with hidden factions
            //// detour RimWorld.Faction.Notify_FactionTick
            //var vanillaNotify_FactionTick = typeof(Faction).GetMethod("Notify_FactionTick", BindingFlags.Instance | BindingFlags.Public);
            //var newNotify_FactionTick = typeof(RA_Faction).GetMethod("Notify_FactionTick", BindingFlags.Instance | BindingFlags.Public);
            //TryDetourFromTo(vanillaNotify_FactionTick, newNotify_FactionTick);

            // allow SetHostileTo with hidden factions
            // detour RimWorld.Faction.SetHostileTo
            //var vanillaSetHostileTo = typeof(Faction).GetMethod("SetHostileTo", BindingFlags.Instance | BindingFlags.Public);
            //var newSetHostileTo = typeof(RA_Faction).GetMethod("SetHostileTo", BindingFlags.Instance | BindingFlags.Public);
            //TryDetourFromTo(vanillaSetHostileTo, newSetHostileTo);

            // removes hidden factions restriction from GenerateFactionsIntoCurrentWorld
            // detour RimWorld.FactionGenerator.GenerateFactionsIntoCurrentWorld
            //var vanillaGenerateFactionsIntoCurrentWorld = typeof(FactionGenerator).GetMethod("GenerateFactionsIntoCurrentWorld", BindingFlags.Static | BindingFlags.Public);
            //var newGenerateFactionsIntoCurrentWorld = typeof(RA_FactionGenerator).GetMethod("GenerateFactionsIntoCurrentWorld", BindingFlags.Static | BindingFlags.Public);
            //TryDetourFromTo(vanillaGenerateFactionsIntoCurrentWorld, newGenerateFactionsIntoCurrentWorld);

            //// detour Verse.Pawn.ExitMap
            //var vanillaExitMap = typeof(Pawn).GetMethod("ExitMap", BindingFlags.Instance | BindingFlags.Public);
            //var newExitMap = typeof(RA_Pawn).GetMethod("ExitMap", BindingFlags.Instance | BindingFlags.Public);
            //TryDetourFromTo(vanillaExitMap, newExitMap);

            #endregion

            #region COMBAT_REALISM

            // Detour VerbsTick
            TryDetourFromTo(typeof(VerbTracker).GetMethod("VerbsTick", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_VerbTracker).GetMethod("VerbsTick", BindingFlags.Static | BindingFlags.NonPublic));

            // Detour TooltipUtility
            TryDetourFromTo(typeof(TooltipUtility).GetMethod("ShotCalculationTipString", BindingFlags.Static | BindingFlags.Public),
                typeof(CR.Detours_TooltipUtility).GetMethod("ShotCalculationTipString", BindingFlags.Static | BindingFlags.NonPublic));

            // Detour FloatMenuMaker
            TryDetourFromTo(typeof(FloatMenuMaker).GetMethod("ChoicesAtFor", BindingFlags.Static | BindingFlags.Public),
                typeof(CR.Detours_FloatMenuMaker).GetMethod("ChoicesAtFor", BindingFlags.Static | BindingFlags.NonPublic));

            // *************************************
            // *** Detour Inventory methods ***
            // *************************************

            // ThingContainer

            MethodInfo tryAddSource = typeof(ThingContainer).GetMethod("TryAdd", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Thing) }, null);
            TryDetourFromTo(tryAddSource, typeof(CR.Detours_ThingContainer).GetMethod("TryAdd", BindingFlags.Static | BindingFlags.NonPublic));

            MethodInfo tryDrop2Source = typeof(ThingContainer).GetMethod("TryDrop",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new Type[] { typeof(Thing), typeof(IntVec3), typeof(ThingPlaceMode), typeof(int), typeof(Thing).MakeByRefType() },
                null);

            MethodInfo tryDrop2Dest = typeof(CR.Detours_ThingContainer).GetMethod("TryDrop",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(ThingContainer), typeof(Thing), typeof(IntVec3), typeof(ThingPlaceMode), typeof(int), typeof(Thing).MakeByRefType() },
                null);

            TryDetourFromTo(tryDrop2Source, tryDrop2Dest);

            TryDetourFromTo(typeof(ThingContainer).GetMethod("Get", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_ThingContainer).GetMethod("Get", BindingFlags.Static | BindingFlags.NonPublic));

            TryDetourFromTo(typeof(ThingContainer).GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_ThingContainer).GetMethod("Remove", BindingFlags.Static | BindingFlags.NonPublic));

            // Pawn_ApparelTracker

            MethodInfo tryDrop3Source = typeof(Pawn_ApparelTracker).GetMethod("TryDrop",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new Type[] { typeof(Apparel), typeof(Apparel).MakeByRefType(), typeof(IntVec3), typeof(bool) },
                null);

            MethodInfo tryDrop3Dest = typeof(CR.Detours_Pawn_ApparelTracker).GetMethod("TryDrop",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(Pawn_ApparelTracker), typeof(Apparel), typeof(Apparel).MakeByRefType(), typeof(IntVec3), typeof(bool) },
                null);

            TryDetourFromTo(tryDrop3Source, tryDrop3Dest);

            TryDetourFromTo(typeof(Pawn_ApparelTracker).GetMethod("Wear", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_Pawn_ApparelTracker).GetMethod("Wear", BindingFlags.Static | BindingFlags.NonPublic));

            TryDetourFromTo(typeof(Pawn_ApparelTracker).GetMethod("Notify_WornApparelDestroyed", BindingFlags.Instance | BindingFlags.NonPublic),
                typeof(CR.Detours_Pawn_ApparelTracker).GetMethod("Notify_WornApparelDestroyed", BindingFlags.Static | BindingFlags.NonPublic));

            // Pawn_EquipmentTracker

            TryDetourFromTo(typeof(Pawn_EquipmentTracker).GetMethod("AddEquipment", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_Pawn_EquipmentTracker).GetMethod("AddEquipment", BindingFlags.Static | BindingFlags.NonPublic));

            TryDetourFromTo(typeof(Pawn_EquipmentTracker).GetMethod("Notify_PrimaryDestroyed", BindingFlags.Instance | BindingFlags.NonPublic),
                typeof(CR.Detours_Pawn_EquipmentTracker).GetMethod("Notify_PrimaryDestroyed", BindingFlags.Static | BindingFlags.NonPublic));

            TryDetourFromTo(typeof(Pawn_EquipmentTracker).GetMethod("TryDropEquipment", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_Pawn_EquipmentTracker).GetMethod("TryDropEquipment", BindingFlags.Static | BindingFlags.NonPublic));

            TryDetourFromTo(typeof(Pawn_EquipmentTracker).GetMethod("TryTransferEquipmentToContainer", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_Pawn_EquipmentTracker).GetMethod("TryTransferEquipmentToContainer", BindingFlags.Static | BindingFlags.NonPublic));

            TryDetourFromTo(typeof(Pawn_EquipmentTracker).GetMethod("TryStartAttack", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_Pawn_EquipmentTracker).GetMethod("TryStartAttack", BindingFlags.Static | BindingFlags.NonPublic));

            // *************************************
            // *** Detour inventory-related methods ***
            // *************************************

            // WorkGiver_InteractAnimal
            TryDetourFromTo(typeof(WorkGiver_InteractAnimal).GetMethod("TakeFoodForAnimalInteractJob", BindingFlags.Instance | BindingFlags.NonPublic),
                typeof(CR.Detours_WorkGiver_InteractAnimal).GetMethod("TakeFoodForAnimalInteractJob", BindingFlags.Static | BindingFlags.NonPublic));

            // WorkGiver_HunterHunt
            TryDetourFromTo(typeof(WorkGiver_HunterHunt).GetMethod("HasHuntingWeapon", BindingFlags.Static | BindingFlags.Public),
                typeof(CR.Detours_WorkGiver_HunterHunt).GetMethod("HasHuntingWeapon", BindingFlags.Static | BindingFlags.NonPublic));

            // Tradeable
            TryDetourFromTo(typeof(Tradeable).GetMethod("PriceFor", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_Tradeable).GetMethod("PriceFor", BindingFlags.Static | BindingFlags.NonPublic));

            // TradeDeal
            TryDetourFromTo(typeof(TradeDeal).GetMethod("UpdateCurrencyCount", BindingFlags.Instance | BindingFlags.Public),
                typeof(CR.Detours_TradeDeal).GetMethod("UpdateCurrencyCount", BindingFlags.Static | BindingFlags.NonPublic));

            CR.ITabInjector.Inject();

            #endregion
        }
    }
}